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
/// Identifies why the N157 resolver selected one semantic backend.
/// </summary>
internal enum TerminalBackendSelectionReason {
	None,
	Explicit,
	Verified,
	TermInfo,
	Advertised,
	SafeFallback
}

/// <summary>
/// Describes the deterministic result of resolving one semantic terminal operation.
/// </summary>
internal readonly record struct TerminalSemanticBackendResolution {
	internal TerminalSemanticBackendResolution(
		TerminalSemanticOperation operation,
		TerminalSemanticBackendCandidate? selectedCandidate,
		TerminalCapabilitySupportState state,
		TerminalCapabilityEvidenceSource? evidenceSource,
		TerminalBackendSelectionReason selectionReason
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}
		if ( !Enum.IsDefined( state ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( state ),
				state,
				"The terminal capability support state is not recognized."
			);
		}
		if ( evidenceSource.HasValue && !Enum.IsDefined( evidenceSource.Value ) ) {
			throw new ArgumentOutOfRangeException( nameof( evidenceSource ) );
		}
		if ( !Enum.IsDefined( selectionReason ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( selectionReason ),
				selectionReason,
				"The terminal backend selection reason is not recognized."
			);
		}
		if ( selectedCandidate.HasValue ) {
			if ( selectedCandidate.Value.Operation != operation ) {
				throw new ArgumentException(
					"The selected backend candidate does not belong to the resolved semantic operation.",
					nameof( selectedCandidate )
				);
			}
			if ( TerminalBackendSelectionReason.None == selectionReason ) {
				throw new ArgumentException(
					"A selected backend must identify why it was selected.",
					nameof( selectionReason )
				);
			}
		} else if ( TerminalBackendSelectionReason.None != selectionReason ) {
			throw new ArgumentException(
				"An unresolved semantic operation cannot identify a backend selection reason.",
				nameof( selectionReason )
			);
		}

		this.Operation = operation;
		this.SelectedCandidate = selectedCandidate;
		this.State = state;
		this.EvidenceSource = evidenceSource;
		this.SelectionReason = selectionReason;
	}

	internal TerminalSemanticOperation Operation {
		get;
	}

	internal TerminalSemanticBackendCandidate? SelectedCandidate {
		get;
	}

	internal TerminalCapabilitySupportState State {
		get;
	}

	internal TerminalCapabilityEvidenceSource? EvidenceSource {
		get;
	}

	internal TerminalBackendSelectionReason SelectionReason {
		get;
	}
}

/// <summary>
/// Resolves semantic intent to one reviewed backend without conflating registry membership,
/// capability evidence, endpoint availability, or caller preference.
/// </summary>
internal static class TerminalSemanticBackendResolver {
	private static readonly ReadOnlyDictionary<
		TerminalSemanticOperation,
		IReadOnlyList<BackendPolicy>
	> Policies = CreatePolicies();

	internal static TerminalSemanticBackendResolution Resolve(
		TerminalSemanticOperation operation,
		TerminalCapabilityEvidenceLedger evidence,
		bool endpointAvailable = true,
		TerminalProtocolBackend? explicitBackend = null
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}
		ArgumentNullException.ThrowIfNull( evidence );
		if ( explicitBackend.HasValue && !Enum.IsDefined( explicitBackend.Value ) ) {
			throw new ArgumentOutOfRangeException( nameof( explicitBackend ) );
		}

		IReadOnlyList<TerminalSemanticBackendCandidate> candidates =
			TerminalSemanticBackendRegistry.GetCandidates( operation );
		if ( !endpointAvailable ) {
			return Unresolved(
				operation,
				TerminalCapabilitySupportState.Unavailable
			);
		}

		if ( explicitBackend.HasValue ) {
			TerminalSemanticBackendCandidate explicitCandidate = FindCandidate(
				operation,
				candidates,
				explicitBackend.Value
			);
			TerminalCapabilityResolution explicitEvidence = ResolveCandidateEvidence(
				explicitCandidate,
				evidence
			);
			if ( explicitEvidence.State is TerminalCapabilitySupportState.Unsupported
				or TerminalCapabilitySupportState.Unavailable ) {
				return new TerminalSemanticBackendResolution(
					operation,
					selectedCandidate: null,
					explicitEvidence.State,
					explicitEvidence.EvidenceSource,
					TerminalBackendSelectionReason.None
				);
			}

			return Selected(
				explicitCandidate,
				explicitEvidence,
				TerminalBackendSelectionReason.Explicit
			);
		}

		IReadOnlyList<BackendPolicy> policies = Policies[ operation ];
		CandidateEvidence[] evaluated = new CandidateEvidence[ candidates.Count ];
		for ( int index = 0; index < candidates.Count; ++index ) {
			TerminalSemanticBackendCandidate candidate = candidates[ index ];
			evaluated[ index ] = new CandidateEvidence(
				candidate,
				FindPolicy(
					policies,
					candidate.Backend
				),
				ResolveCandidateEvidence(
					candidate,
					evidence
				)
			);
		}

		CandidateEvidence? verified = Best(
			evaluated,
			static candidate => TerminalCapabilitySupportState.Verified == candidate.Evidence.State
		);
		if ( verified.HasValue ) {
			return Selected(
				verified.Value.Candidate,
				verified.Value.Evidence,
				TerminalBackendSelectionReason.Verified
			);
		}

		CandidateEvidence? termInfo = Best(
			evaluated,
			static candidate => TerminalProtocolBackend.TermInfoCapability == candidate.Candidate.Backend
				&& TerminalCapabilitySupportState.Advertised == candidate.Evidence.State
				&& TerminalCapabilityEvidenceSource.TermInfo == candidate.Evidence.EvidenceSource
		);
		if ( termInfo.HasValue ) {
			return Selected(
				termInfo.Value.Candidate,
				termInfo.Value.Evidence,
				TerminalBackendSelectionReason.TermInfo
			);
		}

		CandidateEvidence? advertised = Best(
			evaluated,
			static candidate => TerminalCapabilitySupportState.Advertised == candidate.Evidence.State
		);
		if ( advertised.HasValue ) {
			return Selected(
				advertised.Value.Candidate,
				advertised.Value.Evidence,
				TerminalBackendSelectionReason.Advertised
			);
		}

		CandidateEvidence? fallback = Best(
			evaluated,
			static candidate => candidate.Policy.SafeFallback
				&& TerminalCapabilitySupportState.Unknown == candidate.Evidence.State
		);
		if ( fallback.HasValue ) {
			return Selected(
				fallback.Value.Candidate,
				fallback.Value.Evidence,
				TerminalBackendSelectionReason.SafeFallback
			);
		}

		bool allUnsupported = evaluated.All(
			static candidate => TerminalCapabilitySupportState.Unsupported == candidate.Evidence.State
		);
		return Unresolved(
			operation,
			allUnsupported
				? TerminalCapabilitySupportState.Unsupported
				: TerminalCapabilitySupportState.Unknown
		);
	}

	private static TerminalCapabilityResolution ResolveCandidateEvidence(
		TerminalSemanticBackendCandidate candidate,
		TerminalCapabilityEvidenceLedger evidence
	) {
		if ( TerminalProtocolBackend.TermInfoCapability == candidate.Backend ) {
			TerminalCapabilityResolution operationEvidence = evidence.Resolve(
				TerminalCapabilitySubject.ForSemanticOperation( candidate.Operation )
			);
			if ( TerminalCapabilitySupportState.Advertised == operationEvidence.State
				&& TerminalCapabilityEvidenceSource.TermInfo == operationEvidence.EvidenceSource ) {
				return new TerminalCapabilityResolution(
					TerminalCapabilitySubject.ForProtocolBackend( candidate.Backend ),
					operationEvidence.State,
					operationEvidence.EvidenceSource
				);
			}

			return new TerminalCapabilityResolution(
				TerminalCapabilitySubject.ForProtocolBackend( candidate.Backend ),
				TerminalCapabilitySupportState.Unknown,
				evidenceSource: null
			);
		}

		return evidence.Resolve(
			TerminalCapabilitySubject.ForProtocolBackend( candidate.Backend )
		);
	}

	private static TerminalSemanticBackendResolution Selected(
		TerminalSemanticBackendCandidate candidate,
		TerminalCapabilityResolution evidence,
		TerminalBackendSelectionReason reason
	) {
		return new TerminalSemanticBackendResolution(
			candidate.Operation,
			candidate,
			evidence.State,
			evidence.EvidenceSource,
			reason
		);
	}

	private static TerminalSemanticBackendResolution Unresolved(
		TerminalSemanticOperation operation,
		TerminalCapabilitySupportState state
	) {
		return new TerminalSemanticBackendResolution(
			operation,
			selectedCandidate: null,
			state,
			evidenceSource: null,
			TerminalBackendSelectionReason.None
		);
	}

	private static TerminalSemanticBackendCandidate FindCandidate(
		TerminalSemanticOperation operation,
		IReadOnlyList<TerminalSemanticBackendCandidate> candidates,
		TerminalProtocolBackend backend
	) {
		for ( int index = 0; index < candidates.Count; ++index ) {
			if ( backend == candidates[ index ].Backend ) {
				return candidates[ index ];
			}
		}

		throw new ArgumentException(
			$"Protocol backend '{backend}' is not a reviewed candidate for semantic operation '{operation}'.",
			nameof( backend )
		);
	}

	private static BackendPolicy FindPolicy(
		IReadOnlyList<BackendPolicy> policies,
		TerminalProtocolBackend backend
	) {
		for ( int index = 0; index < policies.Count; ++index ) {
			if ( backend == policies[ index ].Backend ) {
				return policies[ index ];
			}
		}

		throw new InvalidOperationException(
			$"Protocol backend '{backend}' does not have an N157 routing policy entry."
		);
	}

	private static CandidateEvidence? Best(
		IReadOnlyList<CandidateEvidence> candidates,
		Func<CandidateEvidence, bool> predicate
	) {
		ArgumentNullException.ThrowIfNull( predicate );
		CandidateEvidence? best = null;
		for ( int index = 0; index < candidates.Count; ++index ) {
			CandidateEvidence candidate = candidates[ index ];
			if ( !predicate( candidate ) ) {
				continue;
			}
			if ( !best.HasValue || candidate.Policy.Preference < best.Value.Policy.Preference ) {
				best = candidate;
			}
		}
		return best;
	}

	private static ReadOnlyDictionary<
		TerminalSemanticOperation,
		IReadOnlyList<BackendPolicy>
	> CreatePolicies() {
		Dictionary<
			TerminalSemanticOperation,
			IReadOnlyList<BackendPolicy>
		> policies = new();

		AddPolicy(
			policies,
			TerminalSemanticOperation.TerminalTitle,
			( TerminalProtocolBackend.Osc2WindowTitle, true ),
			( TerminalProtocolBackend.Osc0Title, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.DesktopNotification,
			( TerminalProtocolBackend.Osc99KittyNotification, false ),
			( TerminalProtocolBackend.Osc777TitledNotification, false ),
			( TerminalProtocolBackend.Osc9Notification, true )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.CurrentLocation,
			( TerminalProtocolBackend.Osc7CurrentLocation, true ),
			( TerminalProtocolBackend.Osc9WindowsCurrentDirectory, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.ShellCurrentDirectoryMetadata,
			( TerminalProtocolBackend.Osc633VsCodeShellIntegration, false ),
			( TerminalProtocolBackend.Osc1337ITerm2ShellIntegration, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.Hyperlink,
			( TerminalProtocolBackend.Osc8Hyperlink, true )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.ClipboardWrite,
			( TerminalProtocolBackend.TermInfoCapability, false ),
			( TerminalProtocolBackend.Osc52Clipboard, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.ClipboardRead,
			( TerminalProtocolBackend.Osc52Clipboard, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.CursorStyle,
			( TerminalProtocolBackend.TermInfoCapability, false ),
			( TerminalProtocolBackend.CsiDecscusrCursorStyle, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.CursorStyleObservation,
			( TerminalProtocolBackend.DcsDecrqss, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.SynchronizedOutput,
			( TerminalProtocolBackend.CsiSynchronizedOutput, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.TerminalProgress,
			( TerminalProtocolBackend.Osc9Progress, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.PointerShape,
			( TerminalProtocolBackend.Osc22PointerShape, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.PaletteColor,
			( TerminalProtocolBackend.TermInfoCapability, false ),
			( TerminalProtocolBackend.Osc4Palette, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.DynamicColor,
			( TerminalProtocolBackend.OscDynamicColor, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.SemanticPromptLifecycle,
			( TerminalProtocolBackend.Osc133SemanticPrompt, true ),
			( TerminalProtocolBackend.Osc633VsCodeShellIntegration, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.ShellIntegrationMetadata,
			( TerminalProtocolBackend.Osc633VsCodeShellIntegration, false ),
			( TerminalProtocolBackend.Osc1337ITerm2ShellIntegration, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.KeyboardReporting,
			( TerminalProtocolBackend.CsiKittyKeyboard, false ),
			( TerminalProtocolBackend.CsiXtermModifyOtherKeys, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.MouseReporting,
			( TerminalProtocolBackend.CsiMouseReporting, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.FocusReporting,
			( TerminalProtocolBackend.CsiFocusReporting, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.BracketedPaste,
			( TerminalProtocolBackend.CsiBracketedPaste, false )
		);
		AddPolicy(
			policies,
			TerminalSemanticOperation.RasterGraphics,
			( TerminalProtocolBackend.ApcKittyGraphics, false ),
			( TerminalProtocolBackend.DcsSixel, false )
		);

		ValidatePolicies( policies );
		return new ReadOnlyDictionary<
			TerminalSemanticOperation,
			IReadOnlyList<BackendPolicy>
		>( policies );
	}

	private static void AddPolicy(
		Dictionary<TerminalSemanticOperation, IReadOnlyList<BackendPolicy>> policies,
		TerminalSemanticOperation operation,
		params ( TerminalProtocolBackend Backend, bool SafeFallback )[] candidates
	) {
		ArgumentNullException.ThrowIfNull( policies );
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException( nameof( operation ) );
		}
		ArgumentNullException.ThrowIfNull( candidates );
		if ( 0 == candidates.Length ) {
			throw new ArgumentException(
				"A routing policy must contain at least one backend candidate.",
				nameof( candidates )
			);
		}

		BackendPolicy[] entries = new BackendPolicy[ candidates.Length ];
		for ( int index = 0; index < candidates.Length; ++index ) {
			entries[ index ] = new BackendPolicy(
				candidates[ index ].Backend,
				index,
				candidates[ index ].SafeFallback
			);
		}
		policies.Add(
			operation,
			Array.AsReadOnly( entries )
		);
	}

	private static void ValidatePolicies(
		IReadOnlyDictionary<TerminalSemanticOperation, IReadOnlyList<BackendPolicy>> policies
	) {
		ArgumentNullException.ThrowIfNull( policies );
		TerminalSemanticOperation[] operations = Enum.GetValues<TerminalSemanticOperation>();
		if ( operations.Length != policies.Count ) {
			throw new InvalidOperationException(
				"Every semantic operation must have one explicit N157 routing policy."
			);
		}

		foreach ( TerminalSemanticOperation operation in operations ) {
			IReadOnlyList<TerminalSemanticBackendCandidate> registered =
				TerminalSemanticBackendRegistry.GetCandidates( operation );
			if ( !policies.TryGetValue(
				operation,
				out IReadOnlyList<BackendPolicy>? policy
			) ) {
				throw new InvalidOperationException(
					$"Semantic operation '{operation}' does not have an N157 routing policy."
				);
			}
			if ( registered.Count != policy.Count ) {
				throw new InvalidOperationException(
					$"Semantic operation '{operation}' routing policy does not cover its complete N156 registry."
				);
			}

			HashSet<TerminalProtocolBackend> seen = [];
			foreach ( BackendPolicy entry in policy ) {
				if ( !seen.Add( entry.Backend ) ) {
					throw new InvalidOperationException(
						$"Semantic operation '{operation}' routing policy contains duplicate backend '{entry.Backend}'."
					);
				}
			}
			foreach ( TerminalSemanticBackendCandidate candidate in registered ) {
				if ( !seen.Contains( candidate.Backend ) ) {
					throw new InvalidOperationException(
						$"Semantic operation '{operation}' routing policy omits backend '{candidate.Backend}'."
					);
				}
			}
		}
	}

	private readonly record struct BackendPolicy(
		TerminalProtocolBackend Backend,
		int Preference,
		bool SafeFallback
	);

	private readonly record struct CandidateEvidence(
		TerminalSemanticBackendCandidate Candidate,
		BackendPolicy Policy,
		TerminalCapabilityResolution Evidence
	);
}
