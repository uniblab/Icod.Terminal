namespace Icod.Terminal.Tests.Session;

using System.IO;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T105 terminal-progress lifecycle, invalidation, failure, and retry semantics.
/// </summary>
public sealed class TerminalProgressLifecycleFailureTests {
	[Fact]
	public async Task SuspendClearsAndResumeRestoresCurrentProgress() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync(
			3,
			10
		);

		await session.ProgressManager.SuspendAsync();
		await session.ProgressManager.ReenterAsync();

		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 30 ),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 1 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 30 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task ReleasingAllOwnersWhileSuspendedPreventsReentry() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.SetIndeterminateAsync();

		await session.ProgressManager.SuspendAsync();
		await progress.DisposeAsync();
		int writesBeforeResume = output.WriteCount;
		await session.ProgressManager.ReenterAsync();

		Assert.Equal( 2, writesBeforeResume );
		Assert.Equal( writesBeforeResume, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 1 )
		);
	}

	[Fact]
	public async Task SessionInvalidationRecoversPreviousLogicalStateBeforeNextReport() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync(
			2,
			10
		);

		session.InvalidateState();
		await progress.ReportAsync(
			3,
			10
		);

		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 20 ),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 20 ),
			output.GetWrite( 1 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 30 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task FailedFinalLeaseClearIsRetryable() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync(
			1,
			2
		);
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => progress.DisposeAsync().AsTask()
		);

		await progress.DisposeAsync();
		int writesAfterRetry = output.WriteCount;
		await progress.DisposeAsync();

		Assert.Equal( 3, writesAfterRetry );
		Assert.Equal( writesAfterRetry, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Normal, 50 ),
			output.GetWrite( 1 )
		);
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( 2 )
		);
	}

	[Fact]
	public async Task FailedManagerCloseRetainsCleanupForRetry() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalProgressManager manager = session.ProgressManager;
		long owner = await manager.AcquireAsync( CancellationToken.None );
		await manager.ReportAsync(
			owner,
			TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Attention,
				3,
				4
			),
			CancellationToken.None
		);
		output.FailNextWrite();

		await Assert.ThrowsAsync<IOException>(
			() => manager.CloseAsync().AsTask()
		);

		await manager.CloseAsync();
		Assert.Equal(
			OscWriter.EncodeOsc9ProgressFrame( Osc9ProgressState.Clear, 0 ),
			output.GetWrite( output.WriteCount - 1 )
		);
	}

	[Fact]
	public async Task ReentryAndCleanupDoubleFailureAreAggregated() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalProgressLease progress = await session.AcquireProgressAsync();
		await progress.ReportAsync(
			4,
			10
		);
		await session.ProgressManager.SuspendAsync();
		output.FailNextWrites( 2 );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => session.ProgressManager.ReenterAsync().AsTask()
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
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
		private readonly List<byte[]> writes = [];
		private int failuresRemaining;

		internal int WriteCount {
			get {
				return this.writes.Count;
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			return this.writes[ index ].ToArray();
		}

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
				throw new IOException( "Injected terminal-progress output failure." );
			}
			this.writes.Add( buffer.ToArray() );
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
				"Size is not required by terminal-progress lifecycle tests."
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
