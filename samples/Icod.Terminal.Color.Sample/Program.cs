/*
	Icod.Terminal.Color.Sample
	Sample application demonstrating Icod.Terminal Color features.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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
		"Pass --mutate to demonstrate lifecycle-safe scoped palette and dynamic-color ownership."
	);
	return 0;
}

await WriteLineAsync(
	session,
	"Scoped mutation queries the exact baseline before changing color and replays that baseline on final release."
);
await WriteLineAsync(
	session,
	"OSC 104/112 reset APIs remain terminal-policy resets and are not used by this demonstration."
);

try {
	await using TerminalPaletteColorLease paletteLease =
		await session.AcquirePaletteColorAsync(
			1,
			TerminalColor.FromRgb8( 255, 64, 64 ),
			timeout
		);
	await using TerminalDynamicColorLease cursorLease =
		await session.AcquireDynamicColorAsync(
			TerminalDynamicColor.TextCursor,
			TerminalColor.FromRgb8( 64, 255, 64 ),
			timeout
		);

	await WriteLineAsync(
		session,
		$"Scoped palette[{paletteLease.Index}] = {Format( paletteLease.Color )}."
	);
	await WriteLineAsync(
		session,
		$"Scoped {cursorLease.Kind} = {Format( cursorLease.Color )}."
	);
	await WriteLineAsync(
		session,
		"Generate one terminal event, or wait 30 seconds. Leaving this scope restores both observed baselines exactly."
	);

	TerminalEvent terminalEvent = await session.ReadEventAsync(
		TimeSpan.FromSeconds( 30 )
	);
	if ( TerminalEventKind.Timeout == terminalEvent.Kind ) {
		await WriteLineAsync(
			session,
			"No terminal event arrived before the timeout; releasing scoped color ownership now."
		);
	} else {
		await WriteLineAsync(
			session,
			$"Observed {terminalEvent.Kind}; releasing scoped color ownership now."
		);
	}
} catch ( TimeoutException ) {
	await WriteLineAsync(
		session,
		"Scoped acquisition timed out while observing a required external baseline; no lease was retained."
	);
} catch ( FormatException exception ) {
	await WriteLineAsync(
		session,
		$"Scoped acquisition received a malformed correlated baseline: {exception.Message}"
	);
} catch ( InvalidOperationException exception ) {
	await WriteLineAsync(
		session,
		$"Scoped color ownership is unavailable: {exception.Message}"
	);
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