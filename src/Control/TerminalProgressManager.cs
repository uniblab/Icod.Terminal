namespace Icod.Terminal;

/// <summary>
/// Owns ordered, identity-aware session-managed OSC 9;4 progress state.
/// </summary>
internal sealed class TerminalProgressManager : ITerminalSessionLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly List<ProgressEntry> entries = [];
	private readonly IDisposable lifecycleRegistration;

	private long nextOwnerId;
	private bool physicalActive;
	private bool cleanupRequired;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalProgressManager(
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

	internal void Invalidate() {
		this.MarkInvalidated();
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
					"Terminal progress ownership cannot be acquired while terminal session state is suspended."
				);
			}
			if ( this.IsInvalidated
				&& 0 == this.entries.Count
				&& !this.physicalActive
				&& !this.cleanupRequired ) {
				this.ClearInvalidated();
			}
			if ( this.cleanupRequired || this.IsInvalidated ) {
				throw new InvalidOperationException(
					"Terminal progress cleanup remains pending from a prior failed or invalidated transition."
				);
			}
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The terminal progress owner identifier space has been exhausted."
				);
			}

			long ownerId = ++this.nextOwnerId;
			this.entries.Add(
				new ProgressEntry(
					ownerId,
					null
				)
			);
			return ownerId;
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReportAsync(
		long ownerId,
		TerminalProgressValue value,
		CancellationToken cancellationToken
	) {
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Terminal progress cannot be reported while terminal session state is suspended."
				);
			}

			int index = this.FindOwnerIndex( ownerId );
			if ( 0 > index ) {
				throw new InvalidOperationException(
					"The terminal progress owner is no longer active."
				);
			}

			if ( this.cleanupRequired || this.IsInvalidated ) {
				await this.RecoverPhysicalStateAsync().ConfigureAwait( false );
				cancellationToken.ThrowIfCancellationRequested();
			}

			bool controlsPhysicalState = true;
			for ( int candidate = this.entries.Count - 1; candidate > index; --candidate ) {
				if ( this.entries[ candidate ].Value.HasValue ) {
					controlsPhysicalState = false;
					break;
				}
			}

			if ( controlsPhysicalState ) {
				byte[] frame = EncodeValueFrame( value );
				try {
					using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
						cancellationToken
					).ConfigureAwait( false );
					cancellationToken.ThrowIfCancellationRequested();
					await this.session.Output.WriteAsync(
						frame,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch {
					this.cleanupRequired = true;
					this.MarkInvalidated();
					throw;
				}

				this.physicalActive = true;
				this.cleanupRequired = false;
				this.ClearInvalidated();
			}

			this.entries[ index ] = this.entries[ index ] with {
				Value = value
			};
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
			if ( this.closed ) {
				return;
			}

			int index = this.FindOwnerIndex( ownerId );
			if ( 0 > index ) {
				return;
			}
			if ( this.suspended ) {
				this.entries.RemoveAt( index );
				return;
			}

			if ( this.cleanupRequired || this.IsInvalidated ) {
				await this.RecoverPhysicalStateAsync().ConfigureAwait( false );
			}

			int controllerIndex = this.FindControllerIndex();
			if ( index != controllerIndex ) {
				this.entries.RemoveAt( index );
				return;
			}

			int nextControllerIndex = this.FindControllerIndex( index );
			byte[] frame = 0 > nextControllerIndex
				? OscWriter.EncodeOsc9ProgressFrame(
					Osc9ProgressState.Clear,
					0
				)
				: EncodeValueFrame( this.entries[ nextControllerIndex ].Value!.Value )
			;

			try {
				await this.WriteCleanupFrameAsync( frame ).ConfigureAwait( false );
			} catch {
				this.cleanupRequired = true;
				this.MarkInvalidated();
				throw;
			}

			this.entries.RemoveAt( index );
			this.physicalActive = 0 <= nextControllerIndex;
			this.cleanupRequired = false;
			this.ClearInvalidated();
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
			if ( !this.physicalActive
				&& !this.cleanupRequired
				&& !this.IsInvalidated ) {
				this.suspended = true;
				return;
			}

			try {
				await this.WriteClearAsync().ConfigureAwait( false );
				this.physicalActive = false;
				this.cleanupRequired = false;
				this.suspended = true;
				this.ClearInvalidated();
			} catch {
				this.cleanupRequired = true;
				this.suspended = true;
				this.MarkInvalidated();
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

			if ( this.cleanupRequired || this.IsInvalidated ) {
				try {
					await this.WriteClearAsync().ConfigureAwait( false );
					this.cleanupRequired = false;
					this.physicalActive = false;
					this.ClearInvalidated();
				} catch {
					this.suspended = true;
					this.MarkInvalidated();
					throw;
				}
			}

			int controllerIndex = this.FindControllerIndex();
			if ( 0 > controllerIndex ) {
				this.physicalActive = false;
				this.suspended = false;
				return;
			}

			byte[] frame = EncodeValueFrame(
				this.entries[ controllerIndex ].Value!.Value
			);
			try {
				await this.WriteCleanupFrameAsync( frame ).ConfigureAwait( false );
				this.physicalActive = true;
				this.cleanupRequired = false;
				this.suspended = false;
				this.ClearInvalidated();
			} catch ( Exception enterFailure ) {
				this.cleanupRequired = true;
				this.suspended = true;
				this.MarkInvalidated();
				try {
					await this.WriteClearAsync().ConfigureAwait( false );
					this.physicalActive = false;
					this.cleanupRequired = false;
					this.ClearInvalidated();
				} catch ( Exception cleanupFailure ) {
					throw new AggregateException(
						"Terminal progress re-entry failed and cleanup also reported an error.",
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
		bool releaseLifecycleRegistration = false;
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}

			if ( this.physicalActive
				|| this.cleanupRequired
				|| this.IsInvalidated ) {
				try {
					await this.WriteClearAsync().ConfigureAwait( false );
					this.physicalActive = false;
					this.cleanupRequired = false;
					this.ClearInvalidated();
				} catch {
					this.cleanupRequired = true;
					this.MarkInvalidated();
					throw;
				}
			}

			this.closed = true;
			this.suspended = true;
			this.entries.Clear();
			releaseLifecycleRegistration = true;
		} finally {
			this.gate.Release();
			if ( releaseLifecycleRegistration ) {
				this.lifecycleRegistration.Dispose();
			}
		}
	}

	private bool IsInvalidated {
		get {
			return 0 != Volatile.Read( ref this.invalidated );
		}
	}

	private async ValueTask RecoverPhysicalStateAsync() {
		int controllerIndex = this.FindControllerIndex();
		byte[] frame = 0 > controllerIndex
			? OscWriter.EncodeOsc9ProgressFrame(
				Osc9ProgressState.Clear,
				0
			)
			: EncodeValueFrame( this.entries[ controllerIndex ].Value!.Value )
		;

		try {
			await this.WriteCleanupFrameAsync( frame ).ConfigureAwait( false );
			this.cleanupRequired = false;
			this.physicalActive = 0 <= controllerIndex;
			this.ClearInvalidated();
		} catch {
			this.cleanupRequired = true;
			this.MarkInvalidated();
			throw;
		}
	}

	private ValueTask WriteClearAsync() {
		return this.WriteCleanupFrameAsync(
			OscWriter.EncodeOsc9ProgressFrame(
				Osc9ProgressState.Clear,
				0
			)
		);
	}

	private async ValueTask WriteCleanupFrameAsync(
		byte[] frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		await this.session.Output.WriteAsync(
			frame,
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private int FindOwnerIndex(
		long ownerId
	) {
		for ( int index = 0; index < this.entries.Count; ++index ) {
			if ( this.entries[ index ].OwnerId == ownerId ) {
				return index;
			}
		}
		return -1;
	}

	private int FindControllerIndex(
		int excludedIndex = -1
	) {
		for ( int index = this.entries.Count - 1; 0 <= index; --index ) {
			if ( index != excludedIndex
				&& this.entries[ index ].Value.HasValue ) {
				return index;
			}
		}
		return -1;
	}

	private void ValidateOutputEndpoint() {
		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Terminal progress operations require an interactive terminal output endpoint."
			);
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private void MarkInvalidated() {
		Volatile.Write( ref this.invalidated, 1 );
	}

	private void ClearInvalidated() {
		Volatile.Write( ref this.invalidated, 0 );
	}

	private static byte[] EncodeValueFrame(
		TerminalProgressValue value
	) {
		return OscWriter.EncodeOsc9ProgressFrame(
			value.GetWireState(),
			value.Percentage
		);
	}

	private sealed record ProgressEntry(
		long OwnerId,
		TerminalProgressValue? Value
	);
}
