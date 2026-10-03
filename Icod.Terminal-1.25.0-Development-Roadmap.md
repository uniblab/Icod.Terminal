# Icod.Terminal 1.25.0 Development Roadmap

**Goal:** Put an immediate, backend-neutral raster image into the existing ordered screen-output transaction so DCurses can use verified Kitty or Sixel for caller-supplied full-frame repaint.

**Status:** 1.25.0-alpha.1 is published with cursor hardening from [PR #73](https://github.com/uniblab/Icod.Terminal/pull/73). [PR #74](https://github.com/uniblab/Icod.Terminal/pull/74) prepares alpha.2 Sixel screen-write coalescing. Physical terminal acceptance and stable closure remain open.

**Release theme:** Ordered Screen Raster Transactions.

**Design authority:** [`docs/superpowers/specs/2026-10-03-1.25.0-screen-raster-transactions-design.md`](docs/superpowers/specs/2026-10-03-1.25.0-screen-raster-transactions-design.md)

**Implementation authority:** [`docs/superpowers/plans/2026-10-03-1.25.0-screen-raster-transactions.md`](docs/superpowers/plans/2026-10-03-1.25.0-screen-raster-transactions.md)

**Baseline:** Published `Icod.Terminal 1.24.1`, merged through [PR #70](https://github.com/uniblab/Icod.Terminal/pull/70) at `b6830ce4d7f6c97ffd9bf5e8fcbcf822febf59ea`. Production dependencies are `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`.

**Compatibility:** Preserve the stable 1.0.0 compatibility floor, .NET 8/9/10 targets, existing public signatures and enum values, managed Windows/Linux/macOS behavior, and one authoritative live input/query path.

## Release decision

The published `DisplayRasterAsync` already routes one immediate `TerminalRasterImage` through verified ordinary Kitty graphics or Sixel. A DCurses refresh cannot currently include that image between semantic cursor plans and text in one screen-output transaction. Add a typed raster item to the existing transaction. No new public backend selector or terminal-name heuristic is needed.

The 1.24 roadmap's conditional frame-edit batching proposal did not meet its real DCurses measurement gate. It remains a separate later candidate. This release is justified by the distinct, observed persistent-Kitty fallback need in DCurses 2.3.

## Governing rules

- Terminal owns live capability evidence, backend routing, private codec/quantizer, bounded precommit preparation, ordered output, lifecycle and failure certainty.
- DCurses owns where and when a complete frame is drawn, visible cells, clipping/masking, panels, text overlays, damage, refresh state, and resize recovery.
- Applications own source pixels and provide a complete current viewport image whenever an immediate repaint is required.
- Ordinary `RasterGraphics` can be verified through Kitty or Sixel; `PersistentRasterGraphics` remains separately verified through persistent Kitty identity. Sixel does not acquire placeholder or frame semantics.
- `CommitAsync` does not run a new probe while holding its output gate. Capability verification occurs before transaction construction; current evidence and epoch are rechecked before output.
- Terminal cannot promise portable graphics clipping, pixel placement, post-raster cursor state, or physical display acknowledgement. Callers include semantic cursor plans after raster output when they need a known text position.
- No native terminal calls, raw escape APIs, asset caches, automatic replay, or remote atomicity.

## Public contract gate

The additive member in the alpha implementation is:

```csharp
public sealed class TerminalScreenOutputTransaction {
    public void WriteRaster(TerminalRasterImage image);
}
```

T2500 freezes the aggregate 64 MiB ceiling for encoded raster data, including APC/DCS framing and existing application text, in one transaction. `WriteRaster` retains an immutable image item for that one transaction only. Null input fails at addition with `ArgumentNullException`; no verified backend or unsupported image semantics fail at commit with `NotSupportedException`; encoded overflow fails with `InvalidOperationException`; stale epoch/evidence fails with `InvalidOperationException`. `CommitAsync` remains single-use and returns `ValueTask`; precommit failures emit no bytes and committed output errors retain their existing uncertainty semantics.

## Work sequence

### T2500 — Published baseline and API-regret gate

- [ ] Record the published 1.24.1 source, package and symbols, API fingerprint, dependencies, and exact current raster/transaction behavior.
- [ ] Confirm the DCurses consumer's complete-frame, cell-pixel geometry, cursor plan, and status-row constraints. Record what needs physical terminal qualification.
- [ ] Freeze `WriteRaster`, the verified-backend rule, the encoded-payload bound, precommit exception types, cursor uncertainty, and compatibility requirements.
- [ ] Establish a `1.25.0-alpha` development identity after the documentation-only planning PR is accepted.

**Acceptance:** One precise public contract has a real consumer and no terminal protocol, viewport, or game policy escapes Terminal.

### T2501 — Failing transaction fixtures

- [ ] Add compile-time and behavioral failures for `WriteRaster` on a verified Kitty backend and on Sixel alone.
- [ ] Assert cursor-plan → raster → text → cursor-plan output order, single-use commit, and no bytes for unverified backend or invalid image.
- [ ] Add preparation failures for unsupported alpha semantics and encoded payload overflow before first output.

**Acceptance:** Fixtures fail against 1.24.1 because the typed transaction item is absent, and pin the precommit/output boundary.

### T2502 — Bounded raster preparation

- [ ] Reuse the current ordinary raster resolver, Kitty adapter, Sixel quantizer, and private framing without changing stand-alone `DisplayRasterAsync` behavior.
- [ ] Prepare and validate every raster item before any transaction byte. Bound aggregate encoded payload and reject overflow without unbounded allocation.
- [ ] Validate generation/backend evidence again at commitment; never switch a prepared payload after output begins.

**Acceptance:** One image is written through the verified backend, and all predictable codec/capability errors are precommit.

### T2503 — Ordered transaction emission and cursor contract

- [ ] Add the new typed item to the existing screen transaction while preserving plan, text, hyperlink, placeholder, synchronized-output, and cursor-visibility semantics.
- [ ] Exercise explicit cursor planning with unknown post-raster position; document backend-specific scrolling and clipping.
- [ ] Ensure a raster object cannot be intentionally truncated by caller cancellation after commitment.

**Acceptance:** One output lease serializes the complete ordered transaction; success reports local write/flush only.

### T2504 — Capability, lifecycle, and failure hardening

- [ ] Cover Kitty preferred, Sixel-only, unknown/unsupported evidence, ordinary Kitty verified with persistent Kitty unsupported, and generation advance.
- [ ] Cover stale output epoch, concurrent writer, session closure, precommit cancellation, write/flush failure, cleanup failure, and repeated commit.
- [ ] Verify no hidden retry, no backend switch after partial output, no broken DCS/APC framing on known input errors, and no regression in existing raster methods.

**Acceptance:** Controlled precommit failure emits nothing; committed failure is explicit and never claims remote pixel certainty.

### T2505 — Sample and DCurses package witness

- [ ] Add a public-only sample path: verify `RasterGraphics`, create the transaction, move to a conservative position, write one complete image, then explicitly reposition and write text.
- [ ] Build a fresh-package, TermInfo-free DCurses-facing consumer using only the additive semantic member and current geometry/capability APIs.
- [ ] Document that DCurses supplies the frame on every repaint and owns text fallback when no backend verifies.

**Acceptance:** Both samples consume the built package with no raw Kitty/Sixel strings, backend names in application branches, or source assembly references.

### T2506 — Documentation, API, package, and security gates

- [ ] Update README, raster guidance, sample index, transaction documentation, release notes, changelog, and XML comments.
- [ ] Freeze equal public API snapshots on net8/net9/net10 and check intended additions against 1.24.1.
- [ ] Run package/symbol, dependencies, licensing, security/privacy, payload bounds, and documentation checks.

**Acceptance:** A package consumer can discover capability requirements, cursor limits, failure certainty, and the absence of persistent Sixel semantics.

### T2507 — Cross-platform exact-head qualification

- [ ] Run the full Windows/Linux/macOS runtime and sample matrix at one exact PR head.
- [ ] Record package-only DCurses witness, API fingerprint, artifact hashes, workflow/run IDs, and all same-head reruns.
- [ ] Collect physical terminal evidence separately for Sixel alignment, overlay, resize, scrolling, and clean exit before DCurses claims visible fallback support.

**Acceptance:** All required CI jobs pass at one source head, with physical results identified as manual evidence rather than CI assertions.

### T2508 — Stable 1.25.0 closure

- [ ] Set stable metadata only after accepted implementation, update release notes and baseline evidence, then rerun the exact stable head.
- [ ] Record source SHA, workflow, package/symbol hashes, API fingerprint, dependencies, test counts, downstream and manual evidence.
- [ ] Present the candidate for maintainer review. Merge, tag, GitHub release, and NuGet publication remain separate actions.

**Acceptance:** One stable candidate meets every release gate without asserting unobserved terminal rendering.

## Live-test follow-ups

### Live-test follow-up: 1.25.0-alpha.1

The October 3 DCurses recordings show complete images in Windows Terminal and
Contour, with intermediate text-map flashes handled in DCurses PR #35. WezTerm
uses text fallback and leaves left-margin player trails. The semantic cursor planner
can choose `cud1=LF` while assuming the column survives host newline processing.

PR #73 excludes CR/LF in both parameterized and repeated relative candidates,
retains safe advertised relative/absolute alternatives, and returns unavailable if
none exists. Regression-only CI reproduced all four new cases on .NET 8/9/10
([run 37136903422](https://github.com/uniblab/Icod.Terminal/actions/runs/37136903422)).
The alpha.1 candidate includes exact-version curated release notes. After CI and
maintainer publication, DCurses must consume alpha.1 and repeat live movement,
resize, fallback, and clean-exit checks. Graphics verification is unchanged.

### Progressive image redraw: 1.25.0-alpha.2

The 17:20–17:22 UTC DCurses retest shows Contour movement without the earlier text
flash, but Windows Terminal clears and progressively redraws horizontal image bands.
The prepared Sixel screen path writes every encoder fragment separately, including
one-byte separators. Microsoft's Sixel parser explicitly supports partial image
flushes while consuming a stream; transport coalescing removes avoidable scheduling
points without assuming an atomic remote display.

PR #74 combines each already-size-checked Sixel image into one contiguous buffer
before commitment, bounded by the existing aggregate encoded limit. Exact bytes,
item ordering, epoch/evidence checks, synchronization cleanup, and failure rules
are unchanged. Kitty chunking and the standalone streaming display API remain as
before. The transient copy can briefly double encoded image storage; it adds no
unbounded cache or public batching API.

The regression-only [run 37140618899](https://github.com/uniblab/Icod.Terminal/actions/runs/37140618899)
reproduced nine writes instead of three for text/image/text on .NET 8/9/10, while
literal bytes and 2,566 other unit cases passed. The alpha.2 candidate has curated
exact-version release notes. A new physical Windows Terminal/Contour retest remains
required after publication; WezTerm text-glyph shaping is a separate hypothesis.

## Scope limits

No Sixel persistent resource, Unicode placeholder, frame copy/update/select, sparse hidden cache, full-screen scene compositor, application pixel ownership, protocol selector, image decoder, native terminal API, layout policy, automatic clipping/scaling, alpha-compositing guarantee, frame-edit batching API, or PTY/ConPTY hosting is introduced in 1.25.

## Evidence log

| Checkpoint | Source or run | Result |
| --- | --- | --- |
| Published baseline | [PR #70](https://github.com/uniblab/Icod.Terminal/pull/70) / merge `b6830ce4d7f6c97ffd9bf5e8fcbcf822febf59ea` | 1.24.1 persistent identity probe is distinct from ordinary Kitty/Sixel raster |
| Downstream motivation | [DCurses PR #35](https://github.com/uniblab/Icod.DCurses/pull/35) | Persistent Kitty unavailable in the tested sessions; controlled text fallback works; Sixel physical support remains to be tested |
| 1.25 draft PR | [#71](https://github.com/uniblab/Icod.Terminal/pull/71) | Design and alpha implementation on the same feature branch |
| T2500 contract | `fab885128d269b573e5669651915157e943835e7` | 64 MiB aggregate encoded bound, exception rules, 1.25.0-alpha metadata |
| T2501 red fixtures | [run 37127265959](https://github.com/uniblab/Icod.Terminal/actions/runs/37127265959) | Expected missing `WriteRaster`; test fixture typo also found and corrected |
| T2502/T2503 first implementation | [run 37127551967](https://github.com/uniblab/Icod.Terminal/actions/runs/37127551967) | Windows/Linux/macOS runtime passed; package API baseline gate rejected intentional new member |
| T2504–T2506 alpha qualification | [run 37127949385](https://github.com/uniblab/Icod.Terminal/actions/runs/37127949385) | Package candidate passed; full matrix in progress; API fingerprint `886a617d961af7eed37feaed026d83bbf06ec508ba248a4ca492baaf7e528146` |
| Stable candidate | Pending | Record exact-head evidence at closure |
