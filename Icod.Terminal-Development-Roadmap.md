# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.13.0` — relative persistent-raster placement ownership
- **Current development line:** `1.14.0` — persistent-raster lifecycle observability
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
- `Icod.Terminal` owns the live terminal conversation, native modes, input decoding, semantic events, lifecycle, query routing, semantic capability evidence/planning, terminal output, ephemeral raster routing, persistent raster resource/placement ownership, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, layout, refresh/diff policy, damage, and higher-level curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## Published release sequence through 1.13.0

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
```

The final 1.13 public API fingerprint is:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

Version `1.13.0` adds exactly two public methods over 1.12:

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

## 1.13 stable architecture

Relative placement is deliberately a bounded ownership/positioning feature rather than a scene graph.

The permanent decisions are:

- parentage is immutable from successful creation through disposal;
- relative parent/child lifetime is orthogonal to raster-resource ownership;
- a child placement may use a different raster resource from its parent;
- deleting a parent placement removes its relative placement subtree but does not automatically dispose descendant raster resources;
- the portable maximum relative depth is 8;
- public construction cannot form cycles; internal ancestry validation fails closed defensively;
- relative offsets are signed terminal-cell offsets, not source pixels or absolute screen coordinates;
- `TerminalRasterPlacementOptions` remains the common crop/extents/z-order contract;
- ordinary `UpdateAsync(...)` preserves the established positioning mode;
- `UpdateRelativeAsync(...)` changes offsets/common geometry but never parentage;
- current cleanup is deepest-first / descendant-before-parent and uses each placement's own owning-resource identity;
- resource and placement capacity ceilings remain 256 and 4096;
- generation-scoped certainty, one authoritative query/input path, committed-output integrity, and no hidden replay remain unchanged;
- no public terminal image/placement/parent numeric identity or backend selector is introduced.

Permanent authority: [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

Release notes: [`docs/releases/1.13.0.md`](docs/releases/1.13.0.md).

Versioned development evidence: [`Icod.Terminal-1.13.0-Development-Roadmap.md`](Icod.Terminal-1.13.0-Development-Roadmap.md).

## 1.14 selected development track

Version `1.14.0` is **Persistent Raster Lifecycle Observability**.

The governing principle is:

> Expose Terminal's current ownership certainty; do not claim to observe terminal state that the protocol cannot non-destructively prove.

The approved design adds side-effect-free lifecycle observation for opaque persistent raster resource/placement handles while preserving backend neutrality, generation-scoped certainty, independent resource versus placement lifetime, and the no-replay/no-private-identity boundaries established through 1.13.

The public model is planned around four semantic states:

```text
Current
Stale
Released
Disposed
```

with one immutable state/reason snapshot. T140 owns the final public spelling/API freeze before production implementation proceeds.

The selected tranche sequence is:

```text
T140  architecture/API-regret gate and public snapshot freeze
T141  internal lifecycle-state normalization
T142  resource ownership observability
T143  placement ownership observability
T144  loss/release reason classification
T145  relative-graph propagation hardening
T146  concurrency and memory-model qualification
T147  sample and downstream consumer qualification
T148  package/API/XML/documentation qualification
T149  stable 1.14 release closure
```

Design authority: [`docs/superpowers/specs/2026-09-13-1.14.0-persistent-raster-lifecycle-observability-design.md`](docs/superpowers/specs/2026-09-13-1.14.0-persistent-raster-lifecycle-observability-design.md).

Implementation plan: [`docs/superpowers/plans/2026-09-13-1.14.0-persistent-raster-lifecycle-observability.md`](docs/superpowers/plans/2026-09-13-1.14.0-persistent-raster-lifecycle-observability.md).

Versioned roadmap: [`Icod.Terminal-1.14.0-Development-Roadmap.md`](Icod.Terminal-1.14.0-Development-Roadmap.md).

The 1.14 architectural design has been approved. The implementation plan is complete; T140 is the next execution tranche.

## Stable architecture guardrails

The 1.x line continues to preserve:

- one authoritative input/query/event path per live session;
- semantic capability planning rather than terminal-brand guessing;
- bounded parsers, queries, semantic events, raster work, persistent registries, and relative graph depth;
- backend-neutral public raster semantics;
- opaque persistent resource/placement identities;
- generation-scoped persistent ownership with no automatic replay;
- deterministic cleanup and committed-output integrity;
- production package dependencies declared centrally by `Icod.Terminal.csproj`;
- cells/windows/layout/damage ownership in `Icod.DCurses`, not `Icod.Terminal`.

## Deferred tracks after 1.14 selection

Future design candidates remain independent questions, including:

- Unicode placeholder / virtual placements;
- animation and frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell positioning;
- richer terminal-side reconciliation only if a truthful non-destructive protocol primitive exists and downstream need justifies it;
- scene/window/cell ownership (still expected to remain above `Icod.Terminal`);
- hidden source-raster replay caches (currently excluded);
- image-file decoding/transcoding;
- PTY/ConPTY process hosting inside `Icod.Terminal`.

These remain outside 1.14 unless the versioned roadmap is explicitly reopened through another API-regret review.

## Release discipline

Stable releases require exact-head Windows/Linux/macOS runtime validation, public API/package freeze, package contract shards, downstream Stable 1.x acceptance, and a validated package artifact before maintainer merge/tag/publish actions.
