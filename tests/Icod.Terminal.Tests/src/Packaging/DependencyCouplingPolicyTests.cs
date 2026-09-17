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
namespace Icod.Terminal.Tests.Packaging;

using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Guards the repository policy that active package verification validates dependency
/// shape without duplicating exact dependency-version requirements.
/// </summary>
public sealed class DependencyCouplingPolicyTests {
	[Fact]
	public void StableDcursesPackageAcceptanceUsesPublishedVersion160() {
		string projectPath = Path.Combine(
			FindRepositoryRoot(),
			"tools",
			"dcurses-package-acceptance",
			"Icod.Terminal.DCursesPackageAcceptance.csproj"
		);
		XDocument project = XDocument.Load( projectPath );
		XElement[] references = project
			.Descendants( "PackageReference" )
			.Where(
				reference => "Icod.DCurses" == (string?)reference.Attribute( "Include" )
			)
			.ToArray();

		XElement reference = Assert.Single( references );
		Assert.Equal( "1.6.0", (string?)reference.Attribute( "Version" ) );
	}

	[Theory]
	[InlineData( "Debug" )]
	[InlineData( "Staging" )]
	[InlineData( "Release" )]
	public void FutureDcursesScreenAcceptanceValidatesEveryCompileAndReferenceInput(
		string configuration
	) {
		string root = FindRepositoryRoot();
		string candidateVersion = EvaluateProjectProperty(
			Path.Combine( root, "Icod.Terminal.csproj" ),
			"PackageVersion",
			configuration
		);
		string acceptanceRoot = Path.Combine(
			root,
			"tools",
			"dcurses-screen-contracts-acceptance"
		);
		string projectPath = Path.Combine(
			acceptanceRoot,
			"Icod.Terminal.DCursesScreenContractsAcceptance.csproj"
		);
		string sourceRoot = Path.Combine( acceptanceRoot, "Source" );

		Assert.True(
			File.Exists( projectPath ),
			$"The future-DCurses screen-contract acceptance project is missing: {projectPath}"
		);
		Assert.True(
			Directory.Exists( sourceRoot ),
			$"The controlled future-DCurses source root is missing: {sourceRoot}"
		);

		XDocument project = XDocument.Load( projectPath );
		Assert.Equal(
			"false",
			project.Descendants( "EnableDefaultCompileItems" ).Single().Value
		);
		XElement compile = Assert.Single( project.Descendants( "Compile" ) );
		Assert.Equal( "Source/**/*.cs", (string?)compile.Attribute( "Include" ) );
		Assert.Null( compile.Attribute( "Link" ) );
		Assert.Null( compile.Attribute( "LinkBase" ) );
		Assert.Null( compile.Attribute( "Update" ) );
		Assert.Null( compile.Attribute( "Remove" ) );
		Assert.Equal(
			"$(IcodTerminalPackageVersion)",
			(string?)Assert.Single(
				project.Descendants( "PackageReference" )
			).Attribute( "Version" )
		);

		string[] ownedSources = Directory.EnumerateFiles(
			sourceRoot,
			"*.cs",
			SearchOption.AllDirectories
		)
			.Select( Path.GetFullPath )
			.Order( StringComparer.OrdinalIgnoreCase )
			.ToArray();
		Assert.NotEmpty( ownedSources );
		foreach ( string acceptanceSource in ownedSources ) {
			string source = File.ReadAllText( acceptanceSource );
			Assert.DoesNotContain(
				"Icod.TermInfo",
				source,
				StringComparison.Ordinal
			);
		}

		foreach ( string targetFramework in new[] { "net8.0", "net9.0", "net10.0" } ) {
			using JsonDocument evaluation = EvaluateProjectItems(
				projectPath,
				targetFramework,
				configuration,
				candidateVersion
			);
			JsonElement properties = evaluation.RootElement.GetProperty( "Properties" );
			Assert.Equal(
				configuration,
				properties.GetProperty( "Configuration" ).GetString()
			);
			Assert.Equal(
				candidateVersion,
				properties.GetProperty( "IcodTerminalPackageVersion" ).GetString()
			);
			JsonElement items = evaluation.RootElement.GetProperty( "Items" );
			JsonElement.ArrayEnumerator compileItems = items
				.GetProperty( "Compile" )
				.EnumerateArray();
			string[] compileInputs = compileItems
				.Select(
					item => Path.GetFullPath(
						item.GetProperty( "FullPath" ).GetString()
							?? string.Empty
					)
				)
				.Order( StringComparer.OrdinalIgnoreCase )
				.ToArray();
			Assert.Equal( ownedSources, compileInputs );
			foreach ( JsonElement compileInput in items
				.GetProperty( "Compile" )
				.EnumerateArray() ) {
				Assert.False(
					compileInput.TryGetProperty( "Link", out JsonElement link )
						&& !string.IsNullOrWhiteSpace( link.GetString() )
				);
			}

			string[] packageReferences = GetEvaluatedIdentities(
				items,
				"PackageReference"
			);
			Assert.Equal( [ "Icod.Terminal" ], packageReferences );
			Assert.Empty( GetEvaluatedIdentities( items, "ProjectReference" ) );
			Assert.Empty( GetEvaluatedIdentities( items, "Reference" ) );
		}
	}

	[Fact]
	public void BoundaryEvaluationActivatesConfigurationAndVersionConditionalInputs() {
		string fixtureRoot = Path.Combine(
			Path.GetTempPath(),
			$"Icod.Terminal-boundary-policy-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory( fixtureRoot );
		try {
			string projectPath = Path.Combine( fixtureRoot, "Conditional.csproj" );
			File.WriteAllText(
				projectPath,
				"""
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <TargetFramework>net10.0</TargetFramework>
				    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
				    <IcodTerminalPackageVersion Condition="'$(IcodTerminalPackageVersion)' == ''">not-the-candidate</IcodTerminalPackageVersion>
				  </PropertyGroup>
				  <ItemGroup Condition="'$(Configuration)' == 'Staging' and '$(IcodTerminalPackageVersion)' == '9.8.7-policy'">
				    <Compile Include="Conditional.cs" />
				    <PackageReference Include="Icod.TermInfo" Version="1.14.0" />
				    <ProjectReference Include="ConditionalReference.csproj" />
				    <Reference Include="Conditional.Assembly" />
				  </ItemGroup>
				</Project>
				"""
			);
			File.WriteAllText(
				Path.Combine( fixtureRoot, "Conditional.cs" ),
				"internal static class Conditional;"
			);

			using JsonDocument evaluation = EvaluateProjectItems(
				projectPath,
				"net10.0",
				"Staging",
				"9.8.7-policy"
			);
			JsonElement items = evaluation.RootElement.GetProperty( "Items" );
			Assert.Equal( [ "Conditional.cs" ], GetEvaluatedIdentities( items, "Compile" ) );
			Assert.Equal( [ "Icod.TermInfo" ], GetEvaluatedIdentities( items, "PackageReference" ) );
			Assert.Equal( [ "ConditionalReference.csproj" ], GetEvaluatedIdentities( items, "ProjectReference" ) );
			Assert.Equal( [ "Conditional.Assembly" ], GetEvaluatedIdentities( items, "Reference" ) );
		} finally {
			Directory.Delete( fixtureRoot, recursive: true );
		}
	}

	[Fact]
	public void ReleaseShardCopiesAndRunsCompleteFutureDcursesWitness() {
		string root = FindRepositoryRoot();
		string shard = File.ReadAllText(
			Path.Combine( root, "packaging", "VerifyPackageContractShard.ps1" )
		);
		string verifier = File.ReadAllText(
			Path.Combine( root, "packaging", "VerifyDCursesPackage.ps1" )
		);

		Assert.Matches(
			"(?s)'release'\\s*\\{\\s*@\\(\\s*'VerifyReleaseLinePackage\\.ps1',\\s*'VerifyDCursesPackage\\.ps1'\\s*\\)\\s*\\}",
			shard
		);
		Assert.Contains(
			"'-getItem:Compile,PackageReference,ProjectReference,Reference'",
			verifier,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"Assert-FutureConsumerBoundary",
			verifier,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"\"-property:IcodTerminalPackageVersion=$PackageVersion\"",
			verifier,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"\"-property:Configuration=$Configuration\"",
			verifier,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"-PackageVersion $ExpectedVersion",
			verifier,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"-Configuration $Configuration",
			verifier,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"-LiteralPath ( Join-Path $futureSourceRoot 'Source' ) \n\t\t-Destination ( Join-Path $futureRoot 'Source' ) \n\t\t-Recurse",
			verifier.Replace( "\r\n", "\n", StringComparison.Ordinal )
				.Replace( "`", string.Empty, StringComparison.Ordinal ),
			StringComparison.Ordinal
		);
		Assert.Contains(
			"'run',\n\t\t\t\t'--project',\n\t\t\t\t$futureProject",
			verifier.Replace( "\r\n", "\n", StringComparison.Ordinal ),
			StringComparison.Ordinal
		);
	}

	[Fact]
	public void DcursesPackageCleanupPreservesPrimaryValidationError() {
		string verifier = File.ReadAllText(
			Path.Combine(
				FindRepositoryRoot(),
				"packaging",
				"VerifyDCursesPackage.ps1"
			)
		);

		int initializePrimaryError = verifier.IndexOf(
			"$primaryError = $null",
			StringComparison.Ordinal
		);
		int capturePrimaryError = verifier.IndexOf(
			"$primaryError = $_",
			StringComparison.Ordinal
		);
		int cleanup = verifier.IndexOf(
			"Remove-Item -LiteralPath $acceptanceRoot -Recurse -Force",
			StringComparison.Ordinal
		);
		int cleanupWarning = verifier.IndexOf(
			"Write-Warning",
			cleanup,
			StringComparison.Ordinal
		);
		int cleanupRethrow = verifier.IndexOf(
			"} else {\n\t\t\t\tthrow",
			cleanup,
			StringComparison.Ordinal
		);
		int rethrowPrimaryError = verifier.IndexOf(
			"throw $primaryError",
			cleanup,
			StringComparison.Ordinal
		);

		Assert.True( 0 <= initializePrimaryError );
		Assert.True( initializePrimaryError < capturePrimaryError );
		Assert.True( capturePrimaryError < cleanup );
		Assert.True( cleanup < cleanupWarning );
		Assert.True( cleanupWarning < cleanupRethrow );
		Assert.True( cleanupRethrow < rethrowPrimaryError );
	}

	[Fact]
	public void PackageVerifierDoesNotPinDependencyVersions() {
		string source = File.ReadAllText(
			Path.Combine(
				FindRepositoryRoot(),
				"tools",
				"package-verifier",
				"Program.cs"
			)
		);

		Assert.DoesNotContain( "TermInfoDependencyVersion", source );
		Assert.DoesNotContain( "TimingDependencyVersion", source );
		Assert.DoesNotContain( "references an unexpected {packageId} version", source );
	}

	[Fact]
	public void AuxiliaryProjectsDoNotPinTerminalRuntimeDependencies() {
		string root = FindRepositoryRoot();
		string[] projectRoots = [
			Path.Combine( root, "tests" ),
			Path.Combine( root, "samples" ),
			Path.Combine( root, "tools" )
		];

		foreach ( string projectRoot in projectRoots ) {
			foreach ( string projectPath in Directory.EnumerateFiles(
				projectRoot,
				"*.csproj",
				SearchOption.AllDirectories
			) ) {
				string source = File.ReadAllText( projectPath );
				Assert.DoesNotContain(
					"PackageReference Include=\"Icod.TermInfo\"",
					source
				);
				Assert.DoesNotContain(
					"PackageReference Include=\"Icod.Timing\"",
					source
				);
			}
		}
	}

	private static string FindRepositoryRoot() {
		DirectoryInfo? directory = new( AppContext.BaseDirectory );
		while ( directory is not null ) {
			if ( File.Exists(
				Path.Combine(
					directory.FullName,
					"Icod.Terminal.csproj"
				)
			) ) {
				return directory.FullName;
			}
			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			"Could not locate the Icod.Terminal repository root from the test output directory."
		);
	}

	private static JsonDocument EvaluateProjectItems(
		string projectPath,
		string targetFramework,
		string configuration,
		string packageVersion
	) {
		string dotnetHost = Environment.GetEnvironmentVariable( "DOTNET_HOST_PATH" )
			?? "dotnet";
		ProcessStartInfo startInfo = new( dotnetHost ) {
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add( "msbuild" );
		startInfo.ArgumentList.Add( projectPath );
		startInfo.ArgumentList.Add( "-nologo" );
		startInfo.ArgumentList.Add(
			"-getItem:Compile,PackageReference,ProjectReference,Reference"
		);
		startInfo.ArgumentList.Add(
			"-getProperty:Configuration,IcodTerminalPackageVersion"
		);
		startInfo.ArgumentList.Add(
			$"-property:TargetFramework={targetFramework}"
		);
		startInfo.ArgumentList.Add(
			$"-property:Configuration={configuration}"
		);
		startInfo.ArgumentList.Add(
			$"-property:IcodTerminalPackageVersion={packageVersion}"
		);
		startInfo.ArgumentList.Add( "-nodeReuse:false" );

		using Process process = Process.Start( startInfo )
			?? throw new InvalidOperationException( "Could not start dotnet MSBuild." );
		string output = process.StandardOutput.ReadToEnd();
		string error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		Assert.True(
			0 == process.ExitCode,
			$"MSBuild evaluation failed for {targetFramework}: {error}{Environment.NewLine}{output}"
		);
		return JsonDocument.Parse( output );
	}

	private static string EvaluateProjectProperty(
		string projectPath,
		string propertyName,
		string configuration
	) {
		string dotnetHost = Environment.GetEnvironmentVariable( "DOTNET_HOST_PATH" )
			?? "dotnet";
		ProcessStartInfo startInfo = new( dotnetHost ) {
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add( "msbuild" );
		startInfo.ArgumentList.Add( projectPath );
		startInfo.ArgumentList.Add( "-nologo" );
		startInfo.ArgumentList.Add( $"-getProperty:{propertyName}" );
		startInfo.ArgumentList.Add( $"-property:Configuration={configuration}" );
		startInfo.ArgumentList.Add( "-nodeReuse:false" );

		using Process process = Process.Start( startInfo )
			?? throw new InvalidOperationException( "Could not start dotnet MSBuild." );
		string output = process.StandardOutput.ReadToEnd();
		string error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		Assert.True(
			0 == process.ExitCode,
			$"MSBuild property evaluation failed for {configuration}: {error}{Environment.NewLine}{output}"
		);
		return output.Trim();
	}

	private static string[] GetEvaluatedIdentities(
		JsonElement items,
		string itemName
	) => items
		.GetProperty( itemName )
		.EnumerateArray()
		.Select(
			item => item.GetProperty( "Identity" ).GetString()
				?? string.Empty
		)
		.ToArray();
}
