# T16 — Focus and Bracketed Paste

**Project:** `Icod.Terminal`
**Development line:** `0.2.0`
**Development version:** `0.2.0-alpha.4`
**Tranche:** T16 — focus and bracketed-paste decoding
**Reference branch:** `0.2.0`
**Status:** Complete — `0.2.0-alpha.4` focus and bracketed-paste decoding validated

---

## 1. Purpose

T16 turns two event families reserved by T14 into live incremental decoder
behavior:

- terminal focus reports;
- bracketed-paste framing and data.

The work remains inside the existing `TerminalInputDecoder` and
`TerminalSession.ReadEventAsync(...)` path. No second input reader, paste
reader, focus side channel, or protocol-specific event loop is introduced.

T15 remains responsible for reversibly enabling and disabling the reporting
protocols. T16 is responsible only for recognizing the corresponding input
frames and producing the typed T14 events.

---

## 2. Capability-defined report markers

T16 does not hard-code the incoming focus or bracketed-paste frame markers.

The selected `Icod.TermInfo.TerminalDescription` supplies the extended
capabilities used by the decoder.

Focus decoding requires both:

```text
kxIN   focus-in report
kxOUT  focus-out report
```

Bracketed-paste decoding requires both:

```text
PS     paste start marker
PE     paste end marker
```

If either member of a marker pair is unavailable, that protocol is not added
to the decoder's recognized sequence set.

The T15 lease availability rules remain stricter because safely owning live
terminal state also requires the corresponding enable/disable capabilities:

```text
focus: fe + fd + kxIN + kxOUT
paste: BE + BD + PS + PE
```

This separation is intentional. T16 interprets incoming protocol bytes; T15
decides whether Terminal can safely request and later restore that protocol.

All terminfo input strings continue to use the reversible one-byte Latin-1
mapping already used for traditional terminfo key capabilities. A marker which
cannot fit inside the configured normal decoder buffer fails deterministically
during decoder construction rather than bypassing the configured bound.

---

## 3. Focus reports

Focus reports enter the same sequence matcher used by traditional terminal key
capabilities.

A recognized `kxIN` marker produces:

```text
TerminalInputEventKind.Focus
TerminalFocusState.Focused
```

A recognized `kxOUT` marker produces:

```text
TerminalInputEventKind.Focus
TerminalFocusState.Unfocused
```

The matcher remains incremental. Focus markers may arrive in one read or be
fragmented at any byte boundary.

Because focus reports normally begin with Escape, a partial focus prefix uses
the existing `EscapeSequenceTimeout`. If the continuation does not arrive
inside that bounded ambiguity interval, the leading Escape is reported using
the existing Escape-key fallback and the remaining bytes continue through the
ordinary decoder.

Focus reports may be adjacent to ordinary text, key sequences, paste frames,
or each other without changing event ordering.

---

## 4. Bracketed-paste state machine

Recognition of `PS` emits:

```text
TerminalInputEventKind.Paste
TerminalPastePhase.Begin
```

and enters paste state.

While paste state is active, ordinary key, focus, and Escape-sequence decoding
is suspended. Bytes are paste data unless they form the exact configured `PE`
marker.

This is required so content such as:

```text
ESC [ 31 m
ESC [ I
ESC [ A
```

remains literal pasted text rather than becoming color-control syntax, a focus
event, or an arrow key.

Recognition of the exact `PE` marker emits:

```text
TerminalInputEventKind.Paste
TerminalPastePhase.End
```

and returns the decoder to ordinary input processing.

A nested `PS` byte pattern inside paste data has no special meaning. The
bracketed-paste protocol itself supplies only one exact terminating marker.

---

## 5. Bounded terminator recognition

The complete paste is never accumulated in the ordinary decoder buffer.

When no complete `PE` marker is present, T16 retains only the longest trailing
suffix which could still become the configured terminator. Therefore the
special protocol-prefix retention required by paste is bounded by:

```text
PE.Length - 1
```

All bytes preceding that possible suffix are safe paste data and may be emitted
immediately.

This prevents a long paste from forcing the decoder to grow its normal
`MaximumBufferedBytes` storage simply because the terminator has not arrived
yet.

---

## 6. Paste chunking and UTF-8

`TerminalInputDecoderOptions.PasteChunkBytes` is now active decoder policy.

It is a target maximum number of raw paste bytes represented by one
`TerminalPastePhase.Data` event.

Data is decoded using the same UTF-8 interpretation as ordinary terminal input.
A data event never splits one UTF-8 scalar. When a scalar begins before the raw
chunk target and ends just after it, the data event may exceed the target by
the few bytes required to finish that scalar.

A fragmented scalar at the end of currently available paste data is retained
until enough bytes arrive to decide it.

Invalid UTF-8 uses the same replacement-scalar policy as the existing T08
fallback path rather than introducing a second application encoding.

`ApplicationEncoding` remains an output-text policy and does not control
terminal input decoding.

---

## 7. End-of-input during paste

An input endpoint may disconnect or reach EOF before an exact `PE` marker.

T16 does not synthesize a paste `End` event which the terminal never sent.

Instead:

1. any remaining bytes are emitted as paste `Data`, including a partial
   terminator prefix which can no longer complete;
2. incomplete UTF-8 at true end-of-input is replaced deterministically;
3. paste state is cleared;
4. the following decoder result is `TerminalInputEventKind.EndOfInput`.

This makes truncation observable without discarding already received paste
content or falsely claiming a normal protocol close.

---

## 8. Cancellation and event-loop behavior

T16 does not change the T08/T14 unified-reader cancellation contract.

`TerminalSession.ReadEventAsync(...)` caller timeout or cancellation continues
to leave the underlying terminal read pending so bytes belonging to a
fragmented key, focus marker, paste marker, UTF-8 scalar, or paste terminator
are preserved for the next call.

Decoder-internal lifecycle cancellation still terminates the pending read
during session shutdown.

A partial rich-input sequence beginning with Escape continues to use the same
bounded Escape ambiguity logic as traditional keys.

---

## 9. Mouse remains deferred

T16 does not decode mouse reports.

T15 may already own and enable a mouse-reporting lease, and T14 already defines
the normalized public mouse event contract, but actual SGR and legacy mouse
frame decoding remains T17.

This keeps the framed focus/paste state machine separate from the more complex
mouse parameter parser.

---

## 10. Validation gate

T16 is complete when:

1. `net8.0`, `net9.0`, and `net10.0` build and test;
2. focus-in and focus-out markers decode when fragmented at arbitrary byte
   boundaries;
3. focus events preserve ordering with adjacent ordinary input;
4. paste begin and end markers decode when arbitrarily fragmented;
5. escape-looking and focus-looking bytes inside paste remain paste data;
6. `PasteChunkBytes` bounds emitted raw data without splitting UTF-8 scalars;
7. a partial paste terminator is retained only as a bounded possible prefix;
8. cancellation can interrupt a wait for a fragmented terminator without
   converting that prefix into data;
9. truncated paste flushes remaining data and reports EOF without synthesizing
   `Paste.End`;
10. the existing T08 keyboard/UTF-8 decoder tests remain green;
11. package verification and fresh package consumers remain green on Windows,
    Linux, and macOS.

The next tranche is T17 — Mouse Input.
