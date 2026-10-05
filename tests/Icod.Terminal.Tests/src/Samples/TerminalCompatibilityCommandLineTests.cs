/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
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
namespace Icod.Terminal.Tests.Samples;

using Icod.Terminal.Compatibility.Sample;
using Xunit;

/// <summary>Freezes the compatibility sample command contract.</summary>
public sealed class TerminalCompatibilityCommandLineTests {
	[Fact]
	public async Task HelpIsHeadlessAndExact() {
		Invocation invocation = new();

		int exitCode = await invocation.RunAsync( "--help" );

		Assert.Equal( 0, exitCode );
		Assert.Equal( CompatibilityCommandLine.HelpText + Environment.NewLine, invocation.Output.ToString() );
		Assert.Equal( 0, invocation.SessionFactoryCalls );
	}

	[Fact]
	public async Task ScenarioListingIsStableAndHeadless() {
		Invocation invocation = new();

		int exitCode = await invocation.RunAsync( "--list-scenarios" );

		Assert.Equal( 0, exitCode );
		string[] lines = invocation.Output.ToString().Split(
			Environment.NewLine,
			StringSplitOptions.RemoveEmptyEntries
		);
		Assert.Equal( 21, lines.Length );
		Assert.StartsWith( "identity.session/v1", lines[ 0 ], StringComparison.Ordinal );
		Assert.StartsWith( "presentation.pointer-colors/v1", lines[ ^1 ], StringComparison.Ordinal );
		Assert.Equal( 0, invocation.SessionFactoryCalls );
	}

	[Fact]
	public async Task DescribeIsHeadless() {
		Invocation invocation = new();

		int exitCode = await invocation.RunAsync( "--describe", "query.dimensions" );

		Assert.Equal( 0, exitCode );
		Assert.Contains( "query.dimensions/v1", invocation.Output.ToString(), StringComparison.Ordinal );
		Assert.Equal( 0, invocation.SessionFactoryCalls );
	}

	[Theory]
	[InlineData( "--unknown" )]
	[InlineData( "--describe" )]
	[InlineData( "--run", "identity.session" )]
	[InlineData( "--run", "identity.session", "--terminal", "kitty", "--terminal", "kitty" )]
	[InlineData( "--run", "identity.session", "--overwrite" )]
	[InlineData( "--run", "identity.session", "--transport", "WSL", "--transport-version", "2" )]
	public async Task InvalidArgumentsDoNotOpenSession(
		params string[] args
	) {
		Invocation invocation = new();

		int exitCode = await invocation.RunAsync( args );

		Assert.Equal( 2, exitCode );
		Assert.NotEmpty( invocation.Error.ToString() );
		Assert.Equal( 0, invocation.SessionFactoryCalls );
	}

	[Fact]
	public async Task ValidLiveArgumentsOpenOneSession() {
		Invocation invocation = new();

		int exitCode = await invocation.RunAsync(
			"--run", "identity.session",
			"--terminal", "kitty",
			"--terminal-version", "0.32.2",
			"--os", "Ubuntu",
			"--os-version", "24.04",
			"--source-commit", "0123456789abcdef0123456789abcdef01234567"
		);

		Assert.Equal( 1, exitCode );
		Assert.Equal( 1, invocation.SessionFactoryCalls );
		Assert.Contains( "test session factory", invocation.Error.ToString(), StringComparison.Ordinal );
	}

	[Fact]
	public async Task MatrixRenderingIsHeadless() {
		Invocation invocation = new();
		string root = TerminalCompatibilityEvidenceTests.FindRepositoryRoot();
		string evidenceDirectory = Path.Combine(
			root,
			"samples",
			"Icod.Terminal.Compatibility.Sample",
			"fixtures"
		);
		string output = Path.Combine( Path.GetTempPath(), $"icod-matrix-{Guid.NewGuid():N}.md" );
		try {
			int exitCode = await invocation.RunAsync(
				"--render-matrix", evidenceDirectory, output
			);

			Assert.Equal( 0, exitCode );
			Assert.Contains(
				"Icod.Terminal 1.26.0 compatibility matrix",
				File.ReadAllText( output ),
				StringComparison.Ordinal
			);
			Assert.Equal( 0, invocation.SessionFactoryCalls );
		} finally {
			if ( File.Exists( output ) ) {
				File.Delete( output );
			}
		}
	}

	[Fact]
	public async Task EmptyEvidenceDirectoryRendersExplicitNotRunLanes() {
		Invocation invocation = new();
		string directory = Path.Combine( Path.GetTempPath(), $"icod-empty-evidence-{Guid.NewGuid():N}" );
		string output = Path.Combine( Path.GetTempPath(), $"icod-empty-matrix-{Guid.NewGuid():N}.md" );
		Directory.CreateDirectory( directory );
		try {
			int exitCode = await invocation.RunAsync(
				"--render-matrix", directory, output
			);

			Assert.Equal( 0, exitCode );
			Assert.Contains(
				"| Windows Terminal | `NotRun` |",
				File.ReadAllText( output ),
				StringComparison.Ordinal
			);
			Assert.Equal( 0, invocation.SessionFactoryCalls );
		} finally {
			if ( File.Exists( output ) ) {
				File.Delete( output );
			}
			Directory.Delete( directory );
		}
	}

	private sealed class Invocation {
		internal StringWriter Output {
			get;
		} = new();

		internal StringWriter Error {
			get;
		} = new();

		internal int SessionFactoryCalls {
			get;
			private set;
		}

		internal ValueTask<int> RunAsync(
			params string[] args
		) => CompatibilityCommandLine.RunAsync(
			args,
			this.Output,
			this.Error,
			cancellationToken => {
				cancellationToken.ThrowIfCancellationRequested();
				++this.SessionFactoryCalls;
				throw new InvalidOperationException( "test session factory" );
			}
		);
	}
}
