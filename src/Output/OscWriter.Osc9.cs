namespace Icod.Terminal;

/// <summary>
/// Identifies the OSC 9;4 wire progress states used internally by the terminal-progress subsystem.
/// </summary>
internal enum Osc9ProgressState {
	Clear = 0,
	Normal = 1,
	Error = 2,
	Indeterminate = 3,
	Attention = 4
}

internal static partial class OscWriter {
	private static readonly byte[] Osc9ProgressPrefix = [
		0x1b,
		(byte)']',
		(byte)'9',
		(byte)';',
		(byte)'4',
		(byte)';'
	];

	/// <summary>
	/// Encodes one complete canonical OSC 9;4 progress frame using BEL termination.
	/// </summary>
	internal static byte[] EncodeOsc9ProgressFrame(
		Osc9ProgressState state,
		int progress
	) {
		ValidateOsc9Progress(
			state,
			progress
		);

		int digitCount = progress switch {
			100 => 3,
			>= 10 => 2,
			_ => 1
		};
		byte[] frame = new byte[ 9 + digitCount ];
		Osc9ProgressPrefix.CopyTo(
			frame,
			0
		);
		frame[ 6 ] = (byte)( (byte)'0' + (int)state );
		frame[ 7 ] = (byte)';';
		WriteDecimalProgress(
			progress,
			frame.AsSpan( 8, digitCount )
		);
		frame[ ^1 ] = 0x07;
		return frame;
	}

	/// <summary>
	/// Validates and emits one complete canonical OSC 9;4 progress frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once the complete
	/// frame has been constructed and transmission begins, the underlying write is not
	/// caller-cancellable so ordinary cancellation cannot deliberately truncate the
	/// OSC control string. This operation does not flush the output service.
	/// </remarks>
	internal static ValueTask WriteOsc9ProgressAsync(
		ITerminalOutput output,
		Osc9ProgressState state,
		int progress,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc9ProgressFrame(
			state,
			progress
		);
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	private static void ValidateOsc9Progress(
		Osc9ProgressState state,
		int progress
	) {
		if ( !Enum.IsDefined( state ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( state ),
				state,
				"OSC 9;4 progress state must be one of clear, normal, error, indeterminate, or attention."
			);
		}
		if ( 0 > progress || 100 < progress ) {
			throw new ArgumentOutOfRangeException(
				nameof( progress ),
				progress,
				"OSC 9;4 determinate progress must be between 0 and 100 inclusive."
			);
		}
		if ( Osc9ProgressState.Clear == state
			&& 0 != progress ) {
			throw new ArgumentOutOfRangeException(
				nameof( progress ),
				progress,
				"OSC 9;4 clear progress is emitted canonically with progress 0."
			);
		}
		if ( Osc9ProgressState.Indeterminate == state
			&& 0 != progress ) {
			throw new ArgumentOutOfRangeException(
				nameof( progress ),
				progress,
				"OSC 9;4 indeterminate progress is emitted canonically with progress 0."
			);
		}
	}

	private static void WriteDecimalProgress(
		int progress,
		Span<byte> destination
	) {
		if ( 100 == progress ) {
			destination[ 0 ] = (byte)'1';
			destination[ 1 ] = (byte)'0';
			destination[ 2 ] = (byte)'0';
			return;
		}
		if ( 10 <= progress ) {
			destination[ 0 ] = (byte)( (byte)'0' + ( progress / 10 ) );
			destination[ 1 ] = (byte)( (byte)'0' + ( progress % 10 ) );
			return;
		}

		destination[ 0 ] = (byte)( (byte)'0' + progress );
	}
}
