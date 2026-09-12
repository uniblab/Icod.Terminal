# Icod.Terminal 1.11.0 Development Roadmap

**Release:** `1.11.0`  
**Theme:** persistent raster resources and placements  
**Status:** complete; C119 accepted and ready for maintainer review  
**Development version:** `1.11.0`  
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

## Frozen public direction

The final public model is:

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

The internal live-registry ceilings are:

```text
256  persistent resources per session
4096 persistent placements per session
```

Persistent resource upload is acknowledged. It uses a private Kitty image number (`I`) so the terminal can return the assigned nonzero image id (`i`). Existing ephemeral raster bytes remain unchanged.

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
C119  public API/documentation/compatibility/release closure     complete
```

## C118 feature acceptance

C111–C117 implemented the frozen persistent-raster protocol, capability, ownership, lifecycle, and cleanup contracts. C118 requalified that complete surface under adversarial responses, repeated lifetime churn, package-only consumption, generated documentation, the backend-neutral sample, downstream DCurses acceptance, and all supported target frameworks.

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

That unchanged feature head passed Windows, Linux, macOS, package/API candidate, all four package-contract shards, and the validated package artifact.

## C110 — architecture and public API regret gate

**Accepted.** C110 froze the ownership model, public API, capability separation, generation-scoped certainty, cancellation/commit semantics, resource bounds, and exclusions before production implementation.

The record is:

[`docs/C110-Persistent-Raster-Architecture-and-API-Regret-Gate.md`](docs/C110-Persistent-Raster-Architecture-and-API-Regret-Gate.md)

## C111 — persistent Kitty protocol foundation

**Complete.** Typed internal Kitty Graphics encoding/parsing supports transmit-only acknowledged resource upload, placement create/update, targeted placement deletion, resource-data deletion, direct transfer, bounded Base64 chunking, private ids, duplicate/overflow rejection, and unchanged ephemeral raster bytes.

## C112 — persistent capability integration

**Complete.** `PersistentRasterGraphics = 9` is additive. `InspectCapability(...)` remains side-effect free; `VerifyCapabilityAsync(...)` uses only the reviewed bounded Kitty support path; Sixel remains valid for ordinary raster without implying persistent ownership.

## C113 — session-owned resource and placement registries

**Complete.** Bounded internal registries provide nonzero collision-safe resource/image-number and placement identities, explicit wraparound, 256/4096 capacity ceilings, generation stamping, parent/child association, concurrency-safe transitions, and no arbitrary source-image cache.

## C114 — persistent resource creation/upload

**Complete.** `CreateRasterResourceAsync(...)` validates capability/state/cancellation, uploads direct raw raster data with a private image number, correlates the terminal acknowledgement, publishes a handle only after nonzero terminal identity is established, preserves committed-output semantics, and does not replay or switch backend after partial failure.

## C115 — placement creation and multi-placement ownership

**Complete.** Resource placement creation uses current-cursor positioning, `Columns`/`Rows` in `1..16384`, no-cursor-movement semantics, multiple placements per resource, opaque handles, bounded reservation, and stale/disposed behavior without public protocol ids.

## C116 — placement update and deterministic disposal

**Complete.** `TerminalRasterPlacement.UpdateAsync(...)` replaces the same private `(image, placement)` identity at the current cursor. Placement/resource disposal is locally idempotent, releases ownership exactly once, cleans children before resource data, and surfaces cleanup transport failures without restoring uncertain local ownership.

The public API fingerprint frozen here and retained through release closure is:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

## C117 — lifecycle invalidation, teardown, and failure semantics

**Complete.** Persistent identities are generation-scoped. `InvalidateState()` and managed lifecycle changes stale existing handles; no automatic replay/source-image retention occurs; stale disposal is local-only; current session teardown deletes placements before resources and integrates failures with existing cleanup/restoration.

## C118 — adversarial, downstream, and package qualification

**Accepted.** Qualification covers malformed/oversized acknowledgements, wrong ids, duplicate fields, capacity/wraparound boundaries, `ENOENT`, repeated ownership cycles, generation invalidation, registry churn, transport/cancellation/redirection boundaries, fresh NuGet-only package consumption, all TFMs, generated XML docs, DCurses acceptance/soak, and the backend-neutral persistent-raster sample.

See the C118 acceptance record linked above.

## C119 — public API/documentation/compatibility/release closure

**Accepted.**

The release-facing state includes:

- final multi-TFM public API fingerprint `9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2`;
- stable `1.11.0` package identity with no prerelease suffix;
- final package release notes;
- final README and changelog entry;
- curated `docs/releases/1.11.0.md`;
- permanent `docs/Persistent-Raster-Ownership.md`;
- 1.11 Architecture/Security/Compatibility authority updates;
- final sample catalog and backend-neutral sample verification;
- long-range roadmap handoff to conditional 1.12 advanced placement/lifecycle work;
- release-closure record at [`docs/C119-1.11.0-Release-Closure.md`](docs/C119-1.11.0-Release-Closure.md).

C119 code/release acceptance is frozen on exact head:

```text
66002280a3e5c800b9b8d230de483f63945d571d
```

with pull-request workflow:

```text
#1611 / 34698727655
```

Workflow #1611 completed successfully on that unchanged SHA. Windows, Linux, macOS, package candidate/API freeze, Package Foundation, Package Presentation, Package Semantic and hardening, Package Stable 1.x release line, and the validated package artifact all passed. The successful macOS rerun used the same SHA and introduced no source change.

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

The established repository policy remains:

```text
reviewed contract
    -> focused tests
        -> implementation
            -> multi-TFM/runtime/package validation
                -> exact-head acceptance evidence
```

The final 1.11 code/release head satisfied the complete Windows/Linux/macOS Staging matrix before maintainer handoff. This documentation-only acceptance commit is the final repository-state qualification step before PR #52 leaves draft.
