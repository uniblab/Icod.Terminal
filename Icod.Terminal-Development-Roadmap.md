# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Prior published line:** `1.16.0` — Persistent Raster Animation and Frame Lifecycle
- **Current development line:** `1.17.0-alpha.1` — Terminal-owned Screen Output Contracts
- **Development status:** T170 architecture and implementation track opened
- **Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range planning. Detailed design evidence remains in versioned roadmaps, release notes, public-API baselines, accepted CI checkpoints, and approved design/implementation documents.

The original pre-1.0 roadmap is preserved at [`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md).

## Latest accepted checkpoint

`Icod.Terminal 1.16.0` was published from annotated tag `v1.16.0` at exact commit `5e28d48936ab1d65657d672feee13bc9ef4fe017` on 2026-09-17. The stable candidate was accepted at exact head `4865691ea65b759a7fe5b279dea08ec8427a6278`, workflow #1959 / run `35250115968`, with the complete nine-job matrix successful.

The candidate produced the stable `1.16.0` NuGet and symbol packages, retained identical public API snapshots across all target frameworks with fingerprint `d2acfa85aad87c739b3f682096d4d7139627f12bc9d8981b65529eeb79a2da8d`, and passed cross-platform runtime/sample, package/API/XML, artifact, TermInfo integration, and stable `Icod.DCurses` downstream qualification.

Version 1.17 development begins from that published stable baseline. Publication of later prerelease/stable artifacts remains an explicit maintainer action.

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
- `Icod.Terminal` owns the live terminal conversation, input/query/event authority, semantic capability evidence and routing, terminal output, ephemeral raster routing, persistent raster resource/placement/placeholder ownership, animation/frame lifecycle, protocol-private encoding/identity, lifecycle certainty, and deterministic cleanup.
- `Icod.DCurses` owns cells, windows, virtual-screen state, screen coordinates, clipping, scrolling, layout, refresh/diff policy, damage, and higher-level presentation policy.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

The production dependency graph remains:

```text
Icod.TermInfo 1.14.0
Icod.Timing   1.0.0
```

Optional integration tests/samples may use `Icod.TermInfo.Inspection 1.14.0`; Inspection and Source remain outside the production package graph.

## Qualified stable-candidate sequence through 1.16.0

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
1.16.0  persistent raster animation and frame lifecycle             PUBLISHED
1.17.0  Terminal-owned dimensions, screen planning, and transactions ACTIVE
```

The final 1.15 public API fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```

Permanent ownership authority: [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

1.15 release notes: [`docs/releases/1.15.0.md`](docs/releases/1.15.0.md).

1.15 versioned evidence: [`Icod.Terminal-1.15.0-Development-Roadmap.md`](Icod.Terminal-1.15.0-Development-Roadmap.md).

## 1.16 stable candidate — Persistent Raster Animation and Frame Lifecycle

The completed 1.16 line extends the existing persistent-raster ownership model with terminal-resident animation frames and playback control while preserving the same architectural boundaries that guided 1.11–1.15.

The governing rule is:

> Terminal owns animation protocol identity, frame-sequence certainty, timing commands, and serialized control; callers own source frames, presentation placement, and higher-level animation policy.

The preferred semantic direction is:

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    +-- TerminalRasterPlaceholder
    +-- TerminalRasterAnimation
            |
            +-- TerminalRasterAnimationFrame
```

`TerminalRasterAnimation` is intended to represent one animation sequence associated with one existing persistent raster resource. The resource's original image data is the root frame. Additional full-size frames are transferred as terminal-resident animation frames and represented publicly by opaque semantic handles/tokens rather than protocol frame numbers.

Version 1.16 targets:

- a distinct semantic `PersistentRasterAnimation` capability;
- full-frame animation transfer only;
- opaque frame identity and bounded sequence tracking;
- per-frame positive timing;
- explicit current-frame selection;
- terminal-driven stop, loading-mode run, normal looping run, and loop-count policy;
- resource/session lifecycle propagation;
- explicit animation-sequence certainty loss after ambiguous committed frame operations;
- no retained source-frame replay cache;
- no transfer of layout/window/cell/damage ownership into Terminal.

Kitty frame composition/delta editing (`a=c`), partial-frame updates, gapless composition frames, and frame-to-frame pixel composition are intentionally deferred until the base animation lifecycle is stable.

## 1.16 tranche sequence

```text
T160  architecture/API-regret gate and public animation contract freeze
T161  semantic PersistentRasterAnimation capability and evidence rules
T162  private frame-sequence model, root-frame semantics, bounded identities
T163  acknowledged full-frame transfer and opaque frame publication
T164  per-frame timing and explicit current-frame selection
T165  terminal-driven stop/loading/run/loop playback semantics
T166  resource/session lifecycle propagation and sequence-certainty loss
T167  ENOENT/EINVAL/timeout/late/transport/capacity/concurrency hardening
T168  executable sample, downstream, package/API/XML/security/docs qualification
T169  stable 1.16.0 release closure
```

Exact public spellings and bounds were frozen by T160 and qualified through T168; the roadmap records the accepted semantic architecture.

## 1.16 design guardrails

The 1.16 design must preserve:

- one authoritative input/query/event path per live session;
- semantic capability planning rather than terminal-brand guessing;
- backend-neutral public raster semantics;
- opaque resource, physical-placement, virtual-placement, animation-frame, and generation identities;
- generation-scoped persistent ownership with no automatic replay;
- deterministic cleanup and committed-output integrity;
- side-effect-free 1.14 ownership observation;
- 1.15 placeholder layout ownership remaining caller-side;
- production routing remaining inside Terminal rather than `Icod.TermInfo.Inspection`;
- bounded memory/work for frame registries, transaction correlation, and tests;
- no hidden source-image/frame cache for reconstruction after lifecycle loss.

## Explicit 1.16 non-goals

Version 1.16 does not add:

- frame-to-frame composition or delta editing;
- partial-frame patch/update APIs;
- gapless composition frames;
- absolute screen-coordinate raster placement;
- pixel-within-cell positioning;
- GIF/APNG/image-file decoding or transcoding;
- audio or timeline synchronization;
- Terminal-owned scene/window/cell/damage/layout policy;
- terminal-authenticated passive existence queries;
- automatic replay/re-upload after generation loss;
- public Kitty image/frame ids or raw animation command dictionaries;
- PTY/ConPTY child-process hosting.

Frame composition remains a strong candidate for a later focused release after the 1.16 frame lifecycle is stable.

## Authorities

- [`Icod.Terminal-1.16.0-Development-Roadmap.md`](Icod.Terminal-1.16.0-Development-Roadmap.md)
- [`docs/superpowers/specs/2026-09-15-1.16.0-persistent-raster-animation-frame-lifecycle-design.md`](docs/superpowers/specs/2026-09-15-1.16.0-persistent-raster-animation-frame-lifecycle-design.md)
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)
- [`docs/Architecture.md`](docs/Architecture.md)
- [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md)
- [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md)

## 1.17 active development — Terminal-owned Screen Output Contracts

Version 1.17 prepares the semantic and transactional Terminal boundary required for a later TermInfo-free DCurses 2.0 package.

The governing rule is:

> Terminal owns terminal dimensions, semantic terminal-profile interpretation, safe screen-operation planning, and serialized output commitment; higher layers own retained cells, layout, damage, and refresh policy.

The tranche sequence is:

```text
T170  architecture, reference snapshot, API-regret gate, and development identity
T171  Terminal-owned dimensions and lifecycle projections
T172  immutable semantic TerminalProfile and screen-capability projection
T173  cursor, ACS glyph, alert, and opaque operation-plan foundation
T174  rendition normalization, color validation, transition, and reset planning
T175  erase, character/line shift, scroll, region, padding, and cost planning
T176  bounded session-bound transaction, output epoch, serialization, and framing
T177  hyperlink/raster composition, commitment, cleanup, and failure aggregation
T178  lifecycle/concurrency/security/downstream/package/API/XML hardening
T179  stable 1.17 release closure
```

Authorities:

- [`Icod.Terminal-1.17.0-Development-Roadmap.md`](Icod.Terminal-1.17.0-Development-Roadmap.md)
- [`docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md`](docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md)
- [`docs/superpowers/plans/2026-09-17-1.17.0-terminal-screen-output.md`](docs/superpowers/plans/2026-09-17-1.17.0-terminal-screen-output.md)

## Later development candidates

After 1.16, independent candidates still include animation-frame composition, absolute screen-coordinate placement, pixel-within-cell positioning, richer terminal-side reconciliation only if a truthful non-destructive primitive exists, image-file decoding/transcoding, and PTY/ConPTY process hosting.

Scene/window/cell ownership and hidden source-raster replay caches remain intentionally outside the Terminal contract.
