# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.3.0`  
**Stable compatibility floor:** `1.0.0`

## Purpose

This file is the permanent entry point for current `Icod.Terminal` development and release planning.

The repository began with a detailed pre-1.0 architecture and milestone plan. That original document remains valuable historical design evidence and is preserved verbatim at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

The completed rc1 development program remains preserved at:

[`Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`](Icod.Terminal-1.0.0-rc1-Development-Roadmap.md)

Stable `1.0.0` established the permanent 1.x contract. Minor releases may add compatible typed protocol APIs while preserving the documented 1.0 ownership, lifecycle, security, and compatibility guarantees.

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
- `Icod.Terminal` owns the live terminal conversation, native terminal modes, input decoding, lifecycle, query routing, semantic terminal output, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, refresh/diff policy, and curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## 1.3.0 program — iTerm2 OSC 1337 shell integration

`1.3.0` adds a typed iTerm2 OSC 1337 shell-integration and semantic-history metadata surface as a distinct vendor protocol family. It does not alias OSC 1337 to OSC 7 or OSC 133.

The 1.3 tranche covers:

```text
I301  OSC 1337 contract/reference review and security boundary
I302  byte-exact bounded encoder/writer foundation
I303  public TerminalSession shell-integration API
I304  output/cancellation/privacy/composition hardening
I305  package/XML/fresh-consumer validation
I306  public-API baseline and release-documentation closure
```

The supported 1.3 wire surface is:

```text
OSC 1337;SetMark ST
OSC 1337;CurrentDir=<path> ST
OSC 1337;RemoteHost=<user>@<host> ST
OSC 1337;SetUserVar=<name>=<base64(utf8(value))> ST
OSC 1337;ShellIntegrationVersion=<version>;shell=<shell> ST
OSC 1337;ClearCapturedOutput ST
```

The public API consists of six explicitly iTerm2-named `TerminalSession` methods. User-variable values are strict UTF-8 followed by Base64. Other fields are strictly validated, bounded, and encoded as UTF-8. The complete OSC payload is limited to 65,536 bytes and all calls participate in the normal session output-serialization/pre-commit-cancellation contract.

Portable OSC 7 remains the preferred current-location API and OSC 133 remains the prompt/command-region API. The library does not infer iTerm2 support or automatically mirror portable metadata into OSC 1337.

The public 1.3 surface deliberately excludes generic raw OSC 1337 dispatch and invasive/overlapping operations for profile mutation, focus stealing, URL opening, pasteboard/file transfer, custom scripts, arbitrary color/cursor mutation, Unicode-version changes, Touch Bar key labels, and arbitrary variable-reporting queries.

## Completed 1.2.0 program — OSC 777 titled desktop notifications

`1.2.0` added one typed urxvt-style OSC 777 desktop-notification operation while preserving the existing OSC 9 notification API unchanged.

The stable public addition is:

```csharp
ValueTask SendTitledNotificationAsync(
	string title,
	string message,
	CancellationToken cancellationToken = default
);
```

The complete OSC 777 payload remains bounded to 4,096 UTF-8 bytes and the library does not silently route between OSC 9 and OSC 777.

## Completed 1.1.0 program — VS Code OSC 633

`1.1.0` added a typed VS Code OSC 633 shell-integration surface as a distinct vendor protocol family rather than aliasing it to OSC 133.

Its stable surface includes:

```text
A                  prompt start
B                  prompt end / command-input start
C                  pre-execution / command-output start
D                  status-less completion / abort
D;<exit-code>      signed decimal completion
E;<command>[;nonce]
P;Cwd=<cwd>[;nonce]
P;IsWindows=True|False
P;ContinuationPrompt=<prompt>
P;HasRichCommandDetection=True|False
```

The complete OSC 633 payload remains bounded to 65,536 UTF-8 bytes with VS Code message escaping and explicit caller-supplied nonces where defined.

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
- `docs/VsCode-Osc633-Shell-Integration.md`
- `docs/Osc777-Desktop-Notifications.md`
- `docs/ITerm2-Osc1337-Shell-Integration.md`
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.0.md`
- `docs/Public-API-Baseline-1.1.md`
- `docs/Public-API-Baseline-1.2.md`
- `docs/Public-API-Baseline-1.3.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.0.0.md`
- `docs/releases/1.1.0.md`
- `docs/releases/1.2.0.md`
- `docs/releases/1.3.0.md`
- `CHANGELOG.md`

Historical T-series, 0.x public API baselines, and `Public-API-Baseline-1.0-rc1.*` remain design/release evidence.

## API baseline policy

The stable 1.0 baseline remains retained as compatibility evidence:

```text
633 lines
77 exported public types
32 public enums / 242 enum values
124 public methods
144 public properties
13 public constructors
SHA-256 8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

The retained additive fingerprints are:

```text
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
1.3  0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
```

`1.3.0` intentionally adds exactly six public `TerminalSession` methods. The machine-generated baseline is identical across `net8.0`, `net9.0`, and `net10.0`. Existing 1.0/1.1/1.2 public members and enum values remain unchanged.

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
- stable 1.0/1.1/1.2 compatibility evidence plus the current 1.3 public-API fingerprint;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.x release-line package contract;
- fresh OSC 633, OSC 777, and OSC 1337 package/XML consumers on all three TFMs;
- current `Icod.DCurses 0.1.0` project-reference and package-boundary compatibility/ownership acceptance.

The DCurses checks are compatibility witnesses for the integration surface its current early release exercises. They are not treated as exhaustive proof of every `Icod.Terminal` contract; Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full 1.x surface.

Tags trigger publication. A `v1.3.0` tag is created only after the exact 1.3 PR head and resulting exact `main` commit pass their required gates and publication is explicitly authorized.
