# Icod.Terminal 1.20.0 — Profile and Capability Decisions Development Roadmap

> **For agentic workers:** Use `superpowers:executing-plans` to implement this roadmap inline, task by task. Preserve completed evidence across interrupted sessions. Checkboxes record executable acceptance, not intent.

**Goal:** Let consumers distinguish static screen advertisement, concrete planning results, live capability evidence, and endpoint availability through Terminal-owned contracts.

**Architecture:** Extend immutable screen-profile facts; qualify the existing three capability-verification paths through the current query coordinator; teach the resulting decisions through the existing capability sample and executed package consumers. Keep routing, framing, state ownership, and retained-screen policy in their existing layers.

**Tech stack:** C# 13; .NET 8/9/10; PowerShell 5.1-compatible packaging scripts; cmd/sh. No Python or new tooling dependencies.

**Spec:** [1.20 profile/capability design](docs/superpowers/specs/2026-09-27-1.20.0-profile-capability-design.md).

**Selected scope:** **3 + focused 6 + 10**.

**Status:** Planning only. Release scope selected on 2026-09-27; T200-T209 remain pending. This PR initially changes planning documents only. The design's additive API proposal is reviewed in T200 before production implementation.

## Global constraints

- Language: C# 13. Target frameworks: `net8.0`; `net9.0`; `net10.0`.
- Stable compatibility floor: `1.0.0`; preserve existing signatures, enum values, and documented behavior.
- Production dependencies: `Icod.TermInfo 1.16.0` and `Icod.Timing 1.0.0`. Optional integration tests/samples: `Icod.TermInfo.Inspection 1.16.0`. No dependency refresh is part of this scope.
- Use C#/.NET, PowerShell 5.1-compatible scripts, and cmd/sh. No Python or new tooling dependency.
- New profile contracts expose no TermInfo types, raw capability identifiers, expansion programs, protocol frames, or backend selectors.
- Static profile construction and capability inspection emit no terminal traffic. Explicit verification retains one authoritative input/query/event path and current output commitment rules.
- Profile advertisement, parameter-specific plan availability, live support evidence, endpoint availability, and successful physical execution remain distinct.
- Live evidence is generation-scoped; immutable returned snapshots and static profile evidence remain stable. Silence/timeout alone is not negative support evidence.
- Preserve existing parser/query/resource bounds and the screen transaction's 65,536 retained-item / 64 MiB limits. No new unbounded cache or background probe.
- Preserve caller ownership of layout, retained cells, damage, physical-state assumptions, and recovery. No hidden replay or automatic query retry policy.
- Historical roadmaps, released API baselines, and exact source/artifact identities remain intact.

## Review focus

| Risk | Required result | Tranche |
| --- | --- | --- |
| Partial, empty, or malformed static capabilities | Advertisement describes presence; planning remains authoritative for the request, including valid zero-byte plans and safe rejection. | T201-T202 |
| Alternative routes without absolute cursor addressing | Static facts do not cause consumers to reject a valid home/relative or row/column plan. | T202, T206 |
| Redirected endpoints, silent peers, or partial raster evidence | Endpoint failure/uncertainty does not fabricate unsupported truth or persistent/animation support. | T203-T204 |
| Late response after cancellation, invalidation, or resume | No cross-request match, new-generation evidence pollution, reopened ownership, or leaked probe registration. | T204-T205 |
| Sample-only coverage or accidental dependency leakage | Execute actual sample logic against a fresh package; direct consumer uses Terminal alone and no second reader. | T206-T207 |

## Starting evidence

- Released baseline: tag `v1.19.0`, merge commit `b3f7adf929d36ea654f2116ad6132781edc3fb3b`, [PR #64](https://github.com/uniblab/Icod.Terminal/pull/64), [GitHub release](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.19.0).
- The final pre-merge dependency-refresh head `5849ec717ba59a37ec7395d11f5c892ef3f380b2` passed all nine jobs in [workflow 36296373936](https://github.com/uniblab/Icod.Terminal/actions/runs/36296373936). Its runtime matrix passed 2,402 unit tests and 15 integration tests per framework on Windows, Linux, and macOS. These are baseline results, not 1.20 qualification.
- Baseline API fingerprint on every framework: `48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d`. Preserve [1.19 evidence](docs/Public-API-Baseline-1.19.md); generate a separate 1.20 baseline after additive API review.
- `TerminalProfile.Create` currently projects color, attributes, absolute addressing, ACS, and visibility facts. Existing planner methods additionally cover alternative cursor routes, erase, character/line shifts, and scroll regions.
- `TerminalSession.CapabilityInspection.cs` has twelve capability mappings and three active verification cases. The guide/sample's older nine/two inventory needs reconciliation.
- Existing inspection/lifecycle/verification tests and package/sample scripts are reusable. Start with a coverage inventory; do not recreate already-proven tests.

## Selected workstreams and completion evidence

| Choice | Deliverable | Completion evidence |
| --- | --- | --- |
| 3 | Immutable semantic advertisement for existing cursor/erase/shift/scroll planning families, plus truthful guidance on limits | Additive reviewed API, source mapping, static projection tests, fallback/parameter tests, no-traffic assertions |
| focused 6 | Existing keyboard, raster, and persistent-raster verification paths qualified end to end | Explicit three-path/no-probe matrix, exact traffic/correlation tests, uncertainty and lifecycle regressions |
| 10 | Current guide and executable capability sample | Twelve-status report, explicit three-path verification, headless help, executed source-linked sample/package checks |

## Tranche sequence

```text
T200  released baseline, contract/source mapping, API review, development identity
T201  immutable screen-profile advertisement surface
T202  advertisement/planning distinction and partial-profile qualification
T203  existing verification-path and no-probe contract matrix
T204  deadlines, cancellation, malformed and late-response qualification
T205  lifecycle generations, endpoint availability, and concurrent ownership
T206  consumer guide and executed capability sample
T207  fresh-package/API/XML/dependency and downstream acceptance
T208  cross-platform regression, independent review, and scope reconciliation
T209  stable 1.20.0 release closure and exact evidence
```

Dependencies: T200 precedes implementation. T201 precedes T202. T203 precedes T204-T205. T206 integrates T202 and T205; T207-T209 follow sequentially. Keep the work inline in the release branch and commit each accepted tranche separately.

## T200 — Baseline and contract freeze

**Files:** this roadmap; the linked design; `Directory.Build.props`; existing authorities `docs/Capability-Inspection-and-Planning.md`, `docs/Screen-Output.md`, and `docs/T192-Permanent-Input-Query-and-Protocol-Semantics.md`.

**Consumes:** released 1.19 source/API and the maintainer-selected scope.

**Produces:** reviewed profile signatures/source mapping, twelve-capability/three-probe truth table, verification timing/error contract, and `1.20.0-alpha.1` development identity.

- [ ] Inventory existing tests for every design obligation, naming the proving test or a specific uncovered scenario. Preserve valid existing coverage.
- [ ] Freeze the design's nine new advertisement properties and three enum-parameter methods, including absence/present-empty/malformed semantics, default values, invalid enum handling, and value equality. Record exact existing planner sources and alternatives for each fact; do not invent new routes.
- [ ] Freeze the current twelve capability values and which three can probe. Trace the keyboard, Kitty, and Sixel paths through actual per-query deadlines, sequencing, late-response ownership, errors, and generation recording. Distinguish a per-query deadline from total operation duration.
- [ ] Review API compatibility and scope against the design. Resolve naming or behavioral changes in the documents before dependent code starts; explicitly defer anything requiring another state owner or protocol family.
- [ ] Run the baseline runtime/package/API gates from the command section or record matching exact-head CI evidence. Record failures before changing code.
- [ ] Set `Directory.Build.props` to `VersionPrefix=1.20.0`, `VersionSuffix=alpha.1` when implementation starts; keep released README/version claims accurate. Commit the reviewed contract and development identity.

**Acceptance:** The profile schema and verification contract are precise enough to implement; baseline evidence is recorded. A planning PR alone does not complete T200.

## T201 — Static screen advertisement

**Modify:** `src/Screen/TerminalProfile.cs`, `src/Screen/TerminalScreenCapabilities.cs`.

**Create:** `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenProfileAdvertisementTests.cs`.

**Consumes:** T200 mappings and existing `TerminalScreenEraseKind`, `TerminalScreenCharacterShiftKind`, and `TerminalScreenLineShiftKind`.

**Produces:** exactly the reviewed `AdvertisesCursor...`, `AdvertisesCarriageReturn`, `AdvertisesScrollRegion`, `AdvertisesErase(kind)`, `AdvertisesCharacterShift(kind)`, and `AdvertisesLineShift(kind)` surface in the design. Existing members retain their behavior.

- [ ] Add table-driven tests for every new member: absent, present-empty, present-valid, parameterized-only, single-step-only where supported, and unrelated capabilities. Assert default capabilities advertise none and invalid enum arguments throw `ArgumentOutOfRangeException`.
- [ ] Add immutability, equality/hash, dependency-neutral public signature, and zero-output assertions. Verify identical descriptions do not compare differently because of backing collection identity.
- [ ] Run the focused tests and observe failure for missing additions; implement bounded presence projection with no expansion, probing, or planner duplication.
- [ ] Run the profile and existing screen-contract tests on all frameworks, then commit.

**Acceptance:** All mappings and old profile behavior are qualified; profile construction emits no traffic and retains no mutable caller-owned state.

## T202 — Advertisement versus executable planning

**Modify/test:** `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenProfileAdvertisementTests.cs`, `TerminalScreenPlannerCoreHardeningTests.cs`, `TerminalScreenEditingPlannerHardeningTests.cs`, and `TerminalScreenRenditionPlannerHardeningTests.cs` in the same directory. Production planner changes are allowed only for a reproduced in-scope defect.

**Consumes:** T201 facts and existing `PlanCursorMove`, `PlanErase`, `PlanCharacterShift`, `PlanLineShift`, `PlanScrollRegion`, and `PlanRenditionBaseline` contracts.

**Produces:** executable decision-table evidence, with no new planner route or transaction API.

- [ ] Add missing tests for home/relative and row/column paths with absolute addressing absent; assert an actual existing plan can be available despite the absolute flag being false.
- [ ] Pin present-empty/zero-byte behavior, parameter/count-sensitive failure, malformed-source rejection, and unrelated valid alternatives using the planner's established null/exception contract. Do not convert all planner errors into `null` merely for the sample.
- [ ] Assert advertisement and planning both emit no bytes; constructing either must not acquire a lease, mutate evidence, or invalidate a pending transaction.
- [ ] Run focused tests, reproduce any defect before correcting it, then run the screen suite on all frameworks and commit.

**Acceptance:** Consumers have executable examples proving that advertisement, a concrete plan, and physical execution are different claims.

## T203 — Verification dispatch and evidence matrix

**Modify/test:** `src/Session/TerminalSession.CapabilityInspection.cs`; `tests/Icod.Terminal.Tests/src/Session/TerminalSessionCapabilityInspectionTests.cs`, `TerminalCapabilityVerificationContractTests.cs`, `TerminalCapabilityAdversarialTests.cs`, and `TerminalPersistentRasterCapabilityTests.cs`.

**Consumes/produces:** unchanged `InspectCapability(TerminalCapability)` and `VerifyCapabilityAsync(TerminalCapability, CancellationToken)` signatures; existing `TerminalCapabilityStatus` dimensions.

- [ ] Parameterize inspection/no-probe tests over all twelve current capability values. Assert zero traffic for inspection and for verification values outside the three approved paths.
- [ ] Qualify each active path from unknown/static evidence through valid positive, authoritative negative where defined, and inconclusive outcomes. Assert support, endpoint, evidence kind, and usability separately.
- [ ] Verify aggregate raster success with one usable backend and persistent-raster rejection of Sixel-only evidence. Do not infer placeholder/animation support from a generic graphics observation.
- [ ] Assert decisive current evidence and unavailable endpoints skip probe traffic, and pre-cancellation/invalid enum handling retain their existing precedence and errors.
- [ ] Run the matrix, fix only reproduced dispatch/projection defects, run related existing tests on all frameworks, and commit.

**Acceptance:** Exact expected query traffic and truthful status results are documented and tested for the complete current capability vocabulary.

## T204 — Bounded query failure and recovery

**Inspect/modify only as needed:** `src/Session/TerminalSession.KittyKeyboard.cs`, `TerminalSession.KittyGraphicsCapabilities.cs`, `TerminalSession.RasterGraphics.cs`, and `TerminalQueryTransactionManager.cs` in the same directory; corresponding coordinator/parser code only where a reproduced defect requires it.

**Test:** `tests/Icod.Terminal.Tests/src/Session/TerminalCapabilityAdversarialTests.cs`; `tests/Icod.Terminal.Tests/src/Input/TerminalQueryTransactionTests.cs`, `TerminalMultiFamilyQueryTransactionTests.cs`, `TerminalKittyGraphicsCapabilityEvidenceTests.cs`, and `TerminalSixelCapabilityEvidenceTests.cs`.

**Consumes:** T203 three-path matrix and T200 deadlines/error semantics.

**Produces:** bounded verification and correct response ownership after uncertainty/failure, without a new scheduler or public timeout API.

- [ ] Add missing silent-peer, truncated/correlated-malformed, oversized, unrelated-response, and fragmented-response cases for the included paths. Assert timeout alone creates no permanent unsupported result; preserve reviewed format/transport exceptions.
- [ ] Qualify cancellation before admission and after request commitment. Assert framing remains intact, probe registrations are cleaned up, and subsequent independent input/query work succeeds.
- [ ] Inject late replies after timeout/cancellation, followed by another query. Assert the old reply cannot satisfy the new request and ordinary input remains available through the authoritative event path.
- [ ] Use deterministic scripted transport/release signals for ordering. Exercise production timeout behavior with an outer test timeout; avoid scheduler-speed assertions or long sleep-based races.
- [ ] Observe regression failure before each production correction, then run related query/input and capability suites on all frameworks and commit.

**Acceptance:** Failures terminate within existing bounds, keep uncertainty truthful, and leave unrelated input/query ownership usable.

## T205 — Lifecycle and concurrent evidence ownership

**Inspect/modify only as needed:** `src/Routing/TerminalCapabilityEvidenceLedger.cs`, `src/Session/TerminalSession.Lifecycle.cs`, and the T203-T204 implementation files.

**Test:** `tests/Icod.Terminal.Tests/src/Session/TerminalCapabilityLifecycleAndOwnershipTests.cs`, `TerminalCapabilityAdversarialTests.cs`; `tests/Icod.Terminal.Tests/src/Routing/TerminalCapabilityEvidenceLedgerTests.cs`.

**Consumes:** the same immutable profile/status and verification contracts.

**Produces:** generation-correct evidence with existing session lifecycle and query ownership.

- [ ] Qualify static facts and immutable snapshots across explicit invalidation and actual managed suspend/resume; assert re-inspection drops stale live evidence while retaining static evidence.
- [ ] Invalidate or resume with a response pending, then deliver the old response. Assert it cannot establish support for the new generation; verify a fresh request can succeed.
- [ ] Qualify redirected/unavailable endpoints, suspended ownership, concurrent inspection/verification, and disposal during an active probe. Assert no reopened disposed session, second reader, leaked registration, or cross-capability evidence contamination.
- [ ] Assert bounded correctness under concurrency; do not require coalesced probes or a new cache. Fix only demonstrated ownership defects, run lifecycle/query regressions on all frameworks, and commit.

**Acceptance:** Static description and current live knowledge remain distinct and truthful across lifecycle/concurrency boundaries.

## T206 — Consumer documentation and executed sample

**Modify:** `samples/Icod.Terminal.CapabilityPlanning.Sample/Program.cs`; `packaging/VerifyCapabilityPlanningSample.ps1`; `README.md`; `samples/README.md`; `docs/Capability-Inspection-and-Planning.md`, `docs/Screen-Output.md`, `docs/Architecture.md`, `docs/Compatibility-and-Versioning.md`, and `docs/Security-and-Privacy.md`.

**Create:** `samples/Icod.Terminal.CapabilityPlanning.Sample/CapabilityPlanningExample.cs`, the sample's `README.md`, and `tests/Icod.Terminal.Tests/src/Integration/CapabilityPlanningSampleTests.cs`. Use the existing test project's source-link mechanism, adding its compile entry if needed.

**Consumes:** T201 profile additions, unchanged planners, and T203-T205 verification behavior.

**Produces:** the design's default/`--verify`/`--help`/`-h` modes; exits 0/1/2/130 as specified. The extracted routine accepts a caller-provided session, report writer, verification selection, and cancellation token; freeze its internal signature with its tests in this tranche.

- [ ] Test help and invalid arguments without opening a terminal; test the actual report routine against deterministic sessions for all twelve status rows, profile facts, concrete plan availability, and unknown/unavailable output.
- [ ] Test that default reporting performs no explicit verification and `--verify` uses only the three included paths. Assert no mode acquisition, persistent upload, clipboard mutation, raw output, or second reader.
- [ ] Implement the source-linkable routine and CLI with cancellation/cleanup; execute the same routine in tests. Run headless help in the sample verifier on all frameworks.
- [ ] Write the advertisement/planning/live-evidence decision guide and current twelve/three matrix, including no-probe values, per-query timing limits, snapshots, and examples of application fallback.
- [ ] Update current documentation links and sample catalog; retain Ken Arnold attribution and accurate released/development version wording. Keep historical evidence unchanged.
- [ ] Build/run sample checks on all frameworks, check local Markdown links, and commit.

**Acceptance:** Documentation teaches the tested contract, and the sample's real paths execute without live-terminal dependence in automated tests.

## T207 — Package, API, and downstream qualification

**Modify:** `tools/package-capability-planning-smoke/Program.cs` and its project; `packaging/VerifyCapabilityPlanningPackage.ps1`, `VerifyPublicApiBaseline.ps1`, and `VerifyPackageArtifact.ps1` as required by the reviewed API/release checks. Retain the existing shard layout.

**Create:** `docs/Public-API-Baseline-1.20.md`, `docs/Public-API-Baseline-1.20.sha256`, and `docs/releases/1.20.0.md` when implementation is ready for qualification.

**Consumes:** T206 sample routine and T201 public additions from a fresh 1.20 package.

**Produces:** additive API evidence, required XML docs, executed consumer/sample behavior, unchanged dependency boundary, and downstream compatibility.

- [ ] Extend the fresh-package capability consumer to exercise new profile facts and the complete verification/no-probe matrix; execute the actual sample routine by source linking. Keep fixture dependencies in the test host and resolve Terminal from the package, not the source project.
- [ ] Require XML documentation for every additive member. Check removed/changed old signatures and enum values against 1.19 independently of the new fingerprint.
- [ ] Generate all-framework 1.20 API snapshots, review additions, and freeze equal fingerprints. Preserve every historical baseline and prove the verifier rejects a deliberately wrong temporary fingerprint.
- [ ] Build one candidate artifact and run all four existing package shards against those exact bytes. Retain DCurses 1.6.0 and 2.2.0, the Terminal-only renderer, and 1.19 screen-sample execution.
- [ ] Check nuspec dependencies, symbols, license, packaged README, release metadata, and consumer reference boundaries. Record source, artifact identity, and hashes; commit qualification changes.

**Acceptance:** The release is consumable as a package with additive documented APIs and no direct TermInfo coupling in the controlled consumer.

## T208 — Regression and review

**Files:** tests and production files only for demonstrated in-scope findings; this roadmap for exact evidence.

- [ ] Pass the full Windows/Linux/macOS runtime matrix across all target frameworks, including optional Inspection integration and sample builds.
- [ ] Pass package preparation, all four contract shards, and validated artifact for the same final source head. Do not reuse earlier-head results as current evidence.
- [ ] Review API meaning, source mappings, parser/correlation behavior, generation races, dependency neutrality, and the sample's no-probe guarantees. Resolve findings with failing regression evidence where applicable.
- [ ] Reconcile every selected requirement with a completed task or explicit maintainer-approved deferral. No unresolved correctness issue or silently dropped Option 3 deliverable.
- [ ] Record actual qualification limits: scripted tests are not physical-emulator certification, performance benchmarks, or a fresh six-runner architecture matrix.

**Acceptance:** All selected behavior is reviewable and the final head has complete regression/package evidence.

## T209 — Stable release closure

**Files:** `Directory.Build.props`; root/current documentation; this roadmap; the main roadmap; `CHANGELOG.md`; `docs/releases/1.20.0.md`; the new 1.20 API baseline.

- [ ] Set stable `1.20.0` metadata only after T200-T208 acceptance; synchronize README, changelog, release notes, and roadmap status.
- [ ] Rebuild and qualify the final stable candidate; record exact source/merge identities, CI jobs, package/symbol hashes, and each framework's API fingerprint.
- [ ] Confirm package contents and public compatibility against 1.19; distinguish historical checkpoints from final artifacts.
- [ ] Present the completed PR for maintainer review. Merge, tag, GitHub Release, and publication are separate maintainer actions.

**Acceptance:** Reviewable stable release candidate with complete evidence; no claim of publication before it occurs.

## Verification commands

Run from the repository root using the repository's supported .NET SDK and PowerShell. These are implementation gates, not claims that the planning-only PR ran or passed them.

```powershell
# Focused profile/planner and capability/query suites; dotnet test covers all project TFMs.
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging --filter 'FullyQualifiedName~TerminalScreen'
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging --filter 'FullyQualifiedName~Capability|FullyQualifiedName~TerminalQuery|FullyQualifiedName~TerminalMultiFamilyQuery'

# Existing complete runtime/source/sample gate.
./packaging/VerifyRuntime.ps1 -Configuration Staging

# Build once, then verify all consumers against the same package.
./packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/1.20-package -Configuration Staging
foreach ($shard in @('foundation', 'presentation', 'semantic', 'release')) {
    ./packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.20-package -Configuration Staging -Shard $shard
}
```

Use the existing CI workflow for platform authority. Where local multi-node MSBuild is restricted, record a temporary single-process invocation workaround without changing the repository workflow or representing local results as cross-platform evidence.

## Deferred scope

New protocol families, broad query-router changes, configurable query scheduling/coalescing, cursor-visibility transaction composition, endpoint/transport expansion, public extensibility, animation composition, new raster positioning, image decoding, PTY hosting, and retained-screen/layout policy remain separate decisions. Performance changes require measured need; no benchmark improvement is promised by this release.
