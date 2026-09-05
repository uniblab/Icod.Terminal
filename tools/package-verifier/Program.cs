namespace Icod.Terminal.PackageVerifier;

using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

internal static class Program {
	private const string PackageId = "Icod.Terminal";
	private const string RepositoryUrl = "https://github.com/uniblab/Icod.Terminal";
	private const string TermInfoDependencyVersion = "1.10.0";
	private const string TimingDependencyVersion = "1.0.0";

	private static readonly string[] TargetFrameworks = [
		"net8.0",
		"net9.0",
		"net10.0"
	];

	private static readonly string[] RequiredQueryDocumentationMembers = [
		"T:Icod.Terminal.TerminalDeviceStatus",
		"T:Icod.Terminal.TerminalPrimaryDeviceAttributes",
		"T:Icod.Terminal.TerminalSecondaryDeviceAttributes",
		"T:Icod.Terminal.TerminalCursorPosition",
		"T:Icod.Terminal.TerminalStatusStringKind",
		"T:Icod.Terminal.TerminalStatusStringResponse",
		"T:Icod.Terminal.TerminalCapabilityObservation",
		"M:Icod.Terminal.TerminalSession.QueryPrimaryDeviceAttributesAsync(System.TimeSpan,System.Threading.CancellationToken)",
		"M:Icod.Terminal.TerminalSession.QuerySecondaryDeviceAttributesAsync(System.TimeSpan,System.Threading.CancellationToken)",
		"M:Icod.Terminal.TerminalSession.QueryDeviceStatusAsync(System.TimeSpan,System.Threading.CancellationToken)",
		"M:Icod.Terminal.TerminalSession.QueryCursorPositionAsync(System.TimeSpan,System.Threading.CancellationToken)",
		"M:Icod.Terminal.TerminalSession.QueryStatusStringAsync(Icod.Terminal.TerminalStatusStringKind,System.TimeSpan,System.Threading.CancellationToken)",
		"M:Icod.Terminal.TerminalSession.QueryLiveCapabilityAsync(System.String,System.TimeSpan,System.Threading.CancellationToken)"
	];

	public static int Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );

		if ( 1 < args.Length ) {
			Console.Error.WriteLine(
				"Usage: dotnet run --project tools/package-verifier/"
					+ "Icod.Terminal.PackageVerifier.csproj -- [artifact-directory]"
			);
			return 2;
		}

		try {
			string root = FindRepositoryRoot();
			string artifactDirectory = 0 == args.Length
				? Path.Combine( root, "artifacts" )
				: Path.GetFullPath( args[ 0 ], root )
			;

			(string PackageVersion, string AssemblyVersion) projectMetadata =
				ReadAndValidateProjectMetadata( root );
			string packageVersion = projectMetadata.PackageVersion;
			string expectedAssemblyVersion = projectMetadata.AssemblyVersion;
			string packagePath = Path.Combine(
				artifactDirectory,
				$"{PackageId}.{packageVersion}.nupkg"
			);
			string symbolsPath = Path.Combine(
				artifactDirectory,
				$"{PackageId}.{packageVersion}.snupkg"
			);

			Require(
				File.Exists( packagePath ),
				$"Package not found: {packagePath}"
			);
			Require(
				File.Exists( symbolsPath ),
				$"Symbol package not found: {symbolsPath}"
			);

			string commit = VerifyPrimaryPackage(
				packagePath,
				packageVersion,
				expectedAssemblyVersion
			);
			VerifySymbolPackage(
				symbolsPath,
				commit
			);

			Console.WriteLine(
				"Verified package structure, multi-target assembly identity, metadata, "
					+ "dependency closure, portable symbols, and Source Link for "
					+ packageVersion
					+ "."
			);
			return 0;
		} catch ( Exception exception ) when (
			exception is IOException
				or UnauthorizedAccessException
				or InvalidDataException
				or InvalidOperationException
				or BadImageFormatException
				or XmlException
		) {
			Console.Error.WriteLine( exception.Message );
			return 1;
		}
	}

	private static string FindRepositoryRoot() {
		string[] starts = [
			Directory.GetCurrentDirectory(),
			AppContext.BaseDirectory
		];

		foreach ( string start in starts ) {
			DirectoryInfo? current = new( start );
			while ( current is not null ) {
				if (
					File.Exists(
						Path.Combine(
							current.FullName,
							"Icod.Terminal.csproj"
						)
					)
					&& Directory.Exists(
						Path.Combine(
							current.FullName,
							"src"
						)
					)
				) {
					return current.FullName;
				}

				current = current.Parent;
			}
		}

		throw new InvalidOperationException(
			"Unable to locate the Icod.Terminal repository root."
		);
	}

	private static (string PackageVersion, string AssemblyVersion) ReadAndValidateProjectMetadata(
		string root
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( root );

		XDocument properties = XDocument.Load(
			Path.Combine(
				root,
				"Directory.Build.props"
			),
			LoadOptions.None
		);
		string? versionPrefix = GetPropertyValue(
			properties,
			"VersionPrefix"
		);
		string versionSuffix = GetPropertyValue(
			properties,
			"VersionSuffix"
		) ?? string.Empty;
		string? packageVersionExpression = GetPropertyValue(
			properties,
			"PackageVersion"
		);
		string? assemblyVersionExpression = GetPropertyValue(
			properties,
			"AssemblyVersion"
		);

		Require(
			!string.IsNullOrWhiteSpace( versionPrefix ),
			"Directory.Build.props must define VersionPrefix."
		);
		Require(
			Version.TryParse(
				versionPrefix,
				out Version? parsedPrefix
			)
				&& 3 == parsedPrefix!.Build
				? false
				: true,
			""
		);

		Version parsed = Version.Parse( versionPrefix! );
		Require(
			0 <= parsed.Build && 0 > parsed.Revision,
			"VersionPrefix must contain exactly major, minor, and patch components."
		);
		Require(
			"$(Version)" == packageVersionExpression,
			"PackageVersion must be derived from Version in Directory.Build.props."
		);
		Require(
			"$(VersionPrefix).0" == assemblyVersionExpression,
			"AssemblyVersion must be derived from VersionPrefix in Directory.Build.props."
		);

		string packageVersion = string.IsNullOrWhiteSpace( versionSuffix )
			? versionPrefix!
			: $"{versionPrefix}-{versionSuffix}"
		;
		string assemblyVersion = $"{versionPrefix}.0";
		Require(
			Version.TryParse(
				assemblyVersion,
				out _
			),
			$"Derived assembly version '{assemblyVersion}' is not valid."
		);

		return ( packageVersion, assemblyVersion );
	}

	private static string? GetPropertyValue(
		XDocument document,
		string name
	) {
		ArgumentNullException.ThrowIfNull( document );
		ArgumentException.ThrowIfNullOrWhiteSpace( name );

		return document
			.Descendants()
			.FirstOrDefault(
				element => name == element.Name.LocalName
			)
			?.Value
			.Trim();
	}

	private static string VerifyPrimaryPackage(
		string packagePath,
		string expectedVersion,
		string expectedAssemblyVersion
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( packagePath );
		ArgumentException.ThrowIfNullOrWhiteSpace( expectedVersion );
		ArgumentException.ThrowIfNullOrWhiteSpace( expectedAssemblyVersion );

		using ZipArchive package = ZipFile.OpenRead( packagePath );
		HashSet<string> names = package.Entries
			.Select( entry => entry.FullName )
			.ToHashSet( StringComparer.Ordinal );

		List<string> required = [
			"README.md",
			"icon.png",
			"icod_tui_toolchain.jpg"
		];
		foreach ( string targetFramework in TargetFrameworks ) {
			required.Add(
				$"lib/{targetFramework}/Icod.Terminal.dll"
			);
			required.Add(
				$"lib/{targetFramework}/Icod.Terminal.xml"
			);
		}

		string[] missing = required
			.Where( name => !names.Contains( name ) )
			.OrderBy( name => name, StringComparer.Ordinal )
			.ToArray();
		Require(
			0 == missing.Length,
			"Primary package is missing required entries: "
				+ string.Join( ", ", missing )
		);
		Require(
			0 < package.GetEntry( "README.md" )!.Length,
			"README.md is empty in the primary package."
		);
		Require(
			0 < package.GetEntry( "icon.png" )!.Length,
			"icon.png is empty in the primary package."
		);
		Require(
			0 < package.GetEntry( "icod_tui_toolchain.jpg" )!.Length,
			"icod_tui_toolchain.jpg is empty in the primary package."
		);
		Require(
			!names.Any(
				name => name.EndsWith(
					".pdb",
					StringComparison.OrdinalIgnoreCase
				)
			),
			"Primary package unexpectedly contains portable PDB payloads."
		);
		Require(
			!names.Any(
				name => name.StartsWith(
					"runtimes/",
					StringComparison.Ordinal
				)
			),
			"Primary package unexpectedly contains a runtimes/ payload."
		);
		Require(
			!names.Any( HasNativeLibraryExtension ),
			"Primary package unexpectedly contains a native library payload."
		);
		Require(
			!names.Any( IsRepositoryOnlyEntry ),
			"Primary package unexpectedly contains repository-only tests, samples, tools, or docs."
		);

		string[] dlls = names
			.Where(
				name => name.EndsWith(
					".dll",
					StringComparison.OrdinalIgnoreCase
				)
			)
			.OrderBy( name => name, StringComparer.Ordinal )
			.ToArray();
		string[] expectedDlls = TargetFrameworks
			.Select(
				targetFramework =>
					$"lib/{targetFramework}/Icod.Terminal.dll"
			)
			.OrderBy( name => name, StringComparer.Ordinal )
			.ToArray();
		Require(
			dlls.SequenceEqual(
				expectedDlls,
				StringComparer.Ordinal
			),
			"Primary package contains unexpected DLL payloads: "
				+ string.Join( ", ", dlls )
		);

		foreach ( string targetFramework in TargetFrameworks ) {
			VerifyAssemblyIdentity(
				package,
				targetFramework,
				expectedAssemblyVersion
			);
			VerifyDocumentation(
				package,
				targetFramework
			);
		}

		ZipArchiveEntry[] nuspecs = package.Entries
			.Where(
				entry => entry.FullName.EndsWith(
					".nuspec",
					StringComparison.OrdinalIgnoreCase
				)
			)
			.ToArray();
		Require(
			1 == nuspecs.Length,
			$"Expected one nuspec, found {nuspecs.Length}."
		);

		using Stream nuspecStream = nuspecs[ 0 ].Open();
		XDocument nuspec = XDocument.Load(
			nuspecStream,
			LoadOptions.None
		);
		XElement? metadata = nuspec
			.Descendants()
			.FirstOrDefault(
				element => "metadata" == element.Name.LocalName
			);
		Require(
			metadata is not null,
			"Package nuspec has no metadata element."
		);

		Require(
			PackageId == GetMetadataText( metadata!, "id" ),
			"Unexpected package id."
		);
		Require(
			expectedVersion == GetMetadataText( metadata!, "version" ),
			"Unexpected package version."
		);
		Require(
			PackageId == GetMetadataText( metadata!, "title" ),
			"Unexpected package title."
		);
		Require(
			"Timothy J. Bruce" == GetMetadataText( metadata!, "authors" ),
			"Unexpected package authors."
		);
		Require(
			RepositoryUrl == GetMetadataText( metadata!, "projectUrl" ),
			"Unexpected package project URL."
		);
		Require(
			"README.md" == GetMetadataText( metadata!, "readme" ),
			"Package metadata does not identify README.md."
		);
		Require(
			"icon.png" == GetMetadataText( metadata!, "icon" ),
			"Package metadata does not identify icon.png."
		);
		Require(
			string.Equals(
				GetMetadataText(
					metadata!,
					"requireLicenseAcceptance"
				),
				"true",
				StringComparison.OrdinalIgnoreCase
			),
			"Package must require license acceptance."
		);
		Require(
			!string.IsNullOrWhiteSpace(
				GetMetadataText(
					metadata!,
					"description"
				)
			),
			"Package description is missing."
		);
		Require(
			!string.IsNullOrWhiteSpace(
				GetMetadataText(
					metadata!,
					"tags"
				)
			),
			"Package tags are missing."
		);

		XElement? license = metadata!
			.Elements()
			.FirstOrDefault(
				element => "license" == element.Name.LocalName
			);
		Require(
			license is not null,
			"Package metadata has no license element."
		);
		Require(
			"expression" == license!.Attribute( "type" )?.Value,
			"Package license is not an expression."
		);
		Require(
			"LGPL-3.0-or-later" == license.Value,
			"Unexpected package license expression."
		);

		VerifyDependencies( metadata! );

		XElement? repository = metadata!
			.Descendants()
			.FirstOrDefault(
				element => "repository" == element.Name.LocalName
			);
		Require(
			repository is not null,
			"Package metadata has no repository element."
		);
		Require(
			"git" == repository!.Attribute( "type" )?.Value,
			"Repository metadata is not git."
		);
		Require(
			RepositoryUrl == repository.Attribute( "url" )?.Value,
			"Unexpected repository URL in package metadata."
		);

		string commit = repository.Attribute( "commit" )?.Value ?? string.Empty;
		Require(
			Regex.IsMatch(
				commit,
				"^[0-9a-fA-F]{40}$",
				RegexOptions.CultureInvariant
			),
			$"Repository metadata has an invalid commit id: '{commit}'."
		);

		return commit;
	}

	private static void VerifyDependencies(
		XElement metadata
	) {
		ArgumentNullException.ThrowIfNull( metadata );

		XElement? dependencies = metadata
			.Elements()
			.FirstOrDefault(
				element => "dependencies" == element.Name.LocalName
			);
		Require(
			dependencies is not null,
			"Package metadata has no dependencies element."
		);
		Require(
			!dependencies!.Elements().Any(
				element => "dependency" == element.Name.LocalName
			),
			"Package dependencies must remain grouped by target framework."
		);

		XElement[] groups = dependencies.Elements()
			.Where(
				element => "group" == element.Name.LocalName
			)
			.ToArray();
		Require(
			TargetFrameworks.Length == groups.Length,
			$"Expected {TargetFrameworks.Length} dependency groups, found {groups.Length}."
		);

		VerifyDependencyFramework(
			groups,
			"8.0"
		);
		VerifyDependencyFramework(
			groups,
			"9.0"
		);
		VerifyDependencyFramework(
			groups,
			"10.0"
		);
	}

	private static void VerifyDependencyFramework(
		IEnumerable<XElement> groups,
		string frameworkVersion
	) {
		ArgumentNullException.ThrowIfNull( groups );
		ArgumentException.ThrowIfNullOrWhiteSpace( frameworkVersion );

		XElement[] matches = groups
			.Where(
				group => ( group.Attribute( "targetFramework" )?.Value ?? string.Empty )
					.Contains(
						frameworkVersion,
						StringComparison.OrdinalIgnoreCase
					)
			)
			.ToArray();
		Require(
			1 == matches.Length,
			$"Expected one dependency group for framework version {frameworkVersion}."
		);

		XElement[] dependencies = matches[ 0 ]
			.Elements()
			.Where(
				element => "dependency" == element.Name.LocalName
			)
			.ToArray();
		Require(
			2 == dependencies.Length,
			$"Framework {frameworkVersion} must contain exactly two runtime dependencies."
		);

		VerifyDependency(
			dependencies,
			"Icod.TermInfo",
			TermInfoDependencyVersion,
			frameworkVersion
		);
		VerifyDependency(
			dependencies,
			"Icod.Timing",
			TimingDependencyVersion,
			frameworkVersion
		);
	}

	private static void VerifyDependency(
		IEnumerable<XElement> dependencies,
		string packageId,
		string expectedVersion,
		string frameworkVersion
	) {
		ArgumentNullException.ThrowIfNull( dependencies );
		ArgumentException.ThrowIfNullOrWhiteSpace( packageId );
		ArgumentException.ThrowIfNullOrWhiteSpace( expectedVersion );
		ArgumentException.ThrowIfNullOrWhiteSpace( frameworkVersion );

		XElement[] matches = dependencies
			.Where(
				dependency => string.Equals(
					dependency.Attribute( "id" )?.Value,
					packageId,
					StringComparison.Ordinal
				)
			)
			.ToArray();
		Require(
			1 == matches.Length,
			$"Framework {frameworkVersion} must reference {packageId} exactly once."
		);
		Require(
			expectedVersion == matches[ 0 ].Attribute( "version" )?.Value,
			$"Framework {frameworkVersion} references an unexpected {packageId} version."
		);
	}

	private static void VerifyDocumentation(
		ZipArchive package,
		string targetFramework
	) {
		ArgumentNullException.ThrowIfNull( package );
		ArgumentException.ThrowIfNullOrWhiteSpace( targetFramework );

		string documentationPath = $"lib/{targetFramework}/Icod.Terminal.xml";
		ZipArchiveEntry? entry = package.GetEntry( documentationPath );
		Require(
			entry is not null,
			$"Primary package is missing {documentationPath}."
		);
		Require(
			0 < entry!.Length,
			$"{documentationPath} is empty."
		);

		using Stream stream = entry.Open();
		XDocument documentation = XDocument.Load(
			stream,
			LoadOptions.None
		);
		string? assemblyName = documentation
			.Descendants()
			.FirstOrDefault(
				element => "assembly" == element.Name.LocalName
			)
			?.Elements()
			.FirstOrDefault(
				element => "name" == element.Name.LocalName
			)
			?.Value;
		Require(
			PackageId == assemblyName,
			$"{documentationPath} identifies unexpected assembly '{assemblyName}'."
		);

		string[] memberNames = documentation
			.Descendants()
			.Where(
				element => "member" == element.Name.LocalName
			)
			.Select(
				element => element.Attribute( "name" )?.Value ?? string.Empty
			)
			.Where(
				static name => !string.IsNullOrWhiteSpace( name )
			)
			.ToArray();
		Require(
			0 < memberNames.Length,
			$"{documentationPath} contains no documented members."
		);

		HashSet<string> documented = memberNames.ToHashSet(
			StringComparer.Ordinal
		);
		string[] missingQueryDocumentation = RequiredQueryDocumentationMembers
			.Where(
				member => !documented.Contains( member )
			)
			.ToArray();
		Require(
			0 == missingQueryDocumentation.Length,
			$"{documentationPath} is missing required 0.3 query documentation: "
				+ string.Join(
					", ",
					missingQueryDocumentation
				)
		);
	}

	private static void VerifyAssemblyIdentity(
		ZipArchive package,
		string targetFramework,
		string expectedAssemblyVersion
	) {
		ArgumentNullException.ThrowIfNull( package );
		ArgumentException.ThrowIfNullOrWhiteSpace( targetFramework );
		ArgumentException.ThrowIfNullOrWhiteSpace( expectedAssemblyVersion );

		string assemblyPath = $"lib/{targetFramework}/Icod.Terminal.dll";
		ZipArchiveEntry? entry = package.GetEntry( assemblyPath );
		Require(
			entry is not null,
			$"Primary package is missing {assemblyPath}."
		);

		string temporaryPath = Path.Combine(
			Path.GetTempPath(),
			"Icod.Terminal-package-verifier-"
				+ Guid.NewGuid().ToString( "N" )
				+ ".dll"
		);

		try {
			using ( Stream source = entry!.Open() )
			using ( FileStream destination = File.Create( temporaryPath ) ) {
				source.CopyTo( destination );
			}

			AssemblyName assemblyName = AssemblyName.GetAssemblyName( temporaryPath );
			Require(
				PackageId == assemblyName.Name,
				$"{assemblyPath} has unexpected assembly name '{assemblyName.Name}'."
			);
			Require(
				expectedAssemblyVersion == assemblyName.Version?.ToString(),
				$"{assemblyPath} has assembly version '{assemblyName.Version}', expected "
					+ expectedAssemblyVersion
					+ "."
			);

			byte[]? publicKeyToken = assemblyName.GetPublicKeyToken();
			Require(
				publicKeyToken is null || 0 == publicKeyToken.Length,
				$"{assemblyPath} is unexpectedly strong-name signed."
			);
		} finally {
			if ( File.Exists( temporaryPath ) ) {
				File.Delete( temporaryPath );
			}
		}
	}

	private static void VerifySymbolPackage(
		string packagePath,
		string commit
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( packagePath );
		ArgumentException.ThrowIfNullOrWhiteSpace( commit );

		using ZipArchive symbols = ZipFile.OpenRead( packagePath );
		HashSet<string> names = symbols.Entries
			.Select( entry => entry.FullName )
			.ToHashSet( StringComparer.Ordinal );
		string[] expectedPdbs = TargetFrameworks
			.Select(
				targetFramework =>
					$"lib/{targetFramework}/Icod.Terminal.pdb"
			)
			.OrderBy( name => name, StringComparer.Ordinal )
			.ToArray();
		string[] pdbs = names
			.Where(
				name => name.EndsWith(
					".pdb",
					StringComparison.OrdinalIgnoreCase
				)
			)
			.OrderBy( name => name, StringComparer.Ordinal )
			.ToArray();
		Require(
			pdbs.SequenceEqual(
				expectedPdbs,
				StringComparer.Ordinal
			),
			"Symbol package contains an unexpected PDB set: "
				+ string.Join( ", ", pdbs )
		);
		Require(
			!names.Any(
				name => name.EndsWith(
					".dll",
					StringComparison.OrdinalIgnoreCase
				)
			),
			"Symbol package unexpectedly contains managed assemblies."
		);

		foreach ( string pdbPath in expectedPdbs ) {
			ZipArchiveEntry? pdbEntry = symbols.GetEntry( pdbPath );
			Require(
				pdbEntry is not null,
				$"Symbol package is missing {pdbPath}."
			);

			using Stream stream = pdbEntry!.Open();
			using MemoryStream buffer = new();
			stream.CopyTo( buffer );
			byte[] pdb = buffer.ToArray();
			Require(
				pdb.AsSpan().StartsWith( "BSJB"u8 ),
				$"{pdbPath} is not a portable PDB."
			);
			Require(
				ContainsAscii(
					pdb,
					"raw.githubusercontent.com/uniblab/Icod.Terminal/"
				),
				$"{pdbPath} does not contain the expected GitHub Source Link mapping."
			);
			Require(
				ContainsAscii(
					pdb,
					commit
				),
				$"{pdbPath} Source Link data does not contain the package repository commit."
			);
		}
	}

	private static string? GetMetadataText(
		XElement metadata,
		string name
	) {
		ArgumentNullException.ThrowIfNull( metadata );
		ArgumentException.ThrowIfNullOrWhiteSpace( name );

		return metadata
			.Elements()
			.FirstOrDefault(
				element => name == element.Name.LocalName
			)
			?.Value;
	}

	private static bool HasNativeLibraryExtension(
		string name
	) {
		ArgumentNullException.ThrowIfNull( name );

		return name.EndsWith(
			".so",
			StringComparison.OrdinalIgnoreCase
		) || name.EndsWith(
			".dylib",
			StringComparison.OrdinalIgnoreCase
		) || name.EndsWith(
			".a",
			StringComparison.OrdinalIgnoreCase
		) || name.EndsWith(
			".lib",
			StringComparison.OrdinalIgnoreCase
		);
	}

	private static bool IsRepositoryOnlyEntry(
		string name
	) {
		ArgumentNullException.ThrowIfNull( name );

		return name.StartsWith(
			".github/",
			StringComparison.OrdinalIgnoreCase
		) || name.StartsWith(
			"docs/",
			StringComparison.OrdinalIgnoreCase
		) || name.StartsWith(
			"samples/",
			StringComparison.OrdinalIgnoreCase
		) || name.StartsWith(
			"tests/",
			StringComparison.OrdinalIgnoreCase
		) || name.StartsWith(
			"tools/",
			StringComparison.OrdinalIgnoreCase
		);
	}

	private static bool ContainsAscii(
		byte[] data,
		string text
	) {
		ArgumentNullException.ThrowIfNull( data );
		ArgumentNullException.ThrowIfNull( text );

		byte[] expected = Encoding.ASCII.GetBytes( text );
		return 0 <= data.AsSpan().IndexOf( expected );
	}

	private static void Require(
		bool condition,
		string message
	) {
		ArgumentNullException.ThrowIfNull( message );

		if ( !condition ) {
			throw new InvalidDataException( message );
		}
	}
}
