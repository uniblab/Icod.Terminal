using Icod.Terminal;

TerminalSemanticPromptOptions prompt = new(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
);
TerminalSemanticPromptOptions defaultPrompt = default;
TerminalSemanticCommandOutputOptions command = new(
	"printf café 😀"
);
TerminalSemanticCommandOutputOptions defaultCommand = default;

Func<TerminalSession, TerminalSemanticPromptOptions, CancellationToken, ValueTask> beginPrompt =
	BindBeginPrompt;
Func<TerminalSession, TerminalSemanticCommandOutputOptions, CancellationToken, ValueTask> beginCommandOutput =
	BindBeginCommandOutput;

_ = prompt.Kind;
_ = prompt.ResizeBehavior;
_ = prompt.UseSpecialCursorKey;
_ = prompt.ClickMode;
_ = defaultPrompt;
_ = command.CommandLine;
_ = defaultCommand;
_ = beginPrompt;
_ = beginCommandOutput;

Console.WriteLine( "Icod.Terminal 0.15 OSC 133 extended semantic metadata package API smoke passed." );

static ValueTask BindBeginPrompt(
	TerminalSession session,
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginPromptAsync(
		options,
		cancellationToken
	);
}

static ValueTask BindBeginCommandOutput(
	TerminalSession session,
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginCommandOutputAsync(
		options,
		cancellationToken
	);
}
