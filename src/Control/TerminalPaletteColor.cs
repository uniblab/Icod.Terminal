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
/// Represents one indexed terminal-palette color.
/// </summary>
public readonly record struct TerminalPaletteColor {
	/// <summary>
	/// Initializes one indexed terminal-palette color.
	/// </summary>
	/// <param name="index">The palette index in the inclusive range 0 through 255.</param>
	/// <param name="color">The normalized terminal color.</param>
	public TerminalPaletteColor(
		byte index,
		TerminalColor color
	) {
		this.Index = index;
		this.Color = color;
	}

	/// <summary>
	/// Gets the palette index.
	/// </summary>
	public byte Index {
		get;
	}

	/// <summary>
	/// Gets the normalized palette color.
	/// </summary>
	public TerminalColor Color {
		get;
	}
}
