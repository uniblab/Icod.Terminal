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

internal static class CompatibilityScenarioCatalog {
	private static readonly IReadOnlyList<CompatibilityScenario> OrderedScenarios =
		CreateScenarios();

	internal static IReadOnlyList<CompatibilityScenario> All => OrderedScenarios;

	internal static CompatibilityScenario? Find(
		string id
	) => OrderedScenarios.FirstOrDefault(
		scenario => string.Equals( scenario.Id, id, StringComparison.Ordinal )
	);

	private static IReadOnlyList<CompatibilityScenario> CreateScenarios() {
		var definitions = new[] {
			( "identity.session", "Session identity", "The public session opens and exposes bounded identity and endpoint observations." ),
			( "query.dimensions", "Terminal and cell dimensions", "Both bounded pixel-dimension queries return typed observations." ),
			( "query.device-attributes", "Primary and secondary device attributes", "Both bounded device-attribute queries return typed observations." ),
			( "query.status-cursor", "Device status and cursor position", "Both bounded CSI status queries return typed observations." ),
			( "query.decrqss", "DEC status string", "The bounded SGR DECRQSS query returns a supported typed observation." ),
			( "query.xtgettcap", "XTGETTCAP terminal name", "The bounded TN query returns a supported typed observation." ),
			( "query.color", "Palette and dynamic colors", "Both bounded color queries return typed observations." ),
			( "query.pointer-shape", "Pointer shape support", "The terminal explicitly reports support for the requested pointer shape." ),
			( "metadata.titles-location", "Titles and current location", "The operator observes the requested bounded title and location metadata." ),
			( "semantic.hyperlinks-regions", "Hyperlinks and semantic regions", "The operator observes the requested hyperlink and semantic prompt boundaries." ),
			( "semantic.vscode-shell", "VS Code shell integration", "The typed OSC 633 sequence is accepted in the VS Code companion lane." ),
			( "semantic.iterm2-shell", "iTerm2 shell integration", "The typed OSC 1337 metadata sequence is accepted in the iTerm2 lane." ),
			( "notifications", "Desktop notifications", "The operator observes a notification emitted through an existing typed backend." ),
			( "progress", "Terminal progress", "The operator observes the bounded progress lifecycle." ),
			( "clipboard.osc52", "OSC 52 clipboard", "An explicitly supplied fixed test value completes a consented clipboard round trip." ),
			( "input.text-key", "Text and key input", "The sample receives distinct typed text and key events." ),
			( "input.paste-focus-mouse", "Paste, focus, and mouse input", "The sample receives distinct typed paste, focus, and mouse events." ),
			( "input.modern-keyboard", "Modern keyboard phases", "The sample observes negotiated modern-keyboard phase information without inventing releases." ),
			( "lifecycle.resize-suspend", "Resize and suspend lifecycle", "The sample observes typed resize and suspend/resume lifecycle events." ),
			( "presentation.cursor-sync", "Cursor style and synchronized output", "The operator observes scoped cursor-style and synchronized-output behavior and cleanup." ),
			( "presentation.pointer-colors", "Pointer and color presentation", "The operator observes scoped pointer and color behavior and cleanup." )
		};

		var scenarios = new List<CompatibilityScenario>( definitions.Length );
		foreach ( (string id, string description, string success) in definitions ) {
			if ( "identity.session" == id ) {
				scenarios.Add(
					new CompatibilityScenario(
						id,
						1,
						description,
						success,
						false,
						static ( _, cancellationToken ) => {
							cancellationToken.ThrowIfCancellationRequested();
							return ValueTask.FromResult(
								new CompatibilityScenarioObservation(
									CompatibilityOutcome.Pass,
									true,
									null,
									"The public terminal session opened with interactive endpoints."
								)
							);
						}
					)
				);
			} else if ( QueryCompatibilityScenarios.ScenarioIds.Contains( id ) ) {
				scenarios.Add(
					new CompatibilityScenario(
						id,
						1,
						description,
						success,
						false,
						( context, cancellationToken ) => QueryCompatibilityScenarios.RunAsync(
							id,
							context.Queries,
							cancellationToken
						)
					)
				);
			} else {
				scenarios.Add(
					new CompatibilityScenario(
						id,
						1,
						description,
						success,
						true,
						( context, cancellationToken ) => LiveCompatibilityScenarios.RunAsync(
							id,
							context.Live,
							cancellationToken
						)
					)
				);
			}
		}
		return scenarios.AsReadOnly();
	}
}
