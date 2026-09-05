using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

Console.WriteLine(
	"Beginning a synchronized-output scope. Successful completion proves only that mode-2026 frames were emitted."
);

await using ( TerminalSynchronizedOutputLease synchronized =
	await session.AcquireSynchronizedOutputAsync() ) {
	await session.WriteTextAsync( "line 1: synchronized update\r\n" );
	await session.WriteTextAsync( "line 2: ordinary session writes remain ordinary writes\r\n" );
	await session.SetWindowTitleAsync( "Icod.Terminal synchronized output" );
	await session.WriteTextAsync( "line 3: final lease disposal emits the synchronized-output end boundary\r\n" );
}

Console.WriteLine(
	"Synchronized-output scope released."
);
