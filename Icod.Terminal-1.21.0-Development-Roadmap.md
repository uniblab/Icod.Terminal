# Icod.Terminal 1.21.0 — Rich Input and Cursor Visibility Development Roadmap

**Goal:** Close demonstrated keyboard/rich-input consumer gaps and let a screen-output transaction temporarily compose cursor visibility with a frame while preserving presentation-lease ownership.

**Status:** Planning in PR for 1.21.0. No 1.21 implementation or release qualification is claimed. The released baseline is 1.20.0.

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
| Rich input and keyboard | At least one audited downstream keyboard/rich-input gap gains a semantic, bounded event or normalization path through the existing reader. Existing reported fields, paste framing, and traditional fallback remain correct. | A before/after failing consumer case, byte-level decoder tests, package-only consumer, and lease/lifecycle tests. T210 freezes the exact gap and public spelling before implementation. |
| Cursor visibility and transactions | An opt-in transaction-scoped visibility request hides or otherwise selects the cursor presentation for one frame and restores the presentation manager's effective owner after commitment or failure. Persistent visibility still uses leases. | Ordered recording-output tests with overlapping leases, stale epoch, cancellation, transport failure, cleanup failure, and lifecycle races. |
| Consumer guidance | A runnable example combines input-driven refresh with transaction-scoped visibility and states when to use a persistent presentation lease. | Headless tests execute the actual example from a fresh package; README/docs and public API/XML gates pass. |

The visibility contract is **one temporary visibility scope around a transaction's output**: enter before the first frame item, leave after the last item, including failure cleanup. This is not an unrestricted raw cursor plan or a permanent lease mutation. T215 freezes the minimal public API and exactly which enum values can be requested; unsupported entry or restoration yields a controlled result before output. If full `VeryVisible` composition cannot be made reversible for a selected profile, limit the request to the capability-backed subset rather than guessing terminal state.

For keyboard expansion, T210 must produce a concrete fixture and downstream consumer before API freeze. Priorities are: previously lost semantic key distinctions in supported traditional/modern forms; bounded handling of associated text and phase combinations in a real editor/game input loop; and a demonstrated rich-input framing gap if the keyboard inventory is complete. Specify the chosen wire forms, public event projection, and fallback in that task. Do not create a generic vendor payload or enable an un-restorable protocol to meet a feature count.

## Sequence and acceptance

### T210 — Baseline, consumer gap, and contract freeze

**Inspect:** `docs/Input-and-Events.md`, `docs/Modern-Keyboard-Security-and-Compatibility.md`, `docs/Presentation-and-Reversible-State.md`, `docs/Screen-Output.md`; `src/Input/TerminalInputEvent.cs`, `TerminalInputDecoder.ModernKeyboard.cs`, `TerminalInputDecoder.TraditionalKeys.cs`, `TerminalInputProtocolManager.cs`; `src/Presentation/TerminalPresentationManager.cs`; `src/Screen/TerminalScreenOutputTransaction.cs`; existing DCurses acceptance harness and samples.

- [ ] Capture exact 1.20 source, package/API baseline, supported frameworks, CI matrix, and already supported rich-input forms.
- [ ] Demonstrate a user-visible input gap with an actual downstream scenario and a failing byte/event fixture; freeze at least one bounded addition with a semantic result and traditional fallback. Record why alternatives are deferred.
- [ ] Draw lock/order and epoch boundaries for presentation manager, state-composition gate, synchronized/hyperlink managers, and screen output gate; freeze the transaction visibility API, unsupported result shape, and allowed values without a raw capability or lease bypass.
- [ ] Update the task inventory if evidence changes spelling or file placement, preserving both selected outcomes. Set prerelease metadata only when implementation begins.

**Acceptance:** Reviewed input fixture and visibility state/ordering table, additive API proposal, and no guessed baseline or duplicate current feature.

### T211 — Input contract and adversarial fixtures

**Likely files:** `src/Input/TerminalInputEvent.cs`, `TerminalRichInputContracts.cs`, relevant decoder contract tests and downstream modern-keyboard witness.

- [ ] Add failing tests for the frozen semantic distinction or rich-input framing case; cover ordinary text vs key events, press/repeat/release, modifiers, associated text where reported, and unknown modern identities without leaking raw private-use codes.
- [ ] Define immutable event shape, invalid combinations, nullability, versioning behavior, and resource limits; preserve existing enum numeric values.
- [ ] Exercise fallback when modern reporting cannot be acquired and interaction with paste, focus, mouse, semantic events, and active query ownership.

**Acceptance:** Tests explain the addition and fail against 1.20 before production changes.

### T212 — Bounded decoder and normalization

**Likely files:** `src/Input/TerminalInputDecoder.*.cs`, `TerminalInputCoordinator.cs` only if a reproduced routing defect needs it, and `tests/Icod.Terminal.Tests/src/Input/*`.

- [ ] Implement the frozen form through the existing incremental decoder and authoritative event stream; keep traditional and xterm decode-only behavior available.
- [ ] Test all split points, UTF-8 scalar boundaries, malformed/truncated/oversized frames, Escape ambiguity, recovery into a subsequent event, and deterministic routing when a query is pending.
- [ ] Keep existing frame, parameter, associated-text, and paste bounds; introduce explicit new limits only if the selected fixture requires them.

**Acceptance:** New fixtures pass without changing an unrelated query, introducing a second reader, or silently mislabeling ordinary text.

### T213 — Negotiation, lifecycle, and input ownership

**Likely files:** `src/Input/TerminalInputProtocolManager.cs`, `src/Session/TerminalSession.KittyKeyboard.cs`, existing keyboard composition/lifecycle tests.

- [ ] Verify that the selected behavior works under overlapping reporting leases and supported progressive modes. Add negotiation only if the frozen case needs it and the stack can be restored truthfully.
- [ ] Cover main/alternate screen handoff, suspend/resume, cancellation before/after output, release of the strongest owner, failed restoration, disposal, and stale generation/late query replies.
- [ ] Keep capability evidence, endpoint availability, and the lease's actual acquisition distinct.

**Acceptance:** One-reader ordering and reversible state ownership hold on all three target frameworks; unsupported negotiation falls back to traditional input.

### T214 — Package-backed input witness

**Likely files:** `samples/Icod.Terminal.RichInput.Sample/Program.cs`, `tools/dcurses-modern-keyboard-acceptance/`, `tools/package-modern-keyboard-smoke/`, associated package verifier and tests.

- [ ] Run the exact new consumer fixture through the public event loop, including unsupported modern reporting; assert phase/text/modifier semantics and unchanged paste/mouse/focus behavior.
- [ ] Use a fresh 1.21 package in the consumer harness; no source-project reference or direct TermInfo in the controlled consumer.

**Acceptance:** The chosen input increment is useful to a real consumer and survives package resolution.

### T215 — Cursor visibility contract and precommit validation

**Likely files:** `src/Screen/TerminalScreenOutputTransaction.cs`, `src/Presentation/TerminalPresentationManager.cs`, `src/Presentation/TerminalPresentationContracts.cs`, related screen/presentation tests.

- [ ] Add tests for an opt-in visibility scope over one frame: enter, ordered items, restore the effective owner; no visibility request emits the old sequence. Explicitly distinguish an active lease from the manager's ordinary capability baseline.
- [ ] Validate capability-backed entry and restoration, same-session ownership, retained-item limits, stale epochs, already-consumed builders, lifecycle admission, and pre-cancelled commits before writing.
- [ ] Freeze a minimal additive builder API in the current namespace. Decide whether a controlled unavailable result or rejected commit best matches existing transaction conventions; document the choice and null/invalid inputs.
- [ ] Confirm lock ordering and reservation lifetime: presentation/state-composition reservation before output, no manager reacquisition while holding the output gate, and no interleaving acquisition/release during temporary ownership.

**Acceptance:** Unsupported or stale work cannot change visibility or leak a synthetic public lease.

### T216 — Transactional visibility commitment and cleanup

**Likely files:** `src/Presentation/TerminalPresentationManager.cs`, `src/Session/TerminalSession.Presentation.cs`, `src/Screen/TerminalScreenOutputTransaction.cs`.

- [ ] Reserve the presentation owner and output epoch before first emission; temporarily apply requested visibility, write existing bounded items, restore the previously effective owner, then release reservations.
- [ ] Compose with synchronized output, hyperlinks, raster placeholders, and current transaction limits; preserve manager/output lock ordering and screen-local keyboard handoff.
- [ ] When a write, flush, cancellation-after-start, or restoration fails, make a best-effort uncancelled cleanup attempt and surface all relevant failures in deterministic order. Mark physical assumptions unknown where restoration cannot be proven. Never promise atomic rollback or replay an emitted prefix.

**Acceptance:** Temporary visibility is owned and cleaned up with the same correctness standard as presentation leases and screen transactions.

### T217 — Adversarial composition and cross-feature tests

**Likely files:** `tests/Icod.Terminal.Tests/src/Screen/TerminalScreenOutputCompositionTests.cs`, `TerminalScreenOutputTransactionHardeningTests.cs`, `tests/Icod.Terminal.Tests/src/Presentation/*`, `tests/Icod.Terminal.Tests/src/Input/TerminalKeyboardCompositionConcurrencyTests.cs`.

- [ ] Matrix: no lease, one lease, nested cursor owners, concurrent acquisition/release, alternate-screen plus Kitty keyboard, suspend/resume, disposal, unsupported normal/hidden/very-visible capabilities.
- [ ] Inject failure at entry, mid-frame, restoration, and flush; verify byte order, reported aggregate errors, no ghost owner, stale transaction consumption, and safe subsequent recovery.
- [ ] Keep zero-item, maximum-item/payload, synchronized-output and hyperlink conflict behavior explicit. Use bounded coordination in tests instead of timing assumptions.

**Acceptance:** Ownership and output are coherent under failure and concurrent lifecycle activity.

### T218 — Documentation and executed example

**Likely files:** `README.md`, `docs/Input-and-Events.md`, `docs/Presentation-and-Reversible-State.md`, `docs/Screen-Output.md`, `docs/Modern-Keyboard-Security-and-Compatibility.md`, `samples/README.md`, screen-output and rich-input samples.

- [ ] Document which input cases are new versus already supported, reporting prerequisites, fallback, privacy implications of associated text, and unsupported forms.
- [ ] Show an input-driven frame that requests temporary visibility, handles unavailable capability and stale epoch, and repaints from caller-owned state after ambiguous output failure; show a lease for long-lived cursor preferences.
- [ ] Execute the real sample routine headlessly in tests and through the package consumer, including the failure path. Keep interactive demonstrations usable without assuming a physical emulator in CI.

**Acceptance:** Guide and sample teach the exercised public contract, including truthful restoration and recovery limits.

### T219 — Package, API, and downstream qualification

**Likely files:** `docs/Public-API-Baseline-1.21.md`, corresponding fingerprint file; package contract tools; `docs/releases/1.21.0.md` and changelog when implementation is ready.

- [ ] Compare 1.21 API against the preserved 1.20 baseline for removed/changed signatures and enum values, XML docs for additive members, and equal snapshots across three frameworks.
- [ ] Build one candidate artifact and run foundation, presentation, semantic, and stable-release package shards against those exact bytes. Exercise DCurses 1.6.0/2.2.0, Terminal-only renderer, input witness, screen sample, and existing capability sample where the gate applies.
- [ ] Verify package metadata, dependencies, README, symbols, source/artifact identity, and no TermInfo leak into package-only consumers.

**Acceptance:** Additive, documented package surface with retained downstream compatibility.

### T220 — Cross-platform review and stable closure

- [ ] Pass full Windows, Linux, and macOS runtime/integration/sample matrix for .NET 8/9/10 at the final implementation head; inspect input routing, manager/output lock ordering, cancellation, and failure reporting inline.
- [ ] Reconcile both selected outcomes against a completed task and named evidence; record scripted-test limits and any explicit deferral.
- [ ] After qualification, set stable 1.21.0 metadata, update changelog/release notes and main roadmap, rerun the final candidate gates, record exact source/CI/artifact/API hashes, and present the PR for maintainer review.

**Acceptance:** A reviewable stable candidate with precise evidence. Merge, tag, GitHub release, and package publication are maintainer actions.

## Verification commands

Run from the repository root with the supported .NET SDK and PowerShell. Existing CI remains the authority for the three-platform result; exact command arguments may be updated when the T210 API and fixture are frozen.

```powershell
dotnet test tests/Icod.Terminal.Tests/Icod.Terminal.Tests.csproj -c Staging --filter 'FullyQualifiedName~TerminalKittyKeyboard|FullyQualifiedName~TerminalRichInput|FullyQualifiedName~TerminalScreenOutput|FullyQualifiedName~TerminalPresentation'
./packaging/VerifyRuntime.ps1 -Configuration Staging
./packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/1.21-package -Configuration Staging
foreach ($shard in @('foundation', 'presentation', 'semantic', 'release')) {
    ./packaging/VerifyPackageContractShard.ps1 -ArtifactDirectory artifacts/1.21-package -Configuration Staging -Shard $shard
}
```

## Deferred work and release boundary

New query families or a general query router redesign, raw vendor input events, xterm `modifyOtherKeys` mutation without a truthful restore path, global hotkeys, OS/IME control, unbounded paste assembly, terminal-brand heuristics, permanent cursor visibility inside a one-shot transaction, retained-screen/layout/damage policy, animation composition, new image protocols, transport/PTY hosting, and broad public extensibility remain outside 1.21.0. Scripted verification is not physical-emulator certification or a measured performance claim.

**Source authorities:** [main roadmap](Icod.Terminal-Development-Roadmap.md), [input/event contract](docs/Input-and-Events.md), [modern keyboard contract](docs/Modern-Keyboard-Security-and-Compatibility.md), [presentation ownership](docs/Presentation-and-Reversible-State.md), [screen output](docs/Screen-Output.md), and [1.20 release roadmap](Icod.Terminal-1.20.0-Development-Roadmap.md).
