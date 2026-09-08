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
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.Notification.Sample <notification text>"
	);
	return 2;
}

string message = string.Join(
	" ",
	args
);

Console.WriteLine(
	"Icod.Terminal 0.16 OSC 9 notification sample."
);
Console.WriteLine(
	"Warning: notification text may be visible in desktop notification history, lock screens, screen sharing, terminal logs, or remote/multiplexed sessions."
);
Console.WriteLine(
	"This sample publishes only the text supplied explicitly on the command line."
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.SendNotificationAsync( message );

Console.WriteLine(
	"OSC 9 notification request emitted. Successful completion does not prove the desktop displayed it."
);
return 0;
