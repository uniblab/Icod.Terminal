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
