# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.10.0`
- **Next development line:** `1.11.0` — persistent raster resources and placements
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
- `Icod.Terminal` owns the live terminal conversation, native terminal modes, input decoding, unsolicited semantic events, lifecycle, query routing, semantic capability evidence/planning, terminal output, raster output, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, refresh/diff policy, and higher-level curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## Published 1.8 result

The 1.5–1.8 feature program is complete:

```text
1.5.0  normalized control families / capability evidence / semantic routing
1.6.0  complete CSI grammar / terminal and cell pixel geometry
1.7.0  DCS / Sixel / public backend-neutral raster contract
1.8.0  APC / Kitty Graphics / verified multi-backend raster routing
1.8.1  documentation and sample maintenance; no runtime/API expansion
```

The public raster intent introduced in 1.7 resolves through two reviewed internal backends:

```text
TerminalSession.DisplayRasterAsync(...)
    -> capability/evidence resolution
        -> verified Kitty Graphics / APC
        -> verified Sixel / DCS
```

Versions 1.8.0 and 1.8.1 add no public API and retain the 1.7 fingerprint:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Detailed 1.8 design and qualification evidence is preserved in:

- [`Icod.Terminal-1.8.0-Development-Roadmap.md`](Icod.Terminal-1.8.0-Development-Roadmap.md)
- [`docs/releases/1.8.0.md`](docs/releases/1.8.0.md)
- `docs/A180-*` through `docs/A189-*`

The 1.8.1 maintenance release is documented in [`docs/releases/1.8.1.md`](docs/releases/1.8.1.md).

## Published 1.9 result

Version 1.9 establishes this authoritative input-routing order:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

The public event stream gains `TerminalEventKind.Semantic` and protocol-neutral `TerminalSemanticEvent` / `TerminalNotificationEvent` payloads. The first semantic family is interactive notification reporting with distinct activation, one-based button activation, close, and close-tracking-unavailable observations.

The existing typed Kitty OSC 99 notification options gain explicit opt-in activation/button reporting, close reporting, and bounded button labels. Existing noninteractive notification behavior remains compatible when those options are unused.

The final 1.9 public API fingerprint is:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

Detailed 1.9 authorities:

- [`Icod.Terminal-1.9.0-Development-Roadmap.md`](Icod.Terminal-1.9.0-Development-Roadmap.md)
- [`docs/releases/1.9.0.md`](docs/releases/1.9.0.md)
- [`docs/Public-API-Baseline-1.9.md`](docs/Public-API-Baseline-1.9.md)
- `docs/E190-*` through `docs/E199-*`

## Published 1.10 result

Version 1.10 adds a deliberately reduced public semantic capability-inspection/planning surface over the evidence/routing architecture established in 1.5–1.9.

The public distinction is:

```text
inspect
    side-effect free
    reports what the session currently knows

verify
    explicit and bounded
    attempts to strengthen current knowledge only where a reviewed live probe exists
```

The stable public types are:

```text
TerminalCapability
TerminalCapabilitySupport
TerminalCapabilityEndpointAvailability
TerminalCapabilityEvidenceKind
TerminalCapabilityStatus
```

The stable public operations are:

```text
TerminalSession.InspectCapability(...)
TerminalSession.VerifyCapabilityAsync(...)
```

Public evidence remains protocol- and dependency-neutral. Callers see `StaticDescription` or `LiveObservation`, not `Icod.TermInfo`, protocol family, backend identity, routing score, or terminal brand.

Version 1.10 also freezes the loose dependency-coupling policy: `Icod.Terminal.csproj` remains the package authority for direct dependency requirements while tests, samples, package smoke consumers, and verification tools avoid independently pinning transitive runtime dependency versions merely to duplicate package metadata. Successful restore/build remains the compatibility witness for the package graph.

The final 1.10 public API fingerprint is:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

Detailed 1.10 authorities:

- [`Icod.Terminal-1.10.0-Development-Roadmap.md`](Icod.Terminal-1.10.0-Development-Roadmap.md)
- [`docs/releases/1.10.0.md`](docs/releases/1.10.0.md)
- [`docs/Capability-Inspection-and-Planning.md`](docs/Capability-Inspection-and-Planning.md)
- [`docs/Public-API-Baseline-1.10.md`](docs/Public-API-Baseline-1.10.md)
- [`docs/C105-C108-Capability-Lifecycle-Samples-Hardening-and-API-Freeze.md`](docs/C105-C108-Capability-Lifecycle-Samples-Hardening-and-API-Freeze.md)

## Approved post-1.10 release train

The feature sequence remains frozen at the roadmap level:

```text
1.11.0  persistent raster resource and placement lifecycle
        + backend-neutral ownership above Kitty image/placement identifiers

1.12.0  conditional advanced raster placement/lifecycle work
        only where downstream requirements and measurements justify it
```

The themes remain ordered by architectural dependency rather than novelty.

## 1.11.0 — persistent raster resources and placements

Version 1.7/1.8 intentionally limits raster output to ephemeral `DisplayRasterAsync(...)` semantics. Version 1.10 deliberately does not widen that contract while introducing capability planning.

Persistent graphics require a new ownership domain covering:

- opaque raster resource identity;
- opaque placement identity;
- terminal-resident lifetime and uncertainty;
- acknowledgement/correlation where available;
- create/upload and placement creation;
- move/resize/update behavior;
- explicit deletion/disposal;
- lifecycle generation invalidation;
- teardown/cleanup authority;
- capability/evidence integration through the 1.10 planning model.

The public shape should remain semantic and opaque rather than publishing Kitty numeric image/placement identifiers as the common API.

The first persistent release should remain narrow:

```text
create/upload a resource
create a placement
reposition/resize a placement
delete/dispose placement/resource
```

Automatic replay across suspend/resume is not assumed. A lifecycle generation change should initially invalidate terminal-resident certainty rather than require the library to retain arbitrarily large source images for hidden replay.

The 1.11 design must also preserve the established committed-output rule: after partial terminal output, the library must not blindly replay or switch backends when terminal state may be uncertain.

## 1.12.0 — conditional advanced placement

Advanced Kitty capabilities such as source rectangles, z-order, Unicode placeholders, relative placement, animation, and richer scene behavior remain candidates rather than promises.

They should enter the core only when:

- the semantic abstraction remains useful beyond one protocol or is deliberately isolated as optional protocol-specific functionality;
- a concrete downstream requirement exists;
- ownership/lifecycle semantics remain supportable;
- bounded-resource behavior can be specified and tested;
- the feature does not turn `Icod.Terminal` into a virtual-screen or scene-graph library.

## Parallel evidence tracks

### Graphics performance

Kitty direct transfer remains the portability/security default. ZLIB/deflate compression may be valuable, but it should be adopted only after benchmark evidence across representative workloads such as icons, flat diagrams, screenshots, gradients, photographs, and high-entropy rasters.

Measure at least wire bytes, CPU time, allocations, first-frame latency, and total transfer latency. File, temporary-file, and shared-memory Kitty transports remain excluded unless direct-transfer measurements demonstrate a concrete problem that justifies their filesystem/IPC complexity.

### Diagnostics and observability

As capability evidence, live probes, semantic routing, lifecycle generations, query ownership, rollback, and graphics transactions become more sophisticated, maintainers need a way to explain decisions without logging private terminal data.

A future diagnostic surface may report semantic operations, support-state changes, probe lifecycle, backend selection, lifecycle-generation changes, restoration failures, and committed graphics failures. It must not expose keyboard text, paste contents, clipboard data, notification contents, hyperlinks, shell command lines, or raster payload bytes by default.

Any such surface must preserve the 1.10 capability-planning abstraction rather than leaking internal evidence/protocol identifiers into ordinary consumer APIs.

## Long-range architectural guardrails

The post-1.10 program preserves the stable layer boundaries:

- no process-global current terminal;
- no second live input reader;
- no raw control-family dispatcher as the normal public extension mechanism;
- no generic vendor-event/raw-frame stream as the ordinary semantic extension mechanism;
- no backend selection based solely on terminal brand, `TERM`, host OS, or environment variables;
- no test/verifier dependency policy that independently pins a specific `Icod.TermInfo` release when ordinary restore/build already proves compatibility;
- no PTY/ConPTY process hosting in `Icod.Terminal`;
- no cells/windows/damage/layout/widget ownership that belongs in `Icod.DCurses`;
- no image-file decoding/transcoding requirement in the core terminal package merely to support a vendor image protocol;
- no hidden replay of persistent terminal state without a separately reviewed ownership contract;
- no unbounded terminal-controlled input, event buffering, query state, graphics state, resource registry, or placement registry;
- no public persistent-graphics abstraction that exposes backend-specific numeric identifiers as its common identity model.

A third graphics backend is not a priority merely because another protocol exists. It must fit an established semantic contract or justify a separate optional boundary.

## Sequencing rationale

The approved order is:

```text
event ownership                  completed in 1.9
    -> capability visibility     completed in 1.10
        -> persistent graphics ownership   next in 1.11
            -> advanced placement only if justified
```

Version 1.9 closed the known event-ownership gap. Version 1.10 made capability knowledge safely visible without exposing protocol internals. Version 1.11 can therefore build persistent graphics ownership on a stable one-reader, lifecycle, evidence, and public planning foundation.

`Icod.DCurses` remains an important downstream witness, but it should consume persistent raster semantics rather than force `Icod.Terminal` to absorb virtual-screen or scene-graph responsibilities.

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
- [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md)
- [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md)
- [`samples/README.md`](samples/README.md)

Versioned roadmaps and tranche documents remain historical design evidence and should not be rewritten merely to make their pre-release status language look current after publication. The 1.10 versioned roadmap is explicitly closed by C109 because that closure itself is part of the release record.
