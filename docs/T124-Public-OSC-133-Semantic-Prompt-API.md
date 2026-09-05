# T124 — Public OSC 133 Semantic-Prompt API

**Release:** `0.12.0`  
**Tranche:** `T124`  
**Development version:** `0.12.0-alpha.5`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T124 exposes the frozen T120 portable OSC 133 semantic-prompt operations through `TerminalSession`.

The public API remains semantic. Callers do not construct OSC 133 marker letters, raw field lists, or arbitrary metadata payloads.

---

## 2. Frozen public surface

T124 exposes exactly these operations:

```csharp
ValueTask BeginPromptAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandInputAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandOutputAsync(
	CancellationToken cancellationToken = default
);

ValueTask FinishCommandAsync(
	byte exitStatus,
	CancellationToken cancellationToken = default
);

ValueTask AbortCommandAsync(
	CancellationToken cancellationToken = default
);
```

These names are the final T120 semantic names for the 0.12 portable core.

---

## 3. Semantic mapping

The public operations map to the portable FinalTerm/iTerm2-style core as follows:

```text
BeginPromptAsync         -> A
BeginCommandInputAsync   -> B
BeginCommandOutputAsync  -> C
FinishCommandAsync(n)    -> D;n
AbortCommandAsync        -> D
```

The protocol letters remain internal implementation details.

---

## 4. Completion and abort remain distinct

`FinishCommandAsync` requires a `byte` exit status, matching the frozen portable range `0..255`.

In particular:

```text
FinishCommandAsync(0) -> D;0
AbortCommandAsync()    -> D
```

The bare D form is therefore not a nullable-status alias and is not interpreted as successful completion.

---

## 5. Independent-call semantics

All five public operations remain independently callable.

T124 does not introduce:

- an A→B→C→D in-memory state machine;
- transition validation;
- nested command-region ownership;
- a scoped command lease;
- retained shell history.

The canonical sequence remains useful documentation for shells and REPLs, but `Icod.Terminal` does not reject partial, repeated, recovery, redraw, or otherwise noncanonical marker sequences merely because the same `TerminalSession` did not observe earlier markers.

---

## 6. Session output semantics

The public methods use the T123 session integration path and therefore:

- require an interactive terminal output endpoint;
- serialize through the normal session output gate;
- preserve ordering relative to application text and other session-owned terminal output;
- observe caller cancellation before output commitment;
- write committed OSC 133 frames non-cancellably;
- perform no implicit flush;
- reject emission after session output has closed.

---

## 7. Support posture

Successful completion means only that the complete OSC 133 marker frame was written through the borrowed terminal output service.

It does not prove that the terminal:

- supports OSC 133;
- recognized the marker;
- created a command mark;
- retained semantic scrollback metadata;
- displayed or otherwise used the exit status.

T124 adds no support query or terminal-emulator inference.

---

## 8. Public surface intentionally omitted

T124 does not expose:

- a public marker enum;
- a public marker struct;
- raw A/B/C/D values;
- `WriteOsc133Async`;
- nullable completion status;
- arbitrary OSC 133 property dictionaries;
- Kitty-specific marker metadata;
- OSC 633 or OSC 1337 aliases.

The five semantic methods are sufficient for the frozen portable 0.12 core.

---

## 9. Tests

`TerminalSessionSemanticPromptPublicApiTests` proves:

- all five public methods compile and emit their canonical frames;
- committed public writes remain non-cancellable;
- no public method flushes implicitly;
- `FinishCommandAsync(0)` differs from `AbortCommandAsync()`;
- the public methods remain independently callable in noncanonical order;
- pre-cancelled public calls emit nothing;
- disposed sessions reject public marker emission.

T123 continues to provide the deeper session-ordering, redirected-output, queued-cancellation, control-gate, and failure-recovery proofs beneath this public layer.

---

## 10. T124 decision

The portable OSC 133 semantic-prompt surface is now public through five explicit `TerminalSession` operations with no raw protocol escape hatch and no synthetic shell-history state machine.

T125 may now harden lifecycle, failure, disposal, cancellation, and ordering behavior around this final public surface.
