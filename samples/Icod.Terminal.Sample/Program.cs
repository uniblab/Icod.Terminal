using Icod.Terminal;

TerminalControlResult<TerminalEndpointObservation> observation =
	SystemTerminalControlProvider.Instance.Observe(
		TerminalEndpoint.StandardInput
	);
TerminalControlResult<TerminalModeSnapshot> modeResult =
	SystemTerminalControlProvider.Instance.GetMode(
		TerminalEndpoint.StandardInput
	);

if ( modeResult.IsAvailable ) {
	TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
		modeResult.GetRequiredValue(),
		TerminalInputMode.CBreak,
		echoInput: false
	);

	Console.WriteLine(
		$"Icod.Terminal T04 loaded; standard input is {observation.Status}; "
		+ $"cbreak/noecho policy produced {configured.Platform} state."
	);
} else {
	Console.WriteLine(
		$"Icod.Terminal T04 loaded; standard input observation: {observation.Status}; "
		+ $"mode state: {modeResult.Status}."
	);
}

return 0;
