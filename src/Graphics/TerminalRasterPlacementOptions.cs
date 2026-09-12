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
/// Configures the cell extents of one persistent raster placement.
/// </summary>
public sealed class TerminalRasterPlacementOptions {
	/// <summary>
	/// Gets or sets the requested placement width in terminal cells, or <see langword="null"/>
	/// to let the terminal derive the width from the image and other placement information.
	/// </summary>
	public int? Columns {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the requested placement height in terminal cells, or <see langword="null"/>
	/// to let the terminal derive the height from the image and other placement information.
	/// </summary>
	public int? Rows {
		get;
		set;
	}

	internal void Validate() {
		ValidateExtent(
			this.Columns,
			nameof( this.Columns )
		);
		ValidateExtent(
			this.Rows,
			nameof( this.Rows )
		);
	}

	private static void ValidateExtent(
		int? value,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( !value.HasValue ) {
			return;
		}
		if ( value.Value is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A persistent raster placement extent must be between 1 and {TerminalRasterImage.MaximumDimension}."
			);
		}
	}
}
