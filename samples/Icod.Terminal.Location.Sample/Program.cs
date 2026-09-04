using Icod.Terminal;

if ( 2 > args.Length || 3 < args.Length ) {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.Location.Sample <posix|windows|unc> <absolute-path> [authority]"
	);
	return 2;
}

TerminalLocationPathStyle pathStyle = args[ 0 ].ToLowerInvariant() switch {
	"posix" => TerminalLocationPathStyle.Posix,
	"windows" => TerminalLocationPathStyle.WindowsDrive,
	"unc" => TerminalLocationPathStyle.WindowsUnc,
	_ => throw new ArgumentException(
		"The path style must be 'posix', 'windows', or 'unc'.",
		nameof( args )
	)
};
string path = args[ 1 ];
string? authority = 3 == args.Length
	? args[ 2 ]
	: null;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync(
	"Icod.Terminal 0.5 OSC 7 current-location sample.\r\n"
);
await session.WriteTextAsync(
	"This sample publishes only the location supplied explicitly on the command line.\r\n"
);
await session.PublishCurrentLocationAsync(
	path,
	pathStyle,
	authority
);
await session.WriteTextAsync(
	"OSC 7 location frame emitted. Successful completion does not prove the terminal used it.\r\n"
);

return 0;
