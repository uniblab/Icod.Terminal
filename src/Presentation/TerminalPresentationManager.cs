namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Serializes and composes reversible presentation-state ownership for one session.
/// </summary>
internal sealed class TerminalPresentationManager {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly Dictionary<long, LeaseEntry> leases = [];

	private PresentationState appliedState = PresentationState.Baseline;
	private long nextLeaseId;
	private bool appliedKnown = true;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalPresentationManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
	}

	internal async ValueTask<TerminalControlResult<TerminalPresentationLease>> AcquireAsync(
		TerminalPresentationOptions options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();

			string? unavailable = this.GetUnavailableReason( options );
			if ( unavailable is not null ) {
				return TerminalControlResult<TerminalPresentationLease>.Unavailable(
					unavailable
				);
			}

			if ( long.MaxValue == this.nextLeaseId ) {
				throw new InvalidOperationException(
					"The terminal presentation lease identifier space has been exhausted."
				);
			}

			long leaseId = ++this.nextLeaseId;
			TerminalPresentationLease lease = new(
				this,
				leaseId,
				options
			);
			LeaseEntry entry = new(
				leaseId,
				options.AlternateScreen,
				options.KeypadMode,
				options.CursorVisibility,
				lease
			);
			this.leases.Add( leaseId, entry );

			if ( this.suspended ) {
				return TerminalControlResult<TerminalPresentationLease>.Available( lease );
			}

			PresentationState desired = this.GetDesiredState();
			bool knownBefore = this.appliedKnown && !this.IsInvalidated;
			PresentationState from = knownBefore
				? this.appliedState
				: PresentationState.Baseline;

			try {
				await this.TransitionTransactionalAsync(
					from,
					desired,
					cancellationToken
				).ConfigureAwait( false );
				this.appliedState = desired;
				this.appliedKnown = true;
				this.ClearInvalidated();
			} catch {
				this.leases.Remove( leaseId );
				this.appliedKnown = false;
				this.MarkInvalidated();
				throw;
			}

			return TerminalControlResult<TerminalPresentationLease>.Available( lease );
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReleaseAsync(
		long leaseId
	) {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if (
				this.closed
				|| !this.leases.TryGetValue( leaseId, out LeaseEntry entry )
			) {
				return;
			}

			PresentationState previousDesired = this.GetDesiredState();
			this.leases.Remove( leaseId );
			PresentationState desired = this.GetDesiredState();

			if ( this.suspended ) {
				entry.Lease.MarkReleasedByOwner();
				return;
			}

			bool knownBefore = this.appliedKnown && !this.IsInvalidated;
			PresentationState from = knownBefore
				? this.appliedState
				: previousDesired;

			try {
				await this.TransitionTransactionalAsync(
					from,
					desired,
					CancellationToken.None
				).ConfigureAwait( false );
				this.appliedState = desired;
				this.appliedKnown = true;
				this.ClearInvalidated();
				entry.Lease.MarkReleasedByOwner();
			} catch {
				this.leases.Add( leaseId, entry );
				this.appliedKnown = false;
				this.MarkInvalidated();
				throw;
			}
		} finally {
			this.gate.Release();
		}
	}

	internal void Invalidate() {
		this.MarkInvalidated();
	}

	internal async ValueTask SuspendAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed || this.suspended ) {
				return;
			}

			PresentationState from = this.appliedKnown && !this.IsInvalidated
				? this.appliedState
				: this.GetDesiredState();
			Exception? exception = await this.RestoreBaselineBestEffortAsync(
				from
			).ConfigureAwait( false );

			this.suspended = true;
			if ( exception is null ) {
				this.appliedState = PresentationState.Baseline;
				this.appliedKnown = true;
				this.ClearInvalidated();
				return;
			}

			this.appliedKnown = false;
			this.MarkInvalidated();
			throw exception;
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

			PresentationState desired = this.GetDesiredState();
			if ( PresentationState.Baseline == desired ) {
				this.suspended = false;
				this.appliedState = PresentationState.Baseline;
				this.appliedKnown = true;
				this.ClearInvalidated();
				return;
			}

			PresentationState from = !this.suspended
				&& this.appliedKnown
				&& !this.IsInvalidated
					? this.appliedState
					: PresentationState.Baseline;

			try {
				await this.TransitionTransactionalAsync(
					from,
					desired,
					CancellationToken.None
				).ConfigureAwait( false );
				this.suspended = false;
				this.appliedState = desired;
				this.appliedKnown = true;
				this.ClearInvalidated();
			} catch {
				this.suspended = true;
				this.appliedKnown = false;
				this.MarkInvalidated();
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

			PresentationState from;
			if ( this.suspended ) {
				from = PresentationState.Baseline;
			} else if ( this.appliedKnown && !this.IsInvalidated ) {
				from = this.appliedState;
			} else {
				from = this.GetDesiredState();
			}

			Exception? exception = await this.RestoreBaselineBestEffortAsync(
				from
			).ConfigureAwait( false );

			this.closed = true;
			this.suspended = true;
			this.appliedState = PresentationState.Baseline;
			this.appliedKnown = true;
			this.ClearInvalidated();

			foreach ( LeaseEntry entry in this.leases.Values ) {
				entry.Lease.MarkReleasedByOwner();
			}
			this.leases.Clear();

			if ( exception is not null ) {
				throw exception;
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

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private string? GetUnavailableReason(
		TerminalPresentationOptions options
	) {
		if ( options.AlternateScreen
			&& ( this.GetCapability( StringCapability.EnterCursorAddressingMode ) is null
				|| this.GetCapability( StringCapability.ExitCursorAddressingMode ) is null ) ) {
			return string.Concat(
				"Terminal '",
				this.session.Terminal.Name,
				"' does not advertise reversible alternate-screen capabilities."
			);
		}

		if ( options.KeypadMode
			&& ( this.GetCapability( StringCapability.EnterKeypadMode ) is null
				|| this.GetCapability( StringCapability.ExitKeypadMode ) is null ) ) {
			return string.Concat(
				"Terminal '",
				this.session.Terminal.Name,
				"' does not advertise reversible keypad/application-mode capabilities."
			);
		}

		if ( options.CursorVisibility.HasValue
			&& ( this.GetCursorCapability( options.CursorVisibility.Value ) is null
				|| this.GetBaselineCursorCapability() is null ) ) {
			return string.Concat(
				"Terminal '",
				this.session.Terminal.Name,
				"' does not advertise the requested reversible cursor presentation."
			);
		}

		return null;
	}

	private PresentationState GetDesiredState() {
		bool alternateScreen = false;
		bool keypadMode = false;
		TerminalCursorVisibility? cursorVisibility = null;
		long cursorOwner = long.MinValue;

		foreach ( LeaseEntry entry in this.leases.Values ) {
			alternateScreen |= entry.AlternateScreen;
			keypadMode |= entry.KeypadMode;
			if ( entry.CursorVisibility.HasValue
				&& entry.Id > cursorOwner ) {
				cursorOwner = entry.Id;
				cursorVisibility = entry.CursorVisibility;
			}
		}

		return new PresentationState(
			alternateScreen,
			keypadMode,
			cursorVisibility
		);
	}

	private async ValueTask TransitionTransactionalAsync(
		PresentationState from,
		PresentationState to,
		CancellationToken cancellationToken
	) {
		TransitionProgress progress = new( from );
		try {
			bool wrote = await this.ApplyTransitionCoreAsync(
				progress,
				to,
				cancellationToken
			).ConfigureAwait( false );
			if ( wrote ) {
				await this.session.Output.FlushAsync( cancellationToken ).ConfigureAwait( false );
			}
		} catch ( Exception exception ) {
			Exception? rollbackException = null;
			try {
				TransitionProgress rollbackProgress = new( progress.State );
				bool rollbackWrote = await this.ApplyTransitionCoreAsync(
					rollbackProgress,
					from,
					CancellationToken.None
				).ConfigureAwait( false );
				if ( rollbackWrote ) {
					await this.session.Output.FlushAsync(
						CancellationToken.None
					).ConfigureAwait( false );
				}
			} catch ( Exception rollbackFailure ) {
				rollbackException = rollbackFailure;
			}

			if ( rollbackException is not null ) {
				throw new AggregateException(
					"Terminal presentation transition failed and rollback also reported an error.",
					exception,
					rollbackException
				);
			}

			throw;
		}
	}

	private async ValueTask<bool> ApplyTransitionCoreAsync(
		TransitionProgress progress,
		PresentationState target,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( progress );

		bool wrote = false;
		PresentationState state = progress.State;
		bool structuralChanged = state.AlternateScreen != target.AlternateScreen
			|| state.KeypadMode != target.KeypadMode;

		if ( state.CursorVisibility.HasValue
			&& !target.CursorVisibility.HasValue ) {
			await this.WriteAsync(
				this.GetBaselineCursorCapability()!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { CursorVisibility = null };
			progress.State = state;
			wrote = true;
		}

		if ( state.KeypadMode && !target.KeypadMode ) {
			await this.WriteAsync(
				this.GetCapability( StringCapability.ExitKeypadMode )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { KeypadMode = false };
			progress.State = state;
			wrote = true;
		}

		if ( state.AlternateScreen && !target.AlternateScreen ) {
			await this.WriteAsync(
				this.GetCapability( StringCapability.ExitCursorAddressingMode )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { AlternateScreen = false };
			progress.State = state;
			wrote = true;
		}

		if ( !state.AlternateScreen && target.AlternateScreen ) {
			await this.WriteAsync(
				this.GetCapability( StringCapability.EnterCursorAddressingMode )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { AlternateScreen = true };
			progress.State = state;
			wrote = true;
		}

		if ( !state.KeypadMode && target.KeypadMode ) {
			await this.WriteAsync(
				this.GetCapability( StringCapability.EnterKeypadMode )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { KeypadMode = true };
			progress.State = state;
			wrote = true;
		}

		if ( target.CursorVisibility.HasValue
			&& ( state.CursorVisibility != target.CursorVisibility
				|| structuralChanged ) ) {
			await this.WriteAsync(
				this.GetCursorCapability( target.CursorVisibility.Value )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { CursorVisibility = target.CursorVisibility };
			progress.State = state;
			wrote = true;
		}

		return wrote;
	}

	private async ValueTask<Exception?> RestoreBaselineBestEffortAsync(
		PresentationState from
	) {
		List<Exception> exceptions = [];
		bool wrote = false;

		if ( from.CursorVisibility.HasValue ) {
			wrote |= await this.TryWriteAsync(
				this.GetBaselineCursorCapability(),
				exceptions
			).ConfigureAwait( false );
		}
		if ( from.KeypadMode ) {
			wrote |= await this.TryWriteAsync(
				this.GetCapability( StringCapability.ExitKeypadMode ),
				exceptions
			).ConfigureAwait( false );
		}
		if ( from.AlternateScreen ) {
			wrote |= await this.TryWriteAsync(
				this.GetCapability( StringCapability.ExitCursorAddressingMode ),
				exceptions
			).ConfigureAwait( false );
		}

		if ( wrote ) {
			try {
				await this.session.Output.FlushAsync(
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				exceptions.Add( exception );
			}
		}

		return BuildException( exceptions );
	}

	private async ValueTask<bool> TryWriteAsync(
		string? value,
		ICollection<Exception> exceptions
	) {
		ArgumentNullException.ThrowIfNull( exceptions );
		if ( value is null ) {
			return false;
		}

		try {
			await this.WriteAsync(
				value,
				CancellationToken.None
			).ConfigureAwait( false );
			return true;
		} catch ( Exception exception ) {
			exceptions.Add( exception );
			return false;
		}
	}

	private ValueTask WriteAsync(
		string value,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( value );
		return this.session.WriteTerminalStringAsync(
			value,
			cancellationToken: cancellationToken
		);
	}

	private string? GetCapability(
		StringCapability capability
	) {
		return this.session.Terminal.GetString( capability );
	}

	private string? GetBaselineCursorCapability() {
		return this.GetCapability( StringCapability.CursorNormal )
			?? this.GetCapability( StringCapability.CursorVeryVisible );
	}

	private string? GetCursorCapability(
		TerminalCursorVisibility visibility
	) {
		return visibility switch {
			TerminalCursorVisibility.Hidden =>
				this.GetCapability( StringCapability.CursorInvisible ),
			TerminalCursorVisibility.Normal =>
				this.GetCapability( StringCapability.CursorNormal )
					?? this.GetCapability( StringCapability.CursorVeryVisible ),
			TerminalCursorVisibility.VeryVisible =>
				this.GetCapability( StringCapability.CursorVeryVisible )
					?? this.GetCapability( StringCapability.CursorNormal ),
			_ => throw new ArgumentOutOfRangeException(
				nameof( visibility ),
				visibility,
				"The terminal cursor visibility is not recognized."
			)
		};
	}

	private void MarkInvalidated() {
		Volatile.Write( ref this.invalidated, 1 );
	}

	private void ClearInvalidated() {
		Volatile.Write( ref this.invalidated, 0 );
	}

	private static Exception? BuildException(
		IReadOnlyCollection<Exception> exceptions
	) {
		ArgumentNullException.ThrowIfNull( exceptions );
		return exceptions.Count switch {
			0 => null,
			1 => exceptions.First(),
			_ => new AggregateException(
				"Multiple errors occurred while restoring terminal presentation state.",
				exceptions
			)
		};
	}

	private sealed class LeaseEntry {
		internal LeaseEntry(
			long id,
			bool alternateScreen,
			bool keypadMode,
			TerminalCursorVisibility? cursorVisibility,
			TerminalPresentationLease lease
		) {
			ArgumentNullException.ThrowIfNull( lease );
			this.Id = id;
			this.AlternateScreen = alternateScreen;
			this.KeypadMode = keypadMode;
			this.CursorVisibility = cursorVisibility;
			this.Lease = lease;
		}

		internal long Id {
			get;
		}

		internal bool AlternateScreen {
			get;
		}

		internal bool KeypadMode {
			get;
		}

		internal TerminalCursorVisibility? CursorVisibility {
			get;
		}

		internal TerminalPresentationLease Lease {
			get;
		}
	}

	private sealed class TransitionProgress {
		internal TransitionProgress(
			PresentationState state
		) {
			this.State = state;
		}

		internal PresentationState State {
			get;
			set;
		}
	}

	private readonly record struct PresentationState(
		bool AlternateScreen,
		bool KeypadMode,
		TerminalCursorVisibility? CursorVisibility
	) {
		internal static PresentationState Baseline {
			get;
		} = new(
			false,
			false,
			null
		);
	}
}
