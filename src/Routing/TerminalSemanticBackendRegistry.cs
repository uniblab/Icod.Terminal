/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

using System.Collections.ObjectModel;

/// <summary>
/// Describes one reviewed implementation candidate for a semantic terminal operation.
/// </summary>
internal readonly record struct TerminalSemanticBackendCandidate {
	internal TerminalSemanticBackendCandidate(
		TerminalSemanticOperation operation,
		TerminalProtocolBackend backend
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}
		if ( !Enum.IsDefined( backend ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( backend ),
				backend,
				"The terminal protocol backend is not recognized."
			);
		}

		this.Operation = operation;
		this.Backend = backend;
		this.ControlFamily = TerminalControlLanguageVocabulary.GetControlFamily(
			backend
		);
	}

	internal TerminalSemanticOperation Operation {
		get;
	}

	internal TerminalProtocolBackend Backend {
		get;
	}

	internal TerminalControlFamily? ControlFamily {
		get;
	}
}

/// <summary>
/// Maps every normalized semantic operation to its reviewed implementation candidates.
/// </summary>
/// <remarks>
/// Registry declaration order is deterministic but is not a routing preference. N157 owns
/// support/evidence resolution and backend preference policy.
/// </remarks>
internal static class TerminalSemanticBackendRegistry {
	private static readonly ReadOnlyDictionary<
		TerminalSemanticOperation,
		IReadOnlyList<TerminalSemanticBackendCandidate>
	> Registry = CreateRegistry();

	private static readonly IReadOnlyList<TerminalSemanticOperation> RegisteredOperations =
		Array.AsReadOnly(
			Enum.GetValues<TerminalSemanticOperation>()
		);

	internal static IReadOnlyList<TerminalSemanticOperation> Operations {
		get {
			return RegisteredOperations;
		}
	}

	internal static IReadOnlyList<TerminalSemanticBackendCandidate> GetCandidates(
		TerminalSemanticOperation operation
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}

		return Registry[ operation ];
	}

	private static ReadOnlyDictionary<
		TerminalSemanticOperation,
		IReadOnlyList<TerminalSemanticBackendCandidate>
	> CreateRegistry() {
		Dictionary<
			TerminalSemanticOperation,
			IReadOnlyList<TerminalSemanticBackendCandidate>
		> registry = new() {
			[ TerminalSemanticOperation.TerminalTitle ] = CreateCandidates(
				TerminalSemanticOperation.TerminalTitle,
				TerminalProtocolBackend.Osc0Title,
				TerminalProtocolBackend.Osc2WindowTitle
			),
			[ TerminalSemanticOperation.DesktopNotification ] = CreateCandidates(
				TerminalSemanticOperation.DesktopNotification,
				TerminalProtocolBackend.Osc9Notification,
				TerminalProtocolBackend.Osc777TitledNotification,
				TerminalProtocolBackend.Osc99KittyNotification
			),
			[ TerminalSemanticOperation.CurrentLocation ] = CreateCandidates(
				TerminalSemanticOperation.CurrentLocation,
				TerminalProtocolBackend.Osc7CurrentLocation,
				TerminalProtocolBackend.Osc9WindowsCurrentDirectory
			),
			[ TerminalSemanticOperation.ShellCurrentDirectoryMetadata ] = CreateCandidates(
				TerminalSemanticOperation.ShellCurrentDirectoryMetadata,
				TerminalProtocolBackend.Osc633VsCodeShellIntegration,
				TerminalProtocolBackend.Osc1337ITerm2ShellIntegration
			),
			[ TerminalSemanticOperation.Hyperlink ] = CreateCandidates(
				TerminalSemanticOperation.Hyperlink,
				TerminalProtocolBackend.Osc8Hyperlink
			),
			[ TerminalSemanticOperation.ClipboardWrite ] = CreateCandidates(
				TerminalSemanticOperation.ClipboardWrite,
				TerminalProtocolBackend.TermInfoCapability,
				TerminalProtocolBackend.Osc52Clipboard
			),
			[ TerminalSemanticOperation.ClipboardRead ] = CreateCandidates(
				TerminalSemanticOperation.ClipboardRead,
				TerminalProtocolBackend.Osc52Clipboard
			),
			[ TerminalSemanticOperation.CursorStyle ] = CreateCandidates(
				TerminalSemanticOperation.CursorStyle,
				TerminalProtocolBackend.TermInfoCapability,
				TerminalProtocolBackend.CsiDecscusrCursorStyle
			),
			[ TerminalSemanticOperation.CursorStyleObservation ] = CreateCandidates(
				TerminalSemanticOperation.CursorStyleObservation,
				TerminalProtocolBackend.DcsDecrqss
			),
			[ TerminalSemanticOperation.SynchronizedOutput ] = CreateCandidates(
				TerminalSemanticOperation.SynchronizedOutput,
				TerminalProtocolBackend.CsiSynchronizedOutput
			),
			[ TerminalSemanticOperation.TerminalProgress ] = CreateCandidates(
				TerminalSemanticOperation.TerminalProgress,
				TerminalProtocolBackend.Osc9Progress
			),
			[ TerminalSemanticOperation.PointerShape ] = CreateCandidates(
				TerminalSemanticOperation.PointerShape,
				TerminalProtocolBackend.Osc22PointerShape
			),
			[ TerminalSemanticOperation.PaletteColor ] = CreateCandidates(
				TerminalSemanticOperation.PaletteColor,
				TerminalProtocolBackend.TermInfoCapability,
				TerminalProtocolBackend.Osc4Palette
			),
			[ TerminalSemanticOperation.DynamicColor ] = CreateCandidates(
				TerminalSemanticOperation.DynamicColor,
				TerminalProtocolBackend.OscDynamicColor
			),
			[ TerminalSemanticOperation.SemanticPromptLifecycle ] = CreateCandidates(
				TerminalSemanticOperation.SemanticPromptLifecycle,
				TerminalProtocolBackend.Osc133SemanticPrompt,
				TerminalProtocolBackend.Osc633VsCodeShellIntegration
			),
			[ TerminalSemanticOperation.ShellIntegrationMetadata ] = CreateCandidates(
				TerminalSemanticOperation.ShellIntegrationMetadata,
				TerminalProtocolBackend.Osc633VsCodeShellIntegration,
				TerminalProtocolBackend.Osc1337ITerm2ShellIntegration
			),
			[ TerminalSemanticOperation.KeyboardReporting ] = CreateCandidates(
				TerminalSemanticOperation.KeyboardReporting,
				TerminalProtocolBackend.CsiKittyKeyboard,
				TerminalProtocolBackend.CsiXtermModifyOtherKeys
			),
			[ TerminalSemanticOperation.MouseReporting ] = CreateCandidates(
				TerminalSemanticOperation.MouseReporting,
				TerminalProtocolBackend.CsiMouseReporting
			),
			[ TerminalSemanticOperation.FocusReporting ] = CreateCandidates(
				TerminalSemanticOperation.FocusReporting,
				TerminalProtocolBackend.CsiFocusReporting
			),
			[ TerminalSemanticOperation.BracketedPaste ] = CreateCandidates(
				TerminalSemanticOperation.BracketedPaste,
				TerminalProtocolBackend.CsiBracketedPaste
			),
			[ TerminalSemanticOperation.RasterGraphics ] = CreateCandidates(
				TerminalSemanticOperation.RasterGraphics,
				TerminalProtocolBackend.DcsSixel,
				TerminalProtocolBackend.ApcKittyGraphics
			),
			[ TerminalSemanticOperation.PersistentRasterGraphics ] = CreateCandidates(
				TerminalSemanticOperation.PersistentRasterGraphics,
				TerminalProtocolBackend.ApcKittyGraphics
			)
		};

		TerminalSemanticOperation[] operations = Enum.GetValues<TerminalSemanticOperation>();
		if ( operations.Length != registry.Count ) {
			throw new InvalidOperationException(
				"Every terminal semantic operation must have an explicit N156 backend registry entry."
			);
		}
		foreach ( TerminalSemanticOperation operation in operations ) {
			if ( !registry.ContainsKey( operation ) ) {
				throw new InvalidOperationException(
					$"Terminal semantic operation '{operation}' does not have an N156 backend registry entry."
				);
			}
		}

		return new ReadOnlyDictionary<
			TerminalSemanticOperation,
			IReadOnlyList<TerminalSemanticBackendCandidate>
		>( registry );
	}

	private static IReadOnlyList<TerminalSemanticBackendCandidate> CreateCandidates(
		TerminalSemanticOperation operation,
		params TerminalProtocolBackend[] backends
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}
		ArgumentNullException.ThrowIfNull( backends );
		if ( 0 == backends.Length ) {
			throw new ArgumentException(
				"A terminal semantic operation must have at least one implementation candidate.",
				nameof( backends )
			);
		}

		HashSet<TerminalProtocolBackend> seen = [];
		TerminalSemanticBackendCandidate[] candidates =
			new TerminalSemanticBackendCandidate[ backends.Length ];
		for ( int index = 0; index < backends.Length; ++index ) {
			TerminalProtocolBackend backend = backends[ index ];
			if ( !seen.Add( backend ) ) {
				throw new InvalidOperationException(
					$"Terminal semantic operation '{operation}' contains duplicate backend '{backend}'."
				);
			}

			candidates[ index ] = new TerminalSemanticBackendCandidate(
				operation,
				backend
			);
		}

		return Array.AsReadOnly( candidates );
	}
}
