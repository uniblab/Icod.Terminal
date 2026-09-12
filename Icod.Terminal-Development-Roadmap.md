# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.11.1`
- **Next development line:** `1.12.0` — bounded advanced persistent-raster placement geometry
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

## Published 1.8 result

The 1.5–1.8 program established normalized control families, complete CSI grammar/pixel geometry, backend-neutral raster images, Sixel, and Kitty Graphics routing.

```text
1.5.0  normalized control families / capability evidence / semantic routing
1.6.0  complete CSI grammar / terminal and cell pixel geometry
1.7.0  DCS / Sixel / public backend-neutral raster contract
1.8.0  APC / Kitty Graphics / verified multi-backend raster routing
1.8.1  documentation and sample maintenance
```

The 1.7/1.8 raster fingerprint is:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

## Published 1.9 result

Version 1.9 established unsolicited semantic-event ownership through the unified session event stream:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

It added the protocol-neutral semantic event envelope and interactive notification reporting while retaining one authoritative input reader.

Final 1.9 fingerprint:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

## Published 1.10 result

Version 1.10 added protocol-neutral semantic capability inspection and explicit bounded verification:

```text
inspect
    side-effect free
    reports what the session currently knows

verify
    explicit and bounded
    strengthens knowledge only through reviewed live probes
```

It also froze loose dependency coupling: `Icod.Terminal.csproj` remains the direct dependency authority, while restore/build is the compatibility witness for the package graph.

Final 1.10 fingerprint:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

## Published 1.11 result

Version 1.11 established a separate persistent raster ownership domain above the existing `TerminalRasterImage` and capability-planning contracts.

The stable semantic flow is:

```text
PersistentRasterGraphics verification
    -> CreateRasterResourceAsync(...)
        -> TerminalRasterResource
            -> CreatePlacementAsync(...)
                -> TerminalRasterPlacement
                    -> UpdateAsync(...)
                    -> DisposeAsync()
            -> DisposeAsync()
```

The public model remains opaque. Kitty image ids, image numbers, placement ids, raw APC command dictionaries, and backend selection stay internal.

Final 1.11 public API fingerprint:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

Detailed 1.11 authorities:

- [`Icod.Terminal-1.11.0-Development-Roadmap.md`](Icod.Terminal-1.11.0-Development-Roadmap.md)
- [`docs/releases/1.11.0.md`](docs/releases/1.11.0.md)
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)
- [`docs/Public-API-Baseline-1.11.md`](docs/Public-API-Baseline-1.11.md)

## Published 1.11.1 integration patch

Version 1.11.1 kept the 1.11 public API and persistent-raster runtime semantics unchanged while proving the intended loose-coupling integration with `Icod.TermInfo.Inspection 1.11.0`.

The accepted consumer flow is:

```text
TermInfo static inspection / classification / planning
    -> optional Terminal live verification
    -> caller-owned Verified lifecycle evidence
    -> TermInfo reclassification / replanning
    -> Terminal runtime resource / placement execution
```

Inspection remains a consumer/test/sample-only dependency. The production package graph remains `Icod.TermInfo 1.11.0` plus `Icod.Timing 1.0.0`.

Detailed 1.11.1 authorities:

- [`Icod.Terminal-1.11.1-Development-Roadmap.md`](Icod.Terminal-1.11.1-Development-Roadmap.md)
- [`docs/releases/1.11.1.md`](docs/releases/1.11.1.md)
- [`docs/T1111-E-1.11.1-Release-Closure.md`](docs/T1111-E-1.11.1-Release-Closure.md)

## Current development line: 1.12.0

Version 1.12 is intentionally bounded to advanced placement geometry that extends the existing opaque placement object without adding a placement graph or virtual-screen ownership.

Approved additions:

```text
pixel-space source rectangles
signed z-order
```

Before those public additions, T121 performs the behavior-preserving table-driven cleanup of `TerminalTermInfoSemanticEvidence` approved at 1.11.1 closure.

The 1.12 sequence is:

```text
T120  architecture/API regret gate + roadmap normalization
T121  table-driven TermInfo semantic evidence
T122  source-rectangle public contract + resource-aware validation
T123  z-order public contract
T124  acknowledged create/update encoder integration
T125  lifecycle/adversarial/boundary hardening
T126  sample/package/downstream qualification
T127  API freeze and stable release closure
```

The versioned roadmap is:

[`Icod.Terminal-1.12.0-Development-Roadmap.md`](Icod.Terminal-1.12.0-Development-Roadmap.md)

The design authority is:

[`docs/superpowers/specs/2026-09-12-1.12.0-advanced-raster-placement-design.md`](docs/superpowers/specs/2026-09-12-1.12.0-advanced-raster-placement-design.md)

### 1.12 boundaries

Source rectangles are zero-based source-image pixel rectangles and must fit completely inside the uploaded raster. The library rejects invalid rectangles before output rather than exposing backend-specific clipping behavior.

Z-order is a nullable signed 32-bit placement property. The existing current-cursor/no-cursor-movement placement semantics remain intact.

The following remain deferred beyond 1.12:

- relative placements and parent identities;
- placement chains, cycles, and depth limits;
- Unicode placeholder/virtual placements;
- animation/frame lifecycle;
- scene/window/cell/layout ownership;
- source-image caches/replay;
- caller-selected raster backends and raw Kitty dispatch.

## Parallel evidence tracks

### Graphics performance

Kitty direct transfer remains the portability/security default. Compression or alternate transport should be adopted only after benchmark evidence across representative icons, diagrams, screenshots, gradients, photographs, and high-entropy rasters.

Measure wire bytes, CPU time, allocations, first-frame latency, and total transfer latency. File, temporary-file, and shared-memory transports remain excluded unless direct-transfer measurements demonstrate a concrete problem that justifies their filesystem/IPC complexity.

### Diagnostics and observability

Future diagnostic surfaces may explain semantic operations, support-state changes, probe lifecycle, backend selection, lifecycle generations, restoration failures, and committed graphics failures.

They must not expose keyboard text, paste contents, clipboard data, notification contents, hyperlinks, shell command lines, or raster payload bytes by default, and must preserve the protocol-neutral public planning model.

## Long-range architectural guardrails

The stable 1.x program preserves these boundaries:

- no process-global current terminal;
- no second live input reader;
- no raw control-family dispatcher as the ordinary extension mechanism;
- no generic vendor-event/raw-frame stream as the ordinary semantic model;
- no support selection based solely on terminal brand, `TERM`, host OS, or environment variables;
- no tests/verifiers that duplicate exact transitive dependency pins merely to restate package metadata;
- no PTY/ConPTY process hosting in `Icod.Terminal`;
- no cells/windows/damage/layout/widget ownership that belongs in `Icod.DCurses`;
- no image-file decoding/transcoding requirement in the core terminal package;
- no hidden replay of persistent terminal state without a separately reviewed contract;
- no unbounded terminal-controlled input, event buffering, query state, graphics state, resource registry, or placement registry;
- no public persistent-graphics abstraction exposing backend-specific numeric identifiers as its common identity model.

## Sequencing rationale

```text
event ownership                    completed in 1.9
    -> capability visibility       completed in 1.10
        -> persistent ownership    completed in 1.11
            -> loose lifecycle planning integration completed in 1.11.1
                -> bounded advanced placement geometry in 1.12
```

`Icod.DCurses` remains the primary downstream witness for richer presentation needs. It should consume `Icod.Terminal` semantic resource/placement ownership rather than force the terminal layer to absorb virtual-screen or scene-graph responsibilities.

## Permanent 1.x authorities

Current contract authorities include:

- [`docs/Architecture.md`](docs/Architecture.md)
- [`docs/Terminal-Session-and-Ownership.md`](docs/Terminal-Session-and-Ownership.md)
- [`docs/Lifecycle-and-Restoration.md`](docs/Lifecycle-and-Restoration.md)
- [`docs/Input-and-Events.md`](docs/Input-and-Events.md)
- [`docs/Queries-and-Responses.md`](docs/Queries-and-Responses.md)
- [`docs/Presentation-and-Reversible-State.md`](docs/Presentation-and-Reversible-State.md)
- [`docs/Semantic-Output-Protocols.md`](docs/Semantic-Output-Protocols.md)
- [`docs/Control-Language-Normalization-and-Graphics-Roadmap.md`](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)
- [`docs/Capability-Inspection-and-Planning.md`](docs/Capability-Inspection-and-Planning.md)
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)
- [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md)
- [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md)
- [`samples/README.md`](samples/README.md)

Versioned roadmaps and tranche documents remain historical design evidence and should not be rewritten merely to make their pre-release status language look current after publication.
