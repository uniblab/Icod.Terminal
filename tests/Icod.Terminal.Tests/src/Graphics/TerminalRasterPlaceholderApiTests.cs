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
/// Freezes the additive 1.15 Unicode-placeholder and virtual-placement public surface.
/// </summary>
public sealed class TerminalRasterPlaceholderApiTests {
	[Fact]
	public void PlaceholderPublicSurfaceHasFrozenSemanticShape() {
		Assert.Equal( 10, (int)TerminalCapability.UnicodeRasterPlaceholders );

		Type optionsType = typeof( TerminalRasterPlaceholderOptions );
		Type placeholderType = typeof( TerminalRasterPlaceholder );
		Type cellType = typeof( TerminalRasterPlaceholderCell );

		PropertyInfo columnsProperty = Assert.IsAssignableFrom<PropertyInfo>(
			optionsType.GetProperty( nameof( TerminalRasterPlaceholderOptions.Columns ) )
		);
		PropertyInfo rowsProperty = Assert.IsAssignableFrom<PropertyInfo>(
			optionsType.GetProperty( nameof( TerminalRasterPlaceholderOptions.Rows ) )
		);
		Assert.Equal( typeof( int ), columnsProperty.PropertyType );
		Assert.Equal( typeof( int ), rowsProperty.PropertyType );
		Assert.True( columnsProperty.CanRead );
		Assert.True( columnsProperty.CanWrite );
		Assert.True( rowsProperty.CanRead );
		Assert.True( rowsProperty.CanWrite );

		PropertyInfo placeholderColumns = Assert.IsAssignableFrom<PropertyInfo>(
			placeholderType.GetProperty( nameof( TerminalRasterPlaceholder.Columns ) )
		);
		PropertyInfo placeholderRows = Assert.IsAssignableFrom<PropertyInfo>(
			placeholderType.GetProperty( nameof( TerminalRasterPlaceholder.Rows ) )
		);
		PropertyInfo ownershipState = Assert.IsAssignableFrom<PropertyInfo>(
			placeholderType.GetProperty( nameof( TerminalRasterPlaceholder.OwnershipState ) )
		);
		Assert.Equal( typeof( int ), placeholderColumns.PropertyType );
		Assert.Equal( typeof( int ), placeholderRows.PropertyType );
		Assert.Equal( typeof( TerminalRasterOwnershipState ), ownershipState.PropertyType );
		Assert.False( placeholderColumns.CanWrite );
		Assert.False( placeholderRows.CanWrite );
		Assert.False( ownershipState.CanWrite );
		Assert.Contains( typeof( IAsyncDisposable ), placeholderType.GetInterfaces() );
		Assert.Empty( placeholderType.GetConstructors( BindingFlags.Instance | BindingFlags.Public ) );

		PropertyInfo cellRow = Assert.IsAssignableFrom<PropertyInfo>(
			cellType.GetProperty( nameof( TerminalRasterPlaceholderCell.Row ) )
		);
		PropertyInfo cellColumn = Assert.IsAssignableFrom<PropertyInfo>(
			cellType.GetProperty( nameof( TerminalRasterPlaceholderCell.Column ) )
		);
		Assert.Equal( typeof( int ), cellRow.PropertyType );
		Assert.Equal( typeof( int ), cellColumn.PropertyType );
		Assert.False( cellRow.CanWrite );
		Assert.False( cellColumn.CanWrite );
		Assert.True( cellType.IsValueType );
		Assert.Empty( cellType.GetConstructors( BindingFlags.Instance | BindingFlags.Public ) );

		MethodInfo getCell = Assert.IsAssignableFrom<MethodInfo>(
			placeholderType.GetMethod(
				nameof( TerminalRasterPlaceholder.GetCell ),
				[ typeof( int ), typeof( int ) ]
			)
		);
		Assert.Equal( typeof( TerminalRasterPlaceholderCell ), getCell.ReturnType );
	}

	[Fact]
	public void PlaceholderCreationAndOutputMethodsHaveFrozenShape() {
		MethodInfo createPlaceholder = Assert.IsAssignableFrom<MethodInfo>(
			typeof( TerminalRasterResource ).GetMethod(
				nameof( TerminalRasterResource.CreatePlaceholderAsync ),
				[ typeof( TerminalRasterPlaceholderOptions ), typeof( CancellationToken ) ]
			)
		);
		Assert.Equal(
			typeof( ValueTask<TerminalControlResult<TerminalRasterPlaceholder>> ),
			createPlaceholder.ReturnType
		);

		MethodInfo createRelative = Assert.IsAssignableFrom<MethodInfo>(
			typeof( TerminalRasterResource ).GetMethod(
				"CreateRelativePlacementFromPlaceholderAsync",
				[
					typeof( TerminalRasterPlaceholder ),
					typeof( int ),
					typeof( int ),
					typeof( TerminalRasterPlacementOptions ),
					typeof( CancellationToken )
				]
			)
		);
		Assert.Equal(
			typeof( ValueTask<TerminalControlResult<TerminalRasterPlacement>> ),
			createRelative.ReturnType
		);

		MethodInfo[] legacyRelativeMethods = typeof( TerminalRasterResource )
			.GetMethods( BindingFlags.Public | BindingFlags.Instance )
			.Where(
				method => nameof( TerminalRasterResource.CreateRelativePlacementAsync ) == method.Name
			)
			.ToArray();
		Assert.Single( legacyRelativeMethods );
		Assert.Equal(
			typeof( TerminalRasterPlacement ),
			legacyRelativeMethods[ 0 ].GetParameters()[ 0 ].ParameterType
		);

		MethodInfo writeCell = Assert.IsAssignableFrom<MethodInfo>(
			typeof( TerminalSession ).GetMethod(
				nameof( TerminalSession.WriteRasterPlaceholderCellAsync ),
				[ typeof( TerminalRasterPlaceholderCell ), typeof( CancellationToken ) ]
			)
		);
		Assert.Equal( typeof( ValueTask ), writeCell.ReturnType );

		MethodInfo writeCells = Assert.IsAssignableFrom<MethodInfo>(
			typeof( TerminalSession ).GetMethod(
				nameof( TerminalSession.WriteRasterPlaceholderCellsAsync ),
				[
					typeof( ReadOnlyMemory<TerminalRasterPlaceholderCell> ),
					typeof( CancellationToken )
				]
			)
		);
		Assert.Equal( typeof( ValueTask ), writeCells.ReturnType );
	}

	[Fact]
	public void PlaceholderSurfaceDoesNotExposeProtocolIdentityOrEncodingVocabulary() {
		string[] forbiddenFragments = [
			"ImageId",
			"ImageNumber",
			"PlacementId",
			"Generation",
			"Kitty",
			"Apc",
			"10EEEE",
			"Diacritic",
			"Combining"
		];

		Type[] publicTypes = [
			typeof( TerminalRasterPlaceholderOptions ),
			typeof( TerminalRasterPlaceholder ),
			typeof( TerminalRasterPlaceholderCell )
		];
		foreach ( Type type in publicTypes ) {
			foreach ( MemberInfo member in type.GetMembers( BindingFlags.Instance | BindingFlags.Public ) ) {
				Assert.DoesNotContain(
					forbiddenFragments,
					fragment => member.Name.Contains(
						fragment,
						StringComparison.OrdinalIgnoreCase
					)
				);
			}
		}
	}
}
