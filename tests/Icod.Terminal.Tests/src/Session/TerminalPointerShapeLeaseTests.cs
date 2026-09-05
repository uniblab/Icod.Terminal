namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T114 public OSC 22 pointer-shape setter, resetter, and lease API.
/// </summary>
public sealed class TerminalPointerShapeLeaseTests {
	[Fact]
	public async Task ExplicitSetAndResetEmitDistinctCanonicalFrames() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SetPointerShapeAsync( TerminalPointerShape.Default );
		await session.ResetPointerShapeAsync();

		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "default" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 1 ]
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task NestedPublicLeasesRestoreNewestRemainingOwner() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeLease outer = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Pointer
		);
		TerminalPointerShapeLease inner = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Wait
		);

		Assert.Equal( TerminalPointerShape.Pointer, outer.Shape );
		Assert.Equal( TerminalPointerShape.Wait, inner.Shape );
		await inner.DisposeAsync();
		await outer.DisposeAsync();

		Assert.Equal( 4, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			output.Writes[ 2 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 3 ]
		);
	}

	[Fact]
	public async Task OutOfOrderPublicLeaseDisposalIsSafe() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeLease first = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Text
		);
		TerminalPointerShapeLease second = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Crosshair
		);

		await first.DisposeAsync();
		Assert.Equal( 2, output.Writes.Count );
		await second.DisposeAsync();

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task UnscopedMutationIsRejectedWhileLeaseIsActive() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalPointerShapeLease lease = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Grab
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.SetPointerShapeAsync(
				TerminalPointerShape.Help
			).AsTask()
		);
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.ResetPointerShapeAsync().AsTask()
		);

		Assert.Single( output.Writes );
	}

	[Fact]
	public async Task SuccessfulLeaseDisposalIsIdempotent() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeLease lease = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Help
		);

		await lease.DisposeAsync();
		int writesAfterRelease = output.Writes.Count;
		await lease.DisposeAsync();

		Assert.Equal( writesAfterRelease, output.Writes.Count );
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
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x00000007u
		);

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
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
				"Size is not required by pointer-shape lease tests."
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
