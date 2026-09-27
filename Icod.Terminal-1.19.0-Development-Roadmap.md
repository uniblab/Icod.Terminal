# Icod.Terminal 1.19.0 Development Roadmap and Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this roadmap task by task. Track progress with the checkboxes below and record evidence before accepting a tranche.

**Goal:** Qualify the Terminal-only screen-consumer boundary through downstream hardening, semantic planner expansion, transaction hardening, and executable documentation.

**Architecture:** Extend the existing session-bound `TerminalScreenPlanner` and `TerminalScreenOutputTransaction`. Keep capability interpretation and output ownership inside Terminal, and exercise the same public contract from a fresh package consumer. DCurses and applications retain cells, layout, clipping, damage, and refresh policy.

**Tech stack:** C# 13; .NET `net8.0`, `net9.0`, `net10.0`; xUnit; repository PowerShell build/package tooling.

**Spec:** [Selected 1.19 scope in the main roadmap](Icod.Terminal-Development-Roadmap.md#119-development-line--downstream-screen-output-hardening-and-planner-expansion), together with the scope and constraints below.

**Status:** T190-T199 accepted for the qualified stable candidate recorded below, with the approved pre-publication documentation/sample follow-up described next. Candidate identity: `1.19.0`; not published. PR #64 records the latest head's validation separately from the historical artifacts below.

**Baseline:** Released `1.18.0`, tag `v1.18.0`, merge commit `3e150377db990141aa6903631a8b95cb2c41116e`.

## Selected scope

The release follows **Option 1 + Option 2 + Option 4 + Option 10** from the release discussion.

| Option | Workstream | Completion evidence |
| --- | --- | --- |
| 1 | Downstream hardening | An executed package-only renderer witness, representative DCurses workload coverage, and retained legacy compatibility. |
| 2 | Semantic planner expansion | Additional safe cursor-planning routes, exact-byte/cost tests, and an explicit ownership decision on cursor-visibility composition. |
| 4 | Transaction model hardening | Deterministic admission, epoch, cancellation, concurrency, bounds, and cleanup/failure tests. |
| 10 | Documentation and samples | A runnable screen-output sample, automated smoke mode, and a consumer guide that matches tested behavior. |

This release improves the existing screen-output contract. It does not establish a new rendering framework or a general protocol-extension API.

## Global constraints

- Language: C# 13. Target frameworks: `net8.0`; `net9.0`; `net10.0`.
- Stable compatibility floor: `1.0.0`. Preserve existing public signatures, enum values, and documented ownership semantics.
- Production dependencies remain `Icod.TermInfo 1.15.0` and `Icod.Timing 1.0.0`; optional integration fixtures may use `Icod.TermInfo.Inspection 1.15.0`. Any dependency change needs its own evidence and review.
- No TermInfo types, raw capability identifiers, expansion programs, or control strings in new public screen contracts. No direct TermInfo reference in the Terminal-only downstream consumer.
- Planning is side-effect free. Plans stay opaque and session-bound; unsupported operations return unavailable results rather than guessed escape sequences.
- A single session retains input/query/event authority and serialized output authority. No second renderer-owned output path.
- Preserve the transaction's existing 65,536 retained-item and 64 MiB payload limits, and the planner's 1,048,576-character repeated-source limit unless a separately reviewed change demonstrates a need.
- Preserve the distinction between unavailable plans and valid zero-byte plans, and between known-state rendition transitions and unknown-state baseline recovery.
- Consumer code owns dimensions-dependent layout decisions, physical-state assumptions, retained cells, Unicode width, repaint policy, and recovery after uncertainty.
- Use repository C#/.NET and PowerShell tooling; maintain Windows PowerShell 5.1 compatibility for applicable packaging scripts.
- Keep historical release evidence and API baselines intact. Do not mark a tranche accepted until its executable evidence exists.

## Review focus

| Risk or input | Required behavior | Owning tranche |
| --- | --- | --- |
| Partial capabilities or malformed/oversized expansions | No unsafe fallback, overflow, unbounded expansion, or planning side effects. | T192-T193 |
| Unknown physical rendition and a missing reset axis | `PlanRenditionBaseline()` remains all-or-nothing; no false claim of restored state. | T193 |
| Resize, suspend/resume, intervening output, or disposal between creation and commit | Existing output-epoch/lifetime rules remain truthful and reject stale work before emission where required. | T194 |
| Cancellation and transport failure during mixed hyperlink/synchronized/raster output | Respect the commitment boundary; attempt required cleanup and retain ordered primary/cleanup failures. | T195 |
| Compile-only consumers or samples that assume unsupported features | Execute the renderer path; demonstrate graceful unavailable handling and recovery without raw TermInfo access. | T191, T196-T197 |

## Starting evidence and design decisions

### Approved pre-publication documentation and sample follow-up

On 2026-09-27 the maintainer approved five focused improvements plus matching TermInfo's Ken Arnold attribution:

- Refresh the root README's current API fingerprint and documentation links, and teach baseline/cursor/text composition in one complete transaction example.
- Give the screen sample a dedicated walkthrough, help, alternate-screen presentation ownership, and explicit q/Q/Escape/EOF exit behavior.
- Remove the unused mandatory dimension query from its fixed-origin frame. A package fixture reproduced the old failure with unavailable dimensions before this correction.
- Replace the raster-placeholder sample's TermInfo expansion/raw terminal-string output with semantic cursor-and-cell transactions; update its source guard and walkthrough.
- Expose a controlled `--recovery` demonstration of deliberate stale-work rejection and a fresh frame. Extend the package harness to execute the actual recovery and presentation/event routines, including missing plans, cancellation, and committed failures. Key-input fixtures stay open so a missing q/Escape handler cannot pass by falling through to EOF. The event loop explicitly handles `TerminalEventKind.Cancelled`.
- Add “Ken Arnold, for his work on `termcap` and `curses`” using the attribution in Icod.TermInfo's root README.

The sample still has no automatic transport-failure retry or retained-screen engine. The library API and production implementation remain unchanged. The first extended package run passed all three frameworks after the dimension and cancellation corrections; final rebuilt-package and exact-head workflow results are maintained in [PR #64](https://github.com/uniblab/Icod.Terminal/pull/64). The hashes below identify their stated earlier checkpoints, not artifacts rebuilt from this follow-up.

### Implementation and review checkpoint

- Alpha source head `aa94ab15a81c6b2d67d74db50ce42ce3a5513273` passed all nine jobs in [workflow 36289477943](https://github.com/uniblab/Icod.Terminal/actions/runs/36289477943): Windows/Linux/macOS runtime, candidate package, all four package shards, and validated artifact. This is the alpha checkpoint, not evidence for later review fixes or final stable metadata.
- The original new cursor tests produced five expected failures, then passed with the route expansion. The downstream example's missing-baseline assertion and sample's frame-output assertion were both observed failing before their corrections.
- Independent review found invalid optional capability data could discard a valid route. Six regression cases reproduced padding, source-length, parameter-format/evaluation, and independent-route failures; candidate-local handling now preserves valid alternatives. Full unit suites passed 2,402 tests on each framework after the fix.
- Actual resize signals preserve a pending transaction when no output ownership is acquired. External resume and suspend/resume presentation restoration invalidate pending transactions; fresh transactions then succeed. These three new qualification cases pass without changing production transaction code.
- Fresh-package downstream checks exercise the controlled renderer, sample frame, DCurses 1.6.0 ownership soak, and DCurses 2.2.0 text/rendition/resize/repaint. The 2.2.0 witness runs 16 resize/repaint iterations and injects committed write failure every fourth iteration, then explicitly recovers.
- The API fingerprint remains unchanged on all frameworks. A temporary all-zero fingerprint was correctly rejected by the real verifier. Production dependencies remain unchanged; license verification passes.
- The sample smoke path is hosted by the package verifier rather than a sample `--smoke` switch, preserving the Terminal-only sample source boundary. The sample demonstrates a single frame and unsupported mandatory plans; exceptions propagate to the host. A larger sample-owned recovery loop remains deferred; the guide and downstream harness cover caller-driven recovery.
- Local package scripts use a temporary single-process invocation wrapper because this environment restricts multi-node MSBuild IPC. CI runs the repository scripts normally. Local staging package hashes are not represented as CI artifact hashes. The PR matrix covers Windows/Linux/macOS; this checkpoint does not claim a new six-runner x64/ARM64 distribution run.

### Stable candidate qualification

The stable source head is `d7e2906e574f6d1b8024626828a46718d52c749b`. [PR workflow #2007 / run 36290312908](https://github.com/uniblab/Icod.Terminal/actions/runs/36290312908) passed all nine jobs against GitHub's PR merge commit `12b451af8d4220db300ba7d6fb08b4c3045d1079`, also recorded in the package's repository metadata. Windows, Linux, and macOS each passed 2,402 unit tests and 15 integration tests on each of `net8.0`, `net9.0`, and `net10.0`, with zero failures or skips. This record is a later documentation update and does not describe its own head as the source of these artifacts.

| Job | Identifier | Result |
| --- | --- | --- |
| Package candidate | `108539002665` | Passed |
| Runtime Linux | `108539002789` | Passed |
| Runtime macOS | `108539002806` | Passed |
| Runtime Windows | `108539002838` | Passed |
| Package Foundation | `108539116345` | Passed |
| Package Presentation | `108539116411` | Passed |
| Package Semantic and hardening | `108539116351` | Passed |
| Package Stable 1.x release line | `108539116322` | Passed |
| Validated package artifact | `108539371285` | Passed |

The candidate artifact is `10921924232` (`icod-terminal-pr-package-candidate`, archive SHA-256 `20d6acd4f4865a2c7bb036e9d6571b2124b41a8cf90c90998beac60ac002e1d8`). The validated copy is `10922345401` (`icod-terminal-pr-packages`, archive SHA-256 `f8566e05c35f6377949e73c15d6777c92a1fe72e09bd7616856612701b380abe`). The archives contain the same package files and API snapshots; archive hashes differ because the upload steps create separate ZIPs. These CI artifacts have seven-day retention.

Hashes measured from the downloaded CI candidate, not a local rebuild:

| File | SHA-256 |
| --- | --- |
| `Icod.Terminal.1.19.0.nupkg` | `1a4bb19b9ef2ece83200e14d05b65effdb7c8adbb19f6ce8f05b632f56dbfed1` |
| `Icod.Terminal.1.19.0.snupkg` | `8527f3755970343b47ffc81fc389230f9308d906bdfd4ef00ce1b7de294166c3` |
| Each of the three public API snapshots | `48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d` |

All four package shards consume the same candidate artifact. The release-line shard runs isolated restores of DCurses 1.6.0, the controlled Terminal-only renderer, DCurses 2.2.0, and the actual sample frame on all three frameworks. The legacy witness checks eight complete ownership cycles, exact mode apply/restore counts, ordered cleanup, and inert disposal of stale leases. The new renderer witness checks 16 resize/repaint iterations, with committed transport failure and recovery every fourth iteration. Existing transaction tests separately cover cancellation before admission and after commitment, noninterleaving, reservation release, and primary/cleanup failure ordering; concurrency/recovery waits in the hardening tests use five-second bounds. The downstream loops have fixed iteration counts and remain subject to the enclosing CI job timeout rather than a new per-loop wall-clock deadline. This is bounded ownership/recovery evidence, not a heap-growth benchmark or long-duration soak.

The qualification covers the existing three-OS PR matrix. A fresh six-runner x64/ARM64 distribution workflow, physical-terminal emulator certification, and performance benchmarking are not claimed. Cursor-visibility composition and a sample-owned automatic recovery loop remain deferred as described above. No merge, tag, GitHub Release, or package publication is part of this checkpoint.

T190 baseline: planning head `8d47c06a3a4260c0b0ad8c9ac7c1b5923fccd99c` passed the full PR workflow [36288163471](https://github.com/uniblab/Icod.Terminal/actions/runs/36288163471). Local Linux baseline execution passed 2,383 unit tests and 15 integration tests on each of net8.0, net9.0, and net10.0; a three-framework baseline package was built. Local multi-node MSBuild is restricted by the execution environment, so local commands use single-process builds; CI retains authority for cross-platform and package gates. The unchanged 1.18 API fingerprint is `48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d`.

The [T190 design record](docs/superpowers/specs/2026-09-26-1.19.0-screen-output-hardening-design.md) admits the two cursor routes and defers cursor visibility to preserve presentation-lease ownership. The decoupled consumer version is pinned to DCurses 2.2.0, whose source declares Terminal 1.18.0 as its sole production dependency; DCurses 1.6.0 remains a separate compatibility gate. A separate synthetic test harness supplies the legacy TermInfo-bearing provider setup while the controlled renderer consumer retains its source/reference boundary checks.

The existing `tools/dcurses-screen-contracts-acceptance/Source/Program.cs` exercises value contracts at entry, but its `FutureDcursesRenderer.RefreshAsync(...)` method is only compile-bound. Executing a representative refresh is therefore a concrete Option 1 deliverable. Its baseline handling also needs to demonstrate that a missing rendition baseline cannot establish known physical state.

`PlanCursorMove(TerminalScreenPosition? current, TerminalScreenPosition target)` already compares absolute addressing, home at the origin, separate row/column addressing, and known-position relative moves. Option 2 will add bounded home-plus-relative and same-row carriage-return-plus-horizontal candidates through this existing method. Selection must compare the complete expanded/padded cost; existing candidates win equal-cost ties to preserve deterministic behavior.

Cursor-visibility composition is a **design candidate**, not a frozen API promise. `TerminalProfile.Screen` exposes cursor visibility evidence, while `TerminalPresentationManager` already owns visibility through presentation leases. T190 must establish how a proposed `PlanCursorVisibility(TerminalCursorVisibility visibility)` can coexist with that owner before it is admitted. If safe composition would require a presentation/lifecycle redesign, explicitly defer this candidate; the cursor-route expansion remains the required Option 2 deliverable.

Existing transaction tests already cover many capacity, cancellation, serialization, and cleanup cases. T194-T195 extend the missing combinations and fix demonstrated defects; they do not duplicate passing tests or presume that the current implementation is broken.

## Tranche sequence and dependencies

| Tranche | Deliverable | Prerequisites |
| --- | --- | --- |
| T190 | Baseline, gap inventory, contract review, development identity | Planning accepted |
| T191 | Executable downstream package witness | T190 |
| T192 | Planner expansion | T190-T191 |
| T193 | Planner safety and cost qualification | T192 |
| T194 | Transaction admission and lifetime qualification | T190-T191 |
| T195 | Mixed-output commitment and cleanup qualification | T193-T194 |
| T196 | Consumer guide and runnable sample | T193-T195 |
| T197 | Package, public API, XML, and downstream acceptance | T196 |
| T198 | Cross-platform and bounded soak qualification | T197 |
| T199 | Stable release closure | T198 |

T194 can proceed independently of T192-T193 after the shared contract and witness are established. Each implementation tranche ends with a focused review and commit. Reuse existing regression tests when they already prove the required behavior.

## T190 — Baseline, gap inventory, and contract freeze

**Files:** `Directory.Build.props`; this roadmap; create `docs/superpowers/specs/2026-09-26-1.19.0-screen-output-hardening-design.md` during implementation planning. Review `docs/Architecture.md`, `docs/Compatibility-and-Versioning.md`, and the existing 1.17/1.18 design authorities.

**Interfaces:** Freeze the continued use of `TerminalSession.Screen`, `TerminalSession.CreateScreenOutputTransaction(...)`, opaque operation plans, and `CommitAsync(CancellationToken)`. Review any additive signature separately before implementation.

- [x] Record the baseline source SHA, three-framework API fingerprint, test counts, and existing package/CI evidence; run the current validation ladder before behavioral changes.
- [x] Build a matrix of real downstream refresh operations against existing tests, including baseline recovery, sparse capabilities, scrolling, glyph/rendition changes, hyperlinks, resize/resume, and cleanup. Pin the published DCurses version used for the new witness and record why it represents the decoupled consumer; retain the existing 1.6.0 compatibility check separately.
- [x] Specify exact output, unsupported behavior, cost/tie rules, bounds, and session ownership for the new cursor routes. Audit home and carriage-return semantics against the capability data and existing contracts; exclude routes whose preconditions cannot be proved.
- [x] Resolve the cursor-visibility candidate against presentation leases, restoration, and lock ordering. Record either a complete additive contract and tests or an explicit deferral; do not create a competing state owner.
- [x] Record the accepted design and API review, then begin implementation with `VersionPrefix=1.19.0` and `VersionSuffix=alpha.1`. Keep package metadata consistent with the development identity.

**Exit:** Reproducible baseline, finite gap list, reviewed expansion contract, and development identity. Scope expansion beyond the four options is a separate release decision.

## T191 — Executable downstream screen-output witness

**Files:** `tools/dcurses-screen-contracts-acceptance/Source/Program.cs`; its existing `.csproj`; `packaging/VerifyDCursesPackage.ps1`; add focused consumer fixtures under `tools/dcurses-screen-contracts-acceptance/Source/` as needed.

**Interfaces:** Consume the packaged `TerminalSession`, `TerminalProfile`, planner, and transaction APIs. Produce an invoked deterministic refresh/recovery witness with a nonzero exit code on assertion failure.

- [x] Replace compile-only acceptance with an invoked synthetic-session refresh using a recording transport and public Terminal APIs. Verify cursor/rendition/text/erase ordering and a second refresh after a deliberate state-invalidating event.
- [x] Add sparse-profile cases: missing baseline must stop a transition that assumes default state; unavailable optional erase or glyph operations must use an explicit consumer fallback. Assert exact bytes and no output on rejected work.
- [x] Restore and run the witness from the candidate NuGet package in the existing isolated temporary consumer directory on all three frameworks. Retain the guard against source-project and direct TermInfo references.
- [x] Add the pinned decoupled DCurses workload from T190 as a separate package acceptance path, covering initial presentation, text/rendition changes, resize, and close/recovery without removing the 1.6.0 compatibility witness.
- [x] Record baseline failures as specific regression cases for T192-T195; commit the executable witness and its verifier changes.

**Exit:** The test entry point executes the refresh path, and package evidence distinguishes the legacy consumer, decoupled DCurses workload, and Terminal-only witness.

## T192 — Semantic planner expansion

**Files:** `src/Screen/TerminalScreenPlanner.cs`; `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenPlannerCoreHardeningTests.cs`; `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenSemanticPlannerTests.cs`. Change `TerminalScreenContracts.cs` and presentation integration only if T190 admits the visibility addition.

**Interfaces:** Extend the existing `PlanCursorMove(TerminalScreenPosition? current, TerminalScreenPosition target)` without changing its signature; return the existing `TerminalScreenOperationPlan?`.

- [x] Add failing tests for an unknown current position where home plus relative movement reaches the target, and a known same-row position where carriage return plus horizontal movement is the cheapest safe route. Use synthetic capability markers to assert exact segment order and full `ByteCount`.
- [x] Test a missing required segment, zero-distance axes, a more expensive fallback, and an equal-cost tie. Assert unavailable results where no complete safe route exists, and preservation of existing-route preference on ties.
- [x] Run the focused tests and record the expected failures before implementing the bounded candidate composition with existing expansion/cost helpers.
- [x] If admitted in T190, implement the reviewed visibility contract with exact-byte, missing-capability, foreign-session, presentation-lease conflict, and cleanup/restoration tests. Otherwise retain the explicit deferral.
- [x] Run the planner tests and T191 witness, review the public API diff, and commit the expansion.

**Exit:** New routes are useful on capability-limited profiles, deterministic, side-effect free, bounded, and compatible with existing ownership.

## T193 — Planner safety, rendition, padding, and cost

**Files:** `src/Screen/TerminalScreenPlanner.cs`; existing `TerminalScreenPlannerCoreHardeningTests.cs`, `TerminalScreenEditingPlannerHardeningTests.cs`, and `TerminalScreenRenditionPlannerHardeningTests.cs` under `tests/Icod.Terminal.Tests/src/Screen/`.

**Interfaces:** Preserve `NormalizeRendition(...)`, `PlanRenditionBaseline()`, known-state transition/reset, erase/shift/scroll planners, `ByteCount`, and `AffectedLines`.

- [x] Extend the existing capability matrix with absent/partial capabilities, padding-sensitive routes, non-ASCII encoded output, invalid counts/regions, and maximum-coordinate or expansion-bound cases for the new paths.
- [x] Verify cost against emitted bytes, stable candidate ordering, correct affected-line accounting, and repeated-call stability. Assert that rejected candidates produce no writes, flushes, output epochs, or lifecycle changes.
- [x] Retain and extend the 1.18 baseline matrix: attribute-only, color-only, complete specific exits, incomplete recovery, and valid zero-byte plans. Add combined downstream cases only where current tests leave a gap.
- [x] For each demonstrated defect, record a failing focused test, make the smallest correction, and run the screen suites plus TermInfo integration tests. Commit the qualified planner changes.

**Exit:** Every admitted planner route has truthful availability, safe recovery semantics, and bounded expansion/cost evidence.

## T194 — Transaction admission, lifetime, and capacity

**Files:** `src/Screen/TerminalScreenOutputTransaction.cs`; `src/Session/TerminalSession.Screen.cs`; `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenOutputTransactionHardeningTests.cs`.

**Interfaces:** Preserve `Add(...)`, `WriteText(...)`, `WriteHyperlink(...)`, raster-placeholder writes, and single-use `CommitAsync(CancellationToken)`.

- [x] Audit existing tests before adding missing cases for default/foreign plans, disposal, intervening ordinary output, resize, suspend/resume, and multiple same-epoch transactions. Pin each event to its documented epoch behavior; do not assume every dimension observation invalidates an epoch.
- [x] Verify rejected mutation leaves counts and retained payload unchanged, including mixed items near the 65,536-item and 64 MiB boundaries. Cover exact limit and limit-plus-one with multibyte text and expanded plans without repeatedly allocating oversized buffers.
- [x] Assert pre-commit rejection emits nothing; preserve consumed-commit behavior after cancellation and the existing empty-commit flush/epoch behavior. Treat transaction construction as single-caller use unless the reviewed contract says otherwise.
- [x] Add red tests only for uncovered behavior, fix demonstrated defects, run transaction and planner regression suites, and commit.

**Exit:** Admission is atomic, resource retention remains bounded, and lifetime/epoch rejection is observable before unsafe output.

## T195 — Mixed output, cancellation, and failure cleanup

**Files:** `src/Screen/TerminalScreenOutputTransaction.cs`; relevant session output/ownership helpers; `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenOutputCompositionTests.cs`; `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenOutputTransactionHardeningTests.cs`.

**Interfaces:** Retain the existing hyperlink, synchronized-output, and session output reservations, commitment boundary, and ordered failure aggregation.

- [x] Extend deterministic barrier-based tests across reservation wait, output-gate wait, first write, body write, cleanup, and flush. Cancellation before commitment emits nothing; cancellation after commitment must not truncate required framing/cleanup.
- [x] Combine plans, text, hyperlinks, and raster placeholder cells with an ordinary concurrent session writer. Assert no interleaving, stale/foreign raster ownership rejection, and released reservations after success or failure.
- [x] Inject primary, hyperlink-close, synchronized-end, and flush failures individually and in combinations. Assert deterministic primary/cleanup failure order, required cleanup attempts, and truthful recovery on the next operation.
- [x] Use bounded waits and explicit test barriers instead of timing sleeps. Fix proven defects, run composition/hardening suites and T191, and commit.

**Exit:** Mixed-output failure cannot silently discard cleanup evidence, leak reservations, or claim rollback of bytes already emitted.

## T196 — Consumer guide and runnable sample

**Files:** create `docs/Screen-Output.md`, `samples/Icod.Terminal.ScreenOutput.Sample/Icod.Terminal.ScreenOutput.Sample.csproj`, `samples/Icod.Terminal.ScreenOutput.Sample/Program.cs`, and `packaging/VerifyScreenOutputSample.ps1`; update `samples/README.md`, `README.md`, `Icod.Terminal.sln`, and `packaging/VerifyRuntime.ps1`.

**Interfaces:** Use the same public planner/transaction flow as T191. Execute the same sample frame routine from the package harness without an interactive terminal; assertion failures terminate the harness with a nonzero exit code.

- [x] Implement a small sample that uses semantic profile planning, establishes a valid rendition baseline, plans cursor/rendition/text output, and commits. The fixed-origin frame requires no dimension query; document availability checks for applications that need dimensions. Demonstrate caller-driven stale-work recovery and execute it in the downstream harness. Show optional synchronized output only when its existing contract permits it.
- [x] Make unavailable plans and baseline failure visible in the sample's control flow; propagate stale-transaction and committed-output failures for the host to handle as documented. Use semantic APIs without raw escape strings or a direct TermInfo dependency.
- [x] Write the guide around those executable steps, including zero-byte versus unavailable plans, single-use transactions, capacity, cancellation, disposal, and the division of screen-state ownership.
- [x] Add the verifier to the runtime gate; compile the sample on all three frameworks and execute its frame routine in the package harness. Link the guide/sample from both READMEs, review XML comments for changed APIs, and commit.

**Exit:** Users can run the documented flow, and CI executes its noninteractive path.

## T197 — Package/API/XML/dependency and downstream qualification

**Files:** `packaging/VerifyPublicApiBaseline.ps1`; `packaging/VerifyReleaseLinePackage.ps1`; `packaging/VerifyDCursesPackage.ps1`; `tools/package-release-line-smoke/Program.cs`; create `docs/Public-API-Baseline-1.19.md` and `docs/Public-API-Baseline-1.19.sha256` when the contract is frozen.

- [x] Review the API diff against 1.18; preserve all existing members and enum values. Freeze matching snapshots for all three frameworks, even if the resulting API is unchanged, and update the active baseline selector.
- [x] Prove the verifier rejects a deliberately mismatched fingerprint in a temporary fixture; restore the accepted baseline before committing.
- [x] Build one candidate package and run every existing contract shard against that artifact. Verify XML documentation, package metadata, license, symbols, and production dependency graph.
- [x] Run all T191 consumers from isolated package restores against the exact candidate version, plus the new sample smoke path. Record framework, package SHA-256, source SHA, and consumer versions; commit acceptance evidence.

**Exit:** Source, public API, documented API, and fresh-package consumers agree; no project-reference success substitutes for package evidence.

## T198 — Cross-platform regression and bounded soak

**Files:** `.github/workflows/pull-request.yaml` and relevant packaging verifiers only where required to include the new evidence; this roadmap for accepted results.

- [x] Pass the full unit and TermInfo integration suites for all three frameworks on Windows, Linux, and macOS through the existing PR matrix.
- [x] Run bounded repeated refresh/recovery/close workloads, supplemented by deterministic cancellation and transport-failure tests. Assert balanced ownership/cleanup and no deadlock or output interleaving; record iteration limits, timeouts, and the limits of the soak evidence.
- [x] Obtain independent review of planner fallback assumptions, presentation ownership if changed, commitment/cleanup rules, and package witness execution. Resolve material findings with focused regression evidence.
- [x] Record the exact tested head and workflow/job/artifact identifiers, limiting the claim to the exercised PR matrix. A fresh six-runner x64/ARM64 distribution run remains a prerequisite to claiming that additional coverage. Commit the qualification record without describing an untested later head as tested.

**Exit:** All required gates are green with traceable artifacts and no unresolved release-blocking findings.

## T199 — Stable 1.19.0 closure

**Files:** `Directory.Build.props`; `Icod.Terminal.csproj`; `README.md`; `CHANGELOG.md`; `docs/Compatibility-and-Versioning.md`; create `docs/releases/1.19.0.md`; update both roadmaps.

- [x] Synchronize release notes, packaged README, compatibility guidance, sample links, accepted/deferred scope, and final API/dependency evidence.
- [x] Remove the prerelease suffix only after implementation qualification; build and validate the resulting stable candidate, including the final metadata changes.
- [x] Record source commit, API fingerprint, package/symbol hashes, workflow runs, artifacts, test results, consumer versions, and remaining limitations. Advance roadmap status only to the stage actually reached.
- [x] Present the qualified stable candidate for maintainer release action. Merge, tag, GitHub Release, and publication are distinct actions from this planning PR.

**Exit:** A reviewable, reproducibly qualified 1.19.0 candidate with accurate release-facing documentation.

## Verification commands

Run focused tests during each code tranche, then broaden at the explicit integration/release gates. Commands below are run from the repository root; repeat focused tests for `net9.0` and `net10.0` before tranche acceptance.

```sh
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net8.0 --filter FullyQualifiedName~Icod.Terminal.Tests.Screen
dotnet test tests/Icod.Terminal.TermInfoIntegration.Tests/Icod.Terminal.TermInfoIntegration.Tests.csproj -c Staging -f net8.0
pwsh -NoProfile -File packaging/VerifyRuntime.ps1 -Configuration Staging
pwsh -NoProfile -File packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/1.19-candidate -Configuration Staging
pwsh -NoProfile -File packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.19-candidate -Configuration Staging -Shard foundation
pwsh -NoProfile -File packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.19-candidate -Configuration Staging -Shard presentation
pwsh -NoProfile -File packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.19-candidate -Configuration Staging -Shard semantic
pwsh -NoProfile -File packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.19-candidate -Configuration Staging -Shard release
```

Passing means exit code zero, no failed tests, matching API fingerprints, and successful fresh-package consumers. The artifact builder recreates its selected output directory, so use a dedicated candidate directory. CI remains the cross-platform authority; local success is not a substitute for unrun platform jobs.

## Explicit non-goals

No Terminal-owned cells/windows/layout/damage engine; no automatic replay or retained physical-screen cache; no keyboard, query/response, operational-protocol, transport, or public-extensibility redesign; no PTY/process hosting; no new raster-animation/composition feature family; no broad terminal-profile redesign. Existing support in those areas remains under regression coverage. Defects directly exposed by the selected screen-output work may receive focused fixes with evidence.

## Authorities

- [Main roadmap](Icod.Terminal-Development-Roadmap.md)
- [1.17 screen-output design](docs/superpowers/specs/2026-09-17-1.17.0-terminal-screen-output-design.md)
- [1.18 rendition-baseline design](docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md)
- [Architecture](docs/Architecture.md)
- [Compatibility and versioning](docs/Compatibility-and-Versioning.md)
- [Security and privacy](docs/Security-and-Privacy.md)
- [Build and packaging workflow](packaging/README.md)
