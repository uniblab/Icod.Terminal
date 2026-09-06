using Icod.Terminal;

if ( 2 > args.Length || 3 < args.Length ) {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.Location.Sample <posix|windows|unc|windows-osc9> <path> [authority]"
	);
	return 2;
}

string mode = args[ 0 ].ToLowerInvariant();
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
	"Icod.Terminal current-location sample.\r\n"
);
await session.WriteTextAsync(
	"Location publication is explicit; the library never reads the process CWD automatically.\r\n"
);

if ( "windows-osc9" == mode ) {
	if ( authority is not null ) {
		throw new ArgumentException(
			"windows-osc9 does not accept an authority argument.",
			nameof( args )
		);
	}

	await session.WriteTextAsync(
		"Using the explicitly secondary OSC 9;9 Windows Terminal/ConEmu compatibility form.\r\n"
	);
	await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
		path
	);
	await session.WriteTextAsync(
		"OSC 9;9 compatibility frame emitted. OSC 7 remains the preferred portable location protocol.\r\n"
	);
	return 0;
}

TerminalLocationPathStyle pathStyle = mode switch {
	"posix" => TerminalLocationPathStyle.Posix,
	"windows" => TerminalLocationPathStyle.WindowsDrive,
	"unc" => TerminalLocationPathStyle.WindowsUnc,
	_ => throw new ArgumentException(
		"The path style must be 'posix', 'windows', 'unc', or 'windows-osc9'.",
		nameof( args )
	)
};

await session.PublishCurrentLocationAsync(
	path,
	pathStyle,
	authority
);
await session.WriteTextAsync(
	"OSC 7 location frame emitted. Successful completion does not prove the terminal used it.\r\n"
);

return 0;
