namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T113 pointer-shape ownership, nesting, restoration, and lifecycle behavior.
/// </summary>
public sealed class TerminalPointerShapeManagerTests {
	[Fact]
	public async Task FirstOwnerEmitsRequestedShape() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;

		long owner = await manager.AcquireAsync(
			TerminalPointerShape.Pointer,
			CancellationToken.None
		);

		Assert.True( 0 < owner );
		Assert.Single( output.Writes );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			output.Writes[ 0 ]
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task NestedControllingReleaseRestoresPreviousOwner() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;
		long outer = await manager.AcquireAsync(
			TerminalPointerShape.Pointer,
			CancellationToken.None
		);
		long inner = await manager.AcquireAsync(
			TerminalPointerShape.Wait,
			CancellationToken.None
		);

		await manager.ReleaseAsync( inner );

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			output.Writes[ 2 ]
		);

		await manager.ReleaseAsync( outer );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 3 ]
		);
	}

	[Fact]
	public async Task OutOfOrderNonControllingReleaseIsSilent() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;
		long first = await manager.AcquireAsync(
			TerminalPointerShape.Text,
			CancellationToken.None
		);
		long second = await manager.AcquireAsync(
			TerminalPointerShape.Crosshair,
			CancellationToken.None
		);

		await manager.ReleaseAsync( first );
		Assert.Equal( 2, output.Writes.Count );

		await manager.ReleaseAsync( second );
		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task SuspendResetsAndResumeRestoresCurrentOwner() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;
		long owner = await manager.AcquireAsync(
			TerminalPointerShape.EastWestResize,
			CancellationToken.None
		);

		await manager.SuspendAsync();
		await manager.ReenterAsync();

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "ew-resize" ),
			output.Writes[ 2 ]
		);

		await manager.ReleaseAsync( owner );
	}

	[Fact]
	public async Task ReleasingAllOwnersWhileSuspendedPreventsReentry() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;
		long owner = await manager.AcquireAsync(
			TerminalPointerShape.Grab,
			CancellationToken.None
		);

		await manager.SuspendAsync();
		await manager.ReleaseAsync( owner );
		int writesBeforeResume = output.Writes.Count;
		await manager.ReenterAsync();

		Assert.Equal( 2, writesBeforeResume );
		Assert.Equal( writesBeforeResume, output.Writes.Count );
	}

	[Fact]
	public async Task RedirectedOutputRejectsAcquisitionWithoutEmission() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			outputIsTerminal: false
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.PointerShapeManager.AcquireAsync(
				TerminalPointerShape.Pointer,
				CancellationToken.None
			).AsTask()
		);
		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task SessionDisposalResetsActivePointerShape() {
		RecordingOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		await session.PointerShapeManager.AcquireAsync(
			TerminalPointerShape.Help,
			CancellationToken.None
		);

		await session.DisposeAsync();

		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ ^1 ]
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingOutput output,
		bool outputIsTerminal = true
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider( outputIsTerminal ),
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

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Writes.Add( buffer.ToArray() );
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
		private readonly bool outputIsTerminal;
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x00000007u
		);

		internal RecordingTerminalControlProvider(
			bool outputIsTerminal
		) {
			this.outputIsTerminal = outputIsTerminal;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = this.outputIsTerminal
				|| 1 != endpoint.FileDescriptor;
			if ( !isTerminal ) {
				return TerminalControlResult<TerminalEndpointObservation>.Available(
					new TerminalEndpointObservation(
						false,
						null,
						null,
						TerminalControlCapabilities.None
					)
				);
			}

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.WindowsConsole,
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
				"Size is not required by pointer-shape manager tests."
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
