# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.5.0`  
**Current status:** N150–N158 complete; N159 acceptance/package/documentation closure in progress  
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

## 1.5.0 program — semantic protocol normalization and control-language foundation

`1.5.0` normalizes the architecture before complete CSI, DCS/Sixel, and APC/Kitty Graphics support is added.

The 1.5 task sequence is:

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

N150–N158 are complete. N159 is the active release-closure tranche.

The `N` prefix is intentional: historical repository documents already use T150–T157 for the old 0.15.0 OSC 133 program. N150 is the requested 1.5 terminology/layer-ownership tranche without overwriting that historical namespace.

The detailed 1.5 program is maintained in:

[`Icod.Terminal-1.5.0-Development-Roadmap.md`](Icod.Terminal-1.5.0-Development-Roadmap.md)

N158 reconciliation is recorded in:

[`docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md`](docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md)

N159 release closure is recorded in:

[`docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md`](docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md)

The longer path through CSI, Sixel, and Kitty Graphics is maintained in:

[`docs/Control-Language-Normalization-and-Graphics-Roadmap.md`](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)

### 1.5 normalization result

Version 1.5 separates five layers which had previously been easy to conflate:

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

### 1.5 compatibility rule

Existing stable 1.x explicit methods keep their exact documented wire meaning. For example:

```text
SendNotificationAsync            -> OSC 9
SendTitledNotificationAsync      -> OSC 777
SendKittyNotificationAsync       -> OSC 99
PublishCurrentLocationAsync      -> OSC 7
VS Code shell-integration APIs   -> OSC 633
iTerm2 shell-integration APIs    -> OSC 1337
```

Version 1.5 introduces no public automatic-routing API. Internal normalization therefore does not silently reinterpret these released methods.

N158 passed the full pull-request Staging matrix on exact head `50b30098ac81c9ad36ab3b9d4e0907efe3c883ad` in workflow run `34374645658`. The package candidate retained the frozen 1.4 public API fingerprint exactly across `net8.0`, `net9.0`, and `net10.0`.

## Planned post-1.5 control-language releases

The current normalized development order is:

```text
1.6.0  complete CSI grammar / CSI consolidation / pixel and cell geometry
1.7.0  DCS foundation / Sixel / common raster model and output transaction
1.8.0  APC foundation / Kitty Graphics / graphics backend routing
```

This ordering allows later graphics protocols to reuse one authoritative input path, one family scanner, one query transaction model, one capability/evidence broker, and one semantic backend resolver.

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
```

Version 1.5 intentionally adds no public API. The generated public surface continues to match the frozen 1.4 fingerprint across `net8.0`, `net9.0`, and `net10.0`.

Because no public bytes changed, the authoritative current machine baseline remains `docs/Public-API-Baseline-1.4.md` / `.sha256`. A duplicate 1.5 baseline file is intentionally not created merely to relabel the same surface.

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
- stable 1.0–1.4 baseline evidence, with 1.5 deliberately retaining the 1.4 public fingerprint;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.x release-line package contract;
- fresh OSC 633, OSC 777, OSC 1337, and OSC 99 package/XML consumers on all three TFMs;
- current `Icod.DCurses` integration/ownership acceptance.

Tags trigger publication. A `v1.5.0` tag is created only after the exact final 1.5 PR head and resulting `main` commit pass the required gates and publication is explicitly authorized. The tag workflow requires curated `docs/releases/1.5.0.md` notes.