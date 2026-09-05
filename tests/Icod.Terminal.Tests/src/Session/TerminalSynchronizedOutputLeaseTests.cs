namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the public T93 synchronized-output lease API.
/// </summary>
public sealed class TerminalSynchronizedOutputLeaseTests {
	[Fact]
	public async Task PublicNestedLeasesUseOneBeginAndOneFinalEnd() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalSynchronizedOutputLease outer =
			await session.AcquireSynchronizedOutputAsync();
		TerminalSynchronizedOutputLease inner =
			await session.AcquireSynchronizedOutputAsync();

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			output.GetWrite( 0 )
		);
		Assert.Equal( 0, output.FlushCount );

		await inner.DisposeAsync();
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		await outer.DisposeAsync();
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( 1 )
		);
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task PublicLeasesMayBeDisposedOutOfOrder() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalSynchronizedOutputLease first =
			await session.AcquireSynchronizedOutputAsync();
		TerminalSynchronizedOutputLease second =
			await session.AcquireSynchronizedOutputAsync();
		TerminalSynchronizedOutputLease third =
			await session.AcquireSynchronizedOutputAsync();

		await first.DisposeAsync();
		await third.DisposeAsync();
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		await second.DisposeAsync();
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task LeaseDisposalIsIdempotentAfterSuccessfulRelease() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalSynchronizedOutputLease lease =
			await session.AcquireSynchronizedOutputAsync();
		await lease.DisposeAsync();
		int writes = output.WriteCount;
		int flushes = output.FlushCount;

		await lease.DisposeAsync();

		Assert.Equal( writes, output.WriteCount );
		Assert.Equal( flushes, output.FlushCount );
	}

	[Fact]
	public async Task FailedFinalReleaseCanBeRetriedThroughSameLease() {
		RecordingOutput output = new( 2 );
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalSynchronizedOutputLease lease =
			await session.AcquireSynchronizedOutputAsync();

		await Assert.ThrowsAsync<IOException>(
			() => lease.DisposeAsync().AsTask()
		);
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );

		output.ClearFailures();
		await lease.DisposeAsync();

		Assert.Equal( 3, output.WriteCount );
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task LeaseDisposalAfterSessionDisposalEmitsNothing() {
		RecordingOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalSynchronizedOutputLease lease =
			await session.AcquireSynchronizedOutputAsync();

		await session.DisposeAsync();
		int writes = output.WriteCount;
		int flushes = output.FlushCount;

		await lease.DisposeAsync();

		Assert.Equal( writes, output.WriteCount );
		Assert.Equal( flushes, output.FlushCount );
	}

	[Fact]
	public async Task PreCancelledAcquisitionEmitsNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.AcquireSynchronizedOutputAsync(
				cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
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
		private readonly HashSet<int> failingWrites;

		internal RecordingOutput(
			params int[] failingWrites
		) {
			ArgumentNullException.ThrowIfNull( failingWrites );
			this.failingWrites = [ .. failingWrites ];
		}

		internal int WriteCount {
			get {
				return this.writes.Count;
			}
		}

		internal int FlushCount {
			get;
			private set;
		}

		internal byte[] GetWrite(
			int index
		) {
			return this.writes[ index ].ToArray();
		}

		internal void ClearFailures() {
			this.failingWrites.Clear();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.writes.Add( buffer.ToArray() );
			int call = this.writes.Count;
			if ( this.failingWrites.Contains( call ) ) {
				return ValueTask.FromException(
					new IOException( $"Synthetic output failure {call}." )
				);
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.FlushCount++;
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
				"Size is not required by synchronized-output lease tests."
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
