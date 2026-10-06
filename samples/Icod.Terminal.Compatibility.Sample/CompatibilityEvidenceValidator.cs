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

using System.Collections.Frozen;

/// <summary>Validates the closed compatibility evidence format.</summary>
internal static class CompatibilityEvidenceValidator {
	private const int IdentityLimit = 128;
	private const int NoteLimit = 512;

	private static readonly FrozenSet<string> TerminalIds = new[] {
		"windows-terminal",
		"apple-terminal",
		"iterm2",
		"kitty",
		"wezterm",
		"ghostty",
		"alacritty",
		"gnome-terminal-vte",
		"konsole",
		"xterm",
		"vscode"
	}.ToFrozenSet( StringComparer.Ordinal );

	private static readonly FrozenDictionary<string, int> ScenarioRevisions =
		new Dictionary<string, int>( StringComparer.Ordinal ) {
			[ "identity.session" ] = 1,
			[ "query.dimensions" ] = 1,
			[ "query.device-attributes" ] = 1,
			[ "query.status-cursor" ] = 1,
			[ "query.decrqss" ] = 1,
			[ "query.xtgettcap" ] = 1,
			[ "query.color" ] = 1,
			[ "query.pointer-shape" ] = 1,
			[ "metadata.titles-location" ] = 1,
			[ "semantic.hyperlinks-regions" ] = 1,
			[ "semantic.vscode-shell" ] = 1,
			[ "semantic.iterm2-shell" ] = 1,
			[ "notifications" ] = 1,
			[ "progress" ] = 1,
			[ "clipboard.osc52" ] = 1,
			[ "input.text-key" ] = 1,
			[ "input.paste-focus-mouse" ] = 1,
			[ "input.modern-keyboard" ] = 1,
			[ "lifecycle.resize-suspend" ] = 1,
			[ "presentation.cursor-sync" ] = 1,
			[ "presentation.pointer-colors" ] = 1,
			[ "query.appearance" ] = 1,
			[ "environment.appearance-reporting" ] = 1,
			[ "environment.in-band-resize" ] = 1
		}.ToFrozenDictionary( StringComparer.Ordinal );

	internal static IReadOnlyDictionary<string, int> KnownScenarioRevisions =>
		ScenarioRevisions;

	internal static void Validate(
		CompatibilityEvidence evidence
	) {
		ArgumentNullException.ThrowIfNull( evidence );
		if ( !string.Equals( "1", evidence.SchemaVersion, StringComparison.Ordinal ) ) {
			throw new FormatException( "The evidence schema version is not supported." );
		}

		ValidateText( evidence.TerminalPackageVersion, nameof( evidence.TerminalPackageVersion ), IdentityLimit );
		ValidateCommit( evidence.SourceCommit );
		ValidateText( evidence.TerminalId, nameof( evidence.TerminalId ), IdentityLimit );
		if ( !TerminalIds.Contains( evidence.TerminalId ) ) {
			throw new FormatException( "The terminal lane identifier is not recognized." );
		}
		ValidateText( evidence.TerminalVersion, nameof( evidence.TerminalVersion ), IdentityLimit );
		ValidateText( evidence.OperatingSystem, nameof( evidence.OperatingSystem ), IdentityLimit );
		ValidateText( evidence.OperatingSystemVersion, nameof( evidence.OperatingSystemVersion ), IdentityLimit );

		bool hasTransport = !string.IsNullOrWhiteSpace( evidence.Transport );
		bool hasTransportVersion = !string.IsNullOrWhiteSpace( evidence.TransportVersion );
		if ( hasTransport != hasTransportVersion ) {
			throw new FormatException( "Transport name and version must be supplied together." );
		}
		if ( hasTransport ) {
			ValidateText( evidence.Transport!, nameof( evidence.Transport ), IdentityLimit );
			ValidateText( evidence.TransportVersion!, nameof( evidence.TransportVersion ), IdentityLimit );
		}

		ValidateText( evidence.ScenarioId, nameof( evidence.ScenarioId ), IdentityLimit );
		if ( !ScenarioRevisions.TryGetValue( evidence.ScenarioId, out int revision )
			|| revision != evidence.ScenarioRevision ) {
			throw new FormatException( "The scenario identifier or revision is not recognized." );
		}
		if ( TimeSpan.Zero != evidence.ObservedAtUtc.Offset ) {
			throw new FormatException( "The observation timestamp must use UTC." );
		}
		if ( !Enum.IsDefined( evidence.Outcome ) ) {
			throw new FormatException( "The evidence outcome is not recognized." );
		}
		ValidateText( evidence.Note, nameof( evidence.Note ), NoteLimit, allowEmpty: true );
	}

	private static void ValidateCommit(
		string sourceCommit
	) {
		ValidateText( sourceCommit, nameof( CompatibilityEvidence.SourceCommit ), 40 );
		if ( 40 != sourceCommit.Length || sourceCommit.Any( static value => !Uri.IsHexDigit( value ) ) ) {
			throw new FormatException( "The source commit must be one complete hexadecimal Git object name." );
		}
	}

	private static void ValidateText(
		string value,
		string name,
		int limit,
		bool allowEmpty = false
	) {
		if ( null == value ) {
			throw new FormatException( $"{name} is required." );
		}
		if ( !allowEmpty && string.IsNullOrWhiteSpace( value ) ) {
			throw new FormatException( $"{name} is required." );
		}
		if ( value.Length > limit ) {
			throw new FormatException( $"{name} exceeds its {limit}-character bound." );
		}
		if ( value.Any( static character =>
			character < ' '
			|| character is '\u007f'
			|| character is >= '\u0080' and <= '\u009f'
		) ) {
			throw new FormatException( $"{name} contains a control character." );
		}
	}
}
