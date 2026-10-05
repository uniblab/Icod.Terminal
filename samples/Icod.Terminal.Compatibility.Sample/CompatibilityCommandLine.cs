/*
	Icod.Terminal.Compatibility.Sample
	Sample application demonstrating Icod.Terminal Compatibility features.
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
namespace Icod.Terminal.Compatibility.Sample;

using System.Text.Json;
using Icod.Terminal;

internal static class CompatibilityCommandLine {
	internal const string HelpText =
		"Usage:\n"
		+ "  Icod.Terminal.Compatibility.Sample --help\n"
		+ "  Icod.Terminal.Compatibility.Sample --list-scenarios\n"
		+ "  Icod.Terminal.Compatibility.Sample --describe <scenario>\n"
		+ "  Icod.Terminal.Compatibility.Sample --render-matrix <evidence-directory> <output-file>\n"
		+ "  Icod.Terminal.Compatibility.Sample --run <scenario> <identity-options> [--output <file>] [--overwrite]\n"
		+ "  Icod.Terminal.Compatibility.Sample --run-all <identity-options> [--output <file>] [--overwrite]\n"
		+ "Identity options: --terminal <id> --terminal-version <version> --os <name> --os-version <version> --source-commit <40-hex-sha>\n"
		+ "Mediated runs also require: --transport <name> --transport-version <version>";

	internal static async ValueTask<int> RunAsync(
		string[] args,
		TextWriter output,
		TextWriter error,
		Func<CancellationToken, ValueTask<TerminalSession>> sessionFactory,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( error );
		ArgumentNullException.ThrowIfNull( sessionFactory );
		try {
			if ( args.SequenceEqual( new[] { "--help" }, StringComparer.Ordinal ) ) {
				await output.WriteLineAsync( HelpText );
				return 0;
			}
			if ( args.SequenceEqual( new[] { "--list-scenarios" }, StringComparer.Ordinal ) ) {
				foreach ( CompatibilityScenario scenario in CompatibilityScenarioCatalog.All ) {
					await output.WriteLineAsync( $"{scenario.Id}/v{scenario.Revision} — {scenario.Description}" );
				}
				return 0;
			}
			if ( 2 == args.Length && "--describe" == args[ 0 ] ) {
				CompatibilityScenario? scenario = CompatibilityScenarioCatalog.Find( args[ 1 ] );
				if ( null == scenario ) {
					return await UsageErrorAsync( error, $"Unknown scenario: {args[ 1 ]}." );
				}
				await output.WriteLineAsync( $"{scenario.Id}/v{scenario.Revision}" );
				await output.WriteLineAsync( scenario.Description );
				await output.WriteLineAsync( $"Success condition: {scenario.SuccessCondition}" );
				await output.WriteLineAsync( $"External side effects: {scenario.HasExternalSideEffects}" );
				return 0;
			}
			if ( 3 == args.Length && "--render-matrix" == args[ 0 ] ) {
				return await RenderMatrixAsync( args[ 1 ], args[ 2 ], error, cancellationToken );
			}
			if ( 3 <= args.Length && args[ 0 ] is "--run" or "--run-all" ) {
				if ( !TryParseLive( args, out ParsedLiveCommand? command, out string? parseError ) ) {
					return await UsageErrorAsync( error, parseError! );
				}
				await using TerminalSession session = await sessionFactory( cancellationToken ).ConfigureAwait( false );
				IReadOnlyList<CompatibilityScenario> scenarios = command!.RunAll
					? CompatibilityScenarioCatalog.All
					: new[] { CompatibilityScenarioCatalog.Find( command.ScenarioId! )! };
				IReadOnlyList<CompatibilityEvidence> evidence = await CompatibilityRunner.RunAsync(
					session,
					scenarios,
					command.Identity,
					output,
					cancellationToken
				).ConfigureAwait( false );
				if ( null != command.OutputPath ) {
					await CompatibilityReportWriter.WriteAsync(
						command.OutputPath,
						evidence,
						command.Overwrite,
						cancellationToken
					).ConfigureAwait( false );
				}
				return 0;
			}
			return await UsageErrorAsync( error, "The command line is incomplete or unknown." );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await error.WriteLineAsync( "Cancelled; terminal session cleanup completed." );
			return 130;
		} catch ( Exception exception ) {
			await error.WriteLineAsync( $"Compatibility run failed: {exception.Message}" );
			return 1;
		}
	}

	private static async ValueTask<int> RenderMatrixAsync(
		string evidenceDirectory,
		string outputPath,
		TextWriter error,
		CancellationToken cancellationToken
	) {
		if ( !Directory.Exists( evidenceDirectory ) ) {
			return await UsageErrorAsync( error, "The evidence directory does not exist." );
		}
		if ( File.Exists( outputPath ) ) {
			return await UsageErrorAsync( error, "The matrix output file already exists." );
		}
		string? outputDirectory = Path.GetDirectoryName( Path.GetFullPath( outputPath ) );
		if ( string.IsNullOrWhiteSpace( outputDirectory ) || !Directory.Exists( outputDirectory ) ) {
			return await UsageErrorAsync( error, "The matrix output directory does not exist." );
		}
		var evidence = new List<CompatibilityEvidence>();
		foreach ( string path in Directory.EnumerateFiles( evidenceDirectory, "*.json" ).Order( StringComparer.Ordinal ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			CompatibilityEvidence? item = JsonSerializer.Deserialize<CompatibilityEvidence>(
				await File.ReadAllTextAsync( path, cancellationToken ).ConfigureAwait( false ),
				CompatibilityReportWriter.JsonOptions
			);
			if ( null == item ) {
				throw new FormatException( $"Evidence file '{path}' contains no result." );
			}
			evidence.Add( item );
		}
		string matrix = CompatibilityMatrixRenderer.Render( evidence, "1.26.0" );
		await File.WriteAllTextAsync( outputPath, matrix, cancellationToken ).ConfigureAwait( false );
		return 0;
	}

	private static bool TryParseLive(
		string[] args,
		out ParsedLiveCommand? command,
		out string? error
	) {
		command = null;
		error = null;
		bool runAll = "--run-all" == args[ 0 ];
		int index = 1;
		string? scenarioId = null;
		if ( !runAll ) {
			if ( index >= args.Length || args[ index ].StartsWith( "--", StringComparison.Ordinal ) ) {
				error = "--run requires one scenario identifier.";
				return false;
			}
			scenarioId = args[ index++ ];
			if ( null == CompatibilityScenarioCatalog.Find( scenarioId ) ) {
				error = $"Unknown scenario: {scenarioId}.";
				return false;
			}
		}

		var values = new Dictionary<string, string>( StringComparer.Ordinal );
		bool overwrite = false;
		while ( index < args.Length ) {
			string option = args[ index++ ];
			if ( "--overwrite" == option ) {
				if ( overwrite ) {
					error = "--overwrite may appear only once.";
					return false;
				}
				overwrite = true;
				continue;
			}
			if ( option is not ( "--terminal" or "--terminal-version" or "--os" or "--os-version" or "--transport" or "--transport-version" or "--source-commit" or "--output" ) ) {
				error = $"Unknown option: {option}.";
				return false;
			}
			if ( index >= args.Length || args[ index ].StartsWith( "--", StringComparison.Ordinal ) ) {
				error = $"{option} requires a value.";
				return false;
			}
			if ( !values.TryAdd( option, args[ index++ ] ) ) {
				error = $"{option} may appear only once.";
				return false;
			}
		}

		foreach ( string required in new[] { "--terminal", "--terminal-version", "--os", "--os-version", "--source-commit" } ) {
			if ( !values.TryGetValue( required, out string? value ) || string.IsNullOrWhiteSpace( value ) ) {
				error = $"{required} is required for live execution.";
				return false;
			}
		}
		bool hasTransport = values.ContainsKey( "--transport" );
		bool hasTransportVersion = values.ContainsKey( "--transport-version" );
		if ( hasTransport != hasTransportVersion ) {
			error = "--transport and --transport-version must be supplied together.";
			return false;
		}
		if ( overwrite && !values.ContainsKey( "--output" ) ) {
			error = "--overwrite is valid only with --output.";
			return false;
		}
		string sourceCommit = values[ "--source-commit" ];
		if ( 40 != sourceCommit.Length || sourceCommit.Any( static value => !Uri.IsHexDigit( value ) ) ) {
			error = "--source-commit must be one complete 40-character hexadecimal Git object name.";
			return false;
		}

		var identity = new CompatibilityRunIdentity(
			values[ "--terminal" ],
			values[ "--terminal-version" ],
			values[ "--os" ],
			values[ "--os-version" ],
			values.GetValueOrDefault( "--transport" ),
			values.GetValueOrDefault( "--transport-version" ),
			sourceCommit
		);
		try {
			CompatibilityEvidenceValidator.Validate(
				new CompatibilityEvidence(
					"1",
					"1.26.0-alpha",
					identity.SourceCommit,
					identity.TerminalId,
					identity.TerminalVersion,
					identity.OperatingSystem,
					identity.OperatingSystemVersion,
					identity.Transport,
					identity.TransportVersion,
					"identity.session",
					1,
					DateTimeOffset.UtcNow,
					CompatibilityOutcome.NotRun,
					null,
					null,
					""
				)
			);
		} catch ( FormatException exception ) {
			error = exception.Message;
			return false;
		}
		command = new ParsedLiveCommand(
			runAll,
			scenarioId,
			identity,
			values.GetValueOrDefault( "--output" ),
			overwrite
		);
		return true;
	}

	private static async ValueTask<int> UsageErrorAsync(
		TextWriter error,
		string message
	) {
		await error.WriteLineAsync( message );
		await error.WriteLineAsync( "Use --help for usage." );
		return 2;
	}

	private sealed record ParsedLiveCommand(
		bool RunAll,
		string? ScenarioId,
		CompatibilityRunIdentity Identity,
		string? OutputPath,
		bool Overwrite
	);
}
