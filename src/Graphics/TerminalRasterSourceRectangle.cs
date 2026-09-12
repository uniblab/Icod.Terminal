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
/// Identifies a bounded rectangular region of source raster pixels for one persistent placement.
/// </summary>
public readonly struct TerminalRasterSourceRectangle {
	/// <summary>
	/// Initializes a source-pixel rectangle.
	/// </summary>
	/// <param name="x">The zero-based source-pixel column.</param>
	/// <param name="y">The zero-based source-pixel row.</param>
	/// <param name="width">The positive source-pixel width.</param>
	/// <param name="height">The positive source-pixel height.</param>
	public TerminalRasterSourceRectangle(
		int x,
		int y,
		int width,
		int height
	) {
		ValidateCoordinate(
			x,
			nameof( x )
		);
		ValidateCoordinate(
			y,
			nameof( y )
		);
		ValidateExtent(
			width,
			nameof( width )
		);
		ValidateExtent(
			height,
			nameof( height )
		);

		this.X = x;
		this.Y = y;
		this.Width = width;
		this.Height = height;
	}

	/// <summary>
	/// Gets the zero-based source-pixel column.
	/// </summary>
	public int X {
		get;
	}

	/// <summary>
	/// Gets the zero-based source-pixel row.
	/// </summary>
	public int Y {
		get;
	}

	/// <summary>
	/// Gets the source-pixel width.
	/// </summary>
	public int Width {
		get;
	}

	/// <summary>
	/// Gets the source-pixel height.
	/// </summary>
	public int Height {
		get;
	}

	private static void ValidateCoordinate(
		int value,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( value is < 0 or >= TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A raster source coordinate must be between 0 and {TerminalRasterImage.MaximumDimension - 1}."
			);
		}
	}

	private static void ValidateExtent(
		int value,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( value is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A raster source extent must be between 1 and {TerminalRasterImage.MaximumDimension}."
			);
		}
	}
}
