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

/// <summary>
/// Identifies caller intent independently from a concrete terminal protocol backend.
/// </summary>
internal enum TerminalSemanticOperation {
	TerminalTitle,
	DesktopNotification,
	CurrentLocation,
	ShellCurrentDirectoryMetadata,
	Hyperlink,
	ClipboardWrite,
	ClipboardRead,
	CursorStyle,
	CursorStyleObservation,
	SynchronizedOutput,
	TerminalProgress,
	PointerShape,
	PaletteColor,
	DynamicColor,
	SemanticPromptLifecycle,
	ShellIntegrationMetadata,
	KeyboardReporting,
	MouseReporting,
	FocusReporting,
	BracketedPaste,
	RasterGraphics
}

/// <summary>
/// Identifies one concrete implementation candidate for a semantic terminal operation.
/// </summary>
internal enum TerminalProtocolBackend {
	TermInfoCapability,
	Osc0Title,
	Osc1IconName,
	Osc2WindowTitle,
	Osc4Palette,
	Osc7CurrentLocation,
	Osc8Hyperlink,
	Osc9Notification,
	Osc9Progress,
	Osc9WindowsCurrentDirectory,
	Osc22PointerShape,
	Osc52Clipboard,
	Osc99KittyNotification,
	Osc133SemanticPrompt,
	Osc633VsCodeShellIntegration,
	Osc777TitledNotification,
	Osc1337ITerm2ShellIntegration,
	CsiPrimaryDeviceAttributes,
	CsiSecondaryDeviceAttributes,
	CsiDeviceStatusReport,
	CsiCursorPositionReport,
	CsiDecPrivateMode,
	CsiDecscusrCursorStyle,
	CsiKittyKeyboard,
	CsiXtermModifyOtherKeys,
	DcsDecrqss,
	DcsXtgetTcap,
	DcsSixel,
	ApcKittyGraphics
}

/// <summary>
/// Identifies the outer ECMA-48/terminal framing family for a control sequence or string.
/// </summary>
internal enum TerminalControlFamily {
	Csi,
	Dcs,
	Osc,
	Apc,
	Pm,
	Sos
}

/// <summary>
/// Identifies the effective support state for one semantic operation or protocol backend.
/// </summary>
internal enum TerminalCapabilitySupportState {
	Unavailable,
	Unsupported,
	Unknown,
	Advertised,
	Verified
}

/// <summary>
/// Identifies the source which supplied capability evidence.
/// </summary>
internal enum TerminalCapabilityEvidenceSource {
	TermInfo,
	BuiltInProfile,
	LiveProbe,
	ProtocolResponse
}

/// <summary>
/// Provides N150-frozen classification for protocol backends without performing routing.
/// </summary>
internal static class TerminalControlLanguageVocabulary {
	/// <summary>
	/// Gets the control family for a backend, or <see langword="null"/> when the backend is an
	/// already-resolved TermInfo capability rather than a raw control-family implementation.
	/// </summary>
	internal static TerminalControlFamily? GetControlFamily(
		TerminalProtocolBackend backend
	) {
		if ( !Enum.IsDefined( backend ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( backend ),
				backend,
				"The terminal protocol backend is not recognized."
			);
		}

		return backend switch {
			TerminalProtocolBackend.TermInfoCapability => null,

			TerminalProtocolBackend.Osc0Title
				or TerminalProtocolBackend.Osc1IconName
				or TerminalProtocolBackend.Osc2WindowTitle
				or TerminalProtocolBackend.Osc4Palette
				or TerminalProtocolBackend.Osc7CurrentLocation
				or TerminalProtocolBackend.Osc8Hyperlink
				or TerminalProtocolBackend.Osc9Notification
				or TerminalProtocolBackend.Osc9Progress
				or TerminalProtocolBackend.Osc9WindowsCurrentDirectory
				or TerminalProtocolBackend.Osc22PointerShape
				or TerminalProtocolBackend.Osc52Clipboard
				or TerminalProtocolBackend.Osc99KittyNotification
				or TerminalProtocolBackend.Osc133SemanticPrompt
				or TerminalProtocolBackend.Osc633VsCodeShellIntegration
				or TerminalProtocolBackend.Osc777TitledNotification
				or TerminalProtocolBackend.Osc1337ITerm2ShellIntegration
					=> TerminalControlFamily.Osc,

			TerminalProtocolBackend.CsiPrimaryDeviceAttributes
				or TerminalProtocolBackend.CsiSecondaryDeviceAttributes
				or TerminalProtocolBackend.CsiDeviceStatusReport
				or TerminalProtocolBackend.CsiCursorPositionReport
				or TerminalProtocolBackend.CsiDecPrivateMode
				or TerminalProtocolBackend.CsiDecscusrCursorStyle
				or TerminalProtocolBackend.CsiKittyKeyboard
				or TerminalProtocolBackend.CsiXtermModifyOtherKeys
					=> TerminalControlFamily.Csi,

			TerminalProtocolBackend.DcsDecrqss
				or TerminalProtocolBackend.DcsXtgetTcap
				or TerminalProtocolBackend.DcsSixel
					=> TerminalControlFamily.Dcs,

			TerminalProtocolBackend.ApcKittyGraphics
				=> TerminalControlFamily.Apc,

			_ => throw new InvalidOperationException(
				"The terminal protocol backend does not have an N150 control-family classification."
			)
		};
	}
}
