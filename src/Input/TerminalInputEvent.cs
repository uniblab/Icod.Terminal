namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Identifies the semantic form of one decoded terminal-input event.
/// </summary>
public enum TerminalInputEventKind {
	/// <summary>Ordinary Unicode text input.</summary>
	Text,

	/// <summary>A named key or modified character key.</summary>
	Key,

	/// <summary>The terminal input endpoint reached end-of-input or disconnected.</summary>
	EndOfInput
}

/// <summary>
/// Identifies a terminal-independent key recognized by the 0.1 input contract.
/// </summary>
public enum TerminalKey {
	/// <summary>No key is associated with this event.</summary>
	None,

	/// <summary>A printable or control-modified character.</summary>
	Character,

	/// <summary>The Enter key.</summary>
	Enter,

	/// <summary>The Space key.</summary>
	Space,

	/// <summary>The Escape key.</summary>
	Escape,

	/// <summary>The Backspace key.</summary>
	Backspace,

	/// <summary>The Tab key.</summary>
	Tab,

	/// <summary>The up-arrow key.</summary>
	Up,

	/// <summary>The down-arrow key.</summary>
	Down,

	/// <summary>The left-arrow key.</summary>
	Left,

	/// <summary>The right-arrow key.</summary>
	Right,

	/// <summary>The Home key.</summary>
	Home,

	/// <summary>The End key.</summary>
	End,

	/// <summary>The Page Up key.</summary>
	PageUp,

	/// <summary>The Page Down key.</summary>
	PageDown,

	/// <summary>The Insert key.</summary>
	Insert,

	/// <summary>The Delete key.</summary>
	Delete,

	/// <summary>A numbered function key.</summary>
	Function
}

/// <summary>
/// Identifies modifiers carried by a decoded terminal key event.
/// </summary>
[Flags]
public enum TerminalKeyModifiers {
	/// <summary>No modifier is present.</summary>
	None = 0,

	/// <summary>The Shift modifier is present.</summary>
	Shift = 1,

	/// <summary>The Control modifier is present.</summary>
	Control = 2,

	/// <summary>The Alt modifier is present.</summary>
	Alt = 4
}

/// <summary>
/// Represents one decoded terminal keyboard or end-of-input event.
/// </summary>
public sealed class TerminalInputEvent {
	private TerminalInputEvent(
		TerminalInputEventKind kind,
		TerminalKey key,
		Rune? character,
		TerminalKeyModifiers modifiers,
		int? functionKeyNumber
	) {
		this.Kind = kind;
		this.Key = key;
		this.Character = character;
		this.Modifiers = modifiers;
		this.FunctionKeyNumber = functionKeyNumber;
	}

	/// <summary>Gets the semantic input-event kind.</summary>
	public TerminalInputEventKind Kind {
		get;
	}

	/// <summary>
	/// Gets the terminal-independent key. Text input uses
	/// <see cref="TerminalKey.Character"/>.
	/// </summary>
	public TerminalKey Key {
		get;
	}

	/// <summary>
	/// Gets the Unicode character for ordinary text or a control-modified character key.
	/// </summary>
	public Rune? Character {
		get;
	}

	/// <summary>Gets key modifiers.</summary>
	public TerminalKeyModifiers Modifiers {
		get;
	}

	/// <summary>
	/// Gets the function-key number when <see cref="Key"/> is
	/// <see cref="TerminalKey.Function"/>.
	/// </summary>
	public int? FunctionKeyNumber {
		get;
	}

	internal static TerminalInputEvent FromText(
		Rune character
	) {
		return new TerminalInputEvent(
			TerminalInputEventKind.Text,
			TerminalKey.Character,
			character,
			TerminalKeyModifiers.None,
			null
		);
	}

	internal static TerminalInputEvent FromKey(
		TerminalKey key,
		TerminalKeyModifiers modifiers = TerminalKeyModifiers.None,
		Rune? character = null,
		int? functionKeyNumber = null
	) {
		if ( !Enum.IsDefined( key ) ) {
			throw new ArgumentOutOfRangeException( nameof( key ) );
		}
		ValidateModifiers( modifiers );

		if ( TerminalKey.Function == key ) {
			if ( functionKeyNumber is < 0 or > 63 ) {
				throw new ArgumentOutOfRangeException( nameof( functionKeyNumber ) );
			}
			if ( !functionKeyNumber.HasValue ) {
				throw new ArgumentNullException( nameof( functionKeyNumber ) );
			}
			if ( character.HasValue ) {
				throw new ArgumentException(
					"A function-key event cannot carry a character.",
					nameof( character )
				);
			}
		} else if ( functionKeyNumber.HasValue ) {
			throw new ArgumentException(
				"A function-key number is valid only for a Function key event.",
				nameof( functionKeyNumber )
			);
		}

		if ( TerminalKey.Character == key ) {
			if ( !character.HasValue ) {
				throw new ArgumentNullException( nameof( character ) );
			}
		} else if ( character.HasValue ) {
			throw new ArgumentException(
				"A character is valid only for a Character key event.",
				nameof( character )
			);
		}

		if ( TerminalKey.None == key ) {
			throw new ArgumentException(
				"None is not a valid decoded key event.",
				nameof( key )
			);
		}

		return new TerminalInputEvent(
			TerminalInputEventKind.Key,
			key,
			character,
			modifiers,
			functionKeyNumber
		);
	}

	internal static TerminalInputEvent EndOfInput() {
		return new TerminalInputEvent(
			TerminalInputEventKind.EndOfInput,
			TerminalKey.None,
			null,
			TerminalKeyModifiers.None,
			null
		);
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
				"The terminal key modifiers contain an unknown flag."
			);
		}
	}
}
