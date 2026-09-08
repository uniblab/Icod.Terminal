/*
	Icod.Terminal.PackageSafeOsc9Smoke
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

Func<TerminalSession, string, CancellationToken, ValueTask> sendNotification =
	BindSendNotification;
Func<TerminalSession, string, CancellationToken, ValueTask> publishWindowsCwd =
	BindPublishWindowsCurrentDirectory;

_ = sendNotification;
_ = publishWindowsCwd;

MethodInfo[] publicMethods = typeof( TerminalSession ).GetMethods(
	BindingFlags.Public | BindingFlags.Instance
);
AssertMethodExists(
	publicMethods,
	nameof( TerminalSession.SendNotificationAsync )
);
AssertMethodExists(
	publicMethods,
	nameof( TerminalSession.PublishWindowsCurrentDirectoryCompatibilityAsync )
);

string[] rawOsc9Methods = publicMethods
	.Where(
		method => method.Name.Contains(
			"Osc9",
			StringComparison.OrdinalIgnoreCase
		)
	)
	.Select( method => method.Name )
	.Distinct( StringComparer.Ordinal )
	.OrderBy( name => name, StringComparer.Ordinal )
	.ToArray();
if ( 0 != rawOsc9Methods.Length ) {
	throw new InvalidOperationException(
		"The shipped public TerminalSession surface contains raw/generic OSC 9 methods: "
			+ string.Join( ", ", rawOsc9Methods )
	);
}

string[] forbiddenNames = [
	"WriteOsc9Async",
	"WriteRawOsc9Async",
	"SendOsc9Async",
	"SleepAsync",
	"ShowMessageBoxAsync",
	"WaitForKeyAsync",
	"ExecuteGuiMacroAsync",
	"RunProcessAsync",
	"ReadEnvironmentVariableAsync",
	"SetXtermEmulationAsync"
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
			$"The shipped public TerminalSession surface exposes forbidden OSC 9 API '{forbiddenName}'."
		);
	}
}

Console.WriteLine(
	"Icod.Terminal 0.16 safe OSC 9 package API and exclusion smoke passed."
);

static void AssertMethodExists(
	IEnumerable<MethodInfo> methods,
	string name
) {
	ArgumentNullException.ThrowIfNull( methods );
	ArgumentException.ThrowIfNullOrEmpty( name );
	if ( !methods.Any(
		method => string.Equals(
			method.Name,
			name,
			StringComparison.Ordinal
		)
	) ) {
		throw new InvalidOperationException(
			$"The shipped package is missing required TerminalSession method '{name}'."
		);
	}
}

static ValueTask BindSendNotification(
	TerminalSession session,
	string message,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( message );
	return session.SendNotificationAsync(
		message,
		cancellationToken
	);
}

static ValueTask BindPublishWindowsCurrentDirectory(
	TerminalSession session,
	string windowsPath,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( windowsPath );
	return session.PublishWindowsCurrentDirectoryCompatibilityAsync(
		windowsPath,
		cancellationToken
	);
}
