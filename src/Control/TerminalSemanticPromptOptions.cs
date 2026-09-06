namespace Icod.Terminal;

/// <summary>
/// Identifies whether an OSC 133 prompt marker describes a primary or secondary prompt.
/// </summary>
public enum TerminalSemanticPromptKind {
	/// <summary>The primary/default prompt.</summary>
	Primary = 0,
	/// <summary>A secondary continuation prompt, emitted as <c>k=s</c>.</summary>
	Secondary = 1
}

/// <summary>
/// Describes shell prompt redraw behavior published through OSC 133 prompt metadata.
/// </summary>
public enum TerminalSemanticPromptResizeBehavior {
	/// <summary>Publishes no redraw metadata.</summary>
	Unspecified = 0,
	/// <summary>Declares that the shell does not redraw the prompt after a resize.</summary>
	ShellDoesNotRedrawPrompt = 1
}

/// <summary>
/// Describes OSC 133 prompt-click coordinate metadata.
/// </summary>
public enum TerminalSemanticPromptClickMode {
	/// <summary>Publishes no prompt-click metadata.</summary>
	None = 0,
	/// <summary>Requests absolute Y-coordinate prompt click events.</summary>
	Absolute = 1,
	/// <summary>Requests Y coordinates relative to the current prompt.</summary>
	Relative = 2
}

/// <summary>
/// Represents validated extended metadata for one OSC 133 prompt-start marker.
/// </summary>
/// <remarks>
/// The default value is valid and is equivalent to the portable bare OSC 133 <c>A</c> marker.
/// Extended metadata is emitted only when explicitly represented by these options. This type does
/// not probe terminal support or enable mouse/key protocols.
/// </remarks>
public readonly struct TerminalSemanticPromptOptions {
	/// <summary>
	/// Initializes extended OSC 133 prompt-start metadata.
	/// </summary>
	/// <param name="kind">Whether the prompt is primary or secondary.</param>
	/// <param name="resizeBehavior">The shell prompt redraw declaration.</param>
	/// <param name="useSpecialCursorKey">Whether the shell declares support for the special cursor-motion key.</param>
	/// <param name="clickMode">The requested prompt-click coordinate mode.</param>
	/// <exception cref="ArgumentOutOfRangeException">One of the enum values is undefined.</exception>
	public TerminalSemanticPromptOptions(
		TerminalSemanticPromptKind kind = TerminalSemanticPromptKind.Primary,
		TerminalSemanticPromptResizeBehavior resizeBehavior = TerminalSemanticPromptResizeBehavior.Unspecified,
		bool useSpecialCursorKey = false,
		TerminalSemanticPromptClickMode clickMode = TerminalSemanticPromptClickMode.None
	) {
		ValidateKind( kind );
		ValidateResizeBehavior( resizeBehavior );
		ValidateClickMode( clickMode );

		this.Kind = kind;
		this.ResizeBehavior = resizeBehavior;
		this.UseSpecialCursorKey = useSpecialCursorKey;
		this.ClickMode = clickMode;
	}

	/// <summary>Gets whether this marker describes a primary or secondary prompt.</summary>
	public TerminalSemanticPromptKind Kind {
		get;
	}

	/// <summary>Gets the shell prompt redraw declaration.</summary>
	public TerminalSemanticPromptResizeBehavior ResizeBehavior {
		get;
	}

	/// <summary>Gets whether the shell declares support for the special cursor-motion key.</summary>
	public bool UseSpecialCursorKey {
		get;
	}

	/// <summary>Gets the prompt-click coordinate mode.</summary>
	public TerminalSemanticPromptClickMode ClickMode {
		get;
	}

	internal void Validate() {
		ValidateKind( this.Kind );
		ValidateResizeBehavior( this.ResizeBehavior );
		ValidateClickMode( this.ClickMode );
	}

	private static void ValidateKind(
		TerminalSemanticPromptKind kind
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"The OSC 133 semantic prompt kind is not defined."
			);
		}
	}

	private static void ValidateResizeBehavior(
		TerminalSemanticPromptResizeBehavior resizeBehavior
	) {
		if ( !Enum.IsDefined( resizeBehavior ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( resizeBehavior ),
				resizeBehavior,
				"The OSC 133 semantic prompt resize behavior is not defined."
			);
		}
	}

	private static void ValidateClickMode(
		TerminalSemanticPromptClickMode clickMode
	) {
		if ( !Enum.IsDefined( clickMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( clickMode ),
				clickMode,
				"The OSC 133 semantic prompt click mode is not defined."
			);
		}
	}
}
