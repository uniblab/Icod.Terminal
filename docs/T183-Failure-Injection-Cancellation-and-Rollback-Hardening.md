# T183 — Failure Injection, Cancellation, and Rollback Hardening

**Release:** `Icod.Terminal 0.18.0-alpha.4`  
**PR:** #31  
**Validation:** workflow #879; presentation rollback audit regression added during T186

## Scope

T183 hardens failure semantics around multi-step state transitions and lifecycle re-entry without adding new terminal protocols or public feature surface.

The tranche focuses on:

- partial transition failure;
- rollback failure after partial mutation;
- caller-visible error preservation;
- manager/session state truthfulness after uncertainty;
- absence of ghost ownership after failed acquisition;
- lifecycle-pump failure propagation;
- final cleanup authority after failed re-entry.

## Rich-input transition rollback

`TerminalInputProtocolManager.TransitionTransactionalAsync(...)` already performs transactional rollback from the last known transition progress.

T183 adds focused failure injection proving the severe two-failure case:

1. bracketed-paste enable succeeds;
2. focus-reporting enable fails;
3. rollback attempts bracketed-paste disable;
4. rollback disable fails too.

The required behavior is:

- the original transition failure is preserved;
- the rollback failure is preserved;
- the caller receives an `AggregateException` rather than one failure masking the other;
- the failed acquisition does not retain a ghost lease;
- the manager marks applied physical state as unknown/invalid rather than falsely claiming the requested or baseline state;
- a later clean acquisition can recover through the normal invalidated-state path;
- normal lease disposal can subsequently restore baseline state.

Workflow #877 proved this path on Windows, Linux, and macOS.

## Presentation transition rollback

The final T186 audit added the corresponding presentation-manager double-failure regression.

The deterministic sequence is:

1. alternate-screen entry succeeds;
2. keypad entry fails;
3. transactional rollback attempts alternate-screen exit;
4. rollback exit fails too;
5. the caller receives an `AggregateException` containing transition and rollback failures;
6. the failed acquisition retains no ghost presentation lease;
7. a later clean alternate-screen acquisition/release succeeds through the invalidated-state recovery path.

This closes the direct regression gap between rich-input and presentation transactional rollback semantics without changing production behavior or public surface.

## Lifecycle re-entry plus rollback failure

T183 also covers lifecycle failure at the native mode layer.

The deterministic test sequence is:

1. initial session input-mode application succeeds;
2. suspend baseline restoration succeeds;
3. resume input-mode reapply fails;
4. lifecycle rollback attempts baseline restoration and also fails;
5. the lifecycle pump terminates;
6. the public lifecycle-event channel closes with the aggregate lifecycle failure as its inner exception;
7. `TerminationToken` is canceled;
8. `IsStateValid` remains `false`;
9. final session disposal remains authoritative and makes another restoration attempt.

The public exception shape is intentionally channel-aware: `ReadLifecycleEventAsync(...)` surfaces `ChannelClosedException`, whose `InnerException` is the `AggregateException` containing the re-entry and rollback failures.

Workflow #879 proved this behavior across Windows, Linux, and macOS, including all retained downstream/package gates.

## Cancellation boundary

Existing query and semantic-writer coverage already freezes the release's cancellation discipline:

- pre-emission cancellation emits nothing;
- query caller cancellation after emission does not truncate the committed wire request;
- post-emission query ownership remains bounded long enough to consume a late correlated response;
- one-frame semantic writers preserve zero partial output on pre-transmission validation/cancellation paths.

T183 found no additional production cancellation defect requiring API or wire changes.

## Closure

T183 is complete at `0.18.0-alpha.4`; the final T186 audit extended direct regression symmetry to presentation rollback failure.

No public API or terminal protocol surface was added.

Next: T184 — platform and terminal-mode hardening.
