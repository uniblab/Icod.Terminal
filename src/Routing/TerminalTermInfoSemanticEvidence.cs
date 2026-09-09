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

using Icod.TermInfo;

/// <summary>
/// Reconciles selected TermInfo capabilities with the N155/N156 semantic model.
/// </summary>
internal static class TerminalTermInfoSemanticEvidence {
	private const string ClipboardWriteCapability = "Ms";
	private const string CursorStyleCapability = "Ss";
	private const string FocusEnableCapability = "fe";
	private const string FocusDisableCapability = "fd";
	private const string FocusInCapability = "kxIN";
	private const string FocusOutCapability = "kxOUT";
	private const string BracketedPasteEnableCapability = "BE";
	private const string BracketedPasteDisableCapability = "BD";
	private const string BracketedPasteStartCapability = "PS";
	private const string BracketedPasteEndCapability = "PE";
	private const string MouseModeCapability = "XM";
	private const string MouseFormatCapability = "xm";

	private const string SgrMousePrefix = "\u001b[<";
	private const string LegacyMousePrefix = "\u001b[M";

	/// <summary>
	/// Seeds static TermInfo evidence for exact semantic equivalents selected by N158.
	/// </summary>
	internal static void Seed(
		TerminalDescription terminal,
		TerminalCapabilityEvidenceLedger evidence
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( evidence );

		if ( HasExtendedString(
			terminal,
			ClipboardWriteCapability
		) ) {
			AdvertiseSemantic(
				evidence,
				TerminalSemanticOperation.ClipboardWrite
			);
		}

		if ( HasExtendedString(
			terminal,
			CursorStyleCapability
		) ) {
			AdvertiseSemantic(
				evidence,
				TerminalSemanticOperation.CursorStyle
			);
		}

		if ( terminal.GetBoolean( BooleanCapability.CanChangeColor )
			&& !string.IsNullOrEmpty(
				terminal.GetString( StringCapability.InitializeColor )
			) ) {
			AdvertiseSemantic(
				evidence,
				TerminalSemanticOperation.PaletteColor
			);
		}

		if ( HasExtendedStringContract(
			terminal,
			FocusEnableCapability,
			FocusDisableCapability,
			FocusInCapability,
			FocusOutCapability
		) ) {
			AdvertiseBackend(
				evidence,
				TerminalProtocolBackend.CsiFocusReporting
			);
		}

		if ( HasExtendedStringContract(
			terminal,
			BracketedPasteEnableCapability,
			BracketedPasteDisableCapability,
			BracketedPasteStartCapability,
			BracketedPasteEndCapability
		) ) {
			AdvertiseBackend(
				evidence,
				TerminalProtocolBackend.CsiBracketedPaste
			);
		}

		if ( HasAdvertisedMouseProtocol( terminal ) ) {
			AdvertiseBackend(
				evidence,
				TerminalProtocolBackend.CsiMouseReporting
			);
		}
	}

	/// <summary>
	/// Gets whether the selected terminal contains an exact TermInfo implementation
	/// for the reviewed semantic operation.
	/// </summary>
	internal static bool HasExactImplementation(
		TerminalDescription terminal,
		TerminalSemanticOperation operation
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}

		return operation switch {
			TerminalSemanticOperation.ClipboardWrite
				=> HasExtendedString(
					terminal,
					ClipboardWriteCapability
				),
			TerminalSemanticOperation.CursorStyle
				=> HasExtendedString(
					terminal,
					CursorStyleCapability
				),
			TerminalSemanticOperation.PaletteColor
				=> terminal.GetBoolean( BooleanCapability.CanChangeColor )
					&& !string.IsNullOrEmpty(
						terminal.GetString( StringCapability.InitializeColor )
					),
			TerminalSemanticOperation.FocusReporting
				=> HasExtendedStringContract(
					terminal,
					FocusEnableCapability,
					FocusDisableCapability,
					FocusInCapability,
					FocusOutCapability
				),
			TerminalSemanticOperation.BracketedPaste
				=> HasExtendedStringContract(
					terminal,
					BracketedPasteEnableCapability,
					BracketedPasteDisableCapability,
					BracketedPasteStartCapability,
					BracketedPasteEndCapability
				),
			_ => false
		};
	}

	/// <summary>
	/// Gets whether TermInfo advertises enough metadata for the existing CSI mouse backend.
	/// </summary>
	internal static bool HasAdvertisedMouseProtocol(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		if ( !HasExtendedStringContract(
			terminal,
			MouseModeCapability,
			MouseFormatCapability
		) ) {
			return false;
		}

		string? keyMouse = terminal.GetString( StringCapability.KeyMouse );
		if ( string.IsNullOrEmpty( keyMouse ) ) {
			return false;
		}

		return keyMouse.StartsWith(
			SgrMousePrefix,
			StringComparison.Ordinal
		) || keyMouse.StartsWith(
			LegacyMousePrefix,
			StringComparison.Ordinal
		);
	}

	private static void AdvertiseSemantic(
		TerminalCapabilityEvidenceLedger evidence,
		TerminalSemanticOperation operation
	) {
		ArgumentNullException.ThrowIfNull( evidence );

		evidence.Record(
			TerminalCapabilitySubject.ForSemanticOperation( operation ),
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.TermInfo
		);
	}

	private static void AdvertiseBackend(
		TerminalCapabilityEvidenceLedger evidence,
		TerminalProtocolBackend backend
	) {
		ArgumentNullException.ThrowIfNull( evidence );

		evidence.Record(
			TerminalCapabilitySubject.ForProtocolBackend( backend ),
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.TermInfo
		);
	}

	private static bool HasExtendedStringContract(
		TerminalDescription terminal,
		params string[] names
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( names );

		foreach ( string name in names ) {
			if ( !HasExtendedString(
				terminal,
				name
			) ) {
				return false;
			}
		}

		return true;
	}

	private static bool HasExtendedString(
		TerminalDescription terminal,
		string name
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentException.ThrowIfNullOrWhiteSpace( name );

		return terminal.TryGetExtendedString(
			name,
			out string? value
		) && !string.IsNullOrEmpty( value );
	}
}
