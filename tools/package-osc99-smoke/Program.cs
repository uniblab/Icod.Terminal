/*
	Icod.Terminal.PackageOsc99Smoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Reflection;
using Icod.Terminal;

Func<TerminalSession, string, string, KittyNotificationOptions?, CancellationToken, ValueTask> send =
	BindSendKittyNotification;
Func<TerminalSession, string, CancellationToken, ValueTask> close = BindCloseKittyNotification;
Func<TerminalSession, TimeSpan, CancellationToken, ValueTask<KittyNotificationSupport>> querySupport =
	BindQuerySupport;
Func<TerminalSession, TimeSpan, CancellationToken, ValueTask<IReadOnlyList<string>>> queryAlive =
	BindQueryAlive;
_ = send;
_ = close;
_ = querySupport;
_ = queryAlive;

RequireEnumValue(
	TerminalEventKind.Input,
	0
);
RequireEnumValue(
	TerminalEventKind.Lifecycle,
	1
);
RequireEnumValue(
	TerminalEventKind.Timeout,
	2
);
RequireEnumValue(
	TerminalEventKind.Cancelled,
	3
);
RequireEnumValue(
	TerminalEventKind.Semantic,
	4
);
RequireEnumValue(
	TerminalSemanticEventKind.Notification,
	0
);
RequireEnumValue(
	TerminalNotificationEventKind.Activated,
	0
);
RequireEnumValue(
	TerminalNotificationEventKind.ButtonActivated,
	1
);
RequireEnumValue(
	TerminalNotificationEventKind.Closed,
	2
);
RequireEnumValue(
	TerminalNotificationEventKind.CloseTrackingUnavailable,
	3
);
RequireEnumValue(
	KittyNotificationOccasion.Always,
	0
);
RequireEnumValue(
	KittyNotificationOccasion.Unfocused,
	1
);
RequireEnumValue(
	KittyNotificationOccasion.Invisible,
	2
);
RequireEnumValue(
	KittyNotificationUrgency.Low,
	0
);
RequireEnumValue(
	KittyNotificationUrgency.Normal,
	1
);
RequireEnumValue(
	KittyNotificationUrgency.Critical,
	2
);

string[] requiredOptionProperties = [
	nameof( KittyNotificationOptions.Identifier ),
	nameof( KittyNotificationOptions.ApplicationName ),
	nameof( KittyNotificationOptions.NotificationTypes ),
	nameof( KittyNotificationOptions.FocusOnActivation ),
	nameof( KittyNotificationOptions.ReportActivation ),
	nameof( KittyNotificationOptions.ReportClose ),
	nameof( KittyNotificationOptions.Buttons ),
	nameof( KittyNotificationOptions.Occasion ),
	nameof( KittyNotificationOptions.Urgency ),
	nameof( KittyNotificationOptions.Expiration ),
	nameof( KittyNotificationOptions.SoundName ),
	nameof( KittyNotificationOptions.IconNames ),
	nameof( KittyNotificationOptions.IconData ),
	nameof( KittyNotificationOptions.IconDataIdentifier )
];
foreach ( string propertyName in requiredOptionProperties ) {
	RequirePublicProperty(
		typeof( KittyNotificationOptions ),
		propertyName
	);
}

string[] requiredSupportProperties = [
	nameof( KittyNotificationSupport.SupportsFocusAction ),
	nameof( KittyNotificationSupport.SupportsActivationReports ),
	nameof( KittyNotificationSupport.SupportsCloseEvents ),
	nameof( KittyNotificationSupport.SupportsTitle ),
	nameof( KittyNotificationSupport.SupportsBody ),
	nameof( KittyNotificationSupport.SupportsClose ),
	nameof( KittyNotificationSupport.SupportsIconData ),
	nameof( KittyNotificationSupport.SupportsAliveQuery ),
	nameof( KittyNotificationSupport.SupportsButtons ),
	nameof( KittyNotificationSupport.SupportsAutoExpiration ),
	nameof( KittyNotificationSupport.Occasions ),
	nameof( KittyNotificationSupport.Urgencies ),
	nameof( KittyNotificationSupport.Sounds )
];
foreach ( string propertyName in requiredSupportProperties ) {
	RequirePublicProperty(
		typeof( KittyNotificationSupport ),
		propertyName
	);
}

RequirePublicProperty(
	typeof( TerminalEvent ),
	nameof( TerminalEvent.Semantic )
);
RequirePublicProperty(
	typeof( TerminalSemanticEvent ),
	nameof( TerminalSemanticEvent.Kind )
);
RequirePublicProperty(
	typeof( TerminalSemanticEvent ),
	nameof( TerminalSemanticEvent.Notification )
);
RequirePublicProperty(
	typeof( TerminalNotificationEvent ),
	nameof( TerminalNotificationEvent.Kind )
);
RequirePublicProperty(
	typeof( TerminalNotificationEvent ),
	nameof( TerminalNotificationEvent.Identifier )
);
RequirePublicProperty(
	typeof( TerminalNotificationEvent ),
	nameof( TerminalNotificationEvent.ButtonNumber )
);

MethodInfo[] publicMethods = typeof( TerminalSession ).GetMethods(
	BindingFlags.Public | BindingFlags.Instance
);
string[] forbiddenNames = [
	"WriteOsc99Async",
	"WriteRawOsc99Async",
	"SendOsc99Async",
	"SendRawKittyNotificationAsync",
	"ReadKittyNotificationEventAsync",
	"WaitForKittyNotificationEventAsync",
	"ReadSemanticEventAsync",
	"ReadTerminalSemanticEventAsync",
	"ReadRawTerminalEventAsync"
];
foreach ( string forbiddenName in forbiddenNames ) {
	if ( publicMethods.Any(
		method => string.Equals(
			method.Name,
			forbiddenName,
			StringComparison.Ordinal
		)
	) ) {
		throw new InvalidOperationException(
			$"The shipped public TerminalSession surface exposes excluded OSC 99 API '{forbiddenName}'."
		);
	}
}

Console.WriteLine(
	"Icod.Terminal 1.9 Kitty OSC 99 semantic-event package API and exclusion smoke passed."
);

static ValueTask BindSendKittyNotification(
	TerminalSession session,
	string title,
	string body,
	KittyNotificationOptions? options,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.SendKittyNotificationAsync(
		title,
		body,
		options,
		cancellationToken
	);
}

static ValueTask BindCloseKittyNotification(
	TerminalSession session,
	string identifier,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.CloseKittyNotificationAsync(
		identifier,
		cancellationToken
	);
}

static ValueTask<KittyNotificationSupport> BindQuerySupport(
	TerminalSession session,
	TimeSpan timeout,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.QueryKittyNotificationSupportAsync(
		timeout,
		cancellationToken
	);
}

static ValueTask<IReadOnlyList<string>> BindQueryAlive(
	TerminalSession session,
	TimeSpan timeout,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.QueryKittyAliveNotificationsAsync(
		timeout,
		cancellationToken
	);
}

static void RequirePublicProperty(
	Type type,
	string propertyName
) {
	ArgumentNullException.ThrowIfNull( type );
	ArgumentException.ThrowIfNullOrEmpty( propertyName );
	PropertyInfo? property = type.GetProperty(
		propertyName,
		BindingFlags.Public | BindingFlags.Instance
	);
	if ( property is null ) {
		throw new InvalidOperationException(
			$"The shipped type '{type.FullName}' is missing public property '{propertyName}'."
		);
	}
}

static void RequireEnumValue<TEnum>(
	TEnum value,
	int expected
) where TEnum : struct, Enum {
	int actual = Convert.ToInt32( value );
	if ( expected != actual ) {
		throw new InvalidOperationException(
			$"The enum value '{typeof( TEnum ).Name}.{value}' is {actual}; expected {expected}."
		);
	}
}
