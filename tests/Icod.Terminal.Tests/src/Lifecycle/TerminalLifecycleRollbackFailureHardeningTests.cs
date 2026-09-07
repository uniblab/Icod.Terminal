namespace Icod.Terminal.Tests.Lifecycle;

using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies that lifecycle re-entry plus rollback failure preserves uncertainty
/// instead of reporting a valid session state.
/// </summary>
public sealed class TerminalLifecycleRollbackFailureHardeningTests {
	[Fact]
	public async Task FailedReentryAndFailedBaselineRollbackSurfaceAggregateFailure() {
		FailingModeProvider provider = new( 3, 4 );
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			provider,
			lifecycle
		);
		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);

		Assert.Equal( 1, provider.SetModeCount );
		Assert.True( session.IsStateValid );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );

		ChannelClosedException channelFailure = await Assert.ThrowsAsync<ChannelClosedException>(
			() => session.ReadLifecycleEventAsync(
				timeout.Token
			).AsTask()
		);
		AggregateException failure = Assert.IsType<AggregateException>(
			channelFailure.InnerException
		);

		Assert.Equal( 4, provider.SetModeCount );
		Assert.False( session.IsStateValid );
		Assert.True( session.TerminationToken.IsCancellationRequested );
		Assert.Equal( 2, failure.InnerExceptions.Count );
		Assert.Contains(
			failure.InnerExceptions,
			exception => exception.Message.Contains(
				"Synthetic mode failure 3",
				StringComparison.Ordinal
			)
		);
		Assert.Contains(
			failure.InnerExceptions,
			exception => exception.Message.Contains(
				"Synthetic mode failure 4",
				StringComparison.Ordinal
			)
		);

		await session.DisposeAsync();
		Assert.Equal( 5, provider.SetModeCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		FailingModeProvider provider,
		TestTerminalLifecycleSource lifecycle
	) {
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( lifecycle );

		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			new TestTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
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

	private sealed class TestTerminalOutput : ITerminalOutput {
		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}

			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
		}
	}

	private sealed class FailingModeProvider : ITerminalControlProvider {
		private readonly HashSet<int> failingCalls;
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

		internal FailingModeProvider(
			params int[] failingCalls
		) {
			ArgumentNullException.ThrowIfNull( failingCalls );
			this.failingCalls = [ .. failingCalls ];
		}

		internal int SetModeCount {
			get;
			private set;
		}

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
			return TerminalControlResult<TerminalSize>.Unavailable(
				"Live size is not required by lifecycle rollback hardening."
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

			int call = ++this.SetModeCount;
			return this.failingCalls.Contains( call )
				? TerminalControlMutationResult.Failed(
					$"Synthetic mode failure {call}."
				)
				: TerminalControlMutationResult.Success()
			;
		}
	}
}
