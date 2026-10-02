# Icod.Terminal 1.23.0 Development Roadmap

**Goal:** Implement bounded partial animation-frame transfer, complete the remaining rich-input contract, and qualify both through consumer samples and package-only acceptance.

**Status:** Implementation candidate in draft PR #68; RED fixtures, production changes, samples, package witnesses, and API freeze are implemented. Cross-platform exact-head qualification is in progress.

**Selected scope:** Option 2 + Option 9 + Option 10.

**Design authority:** [`docs/superpowers/specs/2026-10-02-1.23.0-partial-frame-rich-input-design.md`](docs/superpowers/specs/2026-10-02-1.23.0-partial-frame-rich-input-design.md)

**Compatibility:** Preserve the stable 1.0.0 floor, existing public enum values, one authoritative input reader, opaque raster identities, and the .NET 8/9/10 target matrix. Production dependencies remain narrow; no PTY, image-codec, or Inspection dependency enters the production graph.

## Governing rules

- Partial transfer updates one existing frame from immutable caller-owned pixels. It creates no frame token and retains no hidden source or delta cache.
- `ComposeFrameAsync` remains the 1.22 source-frame operation. 1.23 partial transfer does not silently change its alpha/replacement semantics.
- Definite protocol rejection and ambiguous committed failure remain separate outcomes. Ambiguous output is never blindly retried.
- Terminal owns protocol identity, encoding, correlation, event framing, leases, lifecycle, and cleanup. Callers own placement, layout, damage, keymaps, editing, and presentation policy.
- Samples and package consumers use semantic APIs only. Physical terminal behavior is reported separately from scripted protocol evidence.

## Work sequence

### T2300 — Baseline and API-regret gate

- [x] Capture the published 1.22.0 source, API fingerprint, package hashes, dependency graph, animation contract, and rich-input inventory.
- [x] Identify concrete downstream use cases for partial updates and the missing rich-input cases; reject scope that lacks a semantic consumer.
- [x] Freeze the failure matrix, bounds, ownership, formats, and public naming before implementation.

**Acceptance:** The design explains why each public addition is needed and preserves existing ownership and compatibility boundaries.

### T2301 — Partial-frame RED fixtures

- [x] Add failing tests for a valid destination-frame region replacement and semantic result behavior.
- [x] Add failing tests for zero dimensions, overflowing coordinates, format/size mismatch, foreign/stale/disposed frames, generation loss, and pre-output cancellation.

**Acceptance:** The tests fail against the published 1.22 API and demonstrate validation before transport commitment.

### T2302 — Partial-frame semantic API and encoder

- [x] Add `UpdateFrameRegionAsync(destination, region, destinationX, destinationY, cancellationToken)` with immutable image ownership and bounded geometry.
- [x] Encode the private partial transfer through the existing graphics transaction path without exposing protocol identifiers.
- [x] Test exact wire fields, row/payload bounds, output ordering, and both RGB24/RGBA32 paths supported by the existing raster adapter.

**Acceptance:** Valid updates produce the reviewed protocol operation and invalid requests emit no bytes.

### T2303 — Partial-frame acknowledgement and lifecycle hardening

- [x] Correlate positive, definite-negative, malformed, timeout, and late responses through the existing query manager.
- [x] Preserve frame ownership on definite rejection and document uncertain affected pixels after committed failure.
- [x] Test disposal, generation loss, playback/update serialization, concurrent updates, cancellation, bounded registry/work, and cleanup.

**Acceptance:** Tests never claim stronger pixel certainty than the observed terminal outcome.

### T2304 — Rich-input gap inventory and RED fixtures

- [x] Inventory current traditional keyboard, CSI-u/Kitty, focus, bracketed-paste, and mouse behavior against the published semantic event model.
- [x] Add failing fixtures for every approved missing case, including malformed/oversized recovery, modifiers, phases, boundaries, and lease cleanup.

**Acceptance:** Each proposed input addition has a named semantic event/result and a failing test before decoder changes.

### T2305 — Rich-input and keyboard completion

- [x] Implement the approved missing decoder and event cases through the authoritative input path.
- [x] Preserve negotiation leases, query correlation, event ordering, nonfatal unknown outer events, and bounded buffering.
- [x] Add privacy and payload-handling guidance for associated text, paste, mouse, and terminal-controlled reports.

**Acceptance:** All approved fixtures pass across net8/net9/net10 without a competing reader or raw protocol API.

### T2306 — Samples and consumer documentation

- [x] Extend the animation sample with a caller-owned partial update and explicit recovery after uncertain commitment.
- [x] Extend the rich-input sample with the completed keyboard/input paths, negotiation, fallback, cleanup, and privacy notes.
- [x] Update `samples/README.md`, root README feature inventory, input/ownership docs, release notes, and migration guidance.

**Acceptance:** Samples agree with the public contract and run their shared semantic paths headlessly in scripted tests.

### T2307 — Fresh-package downstream witnesses

- [x] Consume only the built 1.23 package in fresh net8/net9/net10 projects.
- [x] Exercise partial transfer, animation recovery, rich-input event handling, and the existing Terminal-only and published-DCurses witnesses.

**Acceptance:** Package consumers use no source internals, protocol numbers, or second reader.

### T2308 — API, XML, dependency, security, and performance gates

- [x] Freeze equal public API snapshots across all target frameworks and compare intentional additive changes with 1.22.
- [ ] Run XML, package/symbol, license, dependency-boundary, security, and sample checks.
- [ ] Benchmark full-frame replacement versus partial transfer for wire bytes, CPU, allocations, first-frame latency, and total transfer latency; retain partial transfer only when measurements justify it.

**Acceptance:** Documentation, package metadata, and measured performance describe the implemented behavior precisely.

### T2309 — Cross-platform qualification and review

- [ ] Run the complete Windows/Linux/macOS runtime and package/artifact matrix on one exact PR head.
- [ ] Review input privacy, lifecycle races, partial-transfer uncertainty, API ambiguity, and downstream evidence.
- [ ] Record all failures and reruns against exact source SHAs.

**Acceptance:** All required jobs are green and limitations are explicit.

### T2310 — Stable 1.23.0 closure

- [ ] Record exact source SHA, CI run, package/symbol hashes, API fingerprint, dependency versions, and downstream witness results.
- [ ] Present the PR for review; merge, tag, GitHub release, and NuGet publication remain separate maintainer actions.

**Acceptance:** One stable candidate satisfies every release gate without claiming unobserved physical-terminal behavior.

## Deferred work

Gapless frames, absolute/pixel placement, stronger reconciliation, extensibility, image codecs, PTY/ConPTY hosting, scene/layout ownership, hidden replay, and unmeasured transport optimizations remain outside 1.23.
