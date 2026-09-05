namespace Icod.Terminal;

using System.Globalization;
using System.Text;

/// <summary>
/// Implements OSC 10-12 / 110-112 dynamic-color framing and response correlation.
/// </summary>
internal static class TerminalDynamicColorProtocol {
	internal static byte[] CreateSetRequest(
		TerminalDynamicColor kind,
		TerminalColor color
	) {
		int osc = GetCommonOscNumber( kind );
		byte[] oscBytes = Encoding.ASCII.GetBytes(
			osc.ToString( CultureInfo.InvariantCulture )
		);
		byte[] colorBytes = TerminalColorCodec.Encode( color );
		byte[] frame = new byte[ 2 + oscBytes.Length + 1 + colorBytes.Length + 2 ];
		int offset = 0;
		frame[ offset++ ] = 0x1b;
		frame[ offset++ ] = (byte)']';
		oscBytes.CopyTo( frame, offset );
		offset += oscBytes.Length;
		frame[ offset++ ] = (byte)';';
		colorBytes.CopyTo( frame, offset );
		offset += colorBytes.Length;
		frame[ offset++ ] = 0x1b;
		frame[ offset ] = (byte)'\\';
		return frame;
	}

	internal static byte[] CreateQueryRequest(
		TerminalDynamicColor kind
	) {
		int osc = GetCommonOscNumber( kind );
		byte[] oscBytes = Encoding.ASCII.GetBytes(
			osc.ToString( CultureInfo.InvariantCulture )
		);
		byte[] frame = new byte[ 2 + oscBytes.Length + 2 + 2 ];
		int offset = 0;
		frame[ offset++ ] = 0x1b;
		frame[ offset++ ] = (byte)']';
		oscBytes.CopyTo( frame, offset );
		offset += oscBytes.Length;
		frame[ offset++ ] = (byte)';';
		frame[ offset++ ] = (byte)'?';
		frame[ offset++ ] = 0x1b;
		frame[ offset ] = (byte)'\\';
		return frame;
	}

	internal static byte[] CreateResetRequest(
		TerminalDynamicColor kind
	) {
		int resetOsc = GetCommonOscNumber( kind ) + 100;
		byte[] oscBytes = Encoding.ASCII.GetBytes(
			resetOsc.ToString( CultureInfo.InvariantCulture )
		);
		byte[] frame = new byte[ 2 + oscBytes.Length + 2 ];
		int offset = 0;
		frame[ offset++ ] = 0x1b;
		frame[ offset++ ] = (byte)']';
		oscBytes.CopyTo( frame, offset );
		offset += oscBytes.Length;
		frame[ offset++ ] = 0x1b;
		frame[ offset ] = (byte)'\\';
		return frame;
	}

	internal static ITerminalResponseMatcher CreateResponseMatcher(
		TerminalDynamicColor kind
	) {
		return new DynamicColorResponseMatcher(
			GetCommonOscNumber( kind )
		);
	}

	internal static TerminalColor ParseObservation(
		TerminalResponseFrame frame,
		TerminalDynamicColor kind
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
			throw new FormatException( "The terminal response is not an OSC frame." );
		}
		if ( !TryGetPayload(
			frame.Bytes.Span,
			GetCommonOscNumber( kind ),
			out ReadOnlySpan<byte> payload
		) ) {
			throw new FormatException(
				"The terminal response is not a correlated dynamic-color response."
			);
		}
		return TerminalColorCodec.Parse( payload );
	}

	private static int GetCommonOscNumber(
		TerminalDynamicColor kind
	) {
		return kind switch {
			TerminalDynamicColor.DefaultForeground => 10,
			TerminalDynamicColor.DefaultBackground => 11,
			TerminalDynamicColor.TextCursor => 12,
			_ => throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"T134 supports only default foreground, default background, and text-cursor dynamic colors."
			)
		};
	}

	private static bool TryGetPayload(
		ReadOnlySpan<byte> bytes,
		int expectedOsc,
		out ReadOnlySpan<byte> payload
	) {
		payload = default;
		int start;
		int end;
		if ( 6 <= bytes.Length
			&& 0x1b == bytes[ 0 ]
			&& (byte)']' == bytes[ 1 ] ) {
			start = 2;
			if ( 0x07 == bytes[ ^1 ] ) {
				end = bytes.Length - 1;
			} else if ( 0x1b == bytes[ ^2 ]
				&& (byte)'\\' == bytes[ ^1 ] ) {
				end = bytes.Length - 2;
			} else {
				return false;
			}
		} else if ( 5 <= bytes.Length
			&& 0x9d == bytes[ 0 ]
			&& 0x9c == bytes[ ^1 ] ) {
			start = 1;
			end = bytes.Length - 1;
		} else {
			return false;
		}

		int separator = bytes[ start..end ].IndexOf( (byte)';' );
		if ( 1 > separator ) {
			return false;
		}
		int separatorIndex = start + separator;
		if ( !TryParseOscNumber(
			bytes[ start..separatorIndex ],
			out int parsedOsc
		) || parsedOsc != expectedOsc ) {
			return false;
		}
		payload = bytes.Slice(
			separatorIndex + 1,
			end - separatorIndex - 1
		);
		return !payload.IsEmpty;
	}

	private static bool HasCorrelatedPrefix(
		IReadOnlyList<byte> bytes,
		int expectedOsc
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

		int separator = start;
		while ( separator < bytes.Count
			&& (byte)';' != bytes[ separator ] ) {
			if ( bytes[ separator ] < (byte)'0'
				|| bytes[ separator ] > (byte)'9' ) {
				return false;
			}
			++separator;
		}
		if ( separator == start || separator >= bytes.Count ) {
			return false;
		}

		int value = 0;
		for ( int offset = start; offset < separator; ++offset ) {
			value = ( value * 10 ) + bytes[ offset ] - (byte)'0';
		}
		return value == expectedOsc;
	}

	private static bool TryParseOscNumber(
		ReadOnlySpan<byte> bytes,
		out int value
	) {
		value = 0;
		if ( bytes.IsEmpty ) {
			return false;
		}
		foreach ( byte character in bytes ) {
			if ( character < (byte)'0' || character > (byte)'9' ) {
				return false;
			}
			value = ( value * 10 ) + character - (byte)'0';
		}
		return true;
	}

	private sealed class DynamicColorResponseMatcher : ITerminalResponseMatcher, ICorrelatedTerminalResponseMatcher {
		private readonly int osc;

		internal DynamicColorResponseMatcher(
			int osc
		) {
			this.osc = osc;
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Osc;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TerminalResponseFrameKind.Osc == frame.Kind
				&& HasCorrelatedPrefix(
					frame.Bytes.ToArray(),
					this.osc
				);
		}

		public bool IsCorrelatedPrefix(
			IReadOnlyList<byte> bytes
		) {
			return HasCorrelatedPrefix(
				bytes,
				this.osc
			);
		}
	}
}
