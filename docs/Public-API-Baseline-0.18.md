# Icod.Terminal 0.18 Public API Baseline

**Release:** `0.18.0`  
**Theme:** hardening and invariant closure  
**Predecessor:** `0.17.0`

---

## 1. Public delta

`0.18.0` introduces **no new public API surface** relative to `0.17.0`.

The production changes in the 0.18 development line are behavioral hardening inside existing public operations. No public type, member, enum value, method signature, parameter, return type, target framework, or terminal wire protocol is intentionally added or removed.

All public contracts frozen by `docs/Public-API-Baseline-0.17.md` remain the 0.18 public surface.

---

## 2. Hardened acquisition semantics

The existing public methods:

```csharp
ValueTask<TerminalControlResult<TerminalInputProtocolLease>> AcquireInputProtocolsAsync(...)
ValueTask<TerminalControlResult<TerminalPresentationLease>> AcquirePresentationAsync(...)
```

retain their signatures and ordinary behavior.

0.18 strengthens their lifecycle/teardown semantics. New state ownership is rejected once the session has released terminal state for suspend/re-entry or once session teardown has begun.

The availability check occurs inside the shared state-composition domain, before either manager is entered.

The authoritative ordering is:

```text
state composition
    -> lifecycle/teardown availability
        -> manager gate
            -> control output
```

This closes the race where a caller could previously begin new state ownership after lifecycle had already declared the session state released.

Cleanup operations remain permitted while state is released so rollback, lease disposal, lifecycle re-entry, and final manager cleanup retain restoration authority.

---

## 3. Query/parser guarantees

0.18 preserves the existing query and parser public surface while strengthening regression guarantees:

- bounded incremental input buffering;
- deterministic malformed/oversized response recovery;
- stale emitted query ownership cannot be overtaken by lifecycle observation traffic;
- queued old-generation transactions never emit after suspend;
- post-emission cancellation does not truncate committed query output;
- ordinary input remains independent of active query routing.

No query API or wire protocol was added.

---

## 4. Failure and rollback guarantees

0.18 freezes stronger failure semantics behind existing APIs:

- multi-step rich-input transition failure attempts rollback;
- if transition and rollback both fail, both errors are preserved;
- failed acquisition leaves no ghost lease;
- uncertain applied state is marked untrustworthy rather than advanced optimistically;
- lifecycle re-entry plus rollback failure leaves `IsStateValid == false` and cancels `TerminationToken`;
- lifecycle-pump failure is surfaced through the existing lifecycle-event channel contract;
- final disposal retains restoration authority after earlier failures.

No new exception type or public failure-result type is introduced by 0.18.

---

## 5. Platform restoration guarantees

The existing POSIX and Windows public terminal-mode contracts are unchanged.

0.18 explicitly freezes session-level symmetry:

- POSIX configured mode application and exact baseline restoration use `TerminalModeApplyTiming.AfterOutputDrained`;
- Windows console configured mode application and exact baseline restoration use `TerminalModeApplyTiming.Immediately`;
- restoration uses the exact captured `TerminalModeSnapshot` rather than synthesizing an approximation;
- redirected/non-interactive endpoint policy remains unchanged.

---

## 6. Downstream integration guarantee

A real `Icod.DCurses 0.1.0` consumer now runs a repeated hardening soak across `net8.0`, `net9.0`, and `net10.0`.

The soak repeatedly proves complete ownership cycles involving:

- TerminalSession open/dispose;
- rich-input protocol acquisition;
- Kitty negotiation and screen-local handoff;
- bracketed paste, focus, and mouse coexistence;
- real DCurses full-screen open/refresh/dispose;
- modern input decoding;
- exact terminal-mode restoration;
- stale-lease disposal without duplicate cleanup.

This extends validation depth without changing public API.

---

## 7. Explicit exclusions

0.18 does not add:

- new terminal protocols;
- raw escape-sequence APIs;
- generic OSC/vendor command writers;
- terminal-brand heuristics;
- keyboard remapping policy or global hotkeys;
- IME control;
- graphics/image protocols;
- broader PTY or process-management surface.

The safety and ownership exclusions from 0.17 remain intact.

---

## 8. Compatibility

The stable package continues to target:

```text
net8.0
net9.0
net10.0
```

Assembly version remains `0.18.0.0` for the 0.18 release line.

A fresh package-only 0.18 hardening consumer must validate the unchanged public surface and the lifecycle state-acquisition hardening contract on every supported TFM, independently of project references.

This document freezes the stable 0.18 public API baseline.
