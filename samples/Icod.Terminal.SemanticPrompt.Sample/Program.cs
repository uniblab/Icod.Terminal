using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.BeginPromptAsync();
await session.WriteTextAsync(
	"demo> "
);

await session.BeginCommandInputAsync();
await session.WriteTextAsync(
	"echo hello\r\n"
);

await session.BeginCommandOutputAsync();
await session.WriteTextAsync(
	"hello\r\n"
);

await session.FinishCommandAsync( 0 );

await session.BeginPromptAsync();
await session.WriteTextAsync(
	"demo> "
);
await session.BeginCommandInputAsync();
await session.WriteTextAsync(
	"cancelled command^C\r\n"
);
await session.AbortCommandAsync();

await session.WriteTextAsync(
	"OSC 133 semantic-prompt sample complete.\r\n"
);
