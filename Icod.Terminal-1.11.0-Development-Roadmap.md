# Icod.Terminal 1.11.0 Development Roadmap

**Release:** `1.11.0`  
**Theme:** persistent raster resources and placements  
**Status:** C110 design/API-regret gate active  
**Development version:** `1.10.0` until implementation planning is approved  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.10.0`

## Why this release exists

Versions 1.7 and 1.8 established a backend-neutral ephemeral raster contract through `TerminalRasterImage` and `TerminalSession.DisplayRasterAsync(...)`. Version 1.10 added protocol-neutral capability inspection and explicit bounded verification without widening graphics ownership.

Version 1.11 introduces a separate persistent graphics ownership domain so callers can upload terminal-resident raster data, create one or more placements, update placement size/location through ordinary terminal positioning plus placement replacement, and deterministically dispose those objects.

The release deliberately does **not** turn `Icod.Terminal` into a virtual-screen or scene-graph library.

The approved architectural specification is:

[`docs/superpowers/specs/2026-09-11-1.11.0-persistent-raster-design.md`](docs/superpowers/specs/2026-09-11-1.11.0-persistent-raster-design.md)

## Architectural objectives

The 1.11 contract must:

1. expose opaque session-owned resource and placement objects rather than Kitty numeric identifiers;
2. distinguish ordinary `RasterGraphics` from new `PersistentRasterGraphics` capability truth;
3. keep Sixel valid for ordinary ephemeral raster display without pretending it supports terminal-resident persistent resources;
4. reuse the existing authoritative query/input path for Kitty acknowledgements;
5. retain committed-output semantics and avoid blind replay/backend switching after partial failure;
6. invalidate terminal-resident identity across lifecycle-generation changes rather than retaining hidden image copies for automatic replay;
7. bound all resource and placement bookkeeping;
8. preserve the stable 1.x compatibility floor and existing enum numerics;
9. keep higher-level layout/cell/window/scene policy in `Icod.DCurses`.

## Intended public direction

Subject to the C110 API-regret gate, the public model is:

```text
TerminalCapability.PersistentRasterGraphics = 9

TerminalRasterResource : IAsyncDisposable
TerminalRasterPlacement : IAsyncDisposable
TerminalRasterPlacementOptions

TerminalSession.CreateRasterResourceAsync(...)
TerminalRasterResource.CreatePlacementAsync(...)
TerminalRasterPlacement.UpdateAsync(...)
```

`TerminalRasterPlacementOptions` remains narrow in 1.11 and initially controls only terminal-cell width/height (`Columns` / `Rows`). Position remains the terminal's current cursor location, and placement output itself does not move the cursor.

No public Kitty image id, image number, placement id, backend selector, raw APC control dictionary, or scene graph is introduced.

## Tranche plan

```text
C110  architecture and public API regret gate                     active
C111  persistent Kitty protocol foundation                       planned
C112  persistent capability integration                          planned
C113  session-owned resource/placement registries                planned
C114  persistent resource creation/upload                        planned
C115  placement creation and multi-placement ownership           planned
C116  placement update and deterministic disposal                planned
C117  lifecycle invalidation, teardown, and failure semantics    planned
C118  adversarial/downstream/package qualification               planned
C119  public API/documentation/compatibility/release closure     planned
```

## C110 — architecture and public API regret gate

Freeze the ownership model before production implementation.

Acceptance requires:

- approved persistent-resource/placement design;
- explicit distinction between `RasterGraphics` and `PersistentRasterGraphics`;
- opaque public identity;
- no public Kitty numeric identifiers;
- no scene-graph/layout ownership;
- no hidden replay contract;
- defined parent/child lifetime rules;
- defined lifecycle-generation invalidation;
- defined commit/cancellation/partial-failure semantics;
- provisional public API surface reviewed for long-term regret.

C110 produces documentation and tests/specification only. Production implementation begins after the written design and implementation plan are approved.

## C111 — persistent Kitty protocol foundation

Add typed internal Kitty Graphics encoding/parsing required by persistent ownership while preserving existing ephemeral raster bytes.

Required protocol operations:

```text
transmit resource without automatic display
create placement for an existing acknowledged resource
replace/update an existing placement
remove one placement
remove terminal-side resource data
```

Acceptance requires:

- direct-transfer media only;
- bounded Base64 chunking retained;
- image-number upload correlation;
- acknowledgement parsing returning terminal image identity;
- private placement-id encoding;
- exact regression vectors for command/control-data bytes;
- zero/overflow/duplicate-field rejection;
- no public raw Kitty writer.

## C112 — persistent capability integration

Add the semantic capability:

```text
PersistentRasterGraphics = 9
```

Integrate it into the 1.10 capability planning system.

Acceptance requires:

- `InspectCapability(...)` remains side-effect free;
- `VerifyCapabilityAsync(...)` may strengthen persistent capability knowledge only through the reviewed bounded Kitty path;
- ordinary `RasterGraphics` remains independently satisfiable by Sixel or Kitty;
- verified Sixel alone never implies persistent-raster support;
- existing `TerminalCapability` numeric values `0..8` remain unchanged;
- API baseline explicitly records the additive enum value.

## C113 — session-owned resource and placement registries

Introduce bounded internal registries and opaque ownership state.

Acceptance requires:

- nonzero private resource/image-number identity allocation;
- nonzero private placement-id allocation;
- collision avoidance among live entries;
- explicit wraparound handling;
- concrete maximum live-resource and live-placement counts;
- generation stamping;
- parent/child association;
- concurrency-safe local state transitions;
- no retained arbitrary source-image cache;
- no protocol output required for registry-only unit tests.

## C114 — persistent resource creation/upload

Implement `TerminalSession.CreateRasterResourceAsync(...)` over the reviewed persistent Kitty subset.

Acceptance requires:

- validate image/capability/endpoint/cancellation before commitment;
- adapt `TerminalRasterImage` through the existing Kitty raw-raster machinery;
- upload without automatic placement;
- correlate the required terminal acknowledgement;
- publish a public resource only after acknowledgement establishes terminal-side identity;
- preserve output-gate serialization through the logical upload transaction;
- surface partial transport failure without automatic replay or Sixel switching;
- return no usable resource after ambiguous failed creation;
- no hidden image retention after successful creation.

## C115 — placement creation and multi-placement ownership

Implement placement creation from a live resource.

Acceptance requires:

- current-cursor positioning;
- optional cell-width/cell-height sizing;
- no-cursor-movement placement behavior;
- multiple placements per resource;
- one opaque public `TerminalRasterPlacement` per accepted placement;
- parent disposal blocks new child creation;
- placement creation on stale/disposed resources fails before terminal output;
- placement identifiers remain private.

## C116 — placement update and deterministic disposal

Implement semantic placement replacement and targeted cleanup.

Acceptance requires:

- `TerminalRasterPlacement.UpdateAsync(...)` reuses the same private placement identity;
- callers reposition by moving the terminal cursor through ordinary terminal operations before update;
- update validates stale/disposed state before output;
- placement disposal is locally idempotent and sends at most one targeted delete while current;
- resource disposal closes child placements before resource data;
- local identity ownership is released even when terminal cleanup fails;
- cleanup failures are surfaced/aggregated rather than hidden;
- no stale numeric identity is reused while live ownership remains.

## C117 — lifecycle invalidation, teardown, and failure semantics

Integrate persistent objects with explicit invalidation, suspend/resume, session disposal, and compound failure paths.

Acceptance requires:

- persistent identities are generation-scoped;
- `InvalidateState()` makes existing persistent handles stale;
- managed lifecycle generation changes make existing handles stale;
- no automatic image replay/re-upload occurs;
- no source-image retention is introduced for hidden restoration;
- stale-handle disposal performs local cleanup without sending stale terminal identifiers;
- current-generation session teardown deletes placements before resources;
- accepted committed transactions drain before final output restoration;
- session cleanup aggregates persistent-graphics failures with existing cleanup failures;
- concurrent create/update/dispose/session-dispose races remain bounded and deterministic.

## C118 — adversarial, downstream, and package qualification

Harden the complete 1.11 surface and prove package usability.

Required coverage includes:

- malformed/oversized acknowledgements;
- wrong image-number/image-id correlation;
- duplicate response fields;
- placement/resource id exhaustion boundaries;
- repeated create/place/update/delete cycles;
- repeated generation invalidation;
- repeated disposal;
- transport failure at each meaningful commitment boundary;
- redirected/noninteractive output;
- cancellation before commitment;
- fresh NuGet-only persistent-raster consumer;
- all supported target frameworks;
- generated XML documentation;
- current `Icod.DCurses` integration/ownership acceptance;
- a focused persistent-raster sample without terminal-brand/backend branching.

No sample should expose protocol ids or teach Kitty-specific application logic.

## C119 — public API/documentation/compatibility/release closure

Freeze the final 1.11 public surface and release-facing repository state.

Acceptance requires:

- final multi-TFM public API snapshot and fingerprint;
- stable `1.11.0` package identity;
- final README/changelog/release notes;
- permanent Architecture updates;
- permanent Security/Privacy updates;
- permanent Compatibility/Versioning updates;
- persistent-raster ownership documentation;
- final sample documentation;
- no prerelease metadata remaining;
- one unchanged final PR head passing the complete Staging matrix.

Merge, `main` Release validation, tagging, GitHub Release creation, and NuGet publication remain maintainer actions.

## Explicit exclusions

Version 1.11 does not include:

- public Kitty image ids/image numbers/placement ids;
- caller-selected graphics backend;
- Sixel persistent-resource emulation;
- automatic replay after lifecycle invalidation;
- retained source-image caches for hidden restoration;
- source rectangles;
- z-order;
- Unicode placeholders;
- relative placements;
- pixel-coordinate placement;
- animation;
- scene-graph ownership;
- image-file decoding/transcoding;
- Kitty file/temp-file/shared-memory transport;
- PTY/ConPTY hosting;
- DCurses cell/window/layout/damage policy.

These remain excluded unless separately reviewed in a later release.

## Release gate

A tranche is not accepted merely because its implementation compiles. Each tranche must preserve the established repository policy:

```text
reviewed contract
    -> focused tests
        -> implementation
            -> multi-TFM/runtime/package validation
                -> exact-head acceptance evidence
```

The final release additionally requires Windows/Linux/macOS Staging validation on one unchanged final PR head.
