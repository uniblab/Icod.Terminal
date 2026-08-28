namespace Icod.Terminal;

/// <summary>
/// Identifies the framing family expected for one terminal response.
/// </summary>
internal enum TerminalResponseFrameKind {
	Csi,
	Dcs
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
			return TerminalResponseFrameKind.Csi == this.Kind
				? 0x9B == this.bytes[ 0 ]
				: 0x90 == this.bytes[ 0 ]
			;
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

	internal bool TrySetResult(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		return this.completion.TrySetResult( frame );
	}

	internal bool TrySetCanceled() {
		return this.completion.TrySetCanceled();
	}
}
