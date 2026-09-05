namespace Icod.Terminal;

/// <summary>
/// Owns identity-aware synchronized-output state for one terminal session.
/// </summary>
internal sealed class TerminalSynchronizedOutputManager : ITerminalSessionLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly HashSet<long> owners = [];
	private readonly IDisposable lifecycleRegistration;

	private long nextOwnerId;
	private bool physicalActive;
	private bool cleanupRequired;
	private bool suspended;
	private bool closed;

	internal TerminalSynchronizedOutputManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
		this.lifecycleRegistration = session.RegisterCoreLifecycleParticipant( this );
	}

	public ValueTask PrepareForTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.SuspendAsync();
	}

	public ValueTask ResumeAfterTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.ReenterAsync();
	}

	internal async ValueTask<long> AcquireAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Synchronized-output ownership cannot be acquired while terminal session state is suspended."
				);
			}
			if ( this.cleanupRequired ) {
				throw new InvalidOperationException(
					"Synchronized-output cleanup remains pending from a prior failed transition."
				);
			}
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The synchronized-output owner identifier space has been exhausted."
				);
			}

			bool firstOwner = 0 == this.owners.Count;
			if ( firstOwner ) {
				await this.EnterFirstOwnerAsync( cancellationToken ).ConfigureAwait( false );
			} else {
				cancellationToken.ThrowIfCancellationRequested();
			}

			long ownerId = ++this.nextOwnerId;
			this.owners.Add( ownerId );
			return ownerId;
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReleaseAsync(
		long ownerId
	) {
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}

		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed || !this.owners.Contains( ownerId ) ) {
				return;
			}

			if ( this.suspended ) {
				this.owners.Remove( ownerId );
				return;
			}

			if ( 1 < this.owners.Count ) {
				this.owners.Remove( ownerId );
				return;
			}

			try {
				await this.LeaveAndFlushAsync().ConfigureAwait( false );
			} catch {
				this.cleanupRequired = true;
				this.physicalActive = true;
				throw;
			}

			this.owners.Remove( ownerId );
			this.cleanupRequired = false;
			this.physicalActive = false;
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask SuspendAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed || this.suspended ) {
				return;
			}

			if ( 0 == this.owners.Count && !this.cleanupRequired ) {
				this.physicalActive = false;
				this.suspended = true;
				return;
			}

			try {
				await this.LeaveAndFlushAsync().ConfigureAwait( false );
				this.cleanupRequired = false;
				this.physicalActive = false;
				this.suspended = true;
			} catch {
				this.cleanupRequired = true;
				this.physicalActive = true;
				this.suspended = true;
				throw;
			}
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReenterAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}

			if ( this.cleanupRequired ) {
				try {
					await this.LeaveAndFlushAsync().ConfigureAwait( false );
					this.cleanupRequired = false;
					this.physicalActive = false;
				} catch {
					this.suspended = true;
					throw;
				}
			}

			if ( 0 == this.owners.Count ) {
				this.suspended = false;
				return;
			}

			try {
				await this.EmitBeginAsync(
					cleanup: true,
					CancellationToken.None
				).ConfigureAwait( false );
				this.physicalActive = true;
				this.suspended = false;
			} catch ( Exception enterFailure ) {
				this.physicalActive = true;
				this.cleanupRequired = true;
				this.suspended = true;
				try {
					await this.LeaveAndFlushAsync().ConfigureAwait( false );
					this.physicalActive = false;
					this.cleanupRequired = false;
				} catch ( Exception cleanupFailure ) {
					throw new AggregateException(
						"Synchronized-output re-entry failed and cleanup also reported an error.",
						enterFailure,
						cleanupFailure
					);
				}

				throw;
			}
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask CloseAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}

			Exception? exception = null;
			if ( this.cleanupRequired || this.physicalActive ) {
				try {
					await this.LeaveAndFlushAsync().ConfigureAwait( false );
				} catch ( Exception failure ) {
					exception = failure;
				}
			}

			this.closed = true;
			this.suspended = true;
			this.physicalActive = false;
			this.cleanupRequired = false;
			this.owners.Clear();

			if ( exception is not null ) {
				throw exception;
			}
		} finally {
			this.gate.Release();
			this.lifecycleRegistration.Dispose();
		}
	}

	private async ValueTask EnterFirstOwnerAsync(
		CancellationToken cancellationToken
	) {
		IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		using ( outputLease ) {
			try {
				await CsiWriter.WriteSynchronizedOutputBeginAsync(
					this.session.Output,
					cancellationToken
				).ConfigureAwait( false );
				this.physicalActive = true;
				return;
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				throw;
			} catch ( Exception enterFailure ) {
				this.physicalActive = true;
				this.cleanupRequired = true;
				try {
					await this.LeaveAndFlushCoreAsync().ConfigureAwait( false );
					this.physicalActive = false;
					this.cleanupRequired = false;
				} catch ( Exception cleanupFailure ) {
					throw new AggregateException(
						"Synchronized-output acquisition failed and cleanup also reported an error.",
						enterFailure,
						cleanupFailure
					);
				}

				throw;
			}
		}
	}

	private async ValueTask EmitBeginAsync(
		bool cleanup,
		CancellationToken cancellationToken
	) {
		IDisposable outputLease = cleanup
			? await this.session.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false )
			: await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait( false )
		;
		using ( outputLease ) {
			await CsiWriter.WriteSynchronizedOutputBeginAsync(
				this.session.Output,
				cleanup ? CancellationToken.None : cancellationToken
			).ConfigureAwait( false );
		}
	}

	private async ValueTask LeaveAndFlushAsync() {
		using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		await this.LeaveAndFlushCoreAsync().ConfigureAwait( false );
	}

	private async ValueTask LeaveAndFlushCoreAsync() {
		await CsiWriter.WriteSynchronizedOutputEndAsync(
			this.session.Output,
			CancellationToken.None
		).ConfigureAwait( false );
		await this.session.Output.FlushAsync(
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private void ValidateOutputEndpoint() {
		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Synchronized-output ownership requires an interactive terminal output endpoint."
			);
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}
}
