# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.12.0` — bounded advanced persistent-raster placement geometry
- **Current development line:** `1.13.0` — relative persistent-raster placement ownership
- **Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range release planning. Detailed historical design evidence remains in versioned roadmaps, tranche records, and release authorities rather than being duplicated here.

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
- `Icod.Terminal` owns the live terminal conversation, native modes, input decoding, semantic events, lifecycle, query routing, semantic capability evidence/planning, terminal output, ephemeral raster routing, persistent raster resource/placement ownership, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, layout, refresh/diff policy, damage, and higher-level curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## Published release sequence through 1.12.0

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
```

The final 1.12 public API fingerprint is:

```text
eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

The final 1.12 exact-head PR qualification was:

```text
cc0508d53e6c16133f638f9f71e65d4d805c6b65  #1673 / 34721041633
```

Version `v1.12.0` is published. Its stable ownership guarantees remain the base for 1.13.

## 1.13.0 — Relative Persistent-Raster Placement Ownership

Version 1.13 extends the opaque persistent-placement model with one new positioning/lifetime mode: a placement may be created relative to another current placement.

The 1.13 design deliberately treats this as an ownership problem rather than merely exposing Kitty `P`, `Q`, `H`, and `V` fields.

### Frozen architectural decisions

- Parentage is **immutable** from successful creation through disposal.
- Relative parent/child lifetime is orthogonal to raster-resource ownership: a relative child may place a different resource than its parent.
- A child raster resource remains independently owned even when one relative placement of that resource dies with its parent.
- The portable maximum relative-placement depth is **8**; deeper creation is rejected locally before output.
- Cycles are impossible through the public create-only immutable-parent API; internal invariants still reject any inconsistent graph state defensively.
- Relative offsets are signed terminal-cell column/row offsets, not pixels and not absolute screen coordinates.
- Existing `TerminalRasterPlacementOptions` remains the single common crop/extents/z-order contract.
- `CreateRelativePlacementAsync(...)` is distinct from current-cursor `CreatePlacementAsync(...)` so coordinate systems cannot change implicitly.
- `TerminalRasterPlacement.UpdateAsync(...)` preserves the placement's established positioning mode. For a relative placement it retains the immutable parent and current relative offsets while replacing common placement options.
- `UpdateRelativeAsync(...)` may change the signed column/row offsets and common placement options, but never the parent.
- No raw terminal image/placement identity becomes public.
- Parent loss, disposal, resource disposal, generation invalidation, and terminal `ENOPARENT` handling must update the local placement graph coherently.

### Planned public additions

Subject to the T130 API-regret gate, the intended additive surface is:

```csharp
TerminalRasterResource.CreateRelativePlacementAsync(
    TerminalRasterPlacement parent,
    int columnOffset,
    int rowOffset,
    TerminalRasterPlacementOptions? options = null,
    CancellationToken cancellationToken = default
)

TerminalRasterPlacement.UpdateRelativeAsync(
    int columnOffset,
    int rowOffset,
    TerminalRasterPlacementOptions? options = null,
    CancellationToken cancellationToken = default
)
```

No new capability enum is planned. Verified `PersistentRasterGraphics` remains the coarse runtime gate for the reviewed persistent backend.

### Tranche sequence

```text
T130  architecture/API regret gate + 1.13 roadmap/spec/plan
T131  internal immutable parent/depth graph model
T132  public relative-placement API contract + API freeze candidate
T133  local parent/current/depth validation and no-output rejection
T134  Kitty P/Q/H/V encoding + acknowledged create/update integration
T135  cascading parent/subtree cleanup + resource/session teardown ordering
T136  ENOPARENT/ECYCLE/ETOODEEP and lifecycle/adversarial hardening
T137  graph boundary/capacity/concurrency regression matrix
T138  sample/package/XML/downstream qualification
T139  final API freeze, release docs, three-OS/package closure
```

The versioned roadmap is [`Icod.Terminal-1.13.0-Development-Roadmap.md`](Icod.Terminal-1.13.0-Development-Roadmap.md).

The design and implementation authorities are:

- [`docs/superpowers/specs/2026-09-12-1.13.0-relative-persistent-raster-placement-design.md`](docs/superpowers/specs/2026-09-12-1.13.0-relative-persistent-raster-placement-design.md)
- [`docs/superpowers/plans/2026-09-12-1.13.0-relative-persistent-raster-placement.md`](docs/superpowers/plans/2026-09-12-1.13.0-relative-persistent-raster-placement.md)

## Stable architecture guardrails

The 1.x line continues to preserve:

- one authoritative input/query/event path per live session;
- semantic capability planning rather than terminal-brand guessing;
- bounded parsers, queries, semantic events, raster work, and persistent registries;
- backend-neutral public raster semantics;
- opaque persistent resource/placement identities;
- generation-scoped persistent ownership with no automatic replay;
- deterministic cleanup and committed-output integrity;
- production package dependencies declared centrally by `Icod.Terminal.csproj`;
- cells/windows/layout/damage ownership in `Icod.DCurses`, not `Icod.Terminal`.

## Deferred tracks after 1.13

The following remain separate future design questions and are not implied by relative placement ownership:

- Unicode placeholder / virtual placements;
- animation and frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell positioning;
- scene/window/cell ownership;
- hidden source-raster replay caches;
- image-file decoding/transcoding;
- PTY/ConPTY process hosting inside `Icod.Terminal`.

A likely future sequence is to consider virtual/Unicode-placeholder anchoring only after the relative-placement ownership graph is stable and a downstream text-grid consumer is ready to use it.

## Release discipline

Every 1.13 implementation tranche must preserve the normal Staging matrix. Stable release closure again requires exact-head Windows/Linux/macOS runtime validation, public API/package freeze, package contract shards, downstream Stable 1.x acceptance, and a validated package artifact before maintainer merge/tag/publish actions.
