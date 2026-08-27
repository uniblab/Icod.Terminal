# T17 — Mouse Input

**Project:** `Icod.Terminal`
**Development line:** `0.2.0`
**Development version:** `0.2.0-alpha.5`
**Tranche:** T17 — mouse input
**Reference branch:** `0.2.0`
**Status:** Implementation prepared; validation gate pending

---

## 1. Purpose

T17 turns the mouse event contract reserved by T14 and the reversible mouse
tracking state implemented by T15 into live incremental input events.

Mouse reports remain inside the existing `TerminalInputDecoder` and
`TerminalSession.ReadEventAsync(...)` path. No mouse reader thread, auxiliary
stream, consumer-side escape parser, or second event loop is introduced.

The decoder recognizes the mouse wire family advertised by the selected
`Icod.TermInfo.TerminalDescription` through `StringCapability.KeyMouse`.

---

## 2. Supported wire families

T17 supports the two wire families accepted by the T15 protocol manager.

### 2.1 SGR mouse

An SGR profile advertises a `key_mouse` prefix beginning with:

```text
ESC [ <
```

Reports use:

```text
CSI < Cb ; Cx ; Cy M
CSI < Cb ; Cx ; Cy m
```

`M` represents press, motion, and wheel reports. Lower-case `m` represents an
explicit button release.

SGR is the preferred representation because coordinates are decimal values and
are not restricted to the legacy single-byte coordinate range.

### 2.2 Legacy xterm/X10-style mouse

A legacy profile advertises a `key_mouse` prefix beginning with:

```text
ESC [ M
```

The prefix is followed by three encoded bytes:

```text
Cb + 32
Cx + 32
Cy + 32
```

Coordinates are one-based on the wire and are therefore restricted to the
traditional 1 through 223 range.

Legacy release reports use button code 3 and do not identify the released
button. T17 resolves that protocol ambiguity using the most recently observed
pressed/held button. A release with no inferable button is not consumed as a
mouse event.

---

## 3. Capability boundary

T17 does not guess a mouse protocol from `TERM`, platform, or terminal name.

Incoming mouse decoding is installed only when `StringCapability.KeyMouse`
advertises one of the supported report prefixes.

This is intentionally a narrower requirement than acquiring a mouse reporting
lease. T15 still requires the complete reversible `XM` / `xm` / `key_mouse`
contract before it will enable mouse reporting. T17 needs only the advertised
incoming frame format in order to decode a report which is already present on
the input stream.

---

## 4. Semantic normalization

Mouse wire values are converted into the T14 `TerminalMouseEvent` contract.

Coordinates are converted from one-based protocol values to zero-based
terminal-cell coordinates:

```text
public column = Cx - 1
public row    = Cy - 1
```

The xterm modifier bits are normalized as:

```text
4   Shift
8   Alt
16  Control
```

The motion bit is:

```text
32
```

The supported button codes are normalized as:

```text
0     Primary
1     Middle
2     Secondary
128   Button4
129   Button5
130   Button6
131   Button7
```

Wheel codes are normalized semantically rather than exposed as button numbers:

```text
64   WheelUp
65   WheelDown
66   WheelLeft
67   WheelRight
```

Motion reports distinguish a held button from no-button motion. A motion code
whose button component is 3 becomes `Move` with `TerminalMouseButton.None`.

---

## 5. Incremental framing

Mouse parsing participates in the same bounded byte buffer used by keyboard,
focus, and paste decoding.

A fragmented mouse prefix is subject to the configured Escape ambiguity
interval. Once the complete advertised mouse prefix has been recognized, the
frame is considered an in-progress terminal protocol report and additional
bytes are awaited through the ordinary pending input read.

This preserves two existing guarantees:

- an isolated Escape or an abandoned partial prefix still resolves according to
  `EscapeSequenceTimeout`;
- a caller-level `TerminalSession.ReadEventAsync(...)` timeout does not discard
  a pending fragmented mouse frame.

The existing `MaximumBufferedBytes` limit remains authoritative. T17 introduces
no unbounded mouse-frame accumulation.

---

## 6. SGR numeric safety

SGR fields are parsed incrementally without converting arbitrary input into
unbounded strings.

The parser requires exactly three non-empty decimal fields and rejects integer
overflow. Both coordinates must be at least one before normalization.

Malformed or unsupported mouse-looking frames are not partially consumed as
mouse events. They fall back to the ordinary bounded input decoder, preserving
the existing unknown-sequence behavior instead of silently dropping bytes.

---

## 7. Interaction with focus and paste

Mouse parsing occurs in the same decoder ordering as the T16 protocols.

When bracketed paste is active, paste framing takes precedence over all mouse,
focus, key, and Escape interpretation. A byte sequence which looks exactly like
a mouse report inside a bracketed-paste body therefore remains paste data until
the exact paste terminator is encountered.

Outside paste state, mouse, focus, traditional key, text, and end-of-input
events remain individually ordered in the shared decoder stream. The outer
`TerminalSession` event loop continues to combine that stream with lifecycle,
timeout, and cancellation events.

---

## 8. Validation coverage

T17 adds in-memory tests for:

- byte-by-byte fragmented SGR reports;
- multiple SGR reports delivered by one transport read;
- one-based to zero-based coordinate normalization;
- SGR coordinates larger than the legacy byte range;
- Shift, Alt, and Control mouse modifiers;
- button press and explicit SGR release;
- button motion and no-button motion;
- vertical and horizontal wheel reports;
- additional buttons represented by the supported xterm codes;
- legacy coordinate boundaries;
- legacy release inference;
- malformed SGR fallback;
- Escape ambiguity for a fragmented mouse prefix;
- ordered interleaving of text, mouse, focus, and bracketed paste;
- mouse-looking content inside paste remaining paste data.

No test requires or modifies the process terminal.

---

## 9. Explicitly deferred

T17 does not add:

- UTF-8 extended legacy mouse encoding;
- urxvt 1015 mouse encoding;
- pixel-coordinate mouse reporting;
- terminal query/response routing;
- modern keyboard negotiation;
- consumer-specific DCurses mouse policy.

Additional historical mouse formats may be considered later only when they
materially improve compatibility without weakening the normalized public event
contract.

---

## 10. Completion gate

T17 is complete when:

1. `net8.0`, `net9.0`, and `net10.0` build and test;
2. SGR reports decode incrementally into normalized `TerminalMouseEvent`
   instances;
3. the supported legacy xterm/X10-style form decodes within its coordinate
   limits;
4. press, release, motion, wheel, modifiers, and supported additional buttons
   are represented semantically;
5. malformed frames remain bounded and deterministic;
6. paste state prevents mouse-looking payload from escaping into mouse events;
7. keyboard, mouse, focus, paste, and lifecycle behavior remain on the single
   `TerminalSession` event path;
8. package verification and fresh package consumers remain green on Windows,
   Linux, and macOS.

The next tranche is T18 — Traditional Keyboard Completeness.
