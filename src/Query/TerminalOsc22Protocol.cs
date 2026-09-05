using System.Text;

namespace Icod.Terminal;

/// <summary>
/// Implements Kitty-compatible OSC 22 pointer-shape queries on the shared query router.
/// </summary>
internal static class TerminalOsc22Protocol {
	private const string CurrentQueryName = "__current__";
	private const string DefaultQueryName = "__default__";
	private const string GrabbedQueryName = "__grabbed__";

	internal static byte[] CreateCurrentQueryRequest() {
		return CreateQueryRequest( CurrentQueryName );
	}

	internal static byte[] CreateDefaultQueryRequest() {
		return CreateQueryRequest( DefaultQueryName );
	}

	internal static byte[] CreateGrabbedQueryRequest() {
		return CreateQueryRequest( GrabbedQueryName );
	}

	internal static byte[] CreateSupportQueryRequest(
		TerminalPointerShape shape
	) {
		return CreateQueryRequest(
			TerminalPointerShapeCodec.GetWireName( shape )
		);
	}

	internal static ITerminalResponseMatcher CreateResponseMatcher() {
		return new TerminalOsc22ResponseMatcher();
	}

	internal static TerminalPointerShape? ParseShapeObservation(
		TerminalResponseFrame frame
	) {
		ReadOnlySpan<byte> payload = GetPayload( frame );
		if ( 1 == payload.Length
			&& (byte)'0' == payload[ 0 ] ) {
			return null;
		}
		if ( payload.IsEmpty ) {
			throw new FormatException(
				"The OSC 22 pointer-shape response payload is empty."
			);
		}
		for ( int index = 0; index < payload.Length; ++index ) {
			if ( 0x7f < payload[ index ] ) {
				throw new FormatException(
					"The OSC 22 pointer-shape response is not valid ASCII."
				);
			}
		}

		return TerminalPointerShapeCodec.ParseWireName(
			Encoding.ASCII.GetString( payload )
		);
	}

	internal static bool ParseSingleShapeSupport(
		TerminalResponseFrame frame
	) {
		ReadOnlySpan<byte> payload = GetPayload( frame );
		if ( 1 != payload.Length ) {
			throw new FormatException(
				"A single-shape OSC 22 support response must contain exactly one 0 or 1 value."
			);
		}

		return payload[ 0 ] switch {
			(byte)'0' => false,
			(byte)'1' => true,
			_ => throw new FormatException(
				"A single-shape OSC 22 support response must contain 0 or 1."
			)
		};
	}

	private static byte[] CreateQueryRequest(
		string queryName
	) {
		ArgumentException.ThrowIfNullOrEmpty( queryName );
		byte[] payload = Encoding.ASCII.GetBytes( queryName );
		byte[] frame = new byte[ 6 + payload.Length + 2 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		frame[ 2 ] = (byte)'2';
		frame[ 3 ] = (byte)'2';
		frame[ 4 ] = (byte)';';
		frame[ 5 ] = (byte)'?';
		payload.CopyTo(
			frame,
			6
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	private static ReadOnlySpan<byte> GetPayload(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
			throw new FormatException( "The terminal response is not an OSC frame." );
		}

		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		int start;
		int end;
		if ( 7 <= bytes.Length
			&& 0x1b == bytes[ 0 ]
			&& (byte)']' == bytes[ 1 ] ) {
			start = 2;
			if ( 0x07 == bytes[ ^1 ] ) {
				end = bytes.Length - 1;
			} else if ( 0x1b == bytes[ ^2 ]
				&& (byte)'\\' == bytes[ ^1 ] ) {
				end = bytes.Length - 2;
			} else {
				throw new FormatException( "The OSC 22 response is not terminated." );
			}
		} else if ( 6 <= bytes.Length
			&& 0x9d == bytes[ 0 ]
			&& 0x9c == bytes[ ^1 ] ) {
			start = 1;
			end = bytes.Length - 1;
		} else {
			throw new FormatException( "The terminal response is not a recognized OSC 22 frame." );
		}

		if ( end - start < 3
			|| (byte)'2' != bytes[ start ]
			|| (byte)'2' != bytes[ start + 1 ]
			|| (byte)';' != bytes[ start + 2 ] ) {
			throw new FormatException( "The terminal response is not an OSC 22 response." );
		}
		return bytes.Slice(
			start + 3,
			end - start - 3
		);
	}

	private static bool HasOsc22Prefix(
		IReadOnlyList<byte> bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		int start;
		if ( 5 <= bytes.Count
			&& 0x1b == bytes[ 0 ]
			&& (byte)']' == bytes[ 1 ] ) {
			start = 2;
		} else if ( 4 <= bytes.Count
			&& 0x9d == bytes[ 0 ] ) {
			start = 1;
		} else {
			return false;
		}

		return (byte)'2' == bytes[ start ]
			&& (byte)'2' == bytes[ start + 1 ]
			&& (byte)';' == bytes[ start + 2 ]
		;
	}

	private sealed class TerminalOsc22ResponseMatcher : ITerminalResponseMatcher, ICorrelatedTerminalResponseMatcher {
		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Osc;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TerminalResponseFrameKind.Osc == frame.Kind
				&& HasOsc22Prefix( frame.Bytes.ToArray() )
			;
		}

		public bool IsCorrelatedPrefix(
			IReadOnlyList<byte> bytes
		) {
			return HasOsc22Prefix( bytes );
		}
	}
}
