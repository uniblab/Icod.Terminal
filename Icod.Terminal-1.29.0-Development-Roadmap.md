# Icod.Terminal 1.29.0 Development Roadmap

> **Execution:** Use `superpowers:executing-plans` task by task in the main
> session, without subagents, after maintainer review of this plan and its design.
> Checked items have implementation or regression evidence. Live qualification and
> stable promotion remain separate release gates.

**Goal:** Enhance OSC 52 clipboard transfers for editor-sized selections while
preserving the 64 KiB default and bounded single-session input/output ownership.

**Release theme:** Bounded Clipboard Transfers.

**Selection:** On 2026-10-10 the maintainer selected clipboard enhancements as
the next Terminal milestone before DCurses/Widgets editor work, and explicitly
confirmed that 64 KiB is the correct default with larger buffers opt-in.

**Status:** Approved and implemented as the `1.29.0-alpha.1` candidate in PR #87.
See [qualification](docs/Clipboard-Transfer-Qualification-1.29.md) for automated
results, measured allocation and outstanding live/release gates. Nothing has been published.

**Architecture:** Add immutable session clipboard read/write budgets and propagate
them through private encoding and correlated response accumulation. Keep ordinary
input bounds and other protocol response budgets intact. Retain the authoritative
input reader, query transaction manager, serialized output and lifecycle ownership.

**Tech Stack:** C# 13; net8.0/net9.0/net10.0; managed code; existing
Icod.TermInfo 1.17.0 and Icod.Timing 1.0.0 dependencies; cmd/sh/PowerShell 5.1.

**Spec:** [Bounded Clipboard Transfers design](docs/Clipboard-Transfer-Design-1.29.md).

## Global constraints

- Default decoded read/write capacity: 65,536 bytes (64 KiB), unchanged.
- Approved explicit opt-in ceiling: 16,777,216 bytes (16 MiB).
- Budgets count bytes; text uses strict UTF-8 without BOM.
- Existing clipboard methods and return types remain source/binary compatible.
- Empty write still explicitly clears the selection; never silently truncate.
- Ordinary input ceiling stays 87,400 bytes; paste chunk default stays 4,096 bytes.
- Session policy is local resource authorization, not remote capacity negotiation.
  OSC 52 has no standard maximum-size handshake; no branding-based capacity claims.
- No OSC 5522, native OS clipboard, new production dependency, public streaming
  API, automatic clipboard access or unrelated graphics changes.
- DLL LGPL-3.0-or-later; tests/samples GPL-3.0-or-later; preserve prescribed headers.
- Inline execution, no subagents. Maintainer merges, tags and publishes.

## Review focus

1. Multibyte text near a byte limit and equal-length base64 blocks must enforce
   decoded-byte capacity exactly (T2901/T2902).
2. Correlated malformed and over-budget replies must not grow unrelated buffers
   or become keyboard input. Wrong-selection frames retain legacy ordinary handling
   and receive no larger allowance (T2903/T2904; implementation ruling).
3. Cancellation/late replies/disposal must preserve correlation and release storage
   without hanging the caller (T2904).
4. Large single-byte-fragmented responses must avoid quadratic scanning and
   excessive retained allocations (T2903/T2905).
5. Sample users must not lose clipboard contents without an explicit operation,
   or have clipboard data copied into logs/evidence (T2906).

## Baseline and source map

Baseline main: `2c410cd80663fef30912079e2c0de7a550852b97`.
[GitHub release v1.28.0](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.28.0)
was published 2026-10-09. Preserve its evidence and API snapshot.

| Existing path | Responsibility/change |
| --- | --- |
| `src/Session/TerminalSessionOptions.cs` | Add validated immutable clipboard options |
| `src/Session/TerminalSession.Clipboard.cs` | Apply write budget and strict text preflight |
| `src/Session/TerminalSession.ClipboardQueries.cs` | Build explicit per-query read budget |
| `src/Output/TerminalOsc52PayloadCodec.cs` | Parameterized bounds and checked base64 geometry |
| `src/Output/OscWriter.Osc52.cs` | Bounded encoding without intermediate base64 string |
| `src/Query/TerminalOsc52Protocol.cs` | Budget-aware correlated response validation |
| `src/Query/TerminalQueryResponsePlan.cs` | Carry clipboard-only capacity without broadening generic rules |
| `src/Input/TerminalInputDecoder.cs` | Feed recognized clipboard accumulation from the single reader |
| `src/Input/TerminalInputDecoder.Responses.cs` | Correlation, overflow draining and reader recovery |
| `src/Input/TerminalResponseFramer.cs` | Preserve ordinary family ceilings; integrate dedicated clipboard path |
| `src/Session/TerminalQueryTransactionManager.cs` | Carry budget/ownership through timeout and late replies |
| `src/Session/TerminalSession.Input.cs` | Keep ordinary public input bound; clarify its documentation |
| `src/Session/TerminalSession.Queries.cs` | Reuse explicit response-plan transaction entry point |
| `samples/Icod.Terminal.Clipboard.Sample/Program.cs` | Public-only opt-in examples |
| `samples/Icod.Terminal.Compatibility.Sample/ClipboardCompatibilityScenarios.cs` | Consented evidence and truthful outcomes |
| `tools/package-clipboard-smoke/` | Fresh-package API/runtime witness |

Proposed new files:

- `src/Clipboard/TerminalClipboardOptions.cs`: public immutable budgets.
- `src/Clipboard/TerminalClipboardLimits.cs`: internal checked size calculations.
- `src/Input/TerminalClipboardResponseAccumulator.cs`: internal bounded state
  for an already-correlated response; no independent reader.
- `tests/Icod.Terminal.Tests/src/Session/TerminalClipboardOptionsTests.cs`
- `tests/Icod.Terminal.Tests/src/Input/TerminalClipboardCapacityTests.cs`
- `tests/Icod.Terminal.Tests/src/Input/TerminalClipboardAllocationTests.cs`
- `docs/Clipboard-Transfer-Qualification-1.29.md`: measured/live evidence.
- `docs/Public-API-Baseline-1.29.md` and matching hash during implementation.

Preserve existing focused OSC 52/session tests; add cases there rather than
replacing their historical regression coverage.

## T2900 — Contract freeze and development identity

**Consumes:** the design and current public/session/query contracts.
**Produces:** reviewed API, bounds and recovery contracts; 1.29.0-alpha.1 identity.

- [x] Review the proposed `TerminalClipboardOptions` names, independent read/write
  budgets, inclusive 1..16 MiB range and default compatibility with the maintainer.
- [x] Trace codec, query rule, ordinary input and discard limits from the baseline.
  Record every coupling that must change; identify existing small-buffer fixtures.
- [x] Prove with a minimal internal test fixture that a correlated response can
  accumulate past the ordinary-input ceiling without increasing unrelated limits.
  Resolve ownership/recovery design before building the public feature.
- [x] Freeze accumulator overflow/EOF/late-response transitions and allocation
  accounting. If the prototype contradicts the design, amend the design for review.
- [x] Establish 1.29.0-alpha.1 in `Directory.Build.props` and development identity
  tests when implementation begins. Preserve 1.28 historical API/evidence files.

**Gate:** no runtime work beyond the approved internal proof until the public and
reader-ownership contracts have been reviewed. No protocol capacity handshake is promised.

## T2901 — Options and checked byte geometry

**Files:** new options/limits classes; session options; options/codec tests.
**Produces:** `TerminalClipboardOptions` from the design and internal
`GetEncodedLength(int decodedBytes)` / `GetResponseFrameLimit(int maximumDecodedBytes)`
helpers in `TerminalClipboardLimits`.

- [x] Add failing options tests: defaults equal 65,536; independent 1 MiB read and
  write settings; 0, -1 and 16,777,217 rejected; null options rejected; no output or
  terminal mode mutation on invalid session settings.
- [x] Add geometry tests for 0, 1, 2, 3, 65,535, 65,536, 65,537, 1 MiB and 16 MiB,
  including expected E/F values in the design and integer-overflow rejection.
- [x] Verify the new tests fail, then implement validation/immutable effective
  budgets and checked arithmetic without altering old default constants.
- [x] Run options/codec tests on all targets; review default-session behavior; commit.

**Gate:** defaults and hard ceilings are explicit and no large eager allocation occurs.

## T2902 — Larger bounded writes

**Files:** clipboard session methods, codec, OSC writer;
`TerminalSessionClipboardTests.cs`, `TerminalSessionClipboardOrderingTests.cs`,
`Osc52WriterTests.cs`, `TerminalOsc52PayloadCodecTests.cs`.

**Consumes:** validated write budget and T2901 geometry.
**Produces:** existing write methods honoring session capacity with unchanged completion semantics.

- [x] Add failing cases: default accepts 65,536/rejects 65,537; explicit 1 MiB and
  hard-ceiling writes; one byte over each budget; empty clear; invalid selection;
  malformed surrogate text; UTF-8 multi-byte boundaries.
- [x] Assert oversize/invalid inputs emit nothing and do not allocate an output frame.
  Assert binary/text exception categories remain those documented in the design.
- [x] Encode directly into bounded frame storage; preserve one serialized logical
  OSC operation and existing public signatures. Never split content into multiple
  replacement writes as an attempted append mechanism.
- [x] Test cancellation before output gate acquisition and before commitment, then
  cancellation after commitment, competing screen output and transport failure.
- [x] Run targeted write/order tests on all targets and commit.

**Gate:** output remains complete and ordered; success reports emission only.

## T2903 — Correlated larger reads

**Files:** query protocol/plan, clipboard query method, decoder/framer integration,
new accumulator; `TerminalClipboardCapacityTests.cs`,
`TerminalOsc52QueryTests.cs`, `TerminalOsc52RoutingTests.cs`.

**Consumes:** validated read budget, existing query ownership and T2901 geometry.
**Produces:** unchanged `ReadClipboardAsync` result contract with clipboard-specific capacity.

- [x] Add failing default-limit and explicit larger-limit read tests through the
  session API, including empty/binary data and exact returned-byte equality.
- [x] Test 1-byte fragmentation, split introducer/selection/base64 padding/terminator,
  and both existing supported OSC termination forms. Verify one incremental scanner feed per input byte (see implementation ruling).
- [x] Introduce the internal incremental accumulator behind complete correlated
  OSC 52 selection-prefix recognition. Feed it from the existing reader and
  attach it to the active/late query identity.
- [x] Build the clipboard-specific response plan explicitly through the existing
  transaction entry point. Keep generic `ForCompletion` and unrelated protocol
  budgets at their pre-feature effective values.
- [x] Validate actual decoded bytes as well as encoded/frame length; reject
  over-budget payloads that share a base64 block length with an allowed payload.
- [x] Test wrong selection, unsolicited OSC, concurrent sessions with different
  budgets, ordinary text/keys/focus/paste before and after replies.
  Preserve ordinary-input options and existing small-buffer behavior.
- [x] Run read/routing/input regression tests on all targets and commit.

**Gate:** larger replies neither require a larger ordinary-input buffer nor permit
unrelated protocol frames to consume the clipboard allowance.

## T2904 — Failure, cancellation and lifecycle

**Files:** accumulator, response decoder, query transaction manager as needed;
`TerminalOsc52OversizedRoutingTests.cs`, clipboard ordering and query tests.

**Consumes:** T2903 query-bound accumulator.
**Produces:** tested completion, overflow, abandoned-query and disposal cleanup.

- [x] Add failing tests for invalid base64, illegal padding, decoded/frame overflow,
  missing terminator, EOF mid-response and transport disconnect.
- [x] Test queue-inclusive timeout before emission: no request bytes. Test timeout
  after emission and late reply followed by a second same-selection query.
  The late reply must never satisfy the second query.
- [x] Enforce bounded scratch-space discard and the reviewed per-query recovery
  allowance. Test input resumption after a valid terminator and failure closed
  after recovery exhaustion; never return payload fragments as keystrokes.
- [x] Test caller cancellation during fragmented input, lifecycle suspension,
  session disposal and concurrent clipboard/screen operations.
- [x] Verify no clipboard payload appears in exceptions or diagnostics; assert
  accumulator buffers and query ownership are released at terminal outcomes.
- [x] Run targeted failures plus existing multi-family transaction tests and commit.

**Gate:** no partial success, stale-result substitution, second input reader or
unbounded recovery storage; existing timing guarantees remain intact.

## T2905 — Allocation and performance qualification

**Files:** `TerminalClipboardAllocationTests.cs`, qualification document.
**Consumes:** implemented transfer paths and deterministic fixture transports.

- [ ] Measure 64 KiB, 1 MiB and 16 MiB reads/writes, plus long fragmented responses;
  include default idle sessions and invalid over-budget calls.
- [x] Verify lazy options/accumulator allocation and rejected-write allocation;
  review accumulator disposal/clearing and no intermediate base64 string on writes.
- [x] Establish linear scanner work and geometric allocation bounds through
  component measurements, source review and one-byte-fragmented session tests.
  Pin deterministic budgets to the measured implementation; do not use fragile
  wall-clock thresholds as unit-test pass criteria.
- [ ] Record allocation totals, peak working storage, runtime/OS/architecture and
  elapsed time. Compare default-path results with baseline 1.28.
- [x] Resolve regressions and document the cost of a complete output frame, including
  the non-cancellable post-commit write interval; commit evidence and tests.

**Gate:** opt-in capacity has demonstrated bounded cost; defaults do not inherit
the high-capacity allocation footprint.

## T2906 — Public sample and compatibility evidence

**Files:** clipboard/compatibility samples, their tests, qualification document.

- [x] Add sample options `--maximum-read-bytes` and `--maximum-write-bytes`
  with validated decimal byte counts and documented defaults. Keep existing modes.
- [x] Offer generated payload fixtures above 64 KiB; require explicit action before
  writing or reading the clipboard. Do not automatically inspect/restore prior
  contents, print payloads, or embed them in reports.
- [x] Add sample tests showing headless/help paths do not access the clipboard,
  limits are applied independently, and over-budget input explains local failure.
- [x] Report emitted bytes separately from decoded read results and operator
  observations. Timeout/unavailable policy is not a positive/negative support proof.
- [ ] Run public-only sample checks and consented live scenarios in available
  terminals. Record exact terminal/version/transport/commit and tested sizes,
  including default transfer, larger transfer and graceful refusal/timeout.
- [x] Mark missing environments NotRun; retain honest limits and commit evidence.

**Gate:** consumers have runnable examples and precise operational expectations.

## T2907 — API, package and downstream qualification

**Files:** public API snapshot/hash, package clipboard smoke tool, existing
package verifier/shards, README, CHANGELOG, security/compatibility docs.

- [x] Update fresh-package smoke coverage for default and custom budgets through
  public APIs only. Verify dependency graph, XML docs and license/header gates.
- [x] Generate identical public API snapshots for net8.0/net9.0/net10.0 and review
  the diff against 1.28 for accidental removals/signature/enum changes.
- [x] Update README with durable examples, UTF-8 byte accounting, local-versus-remote
  limits and fallback guidance. Keep measured qualification in its dedicated doc.
- [x] Record implemented changes in CHANGELOG only once delivered. Add curated
  alpha/stable release notes at the appropriate promotion step.
- [ ] Run full solution, integration, package and existing downstream DCurses gates.
  Confirm ordinary input, graphics, semantic events and clipboard ordering regressions
  remain covered without changing downstream repositories.
- [x] Commit docs/package evidence and report remaining caveats.

**Gate:** the package delivers the same reviewed contract as source, with existing
dependencies and a clear contract for the later DCurses facade.

## T2908 — Release closure

- [ ] Resolve every unchecked implementation gate or explicitly recorded release
  exception; no invented live-platform result.
- [ ] Promote version metadata from alpha to stable 1.29.0 only when authorized.
- [ ] Run complete CI/package verification on the final candidate commit and
  record workflow IDs, API fingerprints and artifact identities.
- [x] Update this roadmap and the main roadmap with actual completion/evidence.
- [ ] Present the reviewed branch to the maintainer for merge/tag/publication.
  Do not perform those actions automatically.

## Verification workflow

For each implementation tranche: add focused failing tests, verify the failure,
implement the smallest coherent change, rerun targeted tests, then commit.
Run commands from repository root:

```text
dotnet build Icod.Terminal.sln -c Release
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Release --no-build
dotnet test tests/Icod.Terminal.TermInfoIntegration.Tests/Icod.Terminal.TermInfoIntegration.Tests.csproj -c Release --no-build
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -c Release -f net10.0 -- --help
```

Targeted unit runs may add
`--filter "FullyQualifiedName~Clipboard|FullyQualifiedName~Osc52"`.
Use the repository's established packaging scripts and CI shard invocation
contracts after inspecting their current parameters; do not invent a parallel
release workflow. Cross-platform automated results and live-terminal observations
are recorded separately.

## Implementation checkpoint

The maintainer approved the design and implementation. T2900–T2904 are delivered;
regressions include default/custom byte limits, 16 MiB transfers, fragmentation,
small ordinary buffers, malformed/oversized replies, recovery exhaustion,
timeout, cancellation, EOF, disposal and independent sessions.

T2905 component allocation/scanner qualification is complete. Whole-session
peak-memory profiling across operating systems remains a release follow-up;
the recorded peak is an analytical live-storage bound, not measured process RSS.
T2906 samples and consented scenario are delivered; all live environments remain
NotRun. T2907 local solution, integration, fresh-package and downstream acceptance
are recorded in the qualification document; final GitHub matrix status belongs
to PR #87. Unchecked measurement/live/CI items are explicit release follow-ups,
not claims of completed qualification.

The remaining release decision is maintainer review, any accepted qualification
exceptions, stable promotion, merge and publication. DCurses/Widgets implementation
follows the accepted Terminal contract.
