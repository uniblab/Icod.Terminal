# T115 — Pointer-Shape Lifecycle, Invalidation, Failure, and Disposal Hardening

**Release:** `0.11.0`  
**Tranche:** `T115`  
**Development version:** `0.11.0-alpha.6`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T115 hardens the OSC 22 pointer-shape subsystem around uncertain physical state, failed output, invalidation recovery, lifecycle transitions, and retryable cleanup.

T110–T114 established the wire grammar, semantic pointer vocabulary, manager ownership model, and public mutation/query surface. T115 closes the failure cases which can occur after an OSC 22 write has been attempted but before the library can truthfully know which physical pointer state the terminal retained.

---

## 2. Acquisition failure recovery

Pointer-shape acquisition differs from progress acquisition because the first lease immediately emits an OSC 22 shape.

If acquisition attempts a physical write and that write fails, no lease is returned. The caller therefore has no owner it can later dispose to repair uncertain state.

T115 makes acquisition perform immediate best-effort recovery:

- failed outermost acquisition attempts terminal-policy reset;
- failed nested acquisition attempts restoration of the previously controlling Icod-owned shape;
- successful recovery clears cleanup debt and invalidation before the original acquisition failure is rethrown;
- if both acquisition and recovery fail, both errors are preserved in `AggregateException`;
- no owner is added unless the requested shape write succeeds.

Cancellation or output-gate acquisition failure before the OSC 22 write attempt emits no cleanup frame.

---

## 3. Invalidation recovery

`TerminalSession.InvalidateState()` marks pointer physical state untrusted.

The next semantic pointer transition now recovers before continuing:

- with active owners, the current controlling Icod-owned shape is re-emitted;
- with no logical owners, terminal-policy reset is emitted;
- after successful recovery, the requested acquisition or unscoped mutation proceeds;
- if recovery fails, cleanup debt and invalidation remain authoritative and the requested transition does not proceed.

This prevents invalidation from becoming a permanent dead-end while preserving truthful physical-state ownership.

---

## 4. Failed controlling release and retry

A failed controlling lease release retains that owner.

A later `DisposeAsync()` retry first re-establishes the current controlling logical shape, then retries restoration of the next owner or final terminal-policy reset.

Successful repeated disposal remains idempotent.

A non-controlling out-of-order release remains logical-only and emits nothing.

---

## 5. Explicit mutation failure

Unscoped `SetPointerShapeAsync(...)` and `ResetPointerShapeAsync(...)` remain unavailable while a scoped owner exists.

When an unscoped OSC 22 write is attempted and fails:

- the manager immediately attempts terminal-policy reset;
- successful reset clears cleanup debt and the original mutation failure is rethrown;
- mutation plus reset failure is aggregated;
- unresolved cleanup debt is recoverable by a later semantic pointer operation;
- ordinary cancellation before the write attempt emits nothing.

Unscoped mutation still creates no lifecycle owner and does not claim restoration of a pre-mutation external pointer shape.

---

## 6. Suspend and re-entry failure

Managed suspension continues to reset active scoped pointer state while retaining logical owners.

Re-entry:

1. resolves any cleanup debt or invalidation;
2. re-applies the newest remaining logical owner when one exists;
3. leaves pointer state at terminal policy when no owners remain.

If re-entry fails after a shape write attempt, the manager attempts terminal-policy reset.

If both re-entry and cleanup fail, both exceptions are retained in an `AggregateException`.

---

## 7. Session cleanup retry

`TerminalPointerShapeManager.CloseAsync()` remains retryable.

If authoritative session reset fails:

- the manager is not marked closed;
- owners are not discarded;
- cleanup debt and invalidation remain set;
- lifecycle registration remains active;
- a later close attempt may retry the terminal-policy reset.

After successful close, late lease disposal emits nothing.

---

## 8. Failure-injection coverage

T115 adds tests proving:

- failed outermost acquisition resets to terminal policy and allows later acquisition;
- failed nested acquisition restores the current outer owner;
- acquisition plus restoration double failure aggregates both exceptions;
- invalidation recovers the current controller before nested acquisition;
- failed final lease reset remains retryable through the same public lease;
- failed manager close retains cleanup responsibility for retry;
- re-entry plus cleanup double failure aggregates both exceptions;
- failed explicit set resets to terminal policy and permits later mutation;
- explicit mutation plus cleanup double failure is recoverable by the next set operation;
- lease disposal after successful session disposal emits nothing.

---

## 9. Cancellation and cleanup rule

Caller cancellation remains a pre-commit concern for requested mutation and acquisition.

Once recovery or cleanup is required, cleanup writes use `CancellationToken.None` and are not abandoned because the initiating caller later cancels.

No pointer-shape recovery path implicitly flushes output.

---

## 10. T115 decision

The OSC 22 pointer-shape subsystem now has a complete retryable ownership model for successful operation, invalidation, failed acquisition, failed explicit mutation, failed release, suspend/re-entry, and session cleanup.

T116 may now focus on composition with the existing semantic output families and real downstream `Icod.DCurses` acceptance rather than additional state-machine semantics.
