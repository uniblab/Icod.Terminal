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
/// Identifies the raw pixel formats used by the reviewed Kitty Graphics direct-transfer path.
/// </summary>
internal enum KittyGraphicsPixelFormat {
	Rgb24 = 24,
	Rgba32 = 32
}

/// <summary>
/// Represents one immutable raw raster payload ready for Kitty Graphics direct transfer.
/// </summary>
internal sealed class KittyRasterData {
	internal KittyRasterData(
		int width,
		int height,
		KittyGraphicsPixelFormat pixelFormat,
		ReadOnlyMemory<byte> bytes
	) {
		if ( width is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}
		if ( height is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException( nameof( height ) );
		}
		if ( !Enum.IsDefined( pixelFormat ) ) {
			throw new ArgumentOutOfRangeException( nameof( pixelFormat ) );
		}

		int pixelCount = checked( width * height );
		if ( TerminalRasterImage.MaximumPixelCount < pixelCount ) {
			throw new ArgumentOutOfRangeException( nameof( height ) );
		}
		int bytesPerPixel = KittyGraphicsPixelFormat.Rgb24 == pixelFormat
			? 3
			: 4
		;
		int requiredByteCount = checked( pixelCount * bytesPerPixel );
		if ( bytes.Length != requiredByteCount ) {
			throw new ArgumentException(
				$"A Kitty {pixelFormat} raster requires exactly {requiredByteCount} bytes for the supplied dimensions.",
				nameof( bytes )
			);
		}
		if ( TerminalRasterImage.MaximumOwnedPixelBytes < requiredByteCount ) {
			throw new ArgumentOutOfRangeException(
				nameof( bytes ),
				requiredByteCount,
				$"A Kitty raster cannot exceed {TerminalRasterImage.MaximumOwnedPixelBytes} raw bytes."
			);
		}

		this.Width = width;
		this.Height = height;
		this.PixelFormat = pixelFormat;
		this.BytesPerPixel = bytesPerPixel;
		this.Bytes = bytes;
	}

	internal int Width {
		get;
	}

	internal int Height {
		get;
	}

	internal KittyGraphicsPixelFormat PixelFormat {
		get;
	}

	internal int BytesPerPixel {
		get;
	}

	internal ReadOnlyMemory<byte> Bytes {
		get;
	}
}

/// <summary>
/// Adapts the backend-neutral raster model to Kitty Graphics raw direct-transfer formats.
/// </summary>
internal static class KittyRasterAdapter {
	internal static KittyRasterData Adapt(
		TerminalRasterImage image
	) {
		ArgumentNullException.ThrowIfNull( image );

		switch ( image.PixelFormat ) {
			case TerminalRasterPixelFormat.Rgb24:
				return new KittyRasterData(
					image.Width,
					image.Height,
					KittyGraphicsPixelFormat.Rgb24,
					image.PixelBytes
				);

			case TerminalRasterPixelFormat.Rgba32:
				return new KittyRasterData(
					image.Width,
					image.Height,
					KittyGraphicsPixelFormat.Rgba32,
					image.PixelBytes
				);

			case TerminalRasterPixelFormat.Indexed8:
				return ExpandIndexed( image );

			default:
				throw new InvalidOperationException(
					"The raster image contains an unknown pixel format."
				);
		}
	}

	private static KittyRasterData ExpandIndexed(
		TerminalRasterImage image
	) {
		ArgumentNullException.ThrowIfNull( image );

		ReadOnlySpan<byte> source = image.PixelBytes.Span;
		ReadOnlySpan<TerminalRasterColor> palette = image.Palette.Span;
		bool requiresAlpha = false;
		for ( int index = 0; index < source.Length; index++ ) {
			if ( byte.MaxValue != palette[ source[ index ] ].Alpha ) {
				requiresAlpha = true;
				break;
			}
		}

		KittyGraphicsPixelFormat pixelFormat = requiresAlpha
			? KittyGraphicsPixelFormat.Rgba32
			: KittyGraphicsPixelFormat.Rgb24
		;
		int bytesPerPixel = requiresAlpha ? 4 : 3;
		byte[] expanded = new byte[
			checked( image.PixelCount * bytesPerPixel )
		];

		int outputOffset = 0;
		for ( int index = 0; index < source.Length; index++ ) {
			TerminalRasterColor color = palette[ source[ index ] ];
			expanded[ outputOffset++ ] = color.Red;
			expanded[ outputOffset++ ] = color.Green;
			expanded[ outputOffset++ ] = color.Blue;
			if ( requiresAlpha ) {
				expanded[ outputOffset++ ] = color.Alpha;
			}
		}

		return new KittyRasterData(
			image.Width,
			image.Height,
			pixelFormat,
			expanded
		);
	}
}
