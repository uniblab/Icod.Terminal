# Changelog

Notable changes to `Icod.Terminal` are recorded here for consumers who need a concise release history. Detailed design evidence remains in the versioned roadmaps, T-series records, and public-API baseline documents.

## Unreleased

No unreleased 1.x changes are currently recorded.

## 1.7.0

### DCS foundation and Sixel graphics

- Begins the DCS/Sixel graphics release on top of the normalized 1.5 control-language model and complete 1.6 CSI foundation.
- Adds the D170 internal canonical seven-bit `DcsWriter` for small bounded DCS frames while keeping the generic framing layer dialect-neutral.
- Validates DCS parameter bytes, intermediate bytes, final selectors, complete-frame size, and payload bytes which could abort or prematurely terminate the DCS string.
- Retains existing DECRQSS and XTGETTCAP request construction unchanged until D171 can migrate both paths with byte-exact regression evidence.
- Separates small complete-frame DCS encoding from the later committed streaming transaction required for large Sixel graphics.
- Keeps Sixel as a DCS dialect and plans a backend-neutral raw raster contract that can also support Kitty Graphics in 1.8.

### Compatibility and validation

- Retains the stable `1.0.0` compatibility floor and all released 1.0–1.6 public signatures and documented wire semantics.
- Retains `net8.0`, `net9.0`, and `net10.0` plus the `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.
- Keeps one authoritative `TerminalSession` input reader, bounded parser/query state, and the rule that timeout is not automatically unsupported truth.
- Introduces no generic raw public DCS/Sixel writer and no image-file decoding dependency.

See `docs/releases/1.7.0.md`, `docs/D170-DCS-Construction-Contract-and-Reference-Freeze.md`, and `Icod.Terminal-1.7.0-Development-Roadmap.md` for the evolving 1.7 contract.

## 1.6.0

### Complete CSI grammar, consolidation, geometry, and hardening

- Completes the first protocol-family tranche on top of the 1.5 control-language normalization.
- Adds the bounded internal `TerminalCsiSyntax` grammar over the normalized structural frame model, preserving seven-bit/eight-bit framing, private-use parameter bytes, semicolon parameters, colon subparameters, omitted/empty components, intermediate bytes, final selectors, and original raw parameter data.
- Adds typed CSI parameter semantics which retain omitted/empty/zero distinctions and perform overflow-safe bounded numeric conversion without baking dialect defaults into framing.
- Consolidates existing Primary/Secondary DA, DSR, CPR, DEC private modes, synchronized output 2026, Kitty keyboard query/push/pop, mouse modes 1000/1002/1003/1006, and cursor-style CSI construction/parsing onto the common grammar/writer path while preserving released bytes.
- Retains TermInfo authority for exact focus/paste recipes and reviewed mouse advertisement rather than reconstructing terminal-specific control strings from assumptions.
- Adds internal terminal-window and character-cell pixel geometry queries using `CSI 14 t` / selector-4 responses and `CSI 16 t` / selector-6 responses for later Sixel/Kitty Graphics work.
- Adds exact-only cell-pixel derivation and deliberately defers a public geometry/provider contract until later graphics integration demonstrates the correct stable shape.
- Hardens CSI boundary behavior at exact raw-parameter, parameter-count, subparameter-count, and numeric ceilings, including private/semicolon/colon/empty mixtures and CAN/SUB invalidation.
- Qualifies every read split point of a representative geometry response through the authoritative `TerminalSession` input path and retains one-reader ownership.
- Adds malformed-correlated and oversized-correlated geometry recovery regressions proving a failed query does not poison the next query on the same session.
- Corrects late-response ownership after query timeout so it is measured from the logical monotonic timeout deadline rather than from when a scheduler happens to run the timeout continuation; cancellation/suspend/disposal keep their actual interruption-time semantics.

### Compatibility and validation

- Adds no public API and retains the frozen 1.4/1.5 public surface and `1.0.0` stable compatibility floor.
- Retains `net8.0`, `net9.0`, and `net10.0` plus the existing `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.
- Keeps one authoritative terminal reader, bounded parser/query state, the 1.5 capability/evidence architecture, and the rule that timeout is not automatically unsupported truth.
- Retains no generic raw public CSI writer and does not prematurely expose Sixel, Kitty Graphics, or the internal geometry substrate.
- Passes the complete C164 Staging matrix on exact head `3f5e1eccf2f655f25faf665c25262ad7a31df999`, workflow `34393525555`: Windows, Linux, macOS, package candidate, all four package shards, and the validated package artifact.
- C165 synchronizes release-facing documentation/package metadata and requires the resulting exact PR head to pass the same complete Staging matrix before the PR leaves draft status.

See `docs/releases/1.6.0.md`, `docs/C160-Complete-CSI-Grammar-Foundation.md`, `docs/C161-Typed-CSI-Parameter-Semantics.md`, `docs/C162-Existing-CSI-Consolidation.md`, `docs/C163-Terminal-and-Cell-Pixel-Geometry.md`, `docs/C164-CSI-Hardening-Fragmentation-and-Recovery.md`, `docs/C165-1.6.0-Acceptance-Package-and-Documentation-Closure.md`, and `Icod.Terminal-1.6.0-Development-Roadmap.md` for the complete 1.6 contract.

## 1.5.0

### Control-language normalization

- Completes the semantic protocol-normalization program required before complete CSI, DCS/Sixel, and APC/Kitty Graphics support.
- Separates semantic operation, protocol backend, control family, support state, and evidence source as distinct internal concepts.
- Generalizes control-family framing across CSI, DCS, OSC, APC, PM, and SOS while preserving the released behavior of existing CSI/DCS/OSC query paths.
- Consolidates family recognition behind one bounded incremental scanner and adds a structural control-frame model that preserves CSI parameters/intermediates/final selectors, DCS headers/payloads, and opaque string-family payloads.
- Adds bounded multi-family query transactions with explicit completion/barrier rules while preserving one authoritative input reader, cancellation semantics, timeout behavior, and unrelated application input.
- Adds a capability/evidence ledger separating `Unavailable`, `Unsupported`, `Unknown`, `Advertised`, and `Verified` from `TermInfo`, built-in profile, live-probe, and protocol-response evidence sources.
- Adds a complete internal semantic backend registry and deterministic routing policy without treating registry order, caller preference, terminal brand, or protocol number as capability truth.
- Reconciles exact TermInfo recipes for clipboard write (`Ms`), cursor style (`Ss`), and palette mutation (`can_change_color` + `initc`) while using complete TermInfo metadata only to advertise the existing CSI focus/paste/mouse backends.
- Avoids overclaiming partial TermInfo overlap: `Cs`/`Cr` do not advertise the complete OSC 10–19 dynamic-color semantic family.
- Feeds successful OSC 99 support responses into the evidence broker as `Verified / ProtocolResponse`; timeout/cancellation does not create false unsupported evidence.
- Feeds existing Kitty keyboard negotiation into the same evidence broker: Kitty flags are `Verified`, a Primary-DA barrier without Kitty flags is `Unsupported`, and timeout remains `Unknown`.
- Makes `TerminalSession.InvalidateState()` expire generation-scoped live evidence while immutable TermInfo/profile advertisement persists; managed resume inherits the same rule.
- Records Sixel as a DCS dialect and Kitty Graphics as an APC dialect while keeping both future public feature families deferred to 1.7/1.8.

### Compatibility and validation

- Preserves every existing stable 1.0–1.4 wire-specific public API and its documented byte semantics; no public automatic-routing API is introduced.
- Retains `net8.0`, `net9.0`, and `net10.0` plus the existing `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.
- Retains the frozen 1.4 public API fingerprint exactly: `3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27`.
- Uses the existing 1.4 baseline as the authoritative machine freeze because 1.5 adds no public-surface delta.
- Passes Windows, Linux, and macOS Staging runtime validation, all four package-contract shards, and the validated package artifact on N158 exact head `50b30098ac81c9ad36ab3b9d4e0907efe3c883ad`.
- Retains the current packaged `Icod.DCurses 0.1.0` compatibility witness and one-reader/session ownership checks.

See `docs/releases/1.5.0.md`, `docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md`, `docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md`, and `Icod.Terminal-1.5.0-Development-Roadmap.md` for the complete 1.5 contract.

## 1.4.0

### Kitty desktop notifications — OSC 99

- Adds typed Kitty OSC 99 notification emission through `SendKittyNotificationAsync(...)` with stable identifiers/update semantics, filtering metadata, focus policy, occasion, urgency, expiration, sound, icon-name lookup, and bounded PNG/JPEG/GIF icon transfer/cache identifiers.
- Adds `CloseKittyNotificationAsync(...)` for explicit close-by-identifier without adding a raw OSC 99 selector/metadata API.
- Encodes title/body and protocol-defined text metadata as strict UTF-8 plus Base64, automatically splits payloads into Kitty-compatible 4,096-byte encoded chunks, and validates the complete frame sequence before output commitment.
- Adds explicit correlated `QueryKittyNotificationSupportAsync(...)` and `QueryKittyAliveNotificationsAsync(...)` through the existing authoritative terminal response router; timeout remains an unanswered query rather than unsupported proof.
- Keeps OSC 9 and OSC 777 notification APIs unchanged and independent; no terminal-brand routing or host-native fallback is introduced.
- Deliberately defers buttons plus unsolicited activation/close reports until the authoritative `TerminalEvent` path has a reviewed OSC 99 event-routing contract.

### Compatibility and validation

- Preserves all existing stable 1.0–1.3 public members and OSC 7/9/133/633/777/1337 behavior; 1.4 is additive.
- Retains `net8.0`, `net9.0`, and `net10.0` and the `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.
- Adds byte-exact/chunking/query-parser/public-session tests plus a fresh NuGet-only OSC 99 package/XML consumer on all three TFMs.
- Intentionally advances the machine public-API baseline to `3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27` while retaining the 1.0–1.3 baselines unchanged.

See `docs/releases/1.4.0.md` for the full release notes.

## 1.3.0

### iTerm2 shell integration and semantic history — OSC 1337

- Adds a separate typed OSC 1337 surface for iTerm2 shell-integration/semantic-history metadata rather than aliasing the vendor namespace to OSC 7 or OSC 133.
- Adds explicit operations for `SetMark`, `CurrentDir`, `RemoteHost`, `SetUserVar`, the current `ShellIntegrationVersion=<version>;shell=<shell>` form, and `ClearCapturedOutput`.
- Encodes user-variable values as strict UTF-8 followed by Base64, matching iTerm2's shell-integration convention.
- Uses canonical ST termination, strict UTF-8, complete-frame prevalidation, a 65,536-byte payload ceiling, bounded delimited fields, and normal `TerminalSession` output serialization/pre-commit cancellation.
- Keeps OSC 7 current-location and OSC 133 prompt/command metadata independent; no automatic vendor detection, aliasing, or fallback is introduced.
- Deliberately excludes generic raw OSC 1337 dispatch, profile/focus/browser/pasteboard/file-transfer/custom-script operations, and overlapping mutation forms already owned by typed terminal APIs.

### Compatibility and validation

- Preserves all existing stable 1.0/1.1/1.2 public members, enum values, OSC 7/9/133/633/777 behavior, and documented ownership/security/restoration guarantees; 1.3 is additive.
- Retains `net8.0`, `net9.0`, and `net10.0` as first-class package targets and retains the existing `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0` dependency floor.
- Adds byte-exact OSC 1337 encoder/writer tests, public-session composition tests, and a fresh NuGet-only package/XML consumer on all three TFMs.
- Intentionally advances the machine public-API baseline for six compatible 1.3 methods while retaining the frozen 1.0, 1.1, and 1.2 baselines as compatibility evidence.

See `docs/releases/1.3.0.md` for the full release notes.

## 1.2.0

### Titled desktop notifications — OSC 777

- Adds `TerminalSession.SendTitledNotificationAsync(title, message, ...)` for the urxvt-style OSC 777 desktop-notification protocol.
- Emits canonical `OSC 777;notify;<title>;<message> ST` framing with strict UTF-8 and canonical ST termination.
- Preserves the existing `SendNotificationAsync(message, ...)` OSC 9 API unchanged; OSC 777 is an additive titled-notification path rather than a silent replacement or automatic fallback.
- Rejects semicolons in title/body because OSC 777 defines no interoperable field-escaping grammar, and rejects C0/C1/DEL controls and malformed Unicode before output commitment.
- Bounds the complete OSC payload to 4,096 encoded bytes and uses the normal `TerminalSession` shared output-serialization/pre-commit-cancellation contract.
- Does not infer OSC 777 support from terminal identity and does not expose a generic raw OSC 777 selector/payload writer.

### Compatibility and validation

- Preserves all existing stable 1.0/1.1 public members, enum values, OSC 9 behavior, OSC 633 behavior, and documented ownership/security/restoration guarantees; 1.2 is an additive minor release.
- Retains `net8.0`, `net9.0`, and `net10.0` as first-class package targets and retains the existing `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0` dependency floor.
- Adds byte-exact OSC 777 encoder/writer tests plus a fresh NuGet-only package consumer and XML-documentation verification on all three TFMs.
- Intentionally advances the machine public-API baseline for the compatible 1.2 addition while retaining the frozen 1.0 and 1.1 baselines as compatibility evidence.

See `docs/releases/1.2.0.md` for the full release notes.

## 1.1.0

### VS Code OSC 633

- Adds a separate typed OSC 633 surface for VS Code shell integration rather than aliasing the vendor protocol to OSC 133.
- Adds semantic A/B/C/D operations for prompt start, command-input start, pre-execution/command-output start, explicit signed exit-code completion, and status-less abort/cancel completion.
- Adds explicit OSC 633 E command-line publication with the documented VS Code escaping rules and optional caller-supplied nonce.
- Adds the stable documented OSC 633 P properties `Cwd`, `IsWindows`, `ContinuationPrompt`, and `HasRichCommandDetection`; current-directory publication remains explicit and does not replace OSC 7.
- Uses canonical ST termination, strict UTF-8, complete-frame prevalidation, a 65,536-byte payload ceiling, and the normal `TerminalSession` output-serialization/commit contract.
- Does not expose a generic raw OSC 633 writer, automatic terminal-brand activation, unfinalized `F`/`G` continuation-region or `H`/`I` right-prompt markers, `SetMark`, or `EnvJson`/`EnvSingle*` environment-transfer operations.

### Compatibility and validation

- Preserves all existing stable 1.0 public members and documented ownership/security/restoration guarantees; 1.1 is an additive minor release.
- Retains `net8.0`, `net9.0`, and `net10.0` as first-class package targets and retains the existing `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0` dependency floor.
- Adds byte-exact encoder/writer tests, public-session integration tests, fresh NuGet-only package consumers on all three TFMs, and XML-documentation verification for every new public OSC 633 member.
- Intentionally advances the machine public-API baseline for the compatible 1.1 additions while retaining the frozen 1.0 baseline as historical compatibility evidence.

See `docs/releases/1.1.0.md` for the full release notes.

## 1.0.0

### Stable contract

- Promotes the `1.0.0-rc1` contract to stable `1.0.0` without changing the frozen public API or adding a new terminal-protocol family.
- Establishes the permanent 1.x architecture, ownership, lifecycle, input/query, presentation/output, security, compatibility, licensing, migration, and public-API documents as the stable support authorities.
- Retains `net8.0`, `net9.0`, and `net10.0` as first-class package targets.
- Retains Windows, Linux, and macOS support in the built-in system terminal-control provider.

### Compatibility

- Keeps the rc1 public API fingerprint unchanged: `8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5`.
- Carries the one pre-1.0 breaking correction forward: public `TerminalSession.Input` remains removed so a live session has one authoritative input/query-routing path.
- Retains public `ITerminalInput`, `ITerminalOutput`, and `ITerminalControlProvider` injection seams.
- Retains `TerminalSession.Output` as the documented advanced borrowed output transport outside session serialization.

### Licensing

- Makes the reusable `Icod.Terminal` library, root project, and library C# sources explicitly `LGPL-3.0-or-later`; the published NuGet package remains LGPL.
- Makes repository test, sample, package-smoke, verification, and downstream acceptance/soak executable programs explicitly `GPL-3.0-or-later`.
- Adds project-appropriate license headers to every tracked `.cs` and `.csproj` file, including owning assembly name, one-line description, and the 2026 Timothy J. Bruce copyright notice.
- Requires every project file to place its license header immediately after `<?xml version="1.0" encoding="utf-8"?>` and before `<Project ...>`.
- Adds an exact license-header verification gate to package-candidate CI and local `all` / `validate` builds.
- Adds `docs/Licensing.md` as the permanent explanation of the library/executable license boundary; the root `LICENSE` contains the LGPLv3 terms and incorporated GPLv3 terms.

### Validation and release engineering

- Uses the optimized runtime/package validation graph proven after rc1: six OS/architecture runtime jobs, one portable package candidate, and four parallel package-contract shards.
- Retains exact package/XML/symbol/Source Link verification and package-only contracts from 0.8 through 0.18.
- Retains the fresh 1.0 release-line package contract and current packaged `Icod.DCurses 0.1.0` compatibility witness.
- Requires curated release notes for tag publication.

See `docs/releases/1.0.0.md` for the full stable release notes.

## 1.0.0-rc1

### Contract freeze

- Freezes the intended `Icod.Terminal` 1.x public contract across `net8.0`, `net9.0`, and `net10.0`.
- Machine-freezes the exported public API and every public enum numeric value.
- Establishes permanent architecture, ownership, lifecycle, input/query, presentation/output, security, compatibility, and migration documentation.
- Defines conservative SemVer expectations for the 1.x line.

### Breaking change

- Removes public `TerminalSession.Input` from a live session so the session retains one authoritative input decoder and query router.
- Retains public `ITerminalInput` for custom transport injection.
- Retains `TerminalSession.Output` as an explicitly advanced borrowed output transport outside normal session serialization.

### Validation and packaging

- Adds fresh NuGet-only 1.0 package validation on all supported TFMs.
- Retains package-only compatibility gates for 0.8 through 0.18.
- Adds a frozen API fingerprint gate to PR and Release distribution validation.
- Adds current `Icod.DCurses 0.1.0` project-reference and package-boundary compatibility/ownership acceptance.
- Qualifies the merged rc1 candidate under Release on Windows x64/ARM64, Linux x64/ARM64, and macOS x64/ARM64.

### Documentation

- Replaces release-number-oriented documentation as the primary consumer authority with permanent 1.x documents.
- Adds a dedicated migration guide from `0.18.0`.
- Reorganizes samples by task rather than historical release number.
- Preserves the original long-form development roadmap under `docs/history/`.

See `docs/releases/1.0.0-rc1.md` for the full curated release notes.

## Pre-1.0 highlights

- **0.18.0 — Hardening and invariant closure:** parser/query, lifecycle/composition, rollback/cancellation, platform restoration, package hardening, and downstream soak.
- **0.17.0 — Modern keyboard:** negotiated Kitty keyboard reporting, traditional fallback, full-screen choreography, and decode-only xterm `modifyOtherKeys` compatibility.
- **0.16.0 — Safe OSC 9:** bounded desktop notifications, terminal progress, and Windows/ConEmu current-directory compatibility; hazardous OSC 9 vendor commands remain excluded.
- **0.15.0 — Extended semantic metadata:** bounded OSC 133 command/prompt metadata.
- **0.14.0 — Lifecycle-safe color ownership:** scoped exact restoration for observable terminal color state.
- **0.13.0 — Terminal colors:** indexed palette and selected dynamic color query/set/reset support.
- **0.12.0 — Semantic prompts:** OSC 133 prompt/command-region semantics.
- **0.11.0 — Pointer shape:** semantic OSC 22 mouse-pointer shape support.
- **0.10.0 — Terminal progress:** semantic OSC 9;4 progress state.
- **0.9.0 — Synchronized output:** DEC private mode 2026 ownership and acceptance.
- **0.8.0 — Cursor style:** semantic DECSCUSR/DECRQSS cursor-style observation and ownership.
- **0.7.0 — Clipboard:** bounded OSC 52 read/write support.
- **0.6.0 — Hyperlinks:** validated OSC 8 hyperlink semantics.
- **0.5.0 — Current location:** canonical OSC 7 `file:` URI publication.
- **0.4.0 — Titles:** semantic OSC 0/1/2 title operations.
- **0.3.x — Queries:** typed live terminal query/correlation foundation.
- **0.2.x — Rich input:** focus, bracketed paste, mouse/input framing, and bounded decoding.
- **0.1.x — Foundation:** live terminal session/control abstractions, platform mode handling, lifecycle, and custom transport/provider seams.