/*
	Icod.Terminal.PackageCompatibilitySmoke
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
using Icod.Terminal;
using Icod.Terminal.Compatibility.Sample;

await VerifyAsync( [ "--help" ], output =>
	output == CompatibilityCommandLine.HelpText + Environment.NewLine );
await VerifyAsync( [ "--list-scenarios" ], output =>
	24 == output.Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries ).Length );
await VerifyAsync( [ "--describe", "clipboard.osc52" ], output =>
	output.Contains( "clipboard.osc52/v1", StringComparison.Ordinal ) );

string fixtureDirectory = Path.Combine( AppContext.BaseDirectory, "fixtures" );
string matrixPath = Path.Combine( Path.GetTempPath(), $"icod-compatibility-{Guid.NewGuid():N}.md" );
try {
	await VerifyAsync(
		[ "--render-matrix", fixtureDirectory, matrixPath ],
		static _ => true
	);
	string expected = await File.ReadAllTextAsync(
		Path.Combine( fixtureDirectory, "expected-matrix.md" )
	);
	string actual = await File.ReadAllTextAsync( matrixPath );
	if ( !string.Equals( expected, actual, StringComparison.Ordinal ) ) {
		throw new InvalidOperationException( "Rendered compatibility matrix did not match the fixture." );
	}
} finally {
	if ( File.Exists( matrixPath ) ) {
		File.Delete( matrixPath );
	}
}

string versionedMatrixPath = Path.Combine(
	Path.GetTempPath(),
	$"icod-versioned-compatibility-{Guid.NewGuid():N}.md"
);
try {
	await VerifyAsync(
		[
			"--render-matrix",
			Path.Combine( AppContext.BaseDirectory, "versioned-evidence" ),
			versionedMatrixPath,
			"--release-version",
			Environment.GetEnvironmentVariable( "ICOD_COMPATIBILITY_MATRIX_VERSION" ) ?? "1.26.0"
		],
		static _ => true
	);
	string expected = await File.ReadAllTextAsync(
		Path.Combine( fixtureDirectory, "versioned-matrix.md" )
	);
	string actual = await File.ReadAllTextAsync( versionedMatrixPath );
	if ( !string.Equals( expected, actual, StringComparison.Ordinal ) ) {
		throw new InvalidOperationException( "The checked-in versioned matrix is stale." );
	}
} finally {
	if ( File.Exists( versionedMatrixPath ) ) {
		File.Delete( versionedMatrixPath );
	}
}

Console.WriteLine( "Icod.Terminal compatibility package smoke passed." );

static async ValueTask VerifyAsync(
	string[] arguments,
	Func<string, bool> verifyOutput
) {
	using StringWriter output = new();
	using StringWriter error = new();
	int exitCode = await CompatibilityCommandLine.RunAsync(
		arguments,
		output,
		error,
		static _ => ValueTask.FromException<TerminalSession>(
			new InvalidOperationException( "A headless command opened a terminal session." )
		)
	);
	if ( 0 != exitCode || 0 != error.GetStringBuilder().Length || !verifyOutput( output.ToString() ) ) {
		throw new InvalidOperationException(
			$"Headless compatibility command failed: {string.Join( ' ', arguments )}. {error}"
		);
	}
}
