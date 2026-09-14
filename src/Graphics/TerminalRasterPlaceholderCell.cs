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

/// <summary>
/// Represents one semantic terminal-cell token belonging to an opaque raster placeholder.
/// </summary>
public readonly struct TerminalRasterPlaceholderCell {
	private readonly TerminalRasterPlaceholder? owner;

	internal TerminalRasterPlaceholderCell(
		TerminalRasterPlaceholder owner,
		int row,
		int column
	) {
		ArgumentNullException.ThrowIfNull( owner );
		this.owner = owner;
		this.Row = row;
		this.Column = column;
	}

	/// <summary>
	/// Gets the zero-based row within the owning raster placeholder.
	/// </summary>
	public int Row {
		get;
	}

	/// <summary>
	/// Gets the zero-based column within the owning raster placeholder.
	/// </summary>
	public int Column {
		get;
	}

	internal TerminalRasterPlaceholder? Owner {
		get {
			return this.owner;
		}
	}
}
