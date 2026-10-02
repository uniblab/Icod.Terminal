# Icod.Terminal 1.24.0 Development Roadmap

**Goal:** Publish the pixel geometry, persistent-resource geometry, bounded local planning, and operation-evidence contracts required by a later `Icod.DCurses` tile renderer, then qualify them with an executable tile-atlas workload.

**Status:** Approved implementation in progress in draft [PR #69](https://github.com/uniblab/Icod.Terminal/pull/69).

**Release theme:** Raster Geometry and Planning Contracts.

**Design authority:** [`docs/superpowers/specs/2026-10-02-1.24.0-raster-geometry-planning-design.md`](docs/superpowers/specs/2026-10-02-1.24.0-raster-geometry-planning-design.md)

**Implementation authority:** [`docs/superpowers/plans/2026-10-02-1.24.0-raster-geometry-planning.md`](docs/superpowers/plans/2026-10-02-1.24.0-raster-geometry-planning.md)

**Compatibility:** Preserve the stable 1.0.0 floor, existing public signatures and enum values, one authoritative input/query path, opaque raster identities, no hidden replay, and the .NET 8/9/10 target matrix. Production dependencies remain `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`; optional integration remains outside the production graph.

## Release decision

Version 1.24 is the only committed release. It supplies information and evidence needed to plan an Ultima IV-style tile presentation without precommitting a new mutation mechanism.

A possible 1.25 release is measurement-gated. It may add bounded multi-region frame-edit execution and Indexed8 regional-transfer parity only when a real DCurses workload shows that individual acknowledged edits plus two-frame presentation are inadequate. If that evidence does not exist, 1.25 remains available for a different feature.

## Governing rules

- Terminal owns live terminal/cell pixel queries, exact integer derivation, intrinsic resource geometry, bounded local ownership accounting, operation evidence, protocol execution, acknowledgement, lifecycle, and cleanup.
- DCurses owns raster-cell retention, tile-to-cell mapping, viewport coordinates, clipping, damage, overlays, refresh policy, and front/back frame-selection policy.
- The game or independent rules engine owns maps, terrain, actors, collision, visibility, animation policy, scheduling, persistence, and rules.
- Geometry and capacity observations are semantic and backend-neutral. They expose no CSI numbers, Kitty ids, backend registries, or evidence ledger.
- A local planning snapshot describes library ceilings and current local bookkeeping only. It never claims authenticated terminal memory or guarantees a later allocation.
- Operation evidence is live-generation-scoped. A successful acknowledgement may verify an operation; timeout, transport loss, missing resource, or generic rejection does not invent unsupported evidence.
- The tile-atlas witness demonstrates Terminal mechanics only. It contains no tile map, camera, scene graph, asset decoder, or game loop.

## Planned public contract

The API-regret gate will freeze the following additive surface before production implementation:

- `TerminalPixelDimensions`: positive pixel `Width` and `Height`.
- `TerminalPixelGeometry.TryDeriveCellDimensions(...)`: exact integer derivation from public character dimensions and terminal pixels; fractional, zero, and inconsistent values return `false`.
- `TerminalSession.QueryTerminalPixelDimensionsAsync(...)` and `QueryCellPixelDimensionsAsync(...)`: public forms of the existing bounded semantic queries, retaining timeout, cancellation, malformed-response, and endpoint failure behavior.
- `TerminalRasterResource.PixelWidth` and `PixelHeight`: immutable intrinsic source geometry with no I/O.
- `TerminalRasterPlanningSnapshot` and `TerminalSession.GetRasterPlanningSnapshot()`: public ceilings plus advisory current local resource, placement, and allocated-frame counts.
- `TerminalRasterOperation`, `TerminalRasterOperationStatus`, and `TerminalSession.InspectRasterOperation(...)`: side-effect-free current-generation status for frame composition, RGB24 region replacement, and RGBA32 region replacement.

No verification method is planned for the raster-operation surface. The actual acknowledged operation is the only truthful active verification path.

## Work sequence

### T2400 — Published baseline, consumer contract, and API-regret gate

- [x] Record the merged/tagged 1.23.0 source identity, public API fingerprint, package and symbol hashes, dependency graph, bounds, and qualification evidence.
- [x] Record the representative consumer: uniform atlas, opaque placeholder cells, caller-owned damage, two known frames, and explicit text fallback.
- [x] Freeze exact public names, values, XML semantics, failure behavior, evidence transitions, counts, and compatibility constraints.
- [x] Establish a `1.24.0-alpha` development identity without changing the 1.0.0 compatibility floor.

**Acceptance:** The public additions are fully specified and no game, DCurses, protocol identifier, or speculative batch abstraction crosses into Terminal.

### T2401 — Pixel-geometry RED fixtures

- [ ] Add compile-time/public-surface failures for the new pixel-dimension type, derivation helper, and two query methods.
- [ ] Add behavior failures for positive dimensions, zero/negative rejection, exact derivation, indivisible dimensions, timeout, cancellation, malformed replies, and mismatched CSI replies.
- [ ] Preserve bounded late-response ownership and the single authoritative input/query coordinator.

**Acceptance:** Tests fail against 1.23 because the public contract is absent while existing internal query behavior remains green.

### T2402 — Semantic pixel geometry

- [ ] Replace the internal `TerminalPixelSize` with the frozen public immutable value type without changing wire behavior.
- [ ] Publish terminal-pixel and cell-pixel query methods through the existing transaction manager.
- [ ] Publish exact derivation without caching, guessing, rounding, or treating timeout as unsupported.
- [ ] Document an explicit consumer fallback: query cell pixels; on timeout, query terminal pixels and combine them with `GetDimensions()` only when exact derivation succeeds.

**Acceptance:** Consumers can obtain or exactly derive cell pixel geometry without raw control sequences or brand detection.

### T2403 — Persistent-resource geometry

- [ ] Add immutable `PixelWidth` and `PixelHeight` properties backed by the resource's existing source dimensions.
- [ ] Verify values across RGB24, RGBA32, and Indexed8 resource creation.
- [ ] Verify that property access performs no I/O, exposes no protocol identity, and retains its intrinsic meaning after lifecycle loss or disposal.

**Acceptance:** A consumer can partition a known atlas using the resource handle as the authoritative geometry source.

### T2404 — Raster planning snapshot

- [ ] Publish all stable library ceilings needed for atlas planning: image dimension, pixel count, owned bytes, palette entries, resources, combined placements, relative depth, placeholder extent, and allocated animation frames.
- [ ] Add advisory local counts for currently owned resources, combined placements, and allocated animation frames, including in-flight frame reservations where they consume admission capacity.
- [ ] Capture each registry's counts under its existing synchronization and document that the combined snapshot is observational rather than an atomic reservation.
- [ ] Keep final create/append results authoritative when concurrent work changes capacity after observation.

**Acceptance:** DCurses can reject an impossible atlas plan before output while never mistaking local bookkeeping for terminal storage.

### T2405 — Generation-scoped raster-operation evidence

- [ ] Add focused raster-operation status rather than expanding the broad `TerminalCapability` enum with format variants.
- [ ] Record semantic-operation evidence separately from the shared Kitty animation backend so one successful operation does not verify unrelated operations.
- [ ] Verify composition after successful `ComposeFrameAsync` acknowledgement and verify the matching RGB24 or RGBA32 variant after successful `UpdateFrameRegionAsync` acknowledgement.
- [ ] Clear live evidence on lifecycle-generation advance; keep initial state unknown and endpoints separately available/unavailable.
- [ ] Leave generic rejection, timeout, cancellation, transport loss, and missing-resource invalidation non-decisive for operation support.

**Acceptance:** Inspection truthfully reports what has succeeded in this live generation without probing or leaking protocol details.

### T2406 — Lifecycle and adversarial hardening

- [ ] Cover suspend/resume, resize, endpoint loss, disposal, foreign and stale handles, generation advance, concurrent observation/allocation, and cancellation.
- [ ] Cover contradictory direct and derived geometry, overflow, malformed/oversized replies, and late replies.
- [ ] Prove snapshot bounds, no unbounded terminal-controlled allocation, no hidden replay, and deterministic cleanup.
- [ ] Preserve existing composition and partial-update commitment/failure semantics.

**Acceptance:** New observations never overstate geometry, capacity, operation support, or remote state.

### T2407 — Tile-atlas and measurement witness

- [ ] Extend the raster-animation sample with a protocol-neutral numbered/colored atlas demonstration.
- [ ] Use only public resource dimensions, planning snapshot, placeholder cells, known frames, regional updates, and selection.
- [ ] Demonstrate two-frame presentation: edit the unselected frame, await every result, then select it and reuse the former front frame.
- [ ] Report one-, four-, sixteen-, and sixty-four-region workloads with operation count, updated pixels, encoded bytes where available, CPU, allocations, acknowledgement latency, and total latency.
- [ ] Exercise explicit text fallback when placeholders or geometry are unavailable.

**Acceptance:** The witness demonstrates the exact Terminal mechanisms needed by DCurses and produces evidence for the 1.25 go/no-go decision without implementing a game.

### T2408 — Documentation and package gates

- [ ] Update the root README, sample index, raster-animation guide, persistent-raster ownership guide, capability guidance, changelog, and 1.24 release notes.
- [ ] Add XML documentation and fresh-package compile/runtime checks for every new public member on net8/net9/net10.
- [ ] Freeze equal public API snapshots across all targets and compare intentional additions with 1.23.
- [ ] Run license, dependency-boundary, package/symbol, sample, security/privacy, and documentation-link checks.

**Acceptance:** A package-only consumer can understand and use every new contract without source internals.

### T2409 — Downstream and cross-platform qualification

- [ ] Compile and execute the Terminal tile-atlas witness from the built package.
- [ ] Compile a TermInfo-free DCurses-facing consumer that uses only semantic Terminal APIs; do not require a DCurses release to complete Terminal.
- [ ] Run the complete Windows/Linux/macOS runtime, package, sample, API, XML, and artifact matrix against one exact PR head.
- [ ] Record failures and reruns against exact source SHAs.

**Acceptance:** Every required job is green at one exact head and downstream boundaries remain intact.

### T2410 — Stable 1.24.0 closure

- [ ] Record exact source SHA, CI run, package/symbol hashes, API fingerprint, dependency versions, measurement results, and downstream witnesses.
- [ ] State the evidence-based 1.25 decision without opening or promising that release automatically.
- [ ] Present the stable candidate for maintainer review; merge, tag, GitHub release, and NuGet publication remain separate maintainer actions.

**Acceptance:** One stable candidate satisfies every release gate without claiming unobserved physical-terminal behavior.

## Conditional 1.25 decision

Open a 1.25 bounded frame-edit release only after a DCurses package consumer shows that separate acknowledged edits materially miss its presentation budget and a proposed execution plan measurably improves that workload without weakening certainty.

Any later batch must validate every edit before first output, remain bounded, execute deterministically, select the final frame only after prior success, report exactly how much completed when knowable, and preserve ambiguity after committed failure. It must not promise remote atomicity, rollback, pixel restoration, hidden caches, or automatic retry.

## Explicit non-goals

Version 1.24 does not add tile maps, cameras, viewports, scene graphs, damage tracking, asset decoding, palette policy, collision, visibility, scheduling, game rules, batch edits, Indexed8 partial updates, gapless frames, absolute screen-coordinate placement, pixel-within-cell positioning, remote-capacity queries, hidden replay, raw protocol extensibility, or PTY/ConPTY hosting.

## Evidence log

| Checkpoint | Source or run | Result |
| --- | --- | --- |
| Published 1.23 source | `v1.23.0` / `65b8a820e2f5d7147e0ec07fec47228166b3f7dc` | Merged and published baseline for 1.24 development |
| Final 1.23 qualification | Run `36996137883` at PR head `312edf46bb2f4f2f2a30dbc84cd97cac17df1b93` | Nine jobs green; runtime suite passed 2,515 tests plus 15 source-integration tests on Windows, Linux, and macOS |
| Published 1.23 package | `Icod.Terminal.1.23.0.nupkg` | SHA-256 `a2793821ff510548465048e0a8b920b440554edebd136573302b47e0c190910b` |
| Published 1.23 symbols | `Icod.Terminal.1.23.0.snupkg` | SHA-256 `f9863a74ed580b52f1555b9923c813f6b02b7d3484b61e480ef55f16f5125b30` |
| Validated 1.23 PR candidate | Artifact `11221771211` | Package SHA-256 `d01e18f0a8285c37c756adf17f368b0a787982faa30f757f6b339ef366bc5a59`; symbols SHA-256 `f87a7e782ac00e6c94558c96c865f0c47a7b4b5551a0d2849ed63bb5acccb9af` |
| 1.23 public API | `docs/Public-API-Baseline-1.23.sha256` | `47ce550ebe58219a46bb711b789608592c3cad34c3c3607ffc7aaf054c8c5358` across net8.0, net9.0, and net10.0 |
| 1.23 production dependencies | Published project graph | `Icod.TermInfo 1.17.0`; `Icod.Timing 1.0.0`; compatibility floor remains 1.0.0 |
| Local planning baseline | Current environment | Repository wrapper unavailable because `pwsh` is absent; direct baseline also unavailable because `dotnet` is absent; GitHub CI is the approved verifier |
| Planning PR | [PR #69](https://github.com/uniblab/Icod.Terminal/pull/69) | Main roadmap, versioned roadmap, design, and implementation plan |
| T2400 implementation baseline | Run `37012305959` at `079bb726d9923992f2eac70bfb1c11a97a5e0bc9` | All nine jobs green; `1.24.0-alpha` pre-feature API fingerprint remains identical to the published 1.23 surface |
