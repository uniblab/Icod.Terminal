/*
	Icod.Terminal.PackageOsc633Smoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Reflection;
using Icod.Terminal;

Func<TerminalSession, CancellationToken, ValueTask> beginPrompt = BindBeginPrompt;
Func<TerminalSession, CancellationToken, ValueTask> beginInput = BindBeginInput;
Func<TerminalSession, CancellationToken, ValueTask> beginOutput = BindBeginOutput;
Func<TerminalSession, int, CancellationToken, ValueTask> finishCommand = BindFinishCommand;
Func<TerminalSession, CancellationToken, ValueTask> abortCommand = BindAbortCommand;
Func<TerminalSession, string, string?, CancellationToken, ValueTask> publishCommandLine = BindPublishCommandLine;
Func<TerminalSession, string, string?, CancellationToken, ValueTask> publishCwd = BindPublishCwd;
Func<TerminalSession, bool, CancellationToken, ValueTask> publishIsWindows = BindPublishIsWindows;
Func<TerminalSession, bool, CancellationToken, ValueTask> publishRichDetection = BindPublishRichDetection;

_ = beginPrompt;
_ = beginInput;
_ = beginOutput;
_ = finishCommand;
_ = abortCommand;
_ = publishCommandLine;
_ = publishCwd;
_ = publishIsWindows;
_ = publishRichDetection;

MethodInfo[] publicMethods = typeof( TerminalSession ).GetMethods(
	BindingFlags.Public | BindingFlags.Instance
);
string[] requiredNames = [
	nameof( TerminalSession.BeginVsCodePromptAsync ),
	nameof( TerminalSession.BeginVsCodeCommandInputAsync ),
	nameof( TerminalSession.BeginVsCodeCommandOutputAsync ),
	nameof( TerminalSession.FinishVsCodeCommandAsync ),
	nameof( TerminalSession.AbortVsCodeCommandAsync ),
	nameof( TerminalSession.PublishVsCodeCommandLineAsync ),
	nameof( TerminalSession.PublishVsCodeCurrentDirectoryAsync ),
	nameof( TerminalSession.PublishVsCodeIsWindowsAsync ),
	nameof( TerminalSession.PublishVsCodeRichCommandDetectionAsync )
];
foreach ( string requiredName in requiredNames ) {
	if ( !publicMethods.Any(
		method => string.Equals(
			method.Name,
			requiredName,
			StringComparison.Ordinal
		)
	) ) {
		throw new InvalidOperationException(
			$"The shipped package is missing required TerminalSession method '{requiredName}'."
		);
	}
}

string[] forbiddenNames = [
	"WriteOsc633Async",
	"WriteRawOsc633Async",
	"SendOsc633Async",
	"PublishVsCodeEnvironmentJsonAsync",
	"BeginVsCodeContinuationAsync"
];
foreach ( string forbiddenName in forbiddenNames ) {
	if ( publicMethods.Any(
		method => string.Equals(
			method.Name,
			forbiddenName,
			StringComparison.Ordinal
		)
	) ) {
		throw new InvalidOperationException(
			$"The shipped public TerminalSession surface exposes forbidden/unfinalized OSC 633 API '{forbiddenName}'."
		);
	}
}

Console.WriteLine(
	"Icod.Terminal 1.1 OSC 633 package API and exclusion smoke passed."
);

static ValueTask BindBeginPrompt(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginVsCodePromptAsync( cancellationToken );
}

static ValueTask BindBeginInput(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginVsCodeCommandInputAsync( cancellationToken );
}

static ValueTask BindBeginOutput(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.BeginVsCodeCommandOutputAsync( cancellationToken );
}

static ValueTask BindFinishCommand(
	TerminalSession session,
	int exitCode,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.FinishVsCodeCommandAsync(
		exitCode,
		cancellationToken
	);
}

static ValueTask BindAbortCommand(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.AbortVsCodeCommandAsync( cancellationToken );
}

static ValueTask BindPublishCommandLine(
	TerminalSession session,
	string commandLine,
	string? nonce,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( commandLine );
	return session.PublishVsCodeCommandLineAsync(
		commandLine,
		nonce,
		cancellationToken
	);
}

static ValueTask BindPublishCwd(
	TerminalSession session,
	string currentDirectory,
	string? nonce,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( currentDirectory );
	return session.PublishVsCodeCurrentDirectoryAsync(
		currentDirectory,
		nonce,
		cancellationToken
	);
}

static ValueTask BindPublishIsWindows(
	TerminalSession session,
	bool isWindows,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.PublishVsCodeIsWindowsAsync(
		isWindows,
		cancellationToken
	);
}

static ValueTask BindPublishRichDetection(
	TerminalSession session,
	bool hasRichCommandDetection,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.PublishVsCodeRichCommandDetectionAsync(
		hasRichCommandDetection,
		cancellationToken
	);
}
