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
