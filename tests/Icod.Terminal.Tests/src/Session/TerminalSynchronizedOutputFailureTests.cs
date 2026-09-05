namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T95 cancellation and synchronized-output failure recovery boundaries.
/// </summary>
public sealed class TerminalSynchronizedOutputFailureTests {
	[Fact]
	public async Task CancellationWhileWaitingForOutputGateEmitsNothing() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using IDisposable heldOutput = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();

		Task<TerminalSynchronizedOutputLease> acquire =
			session.AcquireSynchronizedOutputAsync(
				cancellation.Token
			).AsTask();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => acquire
		);
		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCallCount );
	}

	[Fact]
	public async Task FailedFinalFlushCanBeRetriedThroughSameLease() {
		RecordingOutput output = new(
			failingWriteCalls: [],
			failingFlushCalls: [ 1 ]
		);
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSynchronizedOutputLease lease =
			await session.AcquireSynchronizedOutputAsync();

		await Assert.ThrowsAsync<IOException>(
			() => lease.DisposeAsync().AsTask()
		);
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 1, output.FlushCallCount );

		output.ClearFailures();
		await lease.DisposeAsync();

		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( 2 )
		);
		Assert.Equal( 2, output.FlushCallCount );
	}

	[Fact]
	public async Task FailedBeginAndFailedCleanupAreRetriedBySessionDisposal() {
		RecordingOutput output = new(
			failingWriteCalls: [ 1, 2 ],
			failingFlushCalls: []
		);
		TerminalSession session = await OpenSessionAsync( output );
		try {
			AggregateException exception =
				await Assert.ThrowsAsync<AggregateException>(
					() => session.AcquireSynchronizedOutputAsync().AsTask()
				);
			Assert.Equal( 2, exception.InnerExceptions.Count );
			Assert.Equal( 2, output.WriteCount );
			Assert.Equal( 0, output.FlushCallCount );

			output.ClearFailures();
			await session.DisposeAsync();

			Assert.Equal( 3, output.WriteCount );
			Assert.Equal(
				CsiWriter.EncodeSynchronizedOutputEndFrame(),
				output.GetWrite( 2 )
			);
			Assert.True( 0 < output.FlushCallCount );
		} finally {
			await session.DisposeAsync();
		}
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
		private readonly HashSet<int> failingWriteCalls;
		private readonly HashSet<int> failingFlushCalls;

		internal RecordingOutput(
			IEnumerable<int> failingWriteCalls,
			IEnumerable<int> failingFlushCalls
		) {
			ArgumentNullException.ThrowIfNull( failingWriteCalls );
			ArgumentNullException.ThrowIfNull( failingFlushCalls );
			this.failingWriteCalls = [ .. failingWriteCalls ];
			this.failingFlushCalls = [ .. failingFlushCalls ];
		}

		internal RecordingOutput()
			: this(
				[],
				[]
			) {
		}

		internal int WriteCount {
			get {
				return this.writes.Count;
			}
		}

		internal int FlushCallCount {
			get;
			private set;
		}

		internal byte[] GetWrite(
			int index
		) {
			return this.writes[ index ].ToArray();
		}

		internal void ClearFailures() {
			this.failingWriteCalls.Clear();
			this.failingFlushCalls.Clear();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.writes.Add( buffer.ToArray() );
			int call = this.writes.Count;
			if ( this.failingWriteCalls.Contains( call ) ) {
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
			int call = ++this.FlushCallCount;
			if ( this.failingFlushCalls.Contains( call ) ) {
				return ValueTask.FromException(
					new IOException( $"Synthetic flush failure {call}." )
				);
			}
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
				"Size is not required by synchronized-output failure tests."
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
