using System.Globalization;
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

TerminalInputProtocolLease? protocols = null;

try {
	TerminalControlResult<TerminalInputProtocolLease> protocolResult =
		await session.AcquireInputProtocolsAsync(
			new TerminalInputProtocolOptions {
				BracketedPaste = true,
				FocusReporting = true,
				MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
			}
		);

	if ( protocolResult.IsAvailable ) {
		protocols = protocolResult.GetRequiredValue();
		await WriteLineAsync(
			session,
			"Rich-input reporting enabled: bracketed paste, focus, and button mouse."
		);
	} else {
		await WriteLineAsync(
			session,
			string.Concat(
				"Rich-input reporting is ",
				protocolResult.Status.ToString(),
				string.IsNullOrWhiteSpace( protocolResult.Message )
					? "."
					: string.Concat(
						": ",
						protocolResult.Message
					)
			)
		);
		await WriteLineAsync(
			session,
			"The event loop remains usable for input supported without that lease."
		);
	}

	await WriteLineAsync(
		session,
		"Type text, use modified/navigation keys, click the mouse, change focus, or paste."
	);
	await WriteLineAsync(
		session,
		"Mouse coordinates below are zero-based terminal-cell coordinates."
	);
	await WriteLineAsync(
		session,
		"Press q, Q, or Escape to exit."
	);
	await WriteLineAsync(
		session,
		string.Empty
	);

	bool running = true;
	while ( running ) {
		TerminalEvent terminalEvent = await session.ReadEventAsync();

		switch ( terminalEvent.Kind ) {
			case TerminalEventKind.Input:
				TerminalInputEvent input = terminalEvent.Input
					?? throw new InvalidOperationException(
						"An Input event did not carry an input payload."
					);

				if ( ShouldExit( input ) ) {
					await WriteLineAsync(
						session,
						"Exit requested."
					);
					running = false;
					break;
				}

				await WriteLineAsync(
					session,
					FormatInput( input )
				);

				if ( TerminalInputEventKind.EndOfInput == input.Kind ) {
					running = false;
				}
				break;

			case TerminalEventKind.Lifecycle:
				TerminalLifecycleEvent lifecycle = terminalEvent.Lifecycle
					?? throw new InvalidOperationException(
						"A Lifecycle event did not carry a lifecycle payload."
					);

				await WriteLineAsync(
					session,
					FormatLifecycle( lifecycle )
				);

				if ( lifecycle.Kind is TerminalLifecycleEventKind.Interrupt
					or TerminalLifecycleEventKind.Termination ) {
					running = false;
				}
				break;

			case TerminalEventKind.Timeout:
				await WriteLineAsync(
					session,
					"Timeout"
				);
				break;

			case TerminalEventKind.Cancelled:
				await WriteLineAsync(
					session,
					"Cancelled"
				);
				running = false;
				break;

			default:
				throw new InvalidOperationException(
					$"Unexpected terminal event kind: {terminalEvent.Kind}."
				);
		}
	}
} finally {
	if ( protocols is not null ) {
		await protocols.DisposeAsync();
	}
}

return 0;

static bool ShouldExit(
	TerminalInputEvent input
) {
	ArgumentNullException.ThrowIfNull( input );

	if ( TerminalInputEventKind.Text == input.Kind
		&& input.Character.HasValue ) {
		int value = input.Character.Value.Value;
		if ( 'q' == value || 'Q' == value ) {
			return true;
		}
	}

	return TerminalInputEventKind.Key == input.Kind
		&& TerminalKey.Escape == input.Key;
}

static string FormatInput(
	TerminalInputEvent input
) {
	ArgumentNullException.ThrowIfNull( input );

	switch ( input.Kind ) {
		case TerminalInputEventKind.Text:
			return string.Concat(
				"Text character=\"",
				EscapeText(
					input.Character?.ToString()
						?? string.Empty
				),
				"\""
			);

		case TerminalInputEventKind.Key:
			return FormatKey( input );

		case TerminalInputEventKind.Mouse:
			TerminalMouseEvent mouse = input.Mouse
				?? throw new InvalidOperationException(
					"A Mouse input event did not carry a mouse payload."
				);
			return string.Concat(
				"Mouse action=",
				mouse.Action.ToString(),
				" button=",
				mouse.Button.ToString(),
				" column=",
				mouse.Column.ToString( CultureInfo.InvariantCulture ),
				" row=",
				mouse.Row.ToString( CultureInfo.InvariantCulture ),
				" modifiers=",
				mouse.Modifiers.ToString()
			);

		case TerminalInputEventKind.Focus:
			TerminalFocusEvent focus = input.Focus
				?? throw new InvalidOperationException(
					"A Focus input event did not carry a focus payload."
				);
			return string.Concat(
				"Focus state=",
				focus.State.ToString()
			);

		case TerminalInputEventKind.Paste:
			TerminalPasteEvent paste = input.Paste
				?? throw new InvalidOperationException(
					"A Paste input event did not carry a paste payload."
				);
			if ( TerminalPastePhase.Data == paste.Phase ) {
				string text = paste.Text
					?? throw new InvalidOperationException(
						"A Paste Data event did not carry text."
					);
				return string.Concat(
					"Paste phase=Data text=\"",
					EscapeText( text ),
					"\""
				);
			}

			return string.Concat(
				"Paste phase=",
				paste.Phase.ToString()
			);

		case TerminalInputEventKind.EndOfInput:
			return "EndOfInput";

		default:
			throw new InvalidOperationException(
				$"Unexpected terminal input kind: {input.Kind}."
			);
	}
}

static string FormatKey(
	TerminalInputEvent input
) {
	ArgumentNullException.ThrowIfNull( input );

	string keyName;
	if ( TerminalKey.Function == input.Key
		&& input.FunctionKeyNumber.HasValue ) {
		keyName = string.Concat(
			"F",
			input.FunctionKeyNumber.Value.ToString( CultureInfo.InvariantCulture )
		);
	} else {
		keyName = input.Key.ToString();
	}

	if ( input.Character.HasValue ) {
		return string.Concat(
			"Key key=",
			keyName,
			" character=\"",
			EscapeText( input.Character.Value.ToString() ),
			"\" modifiers=",
			input.Modifiers.ToString()
		);
	}

	return string.Concat(
		"Key key=",
		keyName,
		" modifiers=",
		input.Modifiers.ToString()
	);
}

static string FormatLifecycle(
	TerminalLifecycleEvent lifecycle
) {
	ArgumentNullException.ThrowIfNull( lifecycle );

	if ( lifecycle.Size.HasValue ) {
		return string.Concat(
			"Lifecycle kind=",
			lifecycle.Kind.ToString(),
			" size=",
			lifecycle.Size.Value.Columns.ToString( CultureInfo.InvariantCulture ),
			"x",
			lifecycle.Size.Value.Rows.ToString( CultureInfo.InvariantCulture )
		);
	}

	return string.Concat(
		"Lifecycle kind=",
		lifecycle.Kind.ToString()
	);
}

static string EscapeText(
	string text
) {
	ArgumentNullException.ThrowIfNull( text );

	return text
		.Replace(
			"\\",
			"\\\\",
			StringComparison.Ordinal
		)
		.Replace(
			"\u001b",
			"\\e",
			StringComparison.Ordinal
		)
		.Replace(
			"\r",
			"\\r",
			StringComparison.Ordinal
		)
		.Replace(
			"\n",
			"\\n",
			StringComparison.Ordinal
		)
		.Replace(
			"\t",
			"\\t",
			StringComparison.Ordinal
		);
}

static ValueTask WriteLineAsync(
	TerminalSession session,
	string text
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( text );

	return session.WriteTextAsync(
		string.Concat(
			text,
			"\r\n"
		)
	);
}
