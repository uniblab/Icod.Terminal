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
/// Internal semantic-routing integration for the selected immutable TermInfo profile.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object semanticRoutingSync = new();
	private TerminalCapabilityEvidenceLedger? semanticCapabilityEvidence;

	internal TerminalSemanticBackendResolution ResolveSemanticBackend(
		TerminalSemanticOperation operation,
		TerminalProtocolBackend? explicitBackend = null
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}
		if ( explicitBackend.HasValue && !Enum.IsDefined( explicitBackend.Value ) ) {
			throw new ArgumentOutOfRangeException( nameof( explicitBackend ) );
		}

		return TerminalSemanticBackendResolver.Resolve(
			operation,
			this.GetSemanticCapabilityEvidence(),
			this.HasSemanticEndpointAvailability( operation ),
			explicitBackend
		);
	}

	internal TerminalCapabilityEvidenceLedger GetSemanticCapabilityEvidence() {
		lock ( this.semanticRoutingSync ) {
			if ( this.semanticCapabilityEvidence is null ) {
				TerminalCapabilityEvidenceLedger evidence = new();
				TerminalTermInfoSemanticEvidence.Seed(
					this.Terminal,
					evidence
				);
				this.semanticCapabilityEvidence = evidence;
			}

			return this.semanticCapabilityEvidence;
		}
	}

	internal void RecordSemanticBackendEvidence(
		TerminalProtocolBackend backend,
		TerminalCapabilitySupportState state,
		TerminalCapabilityEvidenceSource source
	) {
		if ( !Enum.IsDefined( backend ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( backend ),
				backend,
				"The terminal protocol backend is not recognized."
			);
		}
		if ( !Enum.IsDefined( state ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( state ),
				state,
				"The terminal capability support state is not recognized."
			);
		}
		if ( !Enum.IsDefined( source ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( source ),
				source,
				"The terminal capability evidence source is not recognized."
			);
		}

		this.GetSemanticCapabilityEvidence().Record(
			TerminalCapabilitySubject.ForProtocolBackend( backend ),
			state,
			source
		);
	}

	internal void AdvanceSemanticLiveEvidenceGeneration() {
		lock ( this.semanticRoutingSync ) {
			this.semanticCapabilityEvidence?.AdvanceLiveGeneration();
		}
	}

	private bool HasSemanticEndpointAvailability(
		TerminalSemanticOperation operation
	) {
		return operation switch {
			TerminalSemanticOperation.ClipboardRead
				or TerminalSemanticOperation.CursorStyleObservation
				or TerminalSemanticOperation.KeyboardReporting
				or TerminalSemanticOperation.MouseReporting
				or TerminalSemanticOperation.FocusReporting
				or TerminalSemanticOperation.BracketedPaste
					=> this.IsInteractive,
			_ => this.OutputObservation.IsTerminal
		};
	}
}
