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

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies A183 bounded direct Base64 chunk encoding for Kitty Graphics.
/// </summary>
public sealed class KittyGraphicsDirectEncoderTests {
	[Fact]
	public void OneRgbPixelUsesOneFinalTransmitAndDisplayChunk() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 0, 0, 0 ]
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
		);

		Assert.Equal(
			"Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AAAA",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void OneRgbaPixelPreservesFourRawBytes() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgba32(
				1,
				1,
				[ 0, 0, 0, 0 ]
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
		);

		Assert.Equal(
			"Ga=T,f=32,s=1,v=1,t=d,m=0,q=2;AAAAAA==",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void ExactMaximumEncodedChunkIsFinalWhenRasterFitsExactly() {
		byte[] pixels = Enumerable.Range(
			0,
			KittyGraphicsDirectEncoder.MaximumRawChunkBytes
		).Select( index => unchecked( (byte)index ) ).ToArray();
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1024,
				1,
				pixels
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
		);
		SplitPayload(
			payload,
			out string controlData,
			out byte[] encoded
		);

		Assert.Equal(
			"Ga=T,f=24,s=1024,v=1,t=d,m=0,q=2;",
			controlData
		);
		Assert.Equal(
			KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes,
			encoded.Length
		);
		Assert.Equal( pixels, Convert.FromBase64String( Encoding.ASCII.GetString( encoded ) ) );
	}

	[Fact]
	public void OneRgbPixelPastRawChunkBoundaryUsesContinuationFrame() {
		byte[] pixels = Enumerable.Range(
			0,
			KittyGraphicsDirectEncoder.MaximumRawChunkBytes + 3
		).Select( index => unchecked( (byte)( index * 17 ) ) ).ToArray();
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1025,
				1,
				pixels
			)
		);

		ReadOnlyMemory<byte>[] payloads = KittyGraphicsDirectEncoder
			.EncodeDisplayPayloads( raster )
			.ToArray();

		Assert.Equal( 2, payloads.Length );
		SplitPayload(
			payloads[ 0 ],
			out string firstControlData,
			out byte[] firstEncoded
		);
		SplitPayload(
			payloads[ 1 ],
			out string finalControlData,
			out byte[] finalEncoded
		);
		Assert.Equal(
			"Ga=T,f=24,s=1025,v=1,t=d,m=1,q=2;",
			firstControlData
		);
		Assert.Equal( "Gm=0,q=2;", finalControlData );
		Assert.Equal(
			KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes,
			firstEncoded.Length
		);
		Assert.Equal( 4, finalEncoded.Length );
		Assert.Equal( pixels, DecodeCombinedPayloads( payloads ) );
	}

	[Fact]
	public void RgbaBoundaryUsesEightByteFinalPayloadForFourRawBytes() {
		byte[] pixels = Enumerable.Range(
			0,
			KittyGraphicsDirectEncoder.MaximumRawChunkBytes + 4
		).Select( index => unchecked( (byte)( index * 29 ) ) ).ToArray();
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgba32(
				769,
				1,
				pixels
			)
		);

		ReadOnlyMemory<byte>[] payloads = KittyGraphicsDirectEncoder
			.EncodeDisplayPayloads( raster )
			.ToArray();
		SplitPayload(
			payloads[ 1 ],
			out string finalControlData,
			out byte[] finalEncoded
		);

		Assert.Equal( 2, payloads.Length );
		Assert.Equal( "Gm=0,q=2;", finalControlData );
		Assert.Equal( 8, finalEncoded.Length );
		Assert.Equal( pixels, DecodeCombinedPayloads( payloads ) );
	}

	[Fact]
	public void EveryEncodedChunkIsBoundedAndNonFinalChunksAreQuadAligned() {
		byte[] pixels = Enumerable.Range(
			0,
			12_303
		).Select( index => unchecked( (byte)( index * 31 ) ) ).ToArray();
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1367,
				3,
				pixels
			)
		);
		ReadOnlyMemory<byte>[] payloads = KittyGraphicsDirectEncoder
			.EncodeDisplayPayloads( raster )
			.ToArray();

		Assert.True( 1 < payloads.Length );
		for ( int index = 0; index < payloads.Length; index++ ) {
			SplitPayload(
				payloads[ index ],
				out _,
				out byte[] encoded
			);
			Assert.True(
				encoded.Length <= KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes
			);
			if ( index + 1 < payloads.Length ) {
				Assert.Equal( 0, encoded.Length % 4 );
			}
		}
		Assert.Equal( pixels, DecodeCombinedPayloads( payloads ) );
	}

	[Fact]
	public void EveryApplicationPayloadFitsTheA180SmallFrameBound() {
		byte[] pixels = new byte[ KittyGraphicsDirectEncoder.MaximumRawChunkBytes + 3 ];
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1025,
				1,
				pixels
			)
		);

		foreach ( ReadOnlyMemory<byte> payload in KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster ) ) {
			byte[] frame = ApcWriter.EncodeFrame( payload.Span );
			Assert.True( frame.Length <= ApcWriter.MaximumEncodedFrameBytes );
		}
	}

	[Fact]
	public void RepeatedEncodingIsDeterministic() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgba32(
				2,
				1,
				[ 1, 2, 3, 4, 5, 6, 7, 8 ]
			)
		);

		byte[][] first = KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
			.Select( item => item.ToArray() )
			.ToArray();
		byte[][] second = KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
			.Select( item => item.ToArray() )
			.ToArray();

		Assert.Equal( first.Length, second.Length );
		for ( int index = 0; index < first.Length; index++ ) {
			Assert.Equal( first[ index ], second[ index ] );
		}
	}

	[Fact]
	public void NullRasterIsRejectedBeforeEnumeration() {
		Assert.Throws<ArgumentNullException>(
			() => KittyGraphicsDirectEncoder.EncodeDisplayPayloads( null! )
		);
	}

	private static byte[] DecodeCombinedPayloads(
		IEnumerable<ReadOnlyMemory<byte>> payloads
	) {
		StringBuilder encoded = new();
		foreach ( ReadOnlyMemory<byte> payload in payloads ) {
			SplitPayload(
				payload,
				out _,
				out byte[] encodedChunk
			);
			_ = encoded.Append( Encoding.ASCII.GetString( encodedChunk ) );
		}
		return Convert.FromBase64String( encoded.ToString() );
	}

	private static void SplitPayload(
		ReadOnlyMemory<byte> payload,
		out string controlData,
		out byte[] encoded
	) {
		ReadOnlySpan<byte> span = payload.Span;
		int separator = span.IndexOf( (byte)';' );
		Assert.True( 0 < separator );
		controlData = Encoding.ASCII.GetString( span[..( separator + 1 )] );
		encoded = span[( separator + 1 )..].ToArray();
	}
}
