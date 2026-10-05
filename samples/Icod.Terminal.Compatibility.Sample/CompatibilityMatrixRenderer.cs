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

using System.Text;

/// <summary>Renders reviewed evidence as deterministic Markdown.</summary>
internal static class CompatibilityMatrixRenderer {
	private static readonly IReadOnlyDictionary<string, int> LaneOrder =
		new Dictionary<string, int>( StringComparer.Ordinal ) {
			[ "windows-terminal" ] = 0,
			[ "apple-terminal" ] = 1,
			[ "iterm2" ] = 2,
			[ "kitty" ] = 3,
			[ "wezterm" ] = 4,
			[ "ghostty" ] = 5,
			[ "alacritty" ] = 6,
			[ "gnome-terminal-vte" ] = 7,
			[ "konsole" ] = 8,
			[ "xterm" ] = 9,
			[ "vscode" ] = 10
		};

	private static readonly IReadOnlyDictionary<string, string> LaneNames =
		new Dictionary<string, string>( StringComparer.Ordinal ) {
			[ "windows-terminal" ] = "Windows Terminal",
			[ "apple-terminal" ] = "Apple Terminal",
			[ "iterm2" ] = "iTerm2",
			[ "kitty" ] = "Kitty",
			[ "wezterm" ] = "WezTerm",
			[ "ghostty" ] = "Ghostty",
			[ "alacritty" ] = "Alacritty",
			[ "gnome-terminal-vte" ] = "GNOME Terminal/VTE",
			[ "konsole" ] = "Konsole",
			[ "xterm" ] = "XTerm",
			[ "vscode" ] = "VS Code"
		};

	internal static string Render(
		IEnumerable<CompatibilityEvidence> evidence,
		string releaseVersion
	) {
		ArgumentNullException.ThrowIfNull( evidence );
		if ( string.IsNullOrWhiteSpace( releaseVersion )
			|| releaseVersion.Any( static character => character < ' ' || character is '|' or '\u007f' ) ) {
			throw new FormatException( "The release version is not valid Markdown text." );
		}

		List<CompatibilityEvidence> ordered = evidence.ToList();
		foreach ( CompatibilityEvidence item in ordered ) {
			CompatibilityEvidenceValidator.Validate( item );
		}
		var duplicates = new HashSet<string>( StringComparer.Ordinal );
		foreach ( CompatibilityEvidence item in ordered ) {
			string key = string.Join(
				'\u001f',
				item.TerminalId,
				item.TerminalVersion,
				item.OperatingSystem,
				item.OperatingSystemVersion,
				item.Transport ?? "",
				item.TransportVersion ?? "",
				item.ScenarioId,
				item.ScenarioRevision.ToString( System.Globalization.CultureInfo.InvariantCulture )
			);
			if ( !duplicates.Add( key ) ) {
				throw new FormatException( "Duplicate terminal/environment/scenario evidence was supplied." );
			}
		}

		ordered.Sort( Compare );
		var output = new StringBuilder();
		output.Append( "# Icod.Terminal " ).Append( releaseVersion ).Append( " compatibility matrix\n\n" );
		output.Append( "Results qualify only the exact scenario revision and environment recorded below. Missing or silent behavior is not inferred.\n\n" );
		output.Append( "## Lane qualification summary\n\n" );
		output.Append( "A lane with no reviewed evidence is explicitly `NotRun`. When evidence exists, every unrecorded scenario remains `NotRun`.\n\n" );
		output.Append( "| Terminal lane | Reviewed evidence |\n" );
		output.Append( "| --- | --- |\n" );
		foreach ( KeyValuePair<string, int> lane in LaneOrder.OrderBy( static value => value.Value ) ) {
			int count = ordered.Count( item => string.Equals(
				item.TerminalId,
				lane.Key,
				StringComparison.Ordinal
			) );
			output.Append( "| " ).Append( EscapeMarkdown( LaneNames[ lane.Key ] ) ).Append( " | " );
			if ( 0 == count ) {
				output.Append( "`NotRun`" );
			} else {
				output.Append( count ).Append( 1 == count ? " reviewed result" : " reviewed results" )
					.Append( "; other scenarios `NotRun`" );
			}
			output.Append( " |\n" );
		}

		output.Append( "\n## Reviewed results\n\n" );
		if ( 0 == ordered.Count ) {
			output.Append( "No reviewed live results are recorded.\n" );
		} else {
			output.Append( "| Terminal lane | Scenario | Outcome | Environment |\n" );
			output.Append( "| --- | --- | --- | --- |\n" );
			for ( int index = 0; index < ordered.Count; ++index ) {
				CompatibilityEvidence item = ordered[ index ];
				output.Append( "| " ).Append( EscapeMarkdown( FormatLane( item ) ) )
					.Append( " | `" ).Append( item.ScenarioId ).Append( "/v" ).Append( item.ScenarioRevision )
					.Append( "` | `" ).Append( item.Outcome ).Append( "` | [" ).Append( index + 1 ).Append( "] |\n" );
			}
		}

		output.Append( "\n## Environments\n\n" );
		if ( 0 == ordered.Count ) {
			output.Append( "No reviewed live environments are recorded.\n" );
		}
		for ( int index = 0; index < ordered.Count; ++index ) {
			CompatibilityEvidence item = ordered[ index ];
			output.Append( index + 1 ).Append( ". " )
				.Append( EscapeMarkdown( LaneNames[ item.TerminalId ] ) ).Append( ' ' )
				.Append( EscapeMarkdown( item.TerminalVersion ) ).Append( "; " )
				.Append( EscapeMarkdown( item.OperatingSystem ) ).Append( ' ' )
				.Append( EscapeMarkdown( item.OperatingSystemVersion ) ).Append( "; " )
				.Append( null == item.Transport
					? "direct"
					: $"{EscapeMarkdown( item.Transport )} {EscapeMarkdown( item.TransportVersion! )}" )
				.Append( "; Icod.Terminal " ).Append( EscapeMarkdown( item.TerminalPackageVersion ) )
				.Append( " at `" ).Append( item.SourceCommit ).Append( "`; observed " )
				.Append( item.ObservedAtUtc.ToString( "O", System.Globalization.CultureInfo.InvariantCulture ) )
				.Append( ". " ).Append( EscapeMarkdown( item.Note ) ).Append( '\n' );
		}

		output.Append( "\n## Outcome legend\n\n" );
		output.Append( "- `Pass`: the exact scenario revision's requested behavior was observed.\n" );
		output.Append( "- `Fail`: execution completed but contradicted the scenario.\n" );
		output.Append( "- `Inconclusive`: permission, policy, timeout, environment, or ambiguity prevented a conclusion.\n" );
		output.Append( "- `NotRun`: no accepted live result exists.\n" );
		output.Append( "- `NotApplicable`: the scenario has no meaningful application to the terminal lane.\n" );
		return output.ToString();
	}

	private static int Compare(
		CompatibilityEvidence left,
		CompatibilityEvidence right
	) {
		int result = LaneOrder[ left.TerminalId ].CompareTo( LaneOrder[ right.TerminalId ] );
		if ( 0 != result ) {
			return result;
		}
		foreach ( Func<CompatibilityEvidence, string> selector in new Func<CompatibilityEvidence, string>[] {
			static value => value.TerminalVersion,
			static value => value.OperatingSystem,
			static value => value.OperatingSystemVersion,
			static value => value.Transport ?? "",
			static value => value.TransportVersion ?? "",
			static value => value.ScenarioId
		} ) {
			result = StringComparer.Ordinal.Compare( selector( left ), selector( right ) );
			if ( 0 != result ) {
				return result;
			}
		}
		return left.ScenarioRevision.CompareTo( right.ScenarioRevision );
	}

	private static string FormatLane(
		CompatibilityEvidence evidence
	) => null == evidence.Transport
		? LaneNames[ evidence.TerminalId ]
		: $"{LaneNames[ evidence.TerminalId ]} via {evidence.Transport} {evidence.TransportVersion}";

	private static string EscapeMarkdown(
		string value
	) => value.Replace( "\\", "\\\\", StringComparison.Ordinal )
		.Replace( "|", "\\|", StringComparison.Ordinal );
}
