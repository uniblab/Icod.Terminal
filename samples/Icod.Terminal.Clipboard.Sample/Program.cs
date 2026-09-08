/*
	Icod.Terminal.Clipboard.Sample
	Sample application demonstrating Icod.Terminal Clipboard features.
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
using System.Text;
using Icod.Terminal;

TerminalClipboardSelection selection = TerminalClipboardSelection.Clipboard;
string text = 1 <= args.Length
	? args[ 0 ]
	: "Icod.Terminal OSC 52 clipboard sample"
;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteClipboardAsync(
	selection,
	text
);
await session.WriteTextAsync(
	"Wrote explicit UTF-8 text to the terminal clipboard selection.\r\n"
);

try {
	byte[] payload = await session.ReadClipboardAsync(
		selection,
		TimeSpan.FromMilliseconds( 750 )
	);
	string value = Encoding.UTF8.GetString( payload );
	await session.WriteTextAsync(
		$"Read {payload.Length} clipboard bytes: {value}\r\n"
	);
} catch ( TimeoutException ) {
	await session.WriteTextAsync(
		"The terminal did not return an OSC 52 clipboard response before the timeout.\r\n"
	);
} catch ( FormatException exception ) {
	await session.WriteTextAsync(
		$"The terminal returned an invalid OSC 52 clipboard response: {exception.Message}\r\n"
	);
}

return 0;
