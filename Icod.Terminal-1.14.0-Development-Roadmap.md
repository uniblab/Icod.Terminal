# Icod.Terminal 1.14.0 Development Roadmap

**Release:** `1.14.0`  
**Theme:** persistent-raster lifecycle observability  
**Status:** architecture selected; design/API review in progress; implementation not begun  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** published `1.13.0`

## Release objective

Version 1.14 makes the persistent-raster ownership state already maintained by `Icod.Terminal` visible to consumers in a truthful, bounded, side-effect-free form.

The release does **not** add a terminal-side authoritative existence query. The reviewed graphics protocol can acknowledge operations and can return negative evidence such as missing resource/parent identities during placement transactions, but it does not provide a passive read-only query that proves an arbitrary previously created image or placement still exists. `Icod.Terminal` must therefore expose only the certainty it actually owns.

The governing rule is:

> Expose Terminal's current ownership certainty; do not claim to observe terminal state that the protocol cannot non-destructively prove.

This release builds directly on the ownership progression established by the previous stable lines:

```text
1.11  own persistent resources and placements
1.12  describe crop and signed stacking geometry
1.13  relate placement positioning/lifetimes through immutable parentage
1.14  observe the certainty and lifecycle of what Terminal owns
```

Design authority:

[`docs/superpowers/specs/2026-09-13-1.14.0-persistent-raster-lifecycle-observability-design.md`](docs/superpowers/specs/2026-09-13-1.14.0-persistent-raster-lifecycle-observability-design.md)

Permanent ownership authority to be updated before stable closure:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Architectural goals

### Side-effect-free observation

Lifecycle inspection must be synchronous and must emit **zero terminal traffic**.

Reading ownership state must not:

- acquire the terminal output gate;
- allocate a query id;
- register a query waiter;
- send APC/DCS/CSI traffic;
- trigger capability verification;
- mutate registry ownership;
- re-upload or replay source pixels.

### Truthful certainty rather than remote existence claims

The public state describes `Icod.Terminal`'s current knowledge and ownership relationship.

It must not be documented as:

- proof that a terminal process still stores the pixels;
- authentication of the terminal, multiplexer, remote endpoint, host, or user;
- a guarantee that a future placement/update will succeed;
- a passive remote `Exists()` query.

A state may be `Current` because the handle remains current-generation and no evidence has invalidated it. That is library-owned certainty, not externally authenticated truth.

### One atomic observation

The state and its reason must be exposed as one immutable snapshot so callers cannot observe a new status paired with an old reason or vice versa.

The preferred public shape for the T140 API-regret gate is conceptually:

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

and one side-effect-free property on each opaque handle:

```csharp
public TerminalRasterOwnershipState OwnershipState { get; }
```

on:

```text
TerminalRasterResource
TerminalRasterPlacement
```

The names above are **candidate public spelling**, not frozen API. T140 owns the final naming and public baseline. The semantic distinctions and atomic-snapshot requirement are release requirements.

## Lifecycle model

### `Current`

`Current` means the local handle belongs to the current session generation, its local ownership has not been released, and `Icod.Terminal` has not received evidence that invalidates the terminal-resident identity required by that handle.

`Current` does not guarantee a future terminal operation will succeed.

### `Stale`

`Stale` means the public handle still exists, but `Icod.Terminal` no longer considers its terminal-resident identity current enough for mutation or new dependent ownership.

Examples include:

- explicit session-state invalidation;
- suspend/resume or another lifecycle generation transition that invalidates certainty;
- correlated missing-resource evidence;
- correlated missing-parent evidence for the affected placement subtree.

A stale handle does not become current again.

### `Released`

`Released` means the wrapper may still be reachable by application code, but the underlying local resource/placement ownership represented by that handle has ended because another owner released it.

The main 1.13 graph cases are:

- a descendant placement removed by disposal of an ancestor placement;
- a placement removed because its owning raster resource was disposed;
- a descendant placement removed by a resource-disposal cascade originating elsewhere in the relative graph.

`Released` is intentionally distinct from `Stale`: release is a local ownership/lifetime fact, while staleness represents loss of terminal certainty.

### `Disposed`

`Disposed` means the public wrapper itself has been disposed by its owner. The observation property remains safe to read after disposal so callers can inspect terminal lifecycle outcome without resurrecting the handle.

Disposal remains locally idempotent and never transitions back to another state.

## Monotonic transition rules

Public lifecycle state is monotonic.

Supported high-level transitions are:

```text
Current  -> Stale
Current  -> Released
Current  -> Disposed
Stale    -> Disposed
Released -> Disposed
```

The following are forbidden:

```text
Stale    -> Current
Released -> Current
Disposed -> Current
Disposed -> Stale
Disposed -> Released
```

T141 must audit existing cleanup/invalidation paths and define any additional internal transition needed to preserve truthful public semantics. No transition may resurrect a lost terminal identity.

## Reason model

Loss/release reasons are semantic categories, not protocol strings.

The preferred categories entering T140 are:

### `None`

Required for `Current`.

### `SessionStateLost`

Covers generation-scoped certainty loss caused by explicit invalidation or a lifecycle transition that invalidates terminal state knowledge.

The public API does not expose the numeric generation.

### `ResourceMissing`

Represents correlated evidence that invalidates the affected persistent raster resource identity and dependent placement certainty.

This must not invalidate unrelated resources.

### `ParentPlacementLost`

Represents correlated evidence such as the reviewed missing-parent condition that invalidates the affected parent placement subtree without automatically declaring the parent raster resource missing.

### `AncestorReleased`

Represents a placement whose lifetime ended because an ancestor placement was intentionally released/disposed.

### `ResourceReleased`

Represents a placement whose lifetime ended because the raster resource owning that placement was intentionally released/disposed.

### `ExplicitDisposal`

Represents disposal of the public wrapper itself.

T140 may refine spelling or collapse categories only if no meaningful consumer distinction is lost and the resulting model remains truthful for the existing 1.11–1.13 lifecycle paths.

## Resource versus placement semantics

The 1.13 two-axis ownership model remains authoritative:

```text
TerminalRasterResource
    -> owns the placement's raster-resource membership

TerminalRasterPlacement parent
    -> owns the relative child's placement-lifetime subtree
```

Observability must make that distinction visible without exposing private graph identity.

Example:

```text
Resource A
  Placement A1
    Placement B1   (uses Resource B)

Dispose Placement A1
    -> Placement B1 = Released / AncestorReleased
    -> Resource B    = Current / None
```

This is a primary 1.14 acceptance scenario.

## Concurrency and memory model

The observation API is expected to be read while asynchronous create/update/dispose/invalidation work is running.

Requirements:

- state and reason are observed from one atomic internal representation;
- a read never reports a state/reason combination that could not have existed semantically;
- observation is thread-safe and side-effect free;
- readers do not acquire the output gate or query manager;
- state transitions are monotonic;
- acknowledgement-driven placement updates do not transiently alter ownership state;
- failed updates do not corrupt lifecycle state;
- disposal remains final even if terminal cleanup later fails;
- stale/released handles cannot be revived by late acknowledgements.

A packed internal integer/enum state is preferred if it simplifies atomic publication, but the internal representation is not part of the public contract.

## Tranche plan

### T140 — Architecture and API-regret gate

Freeze the public lifecycle semantics before production implementation.

Work:

- audit every current resource/placement invalidation and release path from 1.11–1.13;
- verify which distinctions are knowable without guessing;
- freeze public snapshot type/property names;
- freeze `Current` / certainty-loss / release / disposed semantics;
- freeze reason categories;
- define post-disposal observation behavior;
- prove no public generation/protocol identity is required;
- add public API baseline candidate and design tests that initially fail because the API does not yet exist.

Exit criteria:

- no ambiguity between local certainty and terminal-authenticated truth;
- one immutable observation object rather than independent mutable state/reason reads;
- final candidate API small enough to regret-review in isolation;
- explicit no-I/O contract written before implementation.

### T141 — Internal lifecycle-state normalization

Normalize existing registry/resource/placement state transitions behind one internal semantic model.

Work:

- introduce internal packed lifecycle state/reason representation;
- route generation invalidation through the normalized transition helper;
- route graph release through the normalized transition helper;
- route resource/placement disposal through the normalized transition helper;
- retain current terminal cleanup and acknowledgement behavior unchanged;
- enforce transition monotonicity centrally.

Tests:

- valid transition matrix;
- forbidden resurrection transitions;
- idempotent repeated transition requests;
- existing cleanup tests remain unchanged in externally observable behavior.

### T142 — Resource ownership observability

Add side-effect-free lifecycle observation to `TerminalRasterResource`.

Required cases:

- acknowledged new resource -> `Current`;
- explicit `InvalidateState()` -> `Stale`;
- lifecycle generation loss -> `Stale`;
- correlated missing-resource evidence -> `Stale`;
- direct resource disposal -> `Disposed`;
- repeated disposal remains `Disposed`;
- status inspection after disposal is safe;
- every read emits zero terminal bytes and registers no query.

### T143 — Placement ownership observability

Add side-effect-free lifecycle observation to `TerminalRasterPlacement`.

Required cases:

- acknowledged ordinary placement -> `Current`;
- acknowledged relative placement -> `Current`;
- parent disposal cascade -> child `Released` while independently owned child resource remains `Current`;
- owning-resource disposal -> placement `Released`;
- generation invalidation -> `Stale`;
- correlated missing-parent evidence -> affected subtree `Stale`;
- correlated missing-resource evidence -> affected placement certainty `Stale`;
- direct wrapper disposal -> `Disposed`;
- observation after cascade and after direct disposal remains safe/no-output.

### T144 — Loss/release reason classification

Wire exact semantic reasons through all approved lifecycle paths.

Tests must prove narrow propagation:

```text
SessionStateLost
ResourceMissing
ParentPlacementLost
AncestorReleased
ResourceReleased
ExplicitDisposal
```

No reason may claim that unrelated resources, placements, or ancestors are missing without supporting evidence.

Malformed responses, wrong identities, timeouts, and transport failures must preserve existing transaction semantics rather than inventing a lifecycle reason merely because an operation failed.

### T145 — Relative-graph propagation hardening

Exercise lifecycle observability across nontrivial 1.13 graphs.

Matrix includes:

- depth 0 through 8;
- branching graphs;
- cross-resource descendants;
- disposal of a middle parent;
- disposal of a middle resource;
- independent descendant resource survival;
- generation invalidation of the complete graph;
- correlated missing-parent evidence;
- correlated missing-resource evidence;
- later harmless disposal of already released descendants.

State/reason propagation must remain bounded to the affected subtree/resource certainty.

### T146 — Concurrency and memory-model qualification

Prove the snapshot contract under concurrent reads and async lifecycle mutation.

Matrix includes concurrent observation during:

- resource creation acknowledgement;
- placement creation acknowledgement;
- placement updates;
- relative updates;
- parent cascade disposal;
- resource disposal;
- session invalidation;
- session teardown;
- late responses after timeout/cancellation ownership windows.

Acceptance:

- no torn status/reason pairs;
- no state resurrection;
- no deadlock with output/query gates;
- no observation-triggered I/O;
- no unbounded allocation or synchronization path for simple reads.

### T147 — Sample and downstream qualification

Expand the persistent-raster sample with lifecycle-state observations while keeping it backend-neutral.

The sample must demonstrate at least:

```text
Resource A Current
Placement A1 Current
Resource B Current
Placement B1 Current

Dispose A1
Placement B1 Released / AncestorReleased
Resource B Current

Create a new ordinary placement from Resource B
```

Add a focused observability sample only if the existing persistent-raster sample would become harder to understand by carrying the new demonstration.

Qualify current `Icod.DCurses` package acceptance/hardening without requiring downstream source changes. A downstream source adoption is a separate decision and is not required for 1.14 success.

### T148 — Package/API/XML/documentation qualification

Qualify the complete release-facing surface:

- fresh NuGet-only consumer on `net8.0`, `net9.0`, `net10.0`;
- public API baseline and fingerprint candidate;
- generated XML documentation for every new public type/member;
- root README;
- `samples/README.md` and local sample README;
- permanent `docs/Persistent-Raster-Ownership.md` authority;
- security/privacy wording around certainty versus authentication;
- compatibility/versioning documentation;
- package metadata/release-note links.

All package verifiers must continue to reject protocol-private identity leakage.

### T149 — Stable 1.14 release closure

Freeze and synchronize:

- stable `1.14.0` package version;
- final public API fingerprint;
- changelog;
- `docs/releases/1.14.0.md`;
- permanent ownership documentation;
- root and versioned roadmaps;
- samples;
- package release metadata;
- downstream Stable 1.x acceptance witness.

Final exact-head gate remains the standard nine-job matrix:

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

Merge, tag, and publication remain maintainer actions after the exact stable head is green.

## Explicit 1.14 non-goals

Version 1.14 does **not** add:

- a passive remote `ExistsAsync()` / `VerifyExistsAsync()` claim;
- terminal-authenticated resource or placement truth;
- terminal-mutating reconciliation probes as ordinary observation;
- automatic raster replay/re-upload;
- hidden source-image caching;
- public generation numbers;
- public Kitty image/placement/parent ids;
- caller-selected graphics backend;
- reparenting or mutable parentage;
- Unicode placeholder / virtual placements;
- animation or frame lifecycle;
- absolute screen-coordinate placement;
- pixel-within-cell positioning;
- scene/window/cell/layout/damage ownership;
- image-file decoding/transcoding;
- PTY/ConPTY process hosting.

## Compatibility target

1.14 is additive over the stable `1.0.0` floor and published 1.13 API.

Requirements:

- existing 1.13 public signatures remain unchanged;
- persistent creation/update/disposal wire behavior remains unchanged unless needed only to publish truthful local lifecycle transitions;
- current-cursor and relative placement bytes remain unchanged;
- capacity ceilings remain 256 resources / 4096 placements / depth 8;
- one authoritative input/query path remains unchanged;
- no new production package dependency is introduced merely for lifecycle observation;
- consumers that ignore the new observation surface retain existing behavior.

## Success criteria

1.14 succeeds when a consumer can inspect the lifecycle certainty of any persistent resource or placement without terminal traffic, understand why ownership certainty/lifetime was lost at the semantic level, and correctly distinguish placement-subtree release from independent raster-resource lifetime—without learning protocol-private identity or relying on invented terminal-side existence guarantees.
