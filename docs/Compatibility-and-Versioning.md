# Compatibility and Versioning

This document defines the permanent stable 1.x compatibility policy for `Icod.Terminal`.

## 1. Stable compatibility floor

`1.0.0` is the stable compatibility floor.

Within the 1.x line, releases are expected to preserve existing public signatures, enum numeric values, result/status semantics, ownership/lifecycle guarantees, and documented protocol-neutral behavior except where a later compatible release adds optional functionality.

A minor release may add new public members and new semantic capabilities. It must not silently repurpose existing public members or require callers to opt into new behavior merely to retain previously documented semantics.

## 2. Supported target frameworks

The stable package targets:

```text
net8.0
net9.0
net10.0
```

A stable release must qualify the public package/runtime graph on all supported TFMs and on Windows, Linux, and macOS through the repository's release-validation matrix.

## 3. Public API baselines

Every API-bearing stable minor release records a deterministic reflection snapshot and SHA256 fingerprint. Historical baselines are immutable evidence and are never rewritten merely because a later release adds compatible members.

Relevant fingerprints include:

```text
1.9   e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
1.10  ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
1.11  9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
1.12  eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

Version 1.11.1 intentionally retained the 1.11 fingerprint because it added no production public API.

## 4. 1.12 additive public API

The only intended additions relative to 1.11 are:

```text
TerminalRasterSourceRectangle
TerminalRasterSourceRectangle..ctor(int,int,int,int)
TerminalRasterSourceRectangle.X
TerminalRasterSourceRectangle.Y
TerminalRasterSourceRectangle.Width
TerminalRasterSourceRectangle.Height
TerminalRasterPlacementOptions.SourceRectangle
TerminalRasterPlacementOptions.ZIndex
```

All existing public signatures and enum numeric values are preserved.

No new `TerminalCapability` numeric value is introduced by 1.12.

## 5. Persistent-raster compatibility

Version 1.11 established opaque persistent raster resources/placements and is the behavioral base for 1.12.

The following remain compatible guarantees:

- public resource/placement protocol identities stay opaque;
- placement position stays the terminal's current cursor location;
- `Columns`/`Rows` remain independently optional and bounded to `1..16384`;
- create/update remain acknowledged operations through the authoritative query path;
- persistent ownership remains generation scoped;
- stale mutation returns controlled `Unavailable` before output;
- stale disposal remains local-only;
- live resource/placement ceilings remain `256` / `4096` per session;
- no hidden source-image cache or automatic replay is introduced;
- persistent transport remains direct Kitty transfer internally;
- current cleanup remains child placement before resource data.

### 5.1 1.12 optional placement geometry

`SourceRectangle` and `ZIndex` are optional. When both are absent, the existing 1.11 persistent placement byte/semantic contract is preserved.

A caller upgrading from 1.11 does not need to change existing placement code.

When `SourceRectangle` is supplied:

- it selects a source-image pixel region;
- its intrinsic scalar contract is revalidated even when the value came from `default(TerminalRasterSourceRectangle)` rather than the public constructor;
- it must fit completely inside the owning resource;
- invalid rectangles are rejected before new placement output;
- source dimensions are retained as metadata only, not as a replay pixel cache.

When `ZIndex` is supplied, its complete signed `int` value is preserved as signed stacking order. This does not create a general scene-layout contract or relative-placement graph.

## 6. Query/input compatibility

Stable 1.x preserves one authoritative terminal input conversation.

Application input, lifecycle observations, query responses, semantic events, raster capability probes, and persistent-raster acknowledgements remain coordinated by the same reader/router model.

Compatibility includes:

- wrong query identities not satisfying another operation;
- malformed owned responses remaining owned/recovered according to bounded parsing rules rather than leaking into application input;
- timeout not automatically becoming unsupported capability truth;
- late responses not satisfying a later transaction with a different correlation identity;
- caller cancellation preserving the established pre-commit/post-commit distinction.

## 7. Capability-planning compatibility

Public capability planning remains semantic and dependency-neutral.

`InspectCapability(...)` is side-effect free. `VerifyCapabilityAsync(...)` is explicit and uses only reviewed bounded live probes.

Endpoint availability remains separate from support knowledge. Static advertisement, live verification, unknown state, and unsupported state are not collapsed merely to make routing simpler.

The internal table-driven TermInfo evidence cleanup completed for 1.12 is behavior preserving and does not alter public evidence states or package dependencies.

## 8. Committed-output compatibility

Committed graphics operations do not intentionally truncate after commitment merely because ordinary caller cancellation arrives.

Partial transport failure is surfaced without blind replay or automatic backend switching. This applies to ephemeral raster output and persistent upload/placement transactions according to their existing logical transaction boundaries.

## 9. Package dependency compatibility

`Icod.Terminal.csproj` remains the direct production dependency authority.

Version 1.12 keeps:

```text
Icod.TermInfo 1.11.0
Icod.Timing   1.0.0
```

Tests, samples, and package-only consumers may reference extra tooling/inspection packages without making those dependencies part of the production package graph.

Stable package qualification verifies restore/build and executable NuGet-only consumption rather than turning one incidental transitive-resolution outcome into a public behavioral promise.

## 10. Backend-neutral public contracts

Stable public APIs describe terminal semantics rather than internal protocol choices wherever practical.

The following remain implementation details rather than compatibility promises:

- Kitty/Sixel backend routing scores;
- raw APC/DCS control dictionaries;
- private image numbers/image ids/placement ids;
- internal registry ordering;
- table representation used for TermInfo semantic evidence;
- concrete parser/helper class names not exposed publicly.

A public semantic operation may be implemented by one reviewed backend today without exposing that backend as caller-controlled policy.

## 11. Deliberate non-promises

Stable 1.x does not promise:

- generic raw vendor command/event dispatch;
- terminal-brand-based support truth;
- automatic graphics replay/re-upload after lifecycle invalidation;
- Sixel emulation of persistent resource ownership;
- caller-selected persistent raster backend;
- public Kitty protocol identities;
- retained persistent source-image cache;
- relative placement graphs or parent placement identities;
- absolute screen-coordinate layout owned by `Icod.Terminal`;
- Unicode placeholder/virtual placements;
- animation/frame lifecycle;
- scene-graph/cells/windows/damage/layout ownership;
- image-file decoding/transcoding;
- PTY/ConPTY process hosting inside this package.

Version 1.12 source rectangles and z-order are deliberately narrow additions and must not be interpreted as promises for these excluded features.

## 12. Release qualification

A stable release candidate is accepted only after the exact head passes:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / public API freeze
Package Foundation
Package Presentation
Package Semantic and hardening
Package Stable 1.x release line
Validated package artifact
```

The Stable 1.x package shard includes fresh package-only consumption and downstream acceptance/hardening where defined by the repository release contract.

Any post-closure pre-merge code or documentation change requires the same exact-head matrix again before the PR is considered merge-ready.

## 13. Maintainer release actions

PR qualification does not itself merge, tag, create a GitHub Release, or publish NuGet packages.

For 1.12, the maintainer/release workflow remains responsible for:

1. merging the qualified PR;
2. validating the mainline Release workflow;
3. creating/pushing `v1.12.0` only after mainline validation succeeds;
4. creating the GitHub Release and publishing NuGet through the established release workflow.
