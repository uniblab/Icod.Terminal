using Icod.Terminal;
using Icod.TermInfo;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

TerminalControlResult<TerminalSize> sizeResult = session.GetSize();
string sizeText;
if ( sizeResult.IsAvailable ) {
	TerminalSize size = sizeResult.GetRequiredValue();
	sizeText = string.Concat(
		size.Columns.ToString( System.Globalization.CultureInfo.InvariantCulture ),
		"x",
		size.Rows.ToString( System.Globalization.CultureInfo.InvariantCulture )
	);
} else {
	sizeText = sizeResult.Status.ToString();
}

await session.WriteTextAsync(
	string.Concat(
		"Icod.Terminal session opened via ",
		session.Identity.Source.ToString(),
		"; size=",
		sizeText,
		".\r\n"
	)
);

return 0;
