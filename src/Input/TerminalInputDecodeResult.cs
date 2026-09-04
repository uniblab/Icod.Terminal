namespace Icod.Terminal;

/// <summary>
/// Represents one unit of progress through the shared terminal input decoder.
/// </summary>
internal readonly struct TerminalInputDecodeResult {
	private readonly TerminalResponseExpectation? responseExpectation;
	private readonly TerminalResponseFrame? responseFrame;
	private readonly Exception? responseException;

	private TerminalInputDecodeResult(
		TerminalInputEvent? inputEvent,
		TerminalResponseExpectation? responseExpectation,
		TerminalResponseFrame? responseFrame,
		Exception? responseException
	) {
		bool responseRouted = responseExpectation is not null;
		if ( responseRouted == ( inputEvent is not null )
			|| !responseRouted && ( responseFrame is not null || responseException is not null )
			|| responseRouted && ( responseFrame is null ) == ( responseException is null ) ) {
			throw new ArgumentException(
				"A terminal decode result must represent either application input or one routed response."
			);
		}

		this.InputEvent = inputEvent;
		this.responseExpectation = responseExpectation;
		this.responseFrame = responseFrame;
		this.responseException = responseException;
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
		if ( this.responseExpectation is null ) {
			throw new InvalidOperationException(
				"The terminal decode result does not contain a routed response."
			);
		}

		if ( this.responseFrame is not null ) {
			this.responseExpectation.TrySetResult( this.responseFrame );
			return;
		}
		if ( this.responseException is not null ) {
			this.responseExpectation.TrySetException( this.responseException );
			return;
		}

		throw new InvalidOperationException(
			"The routed terminal response contains neither a frame nor a failure."
		);
	}

	internal static TerminalInputDecodeResult FromInput(
		TerminalInputEvent inputEvent
	) {
		ArgumentNullException.ThrowIfNull( inputEvent );
		return new TerminalInputDecodeResult(
			inputEvent,
			responseExpectation: null,
			responseFrame: null,
			responseException: null
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
			frame,
			responseException: null
		);
	}

	internal static TerminalInputDecodeResult RoutedFailure(
		TerminalResponseExpectation expectation,
		Exception exception
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		ArgumentNullException.ThrowIfNull( exception );
		return new TerminalInputDecodeResult(
			inputEvent: null,
			expectation,
			responseFrame: null,
			exception
		);
	}
}
