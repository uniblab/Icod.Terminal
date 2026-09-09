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
/// Builds and emits structurally validated seven-bit CSI frames for internal
/// semantic terminal operations.
/// </summary>
internal static class CsiWriter {
	private const byte EscapeByte = 0x1B;
	private const byte CsiFinalByte = 0x5B;

	/// <summary>
	/// Encodes one complete canonical seven-bit CSI frame from structural fields.
	/// </summary>
	internal static byte[] EncodeFrame(
		ReadOnlySpan<byte> parameterBytes,
		ReadOnlySpan<byte> intermediateBytes,
		byte finalByte
	) {
		ValidateParameterBytes( parameterBytes );
		ValidateIntermediateBytes( intermediateBytes );
		ValidateFinalByte( finalByte );

		int frameLength = checked(
			3 + parameterBytes.Length + intermediateBytes.Length
		);
		byte[] frame = new byte[ frameLength ];
		frame[ 0 ] = EscapeByte;
		frame[ 1 ] = CsiFinalByte;

		int offset = 2;
		parameterBytes.CopyTo( frame.AsSpan( offset ) );
		offset += parameterBytes.Length;
		intermediateBytes.CopyTo( frame.AsSpan( offset ) );
		offset += intermediateBytes.Length;
		frame[ offset ] = finalByte;
		return frame;
	}

	/// <summary>
	/// Encodes one complete canonical seven-bit CSI frame as a Latin-1 terminal string.
	/// </summary>
	internal static string EncodeFrameString(
		ReadOnlySpan<byte> parameterBytes,
		ReadOnlySpan<byte> intermediateBytes,
		byte finalByte
	) {
		return Encoding.Latin1.GetString(
			EncodeFrame(
				parameterBytes,
				intermediateBytes,
				finalByte
			)
		);
	}

	/// <summary>
	/// Encodes one DECSCUSR cursor-style frame for a frozen 0.8 parameter.
	/// </summary>
	internal static byte[] EncodeCursorStyleFrame(
		int parameter
	) {
		ValidateCursorStyleParameter( parameter );
		return EncodeFrame(
			[ (byte)( (byte)'0' + parameter ) ],
			[ (byte)' ' ],
			(byte)'q'
		);
	}

	/// <summary>
	/// Encodes one canonical DEC private-mode set/reset frame.
	/// </summary>
	internal static byte[] EncodeDecPrivateModeFrame(
		int mode,
		bool enabled
	) {
		if ( 0 >= mode ) {
			throw new ArgumentOutOfRangeException( nameof( mode ) );
		}

		byte[] parameterBytes = EncodePrefixedDecimalParameter(
			(byte)'?',
			mode
		);
		return EncodeFrame(
			parameterBytes,
			ReadOnlySpan<byte>.Empty,
			enabled ? (byte)'h' : (byte)'l'
		);
	}

	/// <summary>
	/// Encodes one canonical DEC private-mode set/reset frame as a terminal string.
	/// </summary>
	internal static string EncodeDecPrivateModeString(
		int mode,
		bool enabled
	) {
		return Encoding.Latin1.GetString(
			EncodeDecPrivateModeFrame(
				mode,
				enabled
			)
		);
	}

	/// <summary>
	/// Encodes Kitty's progressive-keyboard flags query.
	/// </summary>
	internal static byte[] EncodeKittyKeyboardQueryFrame() {
		return EncodeFrame(
			[ (byte)'?' ],
			ReadOnlySpan<byte>.Empty,
			(byte)'u'
		);
	}

	/// <summary>
	/// Encodes one Kitty progressive-keyboard push frame.
	/// </summary>
	internal static byte[] EncodeKittyKeyboardPushFrame(
		int flags
	) {
		if ( 0 > flags ) {
			throw new ArgumentOutOfRangeException( nameof( flags ) );
		}

		byte[] parameterBytes = EncodePrefixedDecimalParameter(
			(byte)'>',
			flags
		);
		return EncodeFrame(
			parameterBytes,
			ReadOnlySpan<byte>.Empty,
			(byte)'u'
		);
	}

	/// <summary>
	/// Encodes one Kitty progressive-keyboard push frame as a terminal string.
	/// </summary>
	internal static string EncodeKittyKeyboardPushString(
		int flags
	) {
		return Encoding.Latin1.GetString( EncodeKittyKeyboardPushFrame( flags ) );
	}

	/// <summary>
	/// Encodes Kitty's progressive-keyboard stack pop frame.
	/// </summary>
	internal static byte[] EncodeKittyKeyboardPopFrame() {
		return EncodeFrame(
			[ (byte)'<' ],
			ReadOnlySpan<byte>.Empty,
			(byte)'u'
		);
	}

	/// <summary>
	/// Encodes Kitty's progressive-keyboard stack pop frame as a terminal string.
	/// </summary>
	internal static string EncodeKittyKeyboardPopString() {
		return Encoding.Latin1.GetString( EncodeKittyKeyboardPopFrame() );
	}

	/// <summary>
	/// Encodes the canonical seven-bit synchronized-output begin frame.
	/// </summary>
	internal static byte[] EncodeSynchronizedOutputBeginFrame() {
		return EncodeDecPrivateModeFrame(
			2026,
			enabled: true
		);
	}

	/// <summary>
	/// Encodes the canonical seven-bit synchronized-output end frame.
	/// </summary>
	internal static byte[] EncodeSynchronizedOutputEndFrame() {
		return EncodeDecPrivateModeFrame(
			2026,
			enabled: false
		);
	}

	/// <summary>
	/// Emits one complete DECSCUSR cursor-style frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission commits. Once the complete
	/// frame has been validated and transmission begins, the underlying write is
	/// intentionally not caller-cancelled so the CSI frame is not truncated. This
	/// operation does not flush the output service.
	/// </remarks>
	internal static ValueTask WriteCursorStyleAsync(
		ITerminalOutput output,
		int parameter,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ValidateCursorStyleParameter( parameter );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeCursorStyleFrame( parameter );
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	/// <summary>
	/// Emits one complete synchronized-output begin frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission commits. The transport write
	/// is intentionally non-cancellable once emission begins. This operation does
	/// not flush the output service.
	/// </remarks>
	internal static ValueTask WriteSynchronizedOutputBeginAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeSynchronizedOutputBeginFrame();
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	/// <summary>
	/// Emits one complete synchronized-output end frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission commits. The transport write
	/// is intentionally non-cancellable once emission begins. Flush policy belongs
	/// to the synchronized-output state manager rather than this framing primitive.
	/// </remarks>
	internal static ValueTask WriteSynchronizedOutputEndAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeSynchronizedOutputEndFrame();
		cancellationToken.ThrowIfCancellationRequested();
		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	private static byte[] EncodePrefixedDecimalParameter(
		byte prefix,
		int value
	) {
		if ( prefix is < 0x3C or > 0x3F ) {
			throw new ArgumentOutOfRangeException( nameof( prefix ) );
		}
		if ( 0 > value ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}

		Span<byte> digits = stackalloc byte[ 10 ];
		int index = digits.Length;
		int remaining = value;
		do {
			int digit = remaining % 10;
			digits[ --index ] = checked( (byte)( (byte)'0' + digit ) );
			remaining /= 10;
		} while ( 0 < remaining );

		int digitCount = digits.Length - index;
		byte[] result = new byte[ digitCount + 1 ];
		result[ 0 ] = prefix;
		digits.Slice(
			index,
			digitCount
		).CopyTo( result.AsSpan( 1 ) );
		return result;
	}

	private static void ValidateParameterBytes(
		ReadOnlySpan<byte> parameterBytes
	) {
		for ( int index = 0; index < parameterBytes.Length; index++ ) {
			byte value = parameterBytes[ index ];
			if ( value is < 0x30 or > 0x3F ) {
				throw new ArgumentException(
					"CSI parameter bytes must be in the inclusive range 0x30 through 0x3F.",
					nameof( parameterBytes )
				);
			}
		}
	}

	private static void ValidateIntermediateBytes(
		ReadOnlySpan<byte> intermediateBytes
	) {
		for ( int index = 0; index < intermediateBytes.Length; index++ ) {
			byte value = intermediateBytes[ index ];
			if ( value is < 0x20 or > 0x2F ) {
				throw new ArgumentException(
					"CSI intermediate bytes must be in the inclusive range 0x20 through 0x2F.",
					nameof( intermediateBytes )
				);
			}
		}
	}

	private static void ValidateFinalByte(
		byte finalByte
	) {
		if ( finalByte is < 0x40 or > 0x7E ) {
			throw new ArgumentOutOfRangeException(
				nameof( finalByte ),
				finalByte,
				"A CSI final byte must be in the inclusive range 0x40 through 0x7E."
			);
		}
	}

	private static void ValidateCursorStyleParameter(
		int parameter
	) {
		if ( parameter is < 1 or > 6 ) {
			throw new ArgumentOutOfRangeException(
				nameof( parameter ),
				parameter,
				"The frozen 0.8 DECSCUSR parameter must be between 1 and 6."
			);
		}
	}
}
