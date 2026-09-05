namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Encodes and parses the frozen T130 terminal-color specification grammar.
/// </summary>
internal static class TerminalColorCodec {
	internal const int CanonicalSpecificationLength = 18;
	internal const int MaximumSpecificationLength = 18;

	internal static byte[] Encode(
		TerminalColor color
	) {
		byte[] encoded = new byte[ CanonicalSpecificationLength ];
		Span<byte> destination = encoded;
		destination[ 0 ] = (byte)'r';
		destination[ 1 ] = (byte)'g';
		destination[ 2 ] = (byte)'b';
		destination[ 3 ] = (byte)':';
		WriteHex4( color.Red, destination[ 4..8 ] );
		destination[ 8 ] = (byte)'/';
		WriteHex4( color.Green, destination[ 9..13 ] );
		destination[ 13 ] = (byte)'/';
		WriteHex4( color.Blue, destination[ 14..18 ] );
		return encoded;
	}

	internal static string EncodeString(
		TerminalColor color
	) {
		return Encoding.ASCII.GetString( Encode( color ) );
	}

	internal static TerminalColor Parse(
		ReadOnlySpan<byte> specification
	) {
		if ( TryParse( specification, out TerminalColor color ) ) {
			return color;
		}

		throw new FormatException( "The terminal color specification is malformed or unsupported." );
	}

	internal static TerminalColor Parse(
		string specification
	) {
		ArgumentNullException.ThrowIfNull( specification );
		if ( specification.Length > MaximumSpecificationLength ) {
			throw new FormatException( "The terminal color specification is malformed or unsupported." );
		}

		Span<byte> bytes = stackalloc byte[ specification.Length ];
		for ( int index = 0; index < specification.Length; ++index ) {
			char value = specification[ index ];
			if ( 0x7f < value ) {
				throw new FormatException( "The terminal color specification must contain ASCII characters only." );
			}
			bytes[ index ] = (byte)value;
		}

		return Parse( bytes );
	}

	internal static bool TryParse(
		ReadOnlySpan<byte> specification,
		out TerminalColor color
	) {
		color = default;
		if ( specification.IsEmpty
			|| ( MaximumSpecificationLength < specification.Length ) ) {
			return false;
		}

		if ( (byte)'#' == specification[ 0 ] ) {
			return TryParseHash(
				specification,
				out color
			);
		}

		return TryParseRgb(
			specification,
			out color
		);
	}

	private static bool TryParseRgb(
		ReadOnlySpan<byte> specification,
		out TerminalColor color
	) {
		color = default;
		if ( 9 > specification.Length
			|| !AsciiEqualsIgnoreCase( specification[ 0 ], (byte)'r' )
			|| !AsciiEqualsIgnoreCase( specification[ 1 ], (byte)'g' )
			|| !AsciiEqualsIgnoreCase( specification[ 2 ], (byte)'b' )
			|| ( (byte)':' != specification[ 3 ] ) ) {
			return false;
		}

		ReadOnlySpan<byte> components = specification[ 4.. ];
		int firstSlash = components.IndexOf( (byte)'/' );
		if ( 1 > firstSlash ) {
			return false;
		}
		int secondRelativeSlash = components[ ( firstSlash + 1 ).. ].IndexOf( (byte)'/' );
		if ( 1 > secondRelativeSlash ) {
			return false;
		}
		int secondSlash = firstSlash + 1 + secondRelativeSlash;
		if ( components[ ( secondSlash + 1 ).. ].Contains( (byte)'/' ) ) {
			return false;
		}

		ReadOnlySpan<byte> red = components[ ..firstSlash ];
		ReadOnlySpan<byte> green = components[ ( firstSlash + 1 )..secondSlash ];
		ReadOnlySpan<byte> blue = components[ ( secondSlash + 1 ).. ];
		if ( red.Length != green.Length
			|| red.Length != blue.Length
			|| 1 > red.Length
			|| 4 < red.Length ) {
			return false;
		}

		if ( !TryReadHex( red, out ushort redValue )
			|| !TryReadHex( green, out ushort greenValue )
			|| !TryReadHex( blue, out ushort blueValue ) ) {
			return false;
		}

		int digits = red.Length;
		color = new TerminalColor(
			NormalizeRgbComponent( redValue, digits ),
			NormalizeRgbComponent( greenValue, digits ),
			NormalizeRgbComponent( blueValue, digits )
		);
		return true;
	}

	private static bool TryParseHash(
		ReadOnlySpan<byte> specification,
		out TerminalColor color
	) {
		color = default;
		int digits = specification.Length - 1;
		if ( 3 != digits
			&& 6 != digits
			&& 9 != digits
			&& 12 != digits ) {
			return false;
		}

		int componentDigits = digits / 3;
		ReadOnlySpan<byte> red = specification.Slice( 1, componentDigits );
		ReadOnlySpan<byte> green = specification.Slice( 1 + componentDigits, componentDigits );
		ReadOnlySpan<byte> blue = specification.Slice( 1 + ( 2 * componentDigits ), componentDigits );
		if ( !TryReadHex( red, out ushort redValue )
			|| !TryReadHex( green, out ushort greenValue )
			|| !TryReadHex( blue, out ushort blueValue ) ) {
			return false;
		}

		int shift = 16 - ( componentDigits * 4 );
		color = new TerminalColor(
			(ushort)( redValue << shift ),
			(ushort)( greenValue << shift ),
			(ushort)( blueValue << shift )
		);
		return true;
	}

	private static ushort NormalizeRgbComponent(
		ushort value,
		int digits
	) {
		return digits switch {
			1 => (ushort)( value * 0x1111 ),
			2 => (ushort)( value * 0x0101 ),
			3 => (ushort)( ( value << 4 ) | ( value >> 8 ) ),
			4 => value,
			_ => throw new ArgumentOutOfRangeException( nameof( digits ) )
		};
	}

	private static bool TryReadHex(
		ReadOnlySpan<byte> source,
		out ushort value
	) {
		value = 0;
		if ( source.IsEmpty || 4 < source.Length ) {
			return false;
		}

		foreach ( byte character in source ) {
			int digit = HexValue( character );
			if ( 0 > digit ) {
				value = 0;
				return false;
			}
			value = (ushort)( ( value << 4 ) | digit );
		}
		return true;
	}

	private static int HexValue(
		byte value
	) {
		if ( value >= (byte)'0' && value <= (byte)'9' ) {
			return value - (byte)'0';
		}
		if ( value >= (byte)'a' && value <= (byte)'f' ) {
			return 10 + value - (byte)'a';
		}
		if ( value >= (byte)'A' && value <= (byte)'F' ) {
			return 10 + value - (byte)'A';
		}
		return -1;
	}

	private static bool AsciiEqualsIgnoreCase(
		byte value,
		byte expectedLowercase
	) {
		return ( value == expectedLowercase )
			|| ( value == expectedLowercase - 32 );
	}

	private static void WriteHex4(
		ushort value,
		Span<byte> destination
	) {
		if ( 4 != destination.Length ) {
			throw new ArgumentException(
				"A four-byte destination is required.",
				nameof( destination )
			);
		}

		for ( int index = 3; 0 <= index; --index ) {
			int digit = value & 0x0f;
			if ( 10 > digit ) {
				destination[ index ] = (byte)( '0' + digit );
			} else {
				destination[ index ] = (byte)( 'a' + digit - 10 );
			}
			value >>= 4;
		}
	}
}
