# Icod.Terminal Development Roadmap

- **Project:** `Icod.Terminal`
- **Package:** `Icod.Terminal`
- **Language:** C# 13
- **Target frameworks:** `net8.0`; `net9.0`; `net10.0`
- **Current published feature line:** `1.20.0` — Profile and capability decisions
- **Previous patch line:** `1.17.1` — Packaged README and release metadata correction
- **Development status:** 1.20.0 merged and published; 1.21.0 stable release preparation in PR #66
- **Active development target:** `1.21.0` — Rich input and cursor visibility
- **Selected scope:** Rich input and keyboard expansion + Cursor-visibility composition with screen transactions
- **Stable compatibility floor:** `1.0.0`

## Purpose

This file is the concise entry point for current `Icod.Terminal` development and long-range planning. Detailed design evidence remains in versioned roadmaps, release notes, public-API baselines, accepted CI checkpoints, and approved design/implementation documents.

The original pre-1.0 roadmap is preserved at [`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md).

## Latest accepted checkpoint

`Icod.Terminal 1.20.0` was merged through [PR #65](https://github.com/uniblab/Icod.Terminal/pull/65) at `8aa6d0543a3d48d6ec28c84f930da35282703b4a`, tagged `v1.20.0`, and published as a [GitHub release](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.20.0) on 2026-09-27. The [1.20 development roadmap](Icod.Terminal-1.20.0-Development-Roadmap.md#stable-candidate-qualification) records its qualified pre-merge source, nine-job CI checkpoint, candidate package hashes, and three-framework API baseline; those candidate hashes are not asserted as the published artifact's hashes. Its selected 3 + focused 6 + 10 scope is complete.

Version 1.21.0 selects **Rich input and keyboard expansion + Cursor-visibility composition with screen transactions**. The [1.21 development roadmap](Icod.Terminal-1.21.0-Development-Roadmap.md) defines T2100-T2110 and records the Kitty functional-key fixture, frame visibility ownership and cleanup, and package/downstream acceptance. The stable candidate is under qualification in PR #66; it has not been merged or published.

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
- `Icod.Terminal` owns the live terminal conversation, input/query/event authority, semantic capability evidence and routing, terminal output, ephemeral raster routing, persistent raster resource/placement/placeholder ownership, animation/frame lifecycle, protocol-private encoding/identity, lifecycle certainty, and deterministic cleanup.
- `Icod.DCurses` owns cells, windows, virtual-screen state, screen coordinates, clipping, scrolling, layout, refresh/diff policy, damage, and higher-level presentation policy.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

The production dependency graph retained from 1.20.0 for the planned 1.21.0 target is:

```text
Icod.TermInfo 1.16.0
Icod.Timing   1.0.0
```

Optional integration tests/samples may use `Icod.TermInfo.Inspection 1.16.0`; Inspection and Source remain outside the production package graph.

## Qualified stable sequence through 1.20.0

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
1.21.0  rich input/keyboard and cursor visibility composition     CANDIDATE
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

The [1.21 development roadmap](Icod.Terminal-1.21.0-Development-Roadmap.md) specifies acceptance per task, package checks, failure semantics, and deferred work. Its implementation evidence identifies the red fixtures and the exact-head runtime/package checkpoints. Stable closure requires the final Windows/Linux/macOS and package matrix in PR #66.

## Later development candidates

Beyond the selected 1.21 scope, independent candidates still include animation-frame composition, absolute screen-coordinate placement, pixel-within-cell positioning, richer terminal-side reconciliation only if a truthful non-destructive primitive exists, image-file decoding/transcoding, and PTY/ConPTY process hosting. Further profile expansion beyond the reviewed 1.20 surface, new query families or broader query architecture, operational-protocol expansion, endpoint/transport expansion, and public extensibility remain separate release decisions. Existing support in those areas remains part of regression qualification.

Scene/window/cell ownership and hidden source-raster replay caches remain intentionally outside the Terminal contract.
