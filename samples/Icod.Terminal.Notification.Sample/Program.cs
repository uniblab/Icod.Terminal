/*
	Icod.Terminal.Notification.Sample
	Sample application demonstrating Icod.Terminal Notification features.
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
using System.Diagnostics;
using Icod.Terminal;

if ( 0 == args.Length ) {
	WriteUsage();
	return 2;
}

bool titled = string.Equals(
	args[ 0 ],
	"--titled",
	StringComparison.Ordinal
);
bool kitty = string.Equals(
	args[ 0 ],
	"--kitty",
	StringComparison.Ordinal
);
bool kittyInteractive = string.Equals(
	args[ 0 ],
	"--kitty-interactive",
	StringComparison.Ordinal
);
if ( ( titled || kitty ) && 3 > args.Length ) {
	WriteUsage();
	return 2;
}
if ( kittyInteractive && 4 > args.Length ) {
	WriteUsage();
	return 2;
}

string? identifier = kittyInteractive
	? args[ 1 ]
	: null
;
string? title = kittyInteractive
	? args[ 2 ]
	: titled || kitty
		? args[ 1 ]
		: null
;
string message = kittyInteractive
	? string.Join(
		" ",
		args[ 3.. ]
	)
	: titled || kitty
		? string.Join(
			" ",
			args[ 2.. ]
		)
		: string.Join(
			" ",
			args
		)
;

Console.WriteLine(
	"Icod.Terminal desktop notification sample."
);
Console.WriteLine(
	"Warning: notification content may be visible in desktop notification history, lock screens, screen sharing, terminal logs, or remote/multiplexed sessions."
);
Console.WriteLine(
	"This sample publishes only values supplied explicitly on the command line plus fixed sample button labels, and never chooses a protocol from terminal branding."
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

if ( kittyInteractive ) {
	await session.SendKittyNotificationAsync(
		title!,
		message,
		new KittyNotificationOptions {
			Identifier = identifier,
			ApplicationName = "Icod.Terminal.Notification.Sample",
			NotificationTypes = [ "sample" ],
			ReportActivation = true,
			ReportClose = true,
			Buttons = [ "Acknowledge", "Dismiss" ]
		}
	);
	Console.WriteLine(
		"Interactive Kitty OSC 99 notification request emitted with activation/button and close reporting enabled."
	);
	Console.WriteLine(
		"Waiting up to 30 seconds for a matching typed notification event through TerminalSession.ReadEventAsync(...)."
	);
	await ObserveInteractiveNotificationAsync(
		session,
		identifier!
	);
} else if ( kitty ) {
	await session.SendKittyNotificationAsync(
		title!,
		message,
		new KittyNotificationOptions {
			ApplicationName = "Icod.Terminal.Notification.Sample",
			NotificationTypes = [ "sample" ]
		}
	);
	Console.WriteLine(
		"Kitty OSC 99 notification request emitted. Successful completion does not prove the desktop displayed it."
	);
} else if ( titled ) {
	await session.SendTitledNotificationAsync(
		title!,
		message
	);
	Console.WriteLine(
		"OSC 777 titled notification request emitted. Successful completion does not prove the desktop displayed it."
	);
} else {
	await session.SendNotificationAsync( message );
	Console.WriteLine(
		"OSC 9 notification request emitted. Successful completion does not prove the desktop displayed it."
	);
}

return 0;

static async ValueTask ObserveInteractiveNotificationAsync(
	TerminalSession session,
	string identifier
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentException.ThrowIfNullOrEmpty( identifier );

	TimeSpan timeout = TimeSpan.FromSeconds( 30 );
	Stopwatch stopwatch = Stopwatch.StartNew();
	while ( stopwatch.Elapsed < timeout ) {
		TimeSpan remaining = timeout - stopwatch.Elapsed;
		TerminalEvent terminalEvent = await session.ReadEventAsync( remaining );
		if ( TerminalEventKind.Timeout == terminalEvent.Kind ) {
			break;
		}
		if ( TerminalEventKind.Semantic != terminalEvent.Kind ) {
			continue;
		}

		TerminalNotificationEvent? notification = terminalEvent.Semantic?.Notification;
		if ( null == notification ) {
			continue;
		}
		if ( !string.Equals(
			notification.Identifier,
			identifier,
			StringComparison.Ordinal
		) ) {
			continue;
		}

		Console.WriteLine(
			$"Received notification event: {notification.Kind}."
		);
		if ( notification.ButtonNumber.HasValue ) {
			Console.WriteLine(
				$"Button number: {notification.ButtonNumber.Value}."
			);
		}
		Console.WriteLine(
			"The report is validated terminal input, not proof of an authenticated desktop interaction."
		);
		return;
	}

	Console.WriteLine(
		"No matching notification event was observed before the sample timeout. This does not prove the terminal lacks support."
	);
}

static void WriteUsage() {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.Notification.Sample <notification text>"
	);
	Console.Error.WriteLine(
		"   or: Icod.Terminal.Notification.Sample --titled <title> <notification text>"
	);
	Console.Error.WriteLine(
		"   or: Icod.Terminal.Notification.Sample --kitty <title> <notification text>"
	);
	Console.Error.WriteLine(
		"   or: Icod.Terminal.Notification.Sample --kitty-interactive <identifier> <title> <notification text>"
	);
}
