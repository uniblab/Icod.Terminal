/*
	Icod.Terminal.Query.Sample
	Sample application demonstrating Icod.Terminal Query features.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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
		"Generate one terminal event within 15 seconds to verify queries, ordinary input, lifecycle, and semantic events share the unified reader."
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

		case TerminalEventKind.Semantic:
			TerminalSemanticEvent semantic = terminalEvent.Semantic
				?? throw new InvalidOperationException(
					"A Semantic event did not carry a semantic payload."
				);
			return FormatSemantic( semantic );

		case TerminalEventKind.Timeout:
			return "Unified event loop: no event before timeout.";

		case TerminalEventKind.Cancelled:
			return "Unified event loop: wait cancelled.";

		default:
			return string.Concat(
				"Unified event loop: event kind=",
				terminalEvent.Kind.ToString(),
				" is not recognized by this sample version."
			);
	}
}

static string FormatSemantic(
	TerminalSemanticEvent semantic
) {
	ArgumentNullException.ThrowIfNull( semantic );
	if ( TerminalSemanticEventKind.Notification == semantic.Kind ) {
		TerminalNotificationEvent notification = semantic.Notification
			?? throw new InvalidOperationException(
				"A Notification semantic event did not carry a notification payload."
			);
		string result = string.Concat(
			"Unified event loop: semantic notification kind=",
			notification.Kind.ToString(),
			" identifier=",
			notification.Identifier
		);
		if ( notification.ButtonNumber.HasValue ) {
			result = string.Concat(
				result,
				" button=",
				notification.ButtonNumber.Value.ToString( CultureInfo.InvariantCulture )
			);
		}
		return result;
	}

	return string.Concat(
		"Unified event loop: semantic kind=",
		semantic.Kind.ToString()
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