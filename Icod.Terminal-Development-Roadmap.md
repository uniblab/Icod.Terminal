# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.8.0`  
**Current status:** Complete — A180 through A189 accepted; exact-head Staging confirmation required before PR readiness  
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

## Current 1.8.0 program — APC and Kitty Graphics

Version 1.8 applies the normalized 1.5 control-language architecture, 1.6 CSI foundation, and 1.7 backend-neutral raster contract to APC/Kitty Graphics.

The detailed program is:

[`Icod.Terminal-1.8.0-Development-Roadmap.md`](Icod.Terminal-1.8.0-Development-Roadmap.md)

The tranche sequence is complete:

```text
A180  APC construction contract and reference freeze           complete
A181  Kitty Graphics control-data and response grammar         complete
A182  backend-neutral raster-to-Kitty raw adaptation           complete
A183  direct Base64 chunk encoder                              complete
A184  committed multi-frame APC graphics transaction           complete
A185  Kitty Graphics live capability probe/correlation         complete
A186  multi-backend raster routing and fallback                complete
A187  raster semantic parity, alpha, geometry, cursor          complete
A188  APC/Kitty hardening and resource closure                 complete
A189  package/documentation/compatibility/release closure      complete
```

### 1.8 architectural result

The common public raster intent introduced in 1.7 now resolves through two reviewed internal backends:

```text
TerminalSession.DisplayRasterAsync(...)
    -> capability/evidence resolution
        -> verified ApcKittyGraphics
            -> RGB24/RGBA32 raw adaptation
            -> bounded Base64 chunks
            -> committed APC transaction
        -> verified DcsSixel
            -> deterministic palette/quantization
            -> bounded Sixel payload segments
            -> committed DCS transaction
```

Version 1.8 adds no public API. `TerminalRasterImage`, `TerminalRasterColor`, `TerminalRasterPixelFormat`, and `TerminalSession.DisplayRasterAsync(...)` retain their 1.7 meaning.

The Kitty implementation provides:

- canonical internal APC framing separated from Kitty dialect syntax;
- typed bounded Kitty control-data and response parsing;
- direct raw RGB24/RGBA32 transmission;
- deterministic Indexed8 expansion preserving referenced palette alpha;
- lazy Base64 chunks with at most 4,096 encoded payload bytes per APC chunk;
- one committed multi-frame session-output transaction with no interleaving;
- protocol-defined Kitty query plus Primary DA synchronization-barrier capability evidence;
- deterministic preference for verified Kitty Graphics with verified Sixel fallback;
- no retry/backend switch after committed graphics output fails;
- backend-native placement/clipping/cursor behavior rather than fabricated cross-backend equivalence;
- hostile-input ownership and bounded recovery for fragmented, malformed, aborted, oversized, and late correlated APC responses.

Direct transmission (`t=d`) is the 1.8 Kitty transport. File, temporary-file, shared-memory, image-file decoding/transcoding, persistent placement/image ownership, z-order, Unicode placeholders, deletion, animation, and public backend selection remain outside the release contract.

### Accepted 1.8 checkpoints

| Tranche | Exact head | Staging workflow |
| --- | --- | --- |
| A180 | `88ac1db0a2904393622082423b8773bd8b19f121` | `34417006958` |
| A181 | `6b5abbdda8ac3ef57bf4c99054a65946e0f13b3a` | `34417741619` |
| A182 | `d9a02336aa2fd61b294064f526ade6021d9d35d2` | `34418298756` |
| A183 | `6f160610bf2df3c666e7d3350e7051475aceca36` | `34418863405` |
| A184 | `d3a7a9262263f28f7ae2d4511520f1b556e5539e` | `34420648720` |
| A185 | `c6a79421b8e8eb3c0c333493f5925f44a2931152` | `34422948067` |
| A186 | `21fb617965f41270e9f3cd43f405fa6ec6e87b2e` | `34426364013` |
| A187 | `80d78b74fe780421bf2e667ef434d38775c0dd48` | `34427249773` |
| A188 | `997feb9628d34389199ca3fffdf90e829f33819a` | `34469956370` |
| A189 qualification | `0de1a1c6b95b2b36407a94935b3126c8bfd6ca5a` | `34476585310` |

Each accepted checkpoint passed Windows, Linux, macOS runtime/source validation, package candidate/public-API freeze, all four package-contract shards, and the validated package artifact.

### 1.8 public API baseline

Version 1.8 adds no public API. The authoritative current machine fingerprint therefore remains the 1.7 raster baseline:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

The current baseline files remain:

- `docs/Public-API-Baseline-1.7.md`
- `docs/Public-API-Baseline-1.7.sha256`

No redundant 1.8 baseline is created. Historical stable baselines remain checked in unchanged.

### A189 closure result

A189 adds no public feature. It closes 1.8 with:

- complete 1.8 curated release notes satisfying the Stable 1.x release-line package gate;
- fresh NuGet-only raster/XML qualification on all three TFMs against the unchanged public raster API;
- package exclusion checks for raw DCS/Sixel and APC/Kitty escape hatches;
- current public API fingerprint verification;
- retained historical stable package contracts;
- current `Icod.DCurses` package-boundary compatibility witness;
- synchronized README, changelog, architecture, security, compatibility, semantic-output, graphics-roadmap, versioned roadmap, NuGet metadata, and PR ledger;
- this overall roadmap promoted to the 1.8 release line.

The A189 qualification head `0de1a1c6b95b2b36407a94935b3126c8bfd6ca5a` passed Staging workflow #1355 / `34476585310` across the full matrix. Only status/evidence closure edits follow it; the actual final PR head must pass the same matrix before PR #46 is marked ready for review.

Closure authority:

[`docs/A189-1.8.0-Package-Documentation-Compatibility-and-Release-Closure.md`](docs/A189-1.8.0-Package-Documentation-Compatibility-and-Release-Closure.md)

## Completed 1.7.0 program — DCS and Sixel raster graphics

The 1.7 release applied the normalized 1.5 control-language architecture and 1.6 CSI foundation to DCS/Sixel graphics and introduced the common public raster model used unchanged by 1.8.

The completed program is preserved at:

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

D179 qualification and final status-only closure both passed the full Staging matrix before PR #45 left draft and was later merged to `main`.

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

Version 1.6 added no public API. It retained the 1.4 public fingerprint while establishing the complete CSI and pixel-geometry substrate used by 1.7 and 1.8.

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

The completed control-language/graphics architecture is maintained in:

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
1.8  unchanged from 1.7
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
- `docs/A180-APC-Construction-Contract-and-Reference-Freeze.md`
- `docs/A181-Kitty-Graphics-Control-Data-and-Response-Grammar.md`
- `docs/A182-Backend-Neutral-Raster-to-Kitty-Raw-Adaptation.md`
- `docs/A183-Direct-Kitty-Base64-Chunk-Encoder.md`
- `docs/A184-Committed-Multi-Frame-APC-Graphics-Transaction.md`
- `docs/A185-Kitty-Graphics-Live-Capability-Probe-and-Correlation.md`
- `docs/A186-Multi-Backend-Raster-Routing-and-Fallback.md`
- `docs/A187-Raster-Semantic-Parity-Alpha-Geometry-and-Cursor.md`
- `docs/A188-APC-Kitty-Hardening-Fragmentation-and-Resource-Closure.md`
- `docs/A189-1.8.0-Package-Documentation-Compatibility-and-Release-Closure.md`
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
- `docs/releases/1.8.0.md`
- `docs/releases/1.7.0.md`
- `CHANGELOG.md`

Historical N150–N159, C160–C165, T-series, 0.x baselines, rc1, and prior stable release documents remain design/release evidence.

## Planned next release

The next release line is intentionally not frozen by 1.8 closure. Advanced graphics placement/lifecycle features require separate semantic, ownership, and security review rather than being implied by Kitty transport support.

Potential future work may include typed placement/lifecycle functionality, but it must preserve the backend-neutral raster contract and must not expose generic Kitty/APC dispatch as a shortcut.

## Release discipline

Pull requests validate Staging on Windows, Linux, and macOS runtime/source paths while one portable package candidate feeds four package-contract shards.

`main` Release validation runs runtime/source checks across Windows/Linux/macOS x64/ARM64 plus the portable package contract.

For 1.8:

1. A180–A188 are accepted on exact green Staging heads;
2. A189 qualification head `0de1a1c6b95b2b36407a94935b3126c8bfd6ca5a` passed the complete Staging matrix in workflow #1355 / `34476585310`;
3. the status/evidence-only final PR head must pass the same matrix before PR #46 leaves draft status;
4. merge remains explicit;
5. the merged `main` head must pass Release distribution validation;
6. `v1.8.0` tagging/publication remains a separate explicit authorization.
