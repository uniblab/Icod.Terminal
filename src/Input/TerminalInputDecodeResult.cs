namespace Icod.Terminal;

/// <summary>
/// Represents one unit of progress through the shared terminal input decoder.
/// </summary>
internal readonly struct TerminalInputDecodeResult {
	private readonly TerminalResponseExpectation? responseExpectation;
	private readonly TerminalResponseFrame? responseFrame;

	private TerminalInputDecodeResult(
		TerminalInputEvent? inputEvent,
		TerminalResponseExpectation? responseExpectation,
		TerminalResponseFrame? responseFrame
	) {
		bool responseRouted = responseExpectation is not null
			|| responseFrame is not null;
		if ( ( responseExpectation is null ) != ( responseFrame is null )
			|| responseRouted == ( inputEvent is not null ) ) {
			throw new ArgumentException(
				"A terminal decode result must represent either application input or one routed response."
			);
		}

		this.InputEvent = inputEvent;
		this.responseExpectation = responseExpectation;
		this.responseFrame = responseFrame;
	}

	internal TerminalInputEvent? InputEvent {
		get;
	}

	internal bool ResponseRouted {
		get {
			return this.responseExpectation is not null;
		}
	}

	internal void CompleteRoutedResponse() {
		if ( this.responseExpectation is null || this.responseFrame is null ) {
			throw new InvalidOperationException(
				"The terminal decode result does not contain a routed response."
			);
		}

		this.responseExpectation.TrySetResult( this.responseFrame );
	}

	internal static TerminalInputDecodeResult FromInput(
		TerminalInputEvent inputEvent
	) {
		ArgumentNullException.ThrowIfNull( inputEvent );
		return new TerminalInputDecodeResult(
			inputEvent,
			responseExpectation: null,
			responseFrame: null
		);
	}

	internal static TerminalInputDecodeResult RoutedResponse(
		TerminalResponseExpectation expectation,
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		ArgumentNullException.ThrowIfNull( frame );
		return new TerminalInputDecodeResult(
			inputEvent: null,
			expectation,
			frame
		);
	}
}
