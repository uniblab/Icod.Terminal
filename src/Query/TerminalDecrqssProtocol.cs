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

using System.Text;

/// <summary>
/// Implements bounded DECRQSS request and DECRPSS response handling.
/// </summary>
internal static class TerminalDecrqssProtocol {
	internal const int MaximumRequestIdentifierBytes = 16;
	internal const int MaximumStatusStringBytes = 1024;

	internal static ITerminalResponseMatcher ResponseMatcher {
		get;
	} = new TerminalDecrqssResponseMatcher();

	internal static ReadOnlyMemory<byte> CreateRequest(
		TerminalStatusStringKind kind
	) {
		byte[] identifier = GetRequestIdentifier( kind );
		if ( MaximumRequestIdentifierBytes < identifier.Length ) {
			throw new InvalidOperationException(
				$"A DECRQSS request identifier cannot exceed {MaximumRequestIdentifierBytes} bytes."
			);
		}

		return DcsWriter.EncodeFrame(
			ReadOnlySpan<byte>.Empty,
			[ (byte)'$' ],
			(byte)'q',
			identifier
		);
	}

	internal static TerminalStatusStringResponse ParseResponse(
		TerminalStatusStringKind kind,
		TerminalResponseFrame frame
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		ArgumentNullException.ThrowIfNull( frame );

		if ( !TryGetResponseStructure(
			frame,
			out TerminalControlFrameStructure structure
		) ) {
			throw new FormatException(
				"The terminal response is not a DECRPSS frame."
			);
		}

		ReadOnlySpan<byte> parameters = structure.ParameterBytes.Span;
		if ( 1 != parameters.Length ) {
			throw new FormatException(
				"A DECRPSS response must contain exactly one validity parameter."
			);
		}

		ReadOnlySpan<byte> payload = structure.PayloadBytes.Span;
		byte validity = parameters[ 0 ];
		if ( (byte)'0' == validity ) {
			if ( !payload.IsEmpty ) {
				throw new FormatException(
					"A negative DECRPSS response cannot contain status-string data."
				);
			}

			return new TerminalStatusStringResponse(
				kind,
				isSupported: false,
				statusString: null
			);
		}
		if ( (byte)'1' != validity ) {
			throw new FormatException(
				"A DECRPSS validity parameter must be 0 or 1."
			);
		}
		if ( payload.IsEmpty ) {
			throw new FormatException(
				"A positive DECRPSS response must contain status-string data."
			);
		}
		if ( MaximumStatusStringBytes < payload.Length ) {
			throw new FormatException(
				$"A DECRPSS status string cannot exceed {MaximumStatusStringBytes} bytes."
			);
		}

		for ( int index = 0; index < payload.Length; index++ ) {
			if ( payload[ index ] is < 0x20 or > 0x7E ) {
				throw new FormatException(
					"A DECRPSS status string contains a non-printable control byte."
				);
			}
		}

		byte[] identifier = GetRequestIdentifier( kind );
		if ( payload.Length < identifier.Length
			|| !payload.Slice(
				payload.Length - identifier.Length
			).SequenceEqual( identifier ) ) {
			throw new FormatException(
				"The DECRPSS status string does not match the requested control function."
			);
		}

		return new TerminalStatusStringResponse(
			kind,
			isSupported: true,
			Encoding.ASCII.GetString( payload )
		);
	}

	internal static byte[] GetRequestIdentifier(
		TerminalStatusStringKind kind
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}

		switch ( kind ) {
			case TerminalStatusStringKind.SelectGraphicRendition:
				return [ (byte)'m' ];

			case TerminalStatusStringKind.ConformanceLevel:
				return [ (byte)'"', (byte)'p' ];

			case TerminalStatusStringKind.CursorStyle:
				return [ (byte)' ', (byte)'q' ];

			case TerminalStatusStringKind.CharacterProtection:
				return [ (byte)'"', (byte)'q' ];

			case TerminalStatusStringKind.ScrollingRegion:
				return [ (byte)'r' ];

			case TerminalStatusStringKind.LeftRightMargins:
				return [ (byte)'s' ];

			case TerminalStatusStringKind.LinesPerPage:
				return [ (byte)'t' ];

			case TerminalStatusStringKind.ColumnsPerPage:
				return [ (byte)'$', (byte)'|' ];

			case TerminalStatusStringKind.ActiveStatusDisplay:
				return [ (byte)'$', (byte)'}' ];

			case TerminalStatusStringKind.StatusLineType:
				return [ (byte)'$', (byte)'~' ];

			case TerminalStatusStringKind.AttributeChangeExtent:
				return [ (byte)'*', (byte)'x' ];

			case TerminalStatusStringKind.LinesPerScreen:
				return [ (byte)'*', (byte)'|' ];

			default:
				throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
	}

	private static bool TryGetResponseStructure(
		TerminalResponseFrame frame,
		out TerminalControlFrameStructure structure
	) {
		ArgumentNullException.ThrowIfNull( frame );

		structure = default;
		if ( TerminalResponseFrameKind.Dcs != frame.Kind
			|| !TerminalControlFrameStructure.TryParse(
				frame,
				out TerminalControlFrameStructure parsed
			) ) {
			return false;
		}
		if ( TerminalControlFamily.Dcs != parsed.Family
			|| !parsed.FinalByte.HasValue
			|| (byte)'r' != parsed.FinalByte.Value ) {
			return false;
		}

		ReadOnlySpan<byte> intermediates = parsed.IntermediateBytes.Span;
		if ( 1 != intermediates.Length || (byte)'$' != intermediates[ 0 ] ) {
			return false;
		}

		structure = parsed;
		return true;
	}

	private sealed class TerminalDecrqssResponseMatcher : ITerminalResponseMatcher {
		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Dcs;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TryGetResponseStructure(
				frame,
				out _
			);
		}
	}
}
