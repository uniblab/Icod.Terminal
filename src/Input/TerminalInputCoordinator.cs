namespace Icod.Terminal;

using System.Threading.Channels;

/// <summary>
/// Owns demand-driven access to one terminal input decoder and preserves
/// application input while internal response transactions are active.
/// </summary>
internal sealed class TerminalInputCoordinator {
	internal const int DefaultDeferredEventCapacity = 256;
	internal const int MaximumDeferredEventCapacity = 4096;

	private readonly object sync = new();
	private readonly TerminalInputDecoder decoder;
	private readonly CancellationToken stopToken;
	private readonly Channel<TerminalInputEvent> applicationEvents;
	private readonly SemaphoreSlim demandSignal = new( 0, 1 );

	private Task? pumpTask;
	private int applicationDemandCount;
	private int queryDemandCount;
	private bool queryDemandPaused;
	private bool endOfInput;
	private bool closed;

	internal TerminalInputCoordinator(
		TerminalInputDecoder decoder,
		CancellationToken stopToken,
		int deferredEventCapacity = DefaultDeferredEventCapacity
	) {
		ArgumentNullException.ThrowIfNull( decoder );
		if ( 1 > deferredEventCapacity
			|| MaximumDeferredEventCapacity < deferredEventCapacity ) {
			throw new ArgumentOutOfRangeException(
				nameof( deferredEventCapacity ),
				deferredEventCapacity,
				$"The deferred terminal-input capacity must be between 1 and "
					+ $"{MaximumDeferredEventCapacity} events."
			);
		}

		this.decoder = decoder;
		this.stopToken = stopToken;
		this.applicationEvents = Channel.CreateBounded<TerminalInputEvent>(
			new BoundedChannelOptions( deferredEventCapacity ) {
				SingleReader = true,
				SingleWriter = true,
				AllowSynchronousContinuations = false,
				FullMode = BoundedChannelFullMode.Wait
			}
		);
	}

	internal async ValueTask<TerminalInputEvent> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();

		if ( this.applicationEvents.Reader.TryRead( out TerminalInputEvent? buffered ) ) {
			return buffered;
		}

		if ( !this.TryAddApplicationDemand() ) {
			return TerminalInputEvent.EndOfInput();
		}
		if ( this.applicationEvents.Reader.TryRead( out buffered ) ) {
			this.ReleaseApplicationDemand();
			return buffered;
		}

		try {
			return await this.applicationEvents.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
		} catch {
			this.ReleaseApplicationDemand();
			throw;
		}
	}

	internal IDisposable AcquireQueryDemand() {
		return this.AcquireQueryDemandCore();
	}

	internal TerminalResponseExpectation RegisterResponseExpectation(
		ITerminalResponseMatcher matcher,
		bool armImmediately
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		return this.decoder.RegisterResponseExpectation(
			matcher,
			armImmediately
		);
	}

	internal void ArmResponseExpectation(
		TerminalResponseExpectation expectation
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		this.decoder.ArmResponseExpectation( expectation );
	}

	internal bool RemoveResponseExpectation(
		TerminalResponseExpectation expectation
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		return this.decoder.RemoveResponseExpectation( expectation );
	}

	private bool TryAddApplicationDemand() {
		lock ( this.sync ) {
			if ( this.endOfInput ) {
				return false;
			}
			this.ThrowIfClosed();

			bool wake = !this.HasRunnableDemand();
			checked {
				++this.applicationDemandCount;
			}
			this.EnsurePumpStarted();
			this.SignalDemandIfNeeded( wake );
			return true;
		}
	}

	private IDisposable AcquireQueryDemandCore() {
		lock ( this.sync ) {
			this.ThrowIfClosed();
			if ( this.endOfInput ) {
				throw new InvalidOperationException(
					"Terminal queries are unavailable after terminal input reaches end-of-input."
				);
			}

			bool wake = !this.HasRunnableDemand();
			checked {
				++this.queryDemandCount;
			}
			this.queryDemandPaused = false;
			this.EnsurePumpStarted();
			this.SignalDemandIfNeeded( wake );

			return new DemandLease( this );
		}
	}

	private void EnsurePumpStarted() {
		this.pumpTask ??= this.RunPumpAsync();
	}

	private void SignalDemandIfNeeded(
		bool wake
	) {
		if ( wake && 0 == this.demandSignal.CurrentCount ) {
			this.demandSignal.Release();
		}
	}

	private async Task RunPumpAsync() {
		Exception? failure = null;
		try {
			while ( true ) {
				this.stopToken.ThrowIfCancellationRequested();
				await this.WaitForDemandAsync().ConfigureAwait( false );

				TerminalInputDecodeResult result = await this.decoder.ReadNextAsync(
					this.stopToken
				).ConfigureAwait( false );
				if ( result.ResponseRouted ) {
					lock ( this.sync ) {
						this.queryDemandPaused = true;
					}
					result.CompleteRoutedResponse();
					continue;
				}

				TerminalInputEvent inputEvent = result.InputEvent
					?? throw new InvalidOperationException(
						"The terminal input coordinator received an empty decoder result."
					);
				bool reachedEndOfInput = TerminalInputEventKind.EndOfInput == inputEvent.Kind;
				if ( reachedEndOfInput ) {
					lock ( this.sync ) {
						this.endOfInput = true;
					}
				}

				await this.applicationEvents.Writer.WriteAsync(
					inputEvent,
					this.stopToken
				).ConfigureAwait( false );
				this.ReleaseApplicationDemand();

				if ( reachedEndOfInput ) {
					return;
				}
			}
		} catch ( OperationCanceledException ) when ( this.stopToken.IsCancellationRequested ) {
		} catch ( Exception exception ) {
			failure = exception;
		} finally {
			lock ( this.sync ) {
				this.closed = true;
			}
			this.applicationEvents.Writer.TryComplete( failure );
		}
	}

	private async ValueTask WaitForDemandAsync() {
		while ( true ) {
			lock ( this.sync ) {
				if ( this.HasRunnableDemand() ) {
					return;
				}
				this.ThrowIfClosed();
			}

			await this.demandSignal.WaitAsync(
				this.stopToken
			).ConfigureAwait( false );
		}
	}

	private bool HasRunnableDemand() {
		return 0 < this.applicationDemandCount
			|| ( 0 < this.queryDemandCount && !this.queryDemandPaused );
	}

	private void ReleaseApplicationDemand() {
		lock ( this.sync ) {
			if ( 0 < this.applicationDemandCount ) {
				--this.applicationDemandCount;
			}
		}
	}

	private void ReleaseQueryDemand() {
		lock ( this.sync ) {
			if ( 0 < this.queryDemandCount ) {
				--this.queryDemandCount;
			}
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed || this.stopToken.IsCancellationRequested ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private sealed class DemandLease : IDisposable {
		private TerminalInputCoordinator? owner;

		internal DemandLease(
			TerminalInputCoordinator owner
		) {
			ArgumentNullException.ThrowIfNull( owner );
			this.owner = owner;
		}

		public void Dispose() {
			TerminalInputCoordinator? prior = Interlocked.Exchange(
				ref this.owner,
				null
			);
			prior?.ReleaseQueryDemand();
		}
	}
}
