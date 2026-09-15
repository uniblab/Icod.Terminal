# Icod.Terminal 1.15.0 Development Roadmap

**Release:** `1.15.0`  
**Theme:** Unicode Placeholder and Virtual Raster Placement  
**Status:** T150–T157 complete; T158 package/API/XML/security/documentation qualification in progress  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** stable `1.14.0`

## Release objective

Version 1.15 adds a backend-neutral semantic abstraction for Unicode-placeholder raster presentation.

The release introduces an opaque `TerminalRasterPlaceholder` handle representing an acknowledged terminal-side virtual raster placement. Applications and higher-level renderers such as `Icod.DCurses` can derive semantic placeholder-cell tokens from that handle and emit them as ordinary terminal text without learning Kitty image ids, placement ids, Unicode placeholder encoding, SGR identity packing, combining-mark tables, or raw APC graphics commands.

The governing rule is:

> Terminal owns protocol identity and encoding; higher-level renderers own cells, cursor position, clipping, scrolling, damage, and layout.

Design authority:

[`docs/superpowers/specs/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement-design.md`](docs/superpowers/specs/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement.md`](docs/superpowers/plans/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement.md)

Permanent ownership authority:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Why this release follows 1.14

The persistent-raster progression is:

```text
1.11  opaque persistent resources and placements
1.12  bounded source-pixel cropping and signed z-order
1.13  immutable-parent relative placement ownership
1.14  lifecycle observability for owned resources and placements
1.15  virtual placements and Unicode-placeholder text-grid rendering
```

Version 1.14 made Terminal's local ownership certainty visible. Version 1.15 uses that ownership/lifecycle foundation to add a different presentation mechanism: an invisible virtual placement whose visible instances are ordinary text cells.

The feature is intentionally designed for higher-level text-grid renderers. It does not make `Icod.Terminal` responsible for screen layout or damage management.

## Frozen public model

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    |       ordinary or relative physical placement
    |
    +-- TerminalRasterPlaceholder
            acknowledged virtual placement
            |
            +-- TerminalRasterPlaceholderCell
                    semantic text-grid token
```

### Placeholder ownership

`TerminalRasterPlaceholder` owns virtual-placement lifetime, rows/columns, private virtual-placement identity, session/generation association, and lifecycle state.

It does not own an absolute screen position, window, DCurses cell, damage state, scrolling policy, layout, or animation.

`TerminalRasterPlaceholderCell` is an immutable semantic token for one placeholder row/column coordinate. Its association with one specific placeholder/session remains private and it is not independently disposable.

## Frozen public API

The T150 public API freeze selected:

```csharp
public sealed class TerminalRasterPlaceholderOptions {
    public int Columns { get; init; }
    public int Rows { get; init; }
}

public sealed class TerminalRasterPlaceholder : IAsyncDisposable {
    public int Columns { get; }
    public int Rows { get; }
    public TerminalRasterOwnershipState OwnershipState { get; }

    public TerminalRasterPlaceholderCell GetCell(
        int row,
        int column
    );

    public ValueTask DisposeAsync();
}

public readonly struct TerminalRasterPlaceholderCell {
    public int Row { get; }
    public int Column { get; }
}
```

`TerminalRasterResource` adds:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlaceholder>>
    CreatePlaceholderAsync(
        TerminalRasterPlaceholderOptions options,
        CancellationToken cancellationToken = default
    );

public ValueTask<TerminalControlResult<TerminalRasterPlacement>>
    CreateRelativePlacementFromPlaceholderAsync(
        TerminalRasterPlaceholder parent,
        int columnOffset,
        int rowOffset,
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );
```

`TerminalSession` adds:

```csharp
public ValueTask WriteRasterPlaceholderCellAsync(
    TerminalRasterPlaceholderCell cell,
    CancellationToken cancellationToken = default
);

public ValueTask WriteRasterPlaceholderCellsAsync(
    ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
    CancellationToken cancellationToken = default
);
```

The distinct `CreateRelativePlacementFromPlaceholderAsync(...)` name is intentional. Adding a second reference-type `CreateRelativePlacementAsync(...)` overload would make existing source such as `CreateRelativePlacementAsync(null!, ...)` ambiguous.

The frozen cross-TFM public API fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```

## Public identity boundary

The 1.15 public surface does not expose:

```text
Kitty image id
Kitty image number
Kitty placement id
virtual-placement protocol id
session generation id
U+10EEEE
row/column combining-mark table
foreground-color image-id packing
underline-color placement-id packing
U=1
raw APC graphics dictionaries
production backend selection
```

The token type has no public constructor that accepts protocol identity.

## Placeholder dimensions and capacities

```text
Columns  1..256
Rows     1..256
```

Placeholder dimensions are required. Version 1.15 does not add source crop or z-order to placeholder options.

Existing capacity ceilings remain one combined bounded ownership system:

```text
maximum live persistent resources                          256
maximum live physical + virtual placements               4096
maximum relative-placement depth                            8
maximum placeholder rows                                  256
maximum placeholder columns                               256
```

Virtual placements count against the existing 4096-placement ceiling.

Private virtual-placement ids use the nonzero 24-bit range:

```text
1..0x00FFFFFF
```

Allocation remains nonzero, collision-safe, monotonic where practical, and wrap-safe.

## Self-contained cell contract

Every generated placeholder cell is independently renderable. Its internal encoding carries complete image identity, complete virtual-placement identity, explicit row, and explicit column.

The implementation does not use left-neighbor shorthand. This preserves correctness for clipping, sparse redraw, scrolling, overlapping rasters, damage-based rendering, arbitrary cell ordering, and future DCurses virtual-screen diffing.

`GetCell(row, column)` is synchronous and side-effect free. It performs no terminal write, query registration, output-gate acquisition, capability verification, lifecycle mutation, cleanup, replay, or backend selection.

Rows/columns outside the declared dimensions are rejected locally.

## Current-cursor emission contract

Placeholder cells are ordinary text-grid output. Single-cell and bulk methods emit at the caller's current text cursor position.

Terminal does not choose screen coordinates or move to a semantic window location. The caller owns cursor movement, clipping, scrolling, layout, and redraw order. Bulk output preserves caller ordering exactly and does not build an unbounded aggregate output buffer.

Placeholder encoding temporarily uses foreground and underline colors as protocol identity channels. Each independently emitted cell terminates private identity state with selective resets equivalent to SGR 39 and SGR 59. Background and unrelated rendition attributes are not reset merely for raster identity.

## Lifecycle model

`TerminalRasterPlaceholder` reuses the 1.14 ownership vocabulary:

```text
new acknowledged placeholder          Current / None
session generation invalidation       Stale / SessionStateLost
correlated missing resource            Stale / ResourceMissing
owning resource intentional disposal   Released / ResourceReleased
explicit placeholder wrapper disposal  Disposed / ExplicitDisposal
```

A stale/released/disposed placeholder never becomes current again.

Placeholder-cell output validates the owning placeholder/session before emitting private identity. Stale, released, disposed, cross-session, or invalid-generation tokens are rejected before output.

## Semantic capability model

Version 1.15 adds:

```text
TerminalCapability.UnicodeRasterPlaceholders = 10
```

The semantic distinction is:

```text
RasterGraphics
    ephemeral raster display

PersistentRasterGraphics
    terminal-resident resource and physical-placement ownership

UnicodeRasterPlaceholders
    virtual-placement ownership and Unicode placeholder-cell rendering
```

Placeholder support uses persistent resources but is not inferred solely from `PersistentRasterGraphics`.

`InspectCapability(...)` remains side-effect free. No terminal-brand heuristic or fictional placeholder-specific live query is introduced. Successful placeholder creation acknowledgement is authoritative for that creation transaction and may strengthen live placeholder evidence.

## Placeholder creation and acknowledgement

`CreatePlaceholderAsync(...)` reuses the authoritative query/input transaction manager. The public handle is returned only after a successful correlated acknowledgement.

Locally knowable invalid operations are rejected before output. Correlated `ENOENT` follows the established missing-resource invalidation model. Malformed responses, wrong identities, timeout, late responses, and generic transport failure do not manufacture missing-resource truth.

If transport fails after placeholder creation commits, no public placeholder is published, no blind retry/backend switch occurs, no raster replay/re-upload occurs, and no unsupported certainty is invented.

Failure while writing visible placeholder text remains ordinary committed output failure and does not automatically stale the virtual placement.

## Virtual placeholder as relative parent

A physical `TerminalRasterPlacement` may use a current `TerminalRasterPlaceholder` as immutable parent. The virtual placeholder itself cannot be relative.

Existing relative-placement guarantees remain:

- immutable parentage;
- signed cell offsets;
- maximum depth 8;
- separate raster-resource lifetime;
- descendant-before-parent cleanup;
- no reparenting;
- `ENOPARENT`, `ECYCLE`, `ETOODEEP`, and `ENOENT` hardening.

Disposing/releasing the virtual parent releases dependent physical placements while child raster resources remain independently owned unless separately released or invalidated.

## DCurses boundary

`Icod.DCurses` owns cells, windows, screen coordinates, clipping, damage, scrolling, layout, refresh ordering, and virtual-screen diffing.

`Icod.Terminal` owns virtual placement lifetime, placeholder token generation, protocol-private identity, placeholder encoding, acknowledgement correlation, lifecycle certainty, and serialized output.

No DCurses source change is required for 1.15 success. Stable downstream package acceptance remains mandatory.

## Icod.TermInfo 1.14 integration boundary

The active 1.15 direct production dependency is:

```text
Icod.TermInfo 1.14.0
Icod.Timing   1.0.0
```

Optional integration tests and `Icod.Terminal.TermInfoPersistentRaster.Sample` use `Icod.TermInfo.Inspection 1.14.0`.

TermInfo 1.14 adds advisory Sixel/Kitty backend availability evidence, candidate evaluation, and explicit backend-selection planning. This is consumed only at the optional integration/application boundary. `RasterBackendPlanner` is **not** used by Icod.Terminal's production router.

Qualified rules are:

- backend availability remains separate from persistent lifecycle and placement truth;
- multiple viable candidates without explicit preference yield a caller decision rather than hidden ranking;
- a conclusive live `PersistentRasterGraphics` result may be caller-mapped to Kitty availability because Terminal's reviewed persistent route is Kitty-based;
- ordinary `RasterGraphics` does not identify Kitty versus Sixel;
- `UnicodeRasterPlaceholders` is not fed into TermInfo 1.14's frozen lifecycle/placement planners;
- separate backend contexts prevent Kitty evidence from silently strengthening Sixel;
- production Terminal routing, protocol commitment, opaque identity, lifecycle, and cleanup remain authoritative.

Inspection and Source remain outside the production package graph.

## Explicit non-goals

Version 1.15 does not add animation/frame lifecycle, absolute screen-coordinate raster placement, pixel-within-cell positioning, general scene graphs, window/cell ownership inside Terminal, reparenting, automatic placeholder redraw, emitted-position tracking, terminal-authenticated existence, hidden source-image replay, caller-visible protocol ids, public raw placeholder encoding helpers, raw Kitty dispatch, Sixel placeholder emulation, image decoding/transcoding, or PTY/ConPTY hosting.

# Tranche roadmap and status

## T150 — Architecture/API-regret gate and public contract freeze — COMPLETE

Frozen the minimum additive public semantic surface, exact method spellings, capability value, identity exclusions, and cross-TFM API fingerprint.

## T151 — Semantic capability and protocol-support evidence model — COMPLETE

Qualified side-effect-free inspection, distinct placeholder semantics, evidence generation/lifetime, and no invented live probe.

## T152 — Virtual-placement ownership and lifecycle integration — COMPLETE

Implemented bounded 24-bit virtual identity, shared placement capacity, acknowledged creation, lifecycle integration, idempotent cleanup, `ENOENT` invalidation, and resource/session teardown.

Acceptance head:

```text
b319051b7830ea2bb6ca7d0a1344e7e4fc67e068
workflow #1820 / 34894849866
```

## T153 — Self-contained placeholder-cell token and encoder — COMPLETE

Implemented exact self-contained Unicode/SGR encoding, full row/column/image identity on every cell, no shorthand, portable 0..255 coordinate tables, and selective identity reset.

Acceptance head:

```text
5666d74f9b91c708869006d3188cb54012bef976
workflow #1823 / 34896470148
```

## T154 — Typed current-cursor placeholder emission — COMPLETE

Qualified single/bulk output, exact caller order, output-gate serialization, cancellation/commitment behavior, token validation, and no unintended query/lifecycle side effects.

Acceptance head:

```text
6b3d8e5393f04fe679de44d6d96851a5700976a1
workflow #1826 / 34897742454
```

## T155 — Virtual-parent relative-placement integration — COMPLETE

Implemented physical placement relative to a virtual placeholder with signed offsets, shared geometry, immutable parentage, cross-resource ownership, depth accounting, descendant-first cleanup, and negative-response classification.

Acceptance head:

```text
4bee4bb76371a1cec2a9e2676c12e1f349e7abda
workflow #1834 / 34899992381
```

## T156 — Error, lifecycle, capacity, and concurrency hardening — COMPLETE

Qualified the 256-resource / 4096-combined-placement bounds, 24-bit wrap/collision handling, placeholder churn, mixed registries, depth boundary, concurrent observation/token generation/output, acknowledgement adversaries, timeout/late response, transport failures, stale/cross-session tokens, and maximum coordinate generation with fixed bounded work.

Acceptance head:

```text
cd219f4661f1f170497514f7c868c2105a93e609
workflow #1850 / 34913940016
```

## T157 — Samples and downstream integration qualification — COMPLETE

Added `samples/Icod.Terminal.RasterPlaceholder.Sample`, backend-neutral source-policy verification, all-TFM sample builds, sparse/out-of-order/current-cursor examples, virtual-parent child placement, and stable Icod.DCurses downstream qualification.

Acceptance head:

```text
4cdb8d432f55d8d9d7e8b9581fb73f8dd31ae32a
workflow #1858 / 34914701316
```

## T158 — Package/API/XML/security/documentation qualification — IN PROGRESS

Completed/implemented work includes:

- fresh package-only placeholder consumer on `net8.0`, `net9.0`, `net10.0`;
- packed generated XML checks for every new public type/member;
- frozen public API fingerprint and identity-exclusion checks;
- package/sample verifier integration;
- stable downstream package acceptance;
- permanent ownership/architecture/security/compatibility documentation synchronization;
- root README and sample-catalog synchronization;
- direct `Icod.TermInfo 1.14.0` dependency alignment;
- optional `Icod.TermInfo.Inspection 1.14.0` integration alignment;
- TermInfo 1.14 raster-backend planner qualification and executable sample adoption without changing the production Terminal router.

Package/API checkpoint:

```text
9bc2fdf8cdb61364adb6c21e54cd55d516f75f72
workflow #1860 / 34915126464
```

The final T158 exact head must pass the full nine-job matrix after all documentation/integration synchronization before this tranche is closed.

## T159 — Stable 1.15.0 release closure — PENDING

T159 will synchronize stable package version/release metadata, changelog, `docs/releases/1.15.0.md`, final README/release links, final API authority, and exact release-candidate bookkeeping.

The release-candidate head must pass:

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

The roadmap must not self-certify the commit that contains its own release-closure status.

Merge, tag, GitHub Release creation, and NuGet publication remain explicit maintainer actions.

## Testing strategy

Testing proceeds from the semantic contract outward:

1. public API freeze;
2. capability evidence contract;
3. virtual-placement ownership and acknowledgement;
4. placeholder-cell token generation and exact encoding;
5. typed current-cursor output;
6. virtual-parent relative-placement bridge;
7. lifecycle/error/capacity/concurrency hardening;
8. sample and downstream acceptance;
9. fresh package/XML/API qualification;
10. optional TermInfo 1.14 integration qualification;
11. complete Windows/Linux/macOS release matrix.

Every public lifecycle/capability state used by the feature has a direct executable witness. Exact byte tests cover protocol encoding; state-machine tests cover ownership; package-only tests cover the shipped surface.

## Compatibility requirements

Version 1.15 is additive over the stable `1.0.0` compatibility floor and complete 1.14 public surface.

Existing consumers that never call placeholder APIs retain existing resource/placement behavior, current-cursor and relative placement behavior, source crop and signed z-order, lifecycle observation, generation-scoped invalidation, 256-resource / 4096-placement ceilings, depth-8 graph bound, deterministic cleanup, no hidden replay, and opaque protocol identity.

No existing public enum numeric value changes.

## Release success definition

Version 1.15 is complete when a consumer can:

```text
create an opaque acknowledged virtual placeholder
    -> derive semantic row/column cell tokens
    -> store/reorder/clip those tokens independently
    -> emit them at caller-owned cursor positions
    -> render every cell without left-neighbor inheritance
    -> observe placeholder lifecycle using the 1.14 state model
    -> use the virtual placeholder as parent for a physical relative placement
```

without learning Kitty numeric identity, placeholder Unicode/diacritic encoding, SGR identity packing, production backend selection, or session generation identity, and without moving window/cell/layout/damage/scene ownership into `Icod.Terminal`.
