using Icod.Terminal;

bool mutate = args.Any(
	argument => string.Equals(
		argument,
		"--mutate",
		StringComparison.OrdinalIgnoreCase
	)
);
TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

TerminalColor palette = await session.QueryPaletteColorAsync(
	1,
	timeout
);
TerminalColor foreground = await session.QueryDynamicColorAsync(
	TerminalDynamicColor.DefaultForeground,
	timeout
);
TerminalColor background = await session.QueryDynamicColorAsync(
	TerminalDynamicColor.DefaultBackground,
	timeout
);

Console.WriteLine(
	$"palette[1] = {Format( palette )}"
);
Console.WriteLine(
	$"default foreground = {Format( foreground )}"
);
Console.WriteLine(
	$"default background = {Format( background )}"
);

if ( !mutate ) {
	Console.WriteLine(
		"Pass --mutate to demonstrate explicit palette/dynamic-color mutation and terminal-policy reset."
	);
	return;
}

await session.SetPaletteColorAsync(
	1,
	TerminalColor.FromRgb8( 255, 64, 64 )
);
await session.SetDynamicColorAsync(
	TerminalDynamicColor.TextCursor,
	TerminalColor.FromRgb8( 64, 255, 64 )
);

Console.WriteLine(
	"Applied demo palette[1] and text-cursor colors. Press Enter to request terminal-policy reset."
);
Console.ReadLine();

await session.ResetPaletteColorAsync( 1 );
await session.ResetDynamicColorAsync(
	TerminalDynamicColor.TextCursor
);

static string Format(
	TerminalColor color
) {
	return $"rgb:{color.Red:x4}/{color.Green:x4}/{color.Blue:x4}";
}
