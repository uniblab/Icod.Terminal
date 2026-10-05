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

using Xunit;

/// <summary>Freezes the safe operator-launcher contracts.</summary>
public sealed class TerminalCompatibilityLauncherScriptTests {
	[Fact]
	public void WindowsLauncherRunsTheSafeWindowsTerminalScenarios() {
		string script = ReadLauncher( "Run-WindowsTerminal.cmd" );

		Assert.Contains( "--terminal windows-terminal", script, StringComparison.Ordinal );
		Assert.Contains( "--run identity.session", script, StringComparison.Ordinal );
		Assert.Contains( "--run query.dimensions", script, StringComparison.Ordinal );
		Assert.Contains( "--render-matrix", script, StringComparison.Ordinal );
		Assert.Contains( "git rev-parse HEAD", script, StringComparison.Ordinal );
		Assert.Contains( "Get-AppxPackage", script, StringComparison.Ordinal );
		Assert.Contains( "TERMINAL_VERSION=unknown", script, StringComparison.Ordinal );
		Assert.DoesNotContain( "fc /b", script, StringComparison.Ordinal );
		Assert.DoesNotContain( "--overwrite", script, StringComparison.Ordinal );
	}

	[Fact]
	public void KittyLauncherRecordsTheWslTransportAndSafeScenarios() {
		string script = ReadLauncher( "run-kitty-wsl.sh" );

		Assert.Contains( "--terminal kitty", script, StringComparison.Ordinal );
		Assert.Contains( "--transport WSL", script, StringComparison.Ordinal );
		Assert.Contains( "--run identity.session", script, StringComparison.Ordinal );
		Assert.Contains( "--run query.dimensions", script, StringComparison.Ordinal );
		Assert.Contains( "--render-matrix", script, StringComparison.Ordinal );
		Assert.Contains( "mktemp -d", script, StringComparison.Ordinal );
		Assert.DoesNotContain( "cmp -s", script, StringComparison.Ordinal );
		Assert.DoesNotContain( "--overwrite", script, StringComparison.Ordinal );
	}

	private static string ReadLauncher(
		string fileName
	) {
		return File.ReadAllText(
			Path.Combine(
				TerminalCompatibilityEvidenceTests.FindRepositoryRoot(),
				"samples",
				"Icod.Terminal.Compatibility.Sample",
				fileName
			)
		);
	}
}
