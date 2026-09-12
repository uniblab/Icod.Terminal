# Icod.Terminal 1.11.0 Development Roadmap

**Release:** `1.11.0`  
**Theme:** persistent raster resources and placements  
**Status:** C118 accepted; C119 public API/documentation/compatibility/release closure active  
**Development version:** `1.11.0-alpha.1`  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.10.0`

## Why this release exists

Versions 1.7 and 1.8 established a backend-neutral ephemeral raster contract through `TerminalRasterImage` and `TerminalSession.DisplayRasterAsync(...)`. Version 1.10 added protocol-neutral capability inspection and explicit bounded verification without widening graphics ownership.

Version 1.11 introduces a separate persistent graphics ownership domain so callers can upload terminal-resident raster data, create one or more placements, update placement size/location through ordinary terminal positioning plus placement replacement, and deterministically dispose those objects.

The release deliberately does **not** turn `Icod.Terminal` into a virtual-screen or scene-graph library.

The approved architectural specification is:

[`docs/superpowers/specs/2026-09-11-1.11.0-persistent-raster-design.md`](docs/superpowers/specs/2026-09-11-1.11.0-persistent-raster-design.md)

The implementation plan is:

[`docs/superpowers/plans/2026-09-11-1.11.0-persistent-raster.md`](docs/superpowers/plans/2026-09-11-1.11.0-persistent-raster.md)

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

## Frozen C110 public direction

The public model is:

```text
TerminalCapability.PersistentRasterGraphics = 9

TerminalRasterResource : IAsyncDisposable
TerminalRasterPlacement : IAsyncDisposable
TerminalRasterPlacementOptions

TerminalSession.CreateRasterResourceAsync(...)
    -> TerminalControlResult<TerminalRasterResource>

TerminalRasterResource.CreatePlacementAsync(...)
    -> TerminalControlResult<TerminalRasterPlacement>

TerminalRasterPlacement.UpdateAsync(...)
    -> TerminalControlMutationResult
```

`TerminalRasterPlacementOptions` contains nullable `Columns` and `Rows`, each accepted from `1..16384`; null means protocol/default behavior. Position remains the terminal's current cursor location, and placement output uses no-cursor-movement semantics.

No public Kitty image id, image number, placement id, backend selector, raw APC control dictionary, or scene graph is introduced.

The internal live-registry ceilings are frozen at:

```text
256  persistent resources per session
4096 persistent placements per session
```

Persistent resource upload is acknowledged. It uses a private Kitty image number (`I`) and deliberately does not use quiet mode so the terminal can return the assigned nonzero image id (`i`). Existing ephemeral raster bytes remain unchanged.

## Tranche plan

```text
C110  architecture and public API regret gate                     complete
C111  persistent Kitty protocol foundation                       complete
C112  persistent capability integration                          complete
C113  session-owned resource/placement registries                complete
C114  persistent resource creation/upload                        complete
C115  placement creation and multi-placement ownership           complete
C116  placement update and deterministic disposal                complete
C117  lifecycle invalidation, teardown, and failure semantics    complete
C118  adversarial/downstream/package qualification               complete
C119  public API/documentation/compatibility/release closure     active
```

## Implementation checkpoint through C118

C111–C117 implemented the frozen persistent-raster protocol, capability, ownership, lifecycle, and cleanup contracts. C118 then requalified that complete surface under adversarial responses, repeated lifetime churn, package-only consumption, generated documentation, the backend-neutral sample, downstream DCurses acceptance, and all supported target frameworks.

The C118 acceptance record is:

[`docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md`](docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md)

Validated feature head:

```text
bc838f7011a2a584f958a0478fd800bf508a4832
```

Acceptance workflow:

```text
#1602 / 34695867877
```

That unchanged feature head passed Windows, Linux, macOS, package/API candidate, all four package-contract shards, and the validated package artifact. C119 is therefore the only remaining 1.11 tranche.

## C110 — architecture and public API regret gate

**Accepted.** The ownership model and public contract are frozen for implementation planning in:

[`docs/C110-Persistent-Raster-Architecture-and-API-Regret-Gate.md`](docs/C110-Persistent-Raster-Architecture-and-API-Regret-Gate.md)

C110 establishes:

- explicit distinction between `RasterGraphics` and `PersistentRasterGraphics`;
- opaque public resource/placement identity;
- no public Kitty numeric identifiers;
- no scene-graph/layout ownership;
- no hidden replay contract;
- defined parent/child lifetime rules;
- generation-scoped terminal-resident certainty;
- defined commit/cancellation/partial-failure semantics;
- exact controlled-result/exception behavior;
- `Columns` / `Rows` range `1..16384`;
- registry ceilings of 256 resources / 4096 placements;
- the additive public API shape approved before production implementation.

C110 is design-only and intentionally changes no runtime API or behavior.

## C111 — persistent Kitty protocol foundation

**Complete.** Typed internal Kitty Graphics encoding/parsing supports persistent ownership while preserving existing ephemeral raster bytes.

Required protocol operations:

```text
transmit resource without automatic display
create placement for an existing acknowledged resource
replace/update an existing placement
remove one placement
remove terminal-side resource data
```

The implementation preserves:

- direct-transfer media only;
- acknowledged transmit-only upload using `a=t` and private image number `I`;
- no quiet-mode `q` on acknowledged upload;
- bounded Base64 chunking;
- image-number upload correlation;
- acknowledgement parsing returning terminal image identity;
- private placement-id encoding;
- exact command/control-data regression vectors;
- zero/overflow/duplicate-field rejection;
- unchanged ephemeral `a=T,...,q=2` bytes;
- no public raw Kitty writer.

## C112 — persistent capability integration

**Complete.** The semantic capability is additive:

```text
PersistentRasterGraphics = 9
```

The 1.10 capability-planning contract remains intact:

- `InspectCapability(...)` is side-effect free;
- `VerifyCapabilityAsync(...)` strengthens persistent capability knowledge only through the reviewed bounded Kitty path;
- ordinary `RasterGraphics` remains independently satisfiable by Sixel or Kitty;
- verified Sixel alone never implies persistent-raster support;
- existing `TerminalCapability` numeric values `0..8` remain unchanged.

## C113 — session-owned resource and placement registries

**Complete.** Bounded internal registries and opaque ownership state provide:

- nonzero private resource/image-number identity allocation;
- nonzero private placement-id allocation;
- collision avoidance among live entries;
- explicit wraparound handling;
- maximum 256 live resources per session;
- maximum 4096 live placements per session;
- controlled `Unavailable` before output when either registry is full;
- generation stamping;
- parent/child association;
- concurrency-safe local state transitions;
- no retained arbitrary source-image cache.

## C114 — persistent resource creation/upload

**Complete.** `TerminalSession.CreateRasterResourceAsync(...)` implements the reviewed persistent Kitty subset and:

- returns `TerminalControlResult<TerminalRasterResource>`;
- validates image/capability/endpoint/cancellation before commitment;
- adapts `TerminalRasterImage` through the existing Kitty raw-raster machinery;
- uploads with `a=t`, direct transfer, and a private image number without automatic placement;
- correlates the required terminal acknowledgement;
- requires matching image number plus nonzero terminal-assigned image id;
- publishes a public resource only after acknowledgement establishes terminal-side identity;
- preserves output-gate serialization through the logical upload transaction;
- surfaces partial transport failure without automatic replay or Sixel switching;
- returns no usable resource after ambiguous failed creation;
- retains no hidden image copy after successful creation.

## C115 — placement creation and multi-placement ownership

**Complete.** Placement creation from a live resource:

- returns `TerminalControlResult<TerminalRasterPlacement>`;
- uses current-cursor positioning;
- accepts optional `Columns` / `Rows` sizing in `1..16384`;
- uses `C=1` no-cursor-movement behavior;
- supports multiple placements per resource;
- returns one opaque public placement per accepted placement;
- blocks new child creation after parent disposal;
- returns controlled `Unavailable` for stale resources before terminal output;
- throws `ObjectDisposedException` for disposed resources;
- keeps placement identifiers private.

## C116 — placement update and deterministic disposal

**Complete.** Semantic placement replacement and targeted cleanup:

- `TerminalRasterPlacement.UpdateAsync(...)` returns `TerminalControlMutationResult`;
- update reuses the same private image-id/placement-id pair;
- callers reposition through ordinary terminal cursor operations before update;
- stale placement update returns controlled `Unavailable` before output;
- disposed placement update throws `ObjectDisposedException`;
- placement disposal is locally idempotent and sends at most one targeted soft-delete while current;
- resource disposal closes child placements before freeing resource data;
- local identity ownership is released even when cleanup transport fails;
- cleanup failures are surfaced/aggregated rather than hidden;
- stale numeric identity is never reused while live ownership remains.

## C117 — lifecycle invalidation, teardown, and failure semantics

**Complete.** Persistent objects are integrated with explicit invalidation, suspend/resume, session disposal, and compound failure paths:

- persistent identities are generation-scoped;
- `InvalidateState()` makes existing persistent handles stale;
- managed lifecycle generation changes make existing handles stale;
- no automatic image replay/re-upload occurs;
- no source-image retention is introduced for hidden restoration;
- stale-handle disposal performs local cleanup without sending stale terminal identifiers;
- current-generation session teardown deletes placements before resources;
- accepted committed transactions drain before final output restoration;
- session cleanup aggregates persistent-graphics failures with existing cleanup failures;
- concurrent ownership transitions remain bounded by the existing session/query synchronization model.

## C118 — adversarial, downstream, and package qualification

**Accepted.** Full details and exact-head evidence are recorded in:

[`docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md`](docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md)

Qualification covers:

- malformed/oversized acknowledgements;
- wrong image-number/image-id/placement-id correlation;
- duplicate response fields;
- 256-resource / 4096-placement exhaustion boundaries;
- image/placement id wraparound and collision avoidance;
- terminal `ENOENT` invalidation;
- repeated create/place/update/delete cycles;
- repeated generation invalidation;
- repeated disposal;
- transport failure at meaningful commitment boundaries;
- redirected/noninteractive output;
- cancellation before commitment and committed-output cancellation semantics;
- fresh NuGet-only persistent-raster consumption;
- `net8.0`, `net9.0`, and `net10.0`;
- generated XML documentation from the produced package;
- current `Icod.DCurses` integration/ownership acceptance and hardening soak;
- a focused persistent-raster sample without terminal-brand/backend branching.

No sample exposes protocol ids or teaches Kitty-specific application logic.

## C119 — public API/documentation/compatibility/release closure

**Active.** Freeze the final 1.11 public surface and release-facing repository state.

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
