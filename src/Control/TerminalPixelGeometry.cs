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

using Icod.TermInfo;

/// <summary>
/// Retains one positive pixel width and height observation.
/// </summary>
internal readonly record struct TerminalPixelSize {
	internal TerminalPixelSize(
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

	internal int Width {
		get;
	}

	internal int Height {
		get;
	}
}

/// <summary>
/// Provides exact geometry derivation helpers without fabricating fractional cell sizes.
/// </summary>
internal static class TerminalPixelGeometry {
	internal static bool TryDeriveCellPixelSize(
		TerminalSize characterSize,
		TerminalPixelSize terminalPixelSize,
		out TerminalPixelSize cellPixelSize
	) {
		ArgumentNullException.ThrowIfNull( characterSize );

		cellPixelSize = default;
		if ( 0 >= characterSize.Columns || 0 >= characterSize.Rows ) {
			return false;
		}
		if ( 0 != terminalPixelSize.Width % characterSize.Columns
			|| 0 != terminalPixelSize.Height % characterSize.Rows ) {
			return false;
		}

		int width = terminalPixelSize.Width / characterSize.Columns;
		int height = terminalPixelSize.Height / characterSize.Rows;
		if ( 0 >= width || 0 >= height ) {
			return false;
		}

		cellPixelSize = new TerminalPixelSize(
			width,
			height
		);
		return true;
	}
}
