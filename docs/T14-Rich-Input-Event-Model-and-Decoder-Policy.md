# T14 — Rich Input Event Model and Decoder Policy

**Project:** `Icod.Terminal`
**Development line:** `0.2.0`
**Development version:** `0.2.0-alpha.2`
**Tranche:** T14 — rich input event model and decoder policy
**Reference branch:** `0.2.0`
**Status:** Complete — `0.2.0-alpha.2` contract validated

---

## 1. Purpose

T14 freezes the public rich-input event vocabulary and the bounded decoder
configuration contract before mouse, focus, and bracketed-paste protocols are
enabled.

No second input loop is introduced. `TerminalEventKind.Input` remains the outer
session event category, and rich terminal input remains represented by
`TerminalInputEvent`.

Protocol enablement is deliberately deferred to T15. Focus and bracketed-paste
decoding begin in T16, and mouse decoding begins in T17.

---

## 2. Event envelope

`TerminalInputEventKind` is extended from the 0.1 keyboard contract to:

```text
Text
Key
Mouse
Focus
Paste
EndOfInput
```

Existing 0.1 properties remain intact. For the new event kinds,
`TerminalInputEvent` exposes one strongly typed nullable payload:

```text
Mouse -> TerminalMouseEvent
Focus -> TerminalFocusEvent
Paste -> TerminalPasteEvent
```

The legacy key fields remain neutral for rich protocol events. This lets
existing keyboard consumers continue to use the 0.1 surface while new callers
can branch on `Kind` and consume the matching payload.

T14 reserves the event model. The 0.2 decoder does not begin emitting these
new rich event kinds until their protocol implementation tranches.

---

## 3. Mouse contract

`TerminalMouseEvent` is protocol-neutral.

It carries:

- `TerminalMouseAction`;
- `TerminalMouseButton`;
- zero-based `Column`;
- zero-based `Row`;
- `TerminalKeyModifiers`.

The action contract is:

```text
Press
Release
Move
WheelUp
WheelDown
WheelLeft
WheelRight
```

The button contract is:

```text
None
Primary
Middle
Secondary
Button4
Button5
Button6
Button7
```

Press and release events must identify a button. A movement event may identify
the held button or `None`. Wheel direction is represented by the action, so
wheel events use `TerminalMouseButton.None`.

Wire-format coordinate bases and button codes are not public API. T17 will
normalize them before constructing `TerminalMouseEvent`.

`TerminalKeyModifiers` remains the shared Shift/Control/Alt modifier vocabulary
for traditional keyboard and mouse reports. T14 does not introduce modern
keyboard-protocol modifier states.

---

## 4. Focus contract

`TerminalFocusEvent` carries one `TerminalFocusState`:

```text
Focused
Unfocused
```

The public contract describes resulting state rather than preserving the raw
focus-in/focus-out escape sequence.

---

## 5. Bracketed-paste contract

Bracketed paste is framed input, not a synthetic series of ordinary keystrokes.

`TerminalPasteEvent` carries:

```text
Begin
Data
End
```

`Data` carries non-empty decoded text. `Begin` and `End` carry no text.

T16 will stream large pastes as multiple bounded `Data` events. The complete
paste is never required to exist in one decoder buffer or one managed string.

Paste text follows the same UTF-8 terminal-input interpretation as ordinary
text input. Invalid UTF-8 handling remains consistent with the existing
incremental decoder rather than becoming a second application-text encoding
policy.

While a bracketed paste is active, escape-looking bytes in the body are paste
content unless they complete the exact paste terminator.

---

## 6. Decoder policy

T14 introduces `TerminalInputDecoderOptions`, exposed from
`TerminalSessionOptions.InputDecoderOptions`.

The policy contains:

- `EscapeSequenceTimeout`;
- `MaximumBufferedBytes`;
- `PasteChunkBytes`.

The defaults preserve the 0.1 behavior:

```text
EscapeSequenceTimeout = TerminalSession.DefaultEscapeSequenceTimeout
MaximumBufferedBytes = TerminalSession.MaximumBufferedInputBytes
PasteChunkBytes = TerminalSession.MaximumBufferedInputBytes
```

`EscapeSequenceTimeout` may be zero but not negative.

`MaximumBufferedBytes` remains bounded between 4 and 4,096 bytes. T14 permits
callers to lower the 0.1 session bound but does not permit them to expand it.
Existing terminfo key sequences still fail deterministically if a configured
decoder limit is too small to represent them.

`PasteChunkBytes` is bounded between 1 and 1,048,576 bytes. T16 will use it as
the target maximum raw paste payload represented by one data chunk while still
allowing the tiny retained prefix required to complete a UTF-8 scalar or the
exact paste terminator.

---

## 7. Unknown sequences

T14 does not add a discard/ignore switch for unknown terminal sequences.

Until the query/response router is introduced in a later milestone, an
unrecognized escape-prefixed input remains subject to the existing deterministic
fallback: the Escape byte can become an Escape key event and following bytes
continue through ordinary decoding.

This preserves 0.1 behavior and avoids teaching the rich-input decoder to
silently consume future terminal responses before response routing exists.

---

## 8. Deferred policy

Mouse protocol preference and mouse tracking intensity belong to T15 because
they control reversible terminal state rather than byte decoding alone.

CSI-u, Kitty keyboard negotiation, and other negotiated modern keyboard
protocols remain outside `0.2.0`.

No raw enable/disable escape string becomes public configuration in T14.

---

## 9. Compatibility

Existing `Text`, `Key`, and `EndOfInput` semantics are unchanged.

The addition of new `TerminalInputEventKind` members is source compatible for
ordinary consumers, although callers using exhaustive switches must handle
future rich-input cases.

The existing `TerminalKey`, `TerminalKeyModifiers`, function-key numbering,
control-key behavior, UTF-8 fragmentation behavior, and Escape ambiguity
defaults remain unchanged.

---

## 10. Validation gate

T14 is complete when:

1. all three target frameworks build and test;
2. the new event payload types reject invalid semantic states;
3. decoder policy defaults reproduce the 0.1 decoder configuration;
4. invalid decoder bounds are rejected before a live session begins reading;
5. `TerminalSession` feeds the configured Escape timeout and buffer limit into
   the existing incremental decoder;
6. existing T08 decoder tests remain green;
7. package verification and fresh package consumers remain green on Windows,
   Linux, and macOS.

The next tranche is T15 — Reversible Input-Protocol Leases.
