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
/// Owns independently negotiated terminal-environment reporting modes.
/// </summary>
internal sealed class TerminalEnvironmentReportingManager {
	private const int AppearanceMode = 2031;
	private const int InBandResizeMode = 2048;

	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly Dictionary<TerminalEnvironmentReportingKind, FacilityState>
		states = [];

	private long nextOwnerId;
	private bool closed;
	private int invalidated;

	internal TerminalEnvironmentReportingManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
	}

	internal ValueTask<IDisposable> AcquireCompositionAsync(
		CancellationToken cancellationToken
	) {
		return this.session.AcquireStateCompositionAsync( cancellationToken );
	}

	internal async ValueTask<TerminalControlResult<TerminalAppearanceReportingLease>>
		AcquireAppearanceAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken
		) {
		cancellationToken.ThrowIfCancellationRequested();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The terminal environment-reporting owner identifier space has been exhausted."
				);
			}

			if ( this.states.TryGetValue(
				TerminalEnvironmentReportingKind.Appearance,
				out FacilityState? current
			) && 0 < current.Owners.Count ) {
				return TerminalControlResult<TerminalAppearanceReportingLease>.Available(
					this.AddAppearanceOwner( current )
				);
			}

			TerminalResponseFrame frame = await this.session.ExecuteQueryAsync(
				TerminalEnvironmentProtocol.CreatePrivateModeQuery( AppearanceMode ),
				TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher( AppearanceMode ),
				timeout,
				cancellationToken
			).ConfigureAwait( false );
			TerminalPrivateModeState baseline =
				TerminalEnvironmentProtocol.ParsePrivateModeState(
					frame,
					AppearanceMode
				);

			if ( baseline is TerminalPrivateModeState.NotRecognized
				or TerminalPrivateModeState.PermanentlyReset ) {
				return TerminalControlResult<TerminalAppearanceReportingLease>.Unavailable(
					"The terminal reported that appearance-change reporting cannot be enabled."
				);
			}

			cancellationToken.ThrowIfCancellationRequested();
			if ( TerminalPrivateModeState.Reset == baseline ) {
				await this.EnableFromResetBaselineAsync(
					AppearanceMode,
					cancellationToken
				).ConfigureAwait( false );
			}

			FacilityState state = new(
				TerminalEnvironmentReportingKind.Appearance,
				AppearanceMode,
				baseline
			);
			this.states[ TerminalEnvironmentReportingKind.Appearance ] = state;
			this.ClearInvalidated();
			return TerminalControlResult<TerminalAppearanceReportingLease>.Available(
				this.AddAppearanceOwner( state )
			);
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask<TerminalControlResult<TerminalInBandResizeReportingLease>>
		AcquireInBandResizeAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken
		) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( long.MaxValue == this.nextOwnerId ) {
				throw new InvalidOperationException(
					"The terminal environment-reporting owner identifier space has been exhausted."
				);
			}
			if ( this.states.TryGetValue(
				TerminalEnvironmentReportingKind.InBandResize,
				out FacilityState? current
			) && 0 < current.Owners.Count ) {
				return TerminalControlResult<TerminalInBandResizeReportingLease>.Available(
					this.AddInBandResizeOwner( current )
				);
			}

			TerminalResponseFrame frame = await this.session.ExecuteQueryAsync(
				TerminalEnvironmentProtocol.CreatePrivateModeQuery( InBandResizeMode ),
				TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher( InBandResizeMode ),
				timeout,
				cancellationToken
			).ConfigureAwait( false );
			TerminalPrivateModeState baseline =
				TerminalEnvironmentProtocol.ParsePrivateModeState(
					frame,
					InBandResizeMode
				);
			if ( baseline is TerminalPrivateModeState.NotRecognized
				or TerminalPrivateModeState.PermanentlyReset ) {
				return TerminalControlResult<TerminalInBandResizeReportingLease>.Unavailable(
					"The terminal reported that in-band resize reporting cannot be enabled."
				);
			}

			cancellationToken.ThrowIfCancellationRequested();
			await this.EnableForAcquisitionAsync(
				InBandResizeMode,
				cleanupToReset: TerminalPrivateModeState.Reset == baseline,
				cancellationToken
			).ConfigureAwait( false );

			FacilityState state = new(
				TerminalEnvironmentReportingKind.InBandResize,
				InBandResizeMode,
				baseline
			);
			this.states[ TerminalEnvironmentReportingKind.InBandResize ] = state;
			this.ClearInvalidated();
			return TerminalControlResult<TerminalInBandResizeReportingLease>.Available(
				this.AddInBandResizeOwner( state )
			);
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReleaseAsync(
		TerminalEnvironmentReportingKind kind,
		long ownerId
	) {
		if ( 0 >= ownerId ) {
			throw new ArgumentOutOfRangeException( nameof( ownerId ) );
		}

		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed
				|| !this.states.TryGetValue( kind, out FacilityState? state )
				|| !state.Owners.TryGetValue(
					ownerId,
					out ITerminalEnvironmentReportingLease? lease
				) ) {
				return;
			}

			if ( 1 == state.Owners.Count
				&& state.BaselineKnown
				&& !this.IsInvalidated
				&& TerminalPrivateModeState.Reset == state.Baseline ) {
				await this.WriteCleanupModeAsync(
					state.Mode,
					enabled: false
				).ConfigureAwait( false );
			}

			state.Owners.Remove( ownerId );
			lease.MarkReleasedByOwner();
			if ( 0 == state.Owners.Count ) {
				this.states.Remove( kind );
			}
		} finally {
			this.gate.Release();
		}
	}

	internal void Invalidate() {
		Volatile.Write( ref this.invalidated, 1 );
	}

	internal async ValueTask CloseAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}

			List<Exception> exceptions = [];
			foreach ( FacilityState state in this.states.Values ) {
				if ( 0 < state.Owners.Count
					&& state.BaselineKnown
					&& !this.IsInvalidated
					&& TerminalPrivateModeState.Reset == state.Baseline ) {
					try {
						await this.WriteCleanupModeAsync(
							state.Mode,
							enabled: false
						).ConfigureAwait( false );
					} catch ( Exception exception ) {
						exceptions.Add( exception );
					}
				}

				foreach ( ITerminalEnvironmentReportingLease lease in state.Owners.Values ) {
					lease.MarkReleasedByOwner();
				}
			}

			this.states.Clear();
			this.closed = true;
			this.ClearInvalidated();
			if ( 1 == exceptions.Count ) {
				throw exceptions[ 0 ];
			}
			if ( 1 < exceptions.Count ) {
				throw new AggregateException(
					"One or more terminal environment-reporting modes could not be restored during closure.",
					exceptions
				);
			}
		} finally {
			this.gate.Release();
		}
	}

	private bool IsInvalidated {
		get {
			return 0 != Volatile.Read( ref this.invalidated );
		}
	}

	private TerminalAppearanceReportingLease AddAppearanceOwner(
		FacilityState state
	) {
		long ownerId = ++this.nextOwnerId;
		TerminalAppearanceReportingLease lease = new( this, ownerId );
		state.Owners.Add( ownerId, lease );
		return lease;
	}

	private TerminalInBandResizeReportingLease AddInBandResizeOwner(
		FacilityState state
	) {
		long ownerId = ++this.nextOwnerId;
		TerminalInBandResizeReportingLease lease = new( this, ownerId );
		state.Owners.Add( ownerId, lease );
		return lease;
	}

	private async ValueTask EnableFromResetBaselineAsync(
		int mode,
		CancellationToken cancellationToken
	) {
		await this.EnableForAcquisitionAsync(
			mode,
			cleanupToReset: true,
			cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask EnableForAcquisitionAsync(
		int mode,
		bool cleanupToReset,
		CancellationToken cancellationToken
	) {
		bool emissionAttempted = false;
		try {
			using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait( false );
			cancellationToken.ThrowIfCancellationRequested();
			emissionAttempted = true;
			await this.session.Output.WriteAsync(
				TerminalEnvironmentProtocol.CreatePrivateModeSet( mode, enabled: true ),
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception acquisitionFailure ) when (
			emissionAttempted && cleanupToReset
		) {
			try {
				await this.WriteCleanupModeAsync(
					mode,
					enabled: false
				).ConfigureAwait( false );
			} catch ( Exception cleanupFailure ) {
				throw new AggregateException(
					"Terminal environment-reporting acquisition failed and restoring the captured reset baseline also failed.",
					acquisitionFailure,
					cleanupFailure
				);
			}

			throw;
		}
	}

	private async ValueTask WriteCleanupModeAsync(
		int mode,
		bool enabled
	) {
		using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		await this.session.Output.WriteAsync(
			TerminalEnvironmentProtocol.CreatePrivateModeSet( mode, enabled ),
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private void ClearInvalidated() {
		Volatile.Write( ref this.invalidated, 0 );
	}

	private sealed class FacilityState(
		TerminalEnvironmentReportingKind kind,
		int mode,
		TerminalPrivateModeState baseline
	) {
		internal TerminalEnvironmentReportingKind Kind {
			get;
		} = kind;

		internal int Mode {
			get;
		} = mode;

		internal TerminalPrivateModeState Baseline {
			get;
		} = baseline;

		internal bool BaselineKnown {
			get;
		} = true;

		internal Dictionary<long, ITerminalEnvironmentReportingLease> Owners {
			get;
		} = [];
	}
}
