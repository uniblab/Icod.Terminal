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

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the internal immutable-parent state contract for relative persistent raster placements.
/// </summary>
public sealed class TerminalPersistentRasterRelativeStateTests {
	[Fact]
	public void OrdinaryPlacementDefaultsToCursorPositioningState() {
		TerminalPersistentRasterResourceState resource = CreateResource(
			imageNumber: 1
		);
		TerminalPersistentRasterPlacementState placement = new(
			resource,
			placementId: 1,
			generation: 1
		);

		Assert.Null( placement.Parent );
		Assert.Equal( 0, placement.RelativeDepth );
		Assert.Equal( 0, placement.ColumnOffset );
		Assert.Equal( 0, placement.RowOffset );
	}

	[Fact]
	public void RelativePlacementPreservesImmutableParentDepthAndOffsets() {
		TerminalPersistentRasterPlacementState parent = new(
			CreateResource( imageNumber: 1 ),
			placementId: 1,
			generation: 1
		);
		TerminalPersistentRasterPlacementState child = new(
			CreateResource( imageNumber: 2 ),
			placementId: 2,
			generation: 1,
			parent,
			relativeDepth: 1,
			columnOffset: -2,
			rowOffset: 3
		);

		Assert.Same( parent, child.Parent );
		Assert.Equal( 1, child.RelativeDepth );
		Assert.Equal( -2, child.ColumnOffset );
		Assert.Equal( 3, child.RowOffset );
	}

	[Fact]
	public void RelativePlacementCommitsOffsetsAsOneAcknowledgedStateChange() {
		TerminalPersistentRasterPlacementState parent = new(
			CreateResource( imageNumber: 1 ),
			placementId: 1,
			generation: 1
		);
		TerminalPersistentRasterPlacementState child = new(
			CreateResource( imageNumber: 2 ),
			placementId: 2,
			generation: 1,
			parent,
			relativeDepth: 1,
			columnOffset: 4,
			rowOffset: -5
		);

		child.CommitRelativeOffsets(
			int.MinValue,
			int.MaxValue
		);

		Assert.Equal( int.MinValue, child.ColumnOffset );
		Assert.Equal( int.MaxValue, child.RowOffset );
		Assert.Same( parent, child.Parent );
		Assert.Equal( 1, child.RelativeDepth );
	}

	[Fact]
	public void OrdinaryPlacementRejectsRelativeOffsetCommit() {
		TerminalPersistentRasterPlacementState placement = new(
			CreateResource( imageNumber: 1 ),
			placementId: 1,
			generation: 1
		);

		_ = Assert.Throws<InvalidOperationException>(
			() => placement.CommitRelativeOffsets(
				1,
				-1
			)
		);
	}

	[Fact]
	public void RelativePlacementRequiresParentDepthToAdvanceExactlyOnce() {
		TerminalPersistentRasterPlacementState parent = new(
			CreateResource( imageNumber: 1 ),
			placementId: 1,
			generation: 1
		);

		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalPersistentRasterPlacementState(
				CreateResource( imageNumber: 2 ),
				placementId: 2,
				generation: 1,
				parent,
				relativeDepth: 2,
				columnOffset: 0,
				rowOffset: 0
			)
		);
	}

	private static TerminalPersistentRasterResourceState CreateResource(
		uint imageNumber
	) {
		return new TerminalPersistentRasterResourceState(
			imageNumber,
			generation: 1,
			sourceWidth: 4,
			sourceHeight: 3
		);
	}
}
