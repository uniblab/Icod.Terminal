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
/// Freezes the additive 1.27 terminal-environment event contract.
/// </summary>
public sealed class TerminalEnvironmentPublicApiTests {
	private static readonly Assembly TerminalAssembly = typeof( TerminalSession ).Assembly;

	[Fact]
	public void AppearanceEnumHasFrozenValues() {
		Type appearance = GetRequiredType( "Icod.Terminal.TerminalAppearance" );

		Assert.True( appearance.IsPublic );
		Assert.True( appearance.IsEnum );
		Assert.Equal(
			new[] { "Unknown", "Dark", "Light" },
			Enum.GetNames( appearance )
		);
		Assert.Equal( 0, Convert.ToInt32( Enum.Parse( appearance, "Unknown" ) ) );
		Assert.Equal( 1, Convert.ToInt32( Enum.Parse( appearance, "Dark" ) ) );
		Assert.Equal( 2, Convert.ToInt32( Enum.Parse( appearance, "Light" ) ) );
	}

	[Fact]
	public void EnvironmentPayloadsAreSealedReadOnlyAndInternallyConstructed() {
		Type appearanceType = GetRequiredType(
			"Icod.Terminal.TerminalAppearanceEvent"
		);
		Type resizeType = GetRequiredType(
			"Icod.Terminal.TerminalInBandResizeEvent"
		);

		AssertPublicSealedInternalConstruction( appearanceType );
		AssertPublicSealedInternalConstruction( resizeType );
		AssertReadOnlyProperty( appearanceType, "Appearance" );
		AssertReadOnlyProperty( resizeType, "Dimensions" );
		AssertReadOnlyProperty( resizeType, "PixelDimensions" );
	}

	[Fact]
	public void SemanticKindsAppendWithoutRenumberingNotification() {
		Assert.Equal( 0, (int)TerminalSemanticEventKind.Notification );

		Type kind = typeof( TerminalSemanticEventKind );
		Assert.Equal(
			new[] { "Notification", "Appearance", "InBandResize" },
			Enum.GetNames( kind )
		);
		Assert.Equal( 1, Convert.ToInt32( Enum.Parse( kind, "Appearance" ) ) );
		Assert.Equal( 2, Convert.ToInt32( Enum.Parse( kind, "InBandResize" ) ) );
	}

	[Fact]
	public void SemanticFactoriesExposeExactlyOneEnvironmentPayload() {
		Type appearanceValueType = GetRequiredType(
			"Icod.Terminal.TerminalAppearance"
		);
		Type appearanceEventType = GetRequiredType(
			"Icod.Terminal.TerminalAppearanceEvent"
		);
		Type resizeEventType = GetRequiredType(
			"Icod.Terminal.TerminalInBandResizeEvent"
		);

		object dark = Enum.Parse( appearanceValueType, "Dark" );
		object appearanceEvent = InvokeInternalConstructor(
			appearanceEventType,
			dark
		);
		object resizeEvent = InvokeInternalConstructor(
			resizeEventType,
			new TerminalDimensions( 120, 40 ),
			new TerminalPixelDimensions( 1200, 800 )
		);
		Assert.Equal(
			dark,
			appearanceEventType.GetProperty( "Appearance" )!.GetValue( appearanceEvent )
		);
		Assert.Equal(
			new TerminalDimensions( 120, 40 ),
			resizeEventType.GetProperty( "Dimensions" )!.GetValue( resizeEvent )
		);
		Assert.Equal(
			new TerminalPixelDimensions( 1200, 800 ),
			resizeEventType.GetProperty( "PixelDimensions" )!.GetValue( resizeEvent )
		);

		TerminalSemanticEvent appearance = InvokeSemanticFactory(
			"FromAppearance",
			appearanceEvent
		);
		TerminalSemanticEvent resize = InvokeSemanticFactory(
			"FromInBandResize",
			resizeEvent
		);

		Assert.Equal( 1, (int)appearance.Kind );
		Assert.Null( appearance.Notification );
		Assert.Same(
			appearanceEvent,
			typeof( TerminalSemanticEvent ).GetProperty( "Appearance" )!.GetValue( appearance )
		);
		Assert.Null(
			typeof( TerminalSemanticEvent ).GetProperty( "InBandResize" )!.GetValue( appearance )
		);

		Assert.Equal( 2, (int)resize.Kind );
		Assert.Null( resize.Notification );
		Assert.Null(
			typeof( TerminalSemanticEvent ).GetProperty( "Appearance" )!.GetValue( resize )
		);
		Assert.Same(
			resizeEvent,
			typeof( TerminalSemanticEvent ).GetProperty( "InBandResize" )!.GetValue( resize )
		);
	}

	[Fact]
	public void AppearanceEventRejectsUnknownAppearance() {
		Type appearanceValueType = GetRequiredType(
			"Icod.Terminal.TerminalAppearance"
		);
		Type appearanceEventType = GetRequiredType(
			"Icod.Terminal.TerminalAppearanceEvent"
		);
		object unknown = Enum.Parse( appearanceValueType, "Unknown" );

		TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
			() => InvokeInternalConstructor( appearanceEventType, unknown )
		);
		Assert.IsType<ArgumentOutOfRangeException>( exception.InnerException );
	}

	[Fact]
	public void ExistingNotificationFactoryStillHasOnlyNotificationPayload() {
		TerminalNotificationEvent notification = TerminalNotificationEvent.Activated(
			"environment-contract"
		);
		TerminalSemanticEvent semantic = TerminalSemanticEvent.FromNotification(
			notification
		);

		Assert.Equal( TerminalSemanticEventKind.Notification, semantic.Kind );
		Assert.Same( notification, semantic.Notification );
		Assert.Null(
			typeof( TerminalSemanticEvent ).GetProperty( "Appearance" )!.GetValue( semantic )
		);
		Assert.Null(
			typeof( TerminalSemanticEvent ).GetProperty( "InBandResize" )!.GetValue( semantic )
		);
	}

	private static Type GetRequiredType(
		string fullName
	) => TerminalAssembly.GetType( fullName, throwOnError: true )!;

	private static void AssertPublicSealedInternalConstruction(
		Type type
	) {
		Assert.True( type.IsPublic );
		Assert.True( type.IsSealed );
		Assert.Empty( type.GetConstructors( BindingFlags.Instance | BindingFlags.Public ) );
		Assert.Single( type.GetConstructors( BindingFlags.Instance | BindingFlags.NonPublic ) );
	}

	private static void AssertReadOnlyProperty(
		Type type,
		string name
	) {
		PropertyInfo property = type.GetProperty( name )!;
		Assert.NotNull( property );
		Assert.NotNull( property.GetMethod );
		Assert.True( property.GetMethod!.IsPublic );
		Assert.Null( property.SetMethod );
	}

	private static object InvokeInternalConstructor(
		Type type,
		params object?[] arguments
	) => Activator.CreateInstance(
		type,
		BindingFlags.Instance | BindingFlags.NonPublic,
		binder: null,
		args: arguments,
		culture: null
	)!;

	private static TerminalSemanticEvent InvokeSemanticFactory(
		string name,
		object payload
	) => (TerminalSemanticEvent)typeof( TerminalSemanticEvent ).GetMethod(
		name,
		BindingFlags.Static | BindingFlags.NonPublic
	)!.Invoke( null, new[] { payload } )!;
}
