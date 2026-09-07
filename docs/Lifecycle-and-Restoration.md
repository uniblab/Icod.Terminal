# Lifecycle and Restoration

This document defines the permanent 1.x lifecycle, invalidation, restoration, and disposal contract for `TerminalSession`.

The central rule is:

> lifecycle is part of terminal-state ownership, not merely a stream of notifications.

When the host supports managed lifecycle observation, the session restores terminal state before suspension and truthfully re-establishes owned state after resume before reporting normal operation again.

## 1. Initial baseline capture

Opening a session requires an observable input terminal mode.

Before applying the requested semantic input policy, `TerminalSession` captures the native input-mode baseline from the configured input endpoint.

That captured snapshot is the authority for native-mode restoration. The library does not define a guessed global “normal terminal mode” and does not restore by constructing one from hard-coded defaults.

After capture, opening may:

- configure host output mode where requested/required;
- apply the requested input mode (`Canonical`, `CBreak`, or `Raw`);
- apply the requested echo policy;
- initialize session-owned managers and lifecycle handling.

If initialization fails after state was changed, cleanup attempts to restore state already acquired. If initialization and restoration both fail, both failures are surfaced rather than one masking the other.

## 2. Platform-specific native restoration timing

The captured native platform determines the mode-application timing used for restoration:

```text
POSIX termios      -> AfterOutputDrained
Windows console    -> Immediately
```

The restored value is the exact captured `TerminalModeSnapshot` for that native platform.

Windows does not synthesize POSIX termios state, and POSIX does not synthesize Windows console mode state.

## 3. Lifecycle support is observable

`SupportsLifecycleEvents` reports whether automatic host lifecycle observation is active for the session.

Automatic observation is normally available only when the system-backed session can install the corresponding platform lifecycle source. Custom terminal-control providers do not imply automatic process-wide signal handling.

When lifecycle observation is unavailable:

- `SupportsLifecycleEvents` is `false`;
- `ReadLifecycleEventAsync(...)` throws `NotSupportedException`;
- ordinary session operation remains valid without automatic lifecycle events.

`ObserveLifecycleEvents = false` deliberately opts out of automatic lifecycle observation.

## 4. Lifecycle event model

The public lifecycle events are:

- `Resize` — terminal dimensions may have changed;
- `Interrupt` — an interactive interrupt request was observed;
- `Termination` — a termination request was observed;
- `Suspending` — session-owned host state has been released in preparation for suspension;
- `Resumed` — session-owned state and registered participant state have been re-entered successfully.

`TerminalLifecycleEvent.Size` may contain a live size for resize/resume events when live dimensions are available.

Resize notifications may be coalesced when the newly observed size is unchanged from the most recently published lifecycle size.

## 5. Termination token

`TerminationToken` is canceled when the lifecycle subsystem observes an interrupt, termination request, or fatal lifecycle-pump failure.

The token is a one-way signal. Cancellation does not itself imply that the session was disposed or that all cleanup succeeded.

Applications may combine `TerminationToken` with their own shutdown policy, but `DisposeAsync()` remains the explicit final session cleanup operation.

## 6. Suspend is a coordinated state handoff

A managed suspend sequence owns a transition from “application controls terminal state” to “host may regain control.”

Before the process is actually suspended, the session:

1. marks lifecycle state as released so new public presentation/rich-input acquisition is rejected;
2. suspends public query transactions and invalidates session physical-state belief;
3. asks registered lifecycle participants to prepare, in reverse registration order;
4. releases session-owned rich-input protocol state;
5. releases session-owned presentation state;
6. flushes pending output;
7. releases host output-mode setup owned by the session;
8. restores the captured native input-mode baseline when restoration is required;
9. publishes `Suspending` only after the release sequence has completed successfully enough to hand control to the suspend controller;
10. asks the platform suspend controller to complete process suspension.

The exact private class/method structure is not a 1.x compatibility promise. The observable ordering and ownership guarantees are.

## 7. Lifecycle participants

Higher layers may register `ITerminalSessionLifecycleParticipant` implementations when they own state that must compose with session suspend/resume.

Participants run from the managed lifecycle pump, not from a native signal callback.

### Preparation order

`PrepareForTerminalSuspendAsync(...)` runs in **reverse registration order** before the session releases its own presentation/native state.

This mirrors nested ownership: a later/higher-layer owner is given the first opportunity to dismantle its state before the lower-level session state is released.

### Resume order

`ResumeAfterTerminalSuspendAsync(...)` runs in **registration order** after the session has re-entered the ordinary host/presentation/input-protocol state required by participants.

The session publishes `Resumed` only after participant resume has completed and ordinary public query availability has returned.

### Query restriction during participant resume

Public terminal query APIs are deliberately unavailable while participant resume callbacks execute.

A lifecycle participant must not call public session query APIs from `ResumeAfterTerminalSuspendAsync(...)`.

Observation-dependent session-owned state uses a separate internal lifecycle observation phase. That internal mechanism does not widen the public participant contract.

## 8. Resume invalidates before re-entry

A resume signal does not mean prior physical-state beliefs are still valid.

The session first invalidates state, then attempts to rebuild the live contract from authoritative baselines/configuration:

1. re-establish host output setup;
2. reapply the configured semantic native input mode from the captured baseline;
3. re-enter session-owned presentation state;
4. re-enter/reconcile session-owned rich-input protocol state, including required observation/negotiation;
5. resume lifecycle participants;
6. re-enable ordinary public query transactions;
7. clear the lifecycle-state-released marker;
8. mark session state valid;
9. publish `Resumed` with the current live size when available.

The session does not simply replay stale believed physical state without re-establishing the contracts that require observation or negotiation.

## 9. Query generations across lifecycle

Active terminal query transactions are part of the lifecycle boundary.

During suspension:

- public query callers are interrupted/rejected according to the query contract;
- already-emitted transactions may retain bounded late-response ownership so stale replies cannot be mistaken for new post-resume responses;
- queued old-generation transactions do not become post-resume wire traffic.

Post-resume observation-dependent session state uses the same authoritative query-routing/ambiguity machinery rather than bypassing stale ownership.

Ordinary public queries return only after lifecycle re-entry and participant resume have completed.

Permanent query details are documented in `Queries-and-Responses.md` during T192.

## 10. State validity

`IsStateValid` represents whether the session currently trusts its own applied physical-state assumptions.

It becomes false when, for example:

- lifecycle releases terminal state for suspend;
- the caller invokes `InvalidateState()` after out-of-band host activity;
- a multi-step state transition fails in a way that makes the physical result uncertain;
- lifecycle re-entry fails;
- final teardown begins.

`IsStateValid == false` does not necessarily mean every logical lease has been discarded. It means previously believed physical state must not be treated as authoritative.

Successful lifecycle re-entry restores validity only after the relevant state has been re-established.

## 11. Explicit invalidation

`InvalidateState()` is the public escape for out-of-band terminal changes not managed by the automatic lifecycle path.

Invalidation:

- marks the session physical-state belief invalid;
- invalidates session-owned rich-input/presentation believed state;
- emits no compensating terminal traffic by itself;
- does not fabricate a new baseline;
- does not silently claim that the terminal was restored.

Callers should invalidate only when they know external activity may have changed state behind the session.

## 12. New state acquisition during lifecycle/teardown

Once lifecycle has declared session terminal state released, new public presentation and input-protocol ownership is rejected.

Once disposal/teardown has begun, new ownership is likewise rejected.

This availability check occurs inside the same state-composition ordering domain as the managers it protects:

```text
state composition
    -> lifecycle/teardown availability
        -> manager
            -> control output
```

The rule prevents new application ownership from entering while lifecycle or disposal is already unwinding terminal state.

Existing cleanup paths remain able to run; otherwise the library could deadlock itself out of restoring state it already owns.

## 13. Re-entry failure and rollback

Resume is a multi-step state transition and can fail after some steps have succeeded.

On re-entry failure, the session attempts to move back toward the captured baseline:

- rich-input state is suspended/restored as appropriate;
- presentation state is suspended/restored as appropriate;
- output-mode setup is released;
- the captured native baseline is restored when required.

If the primary re-entry operation fails and rollback also fails, both failures are preserved, typically through an `AggregateException` at the internal lifecycle failure boundary.

A fatal lifecycle-pump failure:

- cancels `TerminationToken`;
- completes the lifecycle-event channel with the failure;
- leaves `IsStateValid == false` when state could not be re-established truthfully.

`ReadLifecycleEventAsync(...)` may therefore surface a `ChannelClosedException` whose inner exception contains the lifecycle failure.

The library does not convert uncertain re-entry into a false `Resumed` event.

## 14. Suspend failure after state release

If the platform cannot complete suspension after terminal state was released, the session attempts normal state re-entry before propagating the suspension failure.

If both suspension and the compensating re-entry fail, both failures are preserved.

This ensures a failed suspend request does not silently leave the application in a host-baseline terminal state while claiming ordinary session operation continues.

## 15. Disposal

`DisposeAsync()` is the final cleanup boundary and is valid whether or not automatic lifecycle observation is enabled.

Final cleanup includes, in broad contract order:

1. stop accepting new ordinary session output/state acquisition;
2. close query transactions;
3. stop lifecycle observation;
4. close/restore owned rich-input protocol state;
5. close/restore owned presentation state;
6. flush output;
7. release owned host output setup;
8. restore the captured native input baseline if required.

Multiple cleanup failures are aggregated rather than silently losing all but one error.

The borrowed control provider, endpoints, input service, and output service are not disposed by the session merely because session cleanup completes.

## 16. Disposal after earlier failure

Earlier lifecycle/state failure does not automatically surrender final cleanup authority.

If a failed transition leaves state invalid/uncertain, final `DisposeAsync()` still attempts the restoration operations that remain meaningful according to the owning managers/native baseline.

This is deliberate: “state belief is invalid” and “there is nothing left to clean up” are not equivalent statements.

## 17. Restoration vs reset policy

The library distinguishes:

- restoring a captured/observed prior state;
- resetting terminal state to emulator/policy defaults.

They are not interchangeable.

A reversible ownership contract uses the captured/observed prior state when that is what it promises. Reset commands are used only where the public operation explicitly defines reset-to-policy semantics.

## 18. Consumer checklist

For lifecycle-safe consumers:

- use `SupportsLifecycleEvents` before waiting for lifecycle events;
- observe `TerminationToken` as a shutdown signal, not as proof cleanup is complete;
- register lifecycle participants only for state genuinely owned above `TerminalSession`;
- do not issue public queries from participant resume callbacks;
- do not acquire new presentation/rich-input state after suspension/teardown has begun;
- call `InvalidateState()` after known out-of-band terminal mutation not already managed by lifecycle;
- always dispose the session asynchronously;
- surface disposal/restoration failures rather than treating them as successful cleanup.

These rules are part of the durable 1.x ownership model.
