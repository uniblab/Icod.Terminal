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
/// Retains one positive pixel width and height observation.
/// </summary>
public readonly record struct TerminalPixelDimensions {
	/// <summary>Initializes positive pixel dimensions.</summary>
	public TerminalPixelDimensions(
		int width,
		int height
	) {
		if ( 0 >= width ) {
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}
		if ( 0 >= height ) {
			throw new ArgumentOutOfRangeException( nameof( height ) );
		}

		this.Width = width;
		this.Height = height;
	}

	/// <summary>Gets the width in pixels.</summary>
	public int Width {
		get;
	}

	/// <summary>Gets the height in pixels.</summary>
	public int Height {
		get;
	}
}

/// <summary>
/// Provides exact geometry derivation helpers without fabricating fractional cell sizes.
/// </summary>
public static class TerminalPixelGeometry {
	/// <summary>
	/// Attempts to derive exact character-cell pixel dimensions from one terminal
	/// character-grid observation and one terminal pixel observation.
	/// </summary>
	/// <remarks>
	/// This method performs no terminal I/O and never rounds or guesses. The two
	/// observations are caller-selected and are not guaranteed to be simultaneous.
	/// </remarks>
	public static bool TryDeriveCellDimensions(
		TerminalDimensions terminalDimensions,
		TerminalPixelDimensions terminalPixelDimensions,
		out TerminalPixelDimensions cellPixelDimensions
	) {
		cellPixelDimensions = default;
		if ( 0 >= terminalDimensions.Columns || 0 >= terminalDimensions.Rows
			|| 0 >= terminalPixelDimensions.Width || 0 >= terminalPixelDimensions.Height ) {
			return false;
		}
		if ( 0 != terminalPixelDimensions.Width % terminalDimensions.Columns
			|| 0 != terminalPixelDimensions.Height % terminalDimensions.Rows ) {
			return false;
		}

		int width = terminalPixelDimensions.Width / terminalDimensions.Columns;
		int height = terminalPixelDimensions.Height / terminalDimensions.Rows;
		if ( 0 >= width || 0 >= height ) {
			return false;
		}

		cellPixelDimensions = new TerminalPixelDimensions(
			width,
			height
		);
		return true;
	}
}
