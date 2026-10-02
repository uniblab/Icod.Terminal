# Icod.Terminal 1.22.0 — Animation Frame Composition Development Roadmap

**Goal:** Add bounded composition of pixels from one known animation frame into another known frame on the same current persistent raster resource, then demonstrate the contract through an executable sample and a downstream consumer witness.

**Status:** Composition implementation, executable sample step, and package-only witness in PR #67. Exact-head qualification and stable closure are in progress; merge and publication are pending. The published baseline is 1.21.0.

**Tech stack:** C# 13; .NET 8, 9, and 10; PowerShell 5.1-compatible package scripts and cmd/sh. No Python or new production package.

**Selected scope:** Option 1, animation-frame composition, plus Option 10, sample, documentation, and downstream acceptance. The dependency baseline advances to production `Icod.TermInfo 1.17.0` and optional test/sample `Icod.TermInfo.Inspection 1.17.0`; `Icod.Timing 1.0.0` remains unchanged.

## Baseline and intent

Version 1.16 introduced one `TerminalRasterAnimation` controller per resource, an opaque root-frame token, acknowledged full-frame append, positive frame timing, selection, terminal-driven playback, bounded frame registry, and sequence certainty. Version 1.21 is published and does not change that animation contract. The 1.22 work makes it possible for a caller to reuse a known background frame and compose a bounded foreground rectangle into another known frame without sending a complete replacement image. A caller such as DCurses can choose the frames and presentation timing while Terminal handles private protocol identifiers and mutation certainty.

The [Kitty graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/#composing-animation-frames) specifies `a=c` with source frame `r`, destination frame `c`, source `X,Y`, destination `x,y`, positive `w,h`, and `C` for alpha blend or replacement. It reports `ENOENT` for missing image/frame, `EINVAL` for invalid geometry or overlapping rectangles on the same frame, and can report `ENOSPC`. These are private wire details. The public API must use owned frame tokens, zero-based pixel geometry, and a semantic composition mode.

## Ownership and design decisions to freeze in T2200

| Boundary | Required decision |
| --- | --- |
| Frame ownership | Both source and destination must be published, current, known tokens of the same animation and resource generation. Reject cross-resource, stale, disposed, released, and foreign tokens before output. |
| Geometry | A nonempty rectangular source region and destination origin are measured in resource pixels. Check arithmetic, source bounds, destination bounds, and same-frame overlap before commitment. Reuse the existing `TerminalRasterSourceRectangle` if its contract fits; avoid a second equivalent rectangle type. |
| Operation | Compose into an existing destination frame. No new frame identity is created and the session-wide 4,096-known-frame ceiling remains unchanged. Support alpha blend and replace only where the backend has an explicit contract. |
| Timing | Composition changes frame pixels; it does not silently change frame duration, current selection, or playback state. Define how concurrent playback and composition are serialized before public API freeze. |
| Result and certainty | A request is locally validated before output. A correlated success preserves current ownership. Distinguish a definite protocol rejection from a committed ambiguous outcome; never retry a possibly applied mutation or claim a rolled-back destination. Record whether ambiguity stales animation certainty, and why, consistently with the 1.16 model. |
| Capability | Reuse `PersistentRasterAnimation` evidence only after T2200 establishes whether it proves composition; otherwise add a separate semantic availability gate. An animation-capable endpoint must not automatically be advertised as composition-capable. |
| Layer | Terminal owns encoding, identity, correlation, order, bounds, and cleanup. DCurses and callers own cells, windows, screen coordinates, clipping, damage, placement, and scene policy. |

The exact public method and enum names are frozen during T2200, after comparing the existing `TerminalRasterAnimation`, `TerminalRasterSourceRectangle`, `TerminalControlMutationResult`, and capability contracts. A likely shape is a `ComposeFrameAsync(source, destination, sourceRectangle, destinationX, destinationY, mode, cancellationToken)` operation on the resource-owned controller. This is a design sketch, not a prematurely frozen signature.

## Scope exclusions

- Partial frame transmission, delta editing, and creation of a new frame from a base frame.
- Gapless or zero-duration intermediate frames, animation scheduling, audio, and a timeline engine.
- Absolute screen placement, pixel offsets inside terminal cells, z-order changes, and scene composition.
- Public Kitty frame/image numbers, raw APC command dictionaries, or a second input reader.
- Hidden source-frame caching, automatic replay after generation loss, passive terminal-side existence claims.
- Image file decoding, PTY/ConPTY child-process hosting, or a direct DCurses dependency in Terminal.

## Work sequence and acceptance

### T2200 — Baseline, protocol contract, and API regret gate

**Inspect:** `src/Graphics/TerminalRasterAnimation.cs`, `TerminalRasterAnimationFrame.cs`, `TerminalRasterSourceRectangle.cs`, `TerminalPersistentRasterAnimationRegistry.cs`; `src/Session/TerminalSession.PersistentRasterAnimation.cs`; `docs/Persistent-Raster-Ownership.md`; 1.16 tests and the Kitty composition reference.

- [x] Capture the 1.21 tag, API fingerprint, supported frameworks, full-frame animation behavior, registry ceiling, and currently published dependency graph.
- [x] Freeze source/destination ownership, same-frame overlap, coordinate conventions, modes, capability evidence, playback ordering, result shape, error classification, and ambiguous-commit policy.
- [x] Review the smallest additive public API with a downstream consumer; preserve 1.0.0 compatibility and avoid duplicating placement geometry or exporting Kitty identities.
- [x] Record the exact reviewed signature and state table here before implementation. Establish 1.22 prerelease metadata when feature implementation begins.

**Acceptance:** A reviewed API and failure-state matrix explain the observable behavior without claiming an unproved terminal capability.

**T2200 contract decision (2026-10-02):** Add `TerminalRasterAnimation.ComposeFrameAsync(source, destination, sourceRectangle, destinationX, destinationY, mode, cancellationToken)` returning `ValueTask<TerminalControlMutationResult>`. `TerminalRasterFrameCompositionMode` contains `AlphaBlend = 0` and `Replace = 1`; the existing `TerminalRasterSourceRectangle` supplies zero-based source geometry. Both frame tokens must belong to this controller, remain registered in its current resource generation, and fit the intrinsic resource dimensions at both ends. Same-frame composition is allowed only for nonoverlapping rectangles. Invalid local geometry or mode throws before output; unavailable ownership/backend yields a controlled result. A correlated `ENOENT` conservatively invalidates the resource because the protocol cannot distinguish a missing image from a missing frame. Other definite negative replies fail the mutation without inventing a new frame. Cancellation, timeout, or transport failure after commitment cannot be blindly retried: the caller must treat the destination pixels as uncertain, while opaque frame identities and sequence length remain locally known. `Animation.State` continues to describe ownership and sequence certainty, not the exact pixels in a frame. The existing `PersistentRasterAnimation` gate selects the backend but does not independently prove `a=c` support; only the correlated result establishes whether this operation worked. No new generic capability claim is added. Query transactions serialize control and playback operations; the writer rechecks ownership just before output to reject stale queued work.

### T2201 — TermInfo 1.17.0 dependency and integration qualification

**Files:** `Icod.Terminal.csproj`; optional Inspection references in `tests/Icod.Terminal.TermInfoIntegration.Tests/Icod.Terminal.TermInfoIntegration.Tests.csproj` and `samples/Icod.Terminal.TermInfoPersistentRaster.Sample/Icod.Terminal.TermInfoPersistentRaster.Sample.csproj`; `IntegrationDependencyBoundaryTests.cs`; current dependency documentation.

- [x] Advance production TermInfo and optional Inspection consistently to 1.17.0, retaining Timing 1.0.0 and excluding Inspection/Source from the production graph.
- [x] Restore, build, and run the TermInfo integration tests on each supported framework; exercise the actual optional sample and package dependency inspection.
- [x] Check the unified directory/hashed catalog change for any altered capability-selection or sample behavior. Record actual compatibility evidence, not just a version-string match.

**Acceptance:** The dependency guard passes, the package graph is unchanged except for the intended TermInfo version, and fresh restore/build/integration evidence is recorded.

### T2202 — RED fixtures and semantic contract

**Likely files:** `tests/Icod.Terminal.Tests/src/Graphics/TerminalRasterAnimation*Tests.cs`; `src/Graphics/TerminalRasterAnimation.cs`; focused geometry/mode contracts if required.

- [x] First add tests that fail on 1.21 for a valid same-resource composition request and for the selected semantic mode projection.
- [ ] Cover root-to-appended, appended-to-root, distinct known frames, same-frame nonoverlap, edge-aligned one-pixel rectangles, invalid/overflowing bounds, and same-frame overlap.
- [ ] Cover null/foreign/cross-session/stale/released/disposed frame tokens and unsupported capability without emitting bytes.

**Acceptance:** Tests distinguish a new operation from the already working full-frame append and demonstrate validation before transport commitment.

### T2203 — Private encoder and output commitment

**Likely files:** `src/Graphics/KittyGraphicsPersistentAnimationEncoder.cs`; a focused composition transaction helper under `src/Graphics/`; matching wire tests.

- [x] Encode private `a=c` control fields with exact zero-based coordinates, positive extent, and the two reviewed composition modes.
- [x] Use the existing session output gate and authoritative graphics response correlation; do not add a competing reader or expose raw identifiers.
- [ ] Test exact wire bytes, malformed/untrusted responses, output order, cancellation before commitment, and behavior after committed partial output.

**Acceptance:** Valid requests produce the reviewed wire contract and a failed precommit request produces no output.

### T2204 — Resource/session mutation and truthful result

**Likely files:** `src/Session/TerminalSession.PersistentRasterAnimation.cs` or a focused composition partial; `src/Graphics/TerminalRasterAnimation.cs`; registry/state types only if the frozen matrix requires them.

- [ ] Resolve both opaque tokens to registered frame states while holding appropriate ownership reservations; prevent disposal and lifecycle changes from racing between validation and output.
- [ ] Publish success only on a correlated positive reply; classify `ENOENT`, `EINVAL`, `ENOSPC`, timeout, transport failure, and late replies according to T2200.
- [ ] Preserve source/resource ownership on destination-only failures where supportable, and mark uncertain state when the destination may have changed without a trustworthy acknowledgement. No blind retry.

**Acceptance:** Tests prove local certainty never becomes stronger than the observed terminal outcome.

### T2205 — Lifecycle, concurrency, and capacity hardening

- [ ] Test resource disposal, session invalidation, generation change, duplicate concurrent composition, playback/control overlap, and concurrent frame append.
- [ ] Keep work and retained state bounded under the existing registry ceiling; composition does not allocate a frame token or source-image cache.
- [ ] Verify deterministic cleanup and no deadlock with query, output, and raster ownership gates.

**Acceptance:** Adversarial fixtures preserve output integrity, isolation between resources, and bounded memory/work.

### T2206 — Executable composition sample and consumer guide

**Likely files:** `samples/Icod.Terminal.RasterAnimation.Sample/` or a focused companion; `docs/Persistent-Raster-Ownership.md`; `samples/README.md`; root README.

- [x] Add an executable case that creates a resource, appends known frames, composes a bounded region, selects or plays the result, and disposes the resource.
- [x] Show capability fallback and how to recover after uncertain composition without guessing terminal state or replaying an emitted prefix.
- [x] Explain source versus destination pixels and why the caller still owns screen placement and presentation timing.

**Acceptance:** The shared preflight and composition steps used by the executable sample run headlessly in scripted tests. The complete interactive playback program is built but not run as a headless executable. The guide and sample agree with the public API and failure model.

### T2207 — Fresh-package downstream witness

- [x] Consume only the built package in a fresh .NET 8/9/10 project and execute the composition sample path.
- [x] Add a representative caller-owned tile frame choice without introducing a direct DCurses dependency in Terminal or shifting layout/damage ownership.
- [x] Preserve the existing published-DCurses and Terminal-only screen-output witnesses.

**Acceptance:** Package consumers can exercise the semantic operation without protocol numbers, source access, or a second reader.

### T2208 — Compatibility, API, documentation, and security gate

- [x] Capture equal public API snapshots across net8/net9/net10 and compare to 1.21 for intentional additive changes only.
- [x] Run XML documentation, NuGet package/symbol, license, dependency-boundary, sample, and security checks.
- [x] Update architecture, persistent ownership, compatibility, security, README, changelog, and versioned release notes for the actual implementation; keep historical release evidence intact.

**Acceptance:** Package and documentation describe precisely the implemented contract and its limits.

### T2209 — Cross-platform qualification and review

- [ ] Run the complete unit/integration suite and the repository's Windows, Linux, macOS, package, and artifact CI matrix on the exact PR head.
- [ ] Review concurrency/error tests and the public API for ambiguity. Distinguish scripted protocol coverage from physical emulator observations.
- [ ] Record failures and reruns against their source SHA; do not infer a clean final head from an earlier run.

**Acceptance:** Required CI jobs and current-head artifact verification are green, with any limitations recorded.

### T2210 — Stable 1.22.0 release closure

- [ ] Remove the prerelease suffix only after all earlier gates; synchronize package release notes, `CHANGELOG.md`, main roadmap, release notes, and sample installation commands.
- [ ] Record exact source SHA, CI run, package/symbol hashes, API fingerprint, and any downstream witness result.
- [ ] Present the PR for maintainer review. Merge, tag, GitHub release, and NuGet publication are separate maintainer actions.

**Acceptance:** A stable candidate at one exact source head satisfies the repository's release gates. The roadmap itself does not certify its own final commit.

## Evidence log

| Checkpoint | Source or run | Result |
| --- | --- | --- |
| 1.21.0 published baseline | [PR #66](https://github.com/uniblab/Icod.Terminal/pull/66), [v1.21.0](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.21.0) | Historical baseline |
| TermInfo 1.17.0 published | [v1.17.0](https://github.com/uniblab/Icod.TermInfo/releases/tag/v1.17.0) | Available for dependency refresh |
| Encoder fixture RED | [run 36974300099](https://github.com/uniblab/Icod.Terminal/actions/runs/36974300099) | Linux compilation failed on missing composition mode as intended |
| Semantic fixture RED | [run 36974852765](https://github.com/uniblab/Icod.Terminal/actions/runs/36974852765) | Linux compilation failed on missing composition API as intended |
| Initial composition implementation | [run 36975138106](https://github.com/uniblab/Icod.Terminal/actions/runs/36975138106) at `d619471177f25696978e9c0567301423279ee175` | Runtime Linux, macOS, and Windows passed; package fingerprint gate expected a new 1.22 baseline |
| 1.22 candidate API snapshot | [run 36975636717](https://github.com/uniblab/Icod.Terminal/actions/runs/36975636717) at `bb9a0f5be8bb1b2397d2ec5c392f68ccdcdc982e` | Package candidate passed; three-framework fingerprint `61bcebdcff55a16a17d4e5a2546421c89fddc017ca3a05ea5d2673faa39cbe34`; later jobs cancelled by subsequent push |
| First full nine-job qualification | [run 36976285028](https://github.com/uniblab/Icod.Terminal/actions/runs/36976285028) at `6b82971f2480edd1be0c5a832b5294844678f4d3` | Nine jobs green for the initial implementation; later sample and consumer changes require a new exact-head run |
| Package-only runtime composition | [run 36978770136](https://github.com/uniblab/Icod.Terminal/actions/runs/36978770136) at `c08fe02f73c2c81caebec0331d4a9eaf5c3a41a8` | Fresh-package semantic shard and artifact validation passed; runtime sample verifier caught diagnostic text containing `s=` |
| Failure and lifecycle expansion | [run 36979231245](https://github.com/uniblab/Icod.Terminal/actions/runs/36979231245) at `b78baeed64f4b23a5c1b4645b68c0f9df4e764c6` | Linux/macOS runtime and all package jobs passed; Windows net9 integration probe returned Unknown under its one-second deadline; rerun/final head needed |
| Final 1.22 PR head qualification | Pending | No stable candidate claim until full exact-head workflow passes |
