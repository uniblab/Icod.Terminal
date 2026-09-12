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
/// Verifies resource-aware validation for persistent raster source rectangles.
/// </summary>
public sealed class TerminalRasterPlacementSourceRectangleTests {
	[Fact]
	public void PlacementOptionsExposeNullableSourceRectangle() {
		TerminalRasterSourceRectangle rectangle = new(
			1,
			2,
			3,
			4
		);
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = rectangle
		};

		Assert.Equal( rectangle, options.SourceRectangle );
		Assert.Equal(
			typeof( TerminalRasterSourceRectangle? ),
			typeof( TerminalRasterPlacementOptions )
				.GetProperty( nameof( TerminalRasterPlacementOptions.SourceRectangle ) )?
				.PropertyType
		);
	}

	[Fact]
	public void FullSourceRectangleFitsResourceExactly() {
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = new TerminalRasterSourceRectangle(
				0,
				0,
				4,
				3
			)
		};

		options.Validate(
			4,
			3
		);
	}

	[Fact]
	public void InteriorSourceRectangleFitsResource() {
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = new TerminalRasterSourceRectangle(
				1,
				1,
				3,
				2
			)
		};

		options.Validate(
			4,
			3
		);
	}

	[Theory]
	[InlineData( 2, 0, 3, 3 )]
	[InlineData( 0, 2, 4, 2 )]
	public void SourceRectangleCannotExtendBeyondResource(
		int x,
		int y,
		int width,
		int height
	) {
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = new TerminalRasterSourceRectangle(
				x,
				y,
				width,
				height
			)
		};

		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => options.Validate(
				4,
				3
			)
		);
	}

	[Fact]
	public void NullSourceRectangleAcceptsValidResourceDimensions() {
		TerminalRasterPlacementOptions options = new();

		options.Validate(
			4,
			3
		);
	}

	[Fact]
	public void ResourceStateRetainsSourceDimensionsOnlyAsMetadata() {
		TerminalPersistentRasterResourceState state = new(
			imageNumber: 1,
			generation: 1,
			sourceWidth: 4,
			sourceHeight: 3
		);

		Assert.Equal( 4, state.SourceWidth );
		Assert.Equal( 3, state.SourceHeight );
	}

	[Fact]
	public void RegistryReservationRetainsSourceDimensions() {
		TerminalPersistentRasterRegistry registry = new();

		Assert.True(
			registry.TryReserveResource(
				4,
				3,
				out TerminalPersistentRasterResourceState? resource
			)
		);
		Assert.NotNull( resource );
		Assert.Equal( 4, resource.SourceWidth );
		Assert.Equal( 3, resource.SourceHeight );
	}
}
