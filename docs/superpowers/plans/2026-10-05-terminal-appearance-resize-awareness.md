# Icod.Terminal 1.27.0 Terminal Appearance and Resize Awareness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This repository's approved execution method is `superpowers:executing-plans` in the main session without subagents.

**Goal:** Add bounded appearance queries and independently owned appearance/resize reporting so applications can consume typed live terminal-environment observations through the existing authoritative event path.

**Architecture:** Add a focused environment protocol/parser layer and one session-owned reporting manager with independent mode 2031 and 2048 state. Reuse the existing query transaction manager, semantic decoder, control-output serialization, lifecycle observation window, and `TerminalControlResult<T>`/lease conventions; do not add a reader, queue, cache, or TermInfo dependency.

**Tech Stack:** C# 13; .NET 8/9/10; xUnit; managed Windows/Linux/macOS; PowerShell 5.1-compatible package gates; cmd/sh compatibility launchers; existing GitHub Actions matrices.

**Spec:** `docs/superpowers/specs/2026-10-05-terminal-appearance-resize-awareness-design.md`

## Global Constraints

- Preserve the stable 1.0.0 compatibility floor, all existing public signatures, and existing enum numeric values.
- Keep `net8.0`, `net9.0`, and `net10.0` and managed Windows/Linux/macOS support.
- Keep production dependencies at `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`; no Icod.TermInfo change belongs in this release.
- Use one authoritative input/query/event reader. Route an active query first, then valid semantic environment reports, then ordinary input.
- Recognize 7-bit and 8-bit CSI; bound each environment frame by `min(4096, TerminalInputDecoderOptions.MaximumBufferedBytes)` and retain the existing session input-buffer bound.
- Preserve the existing one-second late-response ownership and 32-pending-query limit. Never invent a request ID or generation token.
- Keep appearance and resize mode ownership independent. Opening a session never enables either mode.
- Preserve serialized output and commitment/cancellation rules. Never hold the control-output gate while awaiting a reply, and never blindly retry ambiguous committed output.
- Acquire gates in this order: state composition, environment-reporting manager, query ambiguity ownership, then control output only for emission. The decoder acquires neither of the first two gates.
- Preserve native resize lifecycle events and synchronous geometry APIs; never republish one in-band resize report as a lifecycle event.
- Do not coalesce or deduplicate reports, including repeated appearance values and pixel-only resize changes. Add no report queue.
- Keep raw input, arbitrary reply bytes, environment dumps, and host identity out of compatibility evidence.
- Retain the graphics hold: no raster work, Kitty workaround, PTY hosting, layout/repaint ownership, or unrelated protocol family.
- Execute each task with a witnessed RED run, the smallest implementation, a GREEN focused run, and a coherent commit. Run the complete verification gates before stable closure.

## Review Focus

1. An appearance report races or follows a query timeout: `TerminalAppearanceQueryTests` and `TerminalEnvironmentRoutingTests` must prove one owner, no duplicate event, one-second late ownership, and recovery of following keyboard input.
2. Reporting was already enabled before acquisition: `TerminalAppearanceReportingTests` and `TerminalInBandResizeReportingTests` must prove final disposal does not disable an external owner and resize re-enable requests the required initial report.
3. A resize changes pixels but not rows/columns, or native and in-band sizes disagree: `TerminalInBandResizeReportingTests` must preserve both facts, provenance, and synchronous native geometry without deduplication or fabrication.
4. Suspend/resume intersects negotiation, release, or disposal: `TerminalEnvironmentLifecycleTests` and `TerminalEnvironmentConcurrencyTests` must deterministically prove bounded completion, the frozen re-entry order, and no gate cycle.
5. A burst mixes reports, partial frames, malformed frames, and keyboard bytes: `TerminalEnvironmentProtocolTests` and `TerminalEnvironmentRoutingTests` must prove bounded recovery, backpressure through `ReadEventAsync(...)`, and no ordinary-input loss.

---

### Task 1: Establish the 1.27 alpha identity and additive event contract

**Files:**
- Modify: `Directory.Build.props`
- Modify: `Icod.Terminal.csproj`
- Create: `docs/releases/1.27.0-alpha.1.md`
- Create: `src/Environment/TerminalAppearance.cs`
- Create: `src/Environment/TerminalInBandResizeEvent.cs`
- Modify: `src/Input/TerminalSemanticEvent.cs`
- Create: `tests/Icod.Terminal.Tests/src/Session/TerminalEnvironmentPublicApiTests.cs`

**Interfaces:**
- Consumes: existing `TerminalDimensions`, `TerminalPixelDimensions`, `TerminalEventKind.Semantic`, XML-doc and public-API conventions.
- Produces: `TerminalAppearance { Unknown = 0, Dark = 1, Light = 2 }`; sealed `TerminalAppearanceEvent` and `TerminalInBandResizeEvent`; appended `TerminalSemanticEventKind.Appearance = 1` and `InBandResize = 2`; nullable `TerminalSemanticEvent.Appearance` and `.InBandResize`; internal `FromAppearance(...)` and `FromInBandResize(...)` factories.

- [x] **Step 1: Write the failing public-contract tests.** Assert exact enum numeric values; internal-only payload constructors; read-only payload properties; exactly one non-null semantic payload; existing `Notification = 0`; and unchanged existing public members.
- [x] **Step 2: Run the focused test to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter FullyQualifiedName~TerminalEnvironmentPublicApiTests`. Expected: FAIL because the environment types and semantic members do not exist.
- [x] **Step 3: Set `VersionPrefix` to `1.27.0` and `VersionSuffix` to `alpha.1`.** Add alpha notes stating that publication is not implied and live support remains evidence-based; update `PackageReleaseNotes` to identify `1.27.0-alpha.1` and link those notes without changing dependency versions.
- [x] **Step 4: Implement the value and event types.** Keep `Unknown` out of parser-produced events; map resize constructor inputs to existing `(columns, rows)` and `(width, height)` types; retain exact-one-payload construction inside `TerminalSemanticEvent`.
- [x] **Step 5: Run the focused test to verify GREEN.** Use the Step 2 command. Expected: all `TerminalEnvironmentPublicApiTests` pass on net10.0.
- [x] **Step 6: Run the existing semantic-event contracts.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~TerminalSemanticEventContractTests|FullyQualifiedName~TerminalSemanticEventSubstrateTests"`. Expected: all selected tests pass and notification behavior is unchanged.
- [x] **Step 7: Commit.** Commit as `feat: establish Terminal 1.27 environment event contract`.

### Task 2: Implement bounded environment protocol parsing and routing

**Files:**
- Create: `src/Environment/TerminalPrivateModeState.cs`
- Create: `src/Environment/TerminalEnvironmentProtocol.cs`
- Modify: `src/Input/TerminalInputDecoder.SemanticEvents.cs`
- Create: `tests/Icod.Terminal.Tests/src/Input/TerminalEnvironmentProtocolTests.cs`
- Create: `tests/Icod.Terminal.Tests/src/Input/TerminalEnvironmentRoutingTests.cs`
- Create: `tests/Icod.Terminal.Tests/src/Query/TerminalEnvironmentCorrelationTests.cs`

**Interfaces:**
- Consumes: `TerminalResponseFrame`, `ITerminalResponseMatcher`, `ICorrelatedTerminalResponseMatcher`, the existing CSI framer, and Task 1 payload factories.
- Produces: internal `TerminalPrivateModeState { NotRecognized = 0, Set = 1, Reset = 2, PermanentlySet = 3, PermanentlyReset = 4 }`; `TerminalEnvironmentProtocol.AppearanceQueryRequest`; `AppearanceReportMatcher`; `CreatePrivateModeQuery(int)`; `CreatePrivateModeSet(int, bool)`; `CreatePrivateModeReportMatcher(int)`; `ParseAppearance(TerminalResponseFrame)`; `ParsePrivateModeState(TerminalResponseFrame, int)`; and `ParseInBandResize(TerminalResponseFrame)`.

- [x] **Step 1: Write table-driven failing codec tests.** Pin exact bytes for `CSI ?996n`, `?2031$p`, `?2048$p`, `?2031h/l`, and `?2048h/l`; accept 7-bit/8-bit CSI; parse dark/light, DECRPM 0..4, and valid resize reports.
- [x] **Step 2: Add failing validation and recovery fixtures.** Cover every byte split, concatenated controls, permitted nonempty colon subparameters only in resize fields, empty subparameters, missing/extra fields, private-marker/intermediate/final errors, signed/nondecimal/overflow values, invalid mode states, zero/mixed-zero pixels, and an oversized frame followed by valid keyboard input.
- [x] **Step 3: Add failing routing/correlation fixtures.** Prove active query ownership precedes semantic recognition; unclaimed valid 2031/2048 DECRPM is consumed; malformed semantic-prefix frames end only at their proven boundary; and following ordinary input survives.
- [x] **Step 4: Run the three classes to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~TerminalEnvironmentProtocolTests|FullyQualifiedName~TerminalEnvironmentRoutingTests|FullyQualifiedName~TerminalEnvironmentCorrelationTests"`. Expected: FAIL because the protocol codec and environment recognizers are absent.
- [x] **Step 5: Implement strict codecs and matchers.** Use invariant decimal parsing with explicit `Int32.MaxValue` checks. Enforce rows/columns `1..Int32.MaxValue`; map `0,0` pixels to `null`; reject mixed zero/nonzero pixels; cap the recognized frame at `min(4096, MaximumBufferedBytes)`.
- [x] **Step 6: Extend semantic CSI recognition after query routing.** Emit one Task 1 semantic event per valid unclaimed appearance/resize report, consume valid orphaned 2031/2048 DECRPM, and retain all existing fallback behavior.
- [x] **Step 7: Run the Step 4 command to verify GREEN.** Expected: all selected tests pass without sleeps.
- [x] **Step 8: Run existing query/input regressions.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~TerminalQueryTransactionTests|FullyQualifiedName~TerminalSemanticEventAdversarialTests|FullyQualifiedName~TerminalSessionSemanticRoutingTests"`. Expected: all selected tests pass.
- [x] **Step 9: Commit.** Commit as `feat: parse and route terminal environment reports`.

### Task 3: Add the one-shot appearance query

**Files:**
- Create: `src/Session/TerminalSession.Environment.cs`
- Create: `tests/Icod.Terminal.Tests/src/Session/TerminalAppearanceQueryTests.cs`

**Interfaces:**
- Consumes: `TerminalEnvironmentProtocol.AppearanceQueryRequest`, `.AppearanceReportMatcher`, `.ParseAppearance(...)`, and `TerminalSession.ExecuteQueryAsync(...)`.
- Produces: `public ValueTask<TerminalAppearance> QueryAppearanceAsync(TimeSpan timeout, CancellationToken cancellationToken = default)`.

- [x] **Step 1: Write failing query tests.** Assert exact request bytes; `Dark`/`Light`; zero and one-minute accepted timeouts; negative/over-one-minute rejection; endpoint failure; caller cancellation; timeout; correlated malformed/unknown values as `FormatException`; and no mode-enable bytes.
- [x] **Step 2: Add the race tests from Review Focus 1.** The first matching report completes the active query and is not duplicated as an event; a matching frame in the one-second late window cannot complete the next ambiguous query or become input; a later report becomes a semantic event.
- [x] **Step 3: Run to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter FullyQualifiedName~TerminalAppearanceQueryTests`. Expected: FAIL because `QueryAppearanceAsync` is absent.
- [x] **Step 4: Implement the public query in `TerminalSession.Environment.cs`.** Validate the timeout with the existing query bound, execute exactly one environment query, and return only `Dark` or `Light`; never substitute `Unknown` for an exception.
- [x] **Step 5: Run the Step 3 command to verify GREEN.** Expected: all appearance query tests pass.
- [x] **Step 6: Run all query-manager lifecycle tests.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~TerminalQueryTransaction|FullyQualifiedName~TerminalQueryLifecycle"`. Expected: all selected tests pass.
- [x] **Step 7: Commit.** Commit as `feat: add bounded terminal appearance query`.

### Task 4: Add independently owned appearance reporting

**Files:**
- Create: `src/Environment/TerminalEnvironmentReportingKind.cs`
- Create: `src/Environment/TerminalEnvironmentReportingManager.cs`
- Create: `src/Environment/TerminalAppearanceReportingLease.cs`
- Modify: `src/Session/TerminalSession.Environment.cs`
- Modify: `src/Session/TerminalSession.cs`
- Create: `tests/Icod.Terminal.Tests/src/Session/TerminalAppearanceReportingTests.cs`

**Interfaces:**
- Consumes: mode 2031 codecs, `ExecuteQueryAsync(...)`, `TerminalControlResult<T>`, and serialized control output.
- Produces: `public ValueTask<TerminalControlResult<TerminalAppearanceReportingLease>> AcquireAppearanceReportingAsync(TimeSpan, CancellationToken = default)`; sealed idempotent `TerminalAppearanceReportingLease : IAsyncDisposable`; manager `AcquireAppearanceAsync(...)`, `ReleaseAsync(TerminalEnvironmentReportingKind, long)`, `Invalidate()`, and `CloseAsync()` entry points.

- [x] **Step 1: Write failing mode-table tests.** Pin all five DECRPM states: 0/4 return `Unavailable`; 1/3 return a lease without mode output; 2 emits one `CSI ?2031h` and final release emits one `CSI ?2031l`; malformed, silence, cancellation, and endpoint failure remain exceptions.
- [x] **Step 2: Add failing ownership tests.** Cover nested/concurrent owners, monotonic IDs, out-of-order disposal, repeated disposal, enable failure, restoration failure retaining ownership for retry, and acquisition cleanup only when a captured reset baseline makes disable correct.
- [x] **Step 3: Add the already-enabled test from Review Focus 2.** Acquire from Set and PermanentlySet, dispose every owner, and assert no disable bytes.
- [x] **Step 4: Run to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter FullyQualifiedName~TerminalAppearanceReportingTests`. Expected: FAIL because acquisition, manager, and lease are absent.
- [x] **Step 5: Implement appearance ownership.** Query outside the control-output gate; serialize committed writes; store independent baseline/owner state behind the manager gate; return controlled unavailable only for states 0/4; preserve same-value semantic reports.
- [x] **Step 6: Integrate manager construction only.** Add one session-owned manager, but defer lifecycle registration and disposal-order changes to Task 6.
- [x] **Step 7: Run the Step 4 command to verify GREEN.** Expected: all appearance reporting tests pass.
- [x] **Step 8: Run existing lease/output-certainty tests.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~LeaseTests|FullyQualifiedName~OutputCertainty"`. Expected: all selected tests pass.
- [x] **Step 9: Commit.** Commit as `feat: add scoped appearance reporting`.

### Task 5: Add independently owned in-band resize reporting

**Files:**
- Create: `src/Environment/TerminalInBandResizeReportingLease.cs`
- Modify: `src/Environment/TerminalEnvironmentReportingManager.cs`
- Modify: `src/Session/TerminalSession.Environment.cs`
- Create: `tests/Icod.Terminal.Tests/src/Session/TerminalInBandResizeReportingTests.cs`

**Interfaces:**
- Consumes: Task 4 manager owner records and mode 2048 protocol codecs.
- Produces: `public ValueTask<TerminalControlResult<TerminalInBandResizeReportingLease>> AcquireInBandResizeReportingAsync(TimeSpan, CancellationToken = default)` and sealed idempotent `TerminalInBandResizeReportingLease : IAsyncDisposable`; independent manager state for `TerminalEnvironmentReportingKind.InBandResize`.

- [x] **Step 1: Write failing mode-table tests.** Pin states 0/4 unavailable; Reset enables and final release disables; Set/PermanentlySet re-enable once to request the initial report and never disable on final release.
- [x] **Step 2: Write failing observation tests.** Cover initial report, row/column change, pixel-only change, repeated report, `0,0` pixels, operation without a native lifecycle source, and exactly one semantic event per wire report.
- [x] **Step 3: Add Review Focus 2 and 3 tests.** Verify already-enabled ownership survives disposal; synchronous `GetSize()`/`GetDimensions()` remain native; native/in-band disagreement remains two provenance-bearing facts; no lifecycle event is synthesized.
- [x] **Step 4: Run to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter FullyQualifiedName~TerminalInBandResizeReportingTests`. Expected: FAIL because resize acquisition and lease are absent.
- [x] **Step 5: Implement resize ownership.** Reuse manager mechanics without coupling its owner set or baseline to appearance. Re-enable Set/PermanentlySet to request a report, but leave that report on the authoritative input path.
- [x] **Step 6: Run the Step 4 command to verify GREEN.** Expected: all resize reporting tests pass.
- [x] **Step 7: Run geometry/lifecycle regression tests.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~Geometry|FullyQualifiedName~Lifecycle"`. Expected: all selected tests pass.
- [x] **Step 8: Commit.** Commit as `feat: add scoped in-band resize reporting`.

### Task 6: Harden lifecycle, invalidation, disposal, and concurrency

**Files:**
- Modify: `src/Environment/TerminalEnvironmentReportingManager.cs`
- Modify: `src/Session/TerminalSession.cs`
- Modify: `src/Session/TerminalSession.Lifecycle.cs` for external resume and partial reporting re-entry rollback
- Create: `tests/Icod.Terminal.Tests/src/Session/EnvironmentTestContext.cs`
- Create: `tests/Icod.Terminal.Tests/src/Session/TerminalEnvironmentLifecycleTests.cs`
- Create: `tests/Icod.Terminal.Tests/src/Session/TerminalEnvironmentConcurrencyTests.cs`

**Interfaces:**
- Consumes: `ITerminalObservedLifecycleParticipant`, `ExecuteLifecycleObservationQueryAsync(...)`, Task 4/5 manager state, existing query suspension, state-composition and output gates.
- Produces: manager implementations of `PrepareForTerminalSuspendAsync(...)`, `RefreshAfterTerminalResumeAsync(...)`, and `ResumeAfterTerminalSuspendAsync(...)`; session invalidation and exact disposal-order integration.

- [x] **Step 1: Write failing suspend/resume tests.** Pin public-query suspension, owned reset-state disable on suspend, the internal observation window, active-owner-only requery, reset re-enable, appearance Set/PermanentlySet no-write, resize Set/PermanentlySet re-enable, then public-query resume.
- [x] **Step 2: Write failing invalidation/disposal tests.** Active owners survive logically; baselines/freshness become unknown; final release emits no speculative toggle until refresh; manager close performs no query; valid owned reset baselines disable; invalidated baselines do not; disposal closes query transactions, lifecycle input, environment reporting, input protocols, presentation, then host restoration.
- [x] **Step 3: Write deterministic concurrency tests for Review Focus 4.** Use controlled gates—not sleeps—to interleave acquire/release, both modes, query timeout/cancellation, suspend/resume, input EOF, endpoint failure, committed-write failure, and disposal; assert completion and the frozen gate order.
- [x] **Step 4: Add Review Focus 5 burst tests.** Feed alternating appearance, resize, partial/malformed frames, and keyboard events beyond a single consumer turn; assert bounded one-event-at-a-time delivery, no deduplication, and keyboard preservation.
- [x] **Step 5: Run to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter "FullyQualifiedName~TerminalEnvironmentLifecycleTests|FullyQualifiedName~TerminalEnvironmentConcurrencyTests"`. Expected: FAIL on absent lifecycle/invalidation/disposal behavior.
- [x] **Step 6: Implement lifecycle participant behavior and session ordering.** Register the manager once as a core observed participant. Lifecycle refresh must release manager/state gates before awaiting replies, and all queries await replies outside the output gate. Ordinary acquisition retains the approved state/manager ordering; neither gate is required by the decoder or query writer. Aggregate cleanup errors through the existing session restoration mechanism. Mark leases released only after manager closure completes.
- [x] **Step 7: Run the Step 5 command to verify GREEN.** Expected: all selected tests pass deterministically.
- [x] **Step 8: Run the complete environment slice on every target.** Run the same command with `-f net8.0`, `-f net9.0`, and `-f net10.0` and filter `FullyQualifiedName~TerminalEnvironment|FullyQualifiedName~TerminalAppearance|FullyQualifiedName~TerminalInBandResize`. Expected: zero failures on each target.
- [x] **Step 9: Run the full runtime gate.** Run `pwsh -File packaging/VerifyRuntime.ps1 -Configuration Staging`. Expected: restore, build, all tests, samples, and downstream runtime checks complete successfully.
- [x] **Step 10: Commit.** Commit as `test: harden environment reporting lifecycle`.

### Task 7: Extend the public-only compatibility sample

**Files:**
- Create: `samples/Icod.Terminal.Compatibility.Sample/EnvironmentCompatibilityScenarios.cs`
- Modify: `samples/Icod.Terminal.Compatibility.Sample/QueryCompatibilityScenarios.cs`
- Modify: `samples/Icod.Terminal.Compatibility.Sample/CompatibilityScenarioCatalog.cs`
- Modify: `samples/Icod.Terminal.Compatibility.Sample/CompatibilityRunner.cs`
- Modify: `samples/Icod.Terminal.Compatibility.Sample/InteractiveConfirmation.cs`
- Create: `samples/Icod.Terminal.Compatibility.Sample/Run-Environment-WindowsTerminal.cmd`
- Create: `samples/Icod.Terminal.Compatibility.Sample/run-environment-kitty-wsl.sh`
- Modify: `samples/Icod.Terminal.Compatibility.Sample/README.md`
- Modify: `samples/README.md`
- Modify: `packaging/VerifyCompatibilitySample.ps1`
- Create: `tests/Icod.Terminal.Tests/src/Samples/TerminalEnvironmentCompatibilityScenarioTests.cs`
- Create: `docs/compatibility/evidence/1.27.0/README.md`
- Create: `docs/compatibility/1.27.0.md`

**Interfaces:**
- Consumes: only packaged public Icod.Terminal APIs from Tasks 1-6 and the existing bounded evidence writer/renderer.
- Produces: revision-1 scenarios `query.appearance/v1`, `environment.appearance-reporting/v1`, and `environment.in-band-resize/v1`; consented live acquisition; version-selectable 1.26/1.27 fixture verification; Windows Terminal cmd and Kitty/WSL sh launchers.

- [x] **Step 1: Write failing catalog/query tests.** Assert the three exact IDs/revisions/descriptions/success conditions, deterministic ordering, `query.appearance` as non-side-effecting, and the two reporting scenarios as consent-required.
- [x] **Step 2: Write failing live-flow tests.** Appearance reporting asks consent before acquisition and asks for an operator theme change only after availability; resize reporting asks consent before acquisition and asks for a resize only after availability; cancellation/disposal are bounded; unavailable is recorded without altering native behavior.
- [x] **Step 3: Add evidence privacy tests.** Assert reports contain typed outcomes and bounded notes, never raw escape bytes, raw keys, environment variables, or host identity.
- [x] **Step 4: Run to verify RED.** Run `dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0 --filter FullyQualifiedName~TerminalEnvironmentCompatibilityScenarioTests`. Expected: FAIL because the scenarios are absent.
- [x] **Step 5: Implement scenarios and launchers.** Keep `--help`, `--list-scenarios`, and `--describe` headless. Launchers collect exact terminal/OS/transport versions and write evidence to a review-only temporary directory; they do not claim support from terminal identity.
- [x] **Step 6: Generalize `VerifyCompatibilitySample.ps1`.** Add a version/evidence-root input so the existing 1.26 matrix remains byte-identical while a separate 1.27 matrix is rendered from 1.27 evidence.
- [x] **Step 7: Run the focused test and fresh-package sample gate.** Run the Step 4 command, then `pwsh -File packaging/VerifyCompatibilitySample.ps1 -Configuration Staging -ExpectedVersion 1.27.0-alpha.1`. Expected: all sample tests pass and package-only help/list/describe/fixture rendering is deterministic on net8/net9/net10.
- [x] **Step 8: Commit.** Commit as `feat: add environment compatibility scenarios`.

### Task 8: Freeze package, API, documentation, and regression contracts

**Files:**
- Create: `packaging/VerifyEnvironmentAwarenessPackage.ps1`
- Modify: `packaging/VerifyPackageContractShard.ps1`
- Create: `tools/package-environment-awareness-smoke/Icod.Terminal.PackageEnvironmentAwarenessSmoke.csproj`
- Create: `tools/package-environment-awareness-smoke/Program.cs`
- Create: `docs/Public-API-Baseline-1.27.md`
- Create: `docs/Public-API-Baseline-1.27.sha256`
- Modify: `packaging/VerifyPublicApiBaseline.ps1`
- Modify: `tests/Icod.Terminal.Tests/src/Packaging/DependencyCouplingPolicyTests.cs`
- Modify: `README.md`
- Modify: `docs/Architecture.md`
- Modify: `docs/Input-and-Events.md`
- Modify: `docs/Queries-and-Responses.md`
- Modify: `docs/Lifecycle-and-Restoration.md`
- Modify: `docs/Security-and-Privacy.md`
- Modify: `docs/Compatibility-and-Versioning.md`
- Modify: `CHANGELOG.md`
- Modify: `Icod.Terminal.csproj`

**Interfaces:**
- Consumes: the fully implemented alpha package and public compatibility sample.
- Produces: identical public API/XML snapshots across all targets; a fresh-package controlled-transport smoke for query/acquisition/events/cleanup; the semantic package-shard gate; consumer documentation that distinguishes observations, support, native resize, and restoration certainty.

- [x] **Step 1: Write the failing package smoke.** From only the `.nupkg`, compile-bind every new type/member, query dark/light, acquire both reporting leases over controlled transports, receive typed events, exercise nested disposal, and verify exact enable/disable bytes on net8/net9/net10.
- [x] **Step 2: Run to verify RED.** Build the candidate with `pwsh -File packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/package -Configuration Staging -ApiOutputDirectory artifacts/public-api`, then run `pwsh -File packaging/VerifyEnvironmentAwarenessPackage.ps1 -ArtifactDirectory artifacts/package -Configuration Staging -ExpectedVersion 1.27.0-alpha.1`. Expected: FAIL because the verifier/smoke is not yet wired.
- [x] **Step 3: Implement the package verifier and add it to the `semantic` shard.** Assert exact package dependency versions, XML members, enum numeric values, public signatures, fresh-package execution, and no direct TermInfo types in public API.
- [x] **Step 4: Generate and freeze the 1.27 public API baseline.** Confirm net8/net9/net10 snapshots are byte-identical; record the fingerprint; point `VerifyPublicApiBaseline.ps1` to the new baseline; prove a deliberate mismatch fails before restoring the correct hash.
- [x] **Step 5: Update permanent and package documentation.** Document explicit acquisition, unavailable versus exceptional outcomes, identical appearance response/report grammar, late-byte/generation limitations, provenance-aware resize reconciliation, no cached geometry replacement, privacy, lifecycle order, and no Icod.TermInfo change.
- [x] **Step 6: Run package/API/document gates.** Run the Step 2 commands again plus `pwsh -File packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/package -Shard semantic -Configuration Staging -ExpectedVersion 1.27.0-alpha.1` and `pwsh -File packaging/VerifyPublicApiBaseline.ps1 -Configuration Staging -BaselinePath docs/Public-API-Baseline-1.27.sha256`. Expected: all gates pass.
- [x] **Step 7: Run full source verification.** Run `pwsh -File packaging/VerifyRuntime.ps1 -Configuration Staging` and the repository's documentation, XML, dependency, license, and artifact workflows. Expected: zero failures on supported local platforms; record unavailable platform lanes for CI.
- [x] **Step 8: Commit.** Commit as `test: qualify Terminal 1.27 environment contracts`.

### Task 9: Collect and review focused live-terminal evidence

**Files:**
- Add after review: `docs/compatibility/evidence/1.27.0/*.json`
- Regenerate: `docs/compatibility/1.27.0.md`
- Modify: `Icod.Terminal-1.27.0-Development-Roadmap.md`

**Interfaces:**
- Consumes: Task 7 launchers and accepted evidence schema.
- Produces: reviewed exact-environment evidence for one positive appearance query/reporting lane, one positive initial/changed resize lane, one unavailable/missing lane preserving native input/resize, and one mediated lane when available.

- [ ] **Step 1: Build the exact candidate once.** Record source SHA, package version/hash, launcher command, terminal/OS/transport versions, and scenario revisions before live runs.
- [ ] **Step 2: Collect positive appearance evidence.** Run `query.appearance/v1`, then consent to `environment.appearance-reporting/v1` and perform one operator-visible appearance change on an exact supporting environment.
- [ ] **Step 3: Collect positive resize evidence.** Consent to `environment.in-band-resize/v1`, record the initial report, resize once, and retain optional-pixel behavior exactly as observed.
- [ ] **Step 4: Collect the fallback lane.** Use an exact environment that times out or explicitly reports unavailable; verify ordinary input and native resize still work; preserve `Inconclusive` versus `Unavailable` truthfully.
- [ ] **Step 5: Review before acceptance.** Reject reports with mismatched source SHA/scenario revision, unbounded notes, raw bytes/input, environment dumps, host identity, or claims inferred from branding.
- [ ] **Step 6: Regenerate and verify the matrix.** Run `dotnet run --project samples/Icod.Terminal.Compatibility.Sample/Icod.Terminal.Compatibility.Sample.csproj -c Staging -- --render-matrix docs/compatibility/evidence/1.27.0 docs/compatibility/1.27.0.md --release-version 1.27.0`, then rerun the package compatibility verifier. Expected: byte-identical rerender and explicit `NotRun` for untested lanes.
- [ ] **Step 7: Stop if either positive witness is missing.** Present the exact gap to the maintainer; do not convert CI, protocol documentation, or terminal branding into live support evidence.
- [ ] **Step 8: Commit.** Commit as `docs: record Terminal 1.27 live compatibility evidence`.

Partial checkpoint (2026-10-06): reviewed the three Windows Terminal `1.24.11911.0` / Windows `10.0.26200.9457` reports at source `a2bd1553765dee585607ea63de2ac6440f343fec`. Appearance query is Inconclusive; both reporting modes are Unavailable. Original JSON is retained and the 1.27 matrix regenerated. Byte-identical rerender and the compatibility verifier against the qualified alpha CI package passed on net8.0/net9.0/net10.0. Native fallback observations and both positive feature witnesses remain missing, so Task 9 is not complete and Task 10 has not started. The renderer preserves existing output files; regeneration used a fresh temporary destination followed by copying the verified result into place.

Further partial checkpoint (2026-10-06): reviewed the three Kitty 0.32.2 / Ubuntu 24.04 / WSL 2.6.1.0 reports at clean source `6c3bcdb5b209c30129bb23ec924f7ab031cd8542`. The recording confirms the detached Linux worktree and completed launcher run. Appearance query is Inconclusive; both reporting modes are Unavailable. Original JSON is retained and the 1.27 matrix includes this mediated lane. Positive feature witnesses and native fallback observations remain missing, so Tasks 9–10 remain incomplete. The corresponding qualified CI artifact identity and Kitty's later protocol introduction versions are recorded in the evidence README; no untested version is promoted to live support.

Kitty 0.49.2 partial checkpoint (2026-10-06): the subsequent clean run at the same `6c3bcdb` source passed bounded appearance query and operator-confirmed initial/changed in-band resize. Appearance reporting reached the prompt but timed out without an operator palette change; retained as Inconclusive. The original reports remain unchanged and both Kitty versions appear separately in the matrix. Positive appearance reporting and native fallback checks remain outstanding; Tasks 9–10 are not complete. Added temporary Kitty color-change shortcut instructions so the operator can act during the bounded wait.

Kitty 0.49.2 successful rerun (2026-10-06): the same clean `6c3bcdb` source/environment now passed query, typed Light appearance reporting with operator confirmation, and initial/changed in-band resize with operator confirmation. Both selected feature witnesses are complete. The successful rerun is the active exact-environment matrix record; unchanged first-run JSON, including appearance Inconclusive, remains in evidence history. Native fallback observations are still required. Task 9 remains partial; Task 10 has not started and alpha metadata is unchanged.

### Task 10: Close the stable 1.27.0 candidate

**Files:**
- Modify: `Directory.Build.props`
- Modify: `Icod.Terminal.csproj`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `Icod.Terminal-Development-Roadmap.md`
- Modify: `Icod.Terminal-1.27.0-Development-Roadmap.md`
- Create: `docs/releases/1.27.0.md`
- Modify: release/package workflow assertions only where the current release line is pinned

**Interfaces:**
- Consumes: one exact fully qualified alpha/RC head, its reviewed live evidence, 1.27 API fingerprint, and package gates.
- Produces: stable `1.27.0` metadata and curated release record. Merge, tag, GitHub release, and NuGet publication remain separate maintainer actions.

- [ ] **Step 1: Reconcile every T2700-T2709 checkbox, defect, limitation, and evidence link.** No unresolved positive-witness or restoration-safety gap may be waived silently.
- [ ] **Step 2: Set stable metadata.** Empty `VersionSuffix`; synchronize README installation version, package release notes, changelog, main roadmap, and curated `docs/releases/1.27.0.md`; retain exact dependency floors and graphics hold.
- [ ] **Step 3: Build one stable candidate.** Run `pwsh -File packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/package -Configuration Release -ApiOutputDirectory artifacts/public-api` and record source/package/symbol hashes.
- [ ] **Step 4: Run complete exact-head verification.** Run runtime, package shards, public API/XML, source integration, compatibility sample/matrix, documentation, dependency, license, artifact, and downstream DCurses gates on the same SHA across GitHub Actions Windows/Linux/macOS lanes.
- [ ] **Step 5: Require clean reruns for any failure.** Record workflow IDs, failing job/test, corrective commit, and same-head rerun; do not cite an earlier head as release evidence.
- [ ] **Step 6: Record release evidence.** Add exact test counts, workflow IDs, source SHA, API fingerprint, dependencies, package/symbol hashes, live terminal versions, and known limitations to the roadmap and curated notes.
- [ ] **Step 7: Request final review and commit.** Resolve all Critical/Important findings, then commit as `docs: close Terminal 1.27 release candidate`.
- [ ] **Step 8: Stop before promotion.** Present the stable candidate to the maintainer; do not merge, tag, publish a GitHub release, or push NuGet without separate approval.

## Self-review record

- Spec coverage: Tasks 1-8 implement every public type, parser rule, routing order, ownership table, lifecycle/invalidation rule, sample, package, API, and documentation requirement; Task 9 supplies live acceptance; Task 10 supplies stable closure.
- Step scan: every implementation task names its exact files, interfaces, RED command/failure, minimal production responsibility, GREEN command/result, and commit boundary. No placeholder marker, generic edge-case instruction, or unbounded refactor remains.
- Type consistency: public names/signatures and enum values match the approved design; internal protocol and manager names are introduced once and consumed consistently by later tasks.
- Review Focus: each of the five listed failure classes is pinned to named tests in Tasks 2-6.
- Proportion: the plan freezes decisions and witnesses without transcribing production method bodies; detailed wire/lifecycle rationale remains in the linked design.


## Resumption checkpoint — 2026-10-06

Tasks 1–8 are implemented and qualified at PR head `0da49e0cb436eb3bc339c93819819b8b0e1bce3b`; [workflow 37453595429](https://github.com/uniblab/Icod.Terminal/actions/runs/37453595429) passed all ten jobs. Windows, Linux, and macOS each passed 2,807 unit tests and 15 integration tests on every target framework, plus their runtime/sample/downstream checks. Package shards and fresh-package compatibility verification passed; both historical 1.26 and separate 1.27 matrices rerender identically. The 1.27 API hash is `356f455ec27065c63a642ae3d5b02125d2aed408d728cb6fadd0d47aeab12487`. Explicitly selecting the historical 1.25 hash against this API failed as intended, then selecting the correct 1.27 hash passed.

Inline review reproduced and fixed missing external-resume query suspension, partial reporting re-entry rollback, stale cross-facility invalidation, restoration after invalidation while awaiting output, sample catalog ordering, ambiguous Windows Terminal installed-version selection, dirty-checkout evidence attribution, and evidence validation for the new scenario IDs. Focused environment/compatibility tests passed 223/223 on net10.0. No Critical or Important source/package findings remain in this review; live acceptance remains outstanding.

Plan deviations are bounded: ordinary acquisition retains the approved state/manager gate order while awaiting a bounded reply because those gates are not needed by the decoder/query writer; lifecycle refresh releases both. The existing runner, confirmation helper, and recursive dependency-policy tests already cover the new flow/project and need no duplicate changes. Package RED evidence was the deliberately stale API baseline and absent environment verifier; the subsequently wired fresh-package smoke passed all three frameworks. Local SDK cold-build and Unix compiler-server restrictions were authoring-environment issues; serial builds with shared compilation disabled and a clean subsequent package build passed without a repository workaround.

Tasks 9–10 remain pending: obtain reviewed positive appearance query/reporting, initial and changed in-band resize, and missing/unavailable native-fallback witnesses. Every 1.27 live lane is NotRun. The candidate remains alpha.1; merge/tag/release/NuGet promotion is a separate maintainer action.
