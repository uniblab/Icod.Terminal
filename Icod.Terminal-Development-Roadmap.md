# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.2.0`  
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

## 1.2.0 program — OSC 777 titled desktop notifications

`1.2.0` adds one typed urxvt-style OSC 777 desktop-notification operation while preserving the existing OSC 9 notification API unchanged.

The 1.2 tranche covers:

```text
N201  OSC 777 contract/reference review and protocol boundary
N202  byte-exact bounded encoder/writer foundation
N203  public TerminalSession titled-notification API
N204  output/cancellation/security hardening
N205  package/XML/fresh-consumer validation
N206  public-API baseline and release-documentation closure
```

The supported wire form is:

```text
OSC 777 ; notify ; <title> ; <message> ST
```

The public API is:

```csharp
ValueTask SendTitledNotificationAsync(
	string title,
	string message,
	CancellationToken cancellationToken = default
);
```

The existing `SendNotificationAsync(message, ...)` remains the OSC 9 compatibility API and is neither redirected nor auto-fallbacked to OSC 777.

OSC 777 title/body fields are strict UTF-8, reject C0/C1/DEL controls, reject semicolons because the protocol defines no interoperable field-escaping grammar, and share a 4,096-byte complete OSC payload bound. The complete frame is encoded before waiting for the session output gate. Calls retain normal pre-commit cancellation and one-write/no-implicit-flush semantics.

The public surface does not expose raw OSC 777 command names, arbitrary field arrays, terminal-brand auto-detection, host-native notification fallbacks, or richer OSC 99-style notification actions/IDs.

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
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.0.md`
- `docs/Public-API-Baseline-1.1.md`
- `docs/Public-API-Baseline-1.2.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.0.0.md`
- `docs/releases/1.1.0.md`
- `docs/releases/1.2.0.md`
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

`1.1.0` intentionally added ten public `TerminalSession` methods and has its own retained baseline.

`1.2.0` intentionally adds exactly one additional public `TerminalSession` method. Its machine-generated baseline must be identical across `net8.0`, `net9.0`, and `net10.0` and is frozen only after the exact generated Staging snapshot is reviewed. Existing 1.0/1.1 public members and enum values remain unchanged.

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
- stable 1.0/1.1 compatibility evidence plus the current 1.2 public-API fingerprint;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.x release-line package contract;
- fresh OSC 633 and OSC 777 package/XML consumers on all three TFMs;
- current `Icod.DCurses 0.1.0` project-reference and package-boundary compatibility/ownership acceptance.

The DCurses checks are compatibility witnesses for the integration surface its current early release exercises. They are not treated as exhaustive proof of every `Icod.Terminal` contract; Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full 1.x surface.

Tags trigger publication. A `v1.2.0` tag is created only after the exact 1.2 PR head and resulting exact `main` commit pass their required gates and publication is explicitly authorized.
