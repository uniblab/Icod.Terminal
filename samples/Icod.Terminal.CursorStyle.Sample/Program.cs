using Icod.Terminal;

TerminalCursorStyle requestedStyle = TerminalCursorStyle.SteadyBar;
if ( 0 < args.Length
	&& !Enum.TryParse(
		args[ 0 ],
		ignoreCase: true,
		out requestedStyle
	) ) {
	Console.Error.WriteLine(
		$"Unknown cursor style '{args[ 0 ]}'."
	);
	Console.Error.WriteLine(
		"Use one of: "
			+ string.Join(
				", ",
				Enum.GetNames<TerminalCursorStyle>()
			)
	);
	return 2;
}

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

Console.WriteLine(
	$"Requesting cursor style: {requestedStyle}"
);
Console.WriteLine(
	"The sample first demonstrates an explicit cursor-style observation."
);
Console.WriteLine(
	"The scoped lease then performs its own baseline observation before mutation so restoration remains authoritative."
);

try {
	TerminalCursorStyleObservation observation = await session.QueryCursorStyleAsync(
		TimeSpan.FromMilliseconds( 750 )
	);
	if ( !observation.IsSupported ) {
		Console.WriteLine(
			"The terminal explicitly reported DECRQSS cursor-style observation as unsupported."
		);
		Console.WriteLine(
			"No cursor-style lease was acquired and no style was changed."
		);
		return 0;
	}

	Console.WriteLine(
		$"Observed current style: {observation.Style}"
	);
	Console.WriteLine(
		"Acquiring a scoped cursor-style lease. The lease re-observes its own restoration baseline."
	);

	await using TerminalCursorStyleLease lease = await session.AcquireCursorStyleAsync(
		requestedStyle,
		TimeSpan.FromMilliseconds( 750 )
	);

	Console.WriteLine(
		$"Leased style: {lease.Style}"
	);
	Console.WriteLine(
		"Press any key to restore the lease's independently observed prior style immediately."
	);

	_ = await session.ReadEventAsync();
} catch ( NotSupportedException ) {
	Console.WriteLine(
		"The lease's baseline observation reported cursor-style state as unsupported. No leased style was retained."
	);
} catch ( TimeoutException ) {
	Console.WriteLine(
		"A cursor-style query timed out. No support conclusion is inferred from a timeout."
	);
} catch ( FormatException exception ) {
	Console.WriteLine(
		$"The terminal returned an unrecognized cursor-style state: {exception.Message}"
	);
}

return 0;
