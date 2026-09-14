# Icod.Terminal 1.14.0 Development Roadmap

**Release:** `1.14.0`  
**Theme:** persistent-raster lifecycle observability  
**Status:** implementation and release closure complete; final exact-head qualification required  
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

Permanent ownership authority:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Why this release follows 1.13

The persistent-raster progression is:

```text
1.11  opaque persistent resources and placements
1.12  bounded source-pixel cropping and signed z-order
1.13  immutable-parent relative placement ownership
1.14  lifecycle observability for owned resources and placements
```

Version 1.13 made placement lifetime and raster-resource lifetime intentionally independent. A relative child placement can die with its parent while the raster resource used by that child remains independently owned and usable. Version 1.14 makes that distinction visible without exposing private protocol identities or moving layout/scene ownership into Terminal.

## Frozen public contract

### Local certainty, not remote existence

The observation API reports `Icod.Terminal`'s current local certainty. It does not authenticate the terminal and does not prove that a subsequent terminal operation must succeed.

The release therefore does not add `ExistsAsync()`, `VerifyExistsAsync()`, or any similar API that would imply a passive remote object-existence query the reviewed backend cannot truthfully provide.

### Public states

```csharp
public enum TerminalRasterOwnershipStatus {
	Current,
	Stale,
	Released,
	Disposed
}
```

- `Current` — the handle remains current under Terminal's local ownership model.
- `Stale` — terminal-resident certainty has been lost and cannot be resurrected.
- `Released` — a reachable placement wrapper represents local placement lifetime ended by another owner.
- `Disposed` — the caller explicitly disposed that public wrapper.

### Public reasons

```csharp
public enum TerminalRasterOwnershipLossReason {
	None,
	SessionStateLost,
	ResourceMissing,
	ParentPlacementLost,
	AncestorReleased,
	ResourceReleased,
	ExplicitDisposal
}
```

Reasons are backend-neutral semantics. Raw Kitty errors, private image/placement ids, and generation numbers remain private.

### Atomic snapshot

```csharp
public readonly record struct TerminalRasterOwnershipState(
	TerminalRasterOwnershipStatus Status,
	TerminalRasterOwnershipLossReason LossReason
);
```

Both opaque handle types expose:

```csharp
public TerminalRasterOwnershipState OwnershipState { get; }
```

Status and reason are observed atomically. Callers cannot observe torn pairs such as `Current / ResourceMissing`, `Released / None`, or `Disposed / ParentPlacementLost`.

### Monotonic lifecycle

Underlying ownership transitions are monotonic:

```text
Current -> Stale
Current -> Released
```

A stale/released underlying ownership state never becomes current again. Wrapper disposal overrides observation for that wrapper as `Disposed / ExplicitDisposal` without resurrecting underlying ownership.

### Side-effect-free observation

Reading `OwnershipState` never:

- writes terminal bytes;
- registers or allocates a terminal query;
- acquires the output gate;
- verifies a capability;
- mutates registry ownership;
- triggers cleanup;
- replays/re-uploads raster content;
- selects or exposes a graphics backend.

Observation is synchronous and bounded.

## Required two-axis ownership example

```text
Resource A      Current / None
  Placement A1 Current / None
Resource B      Current / None
  Placement B1 Current / None, relative to A1

Dispose A1
  Placement A1 Disposed / ExplicitDisposal
  Placement B1 Released / AncestorReleased
  Resource B    Current / None
```

Resource B remains usable for a fresh ordinary placement if no independent evidence invalidated it.

## Tranche completion

### T140 — Architecture/API-regret gate and public snapshot freeze

**Status: complete.**

The exact public spelling above is frozen. The RED contract commit `05022ae3f611ca24c0ce25e3a81cbf9d1a889b51` was independently re-run and failed across `net8.0`, `net9.0`, and `net10.0` because the lifecycle types and `OwnershipState` members did not yet exist. The subsequent implementation satisfied that contract without exposing protocol/session identity.

`docs/Public-API-Baseline-1.14.md` is the human-readable surface authority.

### T141 — Internal lifecycle-state normalization

**Status: complete.**

A packed internal lifecycle value uses atomic observation and compare/exchange transitions. Resource and placement states each own one lifecycle state. Only current state can transition to stale/released; late work cannot resurrect certainty. Explicit wrapper disposal remains wrapper-local.

Direct transition tests cover valid reasons, impossible state/reason pairings, first-writer monotonicity, and no resurrection.

### T142 — Resource ownership observability

**Status: complete.**

Required witnesses are implemented:

```text
new acknowledged resource          Current / None
session/lifecycle certainty loss   Stale / SessionStateLost
correlated missing resource        Stale / ResourceMissing
explicit wrapper disposal          Disposed / ExplicitDisposal
```

Repeated observation emits no terminal output and performs no cleanup/query work.

### T143 — Placement ownership observability

**Status: complete.**

Required witnesses are implemented:

```text
new acknowledged placement         Current / None
session certainty loss             Stale / SessionStateLost
parent-loss evidence               Stale / ParentPlacementLost
resource-missing evidence          Stale / ResourceMissing
ancestor placement disposal        Released / AncestorReleased
owning-resource disposal           Released / ResourceReleased
explicit wrapper disposal          Disposed / ExplicitDisposal
```

The Resource A / Resource B cross-resource example is covered both at registry level and with live public handles.

### T144 — Loss/release reason classification

**Status: complete.**

The new observation layer reuses the existing narrow response classification; it does not add a second protocol classifier.

- correlated `ENOPARENT` publishes `Stale / ParentPlacementLost` for the affected placement subtree without automatically staling its raster resources;
- correlated `ENOENT` denoting missing resource identity publishes `Stale / ResourceMissing` for the affected resource and dependent placements;
- `ECYCLE`, `ETOODEEP`, malformed responses, wrong identities, timeout, and transport failure do not manufacture lifecycle loss unless the established path independently invalidates state.

Dedicated public-handle protocol-observation tests assert these state/reason results directly.

### T145 — Relative-graph propagation hardening

**Status: complete.**

Coverage retains the complete bounded 1.13 graph behavior and adds lifecycle-state assertions for:

- depth 0 through 8 and attempted depth 9;
- branching/cross-resource ownership;
- middle-parent release;
- middle-resource release;
- complete session invalidation;
- parent-loss evidence;
- missing-resource evidence;
- unchanged 256-resource / 4096-placement ceilings;
- unchanged private identity/collision behavior.

Cleanup remains descendant-before-parent. Descendant raster resources remain independent unless separately invalidated/disposed.

### T146 — Concurrency and memory-model qualification

**Status: complete.**

Concurrency tests prove:

- concurrent observation versus lifecycle transition does not tear status/reason;
- competing stale/released transitions have exactly one winner;
- readers can see only `Current / None` or the winning terminal pair;
- generation invalidation never resurrects current state;
- observation uses atomic local reads rather than the terminal output/query path.

No lifecycle observation lock or output-gate dependency was introduced.

### T147 — Sample and downstream consumer qualification

**Status: complete.**

`Icod.Terminal.PersistentRaster.Sample` now demonstrates:

1. resources/placements begin `Current / None`;
2. Resource B placement is relative to Resource A placement;
3. parent disposal makes the parent wrapper `Disposed / ExplicitDisposal`;
4. the relative child becomes `Released / AncestorReleased`;
5. Resource B remains `Current / None`;
6. Resource B creates a fresh ordinary current placement;
7. explicit disposal of the released child wrapper produces `Disposed / ExplicitDisposal`.

The sample remains backend-neutral and contains no public/private protocol ids. Stable `Icod.DCurses` package acceptance remains part of the release-line package shard and requires no downstream source adoption.

### T148 — Package/API/XML/documentation qualification

**Status: complete.**

The fresh NuGet-only persistent-raster consumer uses the new lifecycle vocabulary on `net8.0`, `net9.0`, and `net10.0`. Generated XML verification requires all new lifecycle types/properties. The package/public-API gate generated identical framework snapshots and froze the final fingerprint:

```text
2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
```

The machine fingerprint is stored in `docs/Public-API-Baseline-1.14.sha256`. Historical 1.13 and earlier fingerprints remain unchanged.

The permanent ownership authority, root README, sample README/catalog, package metadata, and public API baseline are synchronized for 1.14.

### T149 — Stable 1.14.0 release closure

**Status: release-closure content complete; final exact-head qualification required.**

The repository version authority is `1.14.0`. Release closure includes:

- `CHANGELOG.md`;
- `docs/releases/1.14.0.md`;
- root README;
- final public API baseline/fingerprint;
- permanent ownership authority;
- main and versioned roadmaps;
- package metadata;
- sample documentation;
- stable downstream package witness.

The final release-closure commit must pass all nine PR jobs:

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

This roadmap deliberately does not self-certify the commit that contains its final release-closure status. Exact-head qualification is recorded in PR #58 after CI completes. Merge, tag, and publication remain maintainer actions.

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

Every public lifecycle state and reason has a direct witness.

## Compatibility requirements

1.14 is additive over the stable `1.0.0` floor and published 1.13 surface.

Existing consumers that never read `OwnershipState` retain existing:

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

The production dependency remains the existing `Icod.TermInfo` package, now at `1.13.0`, plus `Icod.Timing 1.0.0`; no new production package is introduced.

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
