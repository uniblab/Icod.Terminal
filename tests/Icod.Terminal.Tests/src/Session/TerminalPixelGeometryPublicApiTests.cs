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
namespace Icod.Terminal.Tests.Session;

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the additive 1.24 semantic pixel-geometry contract.
/// </summary>
public sealed class TerminalPixelGeometryPublicApiTests {
	[Fact]
	public void PixelDimensionsRequirePositiveAxes() {
		TerminalPixelDimensions dimensions = new( 8, 16 );

		Assert.Equal( 8, dimensions.Width );
		Assert.Equal( 16, dimensions.Height );
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalPixelDimensions( 0, 16 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalPixelDimensions( 8, 0 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalPixelDimensions( -1, 16 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalPixelDimensions( 8, -1 )
		);
	}

	[Fact]
	public void ExactTerminalGeometryDerivesCellDimensions() {
		bool success = TerminalPixelGeometry.TryDeriveCellDimensions(
			new TerminalDimensions( 100, 30 ),
			new TerminalPixelDimensions( 800, 480 ),
			out TerminalPixelDimensions cell
		);

		Assert.True( success );
		Assert.Equal( new TerminalPixelDimensions( 8, 16 ), cell );
	}

	[Fact]
	public void InexactOrInvalidObservationsDoNotFabricateCellDimensions() {
		Assert.False(
			TerminalPixelGeometry.TryDeriveCellDimensions(
				new TerminalDimensions( 99, 30 ),
				new TerminalPixelDimensions( 800, 480 ),
				out TerminalPixelDimensions fractional
			)
		);
		Assert.Equal( default, fractional );
		Assert.False(
			TerminalPixelGeometry.TryDeriveCellDimensions(
				default,
				new TerminalPixelDimensions( 800, 480 ),
				out TerminalPixelDimensions invalidGrid
			)
		);
		Assert.Equal( default, invalidGrid );
		Assert.False(
			TerminalPixelGeometry.TryDeriveCellDimensions(
				new TerminalDimensions( 100, 30 ),
				default,
				out TerminalPixelDimensions invalidPixels
			)
		);
		Assert.Equal( default, invalidPixels );
	}

	[Fact]
	public void PublicQueriesUseTheFrozenValueTaskSignatures() {
		AssertQuerySignature( nameof( TerminalSession.QueryTerminalPixelDimensionsAsync ) );
		AssertQuerySignature( nameof( TerminalSession.QueryCellPixelDimensionsAsync ) );
	}

	private static void AssertQuerySignature(
		string methodName
	) {
		MethodInfo? method = typeof( TerminalSession ).GetMethod(
			methodName,
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			types: [ typeof( TimeSpan ), typeof( CancellationToken ) ],
			modifiers: null
		);

		Assert.NotNull( method );
		Assert.Equal(
			typeof( ValueTask<TerminalPixelDimensions> ),
			method.ReturnType
		);
		ParameterInfo cancellationToken = method.GetParameters()[ 1 ];
		Assert.True( cancellationToken.HasDefaultValue );
	}
}
