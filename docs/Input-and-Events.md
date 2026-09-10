# Input and Events

This document is the permanent 1.x contract for terminal input decoding and the unified `TerminalSession` event stream.

Historical release notes and `Txxx` / `Exxx` records remain design evidence. When historical wording differs from this document because the pre-1.0 or later additive surface evolved, this document describes the supported 1.x contract.

## 1. One authoritative session reader

A live `TerminalSession` owns one authoritative path from terminal input bytes to decoded application events and correlated query responses.

The public session input path is:

```csharp
TerminalEvent terminalEvent = await session.ReadEventAsync(...);
```

Typed query methods share the same underlying input stream and response router.

`ITerminalInput` remains public so custom transports can be supplied when opening a session. A live session does not expose that borrowed input transport as a public property. Callers must not create a competing reader over the same transport while the session owns it.

This rule is normative. It prevents an external read from stealing bytes belonging to fragmented UTF-8, terminal keys, mouse/focus/paste frames, modern keyboard frames, active query responses, or unsolicited semantic reports.

## 2. Unified event model

`ReadEventAsync(...)` returns a `TerminalEvent` whose `Kind` is one of:

- `Input` — one decoded `TerminalInputEvent` is available;
- `Lifecycle` — one normalized lifecycle event is available;
- `Timeout` — the caller-supplied wait interval/deadline expired;
- `Cancelled` — the caller canceled this wait;
- `Semantic` — one validated protocol-neutral `TerminalSemanticEvent` is available.

Input, lifecycle, and semantic events remain logically distinct even though they can be consumed through one event loop.

Semantic events are application observations decoded from the same terminal byte stream as ordinary input. They therefore share the same authoritative reader and bounded application-event ordering domain. A semantic event is not delivered through a second callback, side channel, or competing reader.

When automatic lifecycle observation is enabled, `ReadEventAsync(...)` and `ReadLifecycleEventAsync(...)` consume the same lifecycle queue. Applications must not run independent competing lifecycle readers and expect each event to be duplicated.

### Consuming the event stream safely

Applications should handle the event kinds they understand explicitly while remaining tolerant of additive event kinds introduced by a later compatible minor release.

A typical event loop is:

```csharp
TerminalEvent terminalEvent = await session.ReadEventAsync();

switch ( terminalEvent.Kind ) {
	case TerminalEventKind.Input:
		TerminalInputEvent input = terminalEvent.Input
			?? throw new InvalidOperationException(
				"An Input event did not carry an input payload."
			);
		// Handle input.
		break;

	case TerminalEventKind.Lifecycle:
		TerminalLifecycleEvent lifecycle = terminalEvent.Lifecycle
			?? throw new InvalidOperationException(
				"A Lifecycle event did not carry a lifecycle payload."
			);
		// Handle lifecycle.
		break;

	case TerminalEventKind.Semantic:
		TerminalSemanticEvent semantic = terminalEvent.Semantic
			?? throw new InvalidOperationException(
				"A Semantic event did not carry a semantic payload."
			);
		// Handle the semantic families understood by this application.
		break;

	case TerminalEventKind.Timeout:
		// The caller-supplied wait expired.
		break;

	case TerminalEventKind.Cancelled:
		// The caller cancelled this wait.
		break;

	default:
		// A newer compatible library version may add another event kind.
		// Ignore, log, or surface it according to application policy.
		break;
}
```

For a known event kind, a missing corresponding payload indicates an invalid library/application state and may reasonably be treated as an error. By contrast, an unknown outer `TerminalEventKind` value should not normally crash a long-running event loop merely because the application is running against a newer compatible minor release.

The same forward-compatible principle applies inside semantic families: applications should handle the semantic kinds they understand and choose an explicit fallback policy for future additions rather than assuming the set can never grow within 1.x.

## 3. Ownership and routing precedence

Framed terminal traffic is assigned deterministically in this order:

```text
1. active query/response ownership
2. recognized unsolicited semantic-report ownership
3. ordinary application-input decoding
```

A frame claimed by an active query is not also published as a semantic event. A recognized unsolicited semantic report does not satisfy an unrelated query merely because it belongs to the same protocol family.

Recognition establishes bounded ownership, not trust. Malformed or oversized frames that are recognized as semantic-protocol candidates are consumed or recovered according to the bounded decoder rules rather than being leaked into ordinary application text.

## 4. Wait cancellation and timeout

Cancellation supplied to `ReadEventAsync(...)` cancels the caller's **wait**, not the underlying terminal read already preserving decoder state.

The method therefore returns `TerminalEventKind.Cancelled` rather than discarding partially accumulated input. A timeout likewise returns `TerminalEventKind.Timeout` without treating partially received bytes as abandoned data.

This behavior is deliberate. A terminal read may already contain the beginning of:

- a multi-byte UTF-8 scalar;
- an Escape-prefixed terminfo key;
- a mouse/focus/paste frame;
- a modern keyboard frame;
- a query response;
- an unsolicited semantic report.

Those bytes remain owned by the session and are available to subsequent reads or response correlation. An already-decoded queued semantic event likewise remains queued when a caller wait is canceled or times out.

This wait-cancellation rule is different from active terminal-query cancellation; query commit boundaries are documented in `Queries-and-Responses.md`.

## 5. Ordinary text

Ordinary terminal text is decoded as Unicode scalar values and returned as:

```text
TerminalInputEventKind.Text
TerminalKey.Character
Character = Rune
KeyPhase = null
```

The application text encoding used for output does not redefine input framing. Terminal input decoding uses the terminal protocol's UTF-8 byte stream semantics.

Malformed UTF-8 does not permit unbounded accumulation. Invalid byte sequences are converted to replacement-character text and decoding continues deterministically.

## 6. Traditional keyboard input

Traditional keyboard decoding is capability-driven from the selected `Icod.TermInfo.TerminalDescription`.

The decoder recognizes terminfo key capabilities for navigation, editing, function keys, and traditional modifier forms, and normalizes them into `TerminalKey`, `TerminalKeyModifiers`, and `FunctionKeyNumber`.

Traditional keyboard input remains available regardless of whether a modern keyboard protocol is negotiated.

### Escape ambiguity

An isolated Escape key is ambiguous with the prefix of many terminal sequences. The decoder therefore uses a bounded ambiguity interval.

The session default is exposed through:

```csharp
TerminalSession.DefaultEscapeSequenceTimeout
```

and may be configured per session through `TerminalInputDecoderOptions.EscapeSequenceTimeout`.

The timeout distinguishes an isolated Escape from an incomplete escape-prefixed sequence; it is not an application-wide input polling interval.

## 7. Rich input events

### Mouse

Mouse reports are normalized into `TerminalMouseEvent`.

Coordinates are zero-based terminal-cell coordinates. Actions are semantic (`Press`, `Release`, `Move`, and wheel directions) rather than raw protocol button codes.

Press/release events identify a button. Wheel direction is represented by the action and uses `TerminalMouseButton.None`. Mouse modifier reporting is limited to the modifier set carried by the supported mouse protocols.

### Focus

Focus reports become `TerminalFocusEvent` with either `Focused` or `Unfocused` state.

The library does not infer application focus from other terminal activity. A focus event represents an actual decoded terminal focus report while focus reporting is active.

### Bracketed paste

A bracketed paste is represented as a frame:

```text
Begin
Data...
End
```

`Begin` and `End` carry no text. Each `Data` event carries a non-empty decoded text chunk.

Data chunk boundaries are resource/decoder boundaries, not semantic text boundaries. Applications that need the complete pasted string must assemble the `Data` chunks between `Begin` and `End`.

`TerminalInputDecoderOptions.PasteChunkBytes` controls the target raw-byte size represented by a data chunk. The decoder may retain the small additional amount required to finish a UTF-8 scalar or exact paste terminator.

A paste is never accumulated into an unbounded single string by `Icod.Terminal`.

## 8. Unsolicited semantic events

`TerminalEventKind.Semantic` carries a protocol-neutral `TerminalSemanticEvent` through the same public `ReadEventAsync(...)` stream.

The first 1.9 semantic family is desktop-notification interaction. `TerminalSemanticEventKind.Notification` carries a `TerminalNotificationEvent` whose kinds are:

```text
Activated
ButtonActivated
Closed
CloseTrackingUnavailable
```

`Identifier` is the terminal-reported notification identifier. `ButtonNumber` is present only for `ButtonActivated` and is one-based.

`CloseTrackingUnavailable` is intentionally distinct from `Closed`. It means the terminal reports that reliable future close tracking is unavailable; the library does not fabricate a close event.

Semantic events are validated but unauthenticated terminal-controlled input. A terminal, multiplexer, remote endpoint, or hostile byte source can fabricate a syntactically valid event. Applications must not use semantic event receipt as an authentication or authorization boundary.

The first implementation recognizes bounded Kitty OSC 99 activation, button, close, and `untracked` reports. Raw OSC frames, selectors, backend identifiers, arbitrary metadata dictionaries, and a generic vendor-event payload are not exposed through the public semantic envelope.

## 9. Modern keyboard semantics

The public modern-keyboard model is semantic and terminal-independent.

`TerminalKeyEventPhase` distinguishes:

- `Press`;
- `Repeat`;
- `Release`.

Modern character-key events may additionally expose:

- `Character`;
- `ShiftedCharacter`;
- `BaseLayoutCharacter`;
- `AssociatedText`;
- expanded modifier flags.

Known functional identities map to `TerminalKey` values. A syntactically valid but unknown modern functional-key identity maps to `TerminalKey.Unrecognized`; raw private-use/vendor key numbers are not exposed as the public semantic identity.

Traditional events that cannot report a distinct event phase normalize as `Press` when represented as key events. Ordinary text events are not falsely labeled as key press/release events and therefore have `KeyPhase = null`.

## 10. Negotiated keyboard reporting

Modern keyboard reporting is requested through `TerminalInputProtocolOptions.KeyboardReportingMode` using semantic intensities:

- `Disambiguated`;
- `EventTypes`;
- `AllKeys`.

Kitty progressive keyboard reporting is the actively negotiated protocol used to satisfy these requests when supported.

The library does not enable modern reporting merely because a terminal brand, `TERM`, or environment variable suggests support. Support is queried before state is acquired.

xterm `modifyOtherKeys` is decode-only compatibility. `Icod.Terminal` does not blindly enable or disable it because arbitrary pre-existing state cannot be restored truthfully.

See `Modern-Keyboard-Security-and-Compatibility.md` for the permanent compatibility and data-exposure notes.

## 11. Reversible input-protocol ownership

Bracketed paste, focus reporting, mouse tracking, and negotiated keyboard reporting are requested through `AcquireInputProtocolsAsync(...)`.

Leases may overlap.

The supported composition rules are:

- bracketed paste remains active until the last requesting lease is released;
- focus reporting remains active until the last requesting lease is released;
- mouse tracking uses the strongest active request;
- modern keyboard reporting uses the strongest active request;
- releasing a stronger request restores the strongest remaining request;
- stale lease disposal after session-owned cleanup is safe and does not resurrect terminal state.

Acquisition is capability/observation driven. The library does not claim successful reversible ownership when the required activation/restoration path cannot be established.

During managed suspend, owned input-protocol state is released. Resume re-establishes the desired state only through the normal lifecycle/revalidation path.

Unsolicited notification reports are observations, not session-owned reversible terminal state. Notification requests are therefore not replayed on resume and identified notifications are not automatically closed on session disposal.

Already-decoded semantic events remain application observations across a suspend/resume cycle and are not replayed. Generation-scoped capability/state beliefs may be invalidated without retroactively invalidating observations already decoded from the byte stream.

## 12. Screen-local modern keyboard state

Kitty keyboard stack state is screen-local. Managed main/alternate-screen transitions therefore coordinate keyboard ownership with presentation transitions.

Conceptually the handoff is:

```text
pop keyboard state
switch screen
push requested keyboard state
```

This handoff is serialized against concurrent input-protocol and presentation mutation. Failure is surfaced rather than converted into a guessed believed state.

## 13. Parser resource bounds

Input decoding is incremental and bounded.

`TerminalInputDecoderOptions` controls:

- Escape ambiguity timeout;
- maximum undecoded buffered bytes;
- paste chunk target size.

`TerminalSession.MaximumBufferedInputBytes` is the public ceiling for undecoded input buffering. Protocol-specific query-response or semantic-report framing may impose a smaller limit.

Malformed or oversized framed input must not force indefinite buffer growth. The parser either consumes/rejects the malformed candidate deterministically or fails the active correlated response and performs bounded resynchronization where the protocol requires draining.

Semantic events use the same bounded coordinator/backpressure domain as ordinary application input. There is no unbounded semantic side queue.

## 14. End of input

`TerminalInputEventKind.EndOfInput` means the input endpoint reached end-of-input or disconnected in a way represented by the underlying byte service as a zero-length read.

It is an input event, not a lifecycle termination event, semantic event, or caller cancellation. A semantic event decoded before end-of-input remains ordered before the subsequent `EndOfInput` event.

## 15. Lifecycle, disposal, and late responses

Caller cancellation and timeout affect only the current `ReadEventAsync(...)` wait. Session disposal is different: it cancels session-lifetime input machinery so a pending read can terminate.

Suspend interrupts active query callers according to the query lifecycle contract while preserving already-decoded application events. Resume does not replay semantic observations or outbound notification requests.

Late correlated query responses retain bounded query ownership after caller timeout/cancellation. They are not reclassified as unsolicited semantic events merely because the original caller has already completed.

## 16. Security and privacy

Keyboard, paste, mouse, focus, and semantic input are application data from an external terminal source.

Modern keyboard associated text and alternate key identities can reveal more context than traditional terminal key reporting. Applications should not log, persist, or transmit these fields unless required for their functionality.

Bracketed paste marks paste boundaries; it does not make pasted content trusted. Applications remain responsible for treating pasted text as untrusted user input where appropriate.

Typed semantic notification events validate grammar and resource bounds but do not prove a desktop user interacted with a trusted notification. Identifiers and button numbers remain untrusted terminal-controlled data.

## 17. Explicit non-contracts

The 1.x input model does not promise:

- a second raw-reader API alongside the session decoder;
- a second semantic-event reader or callback delivery path;
- raw escape/OSC-frame events;
- raw Kitty private-use key codes;
- arbitrary future vendor event dictionaries;
- global hotkeys or OS keyboard hooks;
- scan-code or IME control;
- unbounded paste or semantic-event assembly;
- terminal-emulator behavior.

New protocol support must preserve the same single-reader, bounded-decoder, semantic-normalization, deterministic ownership, and truthful-state rules.