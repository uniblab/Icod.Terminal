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

/// <summary>Represents positive terminal dimensions measured in character cells.</summary>
public readonly record struct TerminalDimensions {
	/// <summary>Initializes terminal dimensions measured in character cells.</summary>
	public TerminalDimensions(
		int columns,
		int rows
	) {
		if ( 0 >= columns ) {
			throw new ArgumentOutOfRangeException( nameof( columns ) );
		}
		if ( 0 >= rows ) {
			throw new ArgumentOutOfRangeException( nameof( rows ) );
		}

		this.Columns = columns;
		this.Rows = rows;
	}

	/// <summary>Gets the number of terminal character columns.</summary>
	public int Columns {
		get;
	}

	/// <summary>Gets the number of terminal character rows.</summary>
	public int Rows {
		get;
	}
}
