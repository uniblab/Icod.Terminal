# T182 — Lifecycle, Composition, and Concurrency Hardening

**Release line:** `Icod.Terminal 0.18.0`  
**Development target:** `0.18.0-alpha.3`  
**PR:** #31  
**Status:** complete and green at workflow #874

## Scope

T182 hardens session-owned state transitions across lifecycle, composition, public lease acquisition, manager serialization, control output, and teardown.

No new protocol surface is introduced.

## Lock graph

The authoritative order for screen-local input/presentation state is:

```text
state composition gate
    -> lifecycle/teardown acquisition-availability check
        -> manager gate
            -> session control-output gate
```

The composition gate is shared by `TerminalInputProtocolManager` and `TerminalPresentationManager` for one session. It prevents cross-manager interleaving such as a rich-input lease mutation entering the middle of a screen-local keyboard handoff.

Manager cleanup paths used by suspend, reentry, rollback, lease disposal, and final session disposal also acquire composition before entering the corresponding manager.

The lifecycle availability check is deliberately not a new mutex. `lifecycleStateReleased` is the state marker that says lifecycle owns terminal-state restoration/reentry. Public acquisition observes it only after acquiring composition, so an acquisition that was already committed before lifecycle began may finish and then be cleaned by suspend, while an acquisition that reaches composition after lifecycle ownership begins is rejected.

Teardown is similarly detected through the existing session-output acceptance state. `DisposeAsync()` closes normal session output before manager cleanup begins. Public state acquisition checks that state while holding composition and fails with `ObjectDisposedException` once teardown has started.

Cleanup is intentionally exempt from the acquisition barrier. Rich-input/presentation lease release, lifecycle suspension/reentry, rollback, and final close must remain able to use the control-output gate after ordinary session output has been closed.

## Defect found

Before T182, `AcquireInputProtocolsAsync()` and `AcquirePresentationAsync()` acquired the composition gate and entered their managers without checking whether lifecycle had already released session state.

Lifecycle sets `lifecycleStateReleased = 1` before participant preparation and before the input/presentation managers are suspended. Therefore a public acquisition starting during participant preparation could acquire new terminal ownership and emit control traffic inside the suspend sequence.

The same structural issue existed at teardown: manager transitions use the control-output gate intentionally so cleanup remains possible after `StopAcceptingSessionOutput()`, but public acquisition had no explicit teardown rejection before entering a manager.

## Correction

T182 adds a session-level `ThrowIfStateAcquisitionUnavailable()` check after acquiring composition and before any public manager acquisition.

It rejects:

- suspension preparation;
- suspended state;
- lifecycle reentry;
- session teardown/disposal.

It does not affect:

- existing lease release;
- lifecycle-owned suspend/reentry;
- rollback;
- manager close;
- final cleanup output.

## New regression coverage

`TerminalStateAcquisitionLifecycleHardeningTests` provides deterministic coverage.

### Suspend-preparation barrier

A lifecycle participant blocks inside `PrepareForTerminalSuspendAsync()` after lifecycle has already marked state released but before rich-input/presentation suspension begins.

While blocked, the test proves:

- `IsStateValid` is false;
- bracketed-paste acquisition is rejected;
- alternate-screen acquisition is rejected;
- neither attempt emits terminal-control bytes;
- after lifecycle resumes successfully, both acquisitions work normally;
- their later lease disposal restores the expected state exactly.

### Teardown barrier

The test starts `DisposeAsync()` and then attempts the same two public acquisitions.

It proves:

- both fail with `ObjectDisposedException`;
- no new terminal-control ownership is emitted after teardown begins.

## Existing retained concurrency coverage

T182 retains T175/T176 coverage proving:

- keyboard lease release cannot interleave with managed alternate-screen handoff;
- failed screen switching restores keyboard ownership and permits retry;
- Kitty pop/switch/push shares the same composition domain as other rich-input mutations;
- real DCurses full-screen ownership composes with rich input and deterministic disposal.

## Closure

Workflow #874 passed Windows, Linux, and macOS together with the retained downstream/package gates. The lock-order audit found no reverse-order acquisition requiring another production change.

T182 closed at `0.18.0-alpha.3` with no public API or wire-protocol expansion.
