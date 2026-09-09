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
/// Identifies whether capability evidence describes a semantic operation or one concrete backend.
/// </summary>
internal enum TerminalCapabilitySubjectKind {
	SemanticOperation,
	ProtocolBackend
}

/// <summary>
/// Identifies one semantic operation or concrete protocol backend without conflating the two.
/// </summary>
internal readonly record struct TerminalCapabilitySubject {
	private TerminalCapabilitySubject(
		TerminalCapabilitySubjectKind kind,
		TerminalSemanticOperation? semanticOperation,
		TerminalProtocolBackend? protocolBackend
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"The terminal capability subject kind is not recognized."
			);
		}
		if ( TerminalCapabilitySubjectKind.SemanticOperation == kind ) {
			if ( !semanticOperation.HasValue || protocolBackend.HasValue ) {
				throw new ArgumentException(
					"A semantic-operation capability subject must identify exactly one semantic operation."
				);
			}
			if ( !Enum.IsDefined( semanticOperation.Value ) ) {
				throw new ArgumentOutOfRangeException( nameof( semanticOperation ) );
			}
		} else {
			if ( !protocolBackend.HasValue || semanticOperation.HasValue ) {
				throw new ArgumentException(
					"A protocol-backend capability subject must identify exactly one protocol backend."
				);
			}
			if ( !Enum.IsDefined( protocolBackend.Value ) ) {
				throw new ArgumentOutOfRangeException( nameof( protocolBackend ) );
			}
		}

		this.Kind = kind;
		this.SemanticOperation = semanticOperation;
		this.ProtocolBackend = protocolBackend;
	}

	internal TerminalCapabilitySubjectKind Kind {
		get;
	}

	internal TerminalSemanticOperation? SemanticOperation {
		get;
	}

	internal TerminalProtocolBackend? ProtocolBackend {
		get;
	}

	internal static TerminalCapabilitySubject ForSemanticOperation(
		TerminalSemanticOperation operation
	) {
		if ( !Enum.IsDefined( operation ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The terminal semantic operation is not recognized."
			);
		}

		return new TerminalCapabilitySubject(
			TerminalCapabilitySubjectKind.SemanticOperation,
			operation,
			protocolBackend: null
		);
	}

	internal static TerminalCapabilitySubject ForProtocolBackend(
		TerminalProtocolBackend backend
	) {
		if ( !Enum.IsDefined( backend ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( backend ),
				backend,
				"The terminal protocol backend is not recognized."
			);
		}

		return new TerminalCapabilitySubject(
			TerminalCapabilitySubjectKind.ProtocolBackend,
			semanticOperation: null,
			backend
		);
	}
}

/// <summary>
/// Represents one effective capability resolution without treating caller preference as evidence.
/// </summary>
internal readonly record struct TerminalCapabilityResolution {
	internal TerminalCapabilityResolution(
		TerminalCapabilitySubject subject,
		TerminalCapabilitySupportState state,
		TerminalCapabilityEvidenceSource? evidenceSource
	) {
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
		if ( TerminalCapabilitySupportState.Unavailable == state && evidenceSource.HasValue ) {
			throw new ArgumentException(
				"Endpoint unavailability is an effective state and cannot identify a capability evidence source.",
				nameof( evidenceSource )
			);
		}

		this.Subject = subject;
		this.State = state;
		this.EvidenceSource = evidenceSource;
	}

	internal TerminalCapabilitySubject Subject {
		get;
	}

	internal TerminalCapabilitySupportState State {
		get;
	}

	internal TerminalCapabilityEvidenceSource? EvidenceSource {
		get;
	}
}

/// <summary>
/// Stores bounded static/live capability evidence and resolves one effective support state.
/// </summary>
internal sealed class TerminalCapabilityEvidenceLedger {
	private readonly object sync = new();
	private readonly Dictionary<TerminalCapabilitySubject, EvidenceBucket> buckets = [];

	private long sequence;
	private long liveGeneration;

	internal long LiveGeneration {
		get {
			lock ( this.sync ) {
				return this.liveGeneration;
			}
		}

	internal void Record(
		TerminalCapabilitySubject subject,
		TerminalCapabilitySupportState state,
		TerminalCapabilityEvidenceSource source
	) {
		ValidateEvidence(
			state,
			source
		);

		lock ( this.sync ) {
			long nextSequence = checked( this.sequence + 1 );
			this.sequence = nextSequence;
			if ( !this.buckets.TryGetValue(
				subject,
				out EvidenceBucket? bucket
			) ) {
				bucket = new EvidenceBucket();
				this.buckets.Add(
					subject,
					bucket
				);
			}

			bucket.Record(
				new EvidenceEntry(
					state,
					source,
					nextSequence,
					this.liveGeneration
				)
			);
		}
	}

	internal TerminalCapabilityResolution Resolve(
		TerminalCapabilitySubject subject,
		bool endpointAvailable = true
	) {
		if ( !endpointAvailable ) {
			return new TerminalCapabilityResolution(
				subject,
				TerminalCapabilitySupportState.Unavailable,
				evidenceSource: null
			);
		}

		lock ( this.sync ) {
			if ( !this.buckets.TryGetValue(
				subject,
				out EvidenceBucket? bucket
			) ) {
				return new TerminalCapabilityResolution(
					subject,
					TerminalCapabilitySupportState.Unknown,
					evidenceSource: null
				);
			}

			EvidenceEntry? decisiveLive = bucket.GetLatestDecisiveLive(
				this.liveGeneration
			);
			if ( decisiveLive.HasValue ) {
				return ToResolution(
					subject,
					decisiveLive.Value
				);
			}

			EvidenceEntry? termInfo = bucket.GetStatic(
				TerminalCapabilityEvidenceSource.TermInfo
			);
			if ( termInfo.HasValue
				&& TerminalCapabilitySupportState.Advertised == termInfo.Value.State ) {
				return ToResolution(
					subject,
					termInfo.Value
				);
			}

			EvidenceEntry? builtInProfile = bucket.GetStatic(
				TerminalCapabilityEvidenceSource.BuiltInProfile
			);
			if ( builtInProfile.HasValue
				&& TerminalCapabilitySupportState.Advertised == builtInProfile.Value.State ) {
				return ToResolution(
					subject,
					builtInProfile.Value
				);
			}

			EvidenceEntry? unknownLive = bucket.GetLatestUnknownLive(
				this.liveGeneration
			);
			if ( unknownLive.HasValue ) {
				return ToResolution(
					subject,
					unknownLive.Value
				);
			}

			return new TerminalCapabilityResolution(
				subject,
				TerminalCapabilitySupportState.Unknown,
				evidenceSource: null
			);
		}
	}

	internal void AdvanceLiveGeneration() {
		lock ( this.sync ) {
			this.liveGeneration = checked( this.liveGeneration + 1 );
			foreach ( EvidenceBucket bucket in this.buckets.Values ) {
				bucket.ClearLive();
			}
		}
	}

	private static TerminalCapabilityResolution ToResolution(
		TerminalCapabilitySubject subject,
		EvidenceEntry entry
	) {
		return new TerminalCapabilityResolution(
			subject,
			entry.State,
			entry.Source
		);
	}

	private static void ValidateEvidence(
		TerminalCapabilitySupportState state,
		TerminalCapabilityEvidenceSource source
	) {
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
		if ( TerminalCapabilitySupportState.Unavailable == state ) {
			throw new ArgumentException(
				"Unavailable is derived from endpoint state and cannot be stored as capability evidence.",
				nameof( state )
			);
		}

		bool staticSource = source is TerminalCapabilityEvidenceSource.TermInfo
			or TerminalCapabilityEvidenceSource.BuiltInProfile;
		if ( staticSource
			&& state is not TerminalCapabilitySupportState.Unknown
			and not TerminalCapabilitySupportState.Advertised ) {
			throw new ArgumentException(
				"Static capability sources may record only Unknown or Advertised evidence.",
				nameof( state )
			);
		}
		if ( !staticSource
			&& state is not TerminalCapabilitySupportState.Unknown
			and not TerminalCapabilitySupportState.Unsupported
			and not TerminalCapabilitySupportState.Verified ) {
			throw new ArgumentException(
				"Live capability sources may record only Unknown, Unsupported, or Verified evidence.",
				nameof( state )
			);
		}
	}

	private readonly record struct EvidenceEntry(
		TerminalCapabilitySupportState State,
		TerminalCapabilityEvidenceSource Source,
		long Sequence,
		long LiveGeneration
	);

	private sealed class EvidenceBucket {
		private EvidenceEntry? termInfo;
		private EvidenceEntry? builtInProfile;
		private EvidenceEntry? liveProbeDecisive;
		private EvidenceEntry? protocolResponseDecisive;
		private EvidenceEntry? liveProbeUnknown;
		private EvidenceEntry? protocolResponseUnknown;

		internal void Record(
			EvidenceEntry entry
		) {
			switch ( entry.Source ) {
				case TerminalCapabilityEvidenceSource.TermInfo:
					this.termInfo = entry;
					break;

				case TerminalCapabilityEvidenceSource.BuiltInProfile:
					this.builtInProfile = entry;
					break;

				case TerminalCapabilityEvidenceSource.LiveProbe:
					if ( TerminalCapabilitySupportState.Unknown == entry.State ) {
						this.liveProbeUnknown = entry;
					} else {
						this.liveProbeDecisive = entry;
					}
					break;

				case TerminalCapabilityEvidenceSource.ProtocolResponse:
					if ( TerminalCapabilitySupportState.Unknown == entry.State ) {
						this.protocolResponseUnknown = entry;
					} else {
						this.protocolResponseDecisive = entry;
					}
					break;

				default:
					throw new InvalidOperationException(
						"The terminal capability evidence source is not recognized."
					);
			}
		}

		internal EvidenceEntry? GetStatic(
			TerminalCapabilityEvidenceSource source
		) {
			return source switch {
				TerminalCapabilityEvidenceSource.TermInfo => this.termInfo,
				TerminalCapabilityEvidenceSource.BuiltInProfile => this.builtInProfile,
				_ => throw new ArgumentOutOfRangeException( nameof( source ) )
			};
		}

		internal EvidenceEntry? GetLatestDecisiveLive(
			long generation
		) {
			return Latest(
				CurrentGeneration(
					this.liveProbeDecisive,
					generation
				),
				CurrentGeneration(
					this.protocolResponseDecisive,
					generation
				)
			);
		}

		internal EvidenceEntry? GetLatestUnknownLive(
			long generation
		) {
			return Latest(
				CurrentGeneration(
					this.liveProbeUnknown,
					generation
				),
				CurrentGeneration(
					this.protocolResponseUnknown,
					generation
				)
			);
		}

		internal void ClearLive() {
			this.liveProbeDecisive = null;
			this.protocolResponseDecisive = null;
			this.liveProbeUnknown = null;
			this.protocolResponseUnknown = null;
		}

		private static EvidenceEntry? CurrentGeneration(
			EvidenceEntry? entry,
			long generation
		) {
			return entry.HasValue && generation == entry.Value.LiveGeneration
				? entry
				: null
			;
		}

		private static EvidenceEntry? Latest(
			EvidenceEntry? first,
			EvidenceEntry? second
		) {
			if ( !first.HasValue ) {
				return second;
			}
			if ( !second.HasValue ) {
				return first;
			}

			return first.Value.Sequence >= second.Value.Sequence
				? first
				: second
			;
		}
	}
}
