namespace Icod.Terminal;

/// <summary>
/// Owns ordered, identity-aware session-managed OSC 22 pointer-shape state.
/// </summary>
internal sealed class TerminalPointerShapeManager : ITerminalSessionLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly List<PointerEntry> entries = [];
	private readonly IDisposable lifecycleRegistration;

	private long nextOwnerId;
	private bool physicalActive;
	private bool cleanupRequired;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalPointerShapeManager(
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
		TerminalPointerShape shape,
		CancellationToken cancellationToken
	) {
		string wireName = TerminalPointerShapeCodec.GetWireName( shape );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Terminal pointer-shape ownership cannot be acquired while terminal session state is suspended."
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
					"Terminal pointer-shape cleanup remains pending from a prior failed or invalidated transition."
				);
			}
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The terminal pointer-shape owner identifier space has been exhausted."
				);
			}

			byte[] frame = OscWriter.EncodeOsc22PointerShapeFrame( wireName );
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

			long ownerId = ++this.nextOwnerId;
			this.entries.Add(
				new PointerEntry(
					ownerId,
					shape
				)
			);
			this.physicalActive = true;
			this.cleanupRequired = false;
			this.ClearInvalidated();
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

			int controllerIndex = this.entries.Count - 1;
			if ( index != controllerIndex ) {
				this.entries.RemoveAt( index );
				return;
			}

			int nextControllerIndex = index - 1;
			byte[] frame = 0 > nextControllerIndex
				? OscWriter.EncodeOsc22PointerShapeFrame( null )
				: EncodeShapeFrame( this.entries[ nextControllerIndex ].Shape )
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
				await this.WriteResetAsync().ConfigureAwait( false );
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
					await this.WriteResetAsync().ConfigureAwait( false );
					this.cleanupRequired = false;
					this.physicalActive = false;
					this.ClearInvalidated();
				} catch {
					this.suspended = true;
					this.MarkInvalidated();
					throw;
				}
			}

			if ( 0 == this.entries.Count ) {
				this.physicalActive = false;
				this.suspended = false;
				return;
			}

			byte[] frame = EncodeShapeFrame( this.entries[ ^1 ].Shape );
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
					await this.WriteResetAsync().ConfigureAwait( false );
					this.physicalActive = false;
					this.cleanupRequired = false;
					this.ClearInvalidated();
				} catch ( Exception cleanupFailure ) {
					throw new AggregateException(
						"Terminal pointer-shape re-entry failed and cleanup also reported an error.",
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
					await this.WriteResetAsync().ConfigureAwait( false );
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
		byte[] frame = 0 == this.entries.Count
			? OscWriter.EncodeOsc22PointerShapeFrame( null )
			: EncodeShapeFrame( this.entries[ ^1 ].Shape )
		;
		try {
			await this.WriteCleanupFrameAsync( frame ).ConfigureAwait( false );
			this.cleanupRequired = false;
			this.physicalActive = 0 != this.entries.Count;
			this.ClearInvalidated();
		} catch {
			this.cleanupRequired = true;
			this.MarkInvalidated();
			throw;
		}
	}

	private ValueTask WriteResetAsync() {
		return this.WriteCleanupFrameAsync(
			OscWriter.EncodeOsc22PointerShapeFrame( null )
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

	private void ValidateOutputEndpoint() {
		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Terminal pointer-shape operations require an interactive terminal output endpoint."
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

	private static byte[] EncodeShapeFrame(
		TerminalPointerShape shape
	) {
		return OscWriter.EncodeOsc22PointerShapeFrame(
			TerminalPointerShapeCodec.GetWireName( shape )
		);
	}

	private sealed record PointerEntry(
		long OwnerId,
		TerminalPointerShape Shape
	);
}
