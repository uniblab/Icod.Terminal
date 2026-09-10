# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current stable release:** `1.8.1`
- **Current maintenance line:** `1.8.x`
- **Next feature line:** `1.9.0` — unsolicited semantic terminal events and interactive notifications
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
- `Icod.Terminal` owns the live terminal conversation, native terminal modes, input decoding, lifecycle, query routing, capability evidence, semantic terminal output, raster output, protocol framing/routing, and reversible/scoped terminal state.
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

Versions 1.8.0 and 1.8.1 add no public API. The current public API fingerprint remains the 1.7 value:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Detailed 1.8 design and qualification evidence is preserved in:

- [`Icod.Terminal-1.8.0-Development-Roadmap.md`](Icod.Terminal-1.8.0-Development-Roadmap.md)
- [`docs/releases/1.8.0.md`](docs/releases/1.8.0.md)
- `docs/A180-*` through `docs/A189-*`

The 1.8.1 maintenance release is documented in [`docs/releases/1.8.1.md`](docs/releases/1.8.1.md).

## Approved post-1.8 release train

The next feature sequence is frozen at the roadmap level:

```text
1.9.0   unsolicited semantic event routing
        + interactive Kitty OSC 99 activation/button/close reports

1.10.0  reduced public semantic capability inspection/planning
        + explicit bounded verification where observation requires live probing

1.11.0  persistent raster resource and placement lifecycle
        + backend-neutral ownership above Kitty image/placement identifiers

1.12.0  conditional advanced raster placement/lifecycle work
        only where downstream requirements and measurements justify it
```

The themes are ordered by architectural dependency rather than novelty.

### 1.9.0 — unsolicited semantic events

The current unified event model handles ordinary decoded input, lifecycle events, timeout, and caller cancellation, while typed terminal query responses are consumed internally by the active query path.

Kitty OSC 99 exposes the missing category: unsolicited activation, button, and close reports are application-relevant terminal events but are not query responses and must not open a second raw reader.

Version 1.9 therefore extends the authoritative input/event architecture first. Kitty OSC 99 is the first implementation and acceptance case, not the permanent definition of the event model.

Detailed roadmap:

[`Icod.Terminal-1.9.0-Development-Roadmap.md`](Icod.Terminal-1.9.0-Development-Roadmap.md)

### 1.10.0 — semantic capability inspection and planning

The internal capability/evidence architecture already distinguishes semantic operation, protocol backend, support state, evidence source, endpoint availability, and routing decision.

Version 1.10 should expose a deliberately reduced semantic projection for higher-level consumers without publishing the internal backend registry, preference table, or evidence ledger.

The intended public distinction is:

```text
inspect
    side-effect free
    reports what the session currently knows

verify / prepare
    explicit and bounded
    may perform a live terminal probe
```

Callers should be able to ask whether a semantic capability such as raster graphics or modern keyboard reporting is presently usable without learning whether Terminal chose Kitty, Sixel, CSI, OSC, or a TermInfo recipe.

### 1.11.0 — persistent raster resources and placements

Version 1.7/1.8 intentionally limits raster output to ephemeral `DisplayRasterAsync(...)` semantics.

Persistent graphics require a new ownership domain covering raster resource identity, placement identity, terminal-resident lifetime, acknowledgement/correlation, move/resize/update, deletion, lifecycle uncertainty, and disposal cleanup.

The public shape should remain semantic and opaque rather than publishing Kitty's numeric image/placement identifiers as the common API. The first persistent release should remain narrow: create/upload a resource, create a placement, reposition/resize it, and delete/dispose it.

Automatic replay across suspend/resume is not assumed; a lifecycle generation change should initially invalidate terminal-resident certainty rather than require the library to retain arbitrarily large source images for hidden replay.

### 1.12.0 — conditional advanced placement

Advanced Kitty capabilities such as source rectangles, z-order, Unicode placeholders, relative placement, animation, and richer scene behavior remain candidates rather than promises.

They should enter the core only when the semantic abstraction remains useful beyond one protocol, a concrete downstream requirement exists, ownership/lifecycle semantics remain supportable, and the feature does not turn `Icod.Terminal` into a virtual-screen or scene-graph library.

## Parallel evidence tracks

### Graphics performance

Kitty direct transfer remains the portability/security default. ZLIB/deflate compression may be valuable, but it should be adopted only after benchmark evidence across representative workloads such as icons, flat diagrams, screenshots, gradients, photographs, and high-entropy rasters.

Measure at least wire bytes, CPU time, allocations, first-frame latency, and total transfer latency. File, temporary-file, and shared-memory Kitty transports remain excluded unless direct-transfer measurements demonstrate a concrete problem that justifies their filesystem/IPC complexity.

### Diagnostics and observability

As capability evidence, live probes, semantic routing, lifecycle generations, query ownership, rollback, and graphics transactions become more sophisticated, maintainers need a way to explain decisions without logging private terminal data.

A future diagnostic surface may report semantic operations, support-state changes, probe lifecycle, backend selection, lifecycle-generation changes, restoration failures, and committed graphics failures. It must not expose keyboard text, paste contents, clipboard data, notification contents, hyperlinks, shell command lines, or raster payload bytes by default.

Diagnostics may be developed alongside 1.10 capability inspection if that produces the cleanest boundary.

## Long-range architectural guardrails

The post-1.8 program preserves the stable layer boundaries:

- no process-global current terminal;
- no second live input reader;
- no raw control-family dispatcher as the normal public extension mechanism;
- no backend selection based solely on terminal brand, `TERM`, host OS, or environment variables;
- no PTY/ConPTY process hosting in `Icod.Terminal`;
- no cells/windows/damage/layout/widget ownership that belongs in `Icod.DCurses`;
- no image-file decoding/transcoding requirement in the core terminal package merely to support a vendor image protocol;
- no hidden replay of persistent terminal state without a separately reviewed ownership contract;
- no unbounded terminal-controlled input, event buffering, query state, or graphics state.

A third graphics backend is not a priority merely because another protocol exists. It must fit the semantic raw-raster contract or justify a separate optional codec/package boundary.

## Sequencing rationale

The approved order is:

```text
event ownership
    -> capability visibility
        -> persistent graphics ownership
            -> advanced placement only if justified
```

This closes a known architectural gap before exposing more public planning state or adding a new stateful graphics ownership domain.

`Icod.DCurses` does not currently force a different order: its near-term work is retained semantic metadata, layers/z-order, layout, and interaction, while raster placement remains later work. Terminal should therefore strengthen the lower-level event and capability foundation first.

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
- [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md)
- [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md)
- [`samples/README.md`](samples/README.md)

Versioned roadmaps and tranche documents remain historical design evidence and should not be rewritten merely to make their pre-release status language look current after publication.
