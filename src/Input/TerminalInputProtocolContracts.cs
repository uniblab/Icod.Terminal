namespace Icod.Terminal;

/// <summary>
/// Identifies the requested intensity of terminal mouse tracking.
/// </summary>
public enum TerminalMouseTrackingMode {
	/// <summary>Report mouse button press, release, and wheel activity.</summary>
	ButtonEvents,

	/// <summary>Report button activity plus motion while a button is held.</summary>
	ButtonMotion,

	/// <summary>Report button activity plus all mouse motion.</summary>
	AnyMotion
}

/// <summary>
/// Describes reversible rich-input protocol reporting requested from a live terminal session.
/// </summary>
public sealed class TerminalInputProtocolOptions {
	/// <summary>
	/// Gets or initializes whether bracketed-paste reporting is required.
	/// </summary>
	public bool BracketedPaste {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes whether terminal focus reporting is required.
	/// </summary>
	public bool FocusReporting {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the requested mouse tracking intensity, when any.
	/// </summary>
	public TerminalMouseTrackingMode? MouseTrackingMode {
		get;
		init;
	}

	internal void Validate() {
		if ( this.MouseTrackingMode.HasValue
			&& !Enum.IsDefined( this.MouseTrackingMode.Value ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.MouseTrackingMode ),
				this.MouseTrackingMode.Value,
				"The terminal mouse tracking mode is not recognized."
			);
		}
		if ( !this.BracketedPaste
			&& !this.FocusReporting
			&& !this.MouseTrackingMode.HasValue ) {
			throw new ArgumentException(
				"At least one terminal input protocol must be requested."
			);
		}
	}
}

/// <summary>
/// Owns one reversible terminal rich-input protocol request.
/// </summary>
/// <remarks>
/// Leases may overlap. Bracketed-paste and focus reporting remain active until
/// the last requesting lease is released. Mouse tracking uses the strongest
/// active request; releasing a stronger request deterministically restores the
/// strongest remaining request.
/// </remarks>
public sealed class TerminalInputProtocolLease : IAsyncDisposable {
	private readonly object sync = new();
	private readonly long leaseId;
	private TerminalInputProtocolManager? owner;
	private Task? disposeTask;

	internal TerminalInputProtocolLease(
		TerminalInputProtocolManager owner,
		long leaseId,
		TerminalInputProtocolOptions options
	) {
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( options );

		this.owner = owner;
		this.leaseId = leaseId;
		this.BracketedPaste = options.BracketedPaste;
		this.FocusReporting = options.FocusReporting;
		this.MouseTrackingMode = options.MouseTrackingMode;
	}

	/// <summary>Gets whether this lease requests bracketed-paste reporting.</summary>
	public bool BracketedPaste {
		get;
	}

	/// <summary>Gets whether this lease requests focus reporting.</summary>
	public bool FocusReporting {
		get;
	}

	/// <summary>Gets the mouse tracking request owned by this lease, when any.</summary>
	public TerminalMouseTrackingMode? MouseTrackingMode {
		get;
	}

	/// <summary>
	/// Releases this input-protocol request and applies any remaining outer requests.
	/// </summary>
	/// <returns>A value task representing asynchronous release.</returns>
	public ValueTask DisposeAsync() {
		lock ( this.sync ) {
			if ( this.owner is null ) {
				return ValueTask.CompletedTask;
			}

			this.disposeTask ??= this.DisposeCoreAsync();
			return new ValueTask( this.disposeTask );
		}
	}

	internal void MarkReleasedByOwner() {
		lock ( this.sync ) {
			this.owner = null;
			this.disposeTask = null;
		}
	}

	private async Task DisposeCoreAsync() {
		TerminalInputProtocolManager? currentOwner;
		lock ( this.sync ) {
			currentOwner = this.owner;
		}
		if ( currentOwner is null ) {
			return;
		}

		bool released = false;
		try {
			await currentOwner.ReleaseAsync( this.leaseId ).ConfigureAwait( false );
			released = true;
		} finally {
			lock ( this.sync ) {
				if ( released ) {
					this.owner = null;
				}
				this.disposeTask = null;
			}
		}
	}
}
