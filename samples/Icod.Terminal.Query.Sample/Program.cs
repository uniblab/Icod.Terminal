using System.Globalization;
using Icod.Terminal;

TimeSpan probeTimeout = TimeSpan.FromMilliseconds( 750 );

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

TerminalPresentationLease? presentation = null;
TerminalInputProtocolLease? inputProtocols = null;

try {
	TerminalControlResult<TerminalPresentationLease> presentationResult =
		await session.AcquirePresentationAsync(
			new TerminalPresentationOptions {
				CursorVisibility = TerminalCursorVisibility.Normal
			}
		);
	if ( presentationResult.IsAvailable ) {
		presentation = presentationResult.GetRequiredValue();
		await WriteLineAsync(
			session,
			"Presentation lease active while explicit probes run."
		);
	} else {
		await WriteLineAsync(
			session,
			"Presentation lease unavailable; probes will still run explicitly."
		);
	}

	TerminalControlResult<TerminalInputProtocolLease> inputResult =
		await session.AcquireInputProtocolsAsync(
			new TerminalInputProtocolOptions {
				BracketedPaste = true,
				FocusReporting = true
			}
		);
	if ( inputResult.IsAvailable ) {
		inputProtocols = inputResult.GetRequiredValue();
		await WriteLineAsync(
			session,
			"Rich-input lease active while explicit probes run."
		);
	} else {
		await WriteLineAsync(
			session,
			"Rich-input lease unavailable; probes will still run explicitly."
		);
	}

	await WriteLineAsync(
		session,
		"Opening the session performs no automatic interrogation."
	);
	await WriteLineAsync(
		session,
		"The following requests are issued only because this sample explicitly asks for them."
	);
	await WriteLineAsync(
		session,
		string.Empty
	);

	await ReportProbeAsync(
		session,
		"Primary DA",
		async () => {
			TerminalPrimaryDeviceAttributes value =
				await session.QueryPrimaryDeviceAttributesAsync( probeTimeout );
			string attributes = string.Join(
				",",
				value.Attributes.Select(
					static attribute => attribute.ToString(
						CultureInfo.InvariantCulture
					)
				)
			);
			return string.Concat(
				"device-code=",
				value.DeviceCode.ToString( CultureInfo.InvariantCulture ),
				" attributes=[",
				attributes,
				"]"
			);
		}
	);

	await ReportProbeAsync(
		session,
		"Secondary DA",
		async () => {
			TerminalSecondaryDeviceAttributes value =
				await session.QuerySecondaryDeviceAttributesAsync( probeTimeout );
			return string.Concat(
				"type=",
				value.TerminalTypeCode.ToString( CultureInfo.InvariantCulture ),
				" firmware=",
				value.FirmwareVersion.ToString( CultureInfo.InvariantCulture ),
				" option=",
				value.OptionCode.ToString( CultureInfo.InvariantCulture )
			);
		}
	);

	await ReportProbeAsync(
		session,
		"Device status",
		async () => {
			TerminalDeviceStatus value =
				await session.QueryDeviceStatusAsync( probeTimeout );
			return value.ToString();
		}
	);

	await ReportProbeAsync(
		session,
		"Cursor position",
		async () => {
			TerminalCursorPosition value =
				await session.QueryCursorPositionAsync( probeTimeout );
			return string.Concat(
				"row=",
				value.Row.ToString( CultureInfo.InvariantCulture ),
				" column=",
				value.Column.ToString( CultureInfo.InvariantCulture ),
				" (one-based)"
			);
		}
	);

	await ReportProbeAsync(
		session,
		"DECRQSS SGR",
		async () => {
			TerminalStatusStringResponse value = await session.QueryStatusStringAsync(
				TerminalStatusStringKind.SelectGraphicRendition,
				probeTimeout
			);
			return value.IsSupported
				? string.Concat(
					"supported status-string=\"",
					value.StatusString,
					"\""
				)
				: "unsupported"
			;
		}
	);

	await ReportProbeAsync(
		session,
		"XTGETTCAP TN",
		async () => {
			TerminalCapabilityObservation value =
				await session.QueryLiveCapabilityAsync(
					"TN",
					probeTimeout
				);
			if ( !value.IsSupported ) {
				return "unsupported";
			}

			IReadOnlyList<byte> bytes = value.ValueBytes
				?? throw new InvalidOperationException(
					"A supported live capability observation did not contain value bytes."
				);
			return string.Concat(
				"supported value-hex=",
				Convert.ToHexString( bytes.ToArray() )
			);
		}
	);

	await WriteLineAsync(
		session,
		string.Empty
	);
	await WriteLineAsync(
		session,
		"Generate one input or lifecycle event within 15 seconds to verify the unified event loop remains live."
	);

	TerminalEvent terminalEvent = await session.ReadEventAsync(
		TimeSpan.FromSeconds( 15 )
	);
	await WriteLineAsync(
		session,
		FormatEvent( terminalEvent )
	);
} finally {
	if ( inputProtocols is not null ) {
		await inputProtocols.DisposeAsync();
	}
	if ( presentation is not null ) {
		await presentation.DisposeAsync();
	}
}

return 0;

static async ValueTask ReportProbeAsync(
	TerminalSession session,
	string label,
	Func<Task<string>> probe
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentException.ThrowIfNullOrWhiteSpace( label );
	ArgumentNullException.ThrowIfNull( probe );

	try {
		string result = await probe();
		await WriteLineAsync(
			session,
			string.Concat(
				label,
				": ",
				result
			)
		);
	} catch ( TimeoutException ) {
		await WriteLineAsync(
			session,
			string.Concat(
				label,
				": timed out"
			)
		);
	} catch ( FormatException exception ) {
		await WriteLineAsync(
			session,
			string.Concat(
				label,
				": malformed correlated response: ",
				exception.Message
			)
		);
	} catch ( InvalidOperationException exception ) {
		await WriteLineAsync(
			session,
			string.Concat(
				label,
				": unavailable: ",
				exception.Message
			)
		);
	}
}

static string FormatEvent(
	TerminalEvent terminalEvent
) {
	ArgumentNullException.ThrowIfNull( terminalEvent );

	switch ( terminalEvent.Kind ) {
		case TerminalEventKind.Input:
			TerminalInputEvent input = terminalEvent.Input
				?? throw new InvalidOperationException(
					"An Input event did not carry an input payload."
				);
			return string.Concat(
				"Unified event loop: input kind=",
				input.Kind.ToString()
			);

		case TerminalEventKind.Lifecycle:
			TerminalLifecycleEvent lifecycle = terminalEvent.Lifecycle
				?? throw new InvalidOperationException(
					"A Lifecycle event did not carry a lifecycle payload."
				);
			return string.Concat(
				"Unified event loop: lifecycle kind=",
				lifecycle.Kind.ToString()
			);

		case TerminalEventKind.Timeout:
			return "Unified event loop: no event before timeout.";

		case TerminalEventKind.Cancelled:
			return "Unified event loop: wait cancelled.";

		default:
			throw new InvalidOperationException(
				$"Unexpected terminal event kind: {terminalEvent.Kind}."
			);
	}
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
