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
/// Verifies T124 deterministic advanced persistent-placement encoding.
/// </summary>
public sealed class KittyGraphicsPersistentAdvancedPlacementEncoderTests {
	[Fact]
	public void AdvancedPlacementUsesDeterministicGeometryOrder() {
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = new TerminalRasterSourceRectangle(
				2,
				3,
				4,
				5
			),
			Columns = 6,
			Rows = 7,
			ZIndex = -1
		};

		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			options
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1,x=2,y=3,w=4,h=5,c=6,r=7,z=-1",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void NullAdvancedGeometryPreservesLegacyPlacementBytes() {
		TerminalRasterPlacementOptions options = new() {
			Columns = 3,
			Rows = 2
		};

		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			options
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1,c=3,r=2",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void NullOptionsPreserveIntrinsicLegacyPlacementBytes() {
		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			options: null
		);

		Assert.Equal(
			"Ga=p,i=99,p=7,C=1",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Theory]
	[InlineData( -1, "Ga=p,i=99,p=7,C=1,z=-1" )]
	[InlineData( int.MinValue, "Ga=p,i=99,p=7,C=1,z=-2147483648" )]
	[InlineData( 0, "Ga=p,i=99,p=7,C=1,z=0" )]
	[InlineData( int.MaxValue, "Ga=p,i=99,p=7,C=1,z=2147483647" )]
	public void ZIndexUsesInvariantSignedDecimal(
		int zIndex,
		string expected
	) {
		TerminalRasterPlacementOptions options = new() {
			ZIndex = zIndex
		};

		ReadOnlyMemory<byte> payload = KittyGraphicsPersistentEncoder.EncodePlacementPayload(
			imageId: 99,
			placementId: 7,
			options
		);

		Assert.Equal(
			expected,
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void MissingSourceRectangleEmitsNoSourceKeys() {
		TerminalRasterPlacementOptions options = new() {
			Columns = 2,
			ZIndex = 3
		};

		string payload = Encoding.ASCII.GetString(
			KittyGraphicsPersistentEncoder.EncodePlacementPayload(
				imageId: 99,
				placementId: 7,
				options
			).Span
		);

		Assert.DoesNotContain( ",x=", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( ",y=", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( ",w=", payload, StringComparison.Ordinal );
		Assert.DoesNotContain( ",h=", payload, StringComparison.Ordinal );
	}
}
