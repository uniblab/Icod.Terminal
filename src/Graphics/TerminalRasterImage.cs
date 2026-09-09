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
public enum TerminalRasterPixelFormat {
	/// <summary>Three bytes per pixel in red, green, blue order.</summary>
	Rgb24,

	/// <summary>Four bytes per pixel in red, green, blue, alpha order.</summary>
	Rgba32,

	/// <summary>One byte per pixel indexing an RGBA8 palette.</summary>
	Indexed8
}

/// <summary>
/// Represents one straight/unpremultiplied RGBA8 raster color.
/// </summary>
public readonly record struct TerminalRasterColor {
	/// <summary>
	/// Initializes one straight/unpremultiplied RGBA8 color.
	/// </summary>
	/// <param name="red">The red channel.</param>
	/// <param name="green">The green channel.</param>
	/// <param name="blue">The blue channel.</param>
	/// <param name="alpha">The alpha channel.</param>
	public TerminalRasterColor(
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

	/// <summary>Gets the red channel.</summary>
	public byte Red {
		get;
	}

	/// <summary>Gets the green channel.</summary>
	public byte Green {
		get;
	}

	/// <summary>Gets the blue channel.</summary>
	public byte Blue {
		get;
	}

	/// <summary>Gets the alpha channel.</summary>
	public byte Alpha {
		get;
	}
}

/// <summary>
/// Owns one bounded immutable snapshot of tightly packed raw raster data.
/// </summary>
public sealed class TerminalRasterImage {
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

	/// <summary>Gets the raster width in pixels.</summary>
	public int Width {
		get;
	}

	/// <summary>Gets the raster height in pixels.</summary>
	public int Height {
		get;
	}

	/// <summary>Gets the raw pixel storage format.</summary>
	public TerminalRasterPixelFormat PixelFormat {
		get;
	}

	internal int BytesPerPixel {
		get;
	}

	internal int RowByteLength {
		get;
	}

	/// <summary>Gets the total number of pixels.</summary>
	public int PixelCount {
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

	/// <summary>
	/// Creates an immutable raster snapshot from tightly packed RGB24 pixels.
	/// </summary>
	/// <param name="width">The positive image width in pixels.</param>
	/// <param name="height">The positive image height in pixels.</param>
	/// <param name="pixels">Exactly three bytes per pixel in red, green, blue order.</param>
	/// <returns>The owned raster snapshot.</returns>
	public static TerminalRasterImage CreateRgb24(
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

	/// <summary>
	/// Creates an immutable raster snapshot from tightly packed straight RGBA32 pixels.
	/// </summary>
	/// <param name="width">The positive image width in pixels.</param>
	/// <param name="height">The positive image height in pixels.</param>
	/// <param name="pixels">Exactly four bytes per pixel in red, green, blue, alpha order.</param>
	/// <returns>The owned raster snapshot.</returns>
	public static TerminalRasterImage CreateRgba32(
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

	/// <summary>
	/// Creates an immutable raster snapshot from one-byte palette indices and an RGBA8 palette.
	/// </summary>
	/// <param name="width">The positive image width in pixels.</param>
	/// <param name="height">The positive image height in pixels.</param>
	/// <param name="pixels">Exactly one palette index byte per pixel.</param>
	/// <param name="palette">Between one and 256 straight RGBA8 palette entries.</param>
	/// <returns>The owned raster snapshot.</returns>
	public static TerminalRasterImage CreateIndexed8(
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

	/// <summary>
	/// Gets the straight RGBA8 color of one pixel.
	/// </summary>
	/// <param name="x">The zero-based horizontal pixel coordinate.</param>
	/// <param name="y">The zero-based vertical pixel coordinate.</param>
	/// <returns>The resolved pixel color.</returns>
	public TerminalRasterColor GetPixelColor(
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
