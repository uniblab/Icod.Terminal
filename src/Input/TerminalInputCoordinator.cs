/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

using System.Threading.Channels;

/// <summary>
/// Owns demand-driven access to one terminal input decoder and preserves
/// ordered application events while internal response transactions are active.
/// </summary>
internal sealed class TerminalInputCoordinator {
	internal const int DefaultDeferredEventCapacity = 256;
	internal const int MaximumDeferredEventCapacity = 4096;

	private readonly object sync = new();
	private readonly TerminalInputDecoder decoder;
	private readonly CancellationToken stopToken;
	private readonly Channel<TerminalApplicationEvent> applicationEvents;
	private readonly SemaphoreSlim demandSignal = new( 0, 1 );

	private Task? pumpTask;
	private int applicationDemandCount;
	private int queryDemandCount;
	private long queryDemandGeneration;
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
				$"The deferred terminal-event capacity must be between 1 and "
					+ $"{MaximumDeferredEventCapacity} events."
			);
		}

		this.decoder = decoder;
		this.stopToken = stopToken;
		this.applicationEvents = Channel.CreateBounded<TerminalApplicationEvent>(
			new BoundedChannelOptions( deferredEventCapacity ) {
				SingleReader = true,
				SingleWriter = true,
				AllowSynchronousContinuations = false,
				FullMode = BoundedChannelFullMode.Wait
			}
		);
	}

	internal async ValueTask<TerminalApplicationEvent> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();

		if ( this.applicationEvents.Reader.TryRead( out TerminalApplicationEvent buffered ) ) {
			return buffered;
		}

		if ( !this.TryAddApplicationDemand() ) {
			return TerminalApplicationEvent.FromInput(
				TerminalInputEvent.EndOfInput()
			);
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

	internal TerminalResponseExpectation RegisterResponseExpectation(
		TerminalQueryResponsePlan responsePlan,
		bool armImmediately
	) {
		ArgumentNullException.ThrowIfNull( responsePlan );
		return this.decoder.RegisterResponseExpectation(
			responsePlan,
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

	internal KittyKeyboardFlagsProbe RegisterKittyKeyboardFlagsProbe() {
		return this.decoder.RegisterKittyKeyboardFlagsProbe();
	}

	internal void RemoveKittyKeyboardFlagsProbe(
		KittyKeyboardFlagsProbe probe
	) {
		ArgumentNullException.ThrowIfNull( probe );
		this.decoder.RemoveKittyKeyboardFlagsProbe( probe );
	}

	internal KittyGraphicsSupportProbe RegisterKittyGraphicsSupportProbe(
		uint imageId
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}
		return this.decoder.RegisterKittyGraphicsSupportProbe( imageId );
	}

	internal void RemoveKittyGraphicsSupportProbe(
		KittyGraphicsSupportProbe probe
	) {
		ArgumentNullException.ThrowIfNull( probe );
		this.decoder.RemoveKittyGraphicsSupportProbe( probe );
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
				++this.queryDemandGeneration;
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

				long observedQueryDemandGeneration;
				lock ( this.sync ) {
					observedQueryDemandGeneration = this.queryDemandGeneration;
				}

				TerminalInputDecodeResult result = await this.decoder.ReadNextAsync(
					this.stopToken
				).ConfigureAwait( false );
				if ( result.RoutingRestartRequired ) {
					continue;
				}
				if ( result.ResponseRouted ) {
					lock ( this.sync ) {
						if ( observedQueryDemandGeneration == this.queryDemandGeneration ) {
							this.queryDemandPaused = true;
						}
					}
					result.CompleteRoutedResponse();
					continue;
				}

				TerminalApplicationEvent applicationEvent = result.ApplicationEvent
					?? throw new InvalidOperationException(
						"The terminal input coordinator received an empty decoder result."
					);
				bool reachedEndOfInput = applicationEvent.IsEndOfInput;
				if ( reachedEndOfInput ) {
					lock ( this.sync ) {
						this.endOfInput = true;
					}
				}

				await this.applicationEvents.Writer.WriteAsync(
					applicationEvent,
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
