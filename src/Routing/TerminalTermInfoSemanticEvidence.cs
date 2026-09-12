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

	private static readonly ExactSemanticContract[] ExactSemanticContracts = [
		new(
			TerminalSemanticOperation.ClipboardWrite,
			HasClipboardWrite
		),
		new(
			TerminalSemanticOperation.CursorStyle,
			HasCursorStyle
		),
		new(
			TerminalSemanticOperation.PaletteColor,
			HasPaletteColor
		)
	];

	private static readonly BackendAdvertisementContract[] BackendAdvertisementContracts = [
		new(
			TerminalProtocolBackend.CsiFocusReporting,
			HasFocusReporting
		),
		new(
			TerminalProtocolBackend.CsiBracketedPaste,
			HasBracketedPaste
		),
		new(
			TerminalProtocolBackend.CsiMouseReporting,
			HasAdvertisedMouseProtocol
		)
	];

	/// <summary>
	/// Seeds static TermInfo evidence for exact semantic equivalents and reviewed
	/// metadata-backed protocol implementations selected by N158.
	/// </summary>
	internal static void Seed(
		TerminalDescription terminal,
		TerminalCapabilityEvidenceLedger evidence
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( evidence );

		foreach ( ExactSemanticContract contract in ExactSemanticContracts ) {
			if ( contract.IsAdvertised( terminal ) ) {
				AdvertiseSemantic(
					evidence,
					contract.Operation
				);
			}
		}

		foreach ( BackendAdvertisementContract contract in BackendAdvertisementContracts ) {
			if ( contract.IsAdvertised( terminal ) ) {
				AdvertiseBackend(
					evidence,
					contract.Backend
				);
			}
		}
	}

	/// <summary>
	/// Gets whether the selected terminal contains an exact TermInfo implementation
	/// for the reviewed semantic operation through the generic TermInfo backend.
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

		foreach ( ExactSemanticContract contract in ExactSemanticContracts ) {
			if ( contract.Operation == operation ) {
				return contract.IsAdvertised( terminal );
			}
		}
		return false;
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

	private static bool HasClipboardWrite(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		return HasExtendedString(
			terminal,
			ClipboardWriteCapability
		);
	}

	private static bool HasCursorStyle(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		return HasExtendedString(
			terminal,
			CursorStyleCapability
		);
	}

	private static bool HasPaletteColor(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		return terminal.GetBoolean( BooleanCapability.CanChangeColor )
			&& !string.IsNullOrEmpty(
				terminal.GetString( StringCapability.InitializeColor )
			);
	}

	private static bool HasFocusReporting(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		return HasExtendedStringContract(
			terminal,
			FocusEnableCapability,
			FocusDisableCapability,
			FocusInCapability,
			FocusOutCapability
		);
	}

	private static bool HasBracketedPaste(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		return HasExtendedStringContract(
			terminal,
			BracketedPasteEnableCapability,
			BracketedPasteDisableCapability,
			BracketedPasteStartCapability,
			BracketedPasteEndCapability
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

	private readonly record struct ExactSemanticContract(
		TerminalSemanticOperation Operation,
		Func<TerminalDescription, bool> IsAdvertised
	);

	private readonly record struct BackendAdvertisementContract(
		TerminalProtocolBackend Backend,
		Func<TerminalDescription, bool> IsAdvertised
	);
}
