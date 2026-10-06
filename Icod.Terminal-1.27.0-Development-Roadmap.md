# Icod.Terminal 1.27.0 Development Roadmap

> **Execution:** Use the executing-plans workflow task by task, in the main session without subagents. This is a release roadmap for review, not authorization to implement unreviewed public signatures. T2700 produces the detailed contract and implementation plan before runtime changes.

**Goal:** Let terminal applications observe appearance and text-area resize changes through bounded semantic APIs and the existing authoritative event path.

**Release theme:** Live Terminal Environment Awareness.

**Selection:** Options **2 + 3: terminal appearance and resize awareness**. Focused compatibility acceptance accompanies these features; the entire ten-emulator qualification backlog is not added to this release.

**Status:** The approved `1.27.0-alpha.1` runtime, lifecycle hardening, public-only compatibility scenarios, and package/API/document gates are implemented. Qualification passed at `0da49e0cb436eb3bc339c93819819b8b0e1bce3b` in workflow `37453595429` (all ten jobs). Tasks 9–10 remain gated on reviewed live-terminal witnesses and stable closure. Execution continues inline without subagents.

**Baseline:** Stable `1.26.0`, confirmed published to NuGet by the maintainer on 2026-10-05. Tag `v1.26.0` resolves to `2c0fafaf7d7a4f6a3cbdad206960f159fb19ef95`, the merge of PR #82. The unchanged 1.25/1.26 API fingerprint is `886a617d961af7eed37feaed026d83bbf06ec508ba248a4ca492baaf7e528146`. Stable workflow `37337222940` passed. Its release artifact ZIP SHA-256 is `4fe00b74d451a8c96b326f1fe4b4c46390392288d1be03ee6a6296608ab7dbc4`; the contained `.nupkg` and `.snupkg` SHA-256 values are `49a019d9ee8c8861ba97aeb23b6fd7f1f6b446812ad7b80cd9fd4d56cb0ace80` and `b9e17324e65efdb5dc3b684cccfdc1144154acb273589ef0df169db13042995d`.

**Architecture:** Extend the existing bounded query, semantic-event, output-serialization, and lifecycle ownership mechanisms. Appearance and resize reporting are independent opt-in facilities. Terminal reports observations; applications choose themes, layout, and repaint policy.

**Tech stack:** C# 13; .NET 8/9/10; managed Windows/Linux/macOS; PowerShell 5.1-compatible packaging scripts and cmd/sh launchers. No Python or native companion component.

**Design authorities:** [Architecture](docs/Architecture.md), [compatibility policy](docs/Compatibility-and-Versioning.md), [security and privacy](docs/Security-and-Privacy.md), and the scope/contract requirements below. T2700 must record an approved detailed design and implementation plan before introducing public API.

## Scope and alternatives

| Approach | Decision and reason |
| --- | --- |
| Appearance alone | Smaller alternative, but does not meet the selected resize goal. |
| Appearance plus negotiated in-band resize | **Selected.** Both improve live environment awareness using existing session authority. They remain independently usable and testable. |
| Broad environment/clipboard/color/transport expansion | Deferred. OSC 5522, OSC 21, ReportCellSize, and transport forwarding would add independent contracts and qualification requirements. |

The release adds an explicit bounded appearance query, an opt-in appearance reporting lifetime, and an opt-in in-band resize reporting lifetime. It does not automatically enable either mode when opening a session.

## Global constraints

- Preserve the stable 1.0.0 compatibility floor and every existing public signature and enum value; additions are reviewed and appended without renumbering.
- Retain `net8.0`, `net9.0`, and `net10.0` and Windows/Linux/macOS support.
- Keep production dependencies at `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`. Neither selected feature inherently needs a TermInfo API/parser change.
- Keep one authoritative input/query/event reader; active-query ownership precedes unsolicited semantic-event recognition, then ordinary input decoding.
- Preserve serialized session output and existing commitment/cancellation rules.
- Keep terminal branding, environment variables, host theme, and successful writes separate from support evidence.
- Timeouts, missing replies, unavailable endpoints, and permission/policy effects do not establish unsupported behavior.
- Retain the graphics-development hold: no raster changes, Kitty workarounds, codecs, replay, or graphics qualification.
- Keep PTY/process hosting in Icod.Pty and cells, theme selection, layout, damage, and repaint policy above Terminal.

## Protocol reference baseline

Reviewed and pinned on 2026-10-05.

- [Contour appearance reporting specification](https://contour-terminal.org/vt-extensions/color-palette-update-notifications/), repository commit `d8ce17bc34c653a3368d45f52bd7eea67452d673`, document blob `009a502852ebed2f4b0576fe1585fc9d1dc73aaa`: query `CSI ? 996 n`; replies `CSI ? 997 ; 1 n` (dark) and `CSI ? 997 ; 2 n` (light); private mode 2031 enables unsolicited reports with the same reply grammar.
- [In-band resize specification](https://gist.github.com/rockorager/e695fb2924d36b2bcf1fff4a3704bd83), revision `a1e61ea1782326e975b6e4cfe0c538bac54c1f42`: query mode 2048 with DECRQM; enable/disable with DECSET/DECRST. Reports are `CSI 48 ; height_chars ; width_chars ; height_pix ; width_pix t`. Zero pixel fields mean unavailable pixel information. Ignore unknown subparameters according to the specification while validating primary values. Enabling, including re-enabling, requests an immediate size report.

Protocol documentation is not live Icod.Terminal compatibility evidence. In particular, the previously tested Kitty 0.32.2 lane is not qualification for these new features.

## Contract requirements

### Appearance

- Expose a semantic dark/light observation and a truthful unknown/no-observation state. Keep query outcome distinct from the appearance value.
- Never infer appearance from RGB luminance, terminal branding, environment variables, or the host operating system.
- A one-shot query does not silently enable ongoing reporting.
- Reporting is independently acquired and released. Query support does not prove reporting-mode support or successful acquisition.
- Notifications mean an appearance/palette observation, not proof that the application theme changed. Preserve valid repeated same-value reports because a palette update need not change light/dark preference.
- Solicited and unsolicited reports share wire grammar. A response cannot be authenticated as a particular request by an invented request ID; document this limitation and test concurrent query/report routing and timeout recovery.

### Resize

- Expose character dimensions and optional text-area pixel dimensions with explicit observation provenance. Do not substitute outer-window pixels or invent cell dimensions by lossy division.
- Validate numeric ranges, required fields, zero/unknown pixel values, and permitted subparameters without changing existing CSI query grammars.
- Preserve native lifecycle resize handling and existing synchronous `GetSize()`/`GetDimensions()` behavior. An in-band report must not silently turn those host observations into cached wire observations.
- The approved event projection is an additive semantic resize observation through `ReadEventAsync(...)`; native resize remains a lifecycle event.
- Do not republish a single in-band report as both a semantic and native lifecycle event. Native and in-band observations can independently describe the same resize; document source-aware reconciliation rather than promise impossible total ordering between host signals and terminal bytes.
- Do not discard a pixel-only change merely because rows and columns are unchanged. Do not convert a resize into process suspend/resume or a broad raster-generation invalidation.
- Initial reporting works without an OS lifecycle source. Lack of in-band support leaves the existing native path intact.

### Reporting ownership, uncertainty, and cleanup

- Establish support and observable prior mode state before promising exact restoration. Define DECRQM values 0/1/2/3/4, malformed replies, and silence explicitly for each facility.
- Unknown baseline state must not produce an exact-restoration lease. Permanent-set and permanent-reset states need explicit behavior, not an unconditional toggle.
- Nested/concurrent acquisitions share session-local ownership; releasing one owner must not disable another. The last release restores only the baseline actually captured and owned.
- Acquire, release, suspend, resume, invalidation, endpoint loss, and disposal participate in established state-composition ordering. Never await a reply while holding a lock/gate needed by the input router or query writer.
- Specify re-entry order against suspended query transactions. Renegotiation requiring live queries must not run while the query manager is suspended.
- Bound negotiation time, retained observations, pending queries, and event buffering. Define overflow/coalescing policy explicitly without introducing a global event-system rewrite.
- Reports contain no wire generation token. Invalidate local knowledge on lifecycle loss and do not present retained observations as current; document that untagged delayed bytes cannot always be distinguished from fresh observations.
- Cleanup failures are surfaced; successful byte emission does not prove remote restoration. No blind retry after ambiguous committed output.

## Source and test map

These are reviewed integration points, not permission for broad refactoring. Confirm exact file placement and freeze new names in T2700.

| Area | Existing files or locations | Planned responsibility |
| --- | --- | --- |
| Session queries | `src/Session/TerminalSession.CsiQueries.cs`, `src/Session/TerminalQueryTransactionManager.cs`, `src/Query/` | Focused appearance/mode query codecs and bounded correlation. |
| Input projection | `src/Input/TerminalInputDecoder.SemanticEvents.cs`, `src/Input/TerminalSemanticEvent.cs`, `src/Input/TerminalEvent.cs` | Additive appearance/resize semantic payloads without changing existing enum values. |
| Reporting state | `src/Session/TerminalSession.InputProtocols.cs`, `src/Input/TerminalInputProtocolManager.cs`, `src/Session/TerminalSession.LifecycleParticipants.cs` | Follow existing ownership patterns; place new protocol-specific logic in focused files. |
| Lifecycle and dimensions | `src/Session/TerminalSession.Lifecycle.cs`, `src/Session/TerminalSession.Screen.cs`, `src/Session/TerminalSession.Geometry.cs` | Preserve native behavior and qualify interactions; avoid silently replacing its authority. |
| Tests | `tests/Icod.Terminal.Tests/src/Input/`, `src/Session/`, `src/Query/` beneath that test project | Fragmentation, correlation, ownership, lifecycle, and public-contract fixtures. |
| Consumer acceptance | `samples/Icod.Terminal.Compatibility.Sample/`, `samples/README.md`, `docs/compatibility/` | Focused new scenarios, safe launchers, reviewed evidence and deterministic matrices. |
| Packaging | `packaging/VerifyPackageContractShard.ps1`, `packaging/VerifyReleaseLinePackage.ps1`, `packaging/VerifyPublicApiBaseline.ps1` | Fresh-package usage, additive API qualification, coherent stable metadata. |

## Review focus

1. Appearance reports racing a query or arriving after timeout: no duplicate delivery, false causal attribution, or ordinary-input corruption (T2701/T2702/T2706).
2. Reporting already enabled before acquisition: disposal must not disable the previous owner (T2703/T2705).
3. Pixel-only resize, unavailable pixels, and native/in-band disagreement: preserve facts and provenance without fabricating geometry (T2704/T2706).
4. Suspend/resume while a negotiation or cleanup is pending: bounded completion and no query/output/state-lock deadlock (T2706).
5. Bursts of reports mixed with keyboard input: bounded retention and no loss caused by misclassifying ordinary input (T2701/T2706).

## Development sequence

Each implementation tranche uses failing regression tests first, a witnessed red run, the smallest implementation, a green focused run, and a coherent commit. Do not mark a tranche complete solely because code was written.

### T2700 — Baseline, detailed design, and API-regret gate

- [x] Record the tagged 1.26.0 source, package/symbol hashes, API fingerprint, dependencies, and exact baseline CI results.
- [x] Pin the two protocol references and map current mode-query, parser, query-manager, semantic-event, lifecycle and lease behavior.
- [x] Review and approve the detailed design under `docs/superpowers/specs/`; approval was recorded on 2026-10-05.
- [x] Write the task-level implementation plan under `docs/superpowers/plans/`; maintainer approval was recorded on 2026-10-05.
- [x] Freeze exact public names/signatures, additive enum values, event projection, mode-state table, numeric/buffer/deadline bounds, correlation limits, and suspend/re-entry order.
- [x] Name every new source/test file and define the failing fixtures and exact verification commands before implementation.
- [x] Obtain maintainer approval of the implementation plan; then introduce the `1.27.0-alpha.1` development identity with matching metadata.

**Acceptance:** A reviewed, executable design exists with no unresolved ownership or compatibility decision. This planning PR alone does not satisfy the gate.

### T2701 — Bounded protocol parsing and routing fixtures

- [x] Add fixtures for appearance replies, mode responses and resize reports, including every byte split, concatenated controls, and interleaved input.
- [x] Cover invalid/missing fields, numeric overflow, unexpected private markers/finals, and permitted resize subparameters.
- [x] Prove malformed or incomplete reports cannot complete an unrelated query or consume following valid input.
- [x] Implement focused private codecs/recognizers and verify existing CSI, keyboard, semantic-notification, and geometry tests remain unchanged.

**Acceptance:** Valid frames have one owner; parsing is bounded and recovery preserves subsequent input.

### T2702 — One-shot appearance observation

- [x] Add public query-result fixtures covering dark, light, timeout, unavailable endpoint, cancellation, and malformed/unknown values.
- [x] Implement the bounded semantic query through the existing transaction manager.
- [x] Test one-shot operation with reporting disabled, concurrent queries, a racing unsolicited observation, and late response behavior.
- [x] Document that a received value is an observation, not proof of uniquely correlated causation or host theme.

**Acceptance:** Consumers can request appearance without enabling reporting, guessing, or introducing another reader.

### T2703 — Appearance reporting ownership

- [x] Add fixtures for observed baseline enabled/disabled, permanent mode states, unsupported response, and unknown baseline.
- [x] Implement independently acquired reporting and additive semantic appearance events.
- [x] Exercise nested and concurrent owners, out-of-order release, failed enable, failed restore, and repeated same-value notifications.
- [x] Verify mode writes use session serialization and previous mode ownership is preserved.

**Acceptance:** Reporting is opt-in and composable, and restoration promises match captured evidence.

### T2704 — In-band resize observations

- [x] Add fixtures for initial size, row/column changes, pixel-only changes, unavailable pixels, and documented subparameters.
- [x] Implement validated typed resize payloads and the reviewed event projection.
- [x] Test operation without a native lifecycle source and preservation of synchronous dimensions APIs.
- [x] Demonstrate that one wire report is not emitted twice through separate event families.

**Acceptance:** Consumers receive bounded text-area observations without changing native resize semantics.

### T2705 — Negotiated resize reporting ownership

- [x] Add fixtures for each mode 2048 state, timeout, cancellation, immediate initial report, and re-enable report.
- [x] Implement support negotiation and mode acquisition with independent ownership from appearance reporting.
- [x] Test nested owners, already-enabled baseline, last-owner cleanup, partial failures, and failed enable.
- [x] Confirm unsupported/unknown acquisition leaves the native resize path intact and does not claim live reporting success.

**Acceptance:** Mode negotiation, observation, and remote state certainty remain distinct.

### T2706 — Cross-feature lifecycle and concurrency hardening

- [x] Test every acquire/release combination with both modes active and with existing input/presentation leases.
- [x] Test suspend, resume, invalidation, disposal, input EOF, endpoint failure, and cancellation during negotiation or cleanup.
- [x] Pin the query/state/output lock order with deterministic interleaving fixtures, not timing-dependent sleeps.
- [x] Test native/in-band disagreement, duplicate observations, unknown-to-known pixels, report bursts, and interleaved keyboard/query traffic.
- [x] Verify bounded retention, stale-state handling, failure aggregation, and documented untagged late-report limitations.

**Acceptance:** No new deadlocks, unbounded queues, fabricated freshness, or changes to existing lifecycle guarantees.

### T2707 — Public-only sample and compatibility scenarios

- [x] Add a public-only environment-awareness walkthrough with bounded duration, clean exit, cancellation, and deterministic lease disposal.
- [x] Extend the existing compatibility sample with separately revisioned appearance query, appearance reporting, and in-band resize scenarios.
- [x] Keep help/list/describe headless; provide cmd/sh launchers with exact emulator/OS/transport identity.
- [x] Obtain explicit consent before enabling reporting; ask the operator to change theme or resize only after negotiation.
- [x] Keep raw keystrokes, environment dumps, host identity, and arbitrary reply bytes out of evidence.
- [x] Preserve historical 1.26 evidence and scenario revisions; generate the new versioned matrix separately.

**Acceptance:** A package consumer can reproduce the success conditions without source internals or raw escapes.

### T2708 — Package, API, documentation, and regression qualification

- [x] Run focused and complete tests on all three target frameworks and supported CI operating systems.
- [x] Add a fresh-package consumer exercising query, acquisition, event payloads, and cleanup with controlled transports.
- [x] Verify additive public API, unchanged existing enum values, XML documentation, dependency graph, licenses, and downstream DCurses compatibility.
- [x] Update README, sample index, architecture/input/query/lifecycle guidance, security notes, changelog and curated release notes for actual delivered behavior.
- [x] Add or extend a gate that checks version metadata, packed README, curated notes, and all release-line required tokens together.

**Acceptance:** Runtime and distribution checks agree; documentation does not claim unobserved emulator support.

### T2709 — Focused live-terminal acceptance

- [ ] Record a successful bounded appearance query and an operator-induced appearance reporting change on an exact supporting environment.
- [ ] Record initial and changed in-band resize observations on an exact supporting environment, including unknown-pixel behavior where observable.
- [ ] Exercise a missing/unsupported-reporting lane while preserving native resize and ordinary input behavior.
- [ ] Test one mediated lane if available; report it separately and leave unavailable lanes NotRun.
- [ ] Review reports and regenerate the 1.27 matrix; retain Fail/Inconclusive outcomes rather than converting them to unsupported.
- [ ] If a positive live witness cannot be obtained for either selected feature, present that gap to the maintainer before stable release; do not silently waive acceptance or infer success from CI.

**Acceptance:** Both features have a positive live witness and the fallback path has evidence. This does not require all ten emulators to implement either protocol.

### T2710 — Stable release closure

- [ ] Reconcile every tranche, open defect, compatibility limit and main-roadmap status.
- [ ] Set stable 1.27.0 metadata only after acceptance; verify empty prerelease suffix, README/install version, changelog and `docs/releases/1.27.0.md`.
- [ ] Verify package release notes include `1.27.0`, `docs/releases/1.27.0.md`, and `Compatibility-and-Versioning.md`; inspect the packed README against the complete current release-line contract.
- [ ] Run the entire runtime/source-integration/sample/package/API/matrix/artifact workflow at one exact stable candidate.
- [ ] Record source SHA, workflow IDs, test counts, failures and same-head reruns, package/symbol hashes, API fingerprint, and dependencies.
- [ ] Present the candidate for maintainer review. Merge, tag, GitHub release, and NuGet publication remain separate maintainer actions.

**Acceptance:** One exact stable candidate is qualified and documented before tagging; no stale metadata or earlier-head test result substitutes for release validation.

## Execution and verification policy

The detailed T2700 plan must retain inline execution without subagents. Start with focused tests and then run the complete suite, for example:

```sh
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net8.0
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net9.0
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging -f net10.0
```

Zero failures are required. Fresh-package, integration, API and artifact checks supplement these commands; they are not replaced by unit tests. Headless CI cannot certify visible terminal behavior. Use existing workflow invocations and record exact commands/results in each tranche checkpoint.

## Explicit non-goals

No OSC 5522, OSC 21, OSC 1337 ReportCellSize backend, host-theme adapter, palette mutation, automatic theme selection, background polling service, raw protocol extension API, multiplexer passthrough, PTY hosting, scene/layout ownership, broad query-router rewrite, or graphics feature. Findings outside the selected scope are recorded for later decisions.

## Evidence log

| Checkpoint | Evidence | State |
| --- | --- | --- |
| Published baseline | Maintainer publication confirmation; v1.26.0 at `2c0fafaf7d7a4f6a3cbdad206960f159fb19ef95`; release workflow `37337222940`; package hashes in this roadmap and the detailed design | Recorded and audited |
| T2700 detailed design | `docs/superpowers/specs/2026-10-05-terminal-appearance-resize-awareness-design.md` | Approved by maintainer on 2026-10-05 |
| T2700 implementation plan | `docs/superpowers/plans/2026-10-05-terminal-appearance-resize-awareness.md` | Approved by maintainer on 2026-10-05; executing inline without subagents |
| Scope selection | Options 2 + 3 approved on 2026-10-05 | Recorded |
| Task 1 additive event contract | Remote head `fff453ac8b3d12444f263d431d64d0a768b44c09`; workflow `37350334445` | Runtime Linux, Windows, and macOS passed; public-API baseline intentionally deferred to T2708 |
| Runtime and distribution qualification | Head `0da49e0cb436eb3bc339c93819819b8b0e1bce3b`; [workflow 37453595429](https://github.com/uniblab/Icod.Terminal/actions/runs/37453595429) | All ten jobs passed; T2701–T2708 complete |
| Live acceptance and stable closure | T2709–T2710 checklists above; 1.27 matrix | Pending; every live lane remains NotRun |


## Qualified alpha checkpoint — 2026-10-06

The interrupted work was recovered at `64bb4707e8038699e6ab86ac02c3ec0463b32d3e` (runtime tasks 1–5). Continuation commits are `2c68b06afaf7053de70dfcfedcc207ffdb28b7e3` (lifecycle/concurrency), `806aea634ddadab255ad172c7bad9866997d0aae` (public compatibility scenarios), and `0da49e0cb436eb3bc339c93819819b8b0e1bce3b` (package/API/docs).

[Workflow 37453595429](https://github.com/uniblab/Icod.Terminal/actions/runs/37453595429) passed all ten jobs: Windows/Linux/macOS runtime; package candidate; foundation, presentation, semantic/hardening and stable 1.x shards; both compatibility matrices; validated artifact. Each OS passed 2,807 unit tests plus 15 TermInfo integration tests per net8.0/net9.0/net10.0 target. License checks covered 626 C# and 66 project files, including Windows PowerShell 5.1. The API snapshots are byte-identical with fingerprint `356f455ec27065c63a642ae3d5b02125d2aed408d728cb6fadd0d47aeab12487`; the wrong historical baseline was deliberately rejected and the correct baseline passed.

The GitHub Actions synthetic merge commit is `30f10117d9a5139d6162e5aa3068f53f4879eb48`. Its tree exactly matches implementation head `0da49e0cb436eb3bc339c93819819b8b0e1bce3b` (tree `1f444ff2481fefb1b574f8a8782f68f75c4bb499`). CI packages embed the synthetic merge source; the separately built local candidate embeds the implementation head. Keep these identities separate when reviewing live reports.

| Artifact | SHA-256 |
| --- | --- |
| CI validated artifact ZIP (`icod-terminal-pr-packages`, artifact `11408047031`) | `f08932a0975264a2c2a58fad4b001906682c19a8eb7e12d79875ba34af3a3b79` |
| CI `Icod.Terminal.1.27.0-alpha.1.nupkg` | `436da3ebe7c762f4adbf5625f2eea987b6f11c5a5757041d8d413c93f4a1d6d8` |
| CI `Icod.Terminal.1.27.0-alpha.1.snupkg` | `403a28e0e37d6d76788ae1ccce341c80fab4cc8316d4d4083716b9c853ba9923` |
| Local implementation-head `.nupkg` | `d66b1b06f70d8443983dba805fa68faaa0eaf6bff07d2646f3e000f416858a4b` |
| Local implementation-head `.snupkg` | `f57aa347d3f28b1cb4bd5289b02abf29dd2013a9ed15b1f305a7a19d02fb2d1c` |

Dependencies remain Icod.TermInfo 1.17.0 and Icod.Timing 1.0.0. Historical 1.26 evidence/matrix is unchanged. Required live evidence remains missing for appearance query/operator-induced reporting, initial/changed resize, and unavailable/missing reporting with ordinary input/native resize intact. Use the environment cmd/sh launchers in the compatibility sample at the exact source head and review their temporary JSON before acceptance. No stable promotion is authorized by these automated results.
