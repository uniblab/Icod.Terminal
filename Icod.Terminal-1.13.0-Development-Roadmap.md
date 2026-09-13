# Icod.Terminal 1.13.0 Development Roadmap

**Release:** `1.13.0`  
**Theme:** relative persistent-raster placement ownership  
**Status:** approved architecture; T130 starting  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** published `1.12.0`

## Release objective

Version 1.13 extends the persistent-raster ownership model with a bounded, explicit parent/child placement graph.

A relative placement belongs to its own raster resource exactly as in 1.11/1.12, but its terminal positioning and lifetime additionally depend on one immutable parent placement. This relationship is intentionally separate from resource ownership so a child resource can outlive one relative placement and continue to participate in other placements.

The release is about ownership and lifecycle, not a scene graph. It does not move cells, windows, layout, damage, virtual-screen state, or composition policy into `Icod.Terminal`.

Design authority:

[`docs/superpowers/specs/2026-09-12-1.13.0-relative-persistent-raster-placement-design.md`](docs/superpowers/specs/2026-09-12-1.13.0-relative-persistent-raster-placement-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-12-1.13.0-relative-persistent-raster-placement.md`](docs/superpowers/plans/2026-09-12-1.13.0-relative-persistent-raster-placement.md)

Permanent 1.x ownership authority remains:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Frozen architecture decisions

### Immutable parentage

Parentage is selected once during relative placement creation and never changes.

This is the central 1.13 simplification:

- no reparent API;
- no mutable parent property;
- no public operation can create a graph cycle after creation;
- relative updates may change offsets and common placement geometry only;
- ordinary updates preserve whichever positioning mode the placement was created with.

### Two independent ownership axes

The local model has two simultaneous relationships:

```text
TerminalRasterResource
    -> owns placement identity/storage membership

TerminalRasterPlacement parent
    -> owns relative-placement lifetime subtree
```

A relative placement can therefore place Resource B while being a child of a placement of Resource A.

Deleting/disposal/invalidation of the parent placement removes the relative child placement and its descendants, but does not automatically dispose Resource B.

### Bounded graph depth

The public contract uses a portable maximum relative depth of:

```text
8
```

Ordinary current-cursor placements have depth 0. A direct relative child has depth 1. Creation that would produce depth 9 is rejected before output.

The limit is deliberately independent of a terminal implementation's potentially larger private ceiling.

### Cycle policy

Because a new child can reference only an already-existing current parent and parentage is immutable, public API construction cannot form a cycle.

The registry still validates parent ancestry defensively so corrupted/internal inconsistent state fails closed rather than emitting terminal traffic.

### Offset semantics

Relative offsets are signed terminal-cell offsets:

```text
columnOffset  horizontal offset in terminal cells
rowOffset     vertical offset in terminal cells
```

The full signed `int` domain is retained unless protocol qualification proves a narrower truthful portable contract is required.

Offsets are not source pixels, terminal screen coordinates, or pixel-within-cell offsets.

### Shared common geometry

`TerminalRasterPlacementOptions` remains the only public common placement-geometry type:

```text
Columns
Rows
SourceRectangle
ZIndex
```

1.13 does not add a duplicated relative-placement options class.

## Intended public API

T130 reviews this exact shape before T132 freezes the API candidate:

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
- `CreateRelativePlacementAsync(...)` creates a child whose terminal position is relative to `parent`.
- The parent must belong to the same live `TerminalSession` and current generation.
- A child may belong to a different raster resource from its parent.
- `UpdateAsync(...)` preserves the established positioning mode. On a relative placement it preserves immutable parent and current offsets while replacing common placement geometry.
- `UpdateRelativeAsync(...)` is valid only for a relative placement and replaces signed offsets plus common placement geometry while preserving parent.
- Reparenting is impossible.

No public protocol IDs or backend selector are added.

## T130 — Architecture and API regret gate

**Goal:** convert the approved design into repository authorities and reject API shapes that would make ownership ambiguous later.

Required conclusions:

- immutable parentage remains final;
- separate create API remains final;
- common geometry reuses `TerminalRasterPlacementOptions`;
- direct signed column/row parameters are preferred over a duplicate options type;
- ordinary `UpdateAsync` preserves positioning mode rather than silently converting relative placements to cursor placement;
- `UpdateRelativeAsync` changes offsets but not parent;
- no public parent/image/placement numeric identity;
- no new capability enum;
- depth limit 8;
- cycle prevention by construction plus defensive internal validation.

Deliverables:

- this roadmap;
- updated root development roadmap;
- approved design spec;
- executable implementation plan;
- draft 1.13 PR.

## T131 — Internal immutable parent/depth graph model

**Goal:** teach the local persistent-raster registry about parent/child placement lifetime without emitting new wire fields or changing public API.

Add placement state for:

```text
Parent         nullable TerminalPersistentRasterPlacementState
RelativeDepth integer 0..8
ColumnOffset   signed int
RowOffset      signed int
```

The registry must maintain:

- resource -> direct placements, as today;
- placement -> relative children;
- placement membership in the global bounded placement set;
- parent/child relationships across different resources.

Required behavior:

- current-cursor placement reservation produces `Parent == null`, depth 0, offsets 0/0;
- relative reservation requires current parent and current child resource;
- same-session/generation is mandatory;
- relative depth is parent depth + 1;
- depth > 8 is rejected without reservation/output;
- public construction cannot form cycles;
- releasing a parent releases its entire descendant subtree locally;
- releasing a resource releases that resource's placements plus any relative descendants they own, even when descendant placements belong to other still-live resources;
- descendant resources themselves remain owned unless separately disposed/invalidated.

Tests are registry/state-only and establish RED before production changes.

## T132 — Public relative-placement API contract

**Goal:** add the minimal additive public API and freeze its semantics before protocol integration.

Add:

```text
TerminalRasterResource.CreateRelativePlacementAsync(...)
TerminalRasterPlacement.UpdateRelativeAsync(...)
```

Update `TerminalRasterPlacement.UpdateAsync(...)` documentation/behavior so a relative placement retains parent/offset positioning while replacing common geometry.

No parent getter is required for 1.13. The relationship remains semantically established but opaque after creation unless later regret-gate evidence proves public introspection necessary.

Package/XML/API-baseline tests begin here.

## T133 — Local validation and no-output rejection

**Goal:** reject graph/lifetime errors before terminal output.

Cover:

- null parent;
- disposed parent handle;
- parent from another `TerminalSession`;
- stale parent generation;
- stale child resource;
- depth 9 creation;
- ordinary placement passed to `UpdateRelativeAsync`;
- relative placement common `UpdateAsync` preserving parent/offset state;
- full signed offset boundaries;
- cancellation before commitment.

No-output assertions are mandatory for every locally rejectable case.

## T134 — Kitty relative placement encoding and acknowledged transactions

**Goal:** integrate reviewed Kitty parent/offset fields through the existing serialized acknowledged placement path.

Relative placement create/update adds deterministic private fields corresponding to:

```text
P  parent image id
Q  parent placement id
H  signed column offset
V  signed row offset
```

The encoder must keep existing 1.11/1.12 bytes unchanged for current-cursor placements.

A relative create/update must still retain:

- private child image id;
- private child placement id;
- `C=1` no-cursor-movement behavior;
- source crop/extents/z-order ordering;
- existing response correlation on the child identity.

Updates commit new local offsets only after successful acknowledgement.

## T135 — Cascading lifetime and cleanup

**Goal:** make local and terminal cleanup ordering truthful for placement subtrees.

Rules:

- explicit parent placement disposal releases descendants first locally and emits cleanup in descendant-before-parent order while identities are current;
- resource disposal removes its own placements and every relative descendant placement they own, regardless of descendant resource;
- child resources remain independently owned;
- session teardown drains placements in deepest-first order before resources;
- stale generation cleanup is local-only;
- placement disposal remains idempotent;
- repeated/cross-resource subtrees do not double-release identities.

## T136 — Terminal error and lifecycle hardening

**Goal:** classify parent-graph protocol failures without over-invalidating unrelated resources.

Review and cover:

```text
ENOPARENT
ECYCLE
ETOODEEP
ENOENT
malformed correlated response
wrong child identity
late response
transport failure
```

Expected direction:

- `ENOPARENT` invalidates certainty for the parent placement subtree, not automatically the parent raster resource;
- `ECYCLE` remains controlled failure because the local API should make it unreachable under correct state;
- `ETOODEEP` remains controlled failure because local depth 8 should make it unreachable on conforming reviewed terminals;
- `ENOENT` retains existing resource-certainty invalidation where it denotes missing image/resource identity.

Any broader invalidation requires explicit evidence and documentation.

## T137 — Adversarial graph and boundary matrix

**Goal:** prove bounded behavior under realistic graph stress.

Include:

- depth 0 through 8 chains;
- attempted depth 9;
- branching parent with many children;
- descendants spanning several resources;
- resource disposal in the middle of a graph;
- parent disposal followed by stale child disposal;
- generation invalidation of a nontrivial graph;
- placement capacity 4096 unchanged;
- resource capacity 256 unchanged;
- placement-id wrap/collision behavior unchanged;
- concurrent create/update/dispose scheduling under existing output/query serialization;
- repeated create/update/delete cycles with signed offset extrema and 1.12 crop/z-order geometry.

## T138 — Sample, package, XML, and downstream qualification

**Goal:** prove the feature through public/package consumption without teaching protocol IDs.

Update the persistent-raster sample to demonstrate:

- Resource A ordinary parent placement;
- Resource B relative child placement;
- signed cell offsets;
- relative offset update;
- deterministic parent/subtree cleanup.

Package-only smoke must compile/run the new public methods on `net8.0`, `net9.0`, and `net10.0`.

Generated XML documentation must include the new public methods.

Stable 1.x `Icod.DCurses` acceptance/hardening remains required. No DCurses source change is expected merely to consume 1.13.

## T139 — Stable release closure

**Goal:** synchronize permanent authorities and qualify the exact stable head.

Required final gates:

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

Release-facing docs must clearly state:

- parentage is immutable;
- depth is bounded to 8;
- resources and placement-parent lifetime are separate ownership axes;
- relative placement does not imply Unicode placeholders, animation, absolute layout, or scene ownership;
- 1.12 current-cursor placement remains source/wire compatible when relative APIs are unused.

## Explicit 1.13 non-goals

Version 1.13 does **not** add:

- reparenting;
- Unicode placeholder / virtual placements;
- animation or frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell offsets;
- source-raster replay/re-upload caching;
- image-file decoding/transcoding;
- public Kitty image/placement ids;
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
- no new production dependency is introduced unless separately approved by a later tranche.
