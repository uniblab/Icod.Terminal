namespace Icod.Terminal;

/// <summary>
/// Owns identity-aware indexed-palette state with observed baseline restoration.
/// </summary>
internal sealed class TerminalPaletteColorManager : ITerminalObservedLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly Dictionary<byte, PaletteState> states = [];
	private readonly IDisposable lifecycleRegistration;

	private long nextOwnerId;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalPaletteColorManager(
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

	public ValueTask RefreshAfterTerminalResumeAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.RefreshBaselinesAsync();
	}

	public ValueTask ResumeAfterTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.ReenterAsync();
	}

	internal void Invalidate() {
		Volatile.Write( ref this.invalidated, 1 );
	}

	internal async ValueTask WriteUnscopedAsync(
		byte[] frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Unscoped palette mutation is unavailable while terminal session state is suspended."
				);
			}
			if ( 0 != this.states.Count ) {
				throw new InvalidOperationException(
					"Unscoped palette mutation/reset is unavailable while a scoped palette-color lease is active."
				);
			}

			await this.WriteFrameAsync(
				frame,
				cleanup: false,
				cancellationToken
			).ConfigureAwait( false );
			Volatile.Write( ref this.invalidated, 0 );
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask<TerminalPaletteColorLease> AcquireAsync(
		byte index,
		TerminalColor color,
		TimeSpan timeout,
		CancellationToken cancellationToken
	) {
		ValidateTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Palette-color leases cannot be acquired while terminal session state is suspended."
				);
			}
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The palette-color owner identifier space has been exhausted."
				);
			}

			if ( this.IsInvalidated && 0 != this.states.Count ) {
				await this.ReapplyOwnedColorsAsync().ConfigureAwait( false );
			}

			bool firstOwner = !this.states.TryGetValue(
				index,
				out PaletteState? state
			);
			TerminalColor previousColor;
			if ( firstOwner ) {
				previousColor = await this.session.QueryPaletteColorAsync(
					index,
					timeout,
					cancellationToken
				).ConfigureAwait( false );
				state = new PaletteState(
					index,
					previousColor,
					timeout
				);
			} else {
				previousColor = state!.Owners[ ^1 ].Color;
			}

			cancellationToken.ThrowIfCancellationRequested();
			byte[] requestedFrame = TerminalOsc4Protocol.CreateSetRequest(
				index,
				color
			);
			bool emissionAttempted = false;
			try {
				using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
					cancellationToken
				).ConfigureAwait( false );
				cancellationToken.ThrowIfCancellationRequested();
				emissionAttempted = true;
				await this.session.Output.WriteAsync(
					requestedFrame,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception acquisitionFailure ) when ( emissionAttempted ) {
				try {
					await this.WriteColorAsync(
						index,
						previousColor,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception restorationFailure ) {
					Volatile.Write( ref this.invalidated, 1 );
					throw new AggregateException(
						"Palette-color lease acquisition failed and restoring the known prior color also failed.",
						acquisitionFailure,
						restorationFailure
					);
				}

				throw;
			}

			long ownerId = ++this.nextOwnerId;
			TerminalPaletteColorLease lease = new(
				this,
				ownerId,
				index,
				color
			);
			state!.Owners.Add(
				new OwnerEntry(
					ownerId,
					color,
					lease
				)
			);
			if ( firstOwner ) {
				this.states.Add( index, state );
			}
			Volatile.Write( ref this.invalidated, 0 );
			return lease;
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

			if ( !this.TryFindOwner(
				ownerId,
				out PaletteState? state,
				out int ownerIndex
			) ) {
				return;
			}

			OwnerEntry owner = state!.Owners[ ownerIndex ];
			if ( this.suspended ) {
				state.Owners.RemoveAt( ownerIndex );
				owner.Lease.MarkReleasedByOwner();
				if ( 0 == state.Owners.Count ) {
					this.states.Remove( state.Index );
				}
				return;
			}

			if ( this.IsInvalidated ) {
				await this.ReapplyOwnedColorsAsync().ConfigureAwait( false );
			}

			int controllerIndex = state.Owners.Count - 1;
			if ( ownerIndex != controllerIndex ) {
				state.Owners.RemoveAt( ownerIndex );
				owner.Lease.MarkReleasedByOwner();
				return;
			}

			TerminalColor restoreColor = 0 == controllerIndex
				? state.Baseline
				: state.Owners[ controllerIndex - 1 ].Color
			;
			try {
				await this.WriteColorAsync(
					state.Index,
					restoreColor,
					cleanup: true,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				Volatile.Write( ref this.invalidated, 1 );
				throw;
			}

			state.Owners.RemoveAt( ownerIndex );
			owner.Lease.MarkReleasedByOwner();
			if ( 0 == state.Owners.Count ) {
				this.states.Remove( state.Index );
			}
			Volatile.Write( ref this.invalidated, 0 );
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

			List<Exception> exceptions = [];
			foreach ( PaletteState state in this.OrderedStates() ) {
				try {
					await this.WriteColorAsync(
						state.Index,
						state.Baseline,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					exceptions.Add( exception );
				}
			}

			this.suspended = true;
			if ( 0 != exceptions.Count ) {
				Volatile.Write( ref this.invalidated, 1 );
				throw BuildException(
					"One or more indexed palette baselines could not be restored before suspension.",
					exceptions
				);
			}
			Volatile.Write( ref this.invalidated, 0 );
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask RefreshBaselinesAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed || 0 == this.states.Count ) {
				return;
			}
			if ( !this.suspended ) {
				throw new InvalidOperationException(
					"Palette baseline refresh is valid only during lifecycle re-entry."
				);
			}

			foreach ( PaletteState state in this.OrderedStates() ) {
				TerminalResponseFrame frame = await this.session.ExecuteLifecycleObservationQueryAsync(
					TerminalOsc4Protocol.CreateQueryRequest( state.Index ),
					TerminalOsc4Protocol.CreateResponseMatcher( state.Index ),
					state.QueryTimeout,
					CancellationToken.None
				).ConfigureAwait( false );
				state.Baseline = TerminalOsc4Protocol.ParseObservation(
					frame,
					state.Index
				);
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

			List<Exception> reentryExceptions = [];
			foreach ( PaletteState state in this.OrderedStates() ) {
				if ( 0 == state.Owners.Count ) {
					continue;
				}
				try {
					await this.WriteColorAsync(
						state.Index,
						state.Owners[ ^1 ].Color,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					reentryExceptions.Add( exception );
				}
			}

			if ( 0 != reentryExceptions.Count ) {
				List<Exception> rollbackExceptions = [];
				foreach ( PaletteState state in this.OrderedStates() ) {
					try {
						await this.WriteColorAsync(
							state.Index,
							state.Baseline,
							cleanup: true,
							CancellationToken.None
						).ConfigureAwait( false );
					} catch ( Exception exception ) {
						rollbackExceptions.Add( exception );
					}
				}

				this.suspended = true;
				Volatile.Write( ref this.invalidated, 1 );
				Exception reentryFailure = BuildException(
					"One or more indexed palette colors could not be reapplied after resume.",
					reentryExceptions
				);
				if ( 0 == rollbackExceptions.Count ) {
					throw reentryFailure;
				}
				throw new AggregateException(
					"Indexed palette re-entry failed and restoring the refreshed external baselines also reported an error.",
					reentryFailure,
					BuildException(
						"One or more refreshed indexed palette baselines could not be restored.",
						rollbackExceptions
					)
				);
			}

			this.suspended = false;
			Volatile.Write( ref this.invalidated, 0 );
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

			List<Exception> exceptions = [];
			foreach ( PaletteState state in this.OrderedStates() ) {
				try {
					await this.WriteColorAsync(
						state.Index,
						state.Baseline,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					exceptions.Add( exception );
				}
			}

			this.closed = true;
			this.suspended = true;
			foreach ( PaletteState state in this.states.Values ) {
				foreach ( OwnerEntry owner in state.Owners ) {
					owner.Lease.MarkReleasedByOwner();
				}
			}
			this.states.Clear();

			if ( 0 != exceptions.Count ) {
				throw BuildException(
					"One or more indexed palette baselines could not be restored during session cleanup.",
					exceptions
				);
			}
		} finally {
			this.gate.Release();
			this.lifecycleRegistration.Dispose();
		}
	}

	private bool IsInvalidated {
		get {
			return 0 != Volatile.Read( ref this.invalidated );
		}
	}

	private async ValueTask ReapplyOwnedColorsAsync() {
		foreach ( PaletteState state in this.OrderedStates() ) {
			if ( 0 == state.Owners.Count ) {
				continue;
			}
			await this.WriteColorAsync(
				state.Index,
				state.Owners[ ^1 ].Color,
				cleanup: true,
				CancellationToken.None
			).ConfigureAwait( false );
		}
		Volatile.Write( ref this.invalidated, 0 );
	}

	private ValueTask WriteColorAsync(
		byte index,
		TerminalColor color,
		bool cleanup,
		CancellationToken cancellationToken
	) {
		return this.WriteFrameAsync(
			TerminalOsc4Protocol.CreateSetRequest(
				index,
				color
			),
			cleanup,
			cancellationToken
		);
	}

	private async ValueTask WriteFrameAsync(
		byte[] frame,
		bool cleanup,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( frame );
		IDisposable outputLease = cleanup
			? await this.session.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false )
			: await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait( false )
		;

		using ( outputLease ) {
			if ( !cleanup ) {
				cancellationToken.ThrowIfCancellationRequested();
			}
			await this.session.Output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}

	private IReadOnlyList<PaletteState> OrderedStates() {
		return this.states.Values
			.OrderBy( static state => state.Index )
			.ToArray();
	}

	private bool TryFindOwner(
		long ownerId,
		out PaletteState? state,
		out int ownerIndex
	) {
		foreach ( PaletteState candidate in this.states.Values ) {
			for ( int index = 0; index < candidate.Owners.Count; ++index ) {
				if ( candidate.Owners[ index ].OwnerId == ownerId ) {
					state = candidate;
					ownerIndex = index;
					return true;
				}
			}
		}

		state = null;
		ownerIndex = -1;
		return false;
	}

	private void ValidateOutputEndpoint() {
		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Indexed palette operations require an interactive terminal output endpoint."
			);
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private static void ValidateTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal palette query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}

	private static Exception BuildException(
		string message,
		IReadOnlyCollection<Exception> exceptions
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( message );
		ArgumentNullException.ThrowIfNull( exceptions );
		return 1 == exceptions.Count
			? exceptions.First()
			: new AggregateException( message, exceptions )
		;
	}

	private sealed class PaletteState {
		internal PaletteState(
			byte index,
			TerminalColor baseline,
			TimeSpan queryTimeout
		) {
			this.Index = index;
			this.Baseline = baseline;
			this.QueryTimeout = queryTimeout;
		}

		internal byte Index {
			get;
		}

		internal TerminalColor Baseline {
			get;
			set;
		}

		internal TimeSpan QueryTimeout {
			get;
		}

		internal List<OwnerEntry> Owners {
			get;
		} = [];
	}

	private sealed record OwnerEntry(
		long OwnerId,
		TerminalColor Color,
		TerminalPaletteColorLease Lease
	);
}
