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
	Alert
}
