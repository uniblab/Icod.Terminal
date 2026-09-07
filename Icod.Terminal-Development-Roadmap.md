# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.0.0-rc1`  
**Current roadmap:** [`Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`](Icod.Terminal-1.0.0-rc1-Development-Roadmap.md)  
**Stable contract target:** `1.0.0`

## Purpose

This file is the permanent entry point for current `Icod.Terminal` development planning.

The repository began with a detailed pre-1.0 architecture and milestone plan. That original document remains valuable historical design evidence and is preserved verbatim at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

The current release-specific roadmap, rather than the historical initial plan, is authoritative for active development state.

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

## Current 1.0 release-candidate program

`1.0.0-rc1` is a contract-freeze release rather than a protocol-expansion release.

Its tranches are:

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

See the current rc1 roadmap for exact tranche status and validation workflow numbers.

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
- `docs/Public-API-Baseline-1.0-rc1.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`

Historical `Txxx`, `Public-API-Baseline-0.x`, and version-specific roadmap files remain design/release evidence.

## Release discipline

Pull requests validate the Staging configuration on Windows, Linux, and macOS. Distribution validation on `main` uses Release across the configured Windows/Linux/macOS x64/ARM64 matrix.

The release candidate retains:

- full build/test coverage on `net8.0`, `net9.0`, and `net10.0`;
- the frozen 1.0 public-API fingerprint;
- exact NuGet artifact/XML verification;
- historical package-only contracts from 0.8 through 0.18;
- a fresh 1.0 release-candidate package-only contract;
- current `Icod.DCurses 0.1.0` project-reference and package-boundary integration/ownership acceptance.

The DCurses checks are compatibility witnesses for the integration surface its current early release exercises. They are not treated as exhaustive proof of every `Icod.Terminal` contract; Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full 1.x surface.

Tags trigger publication. A tag is created only after the exact release candidate has passed the required PR and post-merge Release gates and publication is explicitly authorized.
