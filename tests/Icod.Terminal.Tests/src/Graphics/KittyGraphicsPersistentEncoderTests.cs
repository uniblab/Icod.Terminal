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
/// Defines the C111 typed persistent Kitty Graphics wire contract.
/// </summary>
public sealed class KittyGraphicsPersistentEncoderTests {
	[Fact]
	public void OneRgbPixelUsesAcknowledgedTransmitOnlyUpload() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 0, 0, 0 ]
			)
		);

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsPersistentEncoder.EncodeUploadPayloads(
				raster,
				31
			)
		);

		Assert.Equal(
			"Ga=t,f=24,s=1,v=1,t=d,I=31,m=0;AAAA",
			Encoding.ASCII.GetString( payload.Span )
		);
		Assert.DoesNotContain(
			"q=",
			Encoding.ASCII.GetString( payload.Span ),
			StringComparison.Ordinal
		);
	}

	[Fact]
	public void MultiChunkUploadRetainsAcknowledgementAndChunkBounds() {
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

		ReadOnlyMemory<byte>[] payloads = KittyGraphicsPersistentEncoder
			.EncodeUploadPayloads(
				raster,
				31
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
			"Ga=t,f=24,s=1025,v=1,t=d,I=31,m=1;",
			firstControl
		);
		Assert.Equal( "Gm=0;", finalControl );
		Assert.DoesNotContain( "q=", firstControl, StringComparison.Ordinal );
		Assert.DoesNotContain( "q=", finalControl, StringComparison.Ordinal );
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
	public void PlacementUsesCurrentCursorWithoutMovingIt() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			columns: 3,
			rows: 2
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1,c=3,r=2",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void PlacementCanUseIntrinsicSizing() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			columns: null,
			rows: null
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void RepeatingPlacementIdentityProducesIdenticalReplacementPayload() {
		ReadOnlyMemory<byte> first = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			columns: 4,
			rows: 3
		);
		ReadOnlyMemory<byte> second = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			columns: 4,
			rows: 3
		);

		Assert.Equal( first.ToArray(), second.ToArray() );
	}

	[Fact]
	public void PlacementDeleteKeepsResourceData() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
			imageId: 99,
			placementId: 7
		);

		Assert.Equal(
			"Ga=d,d=i,i=99,p=7",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void ResourceDeleteUsesHardImageSelector() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodeDeleteResourcePayload(
			imageId: 99
		);

		Assert.Equal(
			"Ga=d,d=I,i=99",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Theory]
	[InlineData( 0u, 7u )]
	[InlineData( 99u, 0u )]
	public void PlacementRejectsZeroIdentity(
		uint imageId,
		uint placementId
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentEncoder.EncodePlacementPayload(
				imageId,
				placementId,
				null,
				null
			)
		);
	}

	[Fact]
	public void UploadRejectsZeroImageNumberBeforeEnumeration() {
		KittyRasterData raster = KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 0, 0, 0 ]
			)
		);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentEncoder.EncodeUploadPayloads(
				raster,
				0
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
