/*
	Icod.Terminal.Sample
	Basic sample application demonstrating Icod.Terminal session usage.
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

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync( "Icod.Terminal sample ready.\r\n" );
await session.WriteTextAsync(
	string.Concat(
		"Identity source: ",
		session.Identity.Source.ToString(),
		"\r\n"
	)
);
await session.WriteTextAsync(
	FormatObservation(
		"Input",
		session.InputObservation
	)
);
await session.WriteTextAsync(
	FormatObservation(
		"Output",
		session.OutputObservation
	)
);
await session.WriteTextAsync(
	"Press a key, or wait one second for the timed read to complete.\r\n"
);

TerminalEvent terminalEvent = await session.ReadEventAsync(
	TimeSpan.FromSeconds( 1 )
);

await session.WriteTextAsync(
	string.Concat(
		"Event: ",
		terminalEvent.Kind.ToString(),
		TerminalEventKind.Input == terminalEvent.Kind
			? string.Concat(
				" / ",
				terminalEvent.Input?.Kind.ToString() ?? "missing input payload"
			)
			: string.Empty,
		"\r\n"
	)
);

return 0;

static string FormatObservation(
	string label,
	TerminalEndpointObservation observation
) {
	ArgumentException.ThrowIfNullOrWhiteSpace( label );
	ArgumentNullException.ThrowIfNull( observation );

	return string.Concat(
		label,
		" endpoint: terminal=",
		observation.IsTerminal.ToString(),
		", platform=",
		observation.Platform?.ToString() ?? "none",
		", pathname=",
		observation.Pathname ?? "unavailable",
		", capabilities=",
		observation.Capabilities.ToString(),
		"\r\n"
	);
}
