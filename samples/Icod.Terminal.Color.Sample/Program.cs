using Icod.Terminal;

bool mutate = args.Any(
	argument => string.Equals(
		argument,
		"--mutate",
		StringComparison.OrdinalIgnoreCase
	)
);
TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await WriteLineAsync(
	session,
	"Opening the session performs no automatic color interrogation."
);
await WriteLineAsync(
	session,
	"The following observations are explicit bounded queries."
);

await ReportColorAsync(
	session,
	"palette[1]",
	() => session.QueryPaletteColorAsync(
		1,
		timeout
	)
);
await ReportColorAsync(
	session,
	"default foreground",
	() => session.QueryDynamicColorAsync(
		TerminalDynamicColor.DefaultForeground,
		timeout
	)
);
await ReportColorAsync(
	session,
	"default background",
	() => session.QueryDynamicColorAsync(
		TerminalDynamicColor.DefaultBackground,
		timeout
	)
);

if ( !mutate ) {
	await WriteLineAsync(
		session,
		"Pass --mutate to demonstrate explicit palette/dynamic-color mutation and terminal-policy reset."
	);
	return 0;
}

await WriteLineAsync(
	session,
	"Mutation mode is opt-in. Reset requests terminal policy/defaults; it is not exact restoration of an observed baseline."
);

bool paletteMutated = false;
bool cursorMutated = false;
try {
	await session.SetPaletteColorAsync(
		1,
		TerminalColor.FromRgb8( 255, 64, 64 )
	);
	paletteMutated = true;

	await session.SetDynamicColorAsync(
		TerminalDynamicColor.TextCursor,
		TerminalColor.FromRgb8( 64, 255, 64 )
	);
	cursorMutated = true;

	await WriteLineAsync(
		session,
		"Applied demo palette[1] and text-cursor colors. Generate one terminal input event, or wait 30 seconds, before reset."
	);

	TerminalEvent terminalEvent = await session.ReadEventAsync(
		TimeSpan.FromSeconds( 30 )
	);
	if ( TerminalEventKind.Timeout == terminalEvent.Kind ) {
		await WriteLineAsync(
			session,
			"No input event arrived before the timeout; requesting terminal-policy reset now."
		);
	} else {
		await WriteLineAsync(
			session,
			$"Observed {terminalEvent.Kind}; requesting terminal-policy reset now."
		);
	}
} finally {
	try {
		if ( cursorMutated ) {
			await session.ResetDynamicColorAsync(
				TerminalDynamicColor.TextCursor
			);
		}
	} finally {
		if ( paletteMutated ) {
			await session.ResetPaletteColorAsync( 1 );
		}
	}
}

return 0;

static async ValueTask ReportColorAsync(
	TerminalSession session,
	string label,
	Func<ValueTask<TerminalColor>> query
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentException.ThrowIfNullOrWhiteSpace( label );
	ArgumentNullException.ThrowIfNull( query );

	try {
		TerminalColor color = await query();
		await WriteLineAsync(
			session,
			$"{label} = {Format( color )}"
		);
	} catch ( TimeoutException ) {
		await WriteLineAsync(
			session,
			$"{label} = timed out"
		);
	} catch ( FormatException exception ) {
		await WriteLineAsync(
			session,
			$"{label} = malformed correlated response: {exception.Message}"
		);
	} catch ( InvalidOperationException exception ) {
		await WriteLineAsync(
			session,
			$"{label} = unavailable: {exception.Message}"
		);
	}
}

static string Format(
	TerminalColor color
) {
	return $"rgb:{color.Red:x4}/{color.Green:x4}/{color.Blue:x4}";
}

static ValueTask WriteLineAsync(
	TerminalSession session,
	string text
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( text );

	return session.WriteTextAsync(
		string.Concat(
			text,
			"\r\n"
		)
	);
}
