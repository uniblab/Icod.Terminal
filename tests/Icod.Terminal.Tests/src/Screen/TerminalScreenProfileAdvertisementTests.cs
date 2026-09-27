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
namespace Icod.Terminal.Tests;

using System.Reflection;
using Icod.TermInfo;
using Xunit;

/// <summary>Qualifies static advertisement independently from parameter-specific screen planning.</summary>
public sealed class TerminalScreenProfileAdvertisementTests {
	private static readonly string[] Properties = [
		"AdvertisesCursorHome", "AdvertisesCursorRowAddressing", "AdvertisesCursorColumnAddressing",
		"AdvertisesCarriageReturn", "AdvertisesCursorUp", "AdvertisesCursorDown",
		"AdvertisesCursorLeft", "AdvertisesCursorRight", "AdvertisesScrollRegion"
	];

	public static IEnumerable<object[]> Representations() {
		yield return [ StringCapability.CursorHome, "AdvertisesCursorHome", -1 ];
		yield return [ StringCapability.RowAddress, "AdvertisesCursorRowAddressing", -1 ];
		yield return [ StringCapability.ColumnAddress, "AdvertisesCursorColumnAddressing", -1 ];
		yield return [ StringCapability.CarriageReturn, "AdvertisesCarriageReturn", -1 ];
		yield return [ StringCapability.CursorUp, "AdvertisesCursorUp", -1 ];
		yield return [ StringCapability.CursorUpOne, "AdvertisesCursorUp", -1 ];
		yield return [ StringCapability.CursorDown, "AdvertisesCursorDown", -1 ];
		yield return [ StringCapability.CursorDownOne, "AdvertisesCursorDown", -1 ];
		yield return [ StringCapability.CursorLeft, "AdvertisesCursorLeft", -1 ];
		yield return [ StringCapability.CursorLeftOne, "AdvertisesCursorLeft", -1 ];
		yield return [ StringCapability.CursorRight, "AdvertisesCursorRight", -1 ];
		yield return [ StringCapability.CursorRightOne, "AdvertisesCursorRight", -1 ];
		yield return [ StringCapability.ChangeScrollRegion, "AdvertisesScrollRegion", -1 ];
		yield return [ StringCapability.ClearToEndOfLine, "AdvertisesErase", 0 ];
		yield return [ StringCapability.ClearToBeginningOfLine, "AdvertisesErase", 1 ];
		yield return [ StringCapability.ClearToEndOfScreen, "AdvertisesErase", 2 ];
		yield return [ StringCapability.ClearScreen, "AdvertisesErase", 3 ];
		yield return [ StringCapability.InsertCharacters, "AdvertisesCharacterShift", 0 ];
		yield return [ StringCapability.InsertCharacter, "AdvertisesCharacterShift", 0 ];
		yield return [ StringCapability.DeleteCharacters, "AdvertisesCharacterShift", 1 ];
		yield return [ StringCapability.DeleteCharacter, "AdvertisesCharacterShift", 1 ];
		yield return [ StringCapability.EraseCharacters, "AdvertisesCharacterShift", 2 ];
		yield return [ StringCapability.InsertLines, "AdvertisesLineShift", 0 ];
		yield return [ StringCapability.InsertLine, "AdvertisesLineShift", 0 ];
		yield return [ StringCapability.DeleteLines, "AdvertisesLineShift", 1 ];
		yield return [ StringCapability.DeleteLine, "AdvertisesLineShift", 1 ];
		yield return [ StringCapability.ScrollForwardLines, "AdvertisesLineShift", 2 ];
		yield return [ StringCapability.ScrollForward, "AdvertisesLineShift", 2 ];
		yield return [ StringCapability.ScrollReverseLines, "AdvertisesLineShift", 3 ];
		yield return [ StringCapability.ScrollReverse, "AdvertisesLineShift", 3 ];
	}

	[Theory]
	[MemberData( nameof( Representations ) )]
	public void EachRepresentationAdvertisesOnlyItsSemanticOperation(
		StringCapability capability, string member, int kind
	) {
		foreach ( string value in new[] { "X", string.Empty, "%p1%q" } ) {
			TerminalDescription terminal = new TerminalDescriptionBuilder( "advertisement" )
				.SetString( capability, value ).Build();
			TerminalScreenCapabilities screen = TerminalProfile.Create( terminal ).Screen;
			Assert.Equal( new[] { member + ":" + kind }, Advertisements( screen ) );
		}
	}

	[Fact]
	public void DefaultAndUnrelatedDescriptionsAdvertiseNoOperations() {
		Assert.Empty( Advertisements( default ) );
		TerminalScreenCapabilities unrelated = TerminalProfile.Create(
			new TerminalDescriptionBuilder( "unrelated" ).SetString( StringCapability.Bell, "B" ).Build()
		).Screen;
		Assert.Empty( Advertisements( unrelated ) );
	}

	[Theory]
	[InlineData( "AdvertisesErase", typeof( TerminalScreenEraseKind ) )]
	[InlineData( "AdvertisesCharacterShift", typeof( TerminalScreenCharacterShiftKind ) )]
	[InlineData( "AdvertisesLineShift", typeof( TerminalScreenLineShiftKind ) )]
	public void UnknownOperationKindIsRejected( string member, Type kindType ) {
		MethodInfo? method = typeof( TerminalScreenCapabilities ).GetMethod( member, [ kindType ] );
		Assert.NotNull( method );
		foreach ( int value in new[] { -1, int.MaxValue } ) {
			TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
				() => method.Invoke( default( TerminalScreenCapabilities ), [ Enum.ToObject( kindType, value ) ] )
			);
			Assert.IsType<ArgumentOutOfRangeException>( exception.InnerException );
		}
	}

	[Fact]
	public void ProjectionIsImmutableAndEqualityUsesFactsRatherThanDescriptionIdentity() {
		TerminalDescriptionBuilder builder = new( "first" );
		builder.SetString( StringCapability.CursorHome, "H" );
		TerminalScreenCapabilities before = TerminalProfile.Create( builder.Build() ).Screen;
		builder.SetString( StringCapability.ClearScreen, "C" );
		TerminalScreenCapabilities after = TerminalProfile.Create( builder.Build() ).Screen;
		TerminalScreenCapabilities equivalent = TerminalProfile.Create(
			new TerminalDescriptionBuilder( "second" ).SetString( StringCapability.CursorHome, "OTHER" ).Build()
		).Screen;
		Assert.Equal( new[] { "AdvertisesCursorHome:-1" }, Advertisements( before ) );
		Assert.Equal( before, equivalent );
		Assert.Equal( before.GetHashCode(), equivalent.GetHashCode() );
		Assert.NotEqual( before, after );
	}

	private static string[] Advertisements( TerminalScreenCapabilities screen ) {
		List<string> result = [];
		foreach ( string member in Properties ) {
			PropertyInfo? property = typeof( TerminalScreenCapabilities ).GetProperty( member );
			Assert.NotNull( property );
			Assert.Null( property.SetMethod );
			if ( (bool) property.GetValue( screen )! ) {
				result.Add( member + ":-1" );
			}
		}
		ReadKinds<TerminalScreenEraseKind>( "AdvertisesErase" );
		ReadKinds<TerminalScreenCharacterShiftKind>( "AdvertisesCharacterShift" );
		ReadKinds<TerminalScreenLineShiftKind>( "AdvertisesLineShift" );
		return result.ToArray();

		void ReadKinds<T>( string member ) where T : struct, Enum {
			MethodInfo? method = typeof( TerminalScreenCapabilities ).GetMethod( member, [ typeof( T ) ] );
			Assert.NotNull( method );
			foreach ( T kind in Enum.GetValues<T>() ) {
				if ( (bool) method.Invoke( screen, [ kind ] )! ) {
					result.Add( member + ":" + Convert.ToInt32( kind ) );
				}
			}
		}
	}
}
