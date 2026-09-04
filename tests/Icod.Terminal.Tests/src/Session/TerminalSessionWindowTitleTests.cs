namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T33 OSC 2 semantic window-title operation without touching the host terminal.
/// </summary>
public sealed class TerminalSessionWindowTitleTests {
	[Theory]
	[InlineData( "", "1B5D323B1B5C" )]
	[InlineData( "window", "1B5D323B77696E646F771B5C" )]
	public async Task SetWindowTitleEmitsExpectedOscTwoFrame(
		string value,
		string expectedHex
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.SetWindowTitleAsync( value );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task SetWindowTitleEncodesMultilingualTextAsUtf8() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await session.SetWindowTitleAsync( "éλ猫" );

		Assert.Equal(
			Convert.FromHexString( "1B5D323BC3A9CEBBE78CAB1B5C" ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task InvalidWindowTitleWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.SetWindowTitleAsync( "bad\u001bwindow" ).AsTask()
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
			() => session.SetWindowTitleAsync( "window" ).AsTask()
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
			() => session.SetWindowTitleAsync(
				"window",
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
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
