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
internal sealed class TerminalEnvironmentReportingManager : ITerminalObservedLifecycleParticipant {
	private const int AppearanceMode = 2031;
	private const int InBandResizeMode = 2048;

	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new(1, 1);
	private readonly Dictionary<TerminalEnvironmentReportingKind, FacilityState>
		states = [];

	private long nextOwnerId;
	private bool closed;
	private long generation;
	private bool suspended;
	private readonly IDisposable lifecycleRegistration;

	internal TerminalEnvironmentReportingManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull(session);
		this.session = session;
		this.lifecycleRegistration = session.RegisterCoreLifecycleParticipant(this);
	}

	internal ValueTask<IDisposable> AcquireCompositionAsync(
		CancellationToken cancellationToken
	) {
		return this.session.AcquireStateCompositionAsync(cancellationToken);
	}

	internal async ValueTask<TerminalControlResult<TerminalAppearanceReportingLease>>
		AcquireAppearanceAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken
		) {
		cancellationToken.ThrowIfCancellationRequested();

		await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try {
			this.ThrowIfClosed();
			if (long.MaxValue == this.nextOwnerId) {
				throw new InvalidOperationException(
					"The terminal environment-reporting owner identifier space has been exhausted."
				);
			}

			if (this.states.TryGetValue(
				TerminalEnvironmentReportingKind.Appearance,
				out FacilityState? current
			) && 0 < current.Owners.Count && this.IsFresh(current)) {
				return TerminalControlResult<TerminalAppearanceReportingLease>.Available(
					this.AddAppearanceOwner(current)
				);
			}

			long observedGeneration = Volatile.Read(ref this.generation);
			TerminalResponseFrame frame = await this.session.ExecuteQueryAsync(
				TerminalEnvironmentProtocol.CreatePrivateModeQuery(AppearanceMode),
				TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher(AppearanceMode),
				timeout,
				cancellationToken
			).ConfigureAwait(false);
			TerminalPrivateModeState baseline =
				TerminalEnvironmentProtocol.ParsePrivateModeState(
					frame,
					AppearanceMode
				);

			if (baseline is TerminalPrivateModeState.NotRecognized
				or TerminalPrivateModeState.PermanentlyReset) {
				return TerminalControlResult<TerminalAppearanceReportingLease>.Unavailable(
					"The terminal reported that appearance-change reporting cannot be enabled."
				);
			}

			this.ValidateGeneration(observedGeneration);
			cancellationToken.ThrowIfCancellationRequested();
			if (TerminalPrivateModeState.Reset == baseline) {
				await this.EnableFromResetBaselineAsync(
					AppearanceMode,
					observedGeneration,
					cancellationToken
				).ConfigureAwait(false);
			}

			this.ValidateGeneration(observedGeneration);
			FacilityState state = current ?? new(TerminalEnvironmentReportingKind.Appearance, AppearanceMode);
			state.Observe(baseline, timeout, observedGeneration, applied: true);
			this.states[TerminalEnvironmentReportingKind.Appearance] = state;
			return TerminalControlResult<TerminalAppearanceReportingLease>.Available(
				this.AddAppearanceOwner(state)
			);
		}
		finally {
			this.gate.Release();
		}
	}

	internal async ValueTask<TerminalControlResult<TerminalInBandResizeReportingLease>>
		AcquireInBandResizeAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken
		) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try {
			this.ThrowIfClosed();
			if (long.MaxValue == this.nextOwnerId) {
				throw new InvalidOperationException(
					"The terminal environment-reporting owner identifier space has been exhausted."
				);
			}
			if (this.states.TryGetValue(
				TerminalEnvironmentReportingKind.InBandResize,
				out FacilityState? current
			) && 0 < current.Owners.Count && this.IsFresh(current)) {
				return TerminalControlResult<TerminalInBandResizeReportingLease>.Available(
					this.AddInBandResizeOwner(current)
				);
			}

			long observedGeneration = Volatile.Read(ref this.generation);
			TerminalResponseFrame frame = await this.session.ExecuteQueryAsync(
				TerminalEnvironmentProtocol.CreatePrivateModeQuery(InBandResizeMode),
				TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher(InBandResizeMode),
				timeout,
				cancellationToken
			).ConfigureAwait(false);
			TerminalPrivateModeState baseline =
				TerminalEnvironmentProtocol.ParsePrivateModeState(
					frame,
					InBandResizeMode
				);
			if (baseline is TerminalPrivateModeState.NotRecognized
				or TerminalPrivateModeState.PermanentlyReset) {
				return TerminalControlResult<TerminalInBandResizeReportingLease>.Unavailable(
					"The terminal reported that in-band resize reporting cannot be enabled."
				);
			}

			this.ValidateGeneration(observedGeneration);
			cancellationToken.ThrowIfCancellationRequested();
			await this.EnableForAcquisitionAsync(
				InBandResizeMode,
				cleanupToReset: TerminalPrivateModeState.Reset == baseline,
				observedGeneration,
				cancellationToken
			).ConfigureAwait(false);

			this.ValidateGeneration(observedGeneration);
			FacilityState state = current ?? new(TerminalEnvironmentReportingKind.InBandResize, InBandResizeMode);
			state.Observe(baseline, timeout, observedGeneration, applied: true);
			this.states[TerminalEnvironmentReportingKind.InBandResize] = state;
			return TerminalControlResult<TerminalInBandResizeReportingLease>.Available(
				this.AddInBandResizeOwner(state)
			);
		}
		finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReleaseAsync(
		TerminalEnvironmentReportingKind kind,
		long ownerId
	) {
		if (0 >= ownerId) {
			throw new ArgumentOutOfRangeException(nameof(ownerId));
		}

		await this.gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
		try {
			if (this.closed
				|| !this.states.TryGetValue(kind, out FacilityState? state)
				|| !state.Owners.TryGetValue(
					ownerId,
					out ITerminalEnvironmentReportingLease? lease
				)) {
				return;
			}

			if (1 == state.Owners.Count
				&& state.Applied
				&& this.IsFresh(state)
				&& TerminalPrivateModeState.Reset == state.Baseline) {
				await this.WriteCleanupModeAsync(
					state.Mode,
					enabled: false,
					state.Generation
				).ConfigureAwait(false);
			}

			state.Owners.Remove(ownerId);
			lease.MarkReleasedByOwner();
			if (0 == state.Owners.Count) {
				this.states.Remove(kind);
			}
		}
		finally {
			this.gate.Release();
		}
	}

	internal void Invalidate() {
		Interlocked.Increment(ref this.generation);
	}

	internal async ValueTask CloseAsync() {
		await this.gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
		try {
			if (this.closed) {
				return;
			}

			List<Exception> exceptions = [];
			foreach (FacilityState state in this.states.Values) {
				if (0 < state.Owners.Count
					&& state.Applied
					&& this.IsFresh(state)
					&& TerminalPrivateModeState.Reset == state.Baseline) {
					try {
						await this.WriteCleanupModeAsync(
							state.Mode,
							enabled: false,
							state.Generation
						).ConfigureAwait(false);
					}
					catch (Exception exception) {
						exceptions.Add(exception);
					}
				}

				foreach (ITerminalEnvironmentReportingLease lease in state.Owners.Values) {
					lease.MarkReleasedByOwner();
				}
			}

			this.states.Clear();
			this.closed = true;
			this.lifecycleRegistration.Dispose();
			if (1 == exceptions.Count) {
				throw exceptions[0];
			}
			if (1 < exceptions.Count) {
				throw new AggregateException(
					"One or more terminal environment-reporting modes could not be restored during closure.",
					exceptions
				);
			}
		}
		finally {
			this.gate.Release();
		}
	}

	private bool IsFresh(FacilityState state) => state.Generation == Volatile.Read(ref this.generation);

	private void ValidateGeneration(long observedGeneration) {
		if (observedGeneration != Volatile.Read(ref this.generation)) {
			throw new InvalidOperationException("Terminal environment reporting was invalidated during negotiation.");
		}
	}

	public async ValueTask PrepareForTerminalSuspendAsync(CancellationToken cancellationToken = default) {
		using IDisposable composition = await this.AcquireCompositionAsync(cancellationToken).ConfigureAwait(false);
		await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try {
			if (this.closed) { return; }
			this.suspended = true;
			List<Exception> exceptions = [];
			foreach (FacilityState state in this.states.Values) {
				if (state.Applied && this.IsFresh(state) && TerminalPrivateModeState.Reset == state.Baseline) {
					try {
						await this.WriteCleanupModeAsync(state.Mode, enabled: false, state.Generation).ConfigureAwait(false);
						state.Applied = false;
					}
					catch (Exception exception) { exceptions.Add(exception); }
				}
			}
			ThrowFailures(exceptions);
		}
		finally { this.gate.Release(); }
	}

	public async ValueTask RefreshAfterTerminalResumeAsync(CancellationToken cancellationToken = default) {
		FacilityState[] active;
		long observedGeneration;
		using (IDisposable composition = await this.AcquireCompositionAsync(cancellationToken).ConfigureAwait(false)) {
			await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try {
				if (this.closed) { return; }
				this.suspended = true;
				observedGeneration = Volatile.Read(ref this.generation);
				active = this.states.Values.Where(static state => 0 < state.Owners.Count).ToArray();
				foreach (FacilityState state in active) { state.Generation = -1; state.Applied = false; }
			}
			finally { this.gate.Release(); }
		}

		// Replies are awaited outside the state and manager gates. A lease may be
		// retired or the session disposed while this bounded observation is pending.
		List<Exception> exceptions = [];
		foreach (FacilityState state in active) {
			try {
				TerminalResponseFrame frame = await this.session.ExecuteLifecycleObservationQueryAsync(
					TerminalEnvironmentProtocol.CreatePrivateModeQuery(state.Mode),
					TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher(state.Mode),
					state.Timeout, cancellationToken
				).ConfigureAwait(false);
				TerminalPrivateModeState baseline = TerminalEnvironmentProtocol.ParsePrivateModeState(frame, state.Mode);
				if (baseline is TerminalPrivateModeState.NotRecognized or TerminalPrivateModeState.PermanentlyReset) {
					throw new InvalidOperationException("An owned terminal environment-reporting mode cannot resume.");
				}
				using IDisposable composition = await this.AcquireCompositionAsync(cancellationToken).ConfigureAwait(false);
				await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
				try {
					if (this.closed) { return; }
					this.ValidateGeneration(observedGeneration);
					if (this.states.TryGetValue(state.Kind, out FacilityState? current) && ReferenceEquals(current, state) && 0 < state.Owners.Count) {
						state.Observe(baseline, state.Timeout, observedGeneration, applied: false);
					}
				}
				finally { this.gate.Release(); }
			}
			catch (Exception exception) { exceptions.Add(exception); }
		}
		ThrowFailures(exceptions);
	}

	public async ValueTask ResumeAfterTerminalSuspendAsync(CancellationToken cancellationToken = default) {
		using IDisposable composition = await this.AcquireCompositionAsync(cancellationToken).ConfigureAwait(false);
		await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try {
			if (this.closed) { return; }
			foreach (FacilityState state in this.states.Values) {
				if (!this.IsFresh(state)) { throw new InvalidOperationException("Terminal environment-reporting baseline is unavailable after resume."); }
				if (TerminalPrivateModeState.Reset == state.Baseline || TerminalEnvironmentReportingKind.InBandResize == state.Kind) {
					// Commitment may have changed the remote mode even when emission fails.
					state.Applied = true;
					await this.WriteCleanupModeAsync(state.Mode, enabled: true, state.Generation).ConfigureAwait(false);
				}
				this.ValidateGeneration(state.Generation);
				state.Applied = true;
			}
			this.suspended = false;
		}
		finally { this.gate.Release(); }
	}

	private static void ThrowFailures(List<Exception> exceptions) {
		if (1 == exceptions.Count) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw(); }
		if (1 < exceptions.Count) { throw new AggregateException("Terminal environment reporting restoration failed.", exceptions); }
	}

	private TerminalAppearanceReportingLease AddAppearanceOwner(
		FacilityState state
	) {
		long ownerId = ++this.nextOwnerId;
		TerminalAppearanceReportingLease lease = new(this, ownerId);
		state.Owners.Add(ownerId, lease);
		return lease;
	}

	private TerminalInBandResizeReportingLease AddInBandResizeOwner(
		FacilityState state
	) {
		long ownerId = ++this.nextOwnerId;
		TerminalInBandResizeReportingLease lease = new(this, ownerId);
		state.Owners.Add(ownerId, lease);
		return lease;
	}

	private async ValueTask EnableFromResetBaselineAsync(
		int mode,
		long observedGeneration,
		CancellationToken cancellationToken
	) {
		await this.EnableForAcquisitionAsync(
			mode,
			cleanupToReset: true,
			observedGeneration,
			cancellationToken
		).ConfigureAwait(false);
	}

	private async ValueTask EnableForAcquisitionAsync(
		int mode,
		bool cleanupToReset,
		long writeGeneration,
		CancellationToken cancellationToken
	) {
		bool emissionAttempted = false;
		try {
			using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			this.ValidateGeneration(writeGeneration);
			emissionAttempted = true;
			await this.session.Output.WriteAsync(
				TerminalEnvironmentProtocol.CreatePrivateModeSet(mode, enabled: true),
				CancellationToken.None
			).ConfigureAwait(false);
		}
		catch (Exception acquisitionFailure) when (
			emissionAttempted && cleanupToReset
			&& writeGeneration == Volatile.Read(ref this.generation)
		) {
			try {
				await this.WriteCleanupModeAsync(
					mode,
					enabled: false,
					writeGeneration
				).ConfigureAwait(false);
			}
			catch (Exception cleanupFailure) {
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
		bool enabled,
		long observedGeneration
	) {
		using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
			CancellationToken.None
		).ConfigureAwait(false);
		// Invalidation may occur while waiting for the output gate. An old
		// observation no longer authorizes restoration or re-entry.
		if (observedGeneration != Volatile.Read(ref this.generation)) {
			if (enabled) { this.ValidateGeneration(observedGeneration); }
			return;
		}
		await this.session.Output.WriteAsync(
			TerminalEnvironmentProtocol.CreatePrivateModeSet(mode, enabled),
			CancellationToken.None
		).ConfigureAwait(false);
	}

	private void ThrowIfClosed() {
		if (this.suspended) {
			throw new InvalidOperationException("Terminal environment reporting cannot be acquired while suspended.");
		}
		if (this.closed) {
			throw new ObjectDisposedException(nameof(TerminalSession));
		}
	}

	private sealed class FacilityState(
		TerminalEnvironmentReportingKind kind,
		int mode
	) {
		internal TerminalEnvironmentReportingKind Kind {
			get;
		} = kind;

		internal int Mode {
			get;
		} = mode;

		internal TerminalPrivateModeState Baseline {
			get; set;
		}

		internal long Generation { get; set; } = -1;
		internal bool Applied { get; set; }
		internal TimeSpan Timeout { get; private set; }
		internal void Observe(TerminalPrivateModeState baseline, TimeSpan timeout, long generation, bool applied) {
			this.Baseline = baseline; this.Timeout = timeout; this.Generation = generation; this.Applied = applied;
		}

		internal Dictionary<long, ITerminalEnvironmentReportingLease> Owners {
			get;
		} = [];
	}
}
