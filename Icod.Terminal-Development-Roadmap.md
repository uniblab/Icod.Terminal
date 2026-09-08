# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.0.0`  
**Stable contract:** `1.0.0`

## Purpose

This file is the permanent entry point for current `Icod.Terminal` development and release planning.

The repository began with a detailed pre-1.0 architecture and milestone plan. That original document remains valuable historical design evidence and is preserved verbatim at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

The completed rc1 development program remains preserved at:

[`Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`](Icod.Terminal-1.0.0-rc1-Development-Roadmap.md)

Stable `1.0.0` promotes that qualified contract without adding a new feature or public-API delta.

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

## Stable 1.0 program

The pre-1.0 contract-freeze program completed T190–T197 covering:

```text
T190  contract and public-API regret audit
T191  permanent architecture / ownership / lifecycle documentation
T192  permanent input / query / protocol documentation
T193  permanent output / presentation / security documentation
T194  exact public API / enum / XML / sample freeze
T195  compatibility / migration / support policy
T196  package metadata / documentation artifact closure
T197  downstream RC acceptance / release-candidate closure
```

`1.0.0-rc1` was published and qualified. Publication/CI hardening then merged to `main` at `dfa149c604aaf860a18923d23999b045ab98532d`; workflow run `34191123678` passed the redesigned six-runtime/single-package/four-shard Release validation graph.

Stable `1.0.0` is a release promotion only. No new terminal protocol, production feature, or public API change is permitted in the stable-promotion branch unless a release-blocking defect is discovered and explicitly re-audited.

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
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.0.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.0.0.md`
- `CHANGELOG.md`

Historical T-series, 0.x public API baselines, and `Public-API-Baseline-1.0-rc1.*` remain design/release evidence.

## Stable API baseline

Stable 1.0 must reproduce the rc1 public API fingerprint exactly:

```text
633 lines
77 exported public types
32 public enums / 242 enum values
124 public methods
144 public properties
13 public constructors
SHA-256 8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

Any deviation during stable promotion is a release blocker requiring explicit compatibility review rather than a baseline update by default.

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
- the frozen stable 1.0 public-API fingerprint;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.0 release-line package contract;
- current `Icod.DCurses 0.1.0` project-reference and package-boundary compatibility/ownership acceptance.

The DCurses checks are compatibility witnesses for the integration surface its current early release exercises. They are not treated as exhaustive proof of every `Icod.Terminal` contract; Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full 1.x surface.

Tags trigger publication. Tag `v1.0.0` is created only after the exact stable-promotion PR head and resulting exact `main` commit pass their required gates and publication is explicitly authorized.
