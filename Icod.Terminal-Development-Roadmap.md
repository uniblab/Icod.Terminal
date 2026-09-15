# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable candidate:** `1.15.0` — Unicode Placeholder and Virtual Raster Placement
- **Stable compatibility floor:** `1.0.0`
- **Next development track:** not yet selected

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range planning. Detailed historical design evidence remains in versioned roadmaps, tranche records, release notes, public-API baselines, and accepted CI checkpoints.

The original pre-1.0 roadmap is preserved at [`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md).

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
- `Icod.Terminal` owns the live terminal conversation, input/query/event authority, semantic capability evidence and routing, terminal output, ephemeral raster routing, persistent resource/physical-placement/virtual-placeholder ownership, protocol-private encoding/identity, lifecycle certainty, and deterministic cleanup.
- `Icod.DCurses` owns cells, windows, virtual-screen state, screen coordinates, clipping, scrolling, layout, refresh/diff policy, and damage.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

The 1.15 production dependency graph is:

```text
Icod.TermInfo 1.14.0
Icod.Timing   1.0.0
```

Optional integration tests/samples use `Icod.TermInfo.Inspection 1.14.0`; Inspection and Source remain outside the production package graph.

## Published/stable release sequence through 1.15.0

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
1.15.0  Unicode placeholder and virtual raster placement
```

The final 1.15 public API fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```

Permanent ownership authority: [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

Release notes: [`docs/releases/1.15.0.md`](docs/releases/1.15.0.md).

Versioned evidence: [`Icod.Terminal-1.15.0-Development-Roadmap.md`](Icod.Terminal-1.15.0-Development-Roadmap.md).

## 1.15 final architecture — Unicode Placeholder and Virtual Raster Placement

The governing principle is:

> Terminal owns protocol identity and encoding; higher-level renderers own cells, cursor position, clipping, scrolling, damage, and layout.

The final public model is:

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

Portable bounds remain explicit:

```text
resources                                      256
physical + virtual placements                 4096
relative depth                                   8
placeholder rows                              1..256
placeholder columns                           1..256
private virtual-placement id          1..0x00FFFFFF
```

`TerminalCapability.UnicodeRasterPlaceholders = 10` is distinct from both ordinary raster display and persistent physical-placement ownership. Generic raster or persistent support does not silently become placeholder verification.

Every placeholder cell is independently renderable and current-cursor based. Protocol-private image identity, virtual-placement identity, reserved codepoint, diacritic tables, and SGR packing remain private. Physical placements may use a current virtual placeholder as immutable relative parent through `CreateRelativePlacementFromPlaceholderAsync(...)`.

## 1.15 tranche closure

```text
T150  architecture/API-regret gate and public contract freeze        COMPLETE
T151  semantic capability and protocol-support evidence model       COMPLETE
T152  virtual-placement ownership and lifecycle integration         COMPLETE
T153  self-contained placeholder-cell token and encoder             COMPLETE
T154  typed current-cursor placeholder emission                     COMPLETE
T155  virtual-parent relative-placement integration                 COMPLETE
T156  error/lifecycle/capacity/concurrency hardening                COMPLETE
T157  samples and downstream integration qualification              COMPLETE
T158  package/API/XML/security/documentation qualification          COMPLETE
T159  stable 1.15.0 release closure                                 COMPLETE
```

Accepted checkpoints:

```text
T152  b319051b7830ea2bb6ca7d0a1344e7e4fc67e068  #1820 / 34894849866
T153  5666d74f9b91c708869006d3188cb54012bef976  #1823 / 34896470148
T154  6b3d8e5393f04fe679de44d6d96851a5700976a1  #1826 / 34897742454
T155  4bee4bb76371a1cec2a9e2676c12e1f349e7abda  #1834 / 34899992381
T156  cd219f4661f1f170497514f7c868c2105a93e609  #1850 / 34913940016
T157  4cdb8d432f55d8d9d7e8b9581fb73f8dd31ae32a  #1858 / 34914701316
T158  72cd6401e473189934391456999deca7ae511fa1  #1877 / 34994616788
T159  1a3c4af503f1256eeff077268bb50e45abac568d  #1882 / 34997043791
```

Each listed acceptance workflow passed the complete nine-job PR matrix at the exact recorded head.

T159's accepted candidate contains the stable `1.15.0` version authority, curated release notes, changelog, package release metadata, packed README, `Icod.TermInfo 1.14.0` production dependency, optional `Icod.TermInfo.Inspection 1.14.0` integration, and the final API fingerprint. This later roadmap bookkeeping commit records that already-qualified candidate; it is not the candidate's self-certification.

## TermInfo 1.14 integration boundary

TermInfo 1.14 advisory raster-backend planning is qualified only at the optional consumer boundary:

```text
Icod.Terminal production router
    owns live routing, protocol commitment, opaque identity, and cleanup

Icod.TermInfo.Inspection RasterBackendPlanner
    remains optional caller-owned advisory planning
```

A conclusive live `PersistentRasterGraphics` observation may be caller-mapped to Kitty availability because Terminal's reviewed persistent route is Kitty-based. Ordinary `RasterGraphics` does not identify Kitty versus Sixel, and `UnicodeRasterPlaceholders` is not fed into TermInfo 1.14's frozen lifecycle/placement planners. Separate backend contexts prevent evidence from silently crossing backend boundaries.

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
- optional TermInfo advisory backend planning without importing it into the production router;
- production package dependencies declared centrally by `Icod.Terminal.csproj`;
- cells/windows/layout/damage ownership in `Icod.DCurses`, not `Icod.Terminal`.

## Future development

No post-1.15 track is selected by this release closure. Independent candidates still include animation/frame lifecycle, absolute screen-coordinate placement, pixel-within-cell positioning, richer terminal-side reconciliation only if a truthful non-destructive primitive exists, image-file decoding/transcoding, and PTY/ConPTY process hosting.

Scene/window/cell ownership and hidden source-raster replay caches remain intentionally outside the current Terminal contract.

Merge, tag `v1.15.0`, GitHub Release creation, and package publication remain explicit maintainer/release-workflow actions after PR qualification.
