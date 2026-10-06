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

using System.Diagnostics;
using Xunit;

/// <summary>Freezes the safe operator-launcher contracts.</summary>
public sealed class TerminalCompatibilityLauncherScriptTests {
	[Fact]
	public void TextCheckoutStaysCleanAcrossWindowsAndWsl() {
		string repositoryRoot = TerminalCompatibilityEvidenceTests.FindRepositoryRoot();
		string checkout = Path.Combine( Path.GetTempPath(), $"icod-shell-checkout-{Guid.NewGuid():N}" );
		string[] paths = [
			"build.sh",
			"samples/Icod.Terminal.Compatibility.Sample/run-kitty-wsl.sh",
			"samples/Icod.Terminal.Compatibility.Sample/run-environment-kitty-wsl.sh"
		];
		string[] textPaths = [ "README.md", "sample.cs", "sample.csproj", "sample.sln", "sample.yaml" ];
		Directory.CreateDirectory( checkout );
		try {
			foreach ( string relativePath in paths ) {
				string destination = Path.Combine( checkout, relativePath );
				Directory.CreateDirectory( Path.GetDirectoryName( destination )! );
				File.Copy( Path.Combine( repositoryRoot, relativePath ), destination );
			}
			foreach ( string fileName in textPaths ) {
				File.WriteAllText( Path.Combine( checkout, fileName ), "first line\nsecond line\n" );
			}
			string evidencePath = "docs/compatibility/evidence/1.27.0/windows-terminal-appearance-query.json";
			string evidenceDestination = Path.Combine( checkout, evidencePath );
			Directory.CreateDirectory( Path.GetDirectoryName( evidenceDestination )! );
			byte[] originalEvidence = File.ReadAllBytes( Path.Combine( repositoryRoot, evidencePath ) );
			File.WriteAllBytes( evidenceDestination, originalEvidence );
			string attributes = Path.Combine( repositoryRoot, ".gitattributes" );
			if ( File.Exists( attributes ) ) {
				File.Copy( attributes, Path.Combine( checkout, ".gitattributes" ) );
			}
			RunGit( checkout, "init", "--quiet" );
			RunGit( checkout, "-c", "core.autocrlf=false", "add", "." );
			RunGit( checkout, "-c", "user.name=Icod.Terminal tests", "-c", "user.email=tests@example.invalid",
				"commit", "--quiet", "--message", "Shell checkout fixture" );
			foreach ( string relativePath in paths ) {
				File.Delete( Path.Combine( checkout, relativePath ) );
			}
			foreach ( string relativePath in textPaths ) {
				File.Delete( Path.Combine( checkout, relativePath ) );
			}
			File.Delete( evidenceDestination );
			RunGit( checkout, "-c", "core.autocrlf=true", "checkout-index", "--all", "--force" );
			foreach ( string relativePath in paths ) {
				Assert.DoesNotContain( '\r', File.ReadAllText( Path.Combine( checkout, relativePath ) ) );
			}
			foreach ( string relativePath in textPaths ) {
				Assert.DoesNotContain( '\r', File.ReadAllText( Path.Combine( checkout, relativePath ) ) );
			}
			Assert.Empty( RunGit( checkout, "-c", "core.autocrlf=true", "status", "--porcelain" ) );
			Assert.Empty( RunGit( checkout, "-c", "core.autocrlf=false", "status", "--porcelain" ) );
			Assert.Equal(
				RunGit( checkout, "rev-parse", "HEAD:README.md" ),
				RunGit( checkout, "-c", "core.autocrlf=false", "hash-object", "--path=README.md", "README.md" )
			);
			Assert.Equal( originalEvidence, File.ReadAllBytes( evidenceDestination ) );
		} finally {
			foreach ( string file in Directory.EnumerateFiles( checkout, "*", SearchOption.AllDirectories ) ) {
				File.SetAttributes( file, FileAttributes.Normal );
			}
			Directory.Delete( checkout, recursive: true );
		}
	}

	private static string RunGit(
		string workingDirectory,
		params string[] arguments
	) {
		var start = new ProcessStartInfo( "git" ) {
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		start.Environment[ "GIT_CONFIG_NOSYSTEM" ] = "1";
		start.Environment[ "GIT_CONFIG_GLOBAL" ] = Path.Combine( workingDirectory, "no-global-config" );
		start.ArgumentList.Add( "-c" );
		start.ArgumentList.Add( $"core.attributesFile={Path.Combine( workingDirectory, "no-global-attributes" )}" );
		foreach ( string argument in arguments ) {
			start.ArgumentList.Add( argument );
		}
		using Process process = Process.Start( start )!;
		string output = process.StandardOutput.ReadToEnd();
		string error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		Assert.True( 0 == process.ExitCode, $"git failed: {output}{error}" );
		return output;
	}

	[Fact]
	public void WindowsLauncherRunsTheSafeWindowsTerminalScenarios() {
		string script = ReadLauncher( "Run-WindowsTerminal.cmd" );

		Assert.Contains( "--terminal windows-terminal", script, StringComparison.Ordinal );
		Assert.Contains( "--run identity.session", script, StringComparison.Ordinal );
		Assert.Contains( "--run query.dimensions", script, StringComparison.Ordinal );
		Assert.Contains( "--render-matrix", script, StringComparison.Ordinal );
		Assert.Contains( "git rev-parse HEAD", script, StringComparison.Ordinal );
		Assert.Contains( "Get-AppxPackage -Name Microsoft.WindowsTerminal", script, StringComparison.Ordinal );
		Assert.DoesNotContain( "Sort-Object", script, StringComparison.Ordinal );
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
		Assert.Contains( "KITTY_WINDOW_ID", script, StringComparison.Ordinal );
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
