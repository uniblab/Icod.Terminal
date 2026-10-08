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
namespace Icod.Terminal.Tests.Samples;

using Icod.Terminal.RasterAnimation.Sample;
using Xunit;

/// <summary>Freezes the tile-atlas sample's conservative geometry fallback boundary.</summary>
public sealed class RasterTileAtlasExampleTests {
	[Fact]
	public void FrameTransferTimeoutPrintsControlledTextFallback() {
		string transcript = RasterTileAtlasExample.FormatTransactionFallback(
			new TimeoutException( "synthetic timeout" )
		);

		Assert.StartsWith( "Tile-atlas text fallback: ", transcript, StringComparison.Ordinal );
		Assert.Contains( "Status=Ambiguous", transcript, StringComparison.Ordinal );
		Assert.Contains( "automatic retry is unsafe", transcript, StringComparison.Ordinal );
		Assert.DoesNotContain( "Confirmation=", transcript, StringComparison.Ordinal );
		Assert.Contains( "Rendered=NotClaimed", transcript, StringComparison.Ordinal );
	}

	[Theory]
	[InlineData( typeof( TimeoutException ), true )]
	[InlineData( typeof( InvalidOperationException ), true )]
	[InlineData( typeof( FormatException ), true )]
	[InlineData( typeof( IOException ), false )]
	[InlineData( typeof( OperationCanceledException ), false )]
	public void GeometryUnavailableFailuresSelectFallback(
		Type exceptionType,
		bool expected
	) {
		Exception error = (Exception)Activator.CreateInstance( exceptionType )!;

		Assert.Equal(
			expected,
			RasterTileAtlasExample.IsGeometryUnavailableFailure( error )
		);
	}
}
