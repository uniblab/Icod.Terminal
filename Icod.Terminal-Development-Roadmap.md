# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.11.0`
- **Next development line:** `1.12.0` — conditional advanced raster placement/lifecycle work
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

Version 1.11 adds a separate persistent raster ownership domain above the existing `TerminalRasterImage` and capability-planning contracts.

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

Stable 1.11 guarantees include:

- `TerminalCapability.PersistentRasterGraphics = 9` with values `0..8` unchanged;
- explicit separation between ephemeral `RasterGraphics` and persistent ownership;
- acknowledged resource creation before a public handle is returned;
- correlated placement create/update responses through the existing one-reader/query path;
- current-cursor placement with optional `Columns` / `Rows` in `1..16384` and no text-cursor movement;
- 256 live resources and 4096 live placements per session;
- nonzero private collision-safe identities with wraparound handling;
- generation-scoped terminal-resident certainty;
- no hidden source-image retention or automatic replay after invalidation/resume;
- `ENOENT` invalidation when the terminal no longer recognizes a believed-current resource;
- child-first cleanup, locally idempotent disposal, and stale local-only cleanup;
- direct transfer only; no file/temp-file/shared-memory transport;
- no scene-graph, cell/window/layout, z-order, animation, source-rectangle, or PTY ownership.

Final 1.11 public API fingerprint:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

Detailed 1.11 authorities:

- [`Icod.Terminal-1.11.0-Development-Roadmap.md`](Icod.Terminal-1.11.0-Development-Roadmap.md)
- [`docs/releases/1.11.0.md`](docs/releases/1.11.0.md)
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)
- [`docs/Public-API-Baseline-1.11.md`](docs/Public-API-Baseline-1.11.md)
- [`docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md`](docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md)
- [`docs/C119-1.11.0-Release-Closure.md`](docs/C119-1.11.0-Release-Closure.md)

## Next development line: 1.12.0

Advanced raster placement/lifecycle features remain candidates rather than promises.

Potential areas include:

- source rectangles;
- z-order;
- Unicode placeholders;
- relative placement;
- richer placement geometry;
- animation or frame lifecycle;
- additional terminal-resident resource operations.

They should enter the core only when:

1. a concrete downstream requirement exists;
2. ownership/lifecycle semantics can be stated precisely;
3. bounded-resource behavior can be specified and tested;
4. the semantic abstraction is useful beyond raw vendor command exposure, or is deliberately isolated as optional protocol-specific functionality;
5. the feature does not turn `Icod.Terminal` into a virtual-screen or scene-graph library.

The default position after 1.11 is therefore **measure and justify**, not automatically expand.

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
            -> advanced placement only if justified in 1.12+
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
