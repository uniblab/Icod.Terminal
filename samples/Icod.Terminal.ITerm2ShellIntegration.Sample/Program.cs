/*
	Icod.Terminal.ITerm2ShellIntegration.Sample
	Sample application demonstrating typed iTerm2 OSC 1337 shell-integration metadata.
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
using System.Globalization;
using Icod.Terminal;

if ( 7 > args.Length ) {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.ITerm2ShellIntegration.Sample <current-directory> <user-name> <host-name> <shell-name> <integration-version> <variable-name> <variable-value> [--clear-captured-output]"
	);
	return 2;
}

if ( !int.TryParse(
	args[ 4 ],
	NumberStyles.None,
	CultureInfo.InvariantCulture,
	out int integrationVersion
) || 0 > integrationVersion ) {
	Console.Error.WriteLine(
		"The integration version must be a non-negative decimal integer."
	);
	return 2;
}

bool clearCapturedOutput = 8 <= args.Length
	&& string.Equals(
		args[ 7 ],
		"--clear-captured-output",
		StringComparison.Ordinal
	);
if ( 8 <= args.Length && !clearCapturedOutput ) {
	Console.Error.WriteLine(
		"The only optional argument is --clear-captured-output."
	);
	return 2;
}

Console.WriteLine(
	"Icod.Terminal 1.3 typed iTerm2 OSC 1337 shell-integration sample."
);
Console.WriteLine(
	"All metadata is supplied explicitly on the command line; the sample does not inspect process, shell, user, host, or environment state."
);
Console.WriteLine(
	"Warning: current directory, remote host, shell identity, and user variables may be retained or exposed by the terminal."
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.SetITerm2MarkAsync();
await session.PublishITerm2RemoteHostAsync(
	args[ 1 ],
	args[ 2 ]
);
await session.PublishITerm2CurrentDirectoryAsync( args[ 0 ] );
await session.SetITerm2UserVariableAsync(
	args[ 5 ],
	args[ 6 ]
);
await session.PublishITerm2ShellIntegrationVersionAsync(
	integrationVersion,
	args[ 3 ]
);

if ( clearCapturedOutput ) {
	await session.ClearITerm2CapturedOutputAsync();
}

Console.WriteLine(
	"OSC 1337 metadata emitted. Successful completion does not prove iTerm2 recognized or retained it."
);
return 0;
