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

using Icod.Terminal;

internal static class MetadataCompatibilityScenarios {
	private const string TestTitle = "Icod.Terminal compatibility test";
	private const string TestDirectory = "/tmp/icod-terminal-compatibility";

	internal static ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		TerminalSession session,
		string scenarioId,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		return scenarioId switch {
			"metadata.titles-location" => RunTitlesAndLocationAsync( session, cancellationToken ),
			"semantic.hyperlinks-regions" => RunHyperlinksAndRegionsAsync( session, cancellationToken ),
			"semantic.vscode-shell" => RunVsCodeAsync( session, cancellationToken ),
			"semantic.iterm2-shell" => RunITerm2Async( session, cancellationToken ),
			_ => throw new ArgumentException( "Unknown metadata scenario.", nameof( scenarioId ) )
		};
	}

	private static async ValueTask<CompatibilityLiveExecution> RunTitlesAndLocationAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		await session.SetTitleAsync( TestTitle, cancellationToken ).ConfigureAwait( false );
		await session.SetIconNameAsync( "Icod.Terminal compatibility", cancellationToken ).ConfigureAwait( false );
		await session.SetWindowTitleAsync( TestTitle, cancellationToken ).ConfigureAwait( false );
		await session.PublishCurrentLocationAsync(
			TestDirectory,
			TerminalLocationPathStyle.Posix,
			authority: null,
			cancellationToken
		).ConfigureAwait( false );
		await session.WriteTextAsync(
			"Requested fixed OSC 0/1/2 titles and OSC 7 location metadata.\r\n",
			cancellationToken
		).ConfigureAwait( false );
		return Visible( "Typed title and location requests completed." );
	}

	private static async ValueTask<CompatibilityLiveExecution> RunHyperlinksAndRegionsAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		await session.WriteHyperlinkAsync(
			"https://example.invalid/icod-terminal-compatibility",
			"Icod.Terminal compatibility hyperlink",
			identifier: "icod-terminal-compatibility",
			cancellationToken
		).ConfigureAwait( false );
		await session.WriteTextAsync( "\r\n", cancellationToken ).ConfigureAwait( false );
		await session.BeginPromptAsync( cancellationToken ).ConfigureAwait( false );
		await session.WriteTextAsync( "compatibility> ", cancellationToken ).ConfigureAwait( false );
		await session.BeginCommandInputAsync( cancellationToken ).ConfigureAwait( false );
		await session.WriteTextAsync( "sample-command\r\n", cancellationToken ).ConfigureAwait( false );
		await session.BeginCommandOutputAsync( cancellationToken ).ConfigureAwait( false );
		await session.WriteTextAsync( "sample output\r\n", cancellationToken ).ConfigureAwait( false );
		await session.FinishCommandAsync( 0, cancellationToken ).ConfigureAwait( false );
		return Visible( "Typed OSC 8 and OSC 133 requests completed." );
	}

	private static async ValueTask<CompatibilityLiveExecution> RunVsCodeAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		await session.BeginVsCodePromptAsync( cancellationToken ).ConfigureAwait( false );
		await session.BeginVsCodeCommandInputAsync( cancellationToken ).ConfigureAwait( false );
		await session.PublishVsCodeCommandLineAsync(
			"sample-command",
			nonce: null,
			cancellationToken
		).ConfigureAwait( false );
		await session.PublishVsCodeCurrentDirectoryAsync(
			TestDirectory,
			nonce: null,
			cancellationToken
		).ConfigureAwait( false );
		await session.BeginVsCodeCommandOutputAsync( cancellationToken ).ConfigureAwait( false );
		await session.FinishVsCodeCommandAsync( 0, cancellationToken ).ConfigureAwait( false );
		await session.WriteTextAsync(
			"Requested a fixed OSC 633 shell-integration lifecycle.\r\n",
			cancellationToken
		).ConfigureAwait( false );
		return Visible( "Typed OSC 633 requests completed." );
	}

	private static async ValueTask<CompatibilityLiveExecution> RunITerm2Async(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		await session.SetITerm2MarkAsync( cancellationToken ).ConfigureAwait( false );
		await session.PublishITerm2CurrentDirectoryAsync( TestDirectory, cancellationToken ).ConfigureAwait( false );
		await session.PublishITerm2RemoteHostAsync(
			"compatibility",
			"localhost",
			cancellationToken
		).ConfigureAwait( false );
		await session.SetITerm2UserVariableAsync(
			"IcodTerminalCompatibility",
			"sample",
			cancellationToken
		).ConfigureAwait( false );
		await session.PublishITerm2ShellIntegrationVersionAsync(
			1,
			"sample",
			cancellationToken
		).ConfigureAwait( false );
		await session.WriteTextAsync(
			"Requested fixed OSC 1337 shell-integration metadata.\r\n",
			cancellationToken
		).ConfigureAwait( false );
		return Visible( "Typed OSC 1337 requests completed." );
	}

	private static CompatibilityLiveExecution Visible(
		string note
	) => new( true, true, note );
}
