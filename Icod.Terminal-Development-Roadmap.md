# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.1.0`  
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

## 1.1.0 program — VS Code OSC 633

`1.1.0` adds a typed VS Code OSC 633 shell-integration surface as a distinct vendor protocol family. It does not alias OSC 633 to the existing portable OSC 133 API.

The 1.1 tranche covers:

```text
V101  OSC 633 contract/reference review and protocol boundary
V102  byte-exact bounded encoder/writer foundation
V103  public TerminalSession semantic API
V104  session ordering/cancellation/security hardening
V105  package/XML/fresh-consumer validation
V106  public-API baseline and release-documentation closure
```

The supported 1.1 protocol surface is limited to the documented VS Code forms:

```text
A                  prompt start
B                  prompt end / command-input start
C                  pre-execution / command-output start
D                  status-less completion / abort
D;<exit-code>      signed decimal completion
E;<command>[;nonce]
P;Cwd=<cwd>[;nonce]
P;IsWindows=True|False
P;HasRichCommandDetection=True|False
```

The public API uses explicitly VS Code-named semantic methods. It does not expose raw OSC 633 marker/property dispatch, the unfinalized `F` continuation marker, private/undocumented `EnvJson`, automatic shell detection, automatic command/environment capture, or shell-startup-file mutation.

The complete OSC payload is bounded to 65,536 UTF-8 bytes. Message fields use the VS Code escaping grammar, optional nonces are explicit bounded caller input, and all operations retain the normal `TerminalSession` output serialization and pre-commit cancellation contract.

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
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.0.md`
- `docs/Public-API-Baseline-1.1.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.0.0.md`
- `docs/releases/1.1.0.md`
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

`1.1.0` intentionally adds nine public `TerminalSession` methods and therefore receives its own machine-generated fingerprint. The 1.1 baseline must be identical across net8.0/net9.0/net10.0 and is frozen only after the exact generated Staging snapshot is reviewed. Existing 1.0 public members and enum values remain unchanged.

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
- retained stable 1.0 public-API compatibility evidence plus the current 1.1 public-API fingerprint;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.0 release-line package contract;
- a fresh 1.1 OSC 633 package/XML consumer on all three TFMs;
- current `Icod.DCurses 0.1.0` project-reference and package-boundary compatibility/ownership acceptance.

The DCurses checks are compatibility witnesses for the integration surface its current early release exercises. They are not treated as exhaustive proof of every `Icod.Terminal` contract; Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full 1.x surface.

Tags trigger publication. A `v1.1.0` tag is created only after the exact 1.1 PR head and resulting exact `main` commit pass their required gates and publication is explicitly authorized.
