namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T83 semantic DECSCUSR cursor-style setter without touching the host terminal.
/// </summary>
public sealed class TerminalSessionCursorStyleTests {
	[Theory]
	[InlineData( TerminalCursorStyle.BlinkingBlock, "1B5B312071" )]
	[InlineData( TerminalCursorStyle.SteadyBlock, "1B5B322071" )]
	[InlineData( TerminalCursorStyle.BlinkingUnderline, "1B5B332071" )]
	[InlineData( TerminalCursorStyle.SteadyUnderline, "1B5B342071" )]
	[InlineData( TerminalCursorStyle.BlinkingBar, "1B5B352071" )]
	[InlineData( TerminalCursorStyle.SteadyBar, "1B5B362071" )]
	public async Task SetCursorStyleEmitsExpectedFrame(
		TerminalCursorStyle style,
		string expectedHex
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.SetCursorStyleAsync( style );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task InvalidStyleWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.SetCursorStyleAsync(
				(TerminalCursorStyle)int.MaxValue
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
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
			() => session.SetCursorStyleAsync(
				TerminalCursorStyle.SteadyBlock
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Empty( output.Bytes );
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
			() => session.SetCursorStyleAsync(
				TerminalCursorStyle.SteadyUnderline,
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task OutputFailurePropagatesWithoutFlush() {
		FailingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => session.SetCursorStyleAsync(
				TerminalCursorStyle.BlinkingBar
			).AsTask()
		);

		Assert.Equal( "Synthetic output failure.", exception.Message );
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task CursorStyleComposesInOrderWithApplicationText() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.WriteTextAsync( "A" );
		await session.SetCursorStyleAsync( TerminalCursorStyle.SteadyBar );
		await session.WriteTextAsync( "B" );

		Assert.Equal( 3, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Equal(
			Convert.FromHexString( "411B5B36207142" ),
			output.Bytes.ToArray()
		);
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
			this.Bytes.AddRange( buffer.ToArray() );
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

	private sealed class FailingTerminalOutput : ITerminalOutput {
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
			return ValueTask.FromException(
				new IOException( "Synthetic output failure." )
			);
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
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
			TerminalPlatformKind? platform = isTerminal
				? TerminalPlatformKind.PosixTermios
				: null;
			TerminalControlCapabilities capabilities = isTerminal
				? TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
				: TerminalControlCapabilities.None;

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					platform,
					capabilities
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
