# T95 — Synchronized Output Lifecycle, Failure Recovery, and Disposal

**Release:** `0.9.0`  
**Tranche:** `T95`  
**Development version:** `0.9.0-alpha.6`  
**Status:** Implemented; exact-head validation pending  
**Theme:** cancellation boundaries, suspend/resume, retryable cleanup, and authoritative session disposal

## 1. Objective

T95 closes the failure and lifecycle semantics of synchronized-output ownership before compatibility/downstream acceptance.

The implementation must distinguish:

- logical owner lifetime;
- whether a physical begin/end frame was attempted;
- whether synchronized-output cleanup remains pending;
- whether the session is currently suspended;
- whether a public lease may retry failed cleanup.

## 2. First-owner commit boundary

First-owner acquisition now makes the physical commit boundary explicit.

The manager:

1. acquires the existing session output gate with caller cancellation;
2. fully constructs the `CSI ? 2026 h` frame;
3. observes caller cancellation immediately before the transport call;
4. invokes the borrowed output with the complete frame and `CancellationToken.None`;
5. treats every exception after that invocation as a potentially committed begin requiring best-effort cleanup.

Therefore cancellation while waiting for the shared output gate or immediately before the transport commit emits no begin frame and does not emit a compensating end frame.

Once the transport write has been invoked, caller cancellation cannot truncate or retrospectively cancel the frame.

## 3. Acquisition double failure

If first-owner begin reports failure after commit was attempted, the manager immediately attempts:

```text
CSI ? 2026 l
flush
```

If that cleanup also fails:

- no public lease is returned;
- both failures are reported in an `AggregateException`;
- cleanup responsibility remains session-owned;
- later `TerminalSession.DisposeAsync()` retries the physical leave best-effort.

## 4. Final-release failure

The final logical owner remains owned until both:

```text
CSI ? 2026 l
flush
```

succeed.

Failure of either the end write or the flush leaves the public lease retryable. A later `DisposeAsync()` on the same lease retries cleanup.

If the end frame succeeded but the flush failed, retry may emit another end frame before retrying the flush. Repeated mode-off requests are preferred to falsely declaring cleanup complete.

## 5. Managed suspension

With one or more logical owners active, managed suspend performs:

```text
CSI ? 2026 l
flush
session-wide suspend flush
host suspension
```

Logical owners remain registered while physical synchronized output is inactive.

On resume, if at least one logical owner remains:

```text
CSI ? 2026 h
```

is re-emitted during lifecycle participant re-entry.

The session reaches `IsStateValid == true` only after lifecycle re-entry succeeds.

## 6. Release while suspended

Releasing synchronized-output leases while the session is suspended is logical-only.

If the last logical owner is released during suspension:

- no additional mode-off frame is emitted;
- resume does not re-enter mode 2026;
- no synchronized-output cleanup remains pending solely because a logical owner disappeared while already physically inactive.

## 7. Session disposal

Session disposal remains authoritative.

Synchronized output closes after cursor-style, hyperlink, and presentation-state cleanup so those restorations can still occur while terminal presentation is deferred. The synchronized-output manager then emits the final mode-off + flush boundary when physical cleanup is still required.

Public lease disposal after session disposal emits nothing.

## 8. Acceptance coverage

T95 adds tests for:

- caller cancellation while waiting for the shared output gate;
- final flush failure followed by successful retry through the same public lease;
- first begin failure plus immediate cleanup failure followed by session-disposal retry;
- active synchronized-output ownership leaving on managed suspend and re-entering on resume;
- release of the final logical owner while suspended preventing re-entry;
- exact mode-2026 frame ordering across lifecycle transitions;
- session state validity after successful resume.

## 9. T95 decision

The synchronized-output manager now has explicit, testable ownership for every failure window relevant to 0.9:

- pre-commit cancellation;
- begin-write failure;
- cleanup-after-begin failure;
- final end failure;
- final flush failure;
- suspend leave;
- release while suspended;
- resume re-entry;
- session-disposal retry.

T96 may therefore focus on compatibility, concurrency/regression acceptance, and the downstream `Icod.DCurses` boundary rather than adding new lifecycle semantics.
