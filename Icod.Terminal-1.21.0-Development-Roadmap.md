# Icod.Terminal 1.21.0 — Rich Input and Cursor Visibility Development Roadmap

**Goal:** Close demonstrated keyboard/rich-input consumer gaps and let a screen-output transaction temporarily compose cursor visibility with a frame while preserving presentation-lease ownership.

**Status:** The stable 1.21.0 candidate passed its nine-job matrix in PR #66. The published baseline remains 1.20.0 until the maintainer merges and publishes 1.21.0.

**Tech stack:** C# 13; .NET 8, 9, and 10; PowerShell 5.1-compatible packaging scripts and cmd/sh. No new tooling dependency.

**Selected scope:** **Rich input and keyboard expansion** + **Cursor-visibility composition with screen transactions**.

## Baseline and ownership

- 1.20.0 already provides traditional key normalization, Kitty progressive reporting (`Disambiguated`, `EventTypes`, `AllKeys`), xterm `modifyOtherKeys` decoding, key phases, modifier flags, shifted/base-layout characters and associated text. Mouse, focus, and bounded bracketed-paste frames already use the one `ReadEventAsync` path. This release must add a demonstrated capability, not duplicate those contracts.
- `AcquireInputProtocolsAsync` owns reversible reporting modes. One session reader routes active query replies before unsolicited semantic events and ordinary input. Traditional input remains the fallback when modern keyboard activation is unavailable.
- Presentation leases own persistent cursor visibility; the newest active cursor request wins, and release restores the next owner or normal capability. Screen transactions own bounded, single-use output commitment and capture an output epoch. A failed commit can emit a prefix and cannot be rolled back.
- `Icod.TermInfo` supplies capability data. Terminal owns decoding, session-level state composition, planning, and output commitment; DCurses retains cells, coordinates, layout, damage, and repaint policy. PTY hosting remains separate.
- Compatibility floor: 1.0.0 public signatures and existing enum values remain intact. The 1.20 baseline API fingerprint is `d308fb6ead5bd24c564d159297e6d08793c08eb4db21418eca6a5563aa5c4cbf`. Production dependencies remain `Icod.TermInfo 1.16.0` and `Icod.Timing 1.0.0`; optional Inspection remains 1.16.0 unless a separately justified change is approved.

## Release outcomes

| Track | Concrete outcome | Acceptance evidence |
| --- | --- | --- |
| Rich input and keyboard | At least one audited downstream keyboard/rich-input gap gains a semantic, bounded event or normalization path through the existing reader. Existing reported fields, paste framing, and traditional fallback remain correct. | A before/after failing consumer case, byte-level decoder tests, package-only consumer, and lease/lifecycle tests. T2100 freezes the exact gap and public spelling before implementation. |
| Cursor visibility and transactions | An opt-in transaction-scoped visibility request hides or otherwise selects the cursor presentation for one frame and restores the presentation manager's effective owner after commitment or failure. Persistent visibility still uses leases. | Ordered recording-output tests with overlapping leases, stale epoch, cancellation, transport failure, cleanup failure, and lifecycle races. |
| Consumer guidance | A runnable example combines input-driven refresh with transaction-scoped visibility and states when to use a persistent presentation lease. | Headless tests execute the actual example from a fresh package; README/docs and public API/XML gates pass. |

The visibility contract is **one temporary visibility scope around a transaction's output**: enter before the first frame item, leave after the last item, including failure cleanup. This is not an unrestricted raw cursor plan or a permanent lease mutation. T2105 freezes the minimal public API and exactly which enum values can be requested; unsupported entry or restoration yields a controlled result before output. If full `VeryVisible` composition cannot be made reversible for a selected profile, limit the request to the capability-backed subset rather than guessing terminal state.

For keyboard expansion, T2100 must produce a concrete fixture and downstream consumer before API freeze. Priorities are: previously lost semantic key distinctions in supported traditional/modern forms; bounded handling of associated text and phase combinations in a real editor/game input loop; and a demonstrated rich-input framing gap if the keyboard inventory is complete. Specify the chosen wire forms, public event projection, and fallback in that task. Do not create a generic vendor payload or enable an un-restorable protocol to meet a feature count.

## Sequence and acceptance

### T2100 — Baseline, consumer gap, and contract freeze

**Inspect:** `docs/Input-and-Events.md`, `docs/Modern-Keyboard-Security-and-Compatibility.md`, `docs/Presentation-and-Reversible-State.md`, `docs/Screen-Output.md`; `src/Input/TerminalInputEvent.cs`, `TerminalInputDecoder.ModernKeyboard.cs`, `TerminalInputDecoder.TraditionalKeys.cs`, `TerminalInputProtocolManager.cs`; `src/Presentation/TerminalPresentationManager.cs`; `src/Screen/TerminalScreenOutputTransaction.cs`; existing DCurses acceptance harness and samples.

- [ ] Capture exact 1.20 source, package/API baseline, supported frameworks, CI matrix, and already supported rich-input forms.
- [ ] Demonstrate a user-visible input gap with an actual downstream scenario and a failing byte/event fixture; freeze at least one bounded addition with a semantic result and traditional fallback. Record why alternatives are deferred.
- [ ] Draw lock/order and epoch boundaries for presentation manager, state-composition gate, synchronized/hyperlink managers, and screen output gate; freeze the transaction visibility API, unsupported result shape, and allowed values without a raw capability or lease bypass.
- [ ] Update the task inventory if evidence changes spelling or file placement, preserving both selected outcomes. Set prerelease metadata only when implementation begins.

**Acceptance:** Reviewed input fixture and visibility state/ordering table, additive API proposal, and no guessed baseline or duplicate current feature.

**Contract decision (implementation checkpoint):** The input increment is Kitty's legacy-functional CSI forms with event-type suffixes, for example `CSI 1;1:3 D` (Left release) and `CSI 2;5:2 ~` (Control+Insert repeat). These forms carry semantic phases in the existing `TerminalInputEvent` shape; the 1.20 decoder only recognizes `CSI u` and xterm modifyOtherKeys `CSI ~` and otherwise falls through to terminfo. A text editor needs the release/hold distinction for navigation. Decode the fixed Kitty functional-key alphabet `A B C D E F H P Q S` and the standard numeric `~` keys with an explicit `modifier:phase`; exclude `R` (cursor-position reply ambiguity), unknown keys, and frames without a phase suffix. The existing 4,096-byte frame limit and `TerminalKey`/`TerminalKeyEventPhase` are sufficient; there is no new public input type or protocol mutation. Canonical CSI-u and traditional terminfo keys retain their current paths. The upstream [Kitty keyboard grammar](https://sw.kovidgoyal.net/kitty/keyboard-protocol/) is the wire reference; tests must show failing bytes and recover after invalid frames.

**Visibility decision:** Add a single optional transaction builder request `SetCursorVisibilityForCommit(TerminalCursorVisibility visibility)`. Its setting is side-effect-free and one request surrounds all existing frame items. `CommitAsync` rejects unsupported entry or return capabilities with `InvalidOperationException` before writing, matching existing transaction conventions. The effective prior cursor value comes from the presentation manager, never from a guessed physical read. Acquire the shared state-composition reservation and presentation manager's gate before the hyperlink and synchronized-output reservations and the screen output epoch gate. The manager reservation must never reacquire its own gate or the output gate while writing under the screen output lease. For a known presentation state, enter the requested capability; restore the manager's effective cursor owner or ordinary capability after the frame. On failure, attempt uncancelled restoration, report both frame and cleanup failures, and invalidate manager certainty if restoration cannot be established. Persistent cursor requests stay lease-owned. This design does not claim physical-terminal rollback.

**Environment:** The planning workspace has no local `dotnet` or PowerShell executable. Record focused RED/GREEN evidence and full matrix from the repository's GitHub Actions workflow at exact commit heads. Do not mark a gate complete merely because source review predicts it.

**T2100 baseline:** Parent `main` is `8aa6d0543a3d48d6ec28c84f930da35282703b4a` (merged PR #65); `v1.20.0` is published. Existing input includes terminfo navigation/function/modifier decoding, Kitty CSI-u press/repeat/release and associated text, xterm modifyOtherKeys decode-only, SGR mouse, focus, and bracketed paste. Existing screen transactions are single-use, epoch-bound, and capped at 65,536 items/64 MiB. Current release matrix is net8/net9/net10 on Windows/Linux/macOS plus the four package shards. No input enum or new protocol negotiation is required for the chosen gap.

| Owner or gate | Existing lock/order | 1.21 visibility obligation |
| --- | --- | --- |
| Shared state composition | Serializes input lease and presentation changes, including screen-local Kitty handoff. | Hold across an opt-in frame; never acquire it from a manager method already under the gate. |
| Presentation manager | Its gate protects the lease set, effective cursor owner, and known/invalidated state. | Reserve before output; validate request and restoration from advertised capabilities; update certainty only after truthful cleanup. |
| Hyperlink and synchronized managers | Their reservations are acquired before the output gate and retained through cleanup. | Keep the existing relative order after the presentation reservation. |
| Session output epoch/gate | Rejects stale frame before writing, then serializes committed bytes. | Emit visibility entry/body/return while holding exactly this output lease; never acquire another output lease within it. |

### T2101 — Input contract and adversarial fixtures

**Likely files:** `src/Input/TerminalInputEvent.cs`, `TerminalRichInputContracts.cs`, relevant decoder contract tests and downstream modern-keyboard witness.

- [ ] Add failing tests for the frozen semantic distinction or rich-input framing case; cover ordinary text vs key events, press/repeat/release, modifiers, associated text where reported, and unknown modern identities without leaking raw private-use codes.
- [ ] Define immutable event shape, invalid combinations, nullability, versioning behavior, and resource limits; preserve existing enum numeric values.
- [ ] Exercise fallback when modern reporting cannot be acquired and interaction with paste, focus, mouse, semantic events, and active query ownership.

**Acceptance:** Tests explain the addition and fail against 1.20 before production changes.

### T2102 — Bounded decoder and normalization

**Likely files:** `src/Input/TerminalInputDecoder.*.cs`, `TerminalInputCoordinator.cs` only if a reproduced routing defect needs it, and `tests/Icod.Terminal.Tests/src/Input/*`.

- [ ] Implement the frozen form through the existing incremental decoder and authoritative event stream; keep traditional and xterm decode-only behavior available.
- [ ] Test all split points, UTF-8 scalar boundaries, malformed/truncated/oversized frames, Escape ambiguity, recovery into a subsequent event, and deterministic routing when a query is pending.
- [ ] Keep existing frame, parameter, associated-text, and paste bounds; introduce explicit new limits only if the selected fixture requires them.

**Acceptance:** New fixtures pass without changing an unrelated query, introducing a second reader, or silently mislabeling ordinary text.

### T2103 — Negotiation, lifecycle, and input ownership

**Likely files:** `src/Input/TerminalInputProtocolManager.cs`, `src/Session/TerminalSession.KittyKeyboard.cs`, existing keyboard composition/lifecycle tests.

- [ ] Verify that the selected behavior works under overlapping reporting leases and supported progressive modes. Add negotiation only if the frozen case needs it and the stack can be restored truthfully.
- [ ] Cover main/alternate screen handoff, suspend/resume, cancellation before/after output, release of the strongest owner, failed restoration, disposal, and stale generation/late query replies.
- [ ] Keep capability evidence, endpoint availability, and the lease's actual acquisition distinct.

**Acceptance:** One-reader ordering and reversible state ownership hold on all three target frameworks; unsupported negotiation falls back to traditional input.

### T2104 — Package-backed input witness

**Likely files:** `samples/Icod.Terminal.RichInput.Sample/Program.cs`, `tools/dcurses-modern-keyboard-acceptance/`, `tools/package-modern-keyboard-smoke/`, associated package verifier and tests.

- [ ] Run the exact new consumer fixture through the public event loop, including unsupported modern reporting; assert phase/text/modifier semantics and unchanged paste/mouse/focus behavior.
- [ ] Use a fresh 1.21 package in the consumer harness; no source-project reference or direct TermInfo in the controlled consumer.

**Acceptance:** The chosen input increment is useful to a real consumer and survives package resolution.

### T2105 — Cursor visibility contract and precommit validation

**Likely files:** `src/Screen/TerminalScreenOutputTransaction.cs`, `src/Presentation/TerminalPresentationManager.cs`, `src/Presentation/TerminalPresentationContracts.cs`, related screen/presentation tests.

- [ ] Add tests for an opt-in visibility scope over one frame: enter, ordered items, restore the effective owner; no visibility request emits the old sequence. Explicitly distinguish an active lease from the manager's ordinary capability baseline.
- [ ] Validate capability-backed entry and restoration, same-session ownership, retained-item limits, stale epochs, already-consumed builders, lifecycle admission, and pre-cancelled commits before writing.
- [ ] Freeze a minimal additive builder API in the current namespace. Decide whether a controlled unavailable result or rejected commit best matches existing transaction conventions; document the choice and null/invalid inputs.
- [ ] Confirm lock ordering and reservation lifetime: presentation/state-composition reservation before output, no manager reacquisition while holding the output gate, and no interleaving acquisition/release during temporary ownership.

**Acceptance:** Unsupported or stale work cannot change visibility or leak a synthetic public lease.

### T2106 — Transactional visibility commitment and cleanup

**Likely files:** `src/Presentation/TerminalPresentationManager.cs`, `src/Session/TerminalSession.Presentation.cs`, `src/Screen/TerminalScreenOutputTransaction.cs`.

- [ ] Reserve the presentation owner and output epoch before first emission; temporarily apply requested visibility, write existing bounded items, restore the previously effective owner, then release reservations.
- [ ] Compose with synchronized output, hyperlinks, raster placeholders, and current transaction limits; preserve manager/output lock ordering and screen-local keyboard handoff.
- [ ] When a write, flush, cancellation-after-start, or restoration fails, make a best-effort uncancelled cleanup attempt and surface all relevant failures in deterministic order. Mark physical assumptions unknown where restoration cannot be proven. Never promise atomic rollback or replay an emitted prefix.

**Acceptance:** Temporary visibility is owned and cleaned up with the same correctness standard as presentation leases and screen transactions.

### T2107 — Adversarial composition and cross-feature tests

**Likely files:** `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenOutputCompositionTests.cs`, `TerminalScreenOutputTransactionHardeningTests.cs`, `tests/Icod.Terminal.Tests/src/Presentation/*`, `tests/Icod.Terminal.Tests/src/Input/TerminalKeyboardCompositionConcurrencyTests.cs`.

- [ ] Matrix: no lease, one lease, nested cursor owners, concurrent acquisition/release, alternate-screen plus Kitty keyboard, suspend/resume, disposal, unsupported normal/hidden/very-visible capabilities.
- [ ] Inject failure at entry, mid-frame, restoration, and flush; verify byte order, reported aggregate errors, no ghost owner, stale transaction consumption, and safe subsequent recovery.
- [ ] Keep zero-item, maximum-item/payload, synchronized-output and hyperlink conflict behavior explicit. Use bounded coordination in tests instead of timing assumptions.

**Acceptance:** Ownership and output are coherent under failure and concurrent lifecycle activity.

### T2108 — Documentation and executed example

**Likely files:** `README.md`, `docs/Input-and-Events.md`, `docs/Presentation-and-Reversible-State.md`, `docs/Screen-Output.md`, `docs/Modern-Keyboard-Security-and-Compatibility.md`, `samples/README.md`, screen-output and rich-input samples.

- [ ] Document which input cases are new versus already supported, reporting prerequisites, fallback, privacy implications of associated text, and unsupported forms.
- [ ] Show an input-driven frame that requests temporary visibility, handles unavailable capability and stale epoch, and repaints from caller-owned state after ambiguous output failure; show a lease for long-lived cursor preferences.
- [ ] Execute the real sample routine headlessly in tests and through the package consumer, including the failure path. Keep interactive demonstrations usable without assuming a physical emulator in CI.

**Acceptance:** Guide and sample teach the exercised public contract, including truthful restoration and recovery limits.

### T2109 — Package, API, and downstream qualification

**Likely files:** `docs/Public-API-Baseline-1.21.md`, corresponding fingerprint file; package contract tools; `docs/releases/1.21.0.md` and changelog when implementation is ready.

- [ ] Compare 1.21 API against the preserved 1.20 baseline for removed/changed signatures and enum values, XML docs for additive members, and equal snapshots across three frameworks.
- [ ] Build one candidate artifact and run foundation, presentation, semantic, and stable-release package shards against those exact bytes. Exercise DCurses 1.6.0/2.2.0, Terminal-only renderer, input witness, screen sample, and existing capability sample where the gate applies.
- [ ] Verify package metadata, dependencies, README, symbols, source/artifact identity, and no TermInfo leak into package-only consumers.

**Acceptance:** Additive, documented package surface with retained downstream compatibility.

### T2110 — Cross-platform review and stable closure

- [ ] Pass full Windows, Linux, and macOS runtime/integration/sample matrix for .NET 8/9/10 at the final implementation head; inspect input routing, manager/output lock ordering, cancellation, and failure reporting inline.
- [ ] Reconcile both selected outcomes against a completed task and named evidence; record scripted-test limits and any explicit deferral.
- [ ] After qualification, set stable 1.21.0 metadata, update changelog/release notes and main roadmap, rerun the final candidate gates, record exact source/CI/artifact/API hashes, and present the PR for maintainer review.

**Acceptance:** A reviewable stable candidate with precise evidence. Merge, tag, GitHub release, and package publication are maintainer actions.

## Verification commands

Run from the repository root with the supported .NET SDK and PowerShell. Existing CI remains the authority for the three-platform result; exact command arguments may be updated when the T2100 API and fixture are frozen.

```powershell
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging --filter 'FullyQualifiedName~TerminalKittyKeyboard|FullyQualifiedName~TerminalRichInput|FullyQualifiedName~TerminalScreenOutput|FullyQualifiedName~TerminalPresentation'
./packaging/VerifyRuntime.ps1 -Configuration Staging
./packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/1.21-package -Configuration Staging
foreach ($shard in @('foundation', 'presentation', 'semantic', 'release')) {
    ./packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.21-package -Configuration Staging -Shard $shard
}
```

## Implementation evidence

| Tranche | Evidence and decision |
| --- | --- |
| T2100–T2102 | The 1.20 decoder lost Kitty phase-bearing functional-key CSI forms. The original fixture failed on all three Linux target frameworks in [workflow 36352906230](https://github.com/uniblab/Icod.Terminal/actions/runs/36352906230), then the bounded semantic decoder landed in `3ad23dd72d0bff7ca40944e0fc7e41be5aa7f916`. Invalid forms drain, and the later oversized-frame fixture exercises recovery into text without expanding the retained frame limit. No public input enum/type or new reporting protocol was added. |
| T2103–T2104 | Existing reporting leases and the authoritative `ReadEventAsync` path own negotiation, fallback, and query ordering. The new functional-key fixture also runs through the fresh-package input consumer alongside traditional, CSI-u, paste, mouse, and focus assertions. No second reader or production Inspection dependency is needed. |
| T2105–T2107 | The only public addition is `TerminalScreenOutputTransaction.SetCursorVisibilityForCommit(TerminalCursorVisibility)`. A state-composition and presentation reservation precedes the existing frame output gate; tests cover lease-owned and ordinary returns, missing capabilities, stale and pre-cancelled frames, failed entry/frame/return, and uncertain-state cleanup. The first failed-return test demonstrated missing disposal retry on all three Linux frameworks in [workflow 36353946489](https://github.com/uniblab/Icod.Terminal/actions/runs/36353946489); `0cfb34799034bcb1a75a217338df98b3a2c962d6` added the retry and bounded-frame drain. An unrelated lease transition cannot establish the outcome of a failed temporary cursor return. |
| T2108 | The screen-output sample now accepts `r` for input-driven refresh with temporary cursor visibility, and falls back when entry/return capabilities are missing. Its actual routine runs in source and fresh-package harnesses. README, input, presentation, modern-keyboard, screen, and sample guides explain limits and recovery. |
| T2109 | The 1.21 public API is additive over the preserved 1.20 baseline. The generated all-framework fingerprint is `939649e1d5c110039cfb3e5057561f8ef7fcb4de20af2de2152f9ec6ab357824`; the 1.21 file and XML gate pin the new method. The candidate and all package shards passed at the preceding [workflow 36354217590](https://github.com/uniblab/Icod.Terminal/actions/runs/36354217590); subsequent final-head qualification is required before closure. |
| T2110 | Stable `1.21.0` metadata, curated release notes, changelog, and README are present. Candidate source `020b82215410c603c590c3ae624170a8a41ef594` passed all nine jobs in [workflow 36354755815](https://github.com/uniblab/Icod.Terminal/actions/runs/36354755815): Windows, Linux, and macOS each passed 2,484 unit and 15 TermInfo integration tests per `net8.0`/`net9.0`/`net10.0`; candidate, foundation, presentation, semantic/hardening, stable 1.x, and validated-artifact jobs all succeeded. The candidate artifact is [10943590769](https://github.com/uniblab/Icod.Terminal/actions/runs/36354755815/artifacts/10943590769); its `Icod.Terminal.1.21.0.nupkg` SHA-256 is `61ce77d4deefad2987ceced40ba16e09d781bfd5f670d2d50b0936de74263fbe`, and its `.snupkg` SHA-256 is `905f31f4acc68217bab4312b92f5dfb79f9e0536d1e7ed3f72cab0addec97e39`. These are candidate bytes, not assertions about a later published package. |

The user-visible phase form and cursor API were frozen before production code. The package harness executes the actual sample from a fresh package and retains the published DCurses 1.6.0 and 2.2.0 plus Terminal-only renderer witnesses. The tests use scripted transports; they are not a physical-emulator or performance certification. Merge, tagging, GitHub release creation, and publication remain maintainer actions.

## Deferred work and release boundary

New query families or a general query router redesign, raw vendor input events, xterm `modifyOtherKeys` mutation without a truthful restore path, global hotkeys, OS/IME control, unbounded paste assembly, terminal-brand heuristics, permanent cursor visibility inside a one-shot transaction, retained-screen/layout/damage policy, animation composition, new image protocols, transport/PTY hosting, and broad public extensibility remain outside 1.21.0. Scripted verification is not physical-emulator certification or a measured performance claim.

**Source authorities:** [main roadmap](Icod.Terminal-Development-Roadmap.md), [input/event contract](docs/Input-and-Events.md), [modern keyboard contract](docs/Modern-Keyboard-Security-and-Compatibility.md), [presentation ownership](docs/Presentation-and-Reversible-State.md), [screen output](docs/Screen-Output.md), and [1.20 release roadmap](Icod.Terminal-1.20.0-Development-Roadmap.md).
