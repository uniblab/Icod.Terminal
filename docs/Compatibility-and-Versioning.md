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

Every API-bearing stable minor release records a deterministic reflection snapshot and SHA-256 fingerprint. Historical baselines are immutable evidence and are never rewritten merely because a later release adds compatible members.

Relevant fingerprints include:

```text
1.9   e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
1.10  ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
1.11  9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
1.12  eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
1.13  c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
1.14  2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
1.15  eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
1.16  d2acfa85aad87c739b3f682096d4d7139627f12bc9d8981b65529eeb79a2da8d
1.17  c0a051a925d551e526343ef59d8c47d75e41868d84235fa30bfa7debe1b3ceb9 (alpha development)
```

Version 1.11.1 intentionally retained the 1.11 fingerprint because it added no production public API.

The final 1.16 fingerprint is enforced across `net8.0`, `net9.0`, and `net10.0`; all historical fingerprints remain immutable.

The 1.17 development fingerprint is likewise identical across all three target frameworks. It remains an alpha authority until stable release closure.

## 4. Additive persistent-raster progression

The stable/additive progression is:

```text
1.11  opaque persistent resources and physical placements
1.12  bounded source-pixel cropping and signed z-order
1.13  bounded immutable-parent relative placement ownership
1.14  side-effect-free ownership-state observation
1.15  opaque virtual placements and typed Unicode-placeholder cells
1.16  resource-owned animation frames and playback lifecycle
```

Each release preserves earlier behavior when its new APIs are unused.

### 4.1 1.12 optional geometry

`TerminalRasterSourceRectangle` and `TerminalRasterPlacementOptions.ZIndex` remain optional. Existing 1.11 placement calls retain their established semantics when those values are absent.

### 4.2 1.13 relative placement

`CreateRelativePlacementAsync(TerminalRasterPlacement, ...)` and `UpdateRelativeAsync(...)` add immutable parentage and signed cell offsets without changing ordinary current-cursor placement behavior.

Resource ownership and placement-parent lifetime remain independent. The portable relative-depth ceiling is 8. There is no public reparenting operation.

### 4.3 1.14 lifecycle observation

`TerminalRasterOwnershipState` and `OwnershipState` properties expose Terminal's local ownership certainty without terminal I/O or a fabricated passive existence query.

Reading the snapshot does not alter existing resource/placement behavior.

### 4.4 1.15 virtual placeholders

Version 1.15 adds:

```text
TerminalCapability.UnicodeRasterPlaceholders = 10
TerminalRasterPlaceholderOptions
TerminalRasterPlaceholder
TerminalRasterPlaceholderCell
TerminalRasterResource.CreatePlaceholderAsync(...)
TerminalRasterResource.CreateRelativePlacementFromPlaceholderAsync(...)
TerminalSession.WriteRasterPlaceholderCellAsync(...)
TerminalSession.WriteRasterPlaceholderCellsAsync(...)
```

The new relative-parent operation deliberately uses a distinct name rather than adding a second reference-type overload of `CreateRelativePlacementAsync(...)`; this avoids creating source ambiguity for existing calls that pass `null` to the physical-parent overload.

Virtual and physical placements share the existing combined 4096 live-placement ceiling. Placeholder dimensions are independently bounded to `1..256`; virtual-placement protocol identity remains private.

The virtual placeholder itself is not relative. A physical placement may use a current placeholder as immutable relative parent, preserving the existing depth and descendant-lifetime model.

Typed placeholder-cell output is current-cursor output. It does not transfer screen-coordinate/layout/clipping/damage ownership into Terminal.

### 4.5 1.16 persistent raster animation

Version 1.16 additively introduces:

```text
TerminalCapability.PersistentRasterAnimation = 11
TerminalRasterAnimation
TerminalRasterAnimationFrame
TerminalRasterAnimationState
TerminalRasterAnimationStatus
TerminalRasterAnimationLossReason
TerminalRasterAnimationPlaybackOptions
TerminalRasterResource.Animation
```

The resource's original image is the root frame. Full-size acknowledged additions return opaque frame tokens. The API adds positive whole-millisecond frame timing, explicit frame selection, stop, loading-mode playback, and finite/indefinite normal playback without exposing protocol frame numbers or control dictionaries.

The controller is resource owned and not independently disposable. Animation sequence certainty is separate from resource ownership: an ambiguous committed append can make the animation `SequenceUncertain` while the resource remains current.

The session-wide animation registry is bounded to 4096 known frames including roots, with one pending append per animation. No source-frame replay cache, partial-frame update, composition, or decoder dependency is added.

### 4.6 1.17 semantic screen output

Version 1.17 additively introduces Terminal-owned dimensions, an immutable semantic profile, TermInfo-free screen values, opaque costed operation plans, and bounded session-bound output transactions.

Existing `GetSize()`, `TerminalLifecycleEvent.Size`, `TerminalSession.Terminal`, low-level terminal-string output, and other 1.x APIs remain available. The new APIs do not move retained cells, layout, Unicode-width policy, damage, refresh comparison, or repaint ownership into Terminal.

## 5. Persistent-raster compatibility guarantees

The following remain compatible guarantees:

- public resource/physical-placement/virtual-placement/animation-frame protocol identities stay opaque;
- ordinary physical placement uses the terminal's current cursor location;
- typed placeholder-cell output uses the caller's current cursor location;
- physical placement `Columns`/`Rows` remain independently optional and bounded to `1..16384`;
- placeholder `Columns`/`Rows` are required and bounded to `1..256`;
- create/update operations that require acknowledgement reuse the authoritative query path;
- persistent ownership remains generation scoped;
- stale mutation/output returns controlled failure before stale identity is emitted;
- stale disposal remains local-only;
- live resource ceiling remains 256 and combined physical/virtual placement ceiling remains 4096 per session;
- known animation-frame capacity remains 4096 per session, including root frames;
- relative depth remains bounded to 8;
- no hidden source-image cache or automatic replay is introduced;
- persistent transport remains direct Kitty transfer internally;
- cleanup remains descendant-before-parent and placement/placeholder before resource data.

## 6. Query/input compatibility

Stable 1.x preserves one authoritative terminal input conversation.

Application input, lifecycle observations, query responses, semantic events, raster capability probes, persistent-raster acknowledgements, placeholder acknowledgements, and animation frame/control acknowledgements remain coordinated by the same reader/router model.

Compatibility includes:

- wrong query identities not satisfying another operation;
- malformed owned responses remaining owned/recovered according to bounded parsing rules rather than leaking into application input;
- timeout not automatically becoming unsupported capability truth;
- late responses not satisfying a later transaction with a different correlation identity;
- caller cancellation preserving the established pre-commit/post-commit distinction.

## 7. Capability-planning compatibility

Public Terminal capability planning remains semantic and dependency-neutral.

`InspectCapability(...)` is side-effect free. `VerifyCapabilityAsync(...)` is explicit and uses only reviewed bounded live probes.

Endpoint availability remains separate from support knowledge. Static advertisement, live verification, unknown state, and unsupported state are not collapsed merely to make routing simpler.

`RasterGraphics`, `PersistentRasterGraphics`, and `UnicodeRasterPlaceholders` remain distinct semantic capabilities. Generic raster or persistent support must not silently manufacture placeholder support.

## 8. TermInfo 1.14 optional integration compatibility

The active 1.16 direct production dependency graph remains:

```text
Icod.TermInfo 1.14.0
Icod.Timing   1.0.0
```

Optional integration tests/samples use `Icod.TermInfo.Inspection 1.14.0`; Inspection and Source remain outside the production graph.

Inspection 1.14 adds advisory raster-backend evidence/candidate/selection planning. Icod.Terminal qualifies that API at the consumer boundary but does **not** use `RasterBackendPlanner` in its production router.

The compatibility rule is:

```text
Terminal semantic capability/routing state
    remains Terminal-owned

TermInfo backend planning
    remains caller-owned advisory policy
```

A caller may map conclusive live `PersistentRasterGraphics` evidence to Kitty Graphics availability because Terminal's reviewed persistent implementation is Kitty-based. That does not make ordinary `RasterGraphics` a concrete-backend identity, and it does not make `UnicodeRasterPlaceholders` an input to TermInfo 1.14's frozen lifecycle/placement planners.

Separate backend contexts prevent evidence for one candidate from silently strengthening another. Multiple viable candidates without explicit preference remain an application decision rather than hidden ranking.

Historical release documents retain the TermInfo/Inspection versions actually shipped by those releases and are not rewritten by this dependency advance.

## 9. Committed-output compatibility

Committed graphics operations do not intentionally truncate after commitment merely because ordinary caller cancellation arrives.

Partial transport failure is surfaced without blind replay or automatic backend switching. This applies to ephemeral raster output, persistent upload/placement/placeholder transactions, animation frame/control transactions, and typed placeholder-cell output according to their existing logical transaction boundaries.

## 10. Backend-neutral public contracts

Stable public APIs describe terminal semantics rather than internal protocol choices wherever practical.

The following remain implementation details rather than compatibility promises:

- Terminal's internal Kitty/Sixel routing scores/registry order;
- raw APC/DCS control dictionaries;
- private image numbers/image ids/physical/virtual placement ids and animation frame numbers;
- session generation ids;
- Unicode placeholder reserved codepoint and combining-mark tables;
- SGR identity packing;
- internal parser/helper class names not exposed publicly.

The optional `Icod.TermInfo.Inspection` backend vocabulary is a separate consumer planning API. Its presence in a sample/test does not expose a caller-selected raw backend switch in `Icod.Terminal` production API.

## 11. Deliberate non-promises after 1.16

Stable 1.x does not promise:

- generic raw vendor command/event dispatch;
- terminal-brand-based support truth;
- automatic graphics replay/re-upload after lifecycle invalidation;
- Sixel emulation of persistent resource or placeholder ownership;
- public/caller-selected Terminal production raster backend;
- public Kitty protocol identities;
- retained persistent source-image cache;
- mutable/reparentable placement graphs;
- absolute screen-coordinate layout owned by `Icod.Terminal`;
- pixel-within-cell positioning;
- automatic placeholder redraw or emitted-screen-position tracking;
- partial-frame animation updates, frame composition, and delta editing;
- scene-graph/cells/windows/damage/layout ownership;
- image-file decoding/transcoding;
- PTY/ConPTY process hosting inside this package.

Relative placement, lifecycle observation, virtual placeholders, and resource-owned animation are deliberately bounded additions and must not be interpreted as promises for these excluded features.

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

The package/public-API gates verify the frozen API fingerprint, generated XML documentation, fresh package-only consumption, dependency boundaries, and downstream acceptance/hardening defined by the repository release contract.

Any post-closure pre-merge code or documentation change requires the same exact-head matrix again before the PR is considered merge-ready.

## 13. Maintainer release actions

PR qualification does not itself merge, tag, create a GitHub Release, or publish NuGet packages.

For 1.16, the maintainer/release workflow remains responsible for:

1. merging the fully qualified PR;
2. validating the mainline Release workflow;
3. creating/pushing `v1.16.0` only after mainline validation succeeds;
4. creating the GitHub Release and publishing NuGet through the established release workflow.
