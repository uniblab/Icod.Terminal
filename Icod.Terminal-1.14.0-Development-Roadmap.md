# Icod.Terminal 1.14.0 Development Roadmap

**Release:** `1.14.0`  
**Theme:** persistent-raster lifecycle observability  
**Status:** approved design; implementation plan complete; T140 ready  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** stable `1.13.0`

## Release objective

Version 1.14 makes the lifecycle certainty already owned internally by `Icod.Terminal` observable to consumers of persistent raster resources and placements.

The release does **not** add a passive terminal-side existence probe. Instead, it exposes one immutable, backend-neutral snapshot describing what `Icod.Terminal` currently knows about a persistent handle: whether its local ownership is current, terminal certainty has gone stale, placement lifetime was released by another owner, or the public wrapper itself has been disposed.

The governing rule is:

> Expose Terminal's current ownership certainty; do not claim to observe terminal state that the protocol cannot non-destructively prove.

Design authority:

[`docs/superpowers/specs/2026-09-13-1.14.0-persistent-raster-lifecycle-observability-design.md`](docs/superpowers/specs/2026-09-13-1.14.0-persistent-raster-lifecycle-observability-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-13-1.14.0-persistent-raster-lifecycle-observability.md`](docs/superpowers/plans/2026-09-13-1.14.0-persistent-raster-lifecycle-observability.md)

Permanent ownership authority to be updated during qualification:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Why this release follows 1.13

The persistent-raster progression is now:

```text
1.11  opaque persistent resources and placements
1.12  bounded source-pixel cropping and signed z-order
1.13  immutable-parent relative placement ownership
1.14  lifecycle observability for owned resources and placements
```

Version 1.13 made placement lifetime and raster-resource lifetime intentionally independent. A relative child placement can die with its parent while the raster resource used by that child remains independently owned and usable. Version 1.14 makes that distinction visible without exposing private protocol identities or moving layout/scene ownership into Terminal.

## Frozen architectural direction

### Local certainty, not remote existence

The observation API reports `Icod.Terminal`'s current local certainty. It does not authenticate the terminal and does not prove that a subsequent terminal operation must succeed.

The release therefore does not add `ExistsAsync()`, `VerifyExistsAsync()`, or any similar API that would imply a passive remote object-existence query the reviewed backend cannot truthfully provide.

### Four semantic states

The approved model distinguishes:

```text
Current
Stale
Released
Disposed
```

- `Current` means the handle belongs to the current local ownership model and no evidence has invalidated the identity required by that handle.
- `Stale` means terminal-resident certainty has been lost and cannot be resurrected.
- `Released` means a reachable placement wrapper represents local ownership that ended because another owner released its lifetime relationship.
- `Disposed` means the caller disposed that public wrapper.

### Semantic reasons

The approved reason model distinguishes:

```text
None
SessionStateLost
ResourceMissing
ParentPlacementLost
AncestorReleased
ResourceReleased
ExplicitDisposal
```

Reasons are backend-neutral semantics. Raw Kitty error strings, private image/placement ids, and generation numbers remain private.

### Atomic snapshot

Status and reason are observed as one immutable public snapshot rather than as independently read properties.

A caller must not observe torn combinations such as:

```text
Current + ResourceMissing
Released + None
Disposed + ParentPlacementLost
```

T140 freezes the exact public spelling. The approved candidate surface is:

```csharp
public enum TerminalRasterOwnershipStatus {
	Current,
	Stale,
	Released,
	Disposed
}

public enum TerminalRasterOwnershipLossReason {
	None,
	SessionStateLost,
	ResourceMissing,
	ParentPlacementLost,
	AncestorReleased,
	ResourceReleased,
	ExplicitDisposal
}

public readonly record struct TerminalRasterOwnershipState(
	TerminalRasterOwnershipStatus Status,
	TerminalRasterOwnershipLossReason LossReason
);
```

and one synchronous property on both opaque handle types:

```csharp
public TerminalRasterOwnershipState OwnershipState { get; }
```

### Monotonic state

High-level lifecycle transitions are monotonic:

```text
Current  -> Stale
Current  -> Released
Current  -> Disposed
Stale    -> Disposed
Released -> Disposed
```

No stale/released/disposed handle becomes current again. Late acknowledgements do not resurrect certainty.

### Side-effect-free observation

Reading lifecycle state must never:

- write terminal bytes;
- register or allocate a terminal query;
- acquire the output gate;
- verify a capability;
- mutate registry ownership;
- trigger cleanup;
- replay/re-upload raster content;
- select or expose a graphics backend.

Observation is synchronous and bounded.

## Required two-axis ownership example

The 1.13 resource/lifetime separation remains visible:

```text
Resource A     Current
  Placement A1 Current
Resource B     Current
  Placement B1 Current, relative to A1

Dispose A1
  Placement A1 Disposed / ExplicitDisposal
  Placement B1 Released / AncestorReleased
  Resource B    Current / None
```

Resource B must remain usable for a fresh ordinary placement if no independent evidence invalidated it.

## Tranche roadmap

### T140 — Architecture/API-regret gate and public snapshot freeze

**Objective:** freeze the minimum additive public surface before internal lifecycle implementation expands.

Deliverables:

- RED compile-time/public-shape tests for the exact lifecycle enums, immutable snapshot, and resource/placement properties;
- no numeric protocol/session identity in the public surface;
- public XML wording that defines `Current` as local certainty, not remote existence/authentication;
- final T140 spelling frozen in `docs/Public-API-Baseline-1.14.md`;
- GREEN public-shape tests on `net8.0`, `net9.0`, and `net10.0`.

The final `.sha256` fingerprint is generated later from the qualified built surface; T140 does not invent one manually.

### T141 — Internal lifecycle-state normalization

**Objective:** represent status and reason with one packed monotonic internal value.

Deliverables:

- one internal lifecycle state object attached to every resource and placement state;
- atomic `Observe()`;
- `TryMarkStale(...)` and placement `TryMarkReleased(...)` operations;
- only `Current` may transition to stale/released;
- wrapper disposal remains wrapper-local and overrides the underlying state with `Disposed / ExplicitDisposal`;
- direct transition-table tests proving impossible combinations and no resurrection.

### T142 — Resource ownership observability

**Objective:** make existing resource certainty loss visible without changing resource operations.

Required witnesses:

```text
new acknowledged resource          Current / None
InvalidateState/lifecycle loss      Stale / SessionStateLost
correlated missing resource         Stale / ResourceMissing
explicit wrapper disposal           Disposed / ExplicitDisposal
```

Repeated state reads must emit no bytes, register no query, and trigger no cleanup.

### T143 — Placement ownership observability

**Objective:** expose placement lifetime separately from resource lifetime.

Required witnesses include:

```text
new acknowledged placement          Current / None
session certainty loss              Stale / SessionStateLost
parent-loss evidence                Stale / ParentPlacementLost
resource-missing evidence           Stale / ResourceMissing
ancestor placement disposal         Released / AncestorReleased
owning-resource disposal            Released / ResourceReleased
explicit wrapper disposal           Disposed / ExplicitDisposal
```

The Resource A / Resource B cross-resource parent example is a release gate.

### T144 — Loss/release reason classification

**Objective:** connect the new semantic reasons to the existing narrow protocol error decisions without adding another response classifier.

Required semantics:

- `ENOPARENT` -> affected placement subtree `Stale / ParentPlacementLost` without automatically staling the parent raster resource;
- `ENOENT` denoting missing resource identity -> affected resource `Stale / ResourceMissing` and dependent placement certainty according to existing narrow rules;
- `ECYCLE`, `ETOODEEP`, malformed responses, wrong identities, timeout, and transport failure do not manufacture lifecycle loss unless the established path independently invalidates state.

### T145 — Relative-graph propagation hardening

**Objective:** prove truthful lifecycle propagation through the complete bounded 1.13 graph.

Matrix:

- depth 0 through 8 and attempted depth 9;
- branching graphs;
- cross-resource A -> B -> C chains;
- middle-parent disposal;
- middle-resource disposal;
- complete session invalidation;
- parent-loss evidence on a subtree;
- missing-resource evidence for one resource;
- unchanged 256-resource / 4096-placement ceilings;
- unchanged collision-safe private identity behavior.

Cleanup remains descendant-before-parent. Descendant raster resources remain independent unless separately invalidated/disposed.

### T146 — Concurrency and memory-model qualification

**Objective:** prove observation is safe while asynchronous ownership work changes state.

Required tests:

- concurrent read vs wrapper disposal;
- concurrent read vs session invalidation;
- concurrent read vs ancestor/resource release;
- no torn status/reason pair;
- no resurrection from late acknowledgements;
- repeated observation while the output gate is held completes without waiting for output ownership;
- no unbounded allocation or synchronization path for reads.

The intended implementation is one packed `int` using `Volatile.Read` / `Interlocked.CompareExchange`; locks are introduced only if a failing test proves that design insufficient.

### T147 — Sample and downstream consumer qualification

**Objective:** make the ownership distinction executable and prove downstream compatibility.

The persistent-raster sample will demonstrate:

1. resources/placements begin `Current`;
2. Resource B placement is relative to Resource A placement;
3. parent disposal makes the relative child `Released / AncestorReleased`;
4. Resource B remains `Current`;
5. Resource B can create a fresh ordinary placement;
6. explicitly disposing the released child wrapper makes that wrapper `Disposed / ExplicitDisposal`.

Sample source remains backend-neutral and free of private IDs.

The current stable `Icod.DCurses` package acceptance/hardening witness must pass. No DCurses source adoption is required for 1.14 success.

### T148 — Package/API/XML/documentation qualification

**Objective:** prove the additive public contract works through the packed NuGet and permanent documentation.

Required work:

- fresh package-only consumer uses the lifecycle types/properties on all three TFMs;
- generated XML contains every lifecycle type/member;
- package verifier freezes the exact API surface;
- `docs/Public-API-Baseline-1.14.sha256` is generated by the existing fingerprint process;
- `docs/Persistent-Raster-Ownership.md`, root README, sample docs, and versioned roadmap are synchronized;
- historical 1.13 and earlier release documents remain unchanged.

### T149 — Stable 1.14.0 release closure

**Objective:** produce one exact stable release candidate and qualify the complete matrix.

Required authorities:

- final package version `1.14.0`;
- `CHANGELOG.md`;
- `docs/releases/1.14.0.md`;
- root README;
- final public API baseline/fingerprint;
- permanent ownership authority;
- main and versioned roadmaps;
- stable downstream package witness.

Required exact-head jobs:

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

The roadmap must not self-certify its own untested final commit. Merge, tag, and publication remain maintainer actions after exact-head validation.

## Testing strategy

Testing proceeds from the semantic state machine outward:

1. public API RED/GREEN contract;
2. packed transition-table tests;
3. resource lifecycle tests;
4. placement lifecycle tests;
5. negative-response reason tests;
6. depth/branch/cross-resource graph tests;
7. concurrency/memory-model tests;
8. zero-output/no-query observation tests;
9. sample/package/XML/public API tests;
10. downstream Stable 1.x acceptance;
11. complete Windows/Linux/macOS release matrix.

Every public lifecycle state and reason requires a direct witness.

## Compatibility requirements

1.14 is additive over the stable `1.0.0` floor and published 1.13 surface.

Existing consumers that never read `OwnershipState` must retain existing:

- resource/placement creation behavior;
- current-cursor and relative placement wire encoding;
- acknowledgement correlation;
- crop/extents/z-order behavior;
- immutable parentage;
- depth/capacity limits;
- generation-scoped invalidation;
- deterministic cleanup;
- no hidden replay.

No new `TerminalCapability` is introduced solely for local lifecycle inspection.

No new production package dependency is introduced.

## Explicit 1.14 non-goals

Version 1.14 does **not** add:

- passive remote `ExistsAsync()` / `VerifyExistsAsync()` semantics;
- terminal-authenticated existence;
- mutating reconciliation probes presented as inspection;
- automatic replay/re-upload;
- hidden source-image caching;
- public generation numbers;
- public image/placement/parent protocol IDs;
- backend selector or raw Kitty dispatch;
- reparenting;
- Unicode placeholder / virtual placements;
- animation/frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell positioning;
- image decoding/transcoding;
- PTY/ConPTY hosting;
- cells/windows/layout/damage/scene ownership.

## Release success definition

Version 1.14 is complete when a consumer can synchronously inspect a persistent raster resource or placement and receive one atomic, backend-neutral, side-effect-free description of `Icod.Terminal`'s current ownership certainty and semantic loss/release reason; relative placement lifetime loss remains distinguishable from independent raster-resource lifetime; no state can resurrect after stale/released/disposed; and the feature introduces no private protocol identity, invented terminal-existence guarantee, replay behavior, or higher-level layout ownership.
