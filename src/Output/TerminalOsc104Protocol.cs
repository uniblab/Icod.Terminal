namespace Icod.Terminal;

using System.Globalization;
using System.Text;

/// <summary>
/// Builds canonical OSC 104 indexed-palette reset frames.
/// </summary>
internal static class TerminalOsc104Protocol {
	internal static byte[] CreateResetAllFrame() {
		return [
			0x1b,
			(byte)']',
			(byte)'1',
			(byte)'0',
			(byte)'4',
			0x1b,
			(byte)'\\'
		];
	}

	internal static byte[] CreateResetFrame(
		byte index
	) {
		return CreateResetFrame( [ index ] );
	}

	internal static byte[] CreateResetFrame(
		IReadOnlyList<byte> indices
	) {
		ArgumentNullException.ThrowIfNull( indices );
		if ( 0 == indices.Count ) {
			throw new ArgumentException(
				"At least one palette index is required for an indexed OSC 104 reset.",
				nameof( indices )
			);
		}
		if ( 256 < indices.Count ) {
			throw new ArgumentException(
				"An indexed OSC 104 reset cannot contain more than 256 entries.",
				nameof( indices )
			);
		}

		bool[] seen = new bool[ 256 ];
		List<byte[]> encodedIndices = new( indices.Count );
		int payloadLength = 3;
		foreach ( byte index in indices ) {
			if ( seen[ index ] ) {
				throw new ArgumentException(
					$"Palette index {index} occurs more than once.",
					nameof( indices )
				);
			}
			seen[ index ] = true;
			byte[] encoded = Encoding.ASCII.GetBytes(
				index.ToString( CultureInfo.InvariantCulture )
			);
			encodedIndices.Add( encoded );
			payloadLength += 1 + encoded.Length;
		}

		byte[] frame = new byte[ 2 + payloadLength + 2 ];
		int offset = 0;
		frame[ offset++ ] = 0x1b;
		frame[ offset++ ] = (byte)']';
		frame[ offset++ ] = (byte)'1';
		frame[ offset++ ] = (byte)'0';
		frame[ offset++ ] = (byte)'4';
		foreach ( byte[] encoded in encodedIndices ) {
			frame[ offset++ ] = (byte)';';
			encoded.CopyTo( frame, offset );
			offset += encoded.Length;
		}
		frame[ offset++ ] = 0x1b;
		frame[ offset ] = (byte)'\\';
		return frame;
	}
}
