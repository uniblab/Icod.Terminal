# Icod.Terminal 1.13.0 Development Roadmap

**Release:** `1.13.0`  
**Theme:** relative persistent-raster placement ownership  
**Status:** stable release candidate; T139 exact-head qualification pending  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** published `1.12.0`

## Release objective

Version 1.13 extends persistent-raster ownership with a bounded, explicit parent/child placement-lifetime graph while keeping raster-resource ownership separate.

A relative placement belongs to its own raster resource exactly as in 1.11/1.12, but its terminal positioning and placement lifetime additionally depend on one immutable parent placement. A child placement may therefore use Resource B while being relative to a placement of Resource A, and Resource B remains independently owned if that child placement later dies with its parent.

The release is about ownership and lifecycle, not a scene graph. It does not move cells, windows, layout, damage, virtual-screen state, animation, or composition policy into `Icod.Terminal`.

Design authority:

[`docs/superpowers/specs/2026-09-12-1.13.0-relative-persistent-raster-placement-design.md`](docs/superpowers/specs/2026-09-12-1.13.0-relative-persistent-raster-placement-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-12-1.13.0-relative-persistent-raster-placement.md`](docs/superpowers/plans/2026-09-12-1.13.0-relative-persistent-raster-placement.md)

Permanent ownership authority:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

Release notes:

[`docs/releases/1.13.0.md`](docs/releases/1.13.0.md)

## Frozen architecture decisions

### Immutable parentage

Parentage is selected once during relative placement creation and never changes.

- no reparent API;
- no mutable parent property;
- no public operation can create a graph cycle after creation;
- relative updates may change offsets and common placement geometry only;
- ordinary `UpdateAsync(...)` preserves the placement's established positioning mode.

### Two independent ownership axes

```text
TerminalRasterResource
    -> owns placement resource/storage membership

TerminalRasterPlacement parent
    -> owns relative-placement lifetime subtree
```

Deleting/disposal/invalidation of a parent placement removes the relative child placement and its descendants but does not automatically dispose descendant raster resources.

### Bounded graph depth

The portable maximum relative depth is:

```text
8
```

Ordinary current-cursor placements have depth 0. A direct relative child has depth 1. Creation that would produce depth 9 is rejected locally before output.

### Cycle policy

A new child can reference only an already-existing current parent and parentage is immutable, so supported public construction cannot form a cycle. Registry ancestry is still validated defensively so inconsistent internal state fails closed rather than emitting terminal traffic.

### Offset semantics

Relative offsets are signed terminal-cell offsets:

```text
columnOffset  horizontal offset in terminal cells
rowOffset     vertical offset in terminal cells
```

The complete signed `int` domain is supported. Offsets are not source pixels, absolute terminal screen coordinates, or pixel-within-cell offsets.

### Shared common geometry

`TerminalRasterPlacementOptions` remains the single common placement-geometry type:

```text
Columns
Rows
SourceRectangle
ZIndex
```

No duplicate relative-placement options type is introduced.

## Final public API

Version 1.13 adds exactly:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlacement>>
    TerminalRasterResource.CreateRelativePlacementAsync(
        TerminalRasterPlacement parent,
        int columnOffset,
        int rowOffset,
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );

public ValueTask<TerminalControlMutationResult>
    TerminalRasterPlacement.UpdateRelativeAsync(
        int columnOffset,
        int rowOffset,
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );
```

Semantics:

- `CreatePlacementAsync(...)` continues to create a current-cursor placement.
- `CreateRelativePlacementAsync(...)` creates a child relative to `parent`.
- parent and child resource must belong to the same live `TerminalSession` generation.
- parent and child may belong to different raster resources.
- `UpdateAsync(...)` preserves ordinary-vs-relative positioning mode.
- `UpdateRelativeAsync(...)` replaces signed offsets plus common geometry while preserving immutable parentage.
- no public protocol IDs, parent identity, backend selector, or reparent operation is added.

Final public API fingerprint:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

See [`docs/Public-API-Baseline-1.13.md`](docs/Public-API-Baseline-1.13.md).

## Tranche record

### T130 — Architecture and API regret gate — complete

Frozen immutable parentage, separate create API, shared geometry, signed cell offsets, depth 8, defensive cycle handling, and no new capability enum/public protocol identity.

Primary authorities were added to the branch and PR #56 was opened as the 1.13 development line.

### T131 — Internal immutable parent/depth graph model — complete

Added internal placement parent/depth/offset state plus resource-to-placement and placement-to-relative-child membership.

Qualified:

- depth-0 ordinary state;
- current parent/resource requirements;
- cross-resource descendants;
- depth accounting/rejection;
- defensive ancestry checks;
- parent subtree release;
- resource release cascading through placement descendants without disposing descendant resources.

### T132 — Public relative-placement API contract — complete

Added the two final public methods and froze their semantics before protocol integration.

The public API candidate became the final stable fingerprint:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

### T133 — Local validation and no-output rejection — complete

Qualified null/disposed/foreign/stale parent handling, stale child resources, depth-9 rejection, ordinary-placement rejection for `UpdateRelativeAsync(...)`, full signed offset boundaries, relative `UpdateAsync(...)` positioning preservation, and cancellation before commitment.

Every locally knowable rejection is no-output.

### T134 — Kitty relative placement encoding and acknowledged transactions — complete

Integrated deterministic private parent/offset fields through the existing serialized acknowledged placement path while retaining child image/placement identity as response-correlation authority.

Existing 1.11/1.12 current-cursor placement bytes remain unchanged when relative APIs are unused. Relative offset state commits only after successful acknowledgement.

### T135 — Cascading lifetime and cleanup — complete

Parent disposal, resource disposal, and session teardown now process relative subtrees deepest-first / descendant-before-parent.

Cross-resource descendants use their own owning-resource image identities for terminal cleanup. Descendant raster resources remain independently owned. Stale cleanup remains local-only and repeated disposal remains harmless.

### T136 — Terminal error and lifecycle hardening — complete

Qualified graph-specific terminal error classification:

```text
ENOPARENT
ECYCLE
ETOODEEP
ENOENT
```

`ENOPARENT` invalidates the affected parent placement subtree without automatically declaring the parent raster resource missing. `ECYCLE`/`ETOODEEP` remain controlled failures because the supported local contract makes them unreachable under correct state. Relative `ENOENT` retains existing resource-certainty invalidation where it denotes missing resource identity.

Established malformed-response, wrong-child-identity, late-response, and transport-failure transaction invariants remain unchanged.

### T137 — Adversarial graph and boundary matrix — complete

Added/qualified:

- depth 0 through 8 and attempted depth 9;
- signed offset extrema;
- branching/cross-resource graph teardown;
- resource disposal in the middle of a graph;
- parent cascade followed by harmless stale child disposal;
- generation invalidation of nontrivial graphs;
- unchanged 256-resource / 4096-placement capacity ceilings;
- nonzero wrap/collision-safe placement identity behavior;
- concurrent registry reserve/release;
- concurrent acknowledged public relative create/update/dispose scheduling;
- repeated acknowledged create/update/delete cycles using signed offset extrema plus 1.12 crop/extents/z-order geometry.

### T138 — Sample, package, XML, and downstream qualification — complete

Updated the persistent-raster sample to demonstrate:

- Resource A ordinary parent placement;
- independently owned Resource B;
- Resource B relative child placement;
- signed cell offsets and relative offset update;
- crop/extents/z-order on relative operations;
- deterministic parent/subtree cleanup while Resource B remains independently owned.

Fresh NuGet-only package consumption compiles/runs the new methods on `net8.0`, `net9.0`, and `net10.0`; generated XML documentation contains both new methods; the stable `Icod.DCurses` acceptance/hardening package shard requires no source change.

T138 exact-head witness:

```text
86bb11e7ff31dd03d5228fe1049f39c3d6cb7d28
workflow #1702 / 34766762349
```

That workflow passed the complete PR matrix:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / public API freeze
Package Foundation
Package Presentation
Package Semantic and hardening
Package Stable 1.x release line
Validated package artifact
```

### T139 — Stable release closure — in progress

Release-facing authorities are synchronized around the frozen 1.13 contract:

- root README;
- changelog;
- package release metadata;
- permanent persistent-raster ownership authority;
- 1.13 release notes;
- final 1.13 public API baseline wording;
- root and versioned development roadmaps;
- stable package version.

The final T139 gate is one exact-head stable `1.13.0` PR workflow containing the same nine required jobs above. Merge/tag/publish remain maintainer actions after that gate is green.

## Explicit 1.13 non-goals

Version 1.13 does **not** add:

- reparenting or mutable parentage;
- Unicode placeholder / virtual placements;
- animation or frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell offsets;
- source-raster replay/re-upload caching;
- image-file decoding/transcoding;
- public Kitty image/placement/parent ids;
- caller-selected graphics backend;
- generic raw Kitty dispatch;
- Sixel persistent-resource emulation;
- PTY/ConPTY hosting;
- cells/windows/layout/damage/scene ownership.

## Compatibility target

1.13 is additive over the stable `1.0.0` floor and the published 1.12 API.

When relative-placement APIs are unused:

- existing public signatures remain unchanged;
- existing current-cursor placement behavior and wire bytes remain unchanged;
- existing source cropping and signed z-order semantics remain unchanged;
- resource/placement capacity ceilings remain 256 / 4096;
- one authoritative query/input path remains the acknowledgement authority;
- no new production dependency is introduced.
