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
/// Public semantic capability inspection over the session's existing evidence and routing state.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Inspects one semantic terminal capability without emitting terminal bytes or performing a
	/// live capability probe.
	/// </summary>
	/// <param name="capability">The semantic capability to inspect.</param>
	/// <returns>The session's current side-effect-free planning status for the capability.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="capability"/> is not a recognized <see cref="TerminalCapability"/> value.
	/// </exception>
	public TerminalCapabilityStatus InspectCapability(
		TerminalCapability capability
	) {
		if ( !Enum.IsDefined( capability ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( capability ),
				capability,
				"The terminal capability is not recognized."
			);
		}

		TerminalSemanticOperation operation = MapCapability( capability );
		TerminalCapabilityEvidenceLedger evidence = this.GetSemanticCapabilityEvidence();
		bool endpointAvailable = this.HasSemanticEndpointAvailability( operation );
		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			operation,
			evidence,
			endpointAvailable: true
		);

		return new TerminalCapabilityStatus(
			capability,
			MapSupport( resolution.State ),
			endpointAvailable
				? TerminalCapabilityEndpointAvailability.Available
				: TerminalCapabilityEndpointAvailability.Unavailable,
			ResolveEvidenceKind(
				operation,
				resolution,
				evidence
			),
			endpointAvailable && resolution.SelectedCandidate.HasValue
		);
	}

	private static TerminalSemanticOperation MapCapability(
		TerminalCapability capability
	) {
		return capability switch {
			TerminalCapability.ClipboardRead => TerminalSemanticOperation.ClipboardRead,
			TerminalCapability.ClipboardWrite => TerminalSemanticOperation.ClipboardWrite,
			TerminalCapability.CursorStyle => TerminalSemanticOperation.CursorStyle,
			TerminalCapability.SynchronizedOutput => TerminalSemanticOperation.SynchronizedOutput,
			TerminalCapability.KeyboardReporting => TerminalSemanticOperation.KeyboardReporting,
			TerminalCapability.MouseReporting => TerminalSemanticOperation.MouseReporting,
			TerminalCapability.FocusReporting => TerminalSemanticOperation.FocusReporting,
			TerminalCapability.BracketedPaste => TerminalSemanticOperation.BracketedPaste,
			TerminalCapability.RasterGraphics => TerminalSemanticOperation.RasterGraphics,
			_ => throw new ArgumentOutOfRangeException(
				nameof( capability ),
				capability,
				"The terminal capability is not recognized."
			)
		};
	}

	private static TerminalCapabilitySupport MapSupport(
		TerminalCapabilitySupportState state
	) {
		return state switch {
			TerminalCapabilitySupportState.Unknown => TerminalCapabilitySupport.Unknown,
			TerminalCapabilitySupportState.Unsupported => TerminalCapabilitySupport.Unsupported,
			TerminalCapabilitySupportState.Advertised => TerminalCapabilitySupport.Advertised,
			TerminalCapabilitySupportState.Verified => TerminalCapabilitySupport.Verified,
			TerminalCapabilitySupportState.Unavailable => throw new InvalidOperationException(
				"Capability inspection resolves support independently from endpoint availability."
			),
			_ => throw new ArgumentOutOfRangeException(
				nameof( state ),
				state,
				"The terminal capability support state is not recognized."
			)
		};
	}

	private static TerminalCapabilityEvidenceKind ResolveEvidenceKind(
		TerminalSemanticOperation operation,
		TerminalSemanticBackendResolution resolution,
		TerminalCapabilityEvidenceLedger evidence
	) {
		if ( resolution.EvidenceSource.HasValue ) {
			return MapEvidenceKind( resolution.EvidenceSource.Value );
		}

		TerminalCapabilityResolution semanticEvidence = evidence.Resolve(
			TerminalCapabilitySubject.ForSemanticOperation( operation )
		);
		TerminalCapabilityEvidenceKind semanticKind = MapEvidenceKind(
			semanticEvidence.EvidenceSource
		);
		if ( TerminalCapabilityEvidenceKind.LiveObservation == semanticKind ) {
			return semanticKind;
		}

		TerminalCapabilityEvidenceKind fallbackKind = semanticKind;
		foreach ( TerminalSemanticBackendCandidate candidate in
			TerminalSemanticBackendRegistry.GetCandidates( operation ) ) {
			TerminalCapabilityResolution candidateEvidence = evidence.Resolve(
				TerminalCapabilitySubject.ForProtocolBackend( candidate.Backend )
			);
			TerminalCapabilityEvidenceKind candidateKind = MapEvidenceKind(
				candidateEvidence.EvidenceSource
			);
			if ( TerminalCapabilityEvidenceKind.LiveObservation == candidateKind ) {
				return candidateKind;
			}
			if ( TerminalCapabilityEvidenceKind.StaticDescription == candidateKind ) {
				fallbackKind = candidateKind;
			}
		}

		if ( TerminalCapabilitySupportState.Unsupported == resolution.State ) {
			return TerminalCapabilityEvidenceKind.LiveObservation;
		}

		return fallbackKind;
	}

	private static TerminalCapabilityEvidenceKind MapEvidenceKind(
		TerminalCapabilityEvidenceSource? source
	) {
		return source switch {
			null => TerminalCapabilityEvidenceKind.None,
			TerminalCapabilityEvidenceSource.TermInfo
				or TerminalCapabilityEvidenceSource.BuiltInProfile
					=> TerminalCapabilityEvidenceKind.StaticDescription,
			TerminalCapabilityEvidenceSource.LiveProbe
				or TerminalCapabilityEvidenceSource.ProtocolResponse
					=> TerminalCapabilityEvidenceKind.LiveObservation,
			_ => throw new ArgumentOutOfRangeException(
				nameof( source ),
				source,
				"The terminal capability evidence source is not recognized."
			)
		};
	}
}
