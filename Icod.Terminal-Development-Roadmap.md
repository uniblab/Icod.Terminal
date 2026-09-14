# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.14.0` — persistent-raster lifecycle observability
- **Current development line:** `1.15.0` — Unicode Placeholder and Virtual Raster Placement
- **Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range release planning. Detailed historical design evidence remains in versioned roadmaps, tranche records, release notes, and public-API baseline documents rather than being duplicated here.

The original pre-1.0 roadmap is preserved at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

## Current architecture

```text
Icod.TermInfo
      ^
      |
Icod.Terminal
      ^
      |
Icod.DCurses
      ^
      |
terminal applications
```

- `Icod.TermInfo` owns immutable terminal capability data and expansion.
- `Icod.Terminal` owns the live terminal conversation, native modes, input decoding, semantic events, lifecycle, query routing, semantic capability evidence/planning, terminal output, ephemeral raster routing, persistent raster resource/placement ownership, lifecycle certainty, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, layout, refresh/diff policy, damage, and higher-level curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## Published release sequence through 1.14.0

```text
1.5.0   normalized control families / capability evidence / semantic routing
1.6.0   complete CSI grammar / terminal and cell pixel geometry
1.7.0   DCS / Sixel / public backend-neutral raster contract
1.8.0   APC / Kitty Graphics / verified multi-backend raster routing
1.8.1   documentation and sample maintenance
1.9.0   unsolicited protocol-neutral semantic events
1.10.0  semantic capability inspection and explicit bounded verification
1.11.0  opaque persistent raster resources and placements
1.11.1  TermInfo persistent-raster lifecycle integration contract
1.12.0  bounded source-pixel cropping and signed z-order
1.13.0  bounded immutable-parent relative placement ownership
1.14.0  side-effect-free persistent-raster lifecycle observability
```

The final 1.14 public API fingerprint is:

```text
2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
```

Version 1.14 added one backend-neutral ownership vocabulary:

```text
TerminalRasterOwnershipStatus
TerminalRasterOwnershipLossReason
TerminalRasterOwnershipState
TerminalRasterResource.OwnershipState
TerminalRasterPlacement.OwnershipState
```

The permanent 1.14 rule remains:

> Expose Terminal's current ownership certainty; do not claim to observe terminal state that the protocol cannot non-destructively prove.

`Current`, `Stale`, `Released`, and `Disposed` remain atomic semantic states paired with semantic loss/release reasons. Observation is synchronous, bounded, and side-effect free.

Permanent ownership authority: [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

Release notes: [`docs/releases/1.14.0.md`](docs/releases/1.14.0.md).

Versioned evidence: [`Icod.Terminal-1.14.0-Development-Roadmap.md`](Icod.Terminal-1.14.0-Development-Roadmap.md).

## 1.15 selected development track — Unicode Placeholder and Virtual Raster Placement

Version `1.15.0` adds a backend-neutral semantic abstraction for Unicode-placeholder raster presentation.

The governing principle is:

> Terminal owns protocol identity and encoding; higher-level renderers own cells, cursor position, clipping, scrolling, damage, and layout.

The selected public-model direction adds a new opaque virtual-placement handle and semantic text-grid cell token:

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    |       ordinary or relative physical placement
    |
    +-- TerminalRasterPlaceholder
            acknowledged virtual placement
            |
            +-- TerminalRasterPlaceholderCell
                    semantic row/column token
```

The placeholder handle owns virtual-placement lifetime, dimensions, private identity, session/generation association, and lifecycle state. It does **not** own a screen position, window, DCurses cell, clipping region, scrolling policy, damage map, or layout.

The cell token exposes semantic row/column coordinates only. Kitty image ids, placement ids, `U+10EEEE`, combining-mark tables, SGR identity packing, `U=1`, raw APC commands, backend selection, and session generation ids remain private.

The approved portable placeholder bounds are:

```text
Columns  1..256
Rows     1..256
```

Virtual placements share the existing 4096 combined placement ceiling. Resources remain bounded to 256 and relative depth remains 8.

Virtual-placement private ids are restricted to the nonzero 24-bit range `1..0x00FFFFFF`, matching the complete placement identity representable in Unicode-placeholder underline color while remaining far above the local 4096-placement ceiling.

Every placeholder cell must be independently renderable. Icod.Terminal will not rely on left-neighbor shorthand for repeated identity or coordinates. This preserves correctness for clipping, sparse redraw, scrolling, overlapping images, arbitrary cell order, and future DCurses virtual-screen diffing.

Placeholder-cell emission is typed current-cursor text output. Terminal serializes and encodes the token but does not choose its screen position. Private foreground/underline identity state is terminated with selective reset so it does not leak into following application text.

Version 1.15 should add one semantic capability, with exact spelling frozen in T150:

```text
TerminalCapability.UnicodeRasterPlaceholders = 10
```

The new capability remains distinct from `PersistentRasterGraphics`; support is not invented from terminal brand or from a generic graphics result without reviewed evidence.

A physical `TerminalRasterPlacement` may use a `TerminalRasterPlaceholder` as immutable relative parent, while the virtual placeholder itself cannot be relative. Existing 1.13 graph/lifetime guarantees remain authoritative.

The selected tranche sequence is:

```text
T150  architecture/API-regret gate and public contract freeze
T151  semantic capability and protocol-support evidence model
T152  virtual-placement ownership and lifecycle integration
T153  self-contained placeholder-cell token and encoder
T154  typed current-cursor placeholder emission
T155  virtual-parent relative-placement integration
T156  error/lifecycle/capacity/concurrency hardening
T157  samples and downstream integration qualification
T158  package/API/XML/security/documentation qualification
T159  stable 1.15.0 release closure
```

Design authority:

[`docs/superpowers/specs/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement-design.md`](docs/superpowers/specs/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement.md`](docs/superpowers/plans/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement.md)

Versioned roadmap:

[`Icod.Terminal-1.15.0-Development-Roadmap.md`](Icod.Terminal-1.15.0-Development-Roadmap.md)

The 1.15 architecture is approved. The implementation plan is complete. T150 is the next execution tranche; no production 1.15 implementation has begun on this planning checkpoint.

## Stable architecture guardrails

The 1.x line continues to preserve:

- one authoritative input/query/event path per live session;
- semantic capability planning rather than terminal-brand guessing;
- bounded parsers, queries, semantic events, raster work, persistent registries, placeholder dimensions, and relative graph depth;
- backend-neutral public raster semantics;
- opaque persistent resource/physical-placement/virtual-placement identities;
- generation-scoped persistent ownership with no automatic replay;
- deterministic cleanup and committed-output integrity;
- side-effect-free local lifecycle observation without invented remote-existence guarantees;
- placeholder text rendering without transferring layout ownership into Terminal;
- production package dependencies declared centrally by `Icod.Terminal.csproj`;
- cells/windows/layout/damage ownership in `Icod.DCurses`, not `Icod.Terminal`.

## Deferred tracks after 1.15 selection

Future independent design candidates include:

- animation and frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell positioning;
- richer terminal-side reconciliation only if a truthful non-destructive protocol primitive exists and downstream need justifies it;
- scene/window/cell ownership (still expected to remain above `Icod.Terminal`);
- hidden source-raster replay caches (currently excluded);
- image-file decoding/transcoding;
- PTY/ConPTY process hosting inside `Icod.Terminal`.

These remain outside 1.15 unless the versioned roadmap is explicitly reopened through another API-regret review.
