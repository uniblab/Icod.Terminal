/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Graphics;

using System.Runtime.InteropServices;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies A182 backend-neutral raster adaptation for Kitty Graphics raw transfer.
/// </summary>
public sealed class KittyRasterAdapterTests {
	[Fact]
	public void Rgb24IsPreservedByteExactWithoutAnotherPixelCopy() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			2,
			1,
			[ 1, 2, 3, 4, 5, 6 ]
		);

		KittyRasterData adapted = KittyRasterAdapter.Adapt( image );

		Assert.Equal( 2, adapted.Width );
		Assert.Equal( 1, adapted.Height );
		Assert.Equal( KittyGraphicsPixelFormat.Rgb24, adapted.PixelFormat );
		Assert.Equal( 3, adapted.BytesPerPixel );
		Assert.Equal(
			new byte[] { 1, 2, 3, 4, 5, 6 },
			adapted.Bytes.ToArray()
		);
		Assert.True(
			MemoryMarshal.TryGetArray(
				image.PixelBytes,
				out ArraySegment<byte> imageBytes
			)
		);
		Assert.True(
			MemoryMarshal.TryGetArray(
				adapted.Bytes,
				out ArraySegment<byte> adaptedBytes
			)
		);
		Assert.Same( imageBytes.Array, adaptedBytes.Array );
	}

	[Fact]
	public void Rgba32PreservesFractionalAlphaByteExactWithoutAnotherPixelCopy() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			2,
			1,
			[ 10, 20, 30, 1, 40, 50, 60, 254 ]
		);

		KittyRasterData adapted = KittyRasterAdapter.Adapt( image );

		Assert.Equal( KittyGraphicsPixelFormat.Rgba32, adapted.PixelFormat );
		Assert.Equal( 4, adapted.BytesPerPixel );
		Assert.Equal(
			new byte[] { 10, 20, 30, 1, 40, 50, 60, 254 },
			adapted.Bytes.ToArray()
		);
		Assert.True(
			MemoryMarshal.TryGetArray(
				image.PixelBytes,
				out ArraySegment<byte> imageBytes
			)
		);
		Assert.True(
			MemoryMarshal.TryGetArray(
				adapted.Bytes,
				out ArraySegment<byte> adaptedBytes
			)
		);
		Assert.Same( imageBytes.Array, adaptedBytes.Array );
	}

	[Fact]
	public void OpaqueIndexedRasterExpandsDeterministicallyToRgb24() {
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			3,
			1,
			[ 1, 0, 1 ],
			[
				new TerminalRasterColor( 10, 20, 30 ),
				new TerminalRasterColor( 40, 50, 60 )
			]
		);

		KittyRasterData adapted = KittyRasterAdapter.Adapt( image );

		Assert.Equal( KittyGraphicsPixelFormat.Rgb24, adapted.PixelFormat );
		Assert.Equal(
			new byte[] {
				40, 50, 60,
				10, 20, 30,
				40, 50, 60
			},
			adapted.Bytes.ToArray()
		);
	}

	[Fact]
	public void UnusedTransparentPaletteEntryDoesNotForceRgba32() {
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			2,
			1,
			[ 0, 0 ],
			[
				new TerminalRasterColor( 1, 2, 3 ),
				new TerminalRasterColor( 4, 5, 6, 0 )
			]
		);

		KittyRasterData adapted = KittyRasterAdapter.Adapt( image );

		Assert.Equal( KittyGraphicsPixelFormat.Rgb24, adapted.PixelFormat );
		Assert.Equal(
			new byte[] { 1, 2, 3, 1, 2, 3 },
			adapted.Bytes.ToArray()
		);
	}

	[Fact]
	public void ReferencedTransparentPaletteEntryExpandsToRgba32() {
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			2,
			1,
			[ 0, 1 ],
			[
				new TerminalRasterColor( 1, 2, 3 ),
				new TerminalRasterColor( 4, 5, 6, 0 )
			]
		);

		KittyRasterData adapted = KittyRasterAdapter.Adapt( image );

		Assert.Equal( KittyGraphicsPixelFormat.Rgba32, adapted.PixelFormat );
		Assert.Equal(
			new byte[] {
				1, 2, 3, 255,
				4, 5, 6, 0
			},
			adapted.Bytes.ToArray()
		);
	}

	[Fact]
	public void IndexedFractionalAlphaIsPreservedExactly() {
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			1,
			2,
			[ 0, 1 ],
			[
				new TerminalRasterColor( 7, 8, 9, 1 ),
				new TerminalRasterColor( 10, 11, 12, 254 )
			]
		);

		KittyRasterData adapted = KittyRasterAdapter.Adapt( image );

		Assert.Equal( 1, adapted.Width );
		Assert.Equal( 2, adapted.Height );
		Assert.Equal( KittyGraphicsPixelFormat.Rgba32, adapted.PixelFormat );
		Assert.Equal(
			new byte[] {
				7, 8, 9, 1,
				10, 11, 12, 254
			},
			adapted.Bytes.ToArray()
		);
	}

	[Fact]
	public void RepeatedIndexedAdaptationIsDeterministic() {
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			2,
			2,
			[ 0, 1, 2, 0 ],
			[
				new TerminalRasterColor( 1, 2, 3 ),
				new TerminalRasterColor( 4, 5, 6, 127 ),
				new TerminalRasterColor( 7, 8, 9 )
			]
		);

		KittyRasterData first = KittyRasterAdapter.Adapt( image );
		KittyRasterData second = KittyRasterAdapter.Adapt( image );

		Assert.Equal( first.PixelFormat, second.PixelFormat );
		Assert.Equal( first.Bytes.ToArray(), second.Bytes.ToArray() );
	}

	[Fact]
	public void NullRasterIsRejectedAtEntry() {
		Assert.Throws<ArgumentNullException>(
			() => KittyRasterAdapter.Adapt( null! )
		);
	}
}
