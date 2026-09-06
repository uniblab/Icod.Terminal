# T146 — Composition and DCurses Scoped Color Acceptance

**Release:** `Icod.Terminal 0.14.0`  
**Version:** `0.14.0-alpha.7`  
**Status:** Implemented; exact-head validation pending.

## Objective

T146 proves that lifecycle-safe palette and dynamic-color ownership compose with the existing terminal-session output model and with a real downstream `Icod.DCurses` consumer using only public APIs.

## Downstream acceptance

The existing `tools/dcurses-color-observation-acceptance` consumer now exercises the scoped ownership APIs directly:

- acquires `TerminalPaletteColorLease` for palette index 2;
- requires an OSC 4 baseline observation before mutation;
- applies the requested owned palette color;
- acquires `TerminalDynamicColorLease` for `DefaultBackground`;
- requires an OSC 11 baseline observation before mutation;
- applies the requested owned dynamic color;
- converts the lease colors through the downstream high-byte adaptation used by DCurses;
- renders a real `CursesStyle` through `CursesSession`;
- proves DCurses emits the expected RGB foreground/background capability paths and application text;
- disposes the downstream curses session while Terminal color leases remain active;
- releases the dynamic and palette leases;
- proves exact replay of the observed OSC 11 and OSC 4 external baselines;
- proves OSC 111 and OSC 104 reset forms are not substituted for restoration.

The consumer targets:

```text
net8.0
net9.0
net10.0
```

and continues to reference the real `Icod.DCurses 0.1.0` package rather than private test doubles for downstream behavior.

## Composition boundary

T140–T144 already prove manager-level composition with the shared active-query router, lifecycle ordering, invalidation, concurrent color ownership, and session cleanup. T146 adds the downstream acceptance layer and confirms that scoped color ownership does not require DCurses to parse OSC, register lifecycle participants, or access internal Terminal state.

Existing CI acceptance for synchronized output, progress, pointer shape, semantic prompt, and color behavior remains retained. No existing manager is bypassed for the scoped-color acceptance path.

## Conclusion

T146 is complete when the exact `0.14.0-alpha.7` head passes the full Windows/Linux/macOS PR workflow, including the extended DCurses color acceptance tool.

The next tranche is T147: public API, documentation, package, and stable-release closure.
