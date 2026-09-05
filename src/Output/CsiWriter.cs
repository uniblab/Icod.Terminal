namespace Icod.Terminal;

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
