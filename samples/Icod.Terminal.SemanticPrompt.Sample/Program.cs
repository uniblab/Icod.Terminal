using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

// The original portable OSC 133 A/B/C/D path remains unchanged.
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

// 0.15 adds typed prompt metadata. Emit extended fields only when the
// application/shell integration intentionally wants those semantics.
TerminalSemanticPromptOptions promptOptions = new(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
);

await session.BeginPromptAsync( promptOptions );
await session.WriteTextAsync(
	"continue> "
);
await session.BeginCommandInputAsync();
await session.WriteTextAsync(
	"printf 'café 😀'\r\n"
);

// Command-line publication is explicit. Do not publish text that may contain
// credentials, tokens, private paths, or other sensitive information unless
// that disclosure is appropriate for the application.
TerminalSemanticCommandOutputOptions commandOptions = new(
	"printf 'café 😀'"
);
await session.BeginCommandOutputAsync( commandOptions );
await session.WriteTextAsync(
	"café 😀\r\n"
);
await session.FinishCommandAsync( 0 );

// Abort remains a bare D marker with no status.
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
