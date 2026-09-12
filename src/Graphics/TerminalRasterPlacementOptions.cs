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
/// Configures the geometry of one persistent raster placement.
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

	/// <summary>
	/// Gets or sets the source-pixel rectangle to display, or <see langword="null"/>
	/// to place the complete source raster.
	/// </summary>
	public TerminalRasterSourceRectangle? SourceRectangle {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the signed placement z-order, or <see langword="null"/> to use the
	/// terminal backend's default placement order.
	/// </summary>
	public int? ZIndex {
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
		if ( this.SourceRectangle is TerminalRasterSourceRectangle rectangle ) {
			rectangle.Validate();
		}
	}

	internal void Validate(
		int sourceWidth,
		int sourceHeight
	) {
		ValidateSourceDimension(
			sourceWidth,
			nameof( sourceWidth )
		);
		ValidateSourceDimension(
			sourceHeight,
			nameof( sourceHeight )
		);
		this.Validate();

		if ( !this.SourceRectangle.HasValue ) {
			return;
		}

		TerminalRasterSourceRectangle rectangle = this.SourceRectangle.Value;
		long right = (long)rectangle.X + rectangle.Width;
		long bottom = (long)rectangle.Y + rectangle.Height;
		if ( sourceWidth < right || sourceHeight < bottom ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.SourceRectangle ),
				this.SourceRectangle,
				"The raster source rectangle must fit completely inside the persistent raster resource."
			);
		}
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

	private static void ValidateSourceDimension(
		int value,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( value is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A persistent raster source dimension must be between 1 and {TerminalRasterImage.MaximumDimension}."
			);
		}
	}
}
