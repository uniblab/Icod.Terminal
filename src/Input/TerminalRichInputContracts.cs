namespace Icod.Terminal;

/// <summary>
/// Identifies the semantic action represented by a terminal mouse event.
/// </summary>
public enum TerminalMouseAction {
	/// <summary>A mouse button was pressed.</summary>
	Press,

	/// <summary>A mouse button was released.</summary>
	Release,

	/// <summary>The mouse moved, optionally while a button remained pressed.</summary>
	Move,

	/// <summary>The mouse wheel moved upward.</summary>
	WheelUp,

	/// <summary>The mouse wheel moved downward.</summary>
	WheelDown,

	/// <summary>The mouse wheel moved left.</summary>
	WheelLeft,

	/// <summary>The mouse wheel moved right.</summary>
	WheelRight
}

/// <summary>
/// Identifies a normalized terminal mouse button.
/// </summary>
public enum TerminalMouseButton {
	/// <summary>No button is associated with the event.</summary>
	None,

	/// <summary>The primary mouse button.</summary>
	Primary,

	/// <summary>The middle mouse button.</summary>
	Middle,

	/// <summary>The secondary mouse button.</summary>
	Secondary,

	/// <summary>An additional mouse button.</summary>
	Button4,

	/// <summary>An additional mouse button.</summary>
	Button5,

	/// <summary>An additional mouse button.</summary>
	Button6,

	/// <summary>An additional mouse button.</summary>
	Button7
}

/// <summary>
/// Represents one normalized mouse report using zero-based terminal-cell coordinates.
/// </summary>
public sealed class TerminalMouseEvent {
	/// <summary>
	/// Initializes a normalized terminal mouse event.
	/// </summary>
	/// <param name="action">The semantic mouse action.</param>
	/// <param name="button">
	/// The affected or held button. Movement may use <see cref="TerminalMouseButton.None"/>
	/// when no pressed button is reported. Wheel events use
	/// <see cref="TerminalMouseButton.None"/> because wheel direction is represented by
	/// <paramref name="action"/>.
	/// </param>
	/// <param name="column">The zero-based terminal cell column.</param>
	/// <param name="row">The zero-based terminal cell row.</param>
	/// <param name="modifiers">Keyboard modifiers reported with the mouse event.</param>
	public TerminalMouseEvent(
		TerminalMouseAction action,
		TerminalMouseButton button,
		int column,
		int row,
		TerminalKeyModifiers modifiers = TerminalKeyModifiers.None
	) {
		if ( !Enum.IsDefined( action ) ) {
			throw new ArgumentOutOfRangeException( nameof( action ) );
		}
		if ( !Enum.IsDefined( button ) ) {
			throw new ArgumentOutOfRangeException( nameof( button ) );
		}
		if ( 0 > column ) {
			throw new ArgumentOutOfRangeException( nameof( column ) );
		}
		if ( 0 > row ) {
			throw new ArgumentOutOfRangeException( nameof( row ) );
		}
		ValidateModifiers( modifiers );

		if ( action is TerminalMouseAction.Press or TerminalMouseAction.Release ) {
			if ( TerminalMouseButton.None == button ) {
				throw new ArgumentException(
					"A mouse press or release must identify a button.",
					nameof( button )
				);
			}
		} else if ( action is TerminalMouseAction.WheelUp
			or TerminalMouseAction.WheelDown
			or TerminalMouseAction.WheelLeft
			or TerminalMouseAction.WheelRight ) {
			if ( TerminalMouseButton.None != button ) {
				throw new ArgumentException(
					"A wheel event represents direction in its action and cannot identify a button.",
					nameof( button )
				);
			}
		}

		this.Action = action;
		this.Button = button;
		this.Column = column;
		this.Row = row;
		this.Modifiers = modifiers;
	}

	/// <summary>Gets the normalized mouse action.</summary>
	public TerminalMouseAction Action {
		get;
	}

	/// <summary>Gets the affected or held mouse button.</summary>
	public TerminalMouseButton Button {
		get;
	}

	/// <summary>Gets the zero-based terminal cell column.</summary>
	public int Column {
		get;
	}

	/// <summary>Gets the zero-based terminal cell row.</summary>
	public int Row {
		get;
	}

	/// <summary>Gets keyboard modifiers reported with the mouse event.</summary>
	public TerminalKeyModifiers Modifiers {
		get;
	}

	private static void ValidateModifiers(
		TerminalKeyModifiers modifiers
	) {
		const TerminalKeyModifiers known =
			TerminalKeyModifiers.Shift
			| TerminalKeyModifiers.Control
			| TerminalKeyModifiers.Alt;

		if ( 0 != ( modifiers & ~known ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( modifiers ),
				modifiers,
				"The terminal mouse modifiers contain an unknown flag."
			);
		}
	}
}

/// <summary>
/// Identifies the terminal focus state represented by a focus report.
/// </summary>
public enum TerminalFocusState {
	/// <summary>The terminal gained focus.</summary>
	Focused,

	/// <summary>The terminal lost focus.</summary>
	Unfocused
}

/// <summary>
/// Represents one terminal focus report.
/// </summary>
public sealed class TerminalFocusEvent {
	/// <summary>
	/// Initializes a terminal focus report.
	/// </summary>
	/// <param name="state">The resulting terminal focus state.</param>
	public TerminalFocusEvent(
		TerminalFocusState state
	) {
		if ( !Enum.IsDefined( state ) ) {
			throw new ArgumentOutOfRangeException( nameof( state ) );
		}

		this.State = state;
	}

	/// <summary>Gets the reported terminal focus state.</summary>
	public TerminalFocusState State {
		get;
	}
}

/// <summary>
/// Identifies one phase of a bracketed-paste frame.
/// </summary>
public enum TerminalPastePhase {
	/// <summary>A bracketed paste began.</summary>
	Begin,

	/// <summary>A bounded chunk of decoded paste text is available.</summary>
	Data,

	/// <summary>The bracketed paste ended.</summary>
	End
}

/// <summary>
/// Represents one phase of a bracketed-paste frame.
/// </summary>
public sealed class TerminalPasteEvent {
	/// <summary>
	/// Initializes one bracketed-paste event.
	/// </summary>
	/// <param name="phase">The paste framing phase.</param>
	/// <param name="text">
	/// Paste text for <see cref="TerminalPastePhase.Data"/>; otherwise
	/// <see langword="null"/>.
	/// </param>
	public TerminalPasteEvent(
		TerminalPastePhase phase,
		string? text = null
	) {
		if ( !Enum.IsDefined( phase ) ) {
			throw new ArgumentOutOfRangeException( nameof( phase ) );
		}
		if ( TerminalPastePhase.Data == phase ) {
			if ( string.IsNullOrEmpty( text ) ) {
				throw new ArgumentException(
					"A paste data event must carry non-empty decoded text.",
					nameof( text )
				);
			}
		} else if ( text is not null ) {
			throw new ArgumentException(
				"Paste begin and end events cannot carry text.",
				nameof( text )
			);
		}

		this.Phase = phase;
		this.Text = text;
	}

	/// <summary>Gets the bracketed-paste framing phase.</summary>
	public TerminalPastePhase Phase {
		get;
	}

	/// <summary>
	/// Gets decoded paste text when <see cref="Phase"/> is
	/// <see cref="TerminalPastePhase.Data"/>.
	/// </summary>
	public string? Text {
		get;
	}
}
