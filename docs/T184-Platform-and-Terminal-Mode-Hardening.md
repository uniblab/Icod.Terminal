# T184 — Platform and Terminal-Mode Hardening

**Release:** `Icod.Terminal 0.18.0-alpha.5`  
**PR:** #31  
**Validation:** workflow #882

## Scope

T184 hardens the platform-specific terminal-mode contract without adding new public API or terminal protocol surface.

The tranche covers:

- POSIX termios semantic mode mapping;
- Windows console semantic mode mapping;
- platform-specific apply timing;
- exact captured-baseline restoration;
- redirected/non-interactive output policy;
- initialization cancellation and failure rollback;
- restoration failure propagation.

## Platform-specific input-mode policy

Existing policy-level tests already prove:

- Linux canonical, cbreak, and raw transformations preserve unrelated native bits;
- macOS raw-mode editing uses Darwin-specific flow-control and local-mode bit values rather than Linux values;
- POSIX noncanonical modes establish the correct VMIN/VTIME semantics;
- Windows canonical, cbreak, and raw mappings preserve unrelated console flags;
- Windows input echo remains independently configurable;
- POSIX semantic input-mode application uses `TerminalModeApplyTiming.AfterOutputDrained`;
- Windows console input-mode application uses `TerminalModeApplyTiming.Immediately`;
- Windows output-mode snapshots cannot be used as input-mode baselines.

## Session-level restoration symmetry

T184 adds explicit session-level tests proving the restore side uses the same platform contract as the apply side.

For POSIX:

- the configured session mode is applied with `AfterOutputDrained`;
- final disposal restores the exact captured `TerminalModeSnapshot` instance;
- restoration uses `AfterOutputDrained`.

For Windows console input:

- the configured session mode is applied with `Immediately`;
- final disposal restores the exact captured `TerminalModeSnapshot` instance;
- restoration uses `Immediately`.

The session does not synthesize an approximate baseline during cleanup. The captured baseline remains the restoration authority.

## Redirected and non-interactive endpoints

Existing `TerminalSessionTests` already freeze the endpoint policy:

- when interactive output is required, redirected output is rejected before mode capture or mutation;
- no terminal mode is changed on that rejection path;
- callers may explicitly permit non-interactive output while retaining interactive input;
- such a session reports `IsInteractive == false` and preserves the observed endpoint truth rather than pretending both endpoints are terminals.

No T184 production change was required here.

## Exceptional initialization and restoration

Existing session coverage also proves:

- controlled mode-apply failure restores the captured baseline;
- provider exceptions during apply still enter baseline restoration;
- cancellation observed after a successful mutation restores the baseline;
- simultaneous initialization and restoration failure surfaces both errors in an `AggregateException`;
- explicit invalidation marks session state invalid while preserving final cleanup authority;
- cancellation before initialization performs no observation or mutation.

Together with T183 lifecycle rollback hardening, this closes the main exceptional terminal-mode paths for the 0.18 line.

## Validation

Workflow #882 passed on Windows, Linux, and macOS. Linux also passed exact package validation and every retained 0.8–0.17 package contract; all real DCurses acceptance gates remained green.

## Closure

T184 is complete at `0.18.0-alpha.5`.

No public API or wire protocol was added or changed.

Next: T185 — downstream soak and integration hardening.
