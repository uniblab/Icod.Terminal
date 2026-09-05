# T105 — Progress Lifecycle, Failure, and Disposal

**Release:** `0.10.0`  
**Tranche:** `T105`  
**Development version:** `0.10.0-alpha.6`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T105 hardens the public OSC 9;4 progress ownership introduced through T104 for long-running terminal applications.

The tranche focuses on lifecycle truthfulness, invalidation, cleanup retry, and failure behavior rather than adding new public API.

---

## 2. Session invalidation

`TerminalSession.InvalidateState()` now includes active progress state.

Invalidation:

- emits no OSC 9;4 frame immediately;
- marks the manager's physical-state knowledge untrusted;
- blocks new progress acquisition while meaningful invalidated progress ownership remains;
- causes the next controlled report/release/lifecycle transition to re-establish a truthful physical state before proceeding.

If the progress manager exists but has no owners, no active physical progress, and no cleanup debt, a subsequent acquisition may clear the stale invalidation marker and proceed without unnecessary output.

---

## 3. Suspend and resume

With active reported progress:

```text
active progress
    |
prepare suspend
    v
OSC 9;4 clear
    |
logical owners retained
    |
resume
    v
restore current controlling logical progress
```

If every owner is released while suspended, resume emits nothing.

Progress reporting/acquisition remains rejected while the progress manager is suspended.

---

## 4. Failed physical transitions

A failed progress write or cleanup transition now records both:

- cleanup debt;
- invalidated physical-state knowledge.

Before a later report or release proceeds, the manager first re-establishes the current logical controller—or clear state when no reported controller remains.

This prevents a failed transition from leaving an untracked terminal spinner/progress state behind while logical ownership continues.

---

## 5. Retryable final cleanup

Final cleanup is retryable at both lease and manager/session levels.

### Lease release

If a final `TerminalProgressLease.DisposeAsync()` clear fails:

- the lease retains ownership;
- cleanup debt remains recorded;
- a later `DisposeAsync()` first recovers the current logical state and then retries the clear;
- successful repeated disposal is idempotent.

### Manager/session close

If progress clear fails during manager/session close:

- the manager does not mark itself closed;
- logical owners are retained;
- lifecycle registration remains active;
- cleanup debt remains recorded;
- a later close/disposal attempt can retry the clear.

The manager is marked closed and its owners discarded only after required cleanup succeeds.

---

## 6. Re-entry failure and double failure

Resume re-entry is transactional:

1. clear any unresolved/invalidated physical state;
2. emit the current controlling logical progress state;
3. if re-entry emission fails, immediately attempt a canonical clear;
4. if both re-entry and cleanup fail, throw `AggregateException` containing both failures.

This matches the truthful cleanup policy established for earlier session-owned terminal state.

---

## 7. Cancellation

Public caller cancellation remains meaningful only before commit.

Once a complete OSC 9;4 frame begins transmission, the transport write is non-caller-cancellable.

Lifecycle cleanup, restoration, and disposal use `CancellationToken.None` so cleanup cannot be abandoned by the caller that triggered it.

---

## 8. Disposal ordering

Session disposal continues to close progress state before synchronized output performs its final leave operation.

That keeps progress cleanup inside the final synchronized-output presentation boundary when both state systems are active.

Late progress-lease disposal after successful session cleanup remains physically silent.

---

## 9. Regression coverage

T105 adds focused tests proving:

- suspend emits clear and resume restores the current progress value;
- releasing all progress owners while suspended prevents resume re-entry;
- `TerminalSession.InvalidateState()` participates in progress invalidation;
- invalidated progress recovers the previous logical controller before accepting a new report;
- failed final lease clear is retryable;
- failed manager close retains cleanup for retry;
- successful repeated lease disposal is idempotent after retry;
- re-entry plus cleanup double failure produces an aggregate exception.

---

## 10. T105 decision

T105 adds no new public API.

The `0.10.0-alpha.6` progress subsystem now has explicit lifecycle and failure semantics suitable for a higher-level consumer such as `Icod.DCurses`.

T106 may proceed after exact-head validation.
