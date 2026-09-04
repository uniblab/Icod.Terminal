using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync(
	"Icod.Terminal 0.4 title sample.\r\n"
);
await session.WriteTextAsync(
	"This sample emits OSC 0, OSC 1, and OSC 2. The previous terminal title is not queried or restored.\r\n"
);

await session.SetTitleAsync(
	"Icod.Terminal — OSC 0"
);
await Task.Delay(
	TimeSpan.FromSeconds( 1 )
);

await session.SetIconNameAsync(
	"Icod.Terminal icon"
);
await Task.Delay(
	TimeSpan.FromSeconds( 1 )
);

await session.SetWindowTitleAsync(
	"Icod.Terminal — OSC 2"
);

await session.WriteTextAsync(
	"Emitted SetTitleAsync, SetIconNameAsync, and SetWindowTitleAsync.\r\n"
);
await session.WriteTextAsync(
	"Successful completion means the frames were written; it does not prove the terminal applied them.\r\n"
);
await session.WriteTextAsync(
	"Press Enter to exit. The title will remain whatever the terminal chose to apply.\r\n"
);

while ( true ) {
	TerminalEvent terminalEvent = await session.ReadEventAsync();
	if ( TerminalEventKind.Input != terminalEvent.Kind
		|| terminalEvent.Input is null ) {
		continue;
	}

	TerminalInputEvent input = terminalEvent.Input;
	if ( TerminalInputEventKind.Text == input.Kind
		&& input.Character.HasValue
		&& ( '\r' == input.Character.Value.Value
			|| '\n' == input.Character.Value.Value ) ) {
		break;
	}
	if ( TerminalInputEventKind.Key == input.Kind
		&& TerminalKey.Enter == input.Key ) {
		break;
	}
}

return 0;
