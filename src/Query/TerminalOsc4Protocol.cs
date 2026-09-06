namespace Icod.Terminal;

using System.Globalization;
using System.Text;

/// <summary>
/// Implements OSC 4 indexed-palette mutation and observation framing.
/// </summary>
internal static class TerminalOsc4Protocol {
	internal static byte[] CreateSetRequest(
		byte index,
		TerminalColor color
	) {
		return CreateSetRequest(
			[ new TerminalPaletteColor( index, color ) ]
		);
	}

	internal static byte[] CreateSetRequest(
		IReadOnlyList<TerminalPaletteColor> entries
	) {
		ArgumentNullException.ThrowIfNull( entries );
		if ( 0 == entries.Count ) {
			throw new ArgumentException(
				"At least one indexed palette color is required.",
				nameof( entries )
			);
		}
		if ( 256 < entries.Count ) {
			throw new ArgumentException(
				"An OSC 4 palette mutation cannot contain more than 256 entries.",
				nameof( entries )
			);
		}

		bool[] seen = new bool[ 256 ];
		List<byte[]> indices = new( entries.Count );
		List<byte[]> colors = new( entries.Count );
		int payloadLength = 1;
		foreach ( TerminalPaletteColor entry in entries ) {
			if ( seen[ entry.Index ] ) {
				throw new ArgumentException(
					$"Palette index {entry.Index} occurs more than once.",
					nameof( entries )
				);
			}
			seen[ entry.Index ] = true;

			byte[] indexBytes = Encoding.ASCII.GetBytes(
				entry.Index.ToString( CultureInfo.InvariantCulture )
			);
			byte[] colorBytes = TerminalColorCodec.Encode( entry.Color );
			indices.Add( indexBytes );
			colors.Add( colorBytes );
			payloadLength += 2 + indexBytes.Length + colorBytes.Length;
		}

		byte[] frame = new byte[ 2 + payloadLength + 2 ];
		int offset = 0;
		frame[ offset++ ] = 0x1b;
		frame[ offset++ ] = (byte)']';
		frame[ offset++ ] = (byte)'4';
		for ( int index = 0; index < entries.Count; ++index ) {
			frame[ offset++ ] = (byte)';';
			indices[ index ].CopyTo( frame, offset );
			offset += indices[ index ].Length;
			frame[ offset++ ] = (byte)';';
			colors[ index ].CopyTo( frame, offset );
			offset += colors[ index ].Length;
		}
		frame[ offset++ ] = 0x1b;
		frame[ offset ] = (byte)'\\';
		return frame;
	}

	internal static byte[] CreateQueryRequest(
		byte index
	) {
		byte[] indexBytes = Encoding.ASCII.GetBytes(
			index.ToString( CultureInfo.InvariantCulture )
		);
		byte[] frame = new byte[ 2 + 2 + indexBytes.Length + 2 + 2 ];
		int offset = 0;
		frame[ offset++ ] = 0x1b;
		frame[ offset++ ] = (byte)']';
		frame[ offset++ ] = (byte)'4';
		frame[ offset++ ] = (byte)';';
		indexBytes.CopyTo( frame, offset );
		offset += indexBytes.Length;
		frame[ offset++ ] = (byte)';';
		frame[ offset++ ] = (byte)'?';
		frame[ offset++ ] = 0x1b;
		frame[ offset ] = (byte)'\\';
		return frame;
	}

	internal static ITerminalResponseMatcher CreateResponseMatcher(
		byte index
	) {
		return new TerminalOsc4ResponseMatcher( index );
	}

	internal static TerminalColor ParseObservation(
		TerminalResponseFrame frame,
		byte expectedIndex
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
			throw new FormatException( "The terminal response is not an OSC frame." );
		}
		if ( !TryGetPayload(
			frame.Bytes.Span,
			expectedIndex,
			out ReadOnlySpan<byte> payload
		) ) {
			throw new FormatException(
				"The terminal response is not a correlated OSC 4 palette response."
			);
		}

		return TerminalColorCodec.Parse( payload );
	}

	private static bool HasCorrelatedPrefix(
		IReadOnlyList<byte> bytes,
		byte expectedIndex
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		int start;
		if ( 6 <= bytes.Count
			&& 0x1b == bytes[ 0 ]
			&& (byte)']' == bytes[ 1 ] ) {
			start = 2;
		} else if ( 5 <= bytes.Count
			&& 0x9d == bytes[ 0 ] ) {
			start = 1;
		} else {
			return false;
		}

		if ( (byte)'4' != bytes[ start ]
			|| (byte)';' != bytes[ start + 1 ] ) {
			return false;
		}

		int indexStart = start + 2;
		int separator = indexStart;
		while ( separator < bytes.Count
			&& (byte)';' != bytes[ separator ] ) {
			if ( bytes[ separator ] < (byte)'0'
				|| bytes[ separator ] > (byte)'9' ) {
				return false;
			}
			++separator;
		}
		if ( separator == indexStart
			|| separator >= bytes.Count ) {
			return false;
		}

		return TryParseIndex(
			bytes,
			indexStart,
			separator,
			out byte parsedIndex
		) && parsedIndex == expectedIndex;
	}

	private static bool TryGetPayload(
		ReadOnlySpan<byte> bytes,
		byte expectedIndex,
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

		if ( end - start < 5
			|| (byte)'4' != bytes[ start ]
			|| (byte)';' != bytes[ start + 1 ] ) {
			return false;
		}

		int indexStart = start + 2;
		int separator = bytes[ indexStart..end ].IndexOf( (byte)';' );
		if ( 1 > separator ) {
			return false;
		}
		int separatorIndex = indexStart + separator;
		if ( !TryParseIndex(
			bytes.ToArray(),
			indexStart,
			separatorIndex,
			out byte parsedIndex
		) || parsedIndex != expectedIndex ) {
			return false;
		}

		payload = bytes.Slice(
			separatorIndex + 1,
			end - separatorIndex - 1
		);
		return !payload.IsEmpty;
	}

	private static bool TryParseIndex(
		IReadOnlyList<byte> bytes,
		int start,
		int end,
		out byte index
	) {
		index = 0;
		if ( start >= end ) {
			return false;
		}

		int value = 0;
		for ( int offset = start; offset < end; ++offset ) {
			byte character = bytes[ offset ];
			if ( character < (byte)'0' || character > (byte)'9' ) {
				return false;
			}
			value = ( value * 10 ) + character - (byte)'0';
			if ( byte.MaxValue < value ) {
				return false;
			}
		}
		index = (byte)value;
		return true;
	}

	private sealed class TerminalOsc4ResponseMatcher : ITerminalResponseMatcher, ICorrelatedTerminalResponseMatcher {
		private readonly byte index;

		internal TerminalOsc4ResponseMatcher(
			byte index
		) {
			this.index = index;
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
					this.index
				);
		}

		public bool IsCorrelatedPrefix(
			IReadOnlyList<byte> bytes
		) {
			return HasCorrelatedPrefix(
				bytes,
				this.index
			);
		}
	}
}
