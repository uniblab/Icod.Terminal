namespace Icod.Terminal;

/// <summary>
/// Identifies the framing family expected for one terminal response.
/// </summary>
internal enum TerminalResponseFrameKind {
	Csi,
	Dcs,
	Osc
}

/// <summary>
/// Matches one completely framed terminal response.
/// </summary>
internal interface ITerminalResponseMatcher {
	TerminalResponseFrameKind FrameKind {
		get;
	}

	bool IsMatch(
		TerminalResponseFrame frame
	);
}

/// <summary>
/// Identifies a response matcher which can claim a structurally correlated
/// response prefix before complete semantic parsing is possible.
/// </summary>
internal interface ICorrelatedTerminalResponseMatcher {
	bool IsCorrelatedPrefix(
		IReadOnlyList<byte> bytes
	);
}

/// <summary>
/// Represents one complete response frame retained as exact terminal bytes.
/// </summary>
internal sealed class TerminalResponseFrame {
	private readonly byte[] bytes;

	internal TerminalResponseFrame(
		TerminalResponseFrameKind kind,
		byte[] bytes
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"The terminal response frame kind is not recognized."
			);
		}
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 0 == bytes.Length ) {
			throw new ArgumentException(
				"A terminal response frame cannot be empty.",
				nameof( bytes )
			);
		}

		this.Kind = kind;
		this.bytes = bytes.ToArray();
	}

	internal TerminalResponseFrameKind Kind {
		get;
	}

	internal ReadOnlyMemory<byte> Bytes {
		get {
			return this.bytes;
		}
	}

	internal bool UsesEightBitIntroducer {
		get {
			return this.Kind switch {
				TerminalResponseFrameKind.Csi => 0x9B == this.bytes[ 0 ],
				TerminalResponseFrameKind.Dcs => 0x90 == this.bytes[ 0 ],
				TerminalResponseFrameKind.Osc => 0x9D == this.bytes[ 0 ],
				_ => throw new InvalidOperationException(
					"The terminal response frame kind is not recognized."
				)
			};
		}
	}
}

/// <summary>
/// Owns one internal response expectation without exposing routing state publicly.
/// </summary>
internal sealed class TerminalResponseExpectation {
	private readonly TaskCompletionSource<TerminalResponseFrame> completion = new(
		TaskCreationOptions.RunContinuationsAsynchronously
	);

	private int protectedBufferedBytes;
	private int armed;

	internal TerminalResponseExpectation(
		ITerminalResponseMatcher matcher
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		this.Matcher = matcher;
	}

	internal ITerminalResponseMatcher Matcher {
		get;
	}

	internal Task<TerminalResponseFrame> Response {
		get {
			return this.completion.Task;
		}
	}

	internal bool IsArmed {
		get {
			return 0 != Volatile.Read( ref this.armed );
		}
	}

	internal int ProtectedBufferedBytes {
		get {
			return Volatile.Read( ref this.protectedBufferedBytes );
		}
	}

	internal void Arm(
		int protectedBufferedBytes
	) {
		if ( 0 > protectedBufferedBytes ) {
			throw new ArgumentOutOfRangeException( nameof( protectedBufferedBytes ) );
		}
		if ( this.IsArmed ) {
			throw new InvalidOperationException(
				"The terminal response expectation is already armed."
			);
		}

		Volatile.Write(
			ref this.protectedBufferedBytes,
			protectedBufferedBytes
		);
		Volatile.Write( ref this.armed, 1 );
	}

	internal void ConsumeProtectedBytes(
		int count
	) {
		if ( 0 > count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}
		int protectedBytes = Volatile.Read( ref this.protectedBufferedBytes );
		if ( 0 == count || 0 == protectedBytes ) {
			return;
		}

		Volatile.Write(
			ref this.protectedBufferedBytes,
			Math.Max(
				0,
				protectedBytes - count
			)
		);
	}

	internal bool TrySetResult(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		return this.completion.TrySetResult( frame );
	}

	internal bool TrySetException(
		Exception exception
	) {
		ArgumentNullException.ThrowIfNull( exception );
		return this.completion.TrySetException( exception );
	}

	internal bool TrySetCanceled() {
		return this.completion.TrySetCanceled();
	}
}
