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
/// Identifies one tightly packed raw raster storage form.
/// </summary>
internal enum TerminalRasterPixelFormat {
	Rgb24,
	Rgba32,
	Indexed8
}

/// <summary>
/// Represents one straight/unpremultiplied RGBA8 raster color.
/// </summary>
internal readonly record struct TerminalRasterColor {
	internal TerminalRasterColor(
		byte red,
		byte green,
		byte blue,
		byte alpha = byte.MaxValue
	) {
		this.Red = red;
		this.Green = green;
		this.Blue = blue;
		this.Alpha = alpha;
	}

	internal byte Red {
		get;
	}

	internal byte Green {
		get;
	}

	internal byte Blue {
		get;
	}

	internal byte Alpha {
		get;
	}
}

/// <summary>
/// Owns one bounded immutable snapshot of tightly packed raw raster data.
/// </summary>
internal sealed class TerminalRasterImage {
	internal const int MaximumDimension = 16_384;
	internal const int MaximumPixelCount = 16 * 1024 * 1024;
	internal const int MaximumOwnedPixelBytes = 64 * 1024 * 1024;
	internal const int MaximumPaletteEntries = 256;

	private readonly byte[] pixelBytes;
	private readonly TerminalRasterColor[] palette;

	private TerminalRasterImage(
		int width,
		int height,
		TerminalRasterPixelFormat pixelFormat,
		int bytesPerPixel,
		int rowByteLength,
		int pixelCount,
		byte[] pixelBytes,
		TerminalRasterColor[] palette
	) {
		this.Width = width;
		this.Height = height;
		this.PixelFormat = pixelFormat;
		this.BytesPerPixel = bytesPerPixel;
		this.RowByteLength = rowByteLength;
		this.PixelCount = pixelCount;
		this.pixelBytes = pixelBytes;
		this.palette = palette;
	}

	internal int Width {
		get;
	}

	internal int Height {
		get;
	}

	internal TerminalRasterPixelFormat PixelFormat {
		get;
	}

	internal int BytesPerPixel {
		get;
	}

	internal int RowByteLength {
		get;
	}

	internal int PixelCount {
		get;
	}

	internal ReadOnlyMemory<byte> PixelBytes {
		get {
			return this.pixelBytes;
		}
	}

	internal ReadOnlyMemory<TerminalRasterColor> Palette {
		get {
			return this.palette;
		}
	}

	internal static TerminalRasterImage CreateRgb24(
		int width,
		int height,
		ReadOnlySpan<byte> pixels
	) {
		return CreateDirect(
			width,
			height,
			TerminalRasterPixelFormat.Rgb24,
			3,
			pixels
		);
	}

	internal static TerminalRasterImage CreateRgba32(
		int width,
		int height,
		ReadOnlySpan<byte> pixels
	) {
		return CreateDirect(
			width,
			height,
			TerminalRasterPixelFormat.Rgba32,
			4,
			pixels
		);
	}

	internal static TerminalRasterImage CreateIndexed8(
		int width,
		int height,
		ReadOnlySpan<byte> pixels,
		ReadOnlySpan<TerminalRasterColor> palette
	) {
		int pixelCount = ValidateDimensions( width, height );
		if ( palette.IsEmpty || MaximumPaletteEntries < palette.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( palette ),
				palette.Length,
				$"An indexed raster palette must contain between 1 and {MaximumPaletteEntries} entries."
			);
		}
		if ( pixels.Length != pixelCount ) {
			throw new ArgumentException(
				$"An Indexed8 raster requires exactly {pixelCount} pixel bytes for the supplied dimensions.",
				nameof( pixels )
			);
		}

		for ( int index = 0; index < pixels.Length; index++ ) {
			if ( palette.Length <= pixels[ index ] ) {
				throw new ArgumentException(
					$"Indexed raster pixel {index} references palette index {pixels[ index ]}, but the palette contains only {palette.Length} entries.",
					nameof( pixels )
				);
			}
		}

		return new TerminalRasterImage(
			width,
			height,
			TerminalRasterPixelFormat.Indexed8,
			1,
			width,
			pixelCount,
			pixels.ToArray(),
			palette.ToArray()
		);
	}

	internal ReadOnlyMemory<byte> GetRowBytes(
		int row
	) {
		if ( row is < 0 || this.Height <= row ) {
			throw new ArgumentOutOfRangeException(
				nameof( row ),
				row,
				$"A raster row must be between 0 and {this.Height - 1}."
			);
		}

		return this.pixelBytes.AsMemory(
			checked( row * this.RowByteLength ),
			this.RowByteLength
		);
	}

	internal TerminalRasterColor GetPixelColor(
		int x,
		int y
	) {
		if ( x is < 0 || this.Width <= x ) {
			throw new ArgumentOutOfRangeException(
				nameof( x ),
				x,
				$"A raster x coordinate must be between 0 and {this.Width - 1}."
			);
		}
		if ( y is < 0 || this.Height <= y ) {
			throw new ArgumentOutOfRangeException(
				nameof( y ),
				y,
				$"A raster y coordinate must be between 0 and {this.Height - 1}."
			);
		}

		int offset = checked(
			y * this.RowByteLength
				+ x * this.BytesPerPixel
		);
		switch ( this.PixelFormat ) {
			case TerminalRasterPixelFormat.Rgb24:
				return new TerminalRasterColor(
					this.pixelBytes[ offset ],
					this.pixelBytes[ offset + 1 ],
					this.pixelBytes[ offset + 2 ]
				);

			case TerminalRasterPixelFormat.Rgba32:
				return new TerminalRasterColor(
					this.pixelBytes[ offset ],
					this.pixelBytes[ offset + 1 ],
					this.pixelBytes[ offset + 2 ],
					this.pixelBytes[ offset + 3 ]
				);

			case TerminalRasterPixelFormat.Indexed8:
				return this.palette[ this.pixelBytes[ offset ] ];

			default:
				throw new InvalidOperationException(
					"The raster image contains an unknown pixel format."
				);
		}
	}

	private static TerminalRasterImage CreateDirect(
		int width,
		int height,
		TerminalRasterPixelFormat pixelFormat,
		int bytesPerPixel,
		ReadOnlySpan<byte> pixels
	) {
		if ( !Enum.IsDefined( pixelFormat )
			|| TerminalRasterPixelFormat.Indexed8 == pixelFormat ) {
			throw new ArgumentOutOfRangeException( nameof( pixelFormat ) );
		}
		if ( bytesPerPixel is < 1 or > 4 ) {
			throw new ArgumentOutOfRangeException( nameof( bytesPerPixel ) );
		}

		int pixelCount = ValidateDimensions( width, height );
		int rowByteLength = checked( width * bytesPerPixel );
		int requiredBytes = checked( pixelCount * bytesPerPixel );
		if ( MaximumOwnedPixelBytes < requiredBytes ) {
			throw new ArgumentOutOfRangeException(
				nameof( pixels ),
				requiredBytes,
				$"A raster image cannot own more than {MaximumOwnedPixelBytes} pixel bytes."
			);
		}
		if ( pixels.Length != requiredBytes ) {
			throw new ArgumentException(
				$"A {pixelFormat} raster requires exactly {requiredBytes} pixel bytes for the supplied dimensions.",
				nameof( pixels )
			);
		}

		return new TerminalRasterImage(
			width,
			height,
			pixelFormat,
			bytesPerPixel,
			rowByteLength,
			pixelCount,
			pixels.ToArray(),
			Array.Empty<TerminalRasterColor>()
		);
	}

	private static int ValidateDimensions(
		int width,
		int height
	) {
		if ( width is < 1 or > MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				nameof( width ),
				width,
				$"A raster width must be between 1 and {MaximumDimension}."
			);
		}
		if ( height is < 1 or > MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				nameof( height ),
				height,
				$"A raster height must be between 1 and {MaximumDimension}."
			);
		}

		int pixelCount = checked( width * height );
		if ( MaximumPixelCount < pixelCount ) {
			throw new ArgumentOutOfRangeException(
				nameof( height ),
				height,
				$"A raster image cannot contain more than {MaximumPixelCount} pixels."
			);
		}

		return pixelCount;
	}
}
