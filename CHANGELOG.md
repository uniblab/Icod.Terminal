# Changelog

Notable changes to `Icod.Terminal` are recorded here for consumers who need a concise release history. Detailed design evidence remains in the versioned roadmaps, T-series records, and public-API baseline documents.

## Unreleased

No unreleased 1.x changes are currently recorded.

## 1.1.0

### VS Code shell integration — OSC 633

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

- Removes public `TerminalSession.Input` from a live session so the session retains one authoritative input decoder/query router.
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
