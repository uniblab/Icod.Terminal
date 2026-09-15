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
/// Freezes the T153 self-contained Unicode raster-placeholder cell encoding contract.
/// </summary>
public sealed class KittyGraphicsPlaceholderCellEncoderTests {
	[Fact]
	public void ZeroCoordinateCellCarriesCompleteImagePlacementAndCoordinateIdentity() {
		TerminalPersistentRasterPlaceholderState placeholder = CreatePlaceholder(
			imageId: 42u,
			placementId: 0x010203u
		);

		ReadOnlyMemory<byte> encoded = KittyGraphicsPlaceholderCellEncoder.Encode(
			placeholder,
			row: 0,
			column: 0
		);

		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b[38;2;0;0;42m"
				+ "\u001b[58;2;1;2;3m"
				+ "\U0010EEEE\u0305\u0305\u0305"
				+ "\u001b[39m\u001b[59m"
			),
			encoded.ToArray()
		);
	}

	[Fact]
	public void HighImageByteUsesThirdDiacriticAfterExplicitRowAndColumn() {
		TerminalPersistentRasterPlaceholderState placeholder = CreatePlaceholder(
			imageId: 0x0200002Au,
			placementId: 1u
		);

		ReadOnlyMemory<byte> encoded = KittyGraphicsPlaceholderCellEncoder.Encode(
			placeholder,
			row: 1,
			column: 0
		);

		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b[38;2;0;0;42m"
				+ "\u001b[58;2;0;0;1m"
				+ "\U0010EEEE\u030D\u0305\u030E"
				+ "\u001b[39m\u001b[59m"
			),
			encoded.ToArray()
		);
	}

	[Fact]
	public void PortableMaximumCoordinatesAndIdentitiesEncodeExactly() {
		TerminalPersistentRasterPlaceholderState placeholder = CreatePlaceholder(
			imageId: 0xFF112233u,
			placementId: 0x00FFFFFFu
		);

		ReadOnlyMemory<byte> encoded = KittyGraphicsPlaceholderCellEncoder.Encode(
			placeholder,
			row: 255,
			column: 255
		);

		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b[38;2;17;34;51m"
				+ "\u001b[58;2;255;255;255m"
				+ "\U0010EEEE\uA8E5\uA8E5\uA8E5"
				+ "\u001b[39m\u001b[59m"
			),
			encoded.ToArray()
		);
	}

	[Fact]
	public void EveryCellIsSelfContainedAndDoesNotUseLeftNeighborShorthand() {
		TerminalPersistentRasterPlaceholderState placeholder = CreatePlaceholder(
			imageId: 0x0200002Au,
			placementId: 0x010203u
		);

		byte[] first = KittyGraphicsPlaceholderCellEncoder.Encode(
			placeholder,
			row: 0,
			column: 0
		).ToArray();
		byte[] second = KittyGraphicsPlaceholderCellEncoder.Encode(
			placeholder,
			row: 0,
			column: 1
		).ToArray();

		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b[38;2;0;0;42m"
				+ "\u001b[58;2;1;2;3m"
				+ "\U0010EEEE\u0305\u0305\u030E"
				+ "\u001b[39m\u001b[59m"
			),
			first
		);
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b[38;2;0;0;42m"
				+ "\u001b[58;2;1;2;3m"
				+ "\U0010EEEE\u0305\u030D\u030E"
				+ "\u001b[39m\u001b[59m"
			),
			second
		);
	}

	[Fact]
	public void EncoderRejectsCoordinatesOutsideOwningPlaceholder() {
		TerminalPersistentRasterPlaceholderState placeholder = CreatePlaceholder(
			imageId: 42u,
			placementId: 1u,
			columns: 2,
			rows: 2
		);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPlaceholderCellEncoder.Encode(
				placeholder,
				row: -1,
				column: 0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPlaceholderCellEncoder.Encode(
				placeholder,
				row: 2,
				column: 0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPlaceholderCellEncoder.Encode(
				placeholder,
				row: 0,
				column: 2
			)
		);
	}

	[Fact]
	public void PlacementIdentityParticipatesInEveryEncodedCell() {
		TerminalPersistentRasterPlaceholderState first = CreatePlaceholder(
			imageId: 42u,
			placementId: 1u
		);
		TerminalPersistentRasterPlaceholderState second = CreatePlaceholder(
			imageId: 42u,
			placementId: 2u
		);

		byte[] firstBytes = KittyGraphicsPlaceholderCellEncoder.Encode(
			first,
			row: 0,
			column: 0
		).ToArray();
		byte[] secondBytes = KittyGraphicsPlaceholderCellEncoder.Encode(
			second,
			row: 0,
			column: 0
		).ToArray();

		Assert.NotEqual( firstBytes, secondBytes );
		Assert.Contains(
			"\u001b[58;2;0;0;1m",
			Encoding.UTF8.GetString( firstBytes )
		);
		Assert.Contains(
			"\u001b[58;2;0;0;2m",
			Encoding.UTF8.GetString( secondBytes )
		);
	}

	private static TerminalPersistentRasterPlaceholderState CreatePlaceholder(
		uint imageId,
		uint placementId,
		int columns = 256,
		int rows = 256
	) {
		TerminalPersistentRasterResourceState resource = new(
			imageNumber: 1u,
			generation: 1L
		);
		resource.BindImageId( imageId );
		return new TerminalPersistentRasterPlaceholderState(
			resource,
			placementId,
			generation: 1L,
			columns,
			rows
		);
	}
}
