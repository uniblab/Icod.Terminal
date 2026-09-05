namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T56 semantic OSC 52 clipboard-write API without touching the host terminal.
/// </summary>
public sealed class TerminalSessionClipboardTests {
	[Theory]
	[InlineData( 0, "hello", "1B5D35323B633B614756736247383D1B5C" )]
	[InlineData( 1, "hello", "1B5D35323B703B614756736247383D1B5C" )]
	[InlineData( 2, "hello", "1B5D35323B713B614756736247383D1B5C" )]
	[InlineData( 3, "hello", "1B5D35323B733B614756736247383D1B5C" )]
	public async Task TextWriteEmitsExpectedOsc52Frame(
		int selectionValue,
		string value,
		string expectedHex
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.WriteClipboardAsync(
			(TerminalClipboardSelection)selectionValue,
			value
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task BinaryWritePreservesExactBytesBeforeBase64Encoding() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		byte[] payload = [ 0x00, 0x1B, 0x5C, 0xFF ];

		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			payload
		);

		Assert.Equal(
			Convert.FromHexString( "1B5D35323B633B414274632F773D3D1B5C" ),
			output.Bytes.ToArray()
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public async Task EmptyPayloadUsesExplicitSetEmptySemantic(
		int selectionValue
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.WriteClipboardAsync(
			(TerminalClipboardSelection)selectionValue,
			ReadOnlyMemory<byte>.Empty
		);

		byte selectionByte = selectionValue switch {
			0 => (byte)'c',
			1 => (byte)'p',
			2 => (byte)'q',
			3 => (byte)'s',
			_ => throw new InvalidOperationException()
		};
		Assert.Equal(
			new byte[] {
				0x1B,
				(byte)']',
				(byte)'5',
				(byte)'2',
				(byte)';',
				selectionByte,
				(byte)';',
				0x1B,
				(byte)'\\'
			},
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task TextWriteUsesStrictUtf8IndependentOfApplicationEncoding() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ApplicationEncoding = Encoding.Unicode
			}
		);

		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			"é"
		);

		Assert.Equal(
			Convert.FromHexString( "1B5D35323B633B77366B3D1B5C" ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task ExactMaximumBinaryPayloadIsAccepted() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		byte[] payload = new byte[ TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes ];

		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			payload
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			TerminalOsc52PayloadCodec.GetWriteFrameLength( payload.Length ),
			output.Bytes.Count
		);
	}

	[Fact]
	public async Task OneByteOverMaximumWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);
		byte[] payload = new byte[
			TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes + 1
		];

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.WriteClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				payload
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task UnknownSelectionWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.WriteClipboardAsync(
				(TerminalClipboardSelection)int.MaxValue,
				"value"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
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
			() => session.WriteClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				"value"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
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
			() => session.WriteClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				"value",
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task OutputFailurePropagates() {
		FailingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => session.WriteClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				"value"
			).AsTask()
		);

		Assert.Equal( "Synthetic output failure.", exception.Message );
		Assert.Equal( 1, output.WriteCount );
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
