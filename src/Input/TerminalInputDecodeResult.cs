namespace Icod.Terminal;

/// <summary>
/// Represents one unit of progress through the shared terminal input decoder.
/// </summary>
internal readonly struct TerminalInputDecodeResult {
	private TerminalInputDecodeResult(
		TerminalInputEvent? inputEvent,
		bool responseRouted
	) {
		if ( responseRouted == ( inputEvent is not null ) ) {
			throw new ArgumentException(
				"A terminal decode result must represent either application input or one routed response."
			);
		}

		this.InputEvent = inputEvent;
		this.ResponseRouted = responseRouted;
	}

	internal TerminalInputEvent? InputEvent {
		get;
	}

	internal bool ResponseRouted {
		get;
	}

	internal static TerminalInputDecodeResult FromInput(
		TerminalInputEvent inputEvent
	) {
		ArgumentNullException.ThrowIfNull( inputEvent );
		return new TerminalInputDecodeResult(
			inputEvent,
			responseRouted: false
		);
	}

	internal static TerminalInputDecodeResult RoutedResponse() {
		return new TerminalInputDecodeResult(
			null,
			responseRouted: true
		);
	}
}
