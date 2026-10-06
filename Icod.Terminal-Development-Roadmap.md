# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current published feature line:** `1.26.0` — Terminal Compatibility Qualification
- **Latest historical patch line:** `1.24.1` — Persistent Kitty resource-identity verification
- **Development status:** `1.26.0` is published to NuGet, confirmed by the maintainer on 2026-10-05. Tag `v1.26.0` resolves to `2c0fafaf7d7a4f6a3cbdad206960f159fb19ef95` (PR #82 merge). `1.27.0-alpha.1` implements the approved appearance query, independent appearance/in-band resize leases, lifecycle hardening, and compatibility/package gates. All ten jobs in workflow `37453595429` passed at implementation head `0da49e0cb436eb3bc339c93819819b8b0e1bce3b`; reviewed live witnesses remain required before stable closure. Graphics development remains on hold, and Kitty persistent-ATLAS acceptance is deferred.
- **Active development target:** `1.27.0` — Live Terminal Environment Awareness.
- **Selected scope:** **Options 2 + 3: terminal appearance and resize awareness**, with focused compatibility scenarios, public-only samples, and no planned Icod.TermInfo change. See the [1.27 development roadmap](Icod.Terminal-1.27.0-Development-Roadmap.md).
- **Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range planning. Detailed design evidence remains in versioned roadmaps, release notes, public-API baselines, accepted CI checkpoints, and approved design/implementation documents.

The original pre-1.0 roadmap is preserved at [`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md).

## Latest accepted checkpoint

`Icod.Terminal 1.26.0` is published. Its release comprises the compatibility sample, bounded evidence model, deterministic versioned matrix, and fresh-package acceptance, with no production runtime API additions. [PR #79](https://github.com/uniblab/Icod.Terminal/pull/79) delivered the feature; PRs #80–#82 completed curated notes and stable package metadata. Tag `v1.26.0` resolves to `2c0fafaf7d7a4f6a3cbdad206960f159fb19ef95`. See the [release notes](docs/releases/1.26.0.md) and [reviewed matrix](docs/compatibility/1.26.0.md). Initial live passes cover identity and dimensions on the two recorded environments only; all unrecorded scenarios remain unqualified.

The maintainer selected **options 2 + 3** for `1.27.0` on 2026-10-05. The [development roadmap](Icod.Terminal-1.27.0-Development-Roadmap.md) defines T2700–T2710 and a contract-review gate before implementation.

### Historical checkpoints

The entries below preserve earlier source/qualification milestones; their then-pending publication wording is historical, not the current release status.

Stable `Icod.Terminal 1.25.0` source was merged through [PR #78](https://github.com/uniblab/Icod.Terminal/pull/78) at `7896879a31e8672683bbc6b7c4cf5f99fafc789a`. The stable source preserves the documented graphics hold and does not claim successful Kitty persistent-ATLAS qualification. Stable publication remains a separate maintainer action; the [T2508 closure record](docs/T2508-1.25-Stable-Source-Closure.md) records the exact limits.

`Icod.Terminal 1.25.0-alpha.5` was merged through [PR #77](https://github.com/uniblab/Icod.Terminal/pull/77) and published. It includes bounded payload-transfer deadlines plus the earlier Unix byte-input, DA1, cursor and Sixel corrections. The downstream Kitty 0.32.2 retest still lacks an animation-upload ACK; the longer deadline bounds the wait without fixing that defect. The maintainer has put this graphics track on hold. This checkpoint does not promote 1.25 to stable or qualify persistent ATLAS from ordinary FRAME fallback.

`Icod.Terminal 1.24.1` was merged through [PR #70](https://github.com/uniblab/Icod.Terminal/pull/70) at `b6830ce4d7f6c97ffd9bf5e8fcbcf822febf59ea` and published. It corrects persistent-raster verification after downstream Contour qualification showed that generic Kitty query support does not guarantee a valid terminal-assigned persistent image id. The new 1.25 track responds to the distinct need for immediate raster output that can be ordered with screen operations when persistence is unavailable.

`Icod.Terminal 1.23.0` was merged through [PR #68](https://github.com/uniblab/Icod.Terminal/pull/68) at `65b8a82`, tagged `v1.23.0`, and published on 2026-10-02. It adds bounded RGB24/RGBA32 animation-frame region replacement, completes legacy Menu phase decoding, repairs malformed modern-keyboard end-of-input recovery, and expands the runnable raster-animation and rich-input guidance. The [1.23 development roadmap](Icod.Terminal-1.23.0-Development-Roadmap.md) preserves its implementation and qualification evidence.

`Icod.Terminal 1.21.0` was merged through [PR #66](https://github.com/uniblab/Icod.Terminal/pull/66) at `24295f83153ce18f731dd2eced83d19ccf70b972`, tagged `v1.21.0`, and published as a [GitHub release](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.21.0) on 2026-09-28. The [1.21 development roadmap](Icod.Terminal-1.21.0-Development-Roadmap.md) records T2100–T2110 and the pre-merge qualification; the final PR head passed [all nine CI jobs](https://github.com/uniblab/Icod.Terminal/actions/runs/36358713525). Its input and cursor-visibility scope is complete.

Version 1.22.0 selects **bounded animation frame composition** with an executable sample and downstream acceptance. [PR #67](https://github.com/uniblab/Icod.Terminal/pull/67) contains the semantic API, private encoder, acknowledgement path, sample, and a fresh-package runtime witness. The implementation head passed [all nine CI jobs](https://github.com/uniblab/Icod.Terminal/actions/runs/36982553507); the [1.22 development roadmap](Icod.Terminal-1.22.0-Development-Roadmap.md) records source and artifact identities. It was subsequently merged and published as 1.22.0; its final nine-job qualification is recorded in that roadmap.

### Earlier checkpoints

`Icod.Terminal 1.19.0` was merged through [PR #64](https://github.com/uniblab/Icod.Terminal/pull/64) at `b3f7adf929d36ea654f2116ad6132781edc3fb3b`, tagged `v1.19.0`, and published as a [GitHub release](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.19.0) on 2026-09-27. The final pre-merge head `5849ec717ba59a37ec7395d11f5c892ef3f380b2` passed all nine jobs in [workflow 36296373936](https://github.com/uniblab/Icod.Terminal/actions/runs/36296373936), including 2,402 unit tests and 15 integration tests per framework on Windows, Linux, and macOS. Its production dependencies are `Icod.TermInfo 1.16.0` and `Icod.Timing 1.0.0`; optional integration uses `Icod.TermInfo.Inspection 1.16.0`.

`Icod.Terminal 1.17.0` was published from annotated tag `v1.17.0` at exact merge commit `ce2d76dda3f7d455a891d4268f453c99112cae8e` on 2026-09-18. The final dependency-refresh evidence head `af6ef4bfc53603302597c927b3058854edd95ebd` passed all nine jobs in pull-request workflow #1984 / run `35297012969` before merge.

The published 1.17.0 checkpoint uses production `Icod.TermInfo 1.15.0` and optional test/sample `Icod.TermInfo.Inspection 1.15.0`. It retains identical public API snapshots across `net8.0`, `net9.0`, and `net10.0` with fingerprint `c0a051a925d551e526343ef59d8c47d75e41868d84235fa30bfa7debe1b3ceb9`.

Version 1.17.1 is a documentation-only patch that corrects the README embedded in 1.17.0 and synchronizes release metadata. It makes no runtime or public-API change.

Version 1.18.0 was merged through PR #63 at `3e150377db990141aa6903631a8b95cb2c41116e` and tagged `v1.18.0`. T180-T185 are accepted: the additive unknown-rendition baseline API, hardening/ownership tests, identical three-framework API fingerprint, package/XML gates, published DCurses 1.6.0 soak, TermInfo-free future-renderer package witness, and nine-job stable-candidate workflow are complete.

The [1.19 roadmap](Icod.Terminal-1.19.0-Development-Roadmap.md#stable-candidate-qualification) preserves earlier qualification checkpoints and their exact source/artifact identities. The final dependency-refresh evidence and merged release identity are recorded above; those earlier hashes are not the final published package hashes.

## Current architecture

```text
Icod.TermInfo
      ^
      |
Icod.Terminal
      ^
      |
Icod.DCurses
      ^
      |
terminal applications
```

- `Icod.TermInfo` owns immutable terminal capability data and expansion.
- `Icod.Terminal` owns the live terminal conversation, input/query/event authority, semantic capability evidence and routing, terminal output, ephemeral raster routing and transaction commitment, persistent raster resource/placement/placeholder ownership, animation/frame lifecycle, protocol-private encoding/identity, lifecycle certainty, and deterministic cleanup.
- `Icod.DCurses` owns cells, windows, virtual-screen state, screen coordinates, clipping, scrolling, layout, refresh/diff policy, damage, and higher-level presentation policy.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

The production dependency baseline for published 1.26 and planned 1.27 is:

```text
Icod.TermInfo 1.17.0
Icod.Timing   1.0.0
```

Optional integration tests/samples use `Icod.TermInfo.Inspection 1.17.0`; Inspection and Source remain outside the production package graph.

## Qualified stable sequence and active target

```text
1.5.0   normalized control families / capability evidence / semantic routing
1.6.0   complete CSI grammar / terminal and cell pixel geometry
1.7.0   DCS / Sixel / public backend-neutral raster contract
1.8.0   APC / Kitty Graphics / verified multi-backend raster routing
1.8.1   documentation and sample maintenance
1.9.0   unsolicited protocol-neutral semantic events
1.10.0  semantic capability inspection and explicit bounded verification
1.11.0  opaque persistent raster resources and placements
1.11.1  TermInfo persistent-raster lifecycle integration contract
1.12.0  bounded source-pixel cropping and signed z-order
1.13.0  bounded immutable-parent relative placement ownership
1.14.0  side-effect-free persistent-raster lifecycle observability
1.15.0  Unicode placeholder and virtual raster placement
1.16.0  persistent raster animation and frame lifecycle             PUBLISHED
1.17.0  Terminal-owned dimensions, screen planning, and transactions PUBLISHED
1.17.1  packaged README and release metadata correction             PATCH
1.18.0  unknown-rendition baseline recovery                         PUBLISHED
1.19.0  downstream/planner/transaction hardening and docs/samples    PUBLISHED
1.20.0  profile and capability decisions                           PUBLISHED
1.21.0  rich input/keyboard and cursor visibility composition     PUBLISHED
1.22.0  bounded animation frame composition                     PUBLISHED
1.23.0  partial frame transfer + rich-input completion           PUBLISHED
1.24.0  raster geometry and planning contracts                 PUBLISHED
1.24.1  persistent Kitty identity verification                PUBLISHED
1.25.0  ordered screen raster transactions                    STABLE SOURCE / GRAPHICS HOLD
1.26.0  terminal compatibility qualification                  PUBLISHED
1.27.0  appearance and resize awareness                       PLANNING / OPTIONS 2 + 3
```

The unchanged 1.18–1.19 public API fingerprint is:

```text
48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d
```

Permanent ownership authority: [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

1.17 release notes: [`docs/releases/1.17.0.md`](docs/releases/1.17.0.md).

1.17 versioned evidence: [`Icod.Terminal-1.17.0-Development-Roadmap.md`](Icod.Terminal-1.17.0-Development-Roadmap.md).

## 1.16 stable release — Persistent Raster Animation and Frame Lifecycle

The completed 1.16 line extends the existing persistent-raster ownership model with terminal-resident animation frames and playback control while preserving the same architectural boundaries that guided 1.11–1.15.

The governing rule is:

> Terminal owns animation protocol identity, frame-sequence certainty, timing commands, and serialized control; callers own source frames, presentation placement, and higher-level animation policy.

The preferred semantic direction is:

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    +-- TerminalRasterPlaceholder
    +-- TerminalRasterAnimation
            |
            +-- TerminalRasterAnimationFrame
```

`TerminalRasterAnimation` is intended to represent one animation sequence associated with one existing persistent raster resource. The resource's original image data is the root frame. Additional full-size frames are transferred as terminal-resident animation frames and represented publicly by opaque semantic handles/tokens rather than protocol frame numbers.

Version 1.16 targets:

- a distinct semantic `PersistentRasterAnimation` capability;
- full-frame animation transfer only;
- opaque frame identity and bounded sequence tracking;
- per-frame positive timing;
- explicit current-frame selection;
- terminal-driven stop, loading-mode run, normal looping run, and loop-count policy;
- resource/session lifecycle propagation;
- explicit animation-sequence certainty loss after ambiguous committed frame operations;
- no retained source-frame replay cache;
- no transfer of layout/window/cell/damage ownership into Terminal.

Kitty frame composition/delta editing (`a=c`), partial-frame updates, gapless composition frames, and frame-to-frame pixel composition are intentionally deferred until the base animation lifecycle is stable.

## 1.16 tranche sequence

```text
T160  architecture/API-regret gate and public animation contract freeze
T161  semantic PersistentRasterAnimation capability and evidence rules
T162  private frame-sequence model, root-frame semantics, bounded identities
T163  acknowledged full-frame transfer and opaque frame publication
T164  per-frame timing and explicit current-frame selection
T165  terminal-driven stop/loading/run/loop playback semantics
T166  resource/session lifecycle propagation and sequence-certainty loss
T167  ENOENT/EINVAL/timeout/late/transport/capacity/concurrency hardening
T168  executable sample, downstream, package/API/XML/security/docs qualification
T169  stable 1.16.0 release closure
```

Exact public spellings and bounds were frozen by T160 and qualified through T168; the roadmap records the accepted semantic architecture.

## 1.16 design guardrails

The 1.16 design must preserve:

- one authoritative input/query/event path per live session;
- semantic capability planning rather than terminal-brand guessing;
- backend-neutral public raster semantics;
- opaque resource, physical-placement, virtual-placement, animation-frame, and generation identities;
- generation-scoped persistent ownership with no automatic replay;
- deterministic cleanup and committed-output integrity;
- side-effect-free 1.14 ownership observation;
- 1.15 placeholder layout ownership remaining caller-side;
- production routing remaining inside Terminal rather than `Icod.TermInfo.Inspection`;
- bounded memory/work for frame registries, transaction correlation, and tests;
- no hidden source-image/frame cache for reconstruction after lifecycle loss.

## Explicit 1.16 non-goals

Version 1.16 does not add:

- frame-to-frame composition or delta editing;
- partial-frame patch/update APIs;
- gapless composition frames;
- absolute screen-coordinate raster placement;
- pixel-within-cell positioning;
- GIF/APNG/image-file decoding or transcoding;
- audio or timeline synchronization;
- Terminal-owned scene/window/cell/damage/layout policy;
- terminal-authenticated passive existence queries;
- automatic replay/re-upload after generation loss;
- public Kitty image/frame ids or raw animation command dictionaries;
- PTY/ConPTY child-process hosting.

Frame composition remains a strong candidate for a later focused release after the 1.16 frame lifecycle is stable.

## Authorities

- [`Icod.Terminal-1.16.0-Development-Roadmap.md`](Icod.Terminal-1.16.0-Development-Roadmap.md)
- [`docs/superpowers/specs/2026-09-15-1.16.0-persistent-raster-animation-frame-lifecycle-design.md`](docs/superpowers/specs/2026-09-15-1.16.0-persistent-raster-animation-frame-lifecycle-design.md)
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)
- [`docs/Architecture.md`](docs/Architecture.md)
- [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md)
- [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md)

## 1.17 stable line — Terminal-owned Screen Output Contracts

Version 1.17 prepares the semantic and transactional Terminal boundary required for a later TermInfo-free DCurses 2.0 package.

The governing rule is:

> Terminal owns terminal dimensions, semantic terminal-profile interpretation, safe screen-operation planning, and serialized output commitment; higher layers own retained cells, layout, damage, and refresh policy.

The tranche sequence is:

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

Authorities:

- [`Icod.Terminal-1.17.0-Development-Roadmap.md`](Icod.Terminal-1.17.0-Development-Roadmap.md)
- [`docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md`](docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md)
- [`docs/superpowers/plans/2026-09-17-1.17.0-terminal-screen-output.md`](docs/superpowers/plans/2026-09-17-1.17.0-terminal-screen-output.md)

## 1.18 stable line — Unknown-rendition Baseline Recovery

Version 1.18 is a focused additive release that closes the sole blocking Terminal contract found by the DCurses 2.0 readiness gate.

The governing rule is:

> Terminal may claim a rendition baseline only when it can unconditionally restore every rendition axis exposed by the selected profile that Terminal can enter; otherwise no plan is available.

The release adds one public planner method:

```csharp
public TerminalScreenOperationPlan? PlanRenditionBaseline();
```

The plan represents unknown physical rendition state, restores attributes before original colors in a safe deterministic order, retains Terminal-owned expansion/padding/cost and same-session transaction ownership, and returns a valid zero-byte plan only when the selected profile exposes no enterable attribute and no selectable color axis. Existing known-state reset and transition behavior remains unchanged.

T180-T185 are accepted. The stable candidate at `56bbc011325e5c88e67f243a9b882b97bae9aac7` passed the complete Windows/Linux/macOS, package, API/XML, downstream, and artifact matrix in workflow run `35382158657`. PR #63 is merged and the release is tagged `v1.18.0`; the versioned roadmap preserves the original candidate-qualification evidence.

The tranche sequence is:

```text
T180  contract freeze, reference snapshot, API-regret gate, and alpha identity
T181  unknown-state rendition-baseline planner and focused red-green tests
T182  capability ordering, padding, cost, zero-byte, and nullability hardening
T183  session ownership, transaction commitment, and adversarial qualification
T184  package/API/XML/docs and DCurses 2.0 package-only acceptance
T185  release-candidate qualification and stable 1.18.0 closure
```

Authorities:

- [`Icod.Terminal-1.18.0-Development-Roadmap.md`](Icod.Terminal-1.18.0-Development-Roadmap.md)
- [`docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md`](docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md)
- [`docs/superpowers/plans/2026-09-18-1.18.0-rendition-baseline.md`](docs/superpowers/plans/2026-09-18-1.18.0-rendition-baseline.md)
- [`Icod.DCurses 2.0 PR #32`](https://github.com/uniblab/Icod.DCurses/pull/32)

## 1.19 released line — Downstream Screen-output Hardening and Planner Expansion

The completed 1.19 release scope was **Option 1 + Option 2 + Option 4 + Option 10**:

| Option | Selected area | Intended result |
| --- | --- | --- |
| 1 | Downstream hardening | Execute representative Terminal-only renderer workloads against the candidate package and preserve DCurses compatibility. |
| 2 | Semantic planner expansion | Broaden safe capability-backed screen planning, with deterministic cost selection and explicit unavailable results. |
| 4 | Transaction model hardening | Qualify output epochs, bounded retention, cancellation, serialization, mixed-output cleanup, and failure reporting. |
| 10 | Documentation and samples | Provide a runnable screen-output sample and a practical consumer guide covering planning, commitment, and recovery. |

The governing rule is:

> Terminal owns capability interpretation, opaque semantic plans, and serialized output commitment; consumers own physical-state assumptions, retained cells, layout, damage, and refresh policy.

The implementation sequence is:

```text
T190  baseline, downstream gap inventory, contract/API review, development identity
T191  executable package-only downstream screen-output witness
T192  capability-backed planner expansion and deterministic fallback selection
T193  planner safety, rendition, padding, cost, and bound qualification
T194  transaction admission, lifetime, output-epoch, and capacity hardening
T195  mixed-output cancellation, serialization, cleanup, and failure qualification
T196  screen-output consumer guide and runnable sample
T197  package/API/XML/dependency and downstream qualification
T198  cross-platform regression, bounded soak, and independent review
T199  stable 1.19.0 release closure and evidence record
```

T190-T199 are complete and 1.19.0 is released. The final pre-merge head passed all nine CI jobs, including 2,402 unit tests and 15 integration tests per framework on each supported PR platform. The planner admits home-plus-relative and same-row carriage-return-plus-relative routes without public API additions; malformed optional routes cannot displace valid alternatives. Cursor-visibility composition is deferred to preserve presentation-lease ownership. The executed package harness covers the Terminal-only renderer, the screen-output sample, and published DCurses 2.2.0 while retaining the separate 1.6.0 compatibility witness. The release also includes the expanded samples, Ken Arnold attribution, and TermInfo/Inspection 1.16.0 dependency refresh.

Authority: [`Icod.Terminal-1.19.0-Development-Roadmap.md`](Icod.Terminal-1.19.0-Development-Roadmap.md).

## 1.20 released line — Profile and Capability Decisions

The selected scope is **3 + focused 6 + 10**:

| Option | Selected area | Intended result |
| --- | --- | --- |
| 3 | Terminal-profile refinement | Add immutable semantic advertisement facts for existing cursor, erase, character/line-shift, and scroll-region planning without exposing TermInfo or promising parameter-independent plan success. |
| focused 6 | Existing capability-verification paths | Qualify `KeyboardReporting`, `RasterGraphics`, and `PersistentRasterGraphics` through the current bounded query/input authority, preserving uncertainty, endpoint separation, and lifecycle evidence. |
| 10 | Documentation and samples | Reconcile the twelve-capability/three-probe inventory and execute the actual capability sample, including static facts, concrete planning, explicit verification, and headless help. |

The governing rule is:

> A profile describes static advertisement; a planner answers a particular request; a session reports current evidence and endpoint availability. None of those alone guarantees physical terminal execution.

The implementation sequence is:

```text
T200  baseline, contract/source mapping, API review, development identity
T201  immutable screen-profile advertisement surface
T202  advertisement/planning distinction and partial-profile qualification
T203  existing verification-path and no-probe contract matrix
T204  deadlines, cancellation, malformed and late-response qualification
T205  lifecycle generations, endpoint availability, concurrent ownership
T206  consumer guide and executed capability sample
T207  fresh-package/API/XML/dependency and downstream acceptance
T208  cross-platform regression, review, and scope reconciliation
T209  stable 1.20.0 release closure and exact evidence
```

T200-T209 are complete; PR #65 has been merged and 1.20.0 published. The stable 1.20.0 candidate package passed the Windows/Linux/macOS runtime matrix and all four package shards. The additive API, generation and late-response fixes, actual capability sample, documentation, and exact source/artifact evidence are recorded in the [accepted release checkpoint](Icod.Terminal-1.20.0-Development-Roadmap.md#stable-candidate-qualification). Existing no-probe capability cases remain no-probe: this selection does not authorize a new query family, router redesign, or background discovery.

Authorities:

- [1.20 development roadmap](Icod.Terminal-1.20.0-Development-Roadmap.md)
- [1.20 profile/capability design](docs/superpowers/specs/2026-09-27-1.20.0-profile-capability-design.md)

## 1.21 development line — Rich Input and Cursor Visibility

The selected scope is **Rich input and keyboard expansion + Cursor-visibility composition with screen transactions**:

| Area | Intended result |
| --- | --- |
| Rich input and keyboard expansion | Close at least one demonstrated consumer gap in semantic input decoding or event projection, with bounded framing, traditional fallback, and the existing single-reader/query authority. Freeze the precise addition against a failing downstream fixture before implementation; existing Kitty phases, associated text, mouse, focus, and paste are the baseline. |
| Cursor-visibility composition with screen transactions | Add an opt-in temporary visibility scope around one committed screen frame; restore the presentation manager's effective lease owner or ordinary capability, including best-effort cleanup after failure. Persistent visibility remains lease-owned. |

The governing rule is:

> The session owns a single input stream and truthful terminal state; a transaction may temporarily present a cursor for one output frame but cannot claim a permanent visibility owner or undo emitted bytes.

The implementation sequence is:

```text
T2100  baseline, input consumer gap, output ownership map, API freeze
T2101  input contract and failing adversarial fixtures
T2102  bounded decoding and semantic normalization
T2103  reporting leases, lifecycle, fallback, and input ownership
T2104  package-backed downstream input witness
T2105  temporary visibility contract and precommit validation
T2106  visibility commitment, presentation composition, cleanup
T2107  lease/transaction/lifecycle adversarial matrix
T2108  guides and executed input-driven screen-output example
T2109  package/API/XML/dependency/downstream qualification
T2110  cross-platform review, stable 1.21.0 closure and evidence
```

The [1.21 development roadmap](Icod.Terminal-1.21.0-Development-Roadmap.md) specifies acceptance per task, package checks, failure semantics, and deferred work. Its implementation evidence identifies the red fixtures, exact-head Windows/Linux/macOS runtime and package qualification, and candidate package/symbol hashes in PR #66.

## 1.22 development line — Bounded Animation Frame Composition

The selected scope is **Option 1 + Option 10**: compose a rectangular pixel region from one known animation frame into another known frame belonging to the same current persistent raster resource, then demonstrate it through an executable sample and a downstream consumer witness. The existing `TerminalRasterAnimation` controller and opaque frame tokens remain the public ownership boundary. The protocol-specific Kitty `a=c` operation remains private.

The governing rule is:

> Terminal owns frame identity, bounded pixel geometry validation, protocol commitment, acknowledgement, and lifecycle certainty; callers own source art, screen placement, timing decisions, and scene composition.

T2200–T2210 cover the protocol/API review, TermInfo 1.17.0 dependency qualification, failing fixtures, semantic composition contract, private encoding and query correlation, failure/lifecycle hardening, sample and downstream acceptance, package/API/XML/security checks, and exact-head stable release closure. The implementation is under test in [PR #67](https://github.com/uniblab/Icod.Terminal/pull/67); see the [1.22 development roadmap](Icod.Terminal-1.22.0-Development-Roadmap.md) for acceptance criteria and evidence. Publication remains separate.

## 1.23 development line — Partial Frame Transfer and Rich-Input Completion

The selected scope is **Option 2 + Option 9 + Option 10**. Option 2 adds a bounded caller-supplied update to an existing known animation frame. Option 9 completes the remaining protocol-neutral rich-input and keyboard contract. Option 10 supplies samples, downstream package witnesses, documentation, measurement, and release qualification.

The approved [1.23 design](docs/superpowers/specs/2026-10-02-1.23.0-partial-frame-rich-input-design.md) and [1.23 development roadmap](Icod.Terminal-1.23.0-Development-Roadmap.md) govern implementation in [PR #68](https://github.com/uniblab/Icod.Terminal/pull/68).

The governing rule is:

> Terminal owns bounded pixel transfer, input framing, event decoding, correlation, leases, lifecycle, and cleanup; callers own source data, placement, layout, keymaps, editing, damage, and presentation policy.

The 1.23 work remains additive to 1.22. It does not add gapless scheduling, absolute or pixel-within-cell placement, hidden replay, raw protocol extensibility, image codecs, PTY hosting, or a second input reader.

## 1.24 development line — Raster Geometry and Planning Contracts

The selected 1.24 scope publishes the geometry, bounded-capacity, and operation-evidence contracts that a later `Icod.DCurses` tile renderer needs without moving tile maps, viewports, damage, asset policy, or game rules into Terminal.

The governing rule is:

> Terminal owns live pixel-geometry queries, exact derivation, resource geometry, bounded local raster ownership, operation evidence, protocol execution, acknowledgement, lifecycle, and cleanup. DCurses owns tile-cell mapping, clipping, damage, viewport state, refresh policy, and frame-selection strategy. The game owns maps, actors, visibility, animation policy, and rules.

The 1.24 closure did not justify a bounded frame-edit execution release. The distinct 1.25 screen raster transaction scope is documented below. Frame-edit batching remains a separate measurement-gated candidate. No batch API, Indexed8 partial-update promise, remote atomicity, rollback, or hidden replay is precommitted.

The [1.24 design](docs/superpowers/specs/2026-10-02-1.24.0-raster-geometry-planning-design.md), [implementation plan](docs/superpowers/plans/2026-10-02-1.24.0-raster-geometry-planning.md), and [1.24 development roadmap](Icod.Terminal-1.24.0-Development-Roadmap.md) record the published release from [PR #69](https://github.com/uniblab/Icod.Terminal/pull/69).

The implementation sequence is:

```text
T2400  published 1.23 baseline, consumer requirements, and API-regret gate
T2401  public pixel-geometry contract and RED fixtures
T2402  semantic terminal/cell pixel-geometry acquisition and exact derivation
T2403  immutable persistent-raster resource geometry
T2404  raster limits and advisory local-capacity planning snapshot
T2405  generation-scoped raster-operation evidence
T2406  lifecycle, concurrency, cancellation, and failure hardening
T2407  tile-atlas planning, double-buffer, and measurement witness
T2408  samples, consumer guide, package/API/XML/security qualification
T2409  downstream and cross-platform exact-head qualification
T2410  stable 1.24.0 closure and evidence record
```

## 1.25 development line — Ordered Screen Raster Transactions

The selected 1.25 scope lets a terminal application order a complete immediate raster with ordinary cursor, text, rendition, and presentation operations in one session-bound screen-output transaction. It extends the existing `RasterGraphics` route: a verified ordinary Kitty backend is preferred and verified Sixel is used when Kitty is unavailable. Persistent Kitty resources and placeholders remain a separate capability and API.

The governing rule is:

> Terminal owns raster backend evidence, protocol-private encoding, bounded transaction preflight, serialized output, and commitment uncertainty. DCurses owns raster-surface placement, source-image provision, clipping, overlay policy, damage, and refresh. Applications own assets and viewport content.

The [1.25 design](docs/superpowers/specs/2026-10-03-1.25.0-screen-raster-transactions-design.md), [implementation plan](docs/superpowers/plans/2026-10-03-1.25.0-screen-raster-transactions.md), and [1.25 development roadmap](Icod.Terminal-1.25.0-Development-Roadmap.md) define the review gates. [Merged PR #71](https://github.com/uniblab/Icod.Terminal/pull/71) introduced the alpha API, tests, sample, package verifier and release documentation. Follow-up PRs #73–#77 are merged; alpha.5 is published. Further graphics development and Kitty live qualification are now on hold. Stable source qualification completed in merged PR #78 with those limits retained; publication remains a separate maintainer action.

```text
T2500  published baseline, placement assumptions, and API-regret gate
T2501  failing transaction/capability fixtures
T2502  bounded Kitty/Sixel payload preparation
T2503  ordered transaction emission and cursor-state contract
T2504  capability, lifecycle, concurrency, and failure hardening
T2505  public-only sample and downstream DCurses acceptance
T2506  documentation, package, API, and security gates
T2507  Windows/Linux/macOS exact-head qualification
T2508  stable 1.25.0 closure and evidence record
```

This feature permits a caller-supplied complete viewport image per repaint without a Terminal-side source cache. It makes no claim that Sixel supports persistent identity, sparse frame edits, portable clipping or terminal-independent post-display cursor behavior. Real terminal acceptance must qualify any stronger visual claim separately.

## 1.26 released line — Terminal Compatibility Qualification

The delivered post-1.25 feature set is **Versioned compatibility matrix and executable acceptance sample**. It provides a public-only sample and a deterministic, evidence-backed matrix with lanes for Windows Terminal, Apple Terminal, iTerm2, Kitty, WezTerm, Ghostty, Alacritty, GNOME Terminal/VTE, Konsole, and XTerm. Lane inclusion is not a support claim; initial accepted evidence covers only identity and dimensions in the two recorded environments. VS Code is an additional OSC 633 integration lane; WSL, ConPTY, SSH, tmux, and similar layers are recorded as exact transport identities.

The historical 1.26 selection menu was (the current decisions are recorded under 1.27 below):

| Option | Feature or feature set | Decision |
| --- | --- | --- |
| 1 | Versioned compatibility matrix and executable acceptance sample | **Selected for 1.26.0** |
| 2 | Appearance queries and change events, including reviewed DEC 2031 behavior | Deferred |
| 3 | Negotiated in-band resize notifications through DEC private mode 2048 | Deferred |
| 4 | MIME-aware, permission-reporting OSC 5522 clipboard support | Deferred |
| 5 | Selected typed OSC 21 color control and observation | Deferred |
| 6 | OSC 1337 `ReportCellSize` as an additional geometry-query backend | Deferred |

None of these options inherently requires an Icod.TermInfo API or parser change. The 1.26 plan keeps `Icod.TermInfo 1.17.0`; a separate TermInfo change is justified only if qualification proves a concrete built-in profile or capability-data defect.

The governing rule is:

> Compatibility claims identify the exact library source/package, terminal, operating system, transport, scenario revision, and observation. Silence, branding, documentation, or successful byte emission cannot become a terminal-behavior claim.

The planned tranche sequence is:

```text
T2600  stable baseline, scope, scenario identities, and API-regret gate
T2601  bounded evidence schema and deterministic matrix renderer
T2602  public-only acceptance sample shell and headless contract
T2603  identity and bounded-query scenarios
T2604  metadata, prompt, and presentation scenarios
T2605  notification, progress, and clipboard scenarios
T2606  input and lifecycle scenarios
T2607  atomic report writing, privacy, and failure hardening
T2608  documentation, fresh-package, API, and matrix gates
T2609  reviewed live-terminal qualification
T2610  cross-platform qualification and stable release closure
```

The [1.26 design](docs/superpowers/specs/2026-10-04-1.26.0-terminal-compatibility-qualification-design.md), [implementation plan](docs/superpowers/plans/2026-10-04-1.26.0-terminal-compatibility-qualification.md), and [versioned development roadmap](Icod.Terminal-1.26.0-Development-Roadmap.md) define the full evidence model, scenario groups, privacy rules, failure semantics, and release gates. The published 1.26 feature adds no production runtime API and preserves the graphics-development hold. Remaining live-terminal coverage is a continuing qualification backlog, not an unimplemented runtime feature.

## 1.27 planned line — Live Terminal Environment Awareness

The maintainer selected **options 2 + 3: terminal appearance and resize awareness** on 2026-10-05. This documentation-only planning PR does not change package versions, public API, runtime code, dependencies, or the graphics hold.

The current full nongraphics feature menu preserves the original option numbers:

| Option | Feature or feature set | Current decision |
| --- | --- | --- |
| 1 | Compatibility qualification expansion | Infrastructure shipped in 1.26; focused new-feature acceptance accompanies 1.27, broader coverage remains ongoing |
| 2 | Appearance queries and change events, including reviewed DEC mode 2031 | **Selected for 1.27.0** |
| 3 | Negotiated in-band resize notifications through DEC private mode 2048 | **Selected for 1.27.0** |
| 4 | MIME-aware, permission-reporting OSC 5522 clipboard support | Deferred |
| 5 | Selected typed OSC 21 color control and observation | Deferred |
| 6 | OSC 1337 ReportCellSize as another geometry-query backend | Deferred |
| 7 | Demonstrated input and lifecycle gaps/hardening | Independent future scope; selected-feature hardening is included in 1.27 |
| 8 | Further profile, planner and capability expansion | Deferred until a concrete consumer gap justifies it |
| 9 | Transport/intermediary compatibility and controlled forwarding | Deferred; exact transport identities remain part of acceptance |
| 10 | Operational and shell-integration expansion | Deferred; existing protocols remain regression coverage |
| 11 | Privacy-safe diagnostics and performance qualification | Independent future scope; boundedness tests are included in 1.27 |
| 12 | Controlled public protocol extensibility | Deferred pending separate architecture and security review |

The governing rule is:

> Terminal owns bounded appearance/resize observation, negotiated reporting, input/query ownership, serialized mode changes, and cleanup. Applications own themes, layout, and repaint policy. Native observations and terminal-reported facts retain their distinct provenance.

The release supplies an explicit appearance query plus independently opt-in appearance and in-band resize reporting lifetimes. It preserves native resize behavior, existing synchronous dimensions APIs, the stable 1.x API floor, and the single authoritative input reader. It does not infer support from branding, silence, or a successful write.

No TermInfo change is planned: retain `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`. A concrete capability-data defect, if found, needs its own reviewed scope.

The tranche sequence is:

```text
T2700  published baseline, detailed design, and API-regret gate
T2701  bounded protocol parsing and routing fixtures
T2702  one-shot appearance observation
T2703  appearance reporting ownership
T2704  in-band resize observations
T2705  negotiated resize reporting ownership
T2706  cross-feature lifecycle and concurrency hardening
T2707  public-only sample and compatibility scenarios
T2708  package, API, documentation, and regression qualification
T2709  focused live-terminal acceptance
T2710  stable release closure and exact evidence
```

The [1.27 development roadmap](Icod.Terminal-1.27.0-Development-Roadmap.md) defines scope, protocol references, ownership requirements, integration points, acceptance criteria and release gates. The [T2700 detailed design](docs/superpowers/specs/2026-10-05-terminal-appearance-resize-awareness-design.md) freezes the proposed public signatures and runtime semantics for approval before implementation planning and runtime changes. Implementation remains inline without subagents.

Stable acceptance requires positive live evidence for both selected features and evidence that unavailable reporting preserves the existing path. Ten-emulator completion is not implied. Curated notes, version metadata, packed README and required package-policy links are checked together before the stable tag.

## Later development candidates

**Graphics hold:** The graphics candidates below are deferred, including frame-edit batching, Indexed8 regional parity, gapless frames, placement expansion and image codecs. No Kitty workaround, new backend replay or additional probe is authorized by this hold. Existing APIs remain available; unrelated input, lifecycle, profile and operational work may be considered under a separate future scope decision. See [the hold and reopening criteria](docs/Graphics-Development-Hold.md).

The deferred bounded frame-edit execution candidate may combine prevalidated region edits and final frame selection, and may add Indexed8 regional-transfer parity, only when a real DCurses workload demonstrates a material benefit. It must not claim remote atomicity or rollback.

Options 2 and 3 are now selected for 1.27 rather than deferred. Options 4–12 remain separate decisions except for narrowly scoped regression/acceptance work needed by the selected features. Graphics candidates still include gapless intermediate frames, absolute screen-coordinate placement, pixel-within-cell positioning, richer terminal-side reconciliation only if a truthful non-destructive primitive exists, and image-file decoding/transcoding. PTY/ConPTY process hosting belongs in the adjacent Icod.Pty project, not a second Terminal runtime implementation. Existing support remains part of regression qualification.

Scene/window/cell ownership and hidden source-raster replay caches remain intentionally outside the Terminal contract.
