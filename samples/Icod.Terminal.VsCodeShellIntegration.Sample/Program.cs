/*
	Icod.Terminal.VsCodeShellIntegration.Sample
	Sample application demonstrating Icod.Terminal VsCodeShellIntegration features.
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

if ( 2 > args.Length || 3 < args.Length ) {
	Console.Error.WriteLine(
		"Usage: Icod.Terminal.VsCodeShellIntegration.Sample <current-directory> <command-line> [nonce]"
	);
	return 2;
}

string currentDirectory = args[ 0 ];
string commandLine = args[ 1 ];
string? nonce = 3 == args.Length
	? args[ 2 ]
	: null;

Console.WriteLine(
	"Icod.Terminal typed VS Code OSC 633 shell-integration sample."
);
Console.WriteLine(
	"The current directory, command line, and optional nonce are supplied explicitly; the sample does not inspect process or environment state."
);
Console.WriteLine(
	"Warning: shell-integration metadata can disclose paths, commands, arguments, and other sensitive text to the terminal."
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.PublishVsCodeRichCommandDetectionAsync( true );
await session.PublishVsCodeCurrentDirectoryAsync(
	currentDirectory,
	nonce
);
await session.BeginVsCodePromptAsync();
await session.WriteTextAsync( "sample> " );
await session.BeginVsCodeCommandInputAsync();
await session.PublishVsCodeCommandLineAsync(
	commandLine,
	nonce
);
await session.WriteTextAsync( string.Concat( commandLine, "\r\n" ) );
await session.BeginVsCodeCommandOutputAsync();
await session.WriteTextAsync(
	"Sample command output would be written here.\r\n"
);
await session.FinishVsCodeCommandAsync( 0 );

Console.WriteLine(
	"OSC 633 metadata emitted. Successful completion proves only that the frames were written, not that the terminal recognized them."
);
return 0;
