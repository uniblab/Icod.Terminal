namespace Icod.Terminal.Tests.Session;

using System.IO;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T115 pointer-shape lifecycle, invalidation, failure, and retry semantics.
/// </summary>
public sealed class TerminalPointerShapeLifecycleFailureTests {
	[Fact]
	public async Task FailedOutermostAcquisitionRestoresTerminalPolicyAndAllowsRetry() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => session.AcquirePointerShapeAsync(
				TerminalPointerShape.Pointer
			).AsTask()
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 0 ]
		);

		await using TerminalPointerShapeLease retry =
			await session.AcquirePointerShapeAsync(
				TerminalPointerShape.Text
			);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "text" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task FailedNestedAcquisitionRestoresCurrentOwner() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalPointerShapeLease outer =
			await session.AcquirePointerShapeAsync(
				TerminalPointerShape.Pointer
			);
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => session.AcquirePointerShapeAsync(
				TerminalPointerShape.Wait
			).AsTask()
		);

		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task AcquisitionAndRestorationDoubleFailureAreAggregated() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		output.FailNextWrites( 2 );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => session.AcquirePointerShapeAsync(
				TerminalPointerShape.Help
			).AsTask()
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task InvalidationRecoversControllerBeforeNestedAcquisition() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeLease outer = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Pointer
		);
		session.InvalidateState();

		TerminalPointerShapeLease inner = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Wait
		);

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "pointer" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "wait" ),
			output.Writes[ 2 ]
		);

		await inner.DisposeAsync();
		await outer.DisposeAsync();
	}

	[Fact]
	public async Task FailedFinalLeaseResetIsRetryable() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeLease lease = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Crosshair
		);
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => lease.DisposeAsync().AsTask()
		);

		await lease.DisposeAsync();
		int writesAfterRetry = output.Writes.Count;
		await lease.DisposeAsync();

		Assert.Equal( 3, writesAfterRetry );
		Assert.Equal( writesAfterRetry, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "crosshair" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task FailedManagerCloseRetainsCleanupForRetry() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;
		await manager.AcquireAsync(
			TerminalPointerShape.Grab,
			CancellationToken.None
		);
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => manager.CloseAsync().AsTask()
		);

		await manager.CloseAsync();
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ ^1 ]
		);
	}

	[Fact]
	public async Task ReentryAndCleanupDoubleFailureAreAggregated() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeManager manager = session.PointerShapeManager;
		await manager.AcquireAsync(
			TerminalPointerShape.EastWestResize,
			CancellationToken.None
		);
		await manager.SuspendAsync();
		output.FailNextWrites( 2 );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => manager.ReenterAsync().AsTask()
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
	}

	[Fact]
	public async Task FailedExplicitSetRestoresTerminalPolicyAndAllowsRetry() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => session.SetPointerShapeAsync(
				TerminalPointerShape.Pointer
			).AsTask()
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 0 ]
		);

		await session.SetPointerShapeAsync(
			TerminalPointerShape.Text
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "text" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task FailedExplicitMutationAndCleanupRecoverOnNextSet() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		output.FailNextWrites( 2 );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => session.SetPointerShapeAsync(
				TerminalPointerShape.ZoomIn
			).AsTask()
		);
		Assert.Equal( 2, exception.InnerExceptions.Count );

		await session.SetPointerShapeAsync(
			TerminalPointerShape.ZoomOut
		);
		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			OscWriter.EncodeOsc22PointerShapeFrame( "zoom-out" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task LeaseDisposalAfterSessionDisposalEmitsNothing() {
		RecordingOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalPointerShapeLease lease = await session.AcquirePointerShapeAsync(
			TerminalPointerShape.Progress
		);

		await session.DisposeAsync();
		int writes = output.Writes.Count;
		await lease.DisposeAsync();

		Assert.Equal( writes, output.Writes.Count );
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
		private int failuresRemaining;

		internal List<byte[]> Writes {
			get;
		} = [];

		internal void FailNextWrite() {
			this.FailNextWrites( 1 );
		}

		internal void FailNextWrites(
			int count
		) {
			if ( 0 >= count ) {
				throw new ArgumentOutOfRangeException( nameof( count ) );
			}
			Volatile.Write( ref this.failuresRemaining, count );
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( 0 < Volatile.Read( ref this.failuresRemaining ) ) {
				Interlocked.Decrement( ref this.failuresRemaining );
				throw new IOException( "Injected terminal pointer-shape output failure." );
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
				"Size is not required by pointer-shape lifecycle tests."
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
