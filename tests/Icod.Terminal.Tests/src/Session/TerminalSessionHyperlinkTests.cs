namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T47 semantic OSC 8 hyperlink operation without touching the host terminal.
/// </summary>
public sealed class TerminalSessionHyperlinkTests {
	[Theory]
	[InlineData(
		"linked",
		"https://example.com/path",
		null,
		"1B5D383B3B68747470733A2F2F6578616D706C652E636F6D2F706174681B5C6C696E6B65641B5D383B3B1B5C"
	)]
	[InlineData(
		"docs",
		"https://example.com/a%2fb?q=1#part",
		"manual-1",
		"1B5D383B69643D6D616E75616C2D313B68747470733A2F2F6578616D706C652E636F6D2F61253246623F713D3123706172741B5C646F63731B5D383B3B1B5C"
	)]
	public async Task WriteHyperlinkEmitsBeginTextAndEnd(
		string value,
		string uri,
		string? identifier,
		string expectedHex
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.WriteHyperlinkAsync(
			value,
			uri,
			identifier
		);

		Assert.Equal( 3, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task ApplicationTextUsesSessionEncoding() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.WriteHyperlinkAsync(
			"café 猫",
			"https://example.com/"
		);

		Assert.Contains(
			System.Text.Encoding.UTF8.GetBytes( "café 猫" ),
			output.Writes
		);
	}

	[Fact]
	public async Task InvalidUriWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"relative/path"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task InvalidIdentifierWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/",
				"bad;id"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task RedirectedOutputIsRejectedWithoutWriting() {
		RecordingTerminalOutput output = new();
		RecordingTerminalControlProvider provider = new() {
			OutputIsTerminal = false
		};
		await using TerminalSession session = await OpenSessionAsync(
			provider,
			output,
			new TerminalSessionOptions {
				RequireInteractiveOutput = false,
				TerminalOverride = TerminalProfiles.Dumb
			}
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task CancellationBeforeTransmissionWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/",
				null,
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task BeginFailurePropagatesWithoutCleanupWrite() {
		FailingAtWriteTerminalOutput output = new( 1 );
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/"
			).AsTask()
		);

		Assert.Equal( "Synthetic output failure 1.", exception.Message );
		Assert.Equal( 1, output.WriteCount );
	}

	[Fact]
	public async Task TextFailureAttemptsCloseThenPropagatesOriginalFailure() {
		FailingAtWriteTerminalOutput output = new( 2 );
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/"
			).AsTask()
		);

		Assert.Equal( "Synthetic output failure 2.", exception.Message );
		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task TextAndCleanupFailureAreBothReported() {
		FailingAtWriteTerminalOutput output = new(
			2,
			3
		);
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/"
			).AsTask()
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
		Assert.Equal( 3, output.WriteCount );
	}

	[Fact]
	public async Task EndFailurePropagates() {
		FailingAtWriteTerminalOutput output = new( 3 );
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/"
			).AsTask()
		);

		Assert.Equal( "Synthetic output failure 3.", exception.Message );
		Assert.Equal( 3, output.WriteCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalControlProvider provider,
		ITerminalOutput output,
		TerminalSessionOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			options
			?? new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte> Bytes {
			get;
		} = [];

		internal List<byte[]> Writes {
			get;
		} = [];

		internal int WriteCount {
			get;
			private set;
		}

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			byte[] bytes = buffer.ToArray();
			this.Writes.Add( bytes );
			this.Bytes.AddRange( bytes );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FailingAtWriteTerminalOutput : ITerminalOutput {
		private readonly HashSet<int> failingWrites;

		internal FailingAtWriteTerminalOutput(
			params int[] failingWrites
		) {
			ArgumentNullException.ThrowIfNull( failingWrites );
			this.failingWrites = [ .. failingWrites ];
		}

		internal List<byte[]> Writes {
			get;
		} = [];

		internal int WriteCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			if ( this.failingWrites.Contains( this.WriteCount ) ) {
				return ValueTask.FromException(
					new IOException(
						$"Synthetic output failure {this.WriteCount}."
					)
				);
			}

			this.Writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0x0002UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);

		internal bool OutputIsTerminal {
			get;
			init;
		} = true;

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = !ReferenceEquals(
				endpoint,
				TerminalEndpoint.StandardOutput
			)
				|| this.OutputIsTerminal;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					isTerminal ? TerminalPlatformKind.PosixTermios : null,
					isTerminal
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}

			return TerminalControlMutationResult.Success();
		}
	}
}
