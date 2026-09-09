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
/// Performs strict, bounded framing through the shared incremental control-language scanner.
/// </summary>
internal static class TerminalResponseFramer {
	internal const int DefaultMaximumFrameBytes = 4096;
	internal const int HardMaximumFrameBytes = TerminalOsc52PayloadCodec.MaximumFrameBytes;

	/// <summary>
	/// Frames one normalized control family without interpreting its dialect payload.
	/// </summary>
	internal static TerminalResponseFrameParseResult Parse(
		IReadOnlyList<byte> bytes,
		TerminalControlFamily family,
		int maximumFrameBytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( family ),
				family,
				"The terminal control family is not recognized."
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

		TerminalControlSequenceScanner scanner = new( maximumFrameBytes );
		foreach ( byte value in bytes ) {
			TerminalResponseFrameParseStatus status = scanner.Feed( value );

			if ( scanner.Family.HasValue
				&& scanner.Family.Value != family ) {
				return new TerminalResponseFrameParseResult(
					TerminalResponseFrameParseStatus.NotCandidate
				);
			}

			if ( status is TerminalResponseFrameParseStatus.NotCandidate
				or TerminalResponseFrameParseStatus.Invalid ) {
				return new TerminalResponseFrameParseResult( status );
			}
			if ( TerminalResponseFrameParseStatus.Complete == status ) {
				return new TerminalResponseFrameParseResult(
					TerminalResponseFrameParseStatus.Complete,
					scanner.Length
				);
			}
		}

		return new TerminalResponseFrameParseResult(
			scanner.Status,
			introducerIncomplete: scanner.IntroducerIncomplete
		);
	}

	/// <summary>
	/// Frames one compatibility response kind through the normalized family parser.
	/// </summary>
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

		return Parse(
			bytes,
			TerminalResponseFrameKinds.GetControlFamily( kind ),
			maximumFrameBytes
		);
	}
}
