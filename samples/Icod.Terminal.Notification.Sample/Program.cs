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
if ( ( titled || kitty ) && 3 > args.Length ) {
	WriteUsage();
	return 2;
}

string? title = titled || kitty
	? args[ 1 ]
	: null
;
string message = titled || kitty
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
	"This sample publishes only the title/text supplied explicitly on the command line and never chooses a protocol from terminal branding."
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

if ( kitty ) {
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
}
