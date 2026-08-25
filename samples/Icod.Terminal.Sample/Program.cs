using Icod.Terminal;

TerminalControlResult<TerminalEndpointObservation> observation =
	SystemTerminalControlProvider.Instance.Observe(
		TerminalEndpoint.StandardInput
	);

Console.WriteLine(
	$"Icod.Terminal T03 loaded; standard input observation: {observation.Status}."
);
return 0;
