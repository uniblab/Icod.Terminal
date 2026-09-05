# T103 — Session Progress Manager and Nesting

**Release:** `0.10.0`  
**Tranche:** `T103`  
**Development version:** `0.10.0-alpha.4`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T103 introduces session-owned logical OSC 9;4 progress state with deterministic nested ownership, restoration, cleanup debt, and lifecycle participation.

The tranche remains internal. The public progress lease/API is assigned to T104.

---

## 2. Ownership model

`TerminalProgressManager` owns an ordered list of logical progress owners.

Each owner may be either:

```text
unreported
reported(value)
```

Acquiring an owner emits no terminal output.

The physical controller is the most recently acquired owner which currently has a reported value. A newer unreported owner does not mask a lower reported owner.

---

## 3. Reporting semantics

When an owner reports a value:

- if no newer reported owner exists, that owner controls physical progress and the value is emitted;
- if a newer reported owner exists, the report updates logical state only and emits nothing;
- if the controlling owner later releases, the most recent remaining reported owner is restored using its latest logical value.

This allows an outer owner to advance from, for example, 30% to 40% while an inner owner temporarily displays indeterminate progress. When the inner owner releases, 40% is restored rather than the stale 30% value.

---

## 4. Release semantics

Releasing a non-controlling owner is physically silent.

Releasing the controlling owner emits exactly one transition:

- restore the newest remaining reported owner; or
- emit canonical OSC 9;4 clear when no reported owner remains.

Final owner release therefore clears library-owned terminal progress.

Owners may be released out of acquisition order without corrupting physical state.

---

## 5. Failure and recovery posture

If a physical progress write reports failure after transmission was attempted, the manager records cleanup debt because terminal state may be uncertain.

Before a later report or active release proceeds, the manager first re-establishes the current logical controller using a non-caller-cancellable control-output write. If no logical controller exists, it emits canonical clear.

This prevents a failed inner progress write from leaving an untracked physical spinner or percentage while the logical model continues under a different assumption.

Detailed failure-matrix acceptance remains assigned to T105.

---

## 6. Lifecycle participation

`TerminalProgressManager` registers as a core lifecycle participant.

Current structural behavior:

```text
suspend -> clear physical progress while retaining logical owners
resume  -> restore newest remaining reported owner
close   -> best-effort clear and discard logical ownership
```

Acquisition and reporting while the manager is suspended are rejected. Release while suspended is logical-only.

T105 will add the full lifecycle/failure regression matrix.

---

## 7. Session disposal ordering

`TerminalSession.ClosePresentationStateAsync()` now closes progress state after ordinary presentation/hyperlink/cursor restoration and before synchronized output.

Therefore, when synchronized output is also active, progress clear occurs before the final synchronized-output leave boundary.

This preserves the 0.9 rule that synchronized-output release remains the final library-owned presentation boundary during disposal.

---

## 8. Tests

`TerminalProgressManagerTests` currently covers:

- acquisition emits nothing;
- newer unreported owner does not mask lower reported progress;
- lower non-controlling updates remain logical-only;
- controlling-owner release restores the latest lower logical value;
- out-of-order non-controlling release is physically silent;
- final release emits canonical clear;
- session disposal clears active progress;
- no implicit progress flush behavior.

---

## 9. Scope boundary

T103 does not add:

- public `TerminalProgressLease`;
- public `AcquireProgressAsync(...)`;
- public report/indeterminate methods;
- package/API baseline changes;
- downstream DCurses progress acceptance.

Those remain assigned to T104–T107.

---

## 10. T103 decision

T103 is implemented with an internal session-owned progress manager using ordered, identity-aware, out-of-order-safe restoration semantics and cleanup-debt recovery. Exact-head validation is required before T104 begins.
