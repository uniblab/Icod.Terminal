# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable candidate:** `1.12.0` — bounded advanced persistent-raster placement geometry
- **Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range release planning. Detailed historical design evidence remains in the versioned roadmaps and tranche documents rather than being duplicated here.

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

## Published release sequence through 1.11.1

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
```

Final 1.11 public API fingerprint, retained by 1.11.1:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

## 1.12.0 stable candidate

Version 1.12 deliberately extends the existing opaque placement object with only:

```text
pixel-space source rectangles
signed z-order
```

It does not add a scene graph, relative placement chain, Unicode placeholder model, animation/frame lifecycle, absolute screen-coordinate placement, public backend selector, or replay cache.

The accepted sequence is:

```text
T120  architecture/API regret gate + roadmap normalization                 accepted
T121  table-driven TermInfo semantic evidence                              accepted
T122  source-rectangle public contract + resource-aware validation         accepted
T123  z-order public contract                                               accepted
T124  acknowledged create/update encoder integration                       accepted
T125  lifecycle/adversarial/boundary hardening                             accepted
T126  sample/package/downstream qualification                              accepted
T127  API freeze and stable release closure                                accepted, with post-closure pre-merge hardening
```

The public 1.12 additions are exactly:

```text
TerminalRasterSourceRectangle
TerminalRasterSourceRectangle..ctor(int,int,int,int)
TerminalRasterSourceRectangle.X
TerminalRasterSourceRectangle.Y
TerminalRasterSourceRectangle.Width
TerminalRasterSourceRectangle.Height
TerminalRasterPlacementOptions.SourceRectangle
TerminalRasterPlacementOptions.ZIndex
```

Final 1.12 public API fingerprint:

```text
eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

Accepted implementation/qualification checkpoints:

```text
T124  aba1f7c0989d2c451294edf75590637c227a7e05  #1653 / 34713178166
T125  799d096fa439c43b4f31399a551c4524fe40fa10  #1657 / 34716833678
T126  7b38994c1ba936df5887d1aa015394c4ed626ddf  #1658 / 34717103814
T127 candidate
      0b7961f6d8151253be57f65a69e17a12ec4bdec5  #1659 / 34717812704
T127 first closure
      ef02714bcbd9ab84572a7ed8200a0ad8071d831a  #1660 / 34718248621
Pre-merge hardening
      4d509c75decc37d280a92769fe668d3c6fbd41ce  #1663 / 34720415393
```

Each listed GREEN workflow passed the full nine-job PR matrix.

The pre-merge audit also preserved the TDD RED witness for default-valued source rectangles at `f5f0a4f69d3571083f63654acd35b9893e8807dd`, workflow `#1662 / 34720241497`. The accepted fix revalidates every present `TerminalRasterSourceRectangle`, including `default(...)` values that bypass the public constructor, without changing the public API fingerprint.

The production dependency graph remains:

```text
Icod.TermInfo 1.11.0
Icod.Timing   1.0.0
```

The versioned roadmap and release authorities are:

- [`Icod.Terminal-1.12.0-Development-Roadmap.md`](Icod.Terminal-1.12.0-Development-Roadmap.md)
- [`docs/T127-1.12.0-Release-Closure.md`](docs/T127-1.12.0-Release-Closure.md)
- [`docs/releases/1.12.0.md`](docs/releases/1.12.0.md)
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)
- [`docs/Public-API-Baseline-1.12.md`](docs/Public-API-Baseline-1.12.md)

## Stable architecture guardrails

The 1.x line continues to preserve:

- one authoritative input/query/event path per live session;
- semantic capability planning rather than terminal-brand guessing;
- bounded parsers, queries, semantic events, raster work, and persistent registries;
- backend-neutral public raster semantics;
- opaque persistent resource/placement identities;
- current-cursor placement rather than a Terminal-owned layout engine;
- generation-scoped persistent ownership with no automatic replay;
- deterministic cleanup and committed-output integrity;
- production package dependencies declared centrally by `Icod.Terminal.csproj`.

## Long-range directions

Future development may consider only independently justified, separately reviewed tracks such as:

- richer terminal observations where the wire contract is sufficiently portable;
- additional semantic capability planning where truthful evidence exists;
- additional bounded output/state semantics with deterministic cleanup;
- higher-level integration needed by `Icod.DCurses` without moving cells/windows/layout into `Icod.Terminal`;
- PTY/ConPTY integration through the separate `Icod.Pty` project rather than by expanding this package’s runtime ownership.

Advanced raster features beyond 1.12—relative placement graphs, Unicode placeholders, animation, frame lifecycle, or scene ownership—remain future design questions rather than implied extensions of source cropping/z-order.

## Maintainer handoff rule

The post-hardening release-facing documentation consistency pass is the final branch change. Its exact head must pass the complete nine-job PR matrix before merge.

After that qualification, merge, mainline Release validation, `v1.12.0` tagging, GitHub Release creation, and NuGet publication remain maintainer/release-workflow actions.
