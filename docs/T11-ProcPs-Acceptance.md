# T11 — ProcPs Acceptance: `watch`, `slabtop`, and `top`

**Status:** Complete

**Release line:** `Icod.Terminal 0.1.0`

**Acceptance layer:** `Icod.DCurses` over `Icod.Terminal` and `Icod.TermInfo`

## Purpose

T11 uses the three initial ProcPs full-screen consumers as an architectural acceptance gate for the `Icod.Terminal 0.1.0` foundation. The purpose is not to duplicate each application's functional test plan. It is to verify that the shared terminal stack supplies the reusable live-terminal mechanisms those applications need without forcing terminal-control logic back into `Icod.DCurses` or the applications themselves.

The accepted runtime layering is:

```text
watch / slabtop / top
        |
   Icod.DCurses
        |
   Icod.Terminal
        |
   Icod.TermInfo
        |
 terminal / tty
```

`Icod.Timing` supplies the monotonic timing primitives used by `Icod.Terminal` for relative event deadlines and Escape-sequence ambiguity handling.

## Acceptance result

The T11 consumer milestone is complete. `watch`, `slabtop`, and `top` are functioning through `Icod.DCurses` on the shared terminal substrate, and no additional application-private terminal mechanism was required to close the acceptance tranche.

### `watch`

The accepted `watch` path uses `Icod.DCurses` / `Icod.Terminal` for its interactive display, input, resize, and restoration behavior. Watched child-process output remains pipe-based; ordinary `watch` semantics do not require PTY ownership.

### `slabtop`

The accepted `slabtop` path uses the shared stack for full-screen presentation, timed refresh combined with immediate input wake-up, resize/repaint behavior, and deterministic terminal restoration.

### `top`

The accepted `top` path uses the shared stack for full-screen presentation, semantic input discipline and noecho operation, shared text/control/navigation input decoding, periodic sampling combined with keyboard wake-up, resize and supported lifecycle handling, and restoration of host mode and presentation state.

## Responsibility boundary confirmed by acceptance

The consumer exercise confirms the intended division of responsibility:

- `Icod.TermInfo` remains the immutable capability and terminal-description authority;
- `Icod.Terminal` owns live endpoint observation, host mode, input decoding, dimensions, lifecycle, terminal identity/output setup, timing-aware event reads, and reversible presentation leases;
- `Icod.DCurses` owns cells, windows, virtual-screen state, rendition policy, damage tracking, and refresh/diff behavior;
- `watch`, `slabtop`, and `top` retain only application policy and presentation choices specific to each utility;
- `Icod.Pty` remains orthogonal and is not required for the `0.1.0` acceptance consumers.

The T10 lifecycle-participant seam remains part of this boundary: higher layers may prepare and restore their own presentation state around suspend/resume without taking ownership of platform signal or console-cancellation plumbing from `TerminalSession`.

## Closure

T11 closes the consumer-driven architectural portion of the `0.1.0` roadmap. It does not by itself declare the `0.1.0` package ready for release. T12 remains responsible for the public-API regret audit, behavioral documentation, package/fresh-consumer validation, release CI, and final non-prerelease package closure.
