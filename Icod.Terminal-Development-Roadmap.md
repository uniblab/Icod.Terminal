# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Published stable release:** `1.8.0`  
**Current maintenance line:** `1.8.1`  
**Current status:** documentation/sample polish in draft PR #47  
**Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and release planning. Detailed historical design evidence remains in the versioned roadmaps and tranche documents rather than being duplicated here.

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

The 1.5–1.8 program is complete:

```text
1.5.0  normalized control families / capability evidence / semantic routing
1.6.0  complete CSI grammar / terminal and cell pixel geometry
1.7.0  DCS / Sixel / public backend-neutral raster contract
1.8.0  APC / Kitty Graphics / verified multi-backend raster routing
```

The public raster intent introduced in 1.7 resolves through two reviewed internal backends:

```text
TerminalSession.DisplayRasterAsync(...)
    -> capability/evidence resolution
        -> verified Kitty Graphics / APC
        -> verified Sixel / DCS
```

Version 1.8 added no public API. The current public API fingerprint remains the 1.7 value:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Detailed 1.8 design and qualification evidence is preserved in:

- [`Icod.Terminal-1.8.0-Development-Roadmap.md`](Icod.Terminal-1.8.0-Development-Roadmap.md)
- [`docs/releases/1.8.0.md`](docs/releases/1.8.0.md)
- `docs/A180-*` through `docs/A189-*`

## 1.8.1 maintenance program

Version `1.8.1` is intentionally a maintenance release. It does not introduce a new terminal protocol, public API, or runtime semantic contract.

The work in PR #47 is limited to:

- correcting stale post-release wording left behind after the 1.8.0 merge/publication sequence;
- making the root README and current roadmap concise, release-aware entry points rather than qualification diaries;
- improving `/samples` discoverability by grouping examples around consumer goals;
- adding a backend-neutral raster sample so the 1.7/1.8 headline graphics API has a first-class executable example;
- adding a focused VS Code OSC 633 sample to complement the existing portable OSC 133 and iTerm2 OSC 1337 examples;
- building the new focused samples on `net8.0`, `net9.0`, and `net10.0` as part of repository validation;
- synchronizing maintenance release notes and package metadata.

### 1.8.1 invariants

1. No public API change.
2. No terminal wire-protocol behavior change in the library runtime.
3. No change to the one-authoritative-reader contract.
4. No change to capability evidence or backend routing semantics.
5. No change to raster bounds, ownership, alpha, cancellation, or committed-output semantics.
6. No new package dependency.
7. Existing `Icod.DCurses` compatibility witnesses remain authoritative.
8. New samples use public APIs only and must build on every supported TFM.
9. The stable `1.0.0` compatibility floor remains unchanged.

## Candidate next development tracks

The release following 1.8.1 is intentionally not frozen by this maintenance work. Current candidates are:

- unsolicited semantic terminal events, beginning with the already-deferred Kitty OSC 99 activation/close/button reports;
- persistent/placed Kitty Graphics, including typed image/placement identity and lifecycle;
- a reduced public capability-inspection/planning surface for higher-level consumers;
- graphics transport/performance work only where measurement justifies the added complexity.

The preferred sequencing remains to address the authoritative input/event-model gap before expanding graphics persistence, unless a concrete downstream `Icod.DCurses` requirement changes that priority.

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
