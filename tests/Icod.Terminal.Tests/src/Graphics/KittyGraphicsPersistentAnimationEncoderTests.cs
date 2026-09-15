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
/// Freezes the T163 full-frame persistent Kitty animation transfer contract.
/// </summary>
public sealed class KittyGraphicsPersistentAnimationEncoderTests {
	[Fact]
	public void OneRgbPixelUsesFullFrameDirectAppendWithPositiveGap() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 1, 2, 3 ]
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsPersistentAnimationEncoder.EncodeFramePayloads(
				raster,
				imageId: 99,
				gapMilliseconds: 48
			)
		);

		Assert.Equal(
			"Ga=f,f=24,s=1,v=1,t=d,i=99,z=48,m=0;AQID",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void OneRgbaPixelPreservesAlphaAndFormat() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgba32(
				1,
				1,
				[ 1, 2, 3, 4 ]
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsPersistentAnimationEncoder.EncodeFramePayloads(
				raster,
				imageId: 99,
				gapMilliseconds: 7
			)
		);

		Assert.Equal(
			"Ga=f,f=32,s=1,v=1,t=d,i=99,z=7,m=0;AQIDBA==",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void IndexedRasterUsesReviewedKittyAdaptationBeforeFrameTransfer() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateIndexed8(
				2,
				1,
				[ 1, 0 ],
				[
					new TerminalRasterColor( 10, 20, 30 ),
					new TerminalRasterColor( 40, 50, 60 )
				]
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsPersistentAnimationEncoder.EncodeFramePayloads(
				raster,
				imageId: 99,
				gapMilliseconds: 25
			)
		);

		Assert.Equal(
			"Ga=f,f=24,s=2,v=1,t=d,i=99,z=25,m=0;KDI8ChQe",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void MultiChunkFrameRepeatsAnimationActionAndPreservesBounds() {
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

		ReadOnlyMemory<byte>[] payloads = KittyGraphicsPersistentAnimationEncoder
			.EncodeFramePayloads(
				raster,
				imageId: 99,
				gapMilliseconds: 48
			)
			.ToArray();

		Assert.Equal( 2, payloads.Length );
		SplitPayload(
			payloads[ 0 ],
			out string firstControl,
			out byte[] firstEncoded
		);
		SplitPayload(
			payloads[ 1 ],
			out string finalControl,
			out byte[] finalEncoded
		);

		Assert.Equal(
			"Ga=f,f=24,s=1025,v=1,t=d,i=99,z=48,m=1;",
			firstControl
		);
		Assert.Equal( "Ga=f,m=0;", finalControl );
		Assert.True(
			firstEncoded.Length
				<= KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes
		);
		Assert.True(
			finalEncoded.Length
				<= KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes
		);
		Assert.Equal( pixels, DecodeCombinedPayloads( payloads ) );
	}

	[Fact]
	public void FrameTransferUsesOnlyDirectTransportAndDoesNotSuppressAcknowledgement() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 1, 2, 3 ]
			)
		);

		string payload = Encoding.ASCII.GetString(
			Assert.Single(
				KittyGraphicsPersistentAnimationEncoder.EncodeFramePayloads(
					raster,
					imageId: 99,
					gapMilliseconds: 48
				)
			).Span
		);

		Assert.Contains( "t=d", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( "t=f", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( "t=t", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( "t=s", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( "q=", payload, StringComparison.Ordinal );
	}

	[Theory]
	[InlineData( 0u, 48 )]
	[InlineData( 99u, 0 )]
	[InlineData( 99u, -1 )]
	public void FrameTransferRejectsInvalidIdentityOrGap(
		uint imageId,
		int gapMilliseconds
	) {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 1, 2, 3 ]
			)
		);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeFramePayloads(
				raster,
				imageId,
				gapMilliseconds
			)
		);
	}

	private static void SplitPayload(
		ReadOnlyMemory<byte> payload,
		out string controlData,
		out byte[] encoded
	) {
		int separator = payload.Span.IndexOf( (byte)';' );
		Assert.True( 0 < separator );
		controlData = Encoding.ASCII.GetString( payload.Span[..( separator + 1 )] );
		encoded = payload.Span[( separator + 1 )..].ToArray();
	}

	private static byte[] DecodeCombinedPayloads(
		IEnumerable<ReadOnlyMemory<byte>> payloads
	) {
		StringBuilder encoded = new();
		foreach ( ReadOnlyMemory<byte> payload in payloads ) {
			int separator = payload.Span.IndexOf( (byte)';' );
			Assert.True( 0 < separator );
			encoded.Append(
				Encoding.ASCII.GetString(
					payload.Span[( separator + 1 )..]
				)
			);
		}
		return Convert.FromBase64String( encoded.ToString() );
	}
}
