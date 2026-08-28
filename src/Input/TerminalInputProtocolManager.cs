namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Serializes and composes reversible rich-input protocol ownership for one session.
/// </summary>
internal sealed class TerminalInputProtocolManager {
	private const string BracketedPasteEnableCapability = "BE";
	private const string BracketedPasteDisableCapability = "BD";
	private const string BracketedPasteStartCapability = "PS";
	private const string BracketedPasteEndCapability = "PE";
	private const string FocusEnableCapability = "fe";
	private const string FocusDisableCapability = "fd";
	private const string FocusInCapability = "kxIN";
	private const string FocusOutCapability = "kxOUT";
	private const string MouseModeCapability = "XM";
	private const string MouseFormatCapability = "xm";

	private const string SgrMousePrefix = "\u001b[<";
	private const string LegacyMousePrefix = "\u001b[M";
	private const string EnableSgrMouseEncoding = "\u001b[?1006h";
	private const string DisableSgrMouseEncoding = "\u001b[?1006l";
	private const string EnableButtonEvents = "\u001b[?1000h";
	private const string DisableButtonEvents = "\u001b[?1000l";
	private const string EnableButtonMotion = "\u001b[?1002h";
	private const string DisableButtonMotion = "\u001b[?1002l";
	private const string EnableAnyMotion = "\u001b[?1003h";
	private const string DisableAnyMotion = "\u001b[?1003l";

	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly Dictionary<long, LeaseEntry> leases = [];

	private InputProtocolState appliedState = InputProtocolState.Baseline;
	private long nextLeaseId;
	private bool appliedKnown = true;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalInputProtocolManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
	}

	internal async ValueTask<TerminalControlResult<TerminalInputProtocolLease>> AcquireAsync(
		TerminalInputProtocolOptions options,
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
				return TerminalControlResult<TerminalInputProtocolLease>.Unavailable(
					unavailable
				);
			}

			if ( long.MaxValue == this.nextLeaseId ) {
				throw new InvalidOperationException(
					"The terminal input-protocol lease identifier space has been exhausted."
				);
			}

			long leaseId = ++this.nextLeaseId;
			TerminalInputProtocolLease lease = new(
				this,
				leaseId,
				options
			);
			LeaseEntry entry = new(
				options.BracketedPaste,
				options.FocusReporting,
				options.MouseTrackingMode,
				lease
			);
			this.leases.Add( leaseId, entry );

			if ( this.suspended ) {
				return TerminalControlResult<TerminalInputProtocolLease>.Available( lease );
			}

			InputProtocolState desired = this.GetDesiredState();
			bool knownBefore = this.appliedKnown && !this.IsInvalidated;
			InputProtocolState from = knownBefore
				? this.appliedState
				: InputProtocolState.Baseline
			;

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

			return TerminalControlResult<TerminalInputProtocolLease>.Available( lease );
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
				|| !this.leases.TryGetValue( leaseId, out LeaseEntry? entry )
			) {
				return;
			}

			InputProtocolState previousDesired = this.GetDesiredState();
			this.leases.Remove( leaseId );
			InputProtocolState desired = this.GetDesiredState();

			if ( this.suspended ) {
				entry.Lease.MarkReleasedByOwner();
				return;
			}

			bool knownBefore = this.appliedKnown && !this.IsInvalidated;
			InputProtocolState from = knownBefore
				? this.appliedState
				: previousDesired
			;

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

			InputProtocolState from = this.appliedKnown && !this.IsInvalidated
				? this.appliedState
				: this.GetDesiredState()
			;
			Exception? exception = await this.RestoreBaselineBestEffortAsync(
				from
			).ConfigureAwait( false );

			this.suspended = true;
			if ( exception is null ) {
				this.appliedState = InputProtocolState.Baseline;
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

			InputProtocolState desired = this.GetDesiredState();
			if ( InputProtocolState.Baseline == desired ) {
				this.suspended = false;
				this.appliedState = InputProtocolState.Baseline;
				this.appliedKnown = true;
				this.ClearInvalidated();
				return;
			}

			InputProtocolState from = !this.suspended
				&& this.appliedKnown
				&& !this.IsInvalidated
					? this.appliedState
					: InputProtocolState.Baseline
			;

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

			InputProtocolState from;
			if ( this.suspended ) {
				from = InputProtocolState.Baseline;
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
			this.appliedState = InputProtocolState.Baseline;
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
		TerminalInputProtocolOptions options
	) {
		if ( !this.session.OutputObservation.IsTerminal ) {
			return "Rich terminal input protocols require an interactive terminal output endpoint.";
		}

		if ( options.BracketedPaste
			&& !this.HasExtendedStringContract(
				BracketedPasteEnableCapability,
				BracketedPasteDisableCapability,
				BracketedPasteStartCapability,
				BracketedPasteEndCapability
			) ) {
			return string.Concat(
				"Terminal '",
				this.session.Terminal.Name,
				"' does not advertise complete reversible bracketed-paste capabilities."
			);
		}

		if ( options.FocusReporting
			&& !this.HasExtendedStringContract(
				FocusEnableCapability,
				FocusDisableCapability,
				FocusInCapability,
				FocusOutCapability
			) ) {
			return string.Concat(
				"Terminal '",
				this.session.Terminal.Name,
				"' does not advertise complete reversible focus-reporting capabilities."
			);
		}

		if ( options.MouseTrackingMode.HasValue
			&& MouseProtocolKind.None == this.GetMouseProtocol() ) {
			return string.Concat(
				"Terminal '",
				this.session.Terminal.Name,
				"' does not advertise a supported reversible mouse-reporting protocol."
			);
		}

		return null;
	}

	private bool HasExtendedStringContract(
		params string[] names
	) {
		ArgumentNullException.ThrowIfNull( names );

		foreach ( string name in names ) {
			if ( !this.session.Terminal.TryGetExtendedString(
				name,
				out string? value
			) || string.IsNullOrEmpty( value ) ) {
				return false;
			}
		}

		return true;
	}

	private MouseProtocolKind GetMouseProtocol() {
		if ( !this.HasExtendedStringContract(
			MouseModeCapability,
			MouseFormatCapability
		) ) {
			return MouseProtocolKind.None;
		}

		string? keyMouse = this.session.Terminal.GetString(
			StringCapability.KeyMouse
		);
		if ( keyMouse is null ) {
			return MouseProtocolKind.None;
		}
		if ( keyMouse.StartsWith(
			SgrMousePrefix,
			StringComparison.Ordinal
		) ) {
			return MouseProtocolKind.Sgr;
		}
		if ( keyMouse.StartsWith(
			LegacyMousePrefix,
			StringComparison.Ordinal
		) ) {
			return MouseProtocolKind.Legacy;
		}

		return MouseProtocolKind.None;
	}

	private InputProtocolState GetDesiredState() {
		bool bracketedPaste = false;
		bool focusReporting = false;
		TerminalMouseTrackingMode? mouseTrackingMode = null;

		foreach ( LeaseEntry entry in this.leases.Values ) {
			bracketedPaste |= entry.BracketedPaste;
			focusReporting |= entry.FocusReporting;
			if ( entry.MouseTrackingMode.HasValue
				&& (
					!mouseTrackingMode.HasValue
					|| GetMouseTrackingStrength( entry.MouseTrackingMode.Value )
						> GetMouseTrackingStrength( mouseTrackingMode.Value )
				) ) {
				mouseTrackingMode = entry.MouseTrackingMode;
			}
		}

		return new InputProtocolState(
			bracketedPaste,
			focusReporting,
			mouseTrackingMode.HasValue
				? this.GetMouseProtocol()
				: MouseProtocolKind.None,
			mouseTrackingMode
		);
	}

	private async ValueTask TransitionTransactionalAsync(
		InputProtocolState from,
		InputProtocolState to,
		CancellationToken cancellationToken
	) {
		using IDisposable controlOutput = await this.session.AcquireControlOutputAsync(
			cancellationToken
		).ConfigureAwait( false );

		TransitionProgress progress = new( from );
		try {
			bool wrote = await this.ApplyTransitionCoreAsync(
				progress,
				to,
				cancellationToken
			).ConfigureAwait( false );
			if ( wrote ) {
				await this.session.Output.FlushAsync(
					cancellationToken
				).ConfigureAwait( false );
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
					"Terminal input-protocol transition failed and rollback also reported an error.",
					exception,
					rollbackException
				);
			}

			throw;
		}
	}

	private async ValueTask<bool> ApplyTransitionCoreAsync(
		TransitionProgress progress,
		InputProtocolState target,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( progress );

		bool wrote = false;
		InputProtocolState state = progress.State;

		if ( state.MouseTrackingMode.HasValue
			&& (
				state.MouseTrackingMode != target.MouseTrackingMode
				|| state.MouseProtocol != target.MouseProtocol
			) ) {
			await this.WriteAsync(
				GetMouseTrackingSequence(
					state.MouseTrackingMode.Value,
					enabled: false
				),
				cancellationToken
			).ConfigureAwait( false );
			state = state with { MouseTrackingMode = null };
			progress.State = state;
			wrote = true;
		}

		if ( MouseProtocolKind.Sgr == state.MouseProtocol
			&& MouseProtocolKind.Sgr != target.MouseProtocol ) {
			await this.WriteAsync(
				DisableSgrMouseEncoding,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { MouseProtocol = MouseProtocolKind.None };
			progress.State = state;
			wrote = true;
		} else if ( MouseProtocolKind.Legacy == state.MouseProtocol
			&& MouseProtocolKind.Legacy != target.MouseProtocol
			&& !state.MouseTrackingMode.HasValue ) {
			state = state with { MouseProtocol = MouseProtocolKind.None };
			progress.State = state;
		}

		if ( state.FocusReporting && !target.FocusReporting ) {
			await this.WriteAsync(
				this.GetExtendedString( FocusDisableCapability )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { FocusReporting = false };
			progress.State = state;
			wrote = true;
		}

		if ( state.BracketedPaste && !target.BracketedPaste ) {
			await this.WriteAsync(
				this.GetExtendedString( BracketedPasteDisableCapability )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { BracketedPaste = false };
			progress.State = state;
			wrote = true;
		}

		if ( !state.BracketedPaste && target.BracketedPaste ) {
			await this.WriteAsync(
				this.GetExtendedString( BracketedPasteEnableCapability )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { BracketedPaste = true };
			progress.State = state;
			wrote = true;
		}

		if ( !state.FocusReporting && target.FocusReporting ) {
			await this.WriteAsync(
				this.GetExtendedString( FocusEnableCapability )!,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { FocusReporting = true };
			progress.State = state;
			wrote = true;
		}

		if ( MouseProtocolKind.None == state.MouseProtocol
			&& MouseProtocolKind.Sgr == target.MouseProtocol ) {
			await this.WriteAsync(
				EnableSgrMouseEncoding,
				cancellationToken
			).ConfigureAwait( false );
			state = state with { MouseProtocol = MouseProtocolKind.Sgr };
			progress.State = state;
			wrote = true;
		} else if ( MouseProtocolKind.None == state.MouseProtocol
			&& MouseProtocolKind.Legacy == target.MouseProtocol ) {
			state = state with { MouseProtocol = MouseProtocolKind.Legacy };
			progress.State = state;
		}

		if ( !state.MouseTrackingMode.HasValue
			&& target.MouseTrackingMode.HasValue ) {
			await this.WriteAsync(
				GetMouseTrackingSequence(
					target.MouseTrackingMode.Value,
					enabled: true
				),
				cancellationToken
			).ConfigureAwait( false );
			state = state with { MouseTrackingMode = target.MouseTrackingMode };
			progress.State = state;
			wrote = true;
		}

		return wrote;
	}

	private async ValueTask<Exception?> RestoreBaselineBestEffortAsync(
		InputProtocolState from
	) {
		using IDisposable controlOutput = await this.session.AcquireControlOutputAsync(
			CancellationToken.None
		).ConfigureAwait( false );

		List<Exception> exceptions = [];
		bool wrote = false;

		if ( from.MouseTrackingMode.HasValue ) {
			wrote |= await this.TryWriteAsync(
				GetMouseTrackingSequence(
					from.MouseTrackingMode.Value,
					enabled: false
				),
				exceptions
			).ConfigureAwait( false );
		}
		if ( MouseProtocolKind.Sgr == from.MouseProtocol ) {
			wrote |= await this.TryWriteAsync(
				DisableSgrMouseEncoding,
				exceptions
			).ConfigureAwait( false );
		}
		if ( from.FocusReporting ) {
			wrote |= await this.TryWriteAsync(
				this.GetExtendedString( FocusDisableCapability ),
				exceptions
			).ConfigureAwait( false );
		}
		if ( from.BracketedPaste ) {
			wrote |= await this.TryWriteAsync(
				this.GetExtendedString( BracketedPasteDisableCapability ),
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

	private string? GetExtendedString(
		string name
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );

		return this.session.Terminal.TryGetExtendedString(
			name,
			out string? value
		)
			? value
			: null
		;
	}

	private static string GetMouseTrackingSequence(
		TerminalMouseTrackingMode mode,
		bool enabled
	) {
		return mode switch {
			TerminalMouseTrackingMode.ButtonEvents => enabled
				? EnableButtonEvents
				: DisableButtonEvents,
			TerminalMouseTrackingMode.ButtonMotion => enabled
				? EnableButtonMotion
				: DisableButtonMotion,
			TerminalMouseTrackingMode.AnyMotion => enabled
				? EnableAnyMotion
				: DisableAnyMotion,
			_ => throw new ArgumentOutOfRangeException(
				nameof( mode ),
				mode,
				"The terminal mouse tracking mode is not recognized."
			)
		};
	}

	private static int GetMouseTrackingStrength(
		TerminalMouseTrackingMode mode
	) {
		return mode switch {
			TerminalMouseTrackingMode.ButtonEvents => 0,
			TerminalMouseTrackingMode.ButtonMotion => 1,
			TerminalMouseTrackingMode.AnyMotion => 2,
			_ => throw new ArgumentOutOfRangeException(
				nameof( mode ),
				mode,
				"The terminal mouse tracking mode is not recognized."
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
				"Multiple errors occurred while restoring terminal input-protocol state.",
				exceptions
			)
		};
	}

	private sealed class LeaseEntry {
		internal LeaseEntry(
			bool bracketedPaste,
			bool focusReporting,
			TerminalMouseTrackingMode? mouseTrackingMode,
			TerminalInputProtocolLease lease
		) {
			ArgumentNullException.ThrowIfNull( lease );
			this.BracketedPaste = bracketedPaste;
			this.FocusReporting = focusReporting;
			this.MouseTrackingMode = mouseTrackingMode;
			this.Lease = lease;
		}

		internal bool BracketedPaste {
			get;
		}

		internal bool FocusReporting {
			get;
		}

		internal TerminalMouseTrackingMode? MouseTrackingMode {
			get;
		}

		internal TerminalInputProtocolLease Lease {
			get;
		}
	}

	private sealed class TransitionProgress {
		internal TransitionProgress(
			InputProtocolState state
		) {
			this.State = state;
		}

		internal InputProtocolState State {
			get;
			set;
		}
	}

	private enum MouseProtocolKind {
		None,
		Sgr,
		Legacy
	}

	private readonly record struct InputProtocolState(
		bool BracketedPaste,
		bool FocusReporting,
		MouseProtocolKind MouseProtocol,
		TerminalMouseTrackingMode? MouseTrackingMode
	) {
		internal static InputProtocolState Baseline {
			get;
		} = new(
			false,
			false,
			MouseProtocolKind.None,
			null
		);
	}
}
