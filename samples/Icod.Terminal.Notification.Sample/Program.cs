using Icod.Terminal;

if ( 0 == args.Length ) {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.Notification.Sample <notification text>"
	);
	return 2;
}

string message = string.Join(
	" ",
	args
);

Console.WriteLine(
	"Icod.Terminal 0.16 OSC 9 notification sample."
);
Console.WriteLine(
	"Warning: notification text may be visible in desktop notification history, lock screens, screen sharing, terminal logs, or remote/multiplexed sessions."
);
Console.WriteLine(
	"This sample publishes only the text supplied explicitly on the command line."
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.SendNotificationAsync( message );

Console.WriteLine(
	"OSC 9 notification request emitted. Successful completion does not prove the desktop displayed it."
);
return 0;
