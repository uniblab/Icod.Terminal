# T15 — Reversible Input-Protocol Leases

**Project:** `Icod.Terminal`
**Development line:** `0.2.0`
**Development version:** `0.2.0-alpha.3`
**Tranche:** T15 — reversible input-protocol leases
**Reference branch:** `0.2.0`
**Status:** Implementation prepared; validation gate pending

---

## 1. Purpose

T15 adds session-owned reversible terminal state for the rich-input protocols
reserved by T14.

The tranche does not yet decode focus reports, bracketed-paste frames, or mouse
reports. It establishes the mechanism which safely requests those reports from
a live terminal and guarantees that the request is removed again during lease
release, suspension, session disposal, and failed transitions.

The public entry point is:

```text
TerminalSession.AcquireInputProtocolsAsync(...)
```

which returns a `TerminalInputProtocolLease` inside the existing
`TerminalControlResult<T>` availability contract.

---

## 2. Public request model

`TerminalInputProtocolOptions` can request any combination of:

```text
BracketedPaste
FocusReporting
MouseTrackingMode
```

`MouseTrackingMode` is nullable. When supplied, it uses one of:

```text
ButtonEvents
ButtonMotion
AnyMotion
```

An empty request is invalid.

Leases compose rather than replace each other. Bracketed paste and focus use
first-owner/last-owner semantics. Mouse tracking uses the strongest active
request:

```text
AnyMotion > ButtonMotion > ButtonEvents
```

Releasing a stronger lease therefore downgrades to the strongest remaining
mouse request instead of disabling mouse reporting outright.

---

## 3. Capability-driven availability

T15 does not enable rich input merely because a terminal name resembles xterm.

Bracketed paste is available only when the selected `Icod.TermInfo`
`TerminalDescription` advertises all four extended string capabilities:

```text
BE  enable bracketed paste
BD  disable bracketed paste
PS  paste start marker
PE  paste end marker
```

Focus reporting is available only when all four extended strings are present:

```text
fe     enable focus reporting
fd     disable focus reporting
kxIN   focus-in report
kxOUT  focus-out report
```

Requiring both the reversible control strings and the future decode markers
prevents Terminal from claiming a protocol which it could enable but could not
subsequently interpret.

Mouse reporting requires:

```text
XM
xm
key_mouse
```

`XM` and `xm` provide terminfo evidence that the profile describes reversible
mouse mode and mouse report formatting. `key_mouse` determines the advertised
wire family.

The recognized `key_mouse` prefixes are:

```text
ESC [ <   SGR mouse reporting
ESC [ M   legacy xterm/X10-style reporting
```

No name-based fallback is used when those capabilities are absent.

---

## 4. Mouse protocol state

SGR mouse reporting is preferred whenever the terminal profile advertises the
SGR `key_mouse` prefix.

For SGR reports, T15 owns the encoding mode:

```text
CSI ? 1006 h   enable SGR mouse encoding
CSI ? 1006 l   disable SGR mouse encoding
```

The tracking intensity is represented by the traditional xterm DEC private
modes:

```text
CSI ? 1000 h/l   button events
CSI ? 1002 h/l   button-motion tracking
CSI ? 1003 h/l   any-motion tracking
```

Legacy mouse profiles use the same tracking modes without enabling mode 1006.

The extended `XM` capability is intentionally not emitted directly for exact
tracking-level transitions. Modern terminfo profiles may define `XM` as a
compound terminal-specific mode which combines 1000, 1003, 1004, 1006, or
other state. That cannot safely satisfy a caller's precise
`ButtonEvents`/`ButtonMotion`/`AnyMotion` request.

The explicit 1000/1002/1003/1006 sequences are therefore used only after
`XM`, `xm`, and a recognized `key_mouse` prefix have established an
xterm-compatible mouse contract. They are not unconditional ANSI guesses.

---

## 5. Transition ordering

A compound acquisition enters state in this order:

```text
bracketed paste
focus reporting
mouse encoding
mouse tracking
```

Release/restoration uses the reverse semantic order:

```text
mouse tracking
mouse encoding
focus reporting
bracketed paste
```

Changing mouse strength disables the currently active tracking mode before
enabling the new mode. SGR encoding remains enabled across an ordinary strength
change.

Every transition is transactional. If a later write fails, completed writes are
rolled back toward the pre-transition state. If rollback also fails, both errors
are preserved in an `AggregateException`.

---

## 6. Suspension and resume

Active input-protocol leases participate in the existing T07/T10 lifecycle
model.

Before suspension, Terminal:

1. prepares lifecycle participants;
2. restores active input protocols to baseline;
3. restores presentation state;
4. flushes output;
5. releases host output setup;
6. restores the captured host input mode.

On resume, Terminal reapplies:

1. host output setup;
2. semantic host input mode;
3. presentation state;
4. input-protocol state;
5. lifecycle participants.

If re-entry fails after a state manager has already re-entered, T15 adds
best-effort rollback for both input-protocol and presentation state before the
host baseline is restored.

---

## 7. Session disposal

`TerminalSession.DisposeAsync()` closes active input-protocol leases before
presentation state and host terminal restoration.

An undisposed lease is marked released by its owning manager after the session
restores baseline state. Disposing that lease afterward is therefore a no-op
and cannot emit duplicate disable sequences.

---

## 8. Invalidation

`TerminalSession.InvalidateState()` now invalidates both presentation-state and
input-protocol state assumptions.

Lifecycle suspension and resume establish the baseline/re-entry boundary used
to recover owned rich-input state after out-of-band host activity.

---

## 9. Deferred decoding

T15 changes terminal reporting state only.

The decoder still emits the T14 event vocabulary only for the event kinds
implemented by its current code. Protocol decoding remains:

```text
T16  focus + bracketed paste
T17  mouse
```

No second reader or protocol-specific side channel is introduced.

---

## 10. Validation gate

T15 is complete when:

1. `net8.0`, `net9.0`, and `net10.0` build and test;
2. bracketed-paste and focus availability requires complete terminfo contracts;
3. SGR and legacy mouse modes are capability-gated rather than name-guessed;
4. nested boolean protocol leases use first-owner/last-owner semantics;
5. nested mouse leases select and restore the strongest active request;
6. partial acquisition failure rolls back completed transitions;
7. session disposal restores an undisposed protocol lease exactly once;
8. suspend/resume restores baseline and re-enters active protocol state;
9. package verification and fresh package consumers remain green on Windows,
    Linux, and macOS.

The next tranche is T16 — Focus and Bracketed Paste.
