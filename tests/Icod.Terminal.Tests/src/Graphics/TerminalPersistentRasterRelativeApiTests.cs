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

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the additive public API candidate for 1.13 relative persistent-raster placements.
/// </summary>
public sealed class TerminalPersistentRasterRelativeApiTests {
	[Fact]
	public void RasterResourceExposesExplicitRelativePlacementCreation() {
		MethodInfo? method = typeof( TerminalRasterResource ).GetMethod(
			"CreateRelativePlacementAsync",
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			[
				typeof( TerminalRasterPlacement ),
				typeof( int ),
				typeof( int ),
				typeof( TerminalRasterPlacementOptions ),
				typeof( CancellationToken )
			],
			modifiers: null
		);

		Assert.NotNull( method );
		Assert.Equal(
			typeof( ValueTask<TerminalControlResult<TerminalRasterPlacement>> ),
			method.ReturnType
		);
		ParameterInfo[] parameters = method.GetParameters();
		Assert.Equal( 5, parameters.Length );
		Assert.Equal( "parent", parameters[ 0 ].Name );
		Assert.Equal( "columnOffset", parameters[ 1 ].Name );
		Assert.Equal( "rowOffset", parameters[ 2 ].Name );
		Assert.Equal( "options", parameters[ 3 ].Name );
		Assert.True( parameters[ 3 ].HasDefaultValue );
		Assert.Null( parameters[ 3 ].DefaultValue );
		Assert.Equal( "cancellationToken", parameters[ 4 ].Name );
		Assert.True( parameters[ 4 ].HasDefaultValue );
	}

	[Fact]
	public void RasterPlacementExposesRelativeOffsetUpdateWithoutParentParameter() {
		MethodInfo? method = typeof( TerminalRasterPlacement ).GetMethod(
			"UpdateRelativeAsync",
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			[
				typeof( int ),
				typeof( int ),
				typeof( TerminalRasterPlacementOptions ),
				typeof( CancellationToken )
			],
			modifiers: null
		);

		Assert.NotNull( method );
		Assert.Equal(
			typeof( ValueTask<TerminalControlMutationResult> ),
			method.ReturnType
		);
		ParameterInfo[] parameters = method.GetParameters();
		Assert.Equal( 4, parameters.Length );
		Assert.Equal( "columnOffset", parameters[ 0 ].Name );
		Assert.Equal( "rowOffset", parameters[ 1 ].Name );
		Assert.Equal( "options", parameters[ 2 ].Name );
		Assert.True( parameters[ 2 ].HasDefaultValue );
		Assert.Null( parameters[ 2 ].DefaultValue );
		Assert.Equal( "cancellationToken", parameters[ 3 ].Name );
		Assert.True( parameters[ 3 ].HasDefaultValue );
	}

	[Fact]
	public void PublicPlacementSurfaceDoesNotExposeMutableParentage() {
		Assert.Null(
			typeof( TerminalRasterPlacement ).GetProperty(
				"Parent",
				BindingFlags.Instance | BindingFlags.Public
			)
		);
		Assert.Null(
			typeof( TerminalRasterPlacement ).GetMethod(
				"ReparentAsync",
				BindingFlags.Instance | BindingFlags.Public
			)
		);
	}
}
