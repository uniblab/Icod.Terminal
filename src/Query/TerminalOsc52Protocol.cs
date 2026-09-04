namespace Icod.Terminal;

/// <summary>
/// Implements OSC 52 response matching for the frozen 0.7 single-selection subset.
/// </summary>
internal static class TerminalOsc52Protocol {
	internal static ITerminalResponseMatcher CreateResponseMatcher(
		TerminalOsc52Selection selection
	) {
		return new TerminalOsc52ResponseMatcher( selection );
	}

	internal static byte[] ParsePayload(
		TerminalResponseFrame frame,
		TerminalOsc52Selection selection
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
			throw new FormatException( "The terminal response is not an OSC frame." );
		}

		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		if ( !TryGetCorrelatedPayload(
			bytes,
			TerminalOsc52SelectionEncoder.Encode( selection ),
			out ReadOnlySpan<byte> payload
		) ) {
			throw new FormatException(
				"The terminal response is not a valid correlated OSC 52 response."
			);
		}

		try {
			return TerminalOsc52PayloadCodec.Decode( payload );
		} catch ( ArgumentOutOfRangeException exception ) {
			throw new FormatException(
				"The correlated OSC 52 response payload exceeds the supported bound.",
				exception
			);
		}
	}

	private static bool TryGetCorrelatedPayload(
		ReadOnlySpan<byte> bytes,
		byte expectedSelection,
		out ReadOnlySpan<byte> payload
	) {
		payload = default;
		int start;
		int end;

		if ( 4 <= bytes.Length
			&& 0x1B == bytes[ 0 ]
			&& (byte)']' == bytes[ 1 ] ) {
			start = 2;
			if ( 0x07 == bytes[ ^1 ] ) {
				end = bytes.Length - 1;
			} else if ( 2 <= bytes.Length
				&& 0x1B == bytes[ ^2 ]
				&& (byte)'\\' == bytes[ ^1 ] ) {
				end = bytes.Length - 2;
			} else {
				return false;
			}
		} else if ( 3 <= bytes.Length
			&& 0x9D == bytes[ 0 ]
			&& 0x9C == bytes[ ^1 ] ) {
			start = 1;
			end = bytes.Length - 1;
		} else {
			return false;
		}

		if ( end - start < 5
			|| (byte)'5' != bytes[ start ]
			|| (byte)'2' != bytes[ start + 1 ]
			|| (byte)';' != bytes[ start + 2 ]
			|| expectedSelection != bytes[ start + 3 ]
			|| (byte)';' != bytes[ start + 4 ] ) {
			return false;
		}

		payload = bytes.Slice(
			start + 5,
			end - start - 5
		);
		return true;
	}

	private sealed class TerminalOsc52ResponseMatcher : ITerminalResponseMatcher {
		private readonly byte selection;

		internal TerminalOsc52ResponseMatcher(
			TerminalOsc52Selection selection
		) {
			this.selection = TerminalOsc52SelectionEncoder.Encode( selection );
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Osc;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TerminalResponseFrameKind.Osc == frame.Kind
				&& TryGetCorrelatedPayload(
					frame.Bytes.Span,
					this.selection,
					out _
				)
			;
		}
	}
}
