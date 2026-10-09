# Icod.Terminal 1.28.0 Development Roadmap

> **Execution:** Use the executing-plans workflow task by task in the main session
> without subagents. The approved T2800 design and implementation plan govern
> runtime and public-API changes. The runtime and public-API work is implemented;
> the stable promotion record below preserves the remaining evidence limits.

**Goal:** Make persistent Kitty Graphics animation transactions conform to the
published protocol, report their actual confirmation strength truthfully, and
provide the dependable Terminal foundation required to qualify the downstream
Icod.DCurses raster atlas.

**Release theme:** Published-Spec Kitty Graphics Transactions.

**Selection:** Follow the published Kitty Graphics Protocol rather than a
terminal-brand or version-specific wire dialect. Record exact implementation
observations separately, including confirmed deviations in Kitty 0.49.2.

**Status:** Stable `1.28.0` promotion is prepared on `release/1.28.0` after
the maintainer accepted the published-spec transaction behavior and the available
downstream ATLAS result. Exact implementation observations remain evidence-scoped;
untested terminals and any unrecorded scenarios remain `NotRun`. Merge, tag,
GitHub Release creation, and NuGet publication remain separate maintainer actions.

## Stable promotion decision — 2026-10-09

The maintainer approved stable promotion after the following bounded evidence:

- Terminal `main` at `da139821290b617e928513330893e95f4a0c6e0f`
  contains the reviewed 1.28 transaction implementation from PR #84.
- Unmodified Kitty 0.49.2 retains the exact negative multi-chunk continuation
  witness. Source-built Kitty
  `96693f4c090e9477b51ffa46aed4abdcef52d037`, containing upstream
  correction `b493a63`, supplies the positive animation, regional-update,
  corrected eight-row layout, cleanup, and exit witnesses.
- Icod.DCurses branch `2.3.0-raster-atlas-roadmap` at
  `d9ae518f0ba075f5e264acaeb14de2c9e8bc2c3d` consumes published
  `Icod.Terminal 1.28.0-alpha.1`, implements the intended compose-publish
  flow, and passed all seven jobs in
  [workflow 37940789076](https://github.com/uniblab/Icod.DCurses/actions/runs/37940789076).
  Its complete local .NET 10 suite passed 1,366 tests.
- The maintainer-observed corrected-source Kitty run selected `ATLAS`, rendered
  the supplied opaque 16×16 tile artwork, and completed interactive movement and
  scrolling responsively after full-coverage damage was coalesced. The maintainer
  accepted the result. `FRAME` and `TEXT` remain controlled alternatives.

This decision does not claim atomic or gapless remote presentation, does not
generalize optional Kitty responses into protocol requirements, and does not
qualify untested terminal implementations. The exact stable release branch and
resulting `main` commit must still pass their complete workflows before
`v1.28.0` may be created.

**Baseline:** Stable `1.27.0` was merged through
[PR #83](https://github.com/uniblab/Icod.Terminal/pull/83) at
`485bedb014ab7b66391661c709833a6a4fa4afcc` and tagged `v1.27.0` on
2026-10-06. The maintainer reports that NuGet upload is in progress. Final PR
head `df6c42189f6b6a2f40771ad2115af41a46dcb4b1` passed all ten jobs in
[workflow 37536612435](https://github.com/uniblab/Icod.Terminal/actions/runs/37536612435):
Windows, Linux and macOS each passed 2,821 unit tests plus 15 TermInfo integration
tests for `net8.0`, `net9.0` and `net10.0`, with all package shards passing. The
three packaged public-API snapshots remain identical at SHA-256
`356f455ec27065c63a642ae3d5b02125d2aed408d728cb6fadd0d47aeab12487`.

**Architecture:** Retain Terminal's existing protocol-private Kitty encoders,
single authoritative input/query path, serialized output, generation-scoped
persistent ownership and bounded transaction manager. Add an action-specific
completion contract that distinguishes protocol acknowledgement from committed
output. Higher layers continue to own cells, damage, atlas policy and fallback.

**Tech stack:** C# 13; .NET 8/9/10; managed Windows/Linux/macOS; PowerShell
5.1-compatible packaging scripts and cmd/sh launchers. No Python, native helper,
terminal-name branch or environment-variable support inference.

**Design authorities:** [Architecture](docs/Architecture.md),
[compatibility and versioning](docs/Compatibility-and-Versioning.md),
[security and privacy](docs/Security-and-Privacy.md),
[persistent raster ownership](docs/Persistent-Raster-Ownership.md), and the
[graphics hold/reopening record](docs/Graphics-Development-Hold.md). T2800 must
write the detailed design and implementation plan before runtime changes.

**T2800 approved design:**
[Published-Spec Kitty Graphics Transactions Design](docs/superpowers/specs/2026-10-06-1.28.0-published-spec-kitty-graphics-transactions-design.md),
approved by the maintainer on 2026-10-06. The corresponding
[implementation plan](docs/superpowers/plans/2026-10-06-1.28.0-published-spec-kitty-graphics-transactions.md)
is the executable RED/GREEN sequence and remains subject to plan review before
runtime implementation begins.

## Decision

### Execution checkpoint — 2026-10-08

The approved transaction implementation and package/API gates are present on PR
#84. The maintainer's corrected-source Kitty build at
`96693f4c090e9477b51ffa46aed4abdcef52d037` completed the default animation sample
(exit 0, visible changing colors and completed cleanup) and the 1/4/16/64 atlas
transaction workloads (acknowledged edits and output-committed selections). This
build contains upstream fix `b493a63` despite reporting version `0.49.2`; the
unpatched stable release remains the negative multi-chunk lane.

The first recorded atlas display was a horizontal strip. The sample row-layout
correction uses the existing public screen transaction to write eight rows and
leave reports below the grid. The follow-up recording on exact Terminal source
`70c6cacd2d26c3f962db513a23035f2570060cea` accepts that corrected grid, visible
changes, all 1/4/16/64 workloads, cleanup and exit status 0. It captures SDK
`10.0.112` and pre-run UTC time `2026-10-08 15:11:42`. All ten jobs in
[workflow 37796031515](https://github.com/uniblab/Icod.Terminal/actions/runs/37796031515)
passed on that source, including the same-head Linux retry after a timed
hardening-test failure. Exact runtime patch and clean/default-configuration
provenance remain unrecorded. T2808 downstream ATLAS, the other
implementation lane and complete T2810 closure remain open; stable metadata and
publication are not authorized by these limited positive results. See the
[versioned compatibility record](docs/Kitty-Graphics-Transaction-Compatibility-1.28.md).

### Published-protocol policy

Icod.Terminal will implement the published Kitty Graphics Protocol, not reproduce
the accidental behavior of one Kitty release. Protocol control-data grammar,
chunking, identifiers, quiet levels and actions remain specification-driven.
Implementation observations are compatibility evidence and may affect truthful
completion reporting or fallback; they do not silently change emitted wire syntax.

The governing rule is:

> Terminal emits the published protocol, waits only for responses the published
> contract guarantees, records stronger observed responses without universalizing
> them, and never upgrades output commitment into remote acknowledgement.

This decision rejects:

- repeating `i=<image-id>` on documented `a=f` continuation chunks solely to
  work around Kitty 0.49.2;
- selecting behavior from `TERM`, process names, version strings or environment
  variables;
- declaring a transaction acknowledged because a write or flush completed;
- declaring a capability unsupported because an optional success response was
  absent;
- weakening bounded cancellation, late-response ownership or ambiguous-commit
  handling to make a live sample appear successful;
- claiming ATLAS support from ordinary FRAME fallback or the persistent-resource
  sample.

## Pinned protocol and implementation evidence

The reference review is pinned on 2026-10-06 so later documentation changes can be
evaluated rather than silently inherited.

- Published protocol:
  [`docs/graphics-protocol.rst`](https://github.com/kovidgoyal/kitty/blob/96693f4c090e9477b51ffa46aed4abdcef52d037/docs/graphics-protocol.rst)
  at Kitty source `96693f4c090e9477b51ffa46aed4abdcef52d037`, document blob
  `1473d807edd602e50d6b6f8834967bf18964e996`.
- Stable implementation under qualification:
  [Kitty 0.49.2](https://github.com/kovidgoyal/kitty/releases/tag/v0.49.2),
  published 2026-10-01.
- Confirmed upstream defect report:
  [kitty #10599](https://github.com/kovidgoyal/kitty/issues/10599), opened and
  closed 2026-10-04.
- Upstream correction:
  [`b493a637b0573997f00423e8454f91a966ac96de`](https://github.com/kovidgoyal/kitty/commit/b493a637b0573997f00423e8454f91a966ac96de),
  `Graphics protocol: Fix no response for animation chunks when a=f specified in
  continuations`, committed 2026-10-04 after 0.49.2.
- Latest reviewed upstream source:
  `96693f4c090e9477b51ffa46aed4abdcef52d037` on 2026-10-06. Source inspection is
  evidence about that implementation, not proof of live behavior.

## Specification-to-implementation ledger

| Area | Published contract | Kitty 0.49.2 observation | Classification and 1.28 consequence |
| --- | --- | --- | --- |
| Chunked frame continuation | For animation frame data, continuation chunks specify `a=f`; only `m` and optional `q` otherwise continue from the starting command. | A final documented `a=f,m=0` chunk stores the frame but emits no response unless the image id is repeated. | **Confirmed 0.49.2 deviation.** Preserve documented encoding. Qualify the upstream fix and retain controlled fallback for affected releases. |
| Frame identity | Every animation command identifies its image through `i` or `I`; continuation state supplies the starting transfer identity. | The response builder loses the starting identity for explicit `a=f` continuations. | **Confirmed 0.49.2 deviation.** Do not expose or repeat private ids as a compatibility dialect. |
| Animation control `a=a` | The protocol documents select, duration, stop, loading and run controls. It does not unambiguously promise a success acknowledgement for every control. | 0.49.2 and the reviewed source execute these controls without a success response; upstream tests expect no response. | **Implementation behavior in a specification ambiguity, not presently asserted as a protocol violation.** Terminal must not require an undocumented success reply or describe output commitment as acknowledgement. |
| Frame composition `a=c` | The protocol defines composition and requires `ENOENT`, `EINVAL` or `ENOSPC` responses for specified failures. | 0.49.2 also emits `OK` for successful composition. | **Stronger implementation behavior than the explicit minimum reviewed here.** Record `OK` when observed, but do not make that Kitty behavior the portable public contract until T2800 completes the response review. |
| Quiet level `q` | `q=1` suppresses `OK`; `q=2` suppresses failure responses. | Frame transfers and composition apply quiet behavior; `a=a` already has no success response. | Preserve published quiet semantics. Do not use `q` to invent confirmation or to hide failures needed by a transaction contract. |

The ledger must remain explicit about three different claims: a confirmed protocol
deviation, behavior allowed or left ambiguous by the protocol, and behavior offered
by one implementation beyond the reviewed minimum. A live result does not move an
entry between those categories without a reference review.

## Required completion model

T2800 must freeze exact public and private spellings, but the semantic states are
already selected:

1. **Not committed:** no command bytes crossed the existing commitment boundary.
2. **Output committed:** the complete command was written and flushed through the
   session's serialized output authority, but no protocol acknowledgement is
   guaranteed or received.
3. **Protocol acknowledged:** a required or optional correlated success response
   was parsed and owned by the correct transaction.
4. **Definitely rejected:** a correlated protocol failure was received before
   local ownership was lost.
5. **Committed outcome uncertain:** output committed and a required result could
   not be established because of timeout, malformed correlation, endpoint loss,
   cancellation after commitment or lifecycle invalidation.

The implementation may reuse or add to existing result types only after the T2800
API-regret gate. It must not rename `TerminalControlStatus.Available` into a lie or
make existing `Succeeded` mean “remotely acknowledged” for an operation that only
committed output. XML documentation, samples and tests must use the same vocabulary.

## Action response policy

T2800 must derive the final matrix from the pinned protocol text and preserve a
source citation for each classification. The intended categories are:

- **Required success/failure response:** install a bounded correlated transaction
  before writing; silence follows the existing ambiguous-commit path.
- **Failure response required, success response not guaranteed:** serialize output,
  own any permitted failure response according to the reviewed correlation/window
  design, and return no stronger confirmation than observed.
- **No response guaranteed:** write and flush without a completion waiter; surface
  output failure and lifecycle loss, but do not time out waiting for impossible
  confirmation.
- **Optional stronger response observed:** parse and record it without turning the
  observation into a universal capability or support requirement.

No category may be chosen by terminal branding. If the protocol leaves a response
contract materially ambiguous, T2800 must record the ambiguity and prefer the
least-claiming safe public result. An upstream specification clarification may be
requested, but implementation must not depend on an unanswered issue.

## Downstream ATLAS direction

Icod.DCurses remains the owner of tile cells, viewport composition, damage,
fallback and repaint policy. Icod.Terminal owns only protocol conformance,
transaction/correlation semantics, persistent identities and lifecycle certainty.

The preferred downstream publication experiment is **compose-publish**:

1. copy the currently visible frame into a scratch frame;
2. apply bounded sparse region edits to the scratch frame;
3. compose the completed scratch frame onto the visible frame;
4. publish the next logical frame only after Terminal reports the truthful
   completion strength for every required operation.

This uses documented `a=f` and `a=c` actions and removes ATLAS's hard dependency on
an `a=a` success acknowledgement. It is not pre-accepted as performant or visually
atomic. T2805 measures and tests the Terminal primitive; T2808 qualifies the real
DCurses workload. If compose-publish cannot satisfy correctness or performance,
the release keeps controlled FRAME/TEXT fallback rather than weakening the contract.

Kitty 0.49.2 is expected to remain a negative ATLAS lane for documented multi-chunk
frame transfer unless the upstream fix is backported. Kitty nightly or the first
stable release containing `b493a63` is the positive candidate. This is a documented
qualification floor, not a runtime terminal-name/version branch.

## Global constraints

- Preserve the stable 1.0.0 compatibility floor and every existing public member
  and enum numeric value; additions require the T2800 API-regret review.
- Retain `net8.0`, `net9.0` and `net10.0` on managed Windows, Linux and macOS.
- Keep production dependencies at `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`
  unless a separately reviewed defect proves a change necessary.
- Preserve one authoritative input/query/event path and register response ownership
  before graphics output commits.
- Preserve serialized output, bounded waits, late-response quarantine, generation
  ownership, deterministic cleanup and no blind retry after committed ambiguity.
- Keep image ids, frame numbers, control dictionaries and implementation profiles
  private.
- Do not add retained source-image replay, hidden atlas ownership, automatic
  re-upload or terminal-side state reconstruction.
- Do not infer support from successful writes, missing optional replies, `TERM`,
  process names, version strings or documentation.
- Record exact terminal, OS, transport, configuration, package/source and UTC
  identities for every live result.
- Preserve ordinary Kitty FRAME and Sixel fallback boundaries; persistent ATLAS
  failure does not authorize replay through another backend.
- Do not broaden 1.28 into codecs, new placement geometry, Indexed8 region updates,
  gapless animation, absolute screen positioning or unrelated protocols.

## Source and test map

Exact new type and member names are frozen at T2800. The existing integration
points below bound the work and discourage unrelated refactoring.

| Area | Existing locations | Planned responsibility |
| --- | --- | --- |
| Public mutation/result contract | `src/Control/TerminalControlContracts.cs`, `src/Graphics/TerminalRasterAnimation.cs` | Represent output commitment and protocol acknowledgement without overstating either. |
| Frame transfer encoding | `src/Graphics/KittyGraphicsPersistentAnimationEncoder.cs`, `src/Graphics/KittyGraphicsPersistentAnimationTransaction.cs` | Preserve published `a=f` chunk grammar and commitment boundary. |
| Frame append/control | `src/Session/TerminalSession.PersistentRasterAnimation.cs` | Apply action-specific response policy to append, duration and selection. |
| Playback control | `src/Session/TerminalSession.PersistentRasterAnimationPlayback.cs` | Remove impossible success waits while preserving serialization and lifecycle checks. |
| Composition | `src/Session/TerminalSession.PersistentRasterAnimationComposition.cs` | Separate protocol-required failure handling from implementation-observed success responses. |
| Partial update | `src/Session/TerminalSession.PersistentRasterAnimationPartialUpdate.cs` | Preserve correlated `a=f` edits, ambiguity and sequence certainty. |
| Response parsing/correlation | `src/Graphics/KittyGraphicsPersistentAnimationResponse*.cs`, `src/Query/`, `src/Session/TerminalQueryTransactionManager.cs` | Own only responses promised or permitted by the frozen action matrix; quarantine late frames. |
| Capability evidence | `src/Capabilities/`, `src/Session/TerminalSession.Capabilities.cs`, operation-evidence files | Record protocol response evidence only when a response was actually observed. |
| Unit/integration tests | `tests/Icod.Terminal.Tests/src/Graphics/`, `tests/Icod.Terminal.IntegrationTests/` | Pin byte grammar, response absence/presence, lifecycle and concurrency semantics. |
| Public sample | `samples/Icod.Terminal.RasterAnimation.Sample`, `samples/Icod.Terminal.PersistentRaster.Sample` | Demonstrate confirmation strength, controlled fallback and deterministic cleanup. |
| Downstream witness | Icod.DCurses `CursesRasterAtlas` and raster-atlas sample | Qualify compose-publish under a real viewport/damage workload. |

## Work sequence

### T2800 — Specification freeze, deviation ledger and API-regret gate

- [x] Re-read the pinned published protocol and classify every emitted persistent
  animation action by required, failure-only, optional or absent response contract.
- [x] Preserve source citations and distinguish confirmed 0.49.2 deviations from
  specification ambiguity and implementation extensions.
- [x] Write the detailed design under `docs/superpowers/specs/` and freeze the
  minimal compatible public confirmation model, exact XML vocabulary and private
  action-policy representation.
- [x] Write the task-by-task implementation plan under `docs/superpowers/plans/`,
  including RED tests, exact files, commands and commit boundaries.
- [x] Obtain maintainer approval of the implementation plan before changing
  runtime code or the development version.

**Acceptance:** An implementer can determine what bytes to emit, whether to wait,
what result to return and what evidence to record for every in-scope action without
consulting terminal branding or guessing from Kitty source.

### T2801 — Action-policy fixtures and response-correlation foundation

- [ ] Add failing fixtures for required acknowledgement, failure-only response,
  no-response and optional-stronger-response actions.
- [ ] Prove a no-response action installs no waiter and cannot consume an unrelated
  APC reply or ordinary input.
- [ ] Prove required-response timeout, malformed reply, wrong image/frame,
  late reply and cancellation retain current commitment/uncertainty rules.
- [ ] Keep the policy internal and exhaustive; unknown actions fail closed in tests
  rather than silently selecting a completion category.

**Acceptance:** Response ownership is determined by the published action contract,
and absence of an optional response cannot stall the session.

### T2802 — Truthful mutation confirmation contract

- [ ] Introduce the T2800-approved additive confirmation representation without
  changing existing enum values or weakening existing source compatibility.
- [ ] Update result factories and XML documentation so successful output commitment
  and protocol acknowledgement are independently observable.
- [ ] Cover default construction paths, existing acknowledged mutations, output-only
  animation controls and definite/ambiguous failures.
- [ ] Regenerate and review identical public API snapshots for all target frameworks.

**Acceptance:** A caller can tell whether Terminal merely committed a command or
received a correlated protocol acknowledgement, and existing acknowledged callers
retain their meaning.

### T2803 — Specification-conformant animation-frame transfer

- [ ] Preserve first-chunk identity and documented `a=f,m=1` / `a=f,m=0`
  continuation bytes for full-frame append and partial-frame edit.
- [ ] Add regression fixtures from kitty #10599 without introducing the repeated-id
  workaround.
- [ ] Verify single-chunk and multi-chunk success, error, quiet, wrong-correlation,
  timeout, late response, output failure and lifecycle invalidation.
- [ ] Qualify the independent two-pixel probe against Kitty 0.49.2 and a build that
  contains upstream commit `b493a63`; record both exact results.

**Acceptance:** Terminal emits one published frame-transfer dialect. Corrected
implementations acknowledge it; Kitty 0.49.2 fails in a bounded, documented way.

### T2804 — Animation-control completion semantics

- [ ] Convert frame selection, duration, stop, loading and run controls from an
  unconditional success-wait model to the frozen action policy.
- [ ] Preserve pre-commit cancellation, output serialization, endpoint checks,
  ownership validation and deterministic flush behavior.
- [ ] Ensure output-only completion does not record `ProtocolResponse` evidence or
  claim terminal state was remotely verified.
- [ ] Test no reply, unexpected permitted reply, write/flush failure, concurrent
  controls, disposal and generation invalidation.

**Acceptance:** A conformant or Kitty-style no-response control completes without a
timeout and returns no stronger confirmation than the transaction actually earned.

### T2805 — Composition, region editing and compose-publish primitive

- [ ] Apply the frozen response policy to `a=c` composition while preserving all
  specified `ENOENT`, `EINVAL` and `ENOSPC` failure semantics.
- [ ] Preserve acknowledged `a=f` regional edits and sequence-certainty loss after
  ambiguous committed mutation.
- [ ] Exercise visible-to-scratch copy, sparse scratch edits and scratch-to-visible
  publication without an `a=a` acknowledgement dependency.
- [ ] Measure bounded command count, payload bytes, terminal-side full-frame copies
  and latency against the existing select-frame double-buffer path.

**Acceptance:** Terminal can support a protocol-conformant compose-publish witness
without claiming remote atomicity, rollback or universal successful-composition ACK.

### T2806 — Lifecycle, concurrency and adversarial hardening

- [ ] Cover resource release/disposal during append, edit, composition and control;
  invalidate all descendants according to existing ownership authority.
- [ ] Cover cancellation before commitment, cancellation after commitment,
  endpoint loss, session close, timeout and cleanup failure for every action class.
- [ ] Stress same-resource and cross-resource concurrency, response reordering,
  late APC frames and ordinary keyboard/input coexistence.
- [ ] Verify bounded allocations, command length, frame counts, deadlines and no
  retained caller raster after completion.

**Acceptance:** No response-policy path leaks ownership, steals input, retries an
ambiguous mutation or presents stale animation state as current.

### T2807 — Public sample and compatibility evidence

- [ ] Expand the raster-animation sample to print operation, completion strength,
  fallback reason and deterministic cleanup without exposing protocol ids.
- [ ] Add a bounded headless transcript/transport harness for response/no-response
  cases; keep live rendering evidence separate.
- [ ] Document the brief persistent-sample color flash as resource qualification,
  not ATLAS qualification.
- [ ] Add versioned evidence scenarios for frame append, composition, selection and
  cleanup with exact source/package, terminal, OS and transport identities.

**Acceptance:** A package consumer can reproduce the protocol transaction results,
while documentation keeps automated, source-based and operator-visible evidence
separate.

### T2808 — Icod.DCurses ATLAS acceptance

- [ ] Update the DCurses development branch to consume the accepted Terminal 1.28
  prerelease package or exact project source through the existing package witness.
- [ ] Implement and compare compose-publish without transferring Terminal ownership
  of cells, damage, viewport state or fallback policy.
- [ ] Exercise atlas creation, 1/4/16/64 sparse updates, movement, collision, water,
  camera scrolling, help open/close, resize, `Q`, Escape and prompt restoration.
- [ ] Require controlled `FRAME` or `TEXT` fallback on unsupported/deviating lanes;
  ordinary fallback does not qualify persistent ATLAS.

**Acceptance:** The raster-atlas sample displays `ATLAS` only after persistent
ownership and the complete live workload pass; Kitty 0.49.2 remains truthfully
negative where its frame-ACK defect prevents setup.

### T2809 — Documentation, package, API and downstream gates

- [ ] Update README, samples, graphics hold, compatibility guidance, changelog and
  release notes with the published-spec policy and exact known deviations.
- [ ] Run unit/integration tests on `net8.0`, `net9.0` and `net10.0` across Windows,
  Linux and macOS; run every package shard and fresh-package graphics sample.
- [ ] Verify public API fingerprints, XML documentation, dependencies, licenses,
  deterministic package content and documentation links.
- [ ] Prove the source and packed-package DCurses witnesses consume only public API.

**Acceptance:** Source, package and documentation describe one truthful contract,
and no artifact embeds a terminal-specific workaround or overstates live support.

### T2810 — Live qualification and stable closure

- [ ] Qualify exact versions of Kitty 0.49.2, a corrected Kitty build containing
  `b493a63`, and other available Kitty-protocol implementations without converting
  one implementation's optional responses into requirements.
- [ ] Record clean/default configuration, direct/mediated transport and operator
  observations separately; preserve every untested lane as `NotRun`.
- [ ] Run the complete exact-head CI matrix, record source/workflow/artifact/API
  identities and review all failures and same-head reruns.
- [ ] Set stable 1.28 metadata only after implementation and live acceptance;
  merge, tag, GitHub release and NuGet publication remain separate maintainer acts.

**Acceptance:** One exact candidate proves the published-spec transaction contract,
truthful confirmation states, controlled 0.49.2 failure and downstream ATLAS result
without brand heuristics or undocumented wire syntax.

## Required test matrix

| Layer | Required coverage |
| --- | --- |
| Encoder | Single/multi-chunk `a=f`; continuation controls; `a=a`; `a=c`; quiet levels; maximum ids, frames, dimensions and payloads |
| Transaction | Required ACK, negative ACK, optional ACK, no reply, wrong identity, malformed response, late response and timeout |
| Commitment | Pre-write cancellation, first-byte commitment, partial/multi-frame output failure, flush failure and post-commit cancellation |
| Ownership | Current/stale/released/disposed resource and animation; frame token ownership; generation invalidation; cascade cleanup |
| Concurrency | Same/cross-resource operations, unrelated APC traffic, ordinary input, session suspension/disposal and deadline contention |
| Evidence | OutputCommitted versus ProtocolAcknowledged; no fabricated ProtocolResponse evidence; exact terminal/transport identity |
| Downstream | ATLAS/FRAME/TEXT selection; sparse updates; gameplay movement; help; resize; exit; cleanup; prompt restoration |
| Package | Three frameworks, three operating systems, API/XML/dependency/license checks, source and fresh-package samples, DCurses witness |

## Documentation and support rules

- “Supports Kitty Graphics” always names the exercised operation and exact evidence;
  it does not imply every animation, placement or implementation version.
- “Acknowledged” means a correlated protocol response was received and parsed.
- “Committed” means Terminal completed its local serialized write/flush boundary.
- “Rendered” requires live operator-visible evidence; it cannot be inferred from
  either committed output or `OK`.
- A Kitty 0.49.2 deviation is documented by exact command, observed response,
  configuration/transport provenance and upstream source/issue reference.
- A current upstream fix is not called released until a stable release containing
  it is identified and tested.
- Unknown, untested, silent and unavailable remain distinct outcomes.

## Explicit non-goals

Version 1.28 does not add image-file decoding, PNG/GIF/APNG animation policy,
Indexed8 regional updates, gapless frames, absolute screen-coordinate placement,
pixel-within-cell placement, hidden replay, terminal-owned atlas/damage/layout,
automatic retry, a generic raw graphics API, PTY hosting or new non-graphics
protocols. It does not promise atomic multi-command remote presentation. A
subsequent release may consider batching only after real ATLAS measurements prove
a need and the public contract can remain truthful.

## Planning-PR acceptance

This planning PR is complete when:

- the main roadmap records 1.27 as merged/tagged and 1.28 as the active target;
- this roadmap records the specification-first decision and T2800–T2810 gates;
- the graphics hold records the bounded reopening, upstream issue/fix and exact
  Kitty 0.49.2 deviations/ambiguities;
- all Markdown links and exact identities are checked;
- no runtime, public API, package version or dependency has changed.

Implementation begins only after maintainer review of this roadmap and approval of
the T2800 detailed design and implementation plan.
