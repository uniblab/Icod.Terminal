/*
	Icod.Terminal.PackageOsc1337Smoke
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

Func<TerminalSession, CancellationToken, ValueTask> setMark = BindSetMark;
Func<TerminalSession, string, CancellationToken, ValueTask> publishCurrentDirectory =
	BindPublishCurrentDirectory;
Func<TerminalSession, string, string, CancellationToken, ValueTask> publishRemoteHost =
	BindPublishRemoteHost;
Func<TerminalSession, string, string, CancellationToken, ValueTask> setUserVariable =
	BindSetUserVariable;
Func<TerminalSession, int, string, CancellationToken, ValueTask> publishVersion =
	BindPublishShellIntegrationVersion;
Func<TerminalSession, CancellationToken, ValueTask> clearCapturedOutput =
	BindClearCapturedOutput;
_ = setMark;
_ = publishCurrentDirectory;
_ = publishRemoteHost;
_ = setUserVariable;
_ = publishVersion;
_ = clearCapturedOutput;

string[] requiredNames = [
	nameof( TerminalSession.SetITerm2MarkAsync ),
	nameof( TerminalSession.PublishITerm2CurrentDirectoryAsync ),
	nameof( TerminalSession.PublishITerm2RemoteHostAsync ),
	nameof( TerminalSession.SetITerm2UserVariableAsync ),
	nameof( TerminalSession.PublishITerm2ShellIntegrationVersionAsync ),
	nameof( TerminalSession.ClearITerm2CapturedOutputAsync )
];
MethodInfo[] publicMethods = typeof( TerminalSession ).GetMethods(
	BindingFlags.Public | BindingFlags.Instance
);
foreach ( string requiredName in requiredNames ) {
	if ( !publicMethods.Any(
		method => string.Equals(
			method.Name,
			requiredName,
			StringComparison.Ordinal
		)
	) ) {
		throw new InvalidOperationException(
			$"The shipped package is missing TerminalSession.{requiredName}."
		);
	}
}

string[] forbiddenNames = [
	"WriteOsc1337Async",
	"WriteRawOsc1337Async",
	"SendOsc1337Async",
	"SendITerm2CommandAsync",
	"WriteITerm2ControlAsync"
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
			$"The shipped public TerminalSession surface exposes raw/generic OSC 1337 API '{forbiddenName}'."
		);
	}
}

Console.WriteLine(
	"Icod.Terminal 1.3 OSC 1337 package API and exclusion smoke passed."
);

static ValueTask BindSetMark(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.SetITerm2MarkAsync( cancellationToken );
}

static ValueTask BindPublishCurrentDirectory(
	TerminalSession session,
	string currentDirectory,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( currentDirectory );
	return session.PublishITerm2CurrentDirectoryAsync(
		currentDirectory,
		cancellationToken
	);
}

static ValueTask BindPublishRemoteHost(
	TerminalSession session,
	string userName,
	string hostName,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( userName );
	ArgumentNullException.ThrowIfNull( hostName );
	return session.PublishITerm2RemoteHostAsync(
		userName,
		hostName,
		cancellationToken
	);
}

static ValueTask BindSetUserVariable(
	TerminalSession session,
	string name,
	string value,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( name );
	ArgumentNullException.ThrowIfNull( value );
	return session.SetITerm2UserVariableAsync(
		name,
		value,
		cancellationToken
	);
}

static ValueTask BindPublishShellIntegrationVersion(
	TerminalSession session,
	int version,
	string shellName,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( shellName );
	return session.PublishITerm2ShellIntegrationVersionAsync(
		version,
		shellName,
		cancellationToken
	);
}

static ValueTask BindClearCapturedOutput(
	TerminalSession session,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	return session.ClearITerm2CapturedOutputAsync( cancellationToken );
}
