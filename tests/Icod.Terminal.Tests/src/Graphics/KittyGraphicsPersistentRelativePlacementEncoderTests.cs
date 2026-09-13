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
/// Defines the T134 internal Kitty relative persistent-placement wire contract.
/// </summary>
public sealed class KittyGraphicsPersistentRelativePlacementEncoderTests {
	[Fact]
	public void RelativePlacementEncodesRelationshipBeforeCommonGeometry() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodeRelativePlacementPayload(
			imageId: 99,
			placementId: 7,
			parentImageId: 41,
			parentPlacementId: 3,
			columnOffset: -2,
			rowOffset: 4,
			new TerminalRasterPlacementOptions {
				SourceRectangle = new TerminalRasterSourceRectangle(
					1,
					2,
					3,
					4
				),
				Columns = 5,
				Rows = 6,
				ZIndex = -7
			}
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1,P=41,Q=3,H=-2,V=4,x=1,y=2,w=3,h=4,c=5,r=6,z=-7",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void RelativePlacementCanUseIntrinsicSizingAndZeroOffsets() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodeRelativePlacementPayload(
			imageId: 99,
			placementId: 7,
			parentImageId: 41,
			parentPlacementId: 3,
			columnOffset: 0,
			rowOffset: 0,
			options: null
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1,P=41,Q=3,H=0,V=0",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void RelativePlacementPreservesFullSignedOffsetDomain() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodeRelativePlacementPayload(
			imageId: uint.MaxValue,
			placementId: uint.MaxValue,
			parentImageId: uint.MaxValue,
			parentPlacementId: uint.MaxValue,
			columnOffset: int.MinValue,
			rowOffset: int.MaxValue,
			options: null
		);

		Assert.Equal(
			"Ga=p,i=4294967295,p=4294967295,C=1,P=4294967295,Q=4294967295,H=-2147483648,V=2147483647",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Theory]
	[InlineData( 0u, 7u, 41u, 3u )]
	[InlineData( 99u, 0u, 41u, 3u )]
	[InlineData( 99u, 7u, 0u, 3u )]
	[InlineData( 99u, 7u, 41u, 0u )]
	public void RelativePlacementRejectsZeroIdentity(
		uint imageId,
		uint placementId,
		uint parentImageId,
		uint parentPlacementId
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentEncoder.EncodeRelativePlacementPayload(
				imageId,
				placementId,
				parentImageId,
				parentPlacementId,
				columnOffset: 0,
				rowOffset: 0,
				options: null
			)
		);
	}

	[Fact]
	public void CurrentCursorPlacementBytesRemainUnchanged() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			new TerminalRasterPlacementOptions {
				Columns = 3,
				Rows = 2,
				ZIndex = -1
			}
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1,c=3,r=2,z=-1",
			Encoding.ASCII.GetString( payload.Span )
		);
		Assert.DoesNotContain(
			",P=",
			Encoding.ASCII.GetString( payload.Span ),
			StringComparison.Ordinal
		);
		Assert.DoesNotContain(
			",Q=",
			Encoding.ASCII.GetString( payload.Span ),
			StringComparison.Ordinal
		);
	}
}
