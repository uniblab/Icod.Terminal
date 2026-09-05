namespace Icod.Terminal;

internal static partial class OscWriter {
	private static readonly byte[] Osc133PromptStartFrame = CreateOsc133MarkerFrame(
		(byte)'A'
	);
	private static readonly byte[] Osc133CommandInputStartFrame = CreateOsc133MarkerFrame(
		(byte)'B'
	);
	private static readonly byte[] Osc133CommandOutputStartFrame = CreateOsc133MarkerFrame(
		(byte)'C'
	);
	private static readonly byte[] Osc133CommandAbortedFrame = CreateOsc133MarkerFrame(
		(byte)'D'
	);

	internal static byte[] EncodeOsc133PromptStartFrame() {
		return Osc133PromptStartFrame.ToArray();
	}

	internal static byte[] EncodeOsc133CommandInputStartFrame() {
		return Osc133CommandInputStartFrame.ToArray();
	}

	internal static byte[] EncodeOsc133CommandOutputStartFrame() {
		return Osc133CommandOutputStartFrame.ToArray();
	}

	internal static byte[] EncodeOsc133CommandFinishedFrame(
		byte exitStatus
	) {
		Span<byte> digits = stackalloc byte[ 3 ];
		int digitCount = WriteDecimalByte(
			exitStatus,
			digits
		);
		byte[] frame = new byte[ 10 + digitCount ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		frame[ 2 ] = (byte)'1';
		frame[ 3 ] = (byte)'3';
		frame[ 4 ] = (byte)'3';
		frame[ 5 ] = (byte)';';
		frame[ 6 ] = (byte)'D';
		frame[ 7 ] = (byte)';';
		digits[ ..digitCount ].CopyTo(
			frame.AsSpan( 8, digitCount )
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	internal static byte[] EncodeOsc133CommandAbortedFrame() {
		return Osc133CommandAbortedFrame.ToArray();
	}

	internal static ValueTask WriteOsc133PromptStartAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc133FrameAsync(
			output,
			Osc133PromptStartFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc133CommandInputStartAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc133FrameAsync(
			output,
			Osc133CommandInputStartFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc133CommandOutputStartAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc133FrameAsync(
			output,
			Osc133CommandOutputStartFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc133CommandFinishedAsync(
		ITerminalOutput output,
		byte exitStatus,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc133FrameAsync(
			output,
			EncodeOsc133CommandFinishedFrame( exitStatus ),
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc133CommandAbortedAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc133FrameAsync(
			output,
			Osc133CommandAbortedFrame,
			cancellationToken
		);
	}

	private static byte[] CreateOsc133MarkerFrame(
		byte marker
	) {
		return [
			0x1b,
			(byte)']',
			(byte)'1',
			(byte)'3',
			(byte)'3',
			(byte)';',
			marker,
			0x1b,
			(byte)'\\'
		];
	}

	private static async ValueTask WriteOsc133FrameAsync(
		ITerminalOutput output,
		ReadOnlyMemory<byte> frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();
		if ( frame.IsEmpty ) {
			throw new ArgumentException(
				"An OSC 133 frame cannot be empty.",
				nameof( frame )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();

		await output.WriteAsync(
			frame,
			CancellationToken.None
		).ConfigureAwait( false );
	}

	private static int WriteDecimalByte(
		byte value,
		Span<byte> destination
	) {
		if ( 100 <= value ) {
			destination[ 0 ] = (byte)( '0' + ( value / 100 ) );
			destination[ 1 ] = (byte)( '0' + ( ( value / 10 ) % 10 ) );
			destination[ 2 ] = (byte)( '0' + ( value % 10 ) );
			return 3;
		}
		if ( 10 <= value ) {
			destination[ 0 ] = (byte)( '0' + ( value / 10 ) );
			destination[ 1 ] = (byte)( '0' + ( value % 10 ) );
			return 2;
		}

		destination[ 0 ] = (byte)( '0' + value );
		return 1;
	}
}
