# Icod.Terminal 1.17.0 Development Roadmap

**Release:** `1.17.0`  
**Theme:** Terminal-owned Dimensions, Semantic Screen Planning, and Session-bound Output Transactions  
**Status:** T170-T179 implemented; TermInfo 1.15 dependency refresh locally qualified, exact-head requalification pending
**Candidate identity:** `1.17.0`
**Stable compatibility floor:** `1.0.0`  
**Prior published release:** `1.16.0` — Persistent Raster Animation and Frame Lifecycle

## Release objective

Version 1.17 prepares the live-terminal layer required to remove `Icod.DCurses`' direct dependency on `Icod.TermInfo` in the subsequent DCurses 2.0 track.

The governing rule is:

> Terminal owns terminal dimensions, semantic terminal-profile interpretation, safe screen-operation planning, and serialized output commitment; higher layers own retained cells, layout, damage, and refresh policy.

The dependency direction remains:

```text
Icod.DCurses -> Icod.Terminal -> Icod.TermInfo
```

TermInfo remains the immutable capability-data, expansion, and padding authority used internally by Terminal. The new 1.17 APIs do not expose raw TermInfo capability identifiers, strings, expansion programs, or descriptions.

## Design authority

- [`docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md`](docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md)
- [`docs/superpowers/plans/2026-09-17-1.17.0-terminal-screen-output.md`](docs/superpowers/plans/2026-09-17-1.17.0-terminal-screen-output.md)

## Scope

Version 1.17 adds:

- Terminal-owned positive cell dimensions and additive lifecycle projections;
- an immutable semantic `TerminalProfile` and screen-capability view;
- protocol-neutral screen position, color, rendition, glyph, alert, erase, shift, scroll, and region vocabulary;
- side-effect-free opaque operation planning with deterministic encoded-byte cost;
- a bounded single-use session-bound output transaction;
- transaction composition for application text, hyperlinks, raster-placeholder cells, optional synchronized framing, and final flush;
- output-epoch validation against intervening session-owned activity;
- adversarial cancellation, transport, cleanup, ownership, lifecycle, and concurrency hardening;
- package-only and downstream DCurses qualification.

## Current implementation checkpoint

The active 1.17 PR contains the T170-T177 production surface: Terminal-owned dimensions/profile contracts, all planned semantic operation families, output-epoch integration, synchronized transaction framing, strict hyperlink composition, and same-session raster-placeholder composition. T178 adds separate candidate-package witnesses for published stable `Icod.DCurses 1.6.0` compatibility and future Terminal-only screen rendering without direct TermInfo use.

The final documented T179 candidate head `0f78f0ceb39d1b8523391671c23f92a6b7c871a3` passed pull-request workflow #1982 / run `35294166853` across all nine Windows, Linux, macOS, package, downstream, API/XML, and artifact jobs. Independent candidate review found no Critical, Important, or Minor issue.

Before merge, dependency-refresh candidate `dbddaaeb656ecf69961af83ef440bf68f28ff75b` advances production `Icod.TermInfo` and optional test/sample `Icod.TermInfo.Inspection` from `1.14.0` to `1.15.0`. Its byte-identical local tree-equivalent commit `1eab0f8793a9b8984e1db9b553671721c6b0b5df` passed 2,369 unit tests and 15 TermInfo integration tests per target framework, all samples and DCurses acceptance, eight hardening-soak cycles per framework, and all four package shards. Exact local package hashes are `d06ca42015c5a1ec92c9f1931ddc842e14131cc7d8347bcd6e9e9b47572032d3` (`.nupkg`) and `555933bf87e060fff92a8a56ebd6a3c6e65a9f77fb30e957f14cd1d097bcc6d9` (`.snupkg`). The `net8.0`, `net9.0`, and `net10.0` API snapshots remain byte-identical with fingerprint `c0a051a925d551e526343ef59d8c47d75e41868d84235fa30bfa7debe1b3ceb9`.

The dependency-refresh commit and later evidence-only bookkeeping head still require the complete matrix; merge, tagging, release creation, and publication remain later explicit maintainer decisions.

## Tranche sequence

```text
T170  architecture, reference snapshot, API-regret gate, and development identity
T171  Terminal-owned dimensions and lifecycle projections
T172  immutable semantic TerminalProfile and screen-capability projection
T173  cursor, ACS glyph, alert, and opaque operation-plan foundation
T174  rendition normalization, color validation, transition, and reset planning
T175  erase, character/line shift, scroll, region, padding, and cost planning
T176  bounded session-bound transaction, output epoch, serialization, and framing
T177  hyperlink/raster composition, commitment, cleanup, and failure aggregation
T178  lifecycle/concurrency/security/downstream/package/API/XML hardening
T179  stable 1.17 release closure
```

## T170 — Architecture and contract freeze

T170 records the exact responsibility boundary before production implementation. It updates the main roadmap, opens the version-specific track, establishes `1.17.0-alpha.1`, and freezes the additive public vocabulary.

Acceptance requires:

- no transfer of cells, layout, damage, Unicode-width policy, or screen comparison into Terminal;
- no raw TermInfo or escape-sequence surface in the new API;
- explicit compatibility with every existing Terminal 1.x signature;
- a bounded and testable transaction commitment model;
- a direct mapping from every current DCurses TermInfo use to either Terminal planning or retained DCurses policy.

## T171 — Terminal-owned dimensions

Add `TerminalDimensions`, `TerminalSession.GetDimensions()`, and `TerminalLifecycleEvent.Dimensions` as additive projections. Existing `GetSize()`, lifecycle `Size`, provider interfaces, and `Icod.TermInfo.TerminalSize` remain unchanged.

Acceptance covers positive validation, unavailable/failed projection fidelity, resize/resume consistency, default-value rejection where consumed, and public signature isolation.

## T172 — Semantic terminal profile

Add one immutable `TerminalProfile` per session with terminal-independent identity metadata and screen capabilities. It reports static selected-profile facts without claiming live verification.

Acceptance covers color models/counts/selectors, default-color restoration, supported/restricted attributes, ACS, cursor visibility/addressing, immutable alias data, and no TermInfo type leakage.

## T173 — Core screen planning

Add a session-bound `TerminalScreenPlanner` and opaque `TerminalScreenOperationPlan`. Initial planning covers cursor movement, alerts, ACS mode transitions, and semantic line glyph mapping.

Candidate selection remains deterministic: shortest encoded byte cost wins, and stable candidate order breaks ties. Planning is side-effect free.

## T174 — Rendition and color planning

Add semantic colors/renditions, normalization, safe reversible transitions, indexed/direct validation, and reset planning. TermInfo color metadata and expansion stay private.

Unsupported or non-reversible requests degrade to the documented normalized rendition rather than emitting an unsafe partial state.

## T175 — Editing and scrolling plans

Add explicit erase, insert/delete character, insert/delete line, forward/reverse scroll, and scroll-region plans. Terminal resolves safe encodings and costs; a higher layer decides whether a plan reproduces its desired screen and beats rewriting.

No retained-screen comparison algorithm enters Terminal.

## T176 — Session-bound transaction foundation

Add a bounded, single-use screen-output transaction. Creation captures the serialized-output epoch; commit rejects intervening session-owned activity before output, acquires the existing output gate, validates the complete batch, writes without interleaving, optionally frames synchronized output, flushes, and releases the gate.

Pre-commit cancellation emits nothing. Post-commit cancellation does not intentionally truncate the transaction or required cleanup.

## T177 — Semantic composition

Compose application text, strict OSC 8 hyperlink text, and opaque same-session raster-placeholder cells in caller-supplied order. Reuse existing encoding and ownership validation without creating a second hyperlink or raster ownership domain.

Independent primary and cleanup failures remain observable; no replay is attempted.

## T178 — Hardening and package qualification

Qualify bounds, overflow, stale epochs, foreign plans/tokens, lifecycle invalidation, disposal races, concurrency, partial writes, cleanup failure, strict encodings, padding, and borrowed-output exclusions.

Add fresh package-only consumers and stable DCurses 1.6 compatibility. Add a dedicated acceptance consumer proving that future DCurses rendering needs only new Terminal screen contracts.

The package release shard runs these as two independent consumers across every supported target framework. Pull-request workflow #1979 / run `35286375813` qualified the T178 hardening head across the complete nine-job matrix. This closes T178 qualification but does not close the T179 stable publication decision.

## T179 — Stable closure

Public API/XML snapshots are frozen across all target frameworks, permanent architecture/compatibility/security authorities and stable release metadata are synchronized, and exact candidate head `d02fc710d2fa8d492153c03dc644f21f194eb1dd` is qualified across Windows, Linux, and macOS. The evidence-only bookkeeping commit defined by [`docs/superpowers/plans/2026-09-18-1.17.0-stable-closure.md`](docs/superpowers/plans/2026-09-18-1.17.0-stable-closure.md) remains to be requalified.

Publication remains an explicit maintainer action after the stable candidate is accepted.

## Explicit non-goals

Version 1.17 does not add:

- Terminal-owned cells, windows, pads, panels, layout, clipping, or damage;
- desired-versus-physical screen comparison or automatic repaint;
- Unicode display-width policy;
- generic raw capability or control-sequence APIs;
- a second terminal-description database or live input/query reader;
- hidden screen/raster replay;
- DCurses 2.0 changes inside the Terminal repository;
- unrelated raster-animation, PTY, process-hosting, or widget features.

## Release gates

The stable candidate must pass:

- runtime tests on `net8.0`, `net9.0`, and `net10.0`;
- Windows, Linux, and macOS workflows;
- package/public API/XML/license validation;
- fresh package-only screen-planning and transaction consumers;
- TermInfo integration qualification;
- stable DCurses downstream compatibility and hardening;
- exact-head artifact validation.
