# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.6.0`  
**Current status:** C160–C164 complete; C165 release acceptance/documentation closure in progress  
**Stable compatibility floor:** `1.0.0`

## Purpose

This file is the permanent entry point for current `Icod.Terminal` development and release planning.

The repository began with a detailed pre-1.0 architecture and milestone plan. That original document remains valuable historical design evidence and is preserved at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

The completed rc1 program remains preserved at:

[`Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`](Icod.Terminal-1.0.0-rc1-Development-Roadmap.md)

Stable `1.0.0` established the permanent 1.x contract. Minor releases may add compatible typed protocol APIs and internal infrastructure while preserving the documented 1.0 ownership, lifecycle, security, and compatibility guarantees.

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
- `Icod.Terminal` owns the live terminal conversation, native terminal modes, input decoding, lifecycle, query routing, semantic terminal output, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, refresh/diff policy, curses presentation abstractions, and other higher-level semantic UI policy.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## 1.6.0 program — complete CSI grammar, consolidation, geometry, and hardening

`1.6.0` is the first protocol-family tranche built on the 1.5 normalization. Its purpose is to make CSI one complete shared grammar, migrate existing CSI operations onto that grammar without changing released bytes, add the internal pixel geometry needed by later raster protocols, and qualify the resulting query/parser substrate against hostile fragmentation and oversized correlated responses.

The 1.6 task sequence is:

```text
C160  complete CSI grammar foundation                  complete
C161  typed CSI parameter semantics                    complete
C162  existing CSI consolidation                       complete
C163  terminal/cell pixel geometry                     complete
C164  CSI hardening, fragmentation, and recovery       complete
C165  acceptance/package/documentation closure         in progress
```

The detailed current program is:

[`Icod.Terminal-1.6.0-Development-Roadmap.md`](Icod.Terminal-1.6.0-Development-Roadmap.md)

The permanent 1.6 tranche records are:

- [`docs/C160-Complete-CSI-Grammar-Foundation.md`](docs/C160-Complete-CSI-Grammar-Foundation.md)
- [`docs/C161-Typed-CSI-Parameter-Semantics.md`](docs/C161-Typed-CSI-Parameter-Semantics.md)
- [`docs/C162-Existing-CSI-Consolidation.md`](docs/C162-Existing-CSI-Consolidation.md)
- [`docs/C163-Terminal-and-Cell-Pixel-Geometry.md`](docs/C163-Terminal-and-Cell-Pixel-Geometry.md)
- [`docs/C164-CSI-Hardening-Fragmentation-and-Recovery.md`](docs/C164-CSI-Hardening-Fragmentation-and-Recovery.md)
- [`docs/C165-1.6.0-Acceptance-Package-and-Documentation-Closure.md`](docs/C165-1.6.0-Acceptance-Package-and-Documentation-Closure.md)

### 1.6 result through C164

The completed implementation now provides:

- one bounded structural CSI grammar preserving private-use bytes, semicolon parameters, colon subparameters, omitted/empty components, intermediates, final selectors, and raw parameter bytes;
- typed bounded numeric/component semantics without conflating omitted, empty, zero, and explicit values;
- shared construction/parsing for DA/DSR/CPR, DEC private modes, synchronized output, Kitty keyboard CSI, mouse tracking/encoding, and cursor-style output;
- TermInfo authority retained for exact focus/paste recipes and reviewed mouse advertisement;
- internal `CSI 14 t` terminal-window pixel and `CSI 16 t` character-cell pixel observations;
- exact-only cell-pixel derivation without rounding;
- every-split-point geometry response qualification through one authoritative session reader;
- exact parser resource-boundary qualification and CAN/SUB invalidation;
- malformed and oversized correlated geometry recovery without poisoning the next query;
- timeout late-response ownership anchored to the logical monotonic timeout deadline rather than scheduler-continuation timing.

The accepted C164 implementation head is:

```text
3f5e1eccf2f655f25faf665c25262ad7a31df999
```

Staging workflow `34393525555` passed Windows, Linux, macOS, package candidate, all four package-contract shards, and the validated package artifact.

### 1.6 compatibility result

Version 1.6 adds no public API. The internal geometry substrate remains deliberately private until later Sixel/Kitty Graphics integration demonstrates the correct stable semantic shape.

Existing stable 1.x CSI-based APIs retain their documented wire meaning. The authoritative public surface therefore remains the frozen 1.4 baseline carried unchanged by 1.5 and 1.6; no duplicate `Public-API-Baseline-1.6` file is required for an identical surface.

A generic raw public CSI writer remains explicitly outside the 1.6 contract.

## Completed 1.5.0 program — semantic protocol normalization and control-language foundation

`1.5.0` normalized the architecture before complete CSI, DCS/Sixel, and APC/Kitty Graphics support.

The completed 1.5 task sequence is:

```text
N150  terminology and layer-ownership freeze
N151  generalized control-family framing
N152  one bounded incremental control-language state machine
N153  structural CSI/DCS/string frame model
N154  multi-family query transactions
N155  capability support/evidence model
N156  semantic backend registry
N157  deterministic routing policy
N158  existing protocol and TermInfo reconciliation
N159  acceptance/package/documentation closure
```

The completed 1.5 program is preserved in:

[`Icod.Terminal-1.5.0-Development-Roadmap.md`](Icod.Terminal-1.5.0-Development-Roadmap.md)

N158 reconciliation is recorded in:

[`docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md`](docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md)

N159 release closure is recorded in:

[`docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md`](docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md)

The longer path through CSI, Sixel, and Kitty Graphics is maintained in:

[`docs/Control-Language-Normalization-and-Graphics-Roadmap.md`](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)

### 1.5 normalization result

Version 1.5 separated five layers which had previously been easy to conflate:

```text
semantic intent
    -> capability/evidence resolution
    -> protocol backend selection
    -> control family framing
    -> dialect codec / wire transport
```

The internal vocabulary distinguishes:

```text
semantic operation
protocol backend
control family
support state
evidence source
```

The normalized control-family vocabulary is:

```text
CSI  ESC [
DCS  ESC P
OSC  ESC ]
APC  ESC _
PM   ESC ^
SOS  ESC X
ST   ESC \
```

Sixel is classified as DCS. Kitty Graphics is classified as APC. APC itself remains an application-defined string container rather than being treated as inherently key/value based. CSI is treated as a structured parameter/intermediate/final grammar rather than merely a decimal parameter list.

The completed 1.5 implementation provides:

- generalized family framing across CSI/DCS/OSC/APC/PM/SOS;
- one bounded incremental control-language scanner;
- structural CSI/DCS/string frame parsing;
- bounded multi-family query transactions;
- capability support/evidence state with static versus live evidence separation;
- a complete internal semantic backend registry;
- deterministic routing independent of registry declaration order;
- exact TermInfo recipe reconciliation without overclaiming partial metadata;
- OSC 99 live protocol evidence integration;
- Kitty keyboard live protocol evidence integration;
- generation-scoped live evidence invalidation through `InvalidateState()`.

### 1.5 compatibility result

Existing stable 1.x explicit methods retain their exact documented wire meaning. Version 1.5 introduced no public automatic-routing API and retained the frozen 1.4 public API fingerprint.

The final 1.5 PR, merged `main` validation, and `v1.5.0` tag-triggered release workflow all completed successfully.

## Planned post-1.6 control-language releases

The normalized development order is:

```text
1.7.0  DCS foundation / Sixel / common raster model and output transaction
1.8.0  APC foundation / Kitty Graphics / graphics backend routing
```

This ordering lets graphics protocols reuse one authoritative input path, one family scanner, one query transaction model, one capability/evidence broker, one semantic backend resolver, and the complete CSI grammar established by 1.6.

## Completed 1.4.0 program — Kitty OSC 99 desktop notifications

`1.4.0` added typed Kitty OSC 99 desktop notifications while preserving OSC 9 and OSC 777 independently.

The stable 1.4 surface includes:

- `SendKittyNotificationAsync(...)`;
- `CloseKittyNotificationAsync(...)`;
- `QueryKittyNotificationSupportAsync(...)`;
- `QueryKittyAliveNotificationsAsync(...)`;
- typed notification options, support observations, urgency, and occasion;
- bounded Base64 chunking and optional icon caching;
- one-authoritative-reader query correlation.

Buttons and unsolicited activation/close events remain deferred until they can be integrated through the authoritative `TerminalEvent` path.

## Completed 1.3.0 program — iTerm2 OSC 1337 shell integration

`1.3.0` added a typed iTerm2 OSC 1337 shell-integration and semantic-history metadata surface as a distinct vendor protocol family.

The stable 1.3 surface covers:

```text
SetMark
CurrentDir
RemoteHost
SetUserVar
ShellIntegrationVersion=...;shell=...
ClearCapturedOutput
```

Portable OSC 7 remains the preferred current-location API and OSC 133 remains the prompt/command-region API.

## Completed 1.2.0 program — OSC 777 titled desktop notifications

`1.2.0` added `SendTitledNotificationAsync(...)` for bounded urxvt-style OSC 777 notifications while preserving OSC 9 unchanged.

## Completed 1.1.0 program — VS Code OSC 633

`1.1.0` added typed VS Code OSC 633 shell integration as a distinct vendor protocol family rather than aliasing it to OSC 133.

Its stable surface includes A/B/C/D/E command-detection forms and the stable `P` properties `Cwd`, `IsWindows`, `ContinuationPrompt`, and `HasRichCommandDetection`.

## Permanent 1.x authorities

Consumers and maintainers should treat these documents as the current contract authorities:

- `docs/Architecture.md`
- `docs/Terminal-Session-and-Ownership.md`
- `docs/Lifecycle-and-Restoration.md`
- `docs/Input-and-Events.md`
- `docs/Queries-and-Responses.md`
- `docs/Modern-Keyboard-Security-and-Compatibility.md`
- `docs/Presentation-and-Reversible-State.md`
- `docs/Semantic-Output-Protocols.md`
- `docs/Control-Language-Normalization-and-Graphics-Roadmap.md`
- `docs/C160-Complete-CSI-Grammar-Foundation.md`
- `docs/C161-Typed-CSI-Parameter-Semantics.md`
- `docs/C162-Existing-CSI-Consolidation.md`
- `docs/C163-Terminal-and-Cell-Pixel-Geometry.md`
- `docs/C164-CSI-Hardening-Fragmentation-and-Recovery.md`
- `docs/C165-1.6.0-Acceptance-Package-and-Documentation-Closure.md`
- `docs/N150-Control-Language-Terminology-and-Layer-Ownership-Freeze.md`
- `docs/N151-Generalized-Control-Family-Framing.md`
- `docs/N152-Incremental-Control-Language-State-Machine.md`
- `docs/N153-Structural-Control-Frame-Model.md`
- `docs/N154-Multi-Family-Query-Transactions.md`
- `docs/N155-Capability-Support-and-Evidence-Model.md`
- `docs/N156-Semantic-Backend-Registry.md`
- `docs/N157-Deterministic-Semantic-Backend-Routing-Policy.md`
- `docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md`
- `docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md`
- `docs/VsCode-Osc633-Shell-Integration.md`
- `docs/Osc777-Desktop-Notifications.md`
- `docs/ITerm2-Osc1337-Shell-Integration.md`
- `docs/Kitty-Osc99-Desktop-Notifications.md`
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.0.md`
- `docs/Public-API-Baseline-1.1.md`
- `docs/Public-API-Baseline-1.2.md`
- `docs/Public-API-Baseline-1.3.md`
- `docs/Public-API-Baseline-1.4.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.0.0.md`
- `docs/releases/1.1.0.md`
- `docs/releases/1.2.0.md`
- `docs/releases/1.3.0.md`
- `docs/releases/1.4.0.md`
- `docs/releases/1.5.0.md`
- `docs/releases/1.6.0.md`
- `CHANGELOG.md`

Historical T-series, 0.x public API baselines, and `Public-API-Baseline-1.0-rc1.*` remain design/release evidence.

## API baseline policy

The stable 1.x baseline history is:

```text
1.0  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
1.3  0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
1.4  3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
1.5  unchanged from 1.4
1.6  unchanged from 1.4/1.5
```

C160–C164 are internal and retain the frozen 1.4/1.5 public fingerprint. C165 therefore does not create a duplicate 1.6 baseline file for an identical surface.

## Release discipline

Pull requests validate Staging on Windows, Linux, and macOS runtime/source paths while one portable package candidate feeds four parallel package-contract shards.

`main` Release validation runs runtime/source checks on:

- Windows x64;
- Windows ARM64;
- Linux x64;
- Linux ARM64;
- macOS x64;
- macOS ARM64;

and separately validates one RID-independent package candidate through the four package shards.

The release line retains:

- full build/test coverage on `net8.0`, `net9.0`, and `net10.0`;
- retained stable 1.x public baseline evidence;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.x release-line package contract;
- current semantic package/XML consumers on all three TFMs;
- current `Icod.DCurses` integration/ownership acceptance.

Tags trigger publication. A `v1.6.0` tag is created only after the exact final 1.6 PR head and resulting `main` commit pass the required gates and publication is explicitly authorized. The tag workflow requires curated `docs/releases/1.6.0.md` notes.
