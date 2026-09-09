# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.7.0`  
**Current status:** Complete — D170 through D179 accepted; final status-only head validating before PR readiness  
**Stable compatibility floor:** `1.0.0`

## Purpose

This file is the permanent entry point for current `Icod.Terminal` development and release planning.

The original pre-1.0 roadmap remains preserved at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

Stable `1.0.0` established the permanent 1.x contract. Minor releases may add compatible typed semantic APIs and internal protocol infrastructure while preserving the documented ownership, lifecycle, query, security, and compatibility guarantees.

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
- `Icod.Terminal` owns the live terminal conversation, native terminal modes, input decoding, lifecycle, query routing, capability evidence, semantic terminal output, raster output, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, refresh/diff policy, and higher-level curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## Completed 1.7.0 program — DCS and Sixel raster graphics

The 1.7 release applies the normalized 1.5 control-language architecture and 1.6 CSI foundation to DCS/Sixel graphics.

The detailed program is:

[`Icod.Terminal-1.7.0-Development-Roadmap.md`](Icod.Terminal-1.7.0-Development-Roadmap.md)

The tranche sequence is complete:

```text
D170  DCS construction contract and reference freeze   complete
D171  existing DCS reconciliation                      complete
D172  Sixel grammar and codec contract                 complete
D173  common raw raster model                          complete
D174  deterministic palette and quantization           complete
D175  Sixel encoder                                    complete
D176  committed streaming graphics output              complete
D177  Sixel capability evidence/live observation       complete
D178  first semantic raster-display operation          complete
D179  hardening/package/documentation closure           complete
```

### 1.7 architectural result

Version 1.7 separates the graphics stack into explicit layers:

```text
semantic raster intent
    -> capability/evidence resolution
        -> DcsSixel backend
            -> Sixel quantizer/encoder
                -> DCS framing
                    -> committed session output
```

The implementation provides:

- canonical internal DCS construction;
- byte-stable DECRQSS and XTGETTCAP migration;
- canonical Sixel grammar and RGB palette semantics;
- bounded immutable-owned RGB24/RGBA32/Indexed8 raster data;
- deterministic bounded palette reduction;
- deterministic six-row Sixel encoding;
- bounded lazy payload segments;
- committed DCS output through the existing session output gate;
- Primary DA attribute `4` as positive Sixel evidence without terminal-brand heuristics;
- public backend-neutral `TerminalRasterImage` / `TerminalRasterColor` / `TerminalRasterPixelFormat`;
- public `TerminalSession.DisplayRasterAsync(...)` semantic display.

The public raster contract is intentionally smaller than the internal Sixel implementation. It does not expose raw DCS/Sixel writing, Sixel palette-register controls, an explicit backend selector, image-file decoding, generalized placement/scaling, animation, or persistent image identifiers.

### Accepted 1.7 checkpoints

| Tranche | Exact head | Staging workflow |
| --- | --- | --- |
| D170 | `7d338fe5a1de122738d4573093db90e1500003a1` | `34399549931` |
| D171 | `277db7da8a586dda44966fa77990b4a9f32e953a` | `34400644772` |
| D172 | `75d5562e5b1c0813a52d43f445365a228948a5f3` | `34401396507` |
| D173 | `b2674f6cb356ca49a0b2564721c12f2eae9b9265` | `34402697961` |
| D174 | `bf0542b77127a5af4d627480cb8d3797710dc93e` | `34405392319` |
| D175 | `005992b27c95fd5c0142d23922aee40e929237e8` | `34407124131` |
| D176 | `9e48c1cee4e44a0f272378ff4f60b4cabc9bed8a` | `34408371837` |
| D177 | `2e241b6d0fcec1d68c6b6aaeaf665750ddf0a053` | `34411677303` |
| D178 | `f9428927168524be5cc552c82ad00e2fcda70b13` | `34412478452` |
| D179 qualification | `2c813df67c3b3aa7d22401ffd2f5a26b1d9d728d` | `34414298714` |

Each accepted checkpoint passed Windows, Linux, macOS runtime/source validation, package candidate, all four package-contract shards, and the validated package artifact. The D179 qualification head contains the complete implementation, hardening, release metadata, package-only raster qualification, and synchronized documentation. Only status-only closure edits follow it.

### 1.7 public API baseline

D178 intentionally advances the current machine public API fingerprint to:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

The current baseline is:

- `docs/Public-API-Baseline-1.7.md`
- `docs/Public-API-Baseline-1.7.sha256`

Historical stable baselines remain checked in unchanged.

### D179 closure result

D179 added no new public feature. It closed the release with:

- maximum-width high- and low-compressibility Sixel segment-bound tests;
- fresh package-only raster/XML qualification on all three TFMs;
- current 1.7 public API fingerprint verification;
- retained historical stable package contracts;
- current `Icod.DCurses` package-boundary compatibility witness;
- synchronized README, changelog, architecture, security, compatibility, release notes, package documentation, both roadmaps, NuGet metadata, and PR ledger.

The complete qualification candidate `2c813df67c3b3aa7d22401ffd2f5a26b1d9d728d` passed Staging workflow #1291 / `34414298714` across the full matrix. The subsequent status-only closure head must pass the same matrix before PR #45 is marked ready.

Closure authority:

[`docs/D179-1.7.0-Hardening-Package-and-Documentation-Closure.md`](docs/D179-1.7.0-Hardening-Package-and-Documentation-Closure.md)

## Completed 1.6.0 program — complete CSI grammar and geometry

Version 1.6 completed the first normalized protocol-family tranche:

```text
C160  complete CSI grammar foundation
C161  typed CSI parameter semantics
C162  existing CSI consolidation
C163  terminal/cell pixel geometry
C164  CSI hardening, fragmentation, and recovery
C165  acceptance/package/documentation closure
```

The completed program is preserved at:

[`Icod.Terminal-1.6.0-Development-Roadmap.md`](Icod.Terminal-1.6.0-Development-Roadmap.md)

Version 1.6 added no public API. It retained the 1.4 public fingerprint while establishing the complete CSI and pixel-geometry substrate used by 1.7.

## Completed 1.5.0 program — control-language normalization

Version 1.5 separated:

```text
semantic intent
    -> capability/evidence resolution
        -> protocol backend selection
            -> control family framing
                -> dialect codec / wire transport
```

The N150–N159 program added generalized CSI/DCS/OSC/APC/PM/SOS framing, one bounded scanner, structural frame parsing, multi-family query transactions, capability evidence, semantic backend registration/routing, and existing-protocol reconciliation.

The completed program is preserved at:

[`Icod.Terminal-1.5.0-Development-Roadmap.md`](Icod.Terminal-1.5.0-Development-Roadmap.md)

The longer graphics architecture is maintained in:

[`docs/Control-Language-Normalization-and-Graphics-Roadmap.md`](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)

## Earlier stable releases

### 1.4.0 — Kitty OSC 99 desktop notifications

Added typed send/update/close/support/alive notification APIs with bounded chunking, explicit capability queries, and no generic raw OSC 99 dispatch.

### 1.3.0 — iTerm2 OSC 1337 shell integration

Added typed `SetMark`, `CurrentDir`, `RemoteHost`, `SetUserVar`, shell-integration version, and clear-captured-output operations while keeping portable OSC 7/133 independent.

### 1.2.0 — OSC 777 titled notifications

Added bounded typed titled desktop notifications while preserving OSC 9 independently.

### 1.1.0 — VS Code OSC 633

Added typed VS Code shell integration rather than aliasing it to portable OSC 133.

### 1.0.0 — stable contract

Established the permanent 1.x ownership, lifecycle, one-reader, query, output-serialization, security, compatibility, licensing, and package-validation contract.

## Public API baseline history

```text
1.0  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
1.3  0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
1.4  3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
1.5  unchanged from 1.4
1.6  unchanged from 1.4/1.5
1.7  847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Compatible additions get a new reviewed baseline; older baseline files remain historical compatibility evidence.

## Permanent 1.x authorities

Current contract authorities include:

- `docs/Architecture.md`
- `docs/Terminal-Session-and-Ownership.md`
- `docs/Lifecycle-and-Restoration.md`
- `docs/Input-and-Events.md`
- `docs/Queries-and-Responses.md`
- `docs/Modern-Keyboard-Security-and-Compatibility.md`
- `docs/Presentation-and-Reversible-State.md`
- `docs/Semantic-Output-Protocols.md`
- `docs/Control-Language-Normalization-and-Graphics-Roadmap.md`
- `docs/D170-DCS-Construction-Contract-and-Reference-Freeze.md`
- `docs/D171-Existing-DCS-Reconciliation.md`
- `docs/D172-Sixel-Grammar-and-Codec-Contract.md`
- `docs/D173-Common-Raw-Raster-Model.md`
- `docs/D174-Deterministic-Sixel-Palette-and-Quantization.md`
- `docs/D175-Sixel-Encoder.md`
- `docs/D176-Committed-Streaming-Graphics-Output.md`
- `docs/D177-Sixel-Capability-Evidence-and-Live-Observation.md`
- `docs/D178-First-Semantic-Raster-Display-Operation.md`
- `docs/D179-1.7.0-Hardening-Package-and-Documentation-Closure.md`
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.7.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.7.0.md`
- `CHANGELOG.md`

Historical N150–N159, C160–C165, T-series, 0.x baselines, rc1, and prior stable release documents remain design/release evidence.

## Planned next release

The next graphics tranche remains:

```text
1.8.0  APC foundation / Kitty Graphics / multi-backend raster routing
```

The 1.8 design should reuse `TerminalRasterImage` and `DisplayRasterAsync(...)` rather than adding a second incompatible public image model.

## Release discipline

Pull requests validate Staging on Windows, Linux, and macOS runtime/source paths while one portable package candidate feeds four package-contract shards.

`main` Release validation runs runtime/source checks across Windows/Linux/macOS x64/ARM64 plus the portable package contract.

For 1.7:

1. the complete D179 qualification candidate has passed the full Staging matrix;
2. the status-only final head must pass the same matrix before PR #45 leaves draft status;
3. merge remains explicit;
4. the merged `main` head must pass Release distribution validation;
5. `v1.7.0` tagging/publication remains a separate explicit authorization.
