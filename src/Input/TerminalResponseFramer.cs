namespace Icod.Terminal;

/// <summary>
/// Identifies the current bounded framing result for a potential response.
/// </summary>
internal enum TerminalResponseFrameParseStatus {
	NotCandidate,
	Incomplete,
	Complete,
	Invalid
}

/// <summary>
/// Describes one bounded response-framing attempt.
/// </summary>
internal readonly struct TerminalResponseFrameParseResult {
	internal TerminalResponseFrameParseResult(
		TerminalResponseFrameParseStatus status,
		int length = 0,
		bool introducerIncomplete = false
	) {
		if ( !Enum.IsDefined( status ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( status ),
				status,
				"The terminal response framing status is not recognized."
			);
		}
		if ( 0 > length ) {
			throw new ArgumentOutOfRangeException( nameof( length ) );
		}
		if ( TerminalResponseFrameParseStatus.Complete == status && 0 == length ) {
			throw new ArgumentOutOfRangeException(
				nameof( length ),
				length,
				"A complete terminal response frame must have a positive length."
			);
		}
		if ( TerminalResponseFrameParseStatus.Complete != status && 0 != length ) {
			throw new ArgumentException(
				"Only a complete terminal response frame may report a positive length.",
				nameof( length )
			);
		}
		if ( introducerIncomplete
			&& TerminalResponseFrameParseStatus.Incomplete != status ) {
			throw new ArgumentException(
				"An incomplete introducer requires incomplete response framing status.",
				nameof( introducerIncomplete )
			);
		}

		this.Status = status;
		this.Length = length;
		this.IntroducerIncomplete = introducerIncomplete;
	}

	internal TerminalResponseFrameParseStatus Status {
		get;
	}

	internal int Length {
		get;
	}

	internal bool IntroducerIncomplete {
		get;
	}
}

/// <summary>
/// Performs strict, bounded framing for CSI, DCS, and OSC terminal responses.
/// </summary>
internal static class TerminalResponseFramer {
	internal const int DefaultMaximumFrameBytes = 4096;
	internal const int HardMaximumFrameBytes = TerminalOsc52PayloadCodec.MaximumFrameBytes;

	private const byte BellByte = 0x07;
	private const byte EscapeByte = 0x1B;
	private const byte CsiByte = 0x9B;
	private const byte DcsByte = 0x90;
	private const byte OscByte = 0x9D;
	private const byte StringTerminatorByte = 0x9C;

	internal static TerminalResponseFrameParseResult Parse(
		IReadOnlyList<byte> bytes,
		TerminalResponseFrameKind kind,
		int maximumFrameBytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"The terminal response frame kind is not recognized."
			);
		}
		if ( 4 > maximumFrameBytes || HardMaximumFrameBytes < maximumFrameBytes ) {
			throw new ArgumentOutOfRangeException( nameof( maximumFrameBytes ) );
		}
		if ( 0 == bytes.Count ) {
			return new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.NotCandidate
			);
		}

		return kind switch {
			TerminalResponseFrameKind.Csi => ParseCsi( bytes, maximumFrameBytes ),
			TerminalResponseFrameKind.Dcs => ParseDcs( bytes, maximumFrameBytes ),
			TerminalResponseFrameKind.Osc => ParseOsc( bytes, maximumFrameBytes ),
			_ => throw new InvalidOperationException(
				"The terminal response frame kind is not recognized."
			)
		};
	}

	private static TerminalResponseFrameParseResult ParseCsi(
		IReadOnlyList<byte> bytes,
		int maximumFrameBytes
	) {
		TerminalResponseFrameParseResult introducer = ParseIntroducer(
			bytes,
			(byte)'[',
			CsiByte
		);
		if ( TerminalResponseFrameParseStatus.Complete != introducer.Status ) {
			return introducer;
		}

		int index = introducer.Length;
		bool intermediateSeen = false;

		while ( index < bytes.Count ) {
			if ( index >= maximumFrameBytes ) {
				return Invalid();
			}

			byte value = bytes[ index ];
			if ( IsFinalByte( value ) ) {
				return Complete( index + 1 );
			}
			if ( IsParameterByte( value ) && !intermediateSeen ) {
				++index;
				continue;
			}
			if ( IsIntermediateByte( value ) ) {
				intermediateSeen = true;
				++index;
				continue;
			}

			return Invalid();
		}

		return bytes.Count >= maximumFrameBytes
			? Invalid()
			: Incomplete()
		;
	}

	private static TerminalResponseFrameParseResult ParseDcs(
		IReadOnlyList<byte> bytes,
		int maximumFrameBytes
	) {
		TerminalResponseFrameParseResult introducer = ParseIntroducer(
			bytes,
			(byte)'P',
			DcsByte
		);
		if ( TerminalResponseFrameParseStatus.Complete != introducer.Status ) {
			return introducer;
		}

		int index = introducer.Length;
		bool intermediateSeen = false;
		bool finalSeen = false;

		while ( index < bytes.Count ) {
			if ( index >= maximumFrameBytes ) {
				return Invalid();
			}

			byte value = bytes[ index ];
			if ( !finalSeen ) {
				if ( IsFinalByte( value ) ) {
					finalSeen = true;
					++index;
					continue;
				}
				if ( IsParameterByte( value ) && !intermediateSeen ) {
					++index;
					continue;
				}
				if ( IsIntermediateByte( value ) ) {
					intermediateSeen = true;
					++index;
					continue;
				}

				return Invalid();
			}

			if ( StringTerminatorByte == value ) {
				return Complete( index + 1 );
			}
			if ( EscapeByte == value ) {
				if ( index + 1 >= maximumFrameBytes ) {
					return Invalid();
				}
				if ( index + 1 >= bytes.Count ) {
					return Incomplete();
				}
				if ( '\\' == bytes[ index + 1 ] ) {
					return Complete( index + 2 );
				}

				return Invalid();
			}
			if ( 0x18 == value || 0x1A == value ) {
				return Invalid();
			}

			++index;
		}

		return bytes.Count >= maximumFrameBytes
			? Invalid()
			: Incomplete()
		;
	}

	private static TerminalResponseFrameParseResult ParseOsc(
		IReadOnlyList<byte> bytes,
		int maximumFrameBytes
	) {
		TerminalResponseFrameParseResult introducer = ParseIntroducer(
			bytes,
			(byte)']',
			OscByte
		);
		if ( TerminalResponseFrameParseStatus.Complete != introducer.Status ) {
			return introducer;
		}

		bool usesEightBitIntroducer = OscByte == bytes[ 0 ];
		int index = introducer.Length;

		while ( index < bytes.Count ) {
			if ( index >= maximumFrameBytes ) {
				return Invalid();
			}

			byte value = bytes[ index ];
			if ( StringTerminatorByte == value ) {
				return usesEightBitIntroducer
					? Complete( index + 1 )
					: Invalid()
				;
			}
			if ( BellByte == value ) {
				return usesEightBitIntroducer
					? Invalid()
					: Complete( index + 1 )
				;
			}
			if ( EscapeByte == value ) {
				if ( usesEightBitIntroducer ) {
					return Invalid();
				}
				if ( index + 1 >= maximumFrameBytes ) {
					return Invalid();
				}
				if ( index + 1 >= bytes.Count ) {
					return Incomplete();
				}
				if ( '\\' == bytes[ index + 1 ] ) {
					return Complete( index + 2 );
				}

				return Invalid();
			}
			if ( 0x18 == value || 0x1A == value ) {
				return Invalid();
			}

			++index;
		}

		return bytes.Count >= maximumFrameBytes
			? Invalid()
			: Incomplete()
		;
	}

	private static TerminalResponseFrameParseResult ParseIntroducer(
		IReadOnlyList<byte> bytes,
		byte sevenBitFinal,
		byte eightBitIntroducer
	) {
		if ( eightBitIntroducer == bytes[ 0 ] ) {
			return Complete( 1 );
		}
		if ( EscapeByte != bytes[ 0 ] ) {
			return new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.NotCandidate
			);
		}
		if ( 1 == bytes.Count ) {
			return new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.Incomplete,
				introducerIncomplete: true
			);
		}
		if ( sevenBitFinal != bytes[ 1 ] ) {
			return new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.NotCandidate
			);
		}

		return Complete( 2 );
	}

	private static bool IsParameterByte(
		byte value
	) {
		return value is >= 0x30 and <= 0x3F;
	}

	private static bool IsIntermediateByte(
		byte value
	) {
		return value is >= 0x20 and <= 0x2F;
	}

	private static bool IsFinalByte(
		byte value
	) {
		return value is >= 0x40 and <= 0x7E;
	}

	private static TerminalResponseFrameParseResult Incomplete() {
		return new TerminalResponseFrameParseResult(
			TerminalResponseFrameParseStatus.Incomplete
		);
	}

	private static TerminalResponseFrameParseResult Complete(
		int length
	) {
		return new TerminalResponseFrameParseResult(
			TerminalResponseFrameParseStatus.Complete,
			length
		);
	}

	private static TerminalResponseFrameParseResult Invalid() {
		return new TerminalResponseFrameParseResult(
			TerminalResponseFrameParseStatus.Invalid
		);
	}
}
