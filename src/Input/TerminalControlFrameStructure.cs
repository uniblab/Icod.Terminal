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
/// Identifies the framing terminator retained by one structured control frame.
/// </summary>
internal enum TerminalStringTerminatorKind {
	None,
	Bell,
	SevenBitSt,
	EightBitSt
}

/// <summary>
/// Retains the structural byte regions of one complete normalized terminal control frame.
/// </summary>
internal readonly struct TerminalControlFrameStructure {
	private readonly ReadOnlyMemory<byte> bytes;
	private readonly int parameterOffset;
	private readonly int parameterLength;
	private readonly int intermediateOffset;
	private readonly int intermediateLength;
	private readonly int payloadOffset;
	private readonly int payloadLength;

	private TerminalControlFrameStructure(
		ReadOnlyMemory<byte> bytes,
		TerminalControlFamily family,
		bool usesEightBitIntroducer,
		int introducerLength,
		int parameterOffset,
		int parameterLength,
		int intermediateOffset,
		int intermediateLength,
		byte? finalByte,
		int payloadOffset,
		int payloadLength,
		TerminalStringTerminatorKind terminatorKind,
		int terminatorLength
	) {
		this.bytes = bytes;
		this.Family = family;
		this.UsesEightBitIntroducer = usesEightBitIntroducer;
		this.IntroducerLength = introducerLength;
		this.parameterOffset = parameterOffset;
		this.parameterLength = parameterLength;
		this.intermediateOffset = intermediateOffset;
		this.intermediateLength = intermediateLength;
		this.FinalByte = finalByte;
		this.payloadOffset = payloadOffset;
		this.payloadLength = payloadLength;
		this.TerminatorKind = terminatorKind;
		this.TerminatorLength = terminatorLength;
	}

	internal TerminalControlFamily Family {
		get;
	}

	internal bool UsesEightBitIntroducer {
		get;
	}

	internal int IntroducerLength {
		get;
	}

	internal ReadOnlyMemory<byte> ParameterBytes {
		get {
			return this.bytes.Slice(
				this.parameterOffset,
				this.parameterLength
			);
		}
	}

	internal ReadOnlyMemory<byte> IntermediateBytes {
		get {
			return this.bytes.Slice(
				this.intermediateOffset,
				this.intermediateLength
			);
		}
	}

	internal byte? FinalByte {
		get;
	}

	internal ReadOnlyMemory<byte> PayloadBytes {
		get {
			return this.bytes.Slice(
				this.payloadOffset,
				this.payloadLength
			);
		}
	}

	internal TerminalStringTerminatorKind TerminatorKind {
		get;
	}

	internal int TerminatorLength {
		get;
	}

	internal static TerminalControlFrameStructure Parse(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );

		TerminalControlFamily family = frame.Kind switch {
			TerminalResponseFrameKind.Csi => TerminalControlFamily.Csi,
			TerminalResponseFrameKind.Dcs => TerminalControlFamily.Dcs,
			TerminalResponseFrameKind.Osc => TerminalControlFamily.Osc,
			_ => throw new InvalidOperationException(
				"The terminal response frame kind is not recognized."
			)
		};

		return Parse(
			frame.Bytes,
			family
		);
	}

	internal static TerminalControlFrameStructure Parse(
		ReadOnlyMemory<byte> bytes,
		TerminalControlFamily family
	) {
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( family ),
				family,
				"The terminal control family is not recognized."
			);
		}
		if ( bytes.IsEmpty ) {
			throw new FormatException(
				"A structured terminal control frame cannot be empty."
			);
		}

		ReadOnlySpan<byte> span = bytes.Span;
		int introducerLength = GetIntroducerLength(
			span,
			family,
			out bool usesEightBitIntroducer
		);
		if ( 0 == introducerLength ) {
			throw new FormatException(
				"The terminal control frame does not contain the expected family introducer."
			);
		}

		return family switch {
			TerminalControlFamily.Csi => ParseCsi(
				bytes,
				usesEightBitIntroducer,
				introducerLength
			),
			TerminalControlFamily.Dcs => ParseDcs(
				bytes,
				usesEightBitIntroducer,
				introducerLength
			),
			TerminalControlFamily.Osc
				or TerminalControlFamily.Apc
				or TerminalControlFamily.Pm
				or TerminalControlFamily.Sos
					=> ParseString(
						bytes,
						family,
						usesEightBitIntroducer,
						introducerLength
					),
			_ => throw new InvalidOperationException(
				"The terminal control family is not recognized."
			)
		};
	}

	internal static bool TryParse(
		TerminalResponseFrame frame,
		out TerminalControlFrameStructure structure
	) {
		ArgumentNullException.ThrowIfNull( frame );

		try {
			structure = Parse( frame );
			return true;
		} catch ( FormatException ) {
			structure = default;
			return false;
		}
	}

	private static TerminalControlFrameStructure ParseCsi(
		ReadOnlyMemory<byte> bytes,
		bool usesEightBitIntroducer,
		int introducerLength
	) {
		ReadOnlySpan<byte> span = bytes.Span;
		if ( span.Length <= introducerLength ) {
			throw new FormatException(
				"A CSI frame must contain a final byte."
			);
		}

		int finalIndex = span.Length - 1;
		byte finalByte = span[ finalIndex ];
		if ( !IsFinalByte( finalByte ) ) {
			throw new FormatException(
				"A CSI frame does not end with a valid final byte."
			);
		}

		GetHeaderRegions(
			span,
			introducerLength,
			finalIndex,
			out int parameterOffset,
			out int parameterLength,
			out int intermediateOffset,
			out int intermediateLength
		);

		return new TerminalControlFrameStructure(
			bytes,
			TerminalControlFamily.Csi,
			usesEightBitIntroducer,
			introducerLength,
			parameterOffset,
			parameterLength,
			intermediateOffset,
			intermediateLength,
			finalByte,
			finalIndex,
			0,
			TerminalStringTerminatorKind.None,
			0
		);
	}

	private static TerminalControlFrameStructure ParseDcs(
		ReadOnlyMemory<byte> bytes,
		bool usesEightBitIntroducer,
		int introducerLength
	) {
		ReadOnlySpan<byte> span = bytes.Span;
		GetStringTerminator(
			span,
			TerminalControlFamily.Dcs,
			usesEightBitIntroducer,
			out TerminalStringTerminatorKind terminatorKind,
			out int terminatorLength
		);

		int contentEnd = span.Length - terminatorLength;
		if ( contentEnd <= introducerLength ) {
			throw new FormatException(
				"A DCS frame must contain a final selector before its payload."
			);
		}

		int index = introducerLength;
		while ( index < contentEnd && IsParameterByte( span[ index ] ) ) {
			++index;
		}
		int parameterOffset = introducerLength;
		int parameterLength = index - parameterOffset;

		int intermediateOffset = index;
		while ( index < contentEnd && IsIntermediateByte( span[ index ] ) ) {
			++index;
		}
		int intermediateLength = index - intermediateOffset;

		if ( index >= contentEnd || !IsFinalByte( span[ index ] ) ) {
			throw new FormatException(
				"A DCS frame must contain a valid final selector after its header."
			);
		}
		byte finalByte = span[ index ];
		int payloadOffset = index + 1;
		int payloadLength = contentEnd - payloadOffset;

		return new TerminalControlFrameStructure(
			bytes,
			TerminalControlFamily.Dcs,
			usesEightBitIntroducer,
			introducerLength,
			parameterOffset,
			parameterLength,
			intermediateOffset,
			intermediateLength,
			finalByte,
			payloadOffset,
			payloadLength,
			terminatorKind,
			terminatorLength
		);
	}

	private static TerminalControlFrameStructure ParseString(
		ReadOnlyMemory<byte> bytes,
		TerminalControlFamily family,
		bool usesEightBitIntroducer,
		int introducerLength
	) {
		ReadOnlySpan<byte> span = bytes.Span;
		GetStringTerminator(
			span,
			family,
			usesEightBitIntroducer,
			out TerminalStringTerminatorKind terminatorKind,
			out int terminatorLength
		);

		int payloadOffset = introducerLength;
		int payloadLength = span.Length - introducerLength - terminatorLength;
		if ( 0 > payloadLength ) {
			throw new FormatException(
				"The terminal control string has invalid framing bounds."
			);
		}

		return new TerminalControlFrameStructure(
			bytes,
			family,
			usesEightBitIntroducer,
			introducerLength,
			payloadOffset,
			0,
			payloadOffset,
			0,
			finalByte: null,
			payloadOffset,
			payloadLength,
			terminatorKind,
			terminatorLength
		);
	}

	private static int GetIntroducerLength(
		ReadOnlySpan<byte> bytes,
		TerminalControlFamily family,
		out bool usesEightBitIntroducer
	) {
		usesEightBitIntroducer = false;
		byte eightBit = GetEightBitIntroducer( family );
		if ( 0 < bytes.Length && eightBit == bytes[ 0 ] ) {
			usesEightBitIntroducer = true;
			return 1;
		}

		if ( 2 > bytes.Length
			|| 0x1B != bytes[ 0 ]
			|| GetSevenBitIntroducerFinal( family ) != bytes[ 1 ] ) {
			return 0;
		}

		return 2;
	}

	private static void GetHeaderRegions(
		ReadOnlySpan<byte> bytes,
		int contentStart,
		int contentEnd,
		out int parameterOffset,
		out int parameterLength,
		out int intermediateOffset,
		out int intermediateLength
	) {
		if ( 0 > contentStart || contentStart > contentEnd || contentEnd > bytes.Length ) {
			throw new ArgumentOutOfRangeException( nameof( contentStart ) );
		}

		int index = contentStart;
		parameterOffset = index;
		while ( index < contentEnd && IsParameterByte( bytes[ index ] ) ) {
			++index;
		}
		parameterLength = index - parameterOffset;

		intermediateOffset = index;
		while ( index < contentEnd && IsIntermediateByte( bytes[ index ] ) ) {
			++index;
		}
		intermediateLength = index - intermediateOffset;

		if ( index != contentEnd ) {
			throw new FormatException(
				"A terminal control header contains bytes outside the parameter/intermediate grammar."
			);
		}
	}

	private static void GetStringTerminator(
		ReadOnlySpan<byte> bytes,
		TerminalControlFamily family,
		bool usesEightBitIntroducer,
		out TerminalStringTerminatorKind terminatorKind,
		out int terminatorLength
	) {
		if ( TerminalControlFamily.Csi == family ) {
			throw new ArgumentException(
				"CSI does not use a string terminator.",
				nameof( family )
			);
		}

		if ( TerminalControlFamily.Osc == family
			&& !usesEightBitIntroducer
			&& 0 < bytes.Length
			&& 0x07 == bytes[ ^1 ] ) {
			terminatorKind = TerminalStringTerminatorKind.Bell;
			terminatorLength = 1;
			return;
		}

		if ( 0 < bytes.Length && 0x9C == bytes[ ^1 ] ) {
			if ( family is TerminalControlFamily.Osc
				or TerminalControlFamily.Apc
				or TerminalControlFamily.Pm
				or TerminalControlFamily.Sos ) {
				if ( !usesEightBitIntroducer ) {
					throw new FormatException(
						"A seven-bit terminal control string cannot terminate with an eight-bit ST in the normalized framing contract."
					);
				}
			}

			terminatorKind = TerminalStringTerminatorKind.EightBitSt;
			terminatorLength = 1;
			return;
		}

		if ( 2 <= bytes.Length
			&& 0x1B == bytes[ ^2 ]
			&& (byte)'\\' == bytes[ ^1 ] ) {
			if ( family is TerminalControlFamily.Osc
				or TerminalControlFamily.Apc
				or TerminalControlFamily.Pm
				or TerminalControlFamily.Sos ) {
				if ( usesEightBitIntroducer ) {
					throw new FormatException(
						"An eight-bit terminal control string cannot terminate with a seven-bit ST in the normalized framing contract."
					);
				}
			}

			terminatorKind = TerminalStringTerminatorKind.SevenBitSt;
			terminatorLength = 2;
			return;
		}

		throw new FormatException(
			"The terminal control string does not contain a recognized terminator."
		);
	}

	private static byte GetSevenBitIntroducerFinal(
		TerminalControlFamily family
	) {
		return family switch {
			TerminalControlFamily.Csi => (byte)'[',
			TerminalControlFamily.Dcs => (byte)'P',
			TerminalControlFamily.Osc => (byte)']',
			TerminalControlFamily.Apc => (byte)'_',
			TerminalControlFamily.Pm => (byte)'^',
			TerminalControlFamily.Sos => (byte)'X',
			_ => throw new ArgumentOutOfRangeException( nameof( family ) )
		};
	}

	private static byte GetEightBitIntroducer(
		TerminalControlFamily family
	) {
		return family switch {
			TerminalControlFamily.Csi => 0x9B,
			TerminalControlFamily.Dcs => 0x90,
			TerminalControlFamily.Osc => 0x9D,
			TerminalControlFamily.Apc => 0x9F,
			TerminalControlFamily.Pm => 0x9E,
			TerminalControlFamily.Sos => 0x98,
			_ => throw new ArgumentOutOfRangeException( nameof( family ) )
		};
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
}
