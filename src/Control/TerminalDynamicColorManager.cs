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

/// <summary>
/// Owns identity-aware dynamic terminal-color state with observed baseline restoration.
/// </summary>
internal sealed class TerminalDynamicColorManager : ITerminalObservedLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly Dictionary<TerminalDynamicColor, DynamicColorState> states = [];
	private readonly IDisposable lifecycleRegistration;

	private long nextOwnerId;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalDynamicColorManager(
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
					"Unscoped dynamic-color mutation is unavailable while terminal session state is suspended."
				);
			}
			if ( 0 != this.states.Count ) {
				throw new InvalidOperationException(
					"Unscoped dynamic-color mutation/reset is unavailable while a scoped dynamic-color lease is active."
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

	internal async ValueTask<TerminalDynamicColorLease> AcquireAsync(
		TerminalDynamicColor kind,
		TerminalColor color,
		TimeSpan timeout,
		CancellationToken cancellationToken
	) {
		_ = TerminalDynamicColorProtocol.CreateSetRequest( kind, color );
		ValidateTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Dynamic-color leases cannot be acquired while terminal session state is suspended."
				);
			}
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The dynamic-color owner identifier space has been exhausted."
				);
			}

			if ( this.IsInvalidated && 0 != this.states.Count ) {
				await this.ReapplyOwnedColorsAsync().ConfigureAwait( false );
			}

			bool firstOwner = !this.states.TryGetValue(
				kind,
				out DynamicColorState? state
			);
			TerminalColor previousColor;
			if ( firstOwner ) {
				previousColor = await this.session.QueryDynamicColorAsync(
					kind,
					timeout,
					cancellationToken
				).ConfigureAwait( false );
				state = new DynamicColorState(
					kind,
					previousColor,
					timeout
				);
			} else {
				previousColor = state!.Owners[ ^1 ].Color;
			}

			cancellationToken.ThrowIfCancellationRequested();
			byte[] requestedFrame = TerminalDynamicColorProtocol.CreateSetRequest(
				kind,
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
						kind,
						previousColor,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception restorationFailure ) {
					Volatile.Write( ref this.invalidated, 1 );
					throw new AggregateException(
						"Dynamic-color lease acquisition failed and restoring the known prior color also failed.",
						acquisitionFailure,
						restorationFailure
					);
				}

				throw;
			}

			long ownerId = ++this.nextOwnerId;
			TerminalDynamicColorLease lease = new(
				this,
				ownerId,
				kind,
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
				this.states.Add( kind, state );
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
				out DynamicColorState? state,
				out int ownerIndex
			) ) {
				return;
			}

			OwnerEntry owner = state!.Owners[ ownerIndex ];
			if ( this.suspended ) {
				state.Owners.RemoveAt( ownerIndex );
				owner.Lease.MarkReleasedByOwner();
				if ( 0 == state.Owners.Count ) {
					this.states.Remove( state.Kind );
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
					state.Kind,
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
				this.states.Remove( state.Kind );
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
			foreach ( DynamicColorState state in this.OrderedStates() ) {
				try {
					await this.WriteColorAsync(
						state.Kind,
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
					"One or more dynamic-color baselines could not be restored before suspension.",
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
					"Dynamic-color baseline refresh is valid only during lifecycle re-entry."
				);
			}

			foreach ( DynamicColorState state in this.OrderedStates() ) {
				TerminalResponseFrame frame = await this.session.ExecuteLifecycleObservationQueryAsync(
					TerminalDynamicColorProtocol.CreateQueryRequest( state.Kind ),
					TerminalDynamicColorProtocol.CreateResponseMatcher( state.Kind ),
					state.QueryTimeout,
					CancellationToken.None
				).ConfigureAwait( false );
				state.Baseline = TerminalDynamicColorProtocol.ParseObservation(
					frame,
					state.Kind
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

			List<DynamicColorState> applied = [];
			try {
				foreach ( DynamicColorState state in this.OrderedStates() ) {
					if ( 0 == state.Owners.Count ) {
						continue;
					}
					await this.WriteColorAsync(
						state.Kind,
						state.Owners[ ^1 ].Color,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
					applied.Add( state );
				}
			} catch ( Exception reentryFailure ) {
				List<Exception> rollbackFailures = [];
				foreach ( DynamicColorState state in applied ) {
					try {
						await this.WriteColorAsync(
							state.Kind,
							state.Baseline,
							cleanup: true,
							CancellationToken.None
						).ConfigureAwait( false );
					} catch ( Exception rollbackFailure ) {
						rollbackFailures.Add( rollbackFailure );
					}
				}
				Volatile.Write( ref this.invalidated, 1 );
				if ( 0 != rollbackFailures.Count ) {
					throw new AggregateException(
						"Dynamic-color re-entry failed and rolling back to refreshed baselines also reported an error.",
						[ reentryFailure, .. rollbackFailures ]
					);
				}
				throw;
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
			foreach ( DynamicColorState state in this.OrderedStates() ) {
				try {
					await this.WriteColorAsync(
						state.Kind,
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
			foreach ( DynamicColorState state in this.states.Values ) {
				foreach ( OwnerEntry owner in state.Owners ) {
					owner.Lease.MarkReleasedByOwner();
				}
			}
			this.states.Clear();

			if ( 0 != exceptions.Count ) {
				throw BuildException(
					"One or more dynamic-color baselines could not be restored during session cleanup.",
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

	private IEnumerable<DynamicColorState> OrderedStates() {
		return this.states.Values.OrderBy(
			static state => (int)state.Kind
		);
	}

	private async ValueTask ReapplyOwnedColorsAsync() {
		foreach ( DynamicColorState state in this.OrderedStates() ) {
			if ( 0 == state.Owners.Count ) {
				continue;
			}
			await this.WriteColorAsync(
				state.Kind,
				state.Owners[ ^1 ].Color,
				cleanup: true,
				CancellationToken.None
			).ConfigureAwait( false );
		}
		Volatile.Write( ref this.invalidated, 0 );
	}

	private ValueTask WriteColorAsync(
		TerminalDynamicColor kind,
		TerminalColor color,
		bool cleanup,
		CancellationToken cancellationToken
	) {
		return this.WriteFrameAsync(
			TerminalDynamicColorProtocol.CreateSetRequest(
				kind,
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

	private bool TryFindOwner(
		long ownerId,
		out DynamicColorState? state,
		out int ownerIndex
	) {
		foreach ( DynamicColorState candidate in this.states.Values ) {
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
				"Dynamic-color operations require an interactive terminal output endpoint."
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
				"A dynamic-color query timeout must be between zero and "
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

	private sealed class DynamicColorState {
		internal DynamicColorState(
			TerminalDynamicColor kind,
			TerminalColor baseline,
			TimeSpan queryTimeout
		) {
			this.Kind = kind;
			this.Baseline = baseline;
			this.QueryTimeout = queryTimeout;
		}

		internal TerminalDynamicColor Kind {
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
		TerminalDynamicColorLease Lease
	);
}
