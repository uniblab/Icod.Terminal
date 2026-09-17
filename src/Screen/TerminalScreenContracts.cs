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

/// <summary>Represents one zero-based terminal-screen position.</summary>
public readonly record struct TerminalScreenPosition {
	/// <summary>Initializes a zero-based terminal-screen position.</summary>
	public TerminalScreenPosition(
		int row,
		int column
	) {
		if ( 0 > row ) {
			throw new ArgumentOutOfRangeException( nameof( row ) );
		}
		if ( 0 > column ) {
			throw new ArgumentOutOfRangeException( nameof( column ) );
		}

		this.Row = row;
		this.Column = column;
	}

	/// <summary>Gets the zero-based row.</summary>
	public int Row {
		get;
	}

	/// <summary>Gets the zero-based column.</summary>
	public int Column {
		get;
	}
}

/// <summary>Selects the preferred terminal alert presentation.</summary>
public enum TerminalAlertKind {
	/// <summary>Prefer an audible alert and fall back to a visual alert.</summary>
	Audible,

	/// <summary>Prefer a visual alert and fall back to an audible alert.</summary>
	Visual
}

/// <summary>Identifies the semantic purpose of an opaque screen-operation plan.</summary>
public enum TerminalScreenOperationKind {
	/// <summary>Move the terminal text cursor.</summary>
	CursorMove,

	/// <summary>Produce an audible or visual terminal alert.</summary>
	Alert,

	/// <summary>Change terminal text rendition.</summary>
	Rendition,

	/// <summary>Enter or leave the terminal alternate character set.</summary>
	AlternateCharacterSet,

	/// <summary>Erase a semantic terminal-screen region.</summary>
	Erase,

	/// <summary>Insert or delete terminal character positions.</summary>
	CharacterShift,

	/// <summary>Insert, delete, or scroll terminal lines.</summary>
	LineShift,

	/// <summary>Change the terminal scrolling region.</summary>
	ScrollRegion
}

/// <summary>Identifies one semantic single-line box-drawing glyph.</summary>
public enum TerminalLineGlyph {
	/// <summary>Horizontal line.</summary>
	Horizontal,

	/// <summary>Vertical line.</summary>
	Vertical,

	/// <summary>Upper-left corner.</summary>
	UpperLeftCorner,

	/// <summary>Upper-right corner.</summary>
	UpperRightCorner,

	/// <summary>Lower-left corner.</summary>
	LowerLeftCorner,

	/// <summary>Lower-right corner.</summary>
	LowerRightCorner,

	/// <summary>T-junction whose branch extends upward.</summary>
	TeeUp,

	/// <summary>T-junction whose branch extends downward.</summary>
	TeeDown,

	/// <summary>T-junction whose branch extends leftward.</summary>
	TeeLeft,

	/// <summary>T-junction whose branch extends rightward.</summary>
	TeeRight,

	/// <summary>Four-way crossing.</summary>
	Crossing
}

/// <summary>Represents one terminal-provided physical form of a semantic line glyph.</summary>
public readonly record struct TerminalLineGlyphRepresentation {
	internal TerminalLineGlyphRepresentation(
		string content,
		bool usesAlternateCharacterSet
	) {
		ArgumentException.ThrowIfNullOrEmpty( content );
		this.Content = content;
		this.UsesAlternateCharacterSet = usesAlternateCharacterSet;
	}

	/// <summary>Gets the application text to emit for the glyph.</summary>
	public string Content {
		get;
	}

	/// <summary>Gets whether the content must be emitted inside alternate-character-set mode.</summary>
	public bool UsesAlternateCharacterSet {
		get;
	}
}

/// <summary>Identifies a semantic terminal erase operation.</summary>
public enum TerminalScreenEraseKind {
	/// <summary>Erase from the cursor through the end of the line.</summary>
	ToEndOfLine,

	/// <summary>Erase from the beginning of the line through the cursor.</summary>
	ToBeginningOfLine,

	/// <summary>Erase from the cursor through the end of the screen.</summary>
	ToEndOfScreen,

	/// <summary>Erase the complete screen.</summary>
	Screen
}

/// <summary>Identifies a semantic character-position shift.</summary>
public enum TerminalScreenCharacterShiftKind {
	/// <summary>Insert blank character positions at the cursor.</summary>
	Insert,

	/// <summary>Delete character positions at the cursor.</summary>
	Delete,

	/// <summary>Erase character positions without shifting remaining content.</summary>
	Erase
}

/// <summary>Identifies a semantic terminal-line shift.</summary>
public enum TerminalScreenLineShiftKind {
	/// <summary>Insert lines at the cursor row.</summary>
	Insert,

	/// <summary>Delete lines at the cursor row.</summary>
	Delete,

	/// <summary>Scroll content forward.</summary>
	ScrollForward,

	/// <summary>Scroll content in reverse.</summary>
	ScrollReverse
}
