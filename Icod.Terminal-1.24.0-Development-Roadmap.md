# Icod.Terminal 1.24.0 Development Roadmap

**Goal:** Publish the pixel geometry, persistent-resource geometry, bounded local planning, and operation-evidence contracts required by a later `Icod.DCurses` tile renderer, then qualify them with an executable tile-atlas workload.

**Status:** Qualified stable package candidate ready for maintainer review in [PR #69](https://github.com/uniblab/Icod.Terminal/pull/69).

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

- [x] Add compile-time/public-surface failures for the new pixel-dimension type, derivation helper, and two query methods.
- [x] Add behavior failures for positive dimensions, zero/negative rejection, exact derivation, indivisible dimensions, timeout, cancellation, malformed replies, and mismatched CSI replies.
- [x] Preserve bounded late-response ownership and the single authoritative input/query coordinator.

**Acceptance:** Tests fail against 1.23 because the public contract is absent while existing internal query behavior remains green.

### T2402 — Semantic pixel geometry

- [x] Replace the internal `TerminalPixelSize` with the frozen public immutable value type without changing wire behavior.
- [x] Publish terminal-pixel and cell-pixel query methods through the existing transaction manager.
- [x] Publish exact derivation without caching, guessing, rounding, or treating timeout as unsupported.
- [x] Document an explicit consumer fallback: query cell pixels; on timeout, query terminal pixels and combine them with `GetDimensions()` only when exact derivation succeeds.

**Acceptance:** Consumers can obtain or exactly derive cell pixel geometry without raw control sequences or brand detection.

### T2403 — Persistent-resource geometry

- [x] Add immutable `PixelWidth` and `PixelHeight` properties backed by the resource's existing source dimensions.
- [x] Verify values across RGB24, RGBA32, and Indexed8 resource creation.
- [x] Verify that property access performs no I/O, exposes no protocol identity, and retains its intrinsic meaning after lifecycle loss or disposal.

**Acceptance:** A consumer can partition a known atlas using the resource handle as the authoritative geometry source.

### T2404 — Raster planning snapshot

- [x] Publish all stable library ceilings needed for atlas planning: image dimension, pixel count, owned bytes, palette entries, resources, combined placements, relative depth, placeholder extent, and allocated animation frames.
- [x] Add advisory local counts for currently owned resources, combined placements, and allocated animation frames, including in-flight frame reservations where they consume admission capacity.
- [x] Capture each registry's counts under its existing synchronization and document that the combined snapshot is observational rather than an atomic reservation.
- [x] Keep final create/append results authoritative when concurrent work changes capacity after observation.

**Acceptance:** DCurses can reject an impossible atlas plan before output while never mistaking local bookkeeping for terminal storage.

### T2405 — Generation-scoped raster-operation evidence

- [x] Add focused raster-operation status rather than expanding the broad `TerminalCapability` enum with format variants.
- [x] Record semantic-operation evidence separately from the shared Kitty animation backend so one successful operation does not verify unrelated operations.
- [x] Verify composition after successful `ComposeFrameAsync` acknowledgement and verify the matching RGB24 or RGBA32 variant after successful `UpdateFrameRegionAsync` acknowledgement.
- [x] Clear live evidence on lifecycle-generation advance; keep initial state unknown and endpoints separately available/unavailable.
- [x] Leave generic rejection, timeout, cancellation, transport loss, and missing-resource invalidation non-decisive for operation support.

**Acceptance:** Inspection truthfully reports what has succeeded in this live generation without probing or leaking protocol details.

### T2406 — Lifecycle and adversarial hardening

- [x] Cover suspend/resume, resize, endpoint loss, disposal, foreign and stale handles, generation advance, concurrent observation/allocation, and cancellation.
- [x] Cover contradictory direct and derived geometry, overflow, malformed/oversized replies, and late replies.
- [x] Prove snapshot bounds, no unbounded terminal-controlled allocation, no hidden replay, and deterministic cleanup.
- [x] Preserve existing composition and partial-update commitment/failure semantics.

**Acceptance:** New observations never overstate geometry, capacity, operation support, or remote state.

### T2407 — Tile-atlas and measurement witness

- [x] Extend the raster-animation sample with a protocol-neutral numbered/colored atlas demonstration.
- [x] Use only public resource dimensions, planning snapshot, placeholder cells, known frames, regional updates, and selection.
- [x] Demonstrate two-frame presentation: edit the unselected frame, await every result, then select it and reuse the former front frame.
- [x] Report one-, four-, sixteen-, and sixty-four-region workloads with operation count, updated pixels, encoded bytes where available, CPU, allocations, acknowledgement latency, and total latency.
- [x] Exercise explicit text fallback when placeholders or geometry are unavailable.

**Acceptance:** The witness demonstrates the exact Terminal mechanisms needed by DCurses and produces evidence for the 1.25 go/no-go decision without implementing a game.

### T2408 — Documentation and package gates

- [x] Update the root README, sample index, raster-animation guide, persistent-raster ownership guide, capability guidance, changelog, and 1.24 release notes.
- [x] Add XML documentation and fresh-package compile/runtime checks for every new public member on net8/net9/net10.
- [x] Freeze equal public API snapshots across all targets and compare intentional additions with 1.23.
- [x] Run license, dependency-boundary, package/symbol, sample, security/privacy, and documentation-link checks.

**Acceptance:** A package-only consumer can understand and use every new contract without source internals.

### T2409 — Downstream and cross-platform qualification

- [x] Compile and execute the Terminal tile-atlas witness from the built package.
- [x] Compile a TermInfo-free DCurses-facing consumer that uses only semantic Terminal APIs; do not require a DCurses release to complete Terminal.
- [x] Run the complete Windows/Linux/macOS runtime, package, sample, API, XML, and artifact matrix against one exact PR head.
- [x] Record failures and reruns against exact source SHAs.

**Acceptance:** Every required job is green at one exact head and downstream boundaries remain intact.

### T2410 — Stable 1.24.0 closure

- [x] Record exact source SHA, CI run, package/symbol hashes, API fingerprint, dependency versions, measurement results, and downstream witnesses.
- [x] State the evidence-based 1.25 decision without opening or promising that release automatically.
- [x] Present the stable candidate for maintainer review; merge, tag, GitHub release, and NuGet publication remain separate maintainer actions.

**Acceptance:** One stable candidate satisfies every release gate without claiming unobserved physical-terminal behavior.

## Conditional 1.25 decision

Open a 1.25 bounded frame-edit release only after a DCurses package consumer shows that separate acknowledged edits materially miss its presentation budget and a proposed execution plan measurably improves that workload without weakening certainty.

**Closure decision:** Do not open the bounded batching/Indexed8 1.25 release from the 1.24 Terminal witness alone. The package-only 1/4/16/64-region results establish bounded linear work and the measurement mechanism, but they do not supply the required real DCurses presentation budget or comparative batch result. Version 1.25 remains uncommitted and available for a different independently justified feature.

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
| T2401/T2402 pixel geometry | Run `37014482468` at `25d6e0a962368f39c51380dd361fd2a94280742c` | All nine jobs green after one same-head Windows rerun isolated an unrelated timing flake; public API fingerprint `41576a33a971ef9634ac0e409ba06c8e264f2d3d264ab8445b983e7d5ebf0fa2` |
| T2403 resource geometry | Run `37017398715` at `42d1299f00e976021a0110d3e4f3fd458f879d76` | All nine jobs green after one same-head Linux rerun isolated an unrelated keyboard-lease timing flake; public API fingerprint `c02db531c9f029635ff436785a8a862b4e5f2ce2665e9c22f5347cc3a6759809` |
| T2404 raster planning | Run `37021056682` at `38958cdc62b05cda6ddf9f86caa07cedd6548689` | All nine jobs green; fixed ceilings and synchronized advisory counts including append reservations; public API fingerprint `67dcb10e5a1dad58db9e4c83e13fda38976e3ac68195fcec7a7f8bbb1b8ca2d8` |
| T2405 raster-operation evidence | Run `37024183550` at `3c4eea8b4d3954680059d6a70de8ae7b8223ad92` | All nine jobs green; direct generation-scoped composition/RGB24/RGBA32 evidence remains isolated from broad backend evidence; public API fingerprint `fc3ebf0fb2f6561084deb2fd49a5043373a27da1486a222f932a7a91fd43f6ca` |
| T2406 lifecycle hardening | Run `37025367302` at `8db73c088ccfa7d802089ba92e17f287e5ba0384` | All nine jobs green; resize/no-cache, malformed and maximum geometry, concurrent count snapshots, pending reservations, suspend/resume evidence expiry, and late-acknowledgement generation races passed on all runtime targets |
| T2407 tile-atlas witness | Run `37027131690` at `878832ac3abb8bba46e076e3b3a6e15f55a97f61` | All nine jobs green; package-only net8/net9/net10 consumers exercised 1/4/16/64 acknowledged region workloads, two-frame selection ordering, total encoded bytes, latency, CPU, allocations, and explicit non-physical-rendering scope |
| T2408/T2409 candidate qualification | Run `37029340298` at `e212a3b14c11f300d236150274df419a45ff3563` | All nine jobs green after a same-head Windows rerun isolated an unrelated OSC 52 timing timeout; 2,547 runtime tests plus 15 source-integration tests passed per framework, expanded XML/API/package gates passed, and package-only tile plus TermInfo-free/published-DCurses consumers passed |
| T2410 stable candidate | Run `37031211110` at `26f650497de6b11c3dbab106e1ecf6d18fbdcdeb`; artifact `11237750694` | All nine jobs green with stable `1.24.0` metadata; 2,547 runtime tests plus 15 source-integration tests passed on net8/net9/net10 across Windows, Linux, and macOS; package SHA-256 `494d917c075cbe7ff4de41a604cf03a2e9fdca0714c35e65e7c6867761bbe483`; symbols SHA-256 `15c81169bd3a90b6b0b8a9e2749af24e6bd225c4101026089bb3be005954c6bf`; API `fc3ebf0fb2f6561084deb2fd49a5043373a27da1486a222f932a7a91fd43f6ca`; dependencies `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`; package-only measurements and TermInfo-free/published-DCurses witnesses passed |
