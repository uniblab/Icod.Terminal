# A187 — Raster Semantic Parity, Alpha, Geometry, and Cursor

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A187  
**Status:** complete — accepted on exact head `80d78b74fe780421bf2e667ef434d38775c0dd48`, Staging workflow #1340 / `34427249773`

## Purpose

A187 freezes the semantic boundary shared by the two verified raster backends now available to `TerminalSession.DisplayRasterAsync(...)` without manufacturing a false equivalence between Kitty Graphics and Sixel.

The public operation remains backend-neutral. The library does not expose protocol-specific placement, image identifiers, palette registers, cursor controls, or scaling controls.

## Intrinsic dimensions

The caller's `TerminalRasterImage.Width` and `Height` remain the source raster's intrinsic pixel dimensions.

For Kitty Graphics, those dimensions are sent directly as the direct-transfer `s` and `v` source dimensions.

For Sixel, those dimensions remain the declared raster extent used by the existing Sixel encoder.

Icod.Terminal 1.8 performs no hidden resize, resampling, aspect correction, terminal-cell fitting, or geometry-derived scaling.

## Alpha semantics

The common raster model preserves straight RGBA alpha.

Kitty Graphics accepts RGB24 and RGBA32 direct transfer, so alpha values `1..254` are preserved byte-for-byte through the Kitty path.

Sixel has no equivalent general per-pixel alpha channel in the reviewed 1.7 contract. Its existing conversion policy therefore remains:

- alpha `0` — transparent/unpainted;
- alpha `255` — opaque;
- alpha `1..254` — controlled `Unsupported` result before graphics commitment.

The library does not silently matte, flatten, premultiply, threshold, or otherwise reinterpret fractional alpha merely to make Sixel accept it.

## All-transparent rasters

All-transparent rasters remain valid.

Kitty preserves the caller's transparent RGBA bytes and intrinsic dimensions.

Sixel preserves the declared raster extent while emitting no opaque color passes; graphics newlines are retained between six-row bands where required by the established D175 contract.

Neither backend substitutes an opaque matte.

## Placement, clipping, and cursor semantics

Version 1.7 deliberately defined the first public raster operation as display at the selected backend's ordinary current-position semantics. A187 retains that contract.

Icod.Terminal therefore does not add a Kitty `C` cursor-movement override merely to imitate one Sixel terminal behavior, and it does not add Kitty `c`/`r` placement sizing controls merely to imitate terminal-cell clipping or fitting.

The library also does not emit extra CSI cursor motion around either backend to manufacture a common post-display cursor position.

Consequently:

- placement begins according to the selected backend's ordinary current-position behavior;
- terminal-side clipping, scrolling, and post-display cursor movement remain backend/terminal semantics;
- successful completion means the reviewed protocol object was emitted successfully, not that both protocols leave the cursor at an identical cell.

This is an intentional semantic boundary, not an implementation gap.

## Geometry

The 1.6 pixel/cell geometry substrate remains available for observation and later features, but A187 does not use geometry to resize or place `DisplayRasterAsync(...)` images.

An additive placement or scaling API may be considered only when a future release can define one coherent backend-neutral meaning and test it across all selected backends.

## Public API

A187 adds no public API.

The public surface remains:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

The XML remarks for `DisplayRasterAsync(...)` now state explicitly that source dimensions are intrinsic and that placement, clipping, and post-display cursor behavior remain backend-native.

## Regression evidence

A187 regression coverage freezes:

- Kitty source `s`/`v` dimensions equal the intrinsic raster dimensions;
- the common Kitty display path emits no `c`, `r`, or `C` placement/cursor override;
- Sixel preserves the same intrinsic raster extent;
- transparent RGBA remains byte-exact through Kitty;
- all-transparent Sixel output preserves extent without painting opaque pixels;
- fractional alpha succeeds through verified Kitty and remains controlled `Unsupported` through verified Sixel;
- the public API fingerprint remains unchanged.

A success-path A186 routing regression was also made deterministic by replacing wall-clock probe delays with a non-advancing monotonic test clock. Production timeout behavior was not changed.

## Deliberately deferred

A187 does not introduce:

- source rectangles;
- destination rectangles;
- scaling or fitting modes;
- persistent Kitty image or placement IDs;
- z-order;
- Unicode placeholders;
- explicit image deletion;
- animation;
- cursor normalization between protocols;
- public backend selection.

## Acceptance

Exact accepted head:

`80d78b74fe780421bf2e667ef434d38775c0dd48`

Staging workflow:

`#1340 / 34427249773`

The exact head passed Runtime Windows, Runtime Linux, Runtime macOS, package candidate/public-API freeze, Package Foundation, Package Presentation, Package Semantic and hardening, Package Stable 1.x release line, and the validated package artifact.

This checkpoint does not authorize merge, tagging, or publication. PR #46 remains draft while A188–A189 are completed.
