# Icod.Terminal Public API Baseline — 1.12.0

**Release:** `1.12.0`  
**Status:** development API freeze after T123  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the reviewed additive public surface planned for `Icod.Terminal 1.12.0` after T122 source rectangles and T123 signed z-order. The historical 1.11 baseline remains unchanged as compatibility evidence.

No further public API additions are planned for T124–T127. Later tranches implement, harden, document, package, and qualify this surface.

## Public additions over 1.11

Version 1.12 adds the backend-neutral immutable source rectangle:

```csharp
public readonly struct TerminalRasterSourceRectangle {
	public TerminalRasterSourceRectangle(
		int x,
		int y,
		int width,
		int height
	);

	public int X { get; }
	public int Y { get; }
	public int Width { get; }
	public int Height { get; }
}
```

`TerminalRasterPlacementOptions` gains:

```csharp
public TerminalRasterSourceRectangle? SourceRectangle { get; set; }
public int? ZIndex { get; set; }
```

`SourceRectangle` uses zero-based source-image pixel coordinates. Its scalar contract requires non-negative `X`/`Y`, positive `Width`/`Height`, and values bounded by `TerminalRasterImage.MaximumDimension`; placement creation/update additionally require the rectangle to fit completely inside the actual uploaded source raster.

`ZIndex` accepts the complete signed 32-bit `int` domain. `null` retains backend/default order.

No new `TerminalCapability` value, public backend selector, Kitty identity, raw command surface, relative placement graph, Unicode placeholder contract, or animation/frame lifecycle is introduced.

## Machine fingerprint

The deterministic reflection snapshot is identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the reviewed 1.12 development fingerprint is:

```text
eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.12.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the public API snapshot independently for every supported target framework and verifies this exact fingerprint.

## Compatibility meaning

The 1.12 surface is additive over the stable `1.0.0` compatibility floor and preserves all existing public signatures and enum numeric values. Existing 1.11 placement behavior remains unchanged when `SourceRectangle` and `ZIndex` are omitted.

T127 must re-run the exact public snapshot and confirm this fingerprint is unchanged before stable release. Any later public API drift requires explicit review rather than silently changing this baseline.
