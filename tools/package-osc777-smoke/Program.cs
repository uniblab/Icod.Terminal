/*
	Icod.Terminal.PackageOsc777Smoke
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

Func<TerminalSession, string, string, CancellationToken, ValueTask> sendTitled =
	BindSendTitledNotification;
_ = sendTitled;

MethodInfo[] publicMethods = typeof( TerminalSession ).GetMethods(
	BindingFlags.Public | BindingFlags.Instance
);
if ( !publicMethods.Any(
	method => string.Equals(
		method.Name,
		nameof( TerminalSession.SendTitledNotificationAsync ),
		StringComparison.Ordinal
	)
) ) {
	throw new InvalidOperationException(
		"The shipped package is missing TerminalSession.SendTitledNotificationAsync."
	);
}

string[] forbiddenNames = [
	"WriteOsc777Async",
	"WriteRawOsc777Async",
	"SendOsc777Async",
	"SendOsc777NotificationAsync"
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
			$"The shipped public TerminalSession surface exposes raw/generic OSC 777 API '{forbiddenName}'."
		);
	}
}

Console.WriteLine(
	"Icod.Terminal 1.2 OSC 777 package API and exclusion smoke passed."
);

static ValueTask BindSendTitledNotification(
	TerminalSession session,
	string title,
	string message,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( title );
	ArgumentNullException.ThrowIfNull( message );
	return session.SendTitledNotificationAsync(
		title,
		message,
		cancellationToken
	);
}
