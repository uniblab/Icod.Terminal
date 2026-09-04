namespace Icod.Terminal;

using System.Text;

internal static partial class OscWriter {
	private static readonly byte[] Osc52Prefix = [
		0x1b,
		(byte)']',
		(byte)'5',
		(byte)'2',
		(byte)';'
	];

	/// <summary>
	/// Encodes one complete canonical OSC 52 write frame.
	/// </summary>
	internal static byte[] EncodeOsc52WriteFrame(
		TerminalOsc52Selection selection,
		ReadOnlySpan<byte> payload
	) {
		byte selectionByte = TerminalOsc52SelectionEncoder.Encode( selection );
		int frameLength = TerminalOsc52PayloadCodec.GetWriteFrameLength( payload.Length );
		string encodedPayload = TerminalOsc52PayloadCodec.Encode( payload );
		byte[] frame = new byte[ frameLength ];

		Osc52Prefix.CopyTo( frame, 0 );
		frame[ 5 ] = selectionByte;
		frame[ 6 ] = (byte)';';
		if ( 0 < encodedPayload.Length ) {
			Encoding.ASCII.GetBytes(
				encodedPayload.AsSpan(),
				frame.AsSpan( 7, encodedPayload.Length )
			);
		}
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	/// <summary>
	/// Encodes one complete canonical OSC 52 query frame.
	/// </summary>
	internal static byte[] EncodeOsc52QueryFrame(
		TerminalOsc52Selection selection
	) {
		byte selectionByte = TerminalOsc52SelectionEncoder.Encode( selection );
		return [
			0x1b,
			(byte)']',
			(byte)'5',
			(byte)'2',
			(byte)';',
			selectionByte,
			(byte)';',
			(byte)'?',
			0x1b,
			(byte)'\\'
		];
	}

	/// <summary>
	/// Validates and emits one complete canonical OSC 52 write frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once the complete
	/// frame has been validated and transmission begins, the underlying write is not
	/// caller-cancellable so ordinary cancellation cannot deliberately abandon an OSC
	/// control string halfway through. This operation does not flush the output service.
	/// </remarks>
	internal static ValueTask WriteOsc52Async(
		ITerminalOutput output,
		TerminalOsc52Selection selection,
		ReadOnlyMemory<byte> payload,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		_ = TerminalOsc52SelectionEncoder.Encode( selection );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc52WriteFrame(
			selection,
			payload.Span
		);
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	/// <summary>
	/// Validates and emits one complete canonical OSC 52 query frame through one output write.
	/// </summary>
	/// <remarks>
	/// This primitive emits only the request frame. T55 and T57 own response routing,
	/// transaction registration, deadline handling, and any flush required by the
	/// conversational query substrate.
	/// </remarks>
	internal static ValueTask WriteOsc52QueryAsync(
		ITerminalOutput output,
		TerminalOsc52Selection selection,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		_ = TerminalOsc52SelectionEncoder.Encode( selection );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc52QueryFrame( selection );
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}
}
