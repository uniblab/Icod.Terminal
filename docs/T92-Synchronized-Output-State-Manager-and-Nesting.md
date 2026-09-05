# T92 — Synchronized Output State Manager and Nesting

**Release:** `0.9.0`  
**Tranche:** `T92`  
**Development version:** `0.9.0-alpha.3`  
**Status:** Implemented; exact-head validation pending  
**Theme:** identity-aware logical ownership, first-owner/last-owner physical transitions, cleanup retention, and session integration

## 1. Manager model

`TerminalSynchronizedOutputManager` is session-owned internal state. It separates:

- logical owners — tracked by monotonic owner IDs in a set;
- physical synchronized-output state — whether the library believes mode 2026 may be active;
- cleanup obligation — whether a prior failed transition requires an explicit leave retry;
- lifecycle suspension state;
- final closed state.

The manager does not expose public API in T92.

## 2. Ownership semantics

The frozen T90 first-owner/last-owner model is implemented directly:

```text
0 owners -> acquire A -> emit begin
1 owner  -> acquire B -> no frame
2 owners -> acquire C -> no frame
release any non-final owner -> no frame
release final owner -> emit end + flush
```

Owner identity is explicit, so out-of-order release is valid. No stack restoration is required because every logical owner requests the same boolean active mode.

Unknown/already-released owner IDs are idempotent no-ops. Invalid non-positive internal owner IDs are rejected.

## 3. Lock ordering

The manager has one `SemaphoreSlim` state gate.

Frozen lock ordering is:

```text
synchronized-output manager gate
    -> short-lived TerminalSession output gate
        -> one physical transition write / required flush
```

No public or internal owner lifetime retains the session byte-output gate. The output gate is acquired only for physical zero-to-one, one-to-zero, lifecycle, or cleanup transitions.

No session-output operation acquires the synchronized-output manager gate, so the design avoids inverse acquisition from the ordinary writer side.

## 4. First acquisition

First acquisition:

1. validates cancellation and interactive output;
2. acquires the manager gate;
3. rejects closed, suspended, cleanup-pending, or exhausted-owner-ID state;
4. acquires the normal session-output gate;
5. emits one canonical mode-2026 begin frame;
6. records physical-active state;
7. commits a new logical owner ID.

Nested acquisition performs no terminal write and commits only a new logical owner ID.

## 5. Acquisition failure

If the first begin write reports failure after emission was attempted:

- no logical owner is created;
- physical state is considered potentially active;
- cleanup responsibility is retained;
- the manager attempts immediate non-cancellable end + flush while still owning the output transition;
- if cleanup succeeds, the original failure is rethrown;
- if cleanup also fails, both failures are reported as an aggregate and cleanup remains pending for later session cleanup.

Pre-commit caller cancellation emits nothing.

## 6. Release semantics

Non-final release removes only that logical owner ID.

Final release attempts end + flush. The final owner is removed only after both operations succeed.

If final release fails, the owner remains logically active and cleanup remains pending. The same owner release or session cleanup may retry. This prevents a failed physical cleanup from losing its last logical recovery handle.

## 7. Lifecycle integration

The manager registers as a core lifecycle participant.

Before suspension:

- if mode 2026 may be active or cleanup is pending, it emits end + flush;
- logical owner IDs remain intact;
- the manager enters suspended state.

After resume:

- pending cleanup is retried first;
- if no logical owners remain, mode 2026 is not re-entered;
- otherwise one begin frame is emitted and physical-active state is restored.

Release while suspended removes logical owner IDs without output. If all owners are released before resume, resume does not emit begin.

## 8. Session disposal ordering

`TerminalSession` owns a lazy synchronized-output manager integration point.

During output-state closure, existing cursor-style, hyperlink, and presentation cleanup executes first while synchronized rendering may still be deferred. Synchronized-output cleanup then emits the final mode-off frame and flushes as the terminal-visible boundary.

This ordering keeps related restoration traffic inside the synchronized update where possible and makes ESU the final library-owned presentation transition.

## 9. T92 tests

Focused manager tests cover:

- multiple owners -> one begin and one final end;
- out-of-order release;
- exact final flush count;
- final-release failure retaining the owner for retry;
- pre-cancelled first acquisition emitting nothing;
- failed first begin attempting immediate cleanup;
- session disposal physically leaving active synchronized output.

T95 will add the fuller suspend/resume and compounded failure matrix.

## 10. T92 acceptance

T92 is accepted when exact-head CI proves:

1. all supported TFMs compile;
2. prior 0.8 regressions remain green;
3. T91 framing remains byte-exact;
4. identity-aware nesting behaves deterministically;
5. final-release flush semantics are exact;
6. session disposal owns final synchronized-output cleanup.

T93 may then expose the reviewed public lease/API over this manager.
