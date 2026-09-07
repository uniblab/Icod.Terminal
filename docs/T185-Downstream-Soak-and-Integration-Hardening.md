# T185 — Downstream Soak and Integration Hardening

**Release:** `Icod.Terminal 0.18.0-alpha.6`  
**PR:** #31  
**Validation:** workflow #889

## Scope

T185 hardens the real `Icod.DCurses` integration boundary through repeated complete ownership cycles rather than isolated one-shot feature probes.

The tranche is intended to expose:

- leaked presentation or input-protocol ownership;
- stale Kitty keyboard state across full-screen handoff;
- duplicate cleanup after owner disposal;
- cumulative parser/session corruption;
- stranded terminal mode state;
- teardown ordering errors that only appear after repeated use.

## Independent 0.18 soak gate

T185 adds a new DCurses hardening soak instead of modifying the historical 0.17 modern-keyboard acceptance. This preserves the old release contract while giving 0.18 a dedicated downstream stress surface.

Each soak cycle performs the complete ownership sequence:

```text
TerminalSession open
    -> rich-input acquisition
        -> Kitty support negotiation
        -> bracketed paste
        -> focus reporting
        -> SGR mouse reporting
        -> Kitty AllKeys reporting
    -> CursesSession open
        -> full-screen keyboard handoff
        -> alternate-screen entry
        -> DCurses refresh
        -> rich input decode
    -> CursesSession disposal
        -> full-screen exit
        -> rich-input baseline restoration
        -> TerminalSession disposal
    -> stale rich-input lease disposal
```

The acceptance runs eight complete cycles for each target framework (`net8.0`, `net9.0`, `net10.0`).

## Required invariants

Across every cycle the soak verifies:

- Kitty support is negotiated before modern keyboard ownership is acquired;
- bracketed paste, focus reporting, SGR mouse reporting, and Kitty keyboard reporting coexist;
- DCurses full-screen entry orders Kitty pop before alternate-screen entry and Kitty push after it;
- a real DCurses refresh produces terminal output while rich input remains active;
- Kitty key events, focus events, and bracketed-paste events remain decodable while DCurses owns the presentation layer;
- DCurses full-screen exit restores keyboard ownership to the main screen in the correct order;
- rich-input cleanup returns the terminal to baseline;
- disposal of the stale caller-held protocol lease after DCurses ownership teardown emits no duplicate cleanup;
- each complete cycle applies and restores the terminal input mode exactly once.

## CI integration

The soak is a first-class validation gate:

- PR validation runs it on Windows, Linux, and macOS;
- each PR OS runs all three supported target frameworks;
- `VerifyDistribution.ps1` also runs it, so Release/main validation inherits the same soak across the six architecture runners.

This gives the 0.18 release line repeated real-downstream coverage in addition to the focused historical DCurses acceptances.

## Validation

Workflow #889 passed on Windows, Linux, and macOS, including the new soak, all focused DCurses acceptances, exact package validation, and every retained 0.8–0.17 package contract.

## Closure

T185 is complete at `0.18.0-alpha.6`.

No public API or terminal protocol surface was added.

Next: T186 — compatibility, documentation, package, and stable release closure.
