namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies public OSC 104 palette reset behavior through TerminalSession.
/// </summary>
public sealed class TerminalPaletteResetTests {
	[Fact]
	public async Task PublicResetShapesEmitCanonicalFramesWithoutFlush() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.ResetPaletteColorAsync( 7 );
		await session.ResetPaletteColorsAsync(
			new byte[] { 3, 255, 0 }
		);
		await session.ResetPaletteAsync();

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104;7\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104;3;255;0\u001b\\" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104\u001b\\" ),
			output.Writes[ 2 ]
		);
		Assert.Equal( 0, output.FlushCount );
		Assert.All(
			output.WriteCancellationTokens,
			token => Assert.False( token.CanBeCanceled )
		);
	}

	[Fact]
	public async Task InvalidBulkResetWritesNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.ResetPaletteColorsAsync(
				new byte[] { 4, 4 }
			).AsTask()
		);
		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task PreCancelledResetWritesNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.ResetPaletteAsync(
				cancellation.Token
			).AsTask()
		);
		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task ResetDoesNotQueryOrReplayObservedColor() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SetPaletteColorAsync(
			5,
			new TerminalColor( 0x1234, 0x5678, 0x9abc )
		);
		await session.ResetPaletteColorAsync( 5 );

		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104;5\u001b\\" ),
			output.Writes[ 1 ]
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class EmptyInput : ITerminalInput {
		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			_ = buffer;
			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}
	}

	private sealed class RecordingOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
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

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by palette reset tests."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
