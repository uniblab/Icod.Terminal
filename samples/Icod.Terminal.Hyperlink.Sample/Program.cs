/*
	Icod.Terminal.Hyperlink.Sample
	Sample application demonstrating Icod.Terminal Hyperlink features.
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

if ( 2 > args.Length ) {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.Hyperlink.Sample <uri> <text> [identifier]"
	);
	return 2;
}

string uri = args[ 0 ];
string text = args[ 1 ];
string? identifier = 3 <= args.Length
	? args[ 2 ]
	: null
;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync(
	"Icod.Terminal 0.6 OSC 8 hyperlink sample.\r\n"
);

await session.WriteHyperlinkAsync(
	text,
	uri,
	identifier
);
await session.WriteTextAsync( "\r\n" );

await using TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
	uri,
	identifier
);
await session.WriteTextAsync( "Scoped hyperlink state: outer" );

await using ( TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
	"https://example.com/",
	"nested"
) ) {
	await session.WriteTextAsync( " -> nested" );
}

await session.WriteTextAsync( " -> outer restored\r\n" );
return 0;
