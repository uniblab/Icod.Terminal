# T125 — OSC 133 Lifecycle, Failure, and Ordering Hardening

**Release:** `0.12.0`  
**Tranche:** `T125`  
**Development version:** `0.12.0-alpha.6`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T125 hardens the public OSC 133 semantic-prompt surface introduced by T124 across cancellation, transport failure, session disposal, managed suspend/resume, lifecycle re-entry failure, and noncanonical ordering.

The tranche intentionally adds no retained command-region state and no OSC 133 lifecycle participant. T120 froze OSC 133 markers as transient semantic events rather than library-owned terminal modes.

---

## 2. Cancellation boundaries

T125 proves both sides of the commit boundary:

- cancellation while waiting for the shared session output gate emits nothing;
- cancellation is observed immediately before commitment;
- once the complete marker write has begun, the transport receives `CancellationToken.None`;
- cancellation after commitment therefore cannot deliberately truncate or retroactively cancel the marker frame.

This preserves the transaction rule established in T120–T123.

---

## 3. Failed committed writes

A transport failure during a committed marker write is propagated to the caller.

The session does not:

- emit a compensating `D` marker;
- emit an abort marker;
- infer whether any prefix reached the terminal;
- mark a synthetic command region complete;
- retain logical OSC 133 state requiring recovery.

A later independent marker operation remains available because the library owns output ordering and byte correctness, not shell-history reconstruction.

---

## 4. Disposal semantics

Session disposal emits no OSC 133 marker.

In particular, disposal does not automatically:

- finish a command;
- abort a command;
- start a prompt;
- replay any prior marker.

Repeated disposal remains idempotent with respect to OSC 133 because there is no semantic-prompt cleanup ownership.

---

## 5. Managed suspend and resume

Managed suspension and resumption do not create, remove, or replay OSC 133 annotations.

If an application emits a command-output marker before suspension, the lifecycle machinery restores/reapplies only the terminal state it actually owns. The OSC 133 event remains historical output and is not synthesized again on resume.

After resume, the application may explicitly emit whichever semantic marker is truthful for its command flow.

---

## 6. Lifecycle failure posture

If terminal-state re-entry fails during lifecycle processing, the session may become invalid for library-owned terminal state, but OSC 133 history is still not fabricated.

T125 proves that lifecycle re-entry failure does not automatically emit completion or abort markers. The marker bytes already written remain the only OSC 133 semantic history produced by the library.

---

## 7. Ordering posture

T125 retains the independently callable public API frozen by T120.

Noncanonical sequences remain accepted. The library does not reject an operation because a preceding marker was not observed through the same `TerminalSession` instance.

This supports prompt redraws, subshells, nested REPLs, interrupted output, and integration which begins partway through a command lifecycle.

---

## 8. Hardening tests

`TerminalSessionSemanticPromptHardeningTests` covers:

- cancellation while queued for the session output gate;
- cancellation after committed transport write begins;
- non-cancellable committed transport token;
- failed committed marker write with no compensation;
- later independent marker after a failed write;
- disposal with no automatic finish/abort marker;
- repeated disposal with no OSC 133 cleanup output;
- suspend/resume with no marker emission or replay;
- explicit completion after resume;
- lifecycle re-entry failure with no fabricated semantic history.

Existing T123/T124 tests continue to cover byte-exact ordering, noncanonical sequences, interactive-output validation, and public API behavior.

---

## 9. Production-code result

The T125 review found no reason to add production recovery state or lifecycle hooks.

The existing T123/T124 implementation already satisfies the frozen contract structurally:

- every marker is a single session-serialized event;
- there is no retained OSC 133 semantic state;
- no lifecycle participant is registered;
- disposal owns no semantic-prompt cleanup;
- committed writes are non-cancellable;
- transport failures propagate directly.

T125 therefore strengthens the release primarily through adversarial proof rather than new stateful machinery.

---

## 10. T125 decision

The public OSC 133 surface is hardened around truthful event semantics. Cancellation cannot create partial caller-driven frames; failures do not fabricate recovery history; suspend/resume/disposal do not invent semantic markers; and independently callable ordering remains intact.

T126 may now focus on composition with the rest of the terminal-control surface and downstream `Icod.DCurses` acceptance.
