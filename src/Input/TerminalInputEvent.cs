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

	/// <summary>A normalized terminal mouse event.</summary>
	Mouse,

	/// <summary>A terminal focus-in or focus-out event.</summary>
	Focus,

	/// <summary>One framed bracketed-paste event.</summary>
	Paste,

	/// <summary>The terminal input endpoint reached end-of-input or disconnected.</summary>
	EndOfInput
}

/// <summary>
/// Identifies one semantic key-event phase.
/// </summary>
public enum TerminalKeyEventPhase {
	/// <summary>The key was pressed.</summary>
	Press = 0,

	/// <summary>The key press repeated while held.</summary>
	Repeat = 1,

	/// <summary>The key was released.</summary>
	Release = 2
}

/// <summary>
/// Identifies a terminal-independent key recognized by the Terminal input contract.
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
	Function,

	/// <summary>The Caps Lock key.</summary>
	CapsLock,
	/// <summary>The Scroll Lock key.</summary>
	ScrollLock,
	/// <summary>The Num Lock key.</summary>
	NumLock,
	/// <summary>The Print Screen key.</summary>
	PrintScreen,
	/// <summary>The Pause key.</summary>
	Pause,
	/// <summary>The Menu/Application key.</summary>
	Menu,

	/// <summary>Keypad digit 0.</summary>
	Keypad0,
	/// <summary>Keypad digit 1.</summary>
	Keypad1,
	/// <summary>Keypad digit 2.</summary>
	Keypad2,
	/// <summary>Keypad digit 3.</summary>
	Keypad3,
	/// <summary>Keypad digit 4.</summary>
	Keypad4,
	/// <summary>Keypad digit 5.</summary>
	Keypad5,
	/// <summary>Keypad digit 6.</summary>
	Keypad6,
	/// <summary>Keypad digit 7.</summary>
	Keypad7,
	/// <summary>Keypad digit 8.</summary>
	Keypad8,
	/// <summary>Keypad digit 9.</summary>
	Keypad9,
	/// <summary>Keypad decimal separator.</summary>
	KeypadDecimal,
	/// <summary>Keypad division operator.</summary>
	KeypadDivide,
	/// <summary>Keypad multiplication operator.</summary>
	KeypadMultiply,
	/// <summary>Keypad subtraction operator.</summary>
	KeypadSubtract,
	/// <summary>Keypad addition operator.</summary>
	KeypadAdd,
	/// <summary>Keypad Enter.</summary>
	KeypadEnter,
	/// <summary>Keypad equals.</summary>
	KeypadEqual,
	/// <summary>Keypad separator.</summary>
	KeypadSeparator,
	/// <summary>Keypad Left.</summary>
	KeypadLeft,
	/// <summary>Keypad Right.</summary>
	KeypadRight,
	/// <summary>Keypad Up.</summary>
	KeypadUp,
	/// <summary>Keypad Down.</summary>
	KeypadDown,
	/// <summary>Keypad Page Up.</summary>
	KeypadPageUp,
	/// <summary>Keypad Page Down.</summary>
	KeypadPageDown,
	/// <summary>Keypad Home.</summary>
	KeypadHome,
	/// <summary>Keypad End.</summary>
	KeypadEnd,
	/// <summary>Keypad Insert.</summary>
	KeypadInsert,
	/// <summary>Keypad Delete.</summary>
	KeypadDelete,
	/// <summary>Keypad Begin.</summary>
	KeypadBegin,

	/// <summary>Media Play.</summary>
	MediaPlay,
	/// <summary>Media Pause.</summary>
	MediaPause,
	/// <summary>Media Play/Pause.</summary>
	MediaPlayPause,
	/// <summary>Media Reverse.</summary>
	MediaReverse,
	/// <summary>Media Stop.</summary>
	MediaStop,
	/// <summary>Media Fast Forward.</summary>
	MediaFastForward,
	/// <summary>Media Rewind.</summary>
	MediaRewind,
	/// <summary>Next media track.</summary>
	MediaTrackNext,
	/// <summary>Previous media track.</summary>
	MediaTrackPrevious,
	/// <summary>Media Record.</summary>
	MediaRecord,
	/// <summary>Volume Down.</summary>
	VolumeDown,
	/// <summary>Volume Up.</summary>
	VolumeUp,
	/// <summary>Volume Mute.</summary>
	VolumeMute,

	/// <summary>Left Shift.</summary>
	LeftShift,
	/// <summary>Left Control.</summary>
	LeftControl,
	/// <summary>Left Alt.</summary>
	LeftAlt,
	/// <summary>Left Super.</summary>
	LeftSuper,
	/// <summary>Left Hyper.</summary>
	LeftHyper,
	/// <summary>Left Meta.</summary>
	LeftMeta,
	/// <summary>Right Shift.</summary>
	RightShift,
	/// <summary>Right Control.</summary>
	RightControl,
	/// <summary>Right Alt.</summary>
	RightAlt,
	/// <summary>Right Super.</summary>
	RightSuper,
	/// <summary>Right Hyper.</summary>
	RightHyper,
	/// <summary>Right Meta.</summary>
	RightMeta,
	/// <summary>ISO Level 3 Shift.</summary>
	IsoLevel3Shift,
	/// <summary>ISO Level 5 Shift.</summary>
	IsoLevel5Shift,

	/// <summary>A syntactically valid modern key identity not recognized by this library version.</summary>
	Unrecognized
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
	Alt = 4,

	/// <summary>The Super modifier is present.</summary>
	Super = 8,

	/// <summary>The Hyper modifier is present.</summary>
	Hyper = 16,

	/// <summary>The Meta modifier is present.</summary>
	Meta = 32,

	/// <summary>Caps Lock is active.</summary>
	CapsLock = 64,

	/// <summary>Num Lock is active.</summary>
	NumLock = 128
}

/// <summary>
/// Represents one decoded terminal input event.
/// </summary>
public sealed class TerminalInputEvent {
	private TerminalInputEvent(
		TerminalInputEventKind kind,
		TerminalKey key,
		Rune? character,
		Rune? shiftedCharacter,
		Rune? baseLayoutCharacter,
		string? associatedText,
		TerminalKeyModifiers modifiers,
		TerminalKeyEventPhase? keyPhase,
		int? functionKeyNumber,
		TerminalMouseEvent? mouse,
		TerminalFocusEvent? focus,
		TerminalPasteEvent? paste
	) {
		this.Kind = kind;
		this.Key = key;
		this.Character = character;
		this.ShiftedCharacter = shiftedCharacter;
		this.BaseLayoutCharacter = baseLayoutCharacter;
		this.AssociatedText = associatedText;
		this.Modifiers = modifiers;
		this.KeyPhase = keyPhase;
		this.FunctionKeyNumber = functionKeyNumber;
		this.Mouse = mouse;
		this.Focus = focus;
		this.Paste = paste;
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
	/// Gets the Unicode character identity for ordinary text or a character key.
	/// </summary>
	public Rune? Character {
		get;
	}

	/// <summary>Gets the shifted-layout character identity when reported by a modern protocol.</summary>
	public Rune? ShiftedCharacter {
		get;
	}

	/// <summary>Gets the base-layout character identity when reported by a modern protocol.</summary>
	public Rune? BaseLayoutCharacter {
		get;
	}

	/// <summary>Gets associated text produced by a modern key event when reported.</summary>
	public string? AssociatedText {
		get;
	}

	/// <summary>Gets key modifiers.</summary>
	public TerminalKeyModifiers Modifiers {
		get;
	}

	/// <summary>Gets the key-event phase for semantic key events.</summary>
	public TerminalKeyEventPhase? KeyPhase {
		get;
	}

	/// <summary>
	/// Gets the function-key number when <see cref="Key"/> is
	/// <see cref="TerminalKey.Function"/>.
	/// </summary>
	public int? FunctionKeyNumber {
		get;
	}

	/// <summary>
	/// Gets the normalized mouse payload when <see cref="Kind"/> is
	/// <see cref="TerminalInputEventKind.Mouse"/>.
	/// </summary>
	public TerminalMouseEvent? Mouse {
		get;
	}

	/// <summary>
	/// Gets the focus payload when <see cref="Kind"/> is
	/// <see cref="TerminalInputEventKind.Focus"/>.
	/// </summary>
	public TerminalFocusEvent? Focus {
		get;
	}

	/// <summary>
	/// Gets the bracketed-paste payload when <see cref="Kind"/> is
	/// <see cref="TerminalInputEventKind.Paste"/>.
	/// </summary>
	public TerminalPasteEvent? Paste {
		get;
	}

	internal static TerminalInputEvent FromText(
		Rune character
	) {
		return new TerminalInputEvent(
			TerminalInputEventKind.Text,
			TerminalKey.Character,
			character,
			shiftedCharacter: null,
			baseLayoutCharacter: null,
			associatedText: null,
			TerminalKeyModifiers.None,
			keyPhase: null,
			functionKeyNumber: null,
			mouse: null,
			focus: null,
			paste: null
		);
	}

	internal static TerminalInputEvent FromKey(
		TerminalKey key,
		TerminalKeyModifiers modifiers = TerminalKeyModifiers.None,
		Rune? character = null,
		int? functionKeyNumber = null,
		TerminalKeyEventPhase keyPhase = TerminalKeyEventPhase.Press,
		Rune? shiftedCharacter = null,
		Rune? baseLayoutCharacter = null,
		string? associatedText = null
	) {
		if ( !Enum.IsDefined( key ) ) {
			throw new ArgumentOutOfRangeException( nameof( key ) );
		}
		ValidateModifiers( modifiers );
		if ( !Enum.IsDefined( keyPhase ) ) {
			throw new ArgumentOutOfRangeException( nameof( keyPhase ) );
		}

		if ( TerminalKey.Function == key ) {
			if ( functionKeyNumber is < 0 or > 63 ) {
				throw new ArgumentOutOfRangeException( nameof( functionKeyNumber ) );
			}
			if ( !functionKeyNumber.HasValue ) {
				throw new ArgumentNullException( nameof( functionKeyNumber ) );
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
		} else if ( character.HasValue || shiftedCharacter.HasValue || baseLayoutCharacter.HasValue ) {
			throw new ArgumentException(
				"Character identities are valid only for a Character key event.",
				nameof( character )
			);
		}

		if ( TerminalKey.None == key ) {
			throw new ArgumentException(
				"None is not a valid decoded key event.",
				nameof( key )
			);
		}

		if ( associatedText is not null && 0 == associatedText.Length ) {
			throw new ArgumentException(
				"Associated key text must be null or non-empty.",
				nameof( associatedText )
			);
		}

		return new TerminalInputEvent(
			TerminalInputEventKind.Key,
			key,
			character,
			shiftedCharacter,
			baseLayoutCharacter,
			associatedText,
			modifiers,
			keyPhase,
			functionKeyNumber,
			mouse: null,
			focus: null,
			paste: null
		);
	}

	internal static TerminalInputEvent FromMouse(
		TerminalMouseEvent mouse
	) {
		ArgumentNullException.ThrowIfNull( mouse );

		return new TerminalInputEvent(
			TerminalInputEventKind.Mouse,
			TerminalKey.None,
			character: null,
			shiftedCharacter: null,
			baseLayoutCharacter: null,
			associatedText: null,
			TerminalKeyModifiers.None,
			keyPhase: null,
			functionKeyNumber: null,
			mouse,
			focus: null,
			paste: null
		);
	}

	internal static TerminalInputEvent FromFocus(
		TerminalFocusEvent focus
	) {
		ArgumentNullException.ThrowIfNull( focus );

		return new TerminalInputEvent(
			TerminalInputEventKind.Focus,
			TerminalKey.None,
			character: null,
			shiftedCharacter: null,
			baseLayoutCharacter: null,
			associatedText: null,
			TerminalKeyModifiers.None,
			keyPhase: null,
			functionKeyNumber: null,
			mouse: null,
			focus,
			paste: null
		);
	}

	internal static TerminalInputEvent FromPaste(
		TerminalPasteEvent paste
	) {
		ArgumentNullException.ThrowIfNull( paste );

		return new TerminalInputEvent(
			TerminalInputEventKind.Paste,
			TerminalKey.None,
			character: null,
			shiftedCharacter: null,
			baseLayoutCharacter: null,
			associatedText: null,
			TerminalKeyModifiers.None,
			keyPhase: null,
			functionKeyNumber: null,
			mouse: null,
			focus: null,
			paste
		);
	}

	internal static TerminalInputEvent EndOfInput() {
		return new TerminalInputEvent(
			TerminalInputEventKind.EndOfInput,
			TerminalKey.None,
			character: null,
			shiftedCharacter: null,
			baseLayoutCharacter: null,
			associatedText: null,
			TerminalKeyModifiers.None,
			keyPhase: null,
			functionKeyNumber: null,
			mouse: null,
			focus: null,
			paste: null
		);
	}

	internal static void ValidateModifiers(
		TerminalKeyModifiers modifiers
	) {
		const TerminalKeyModifiers known =
			TerminalKeyModifiers.Shift
			| TerminalKeyModifiers.Control
			| TerminalKeyModifiers.Alt
			| TerminalKeyModifiers.Super
			| TerminalKeyModifiers.Hyper
			| TerminalKeyModifiers.Meta
			| TerminalKeyModifiers.CapsLock
			| TerminalKeyModifiers.NumLock;

		if ( 0 != ( modifiers & ~known ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( modifiers ),
				modifiers,
				"The terminal key modifiers contain an unknown flag."
			);
		}
	}
}
