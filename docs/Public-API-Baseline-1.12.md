# Icod.Terminal Public API Baseline — 1.12.0

**Release:** `1.12.0`  
**Status:** final stable API freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the reviewed additive public surface for `Icod.Terminal 1.12.0`. The historical 1.11 baseline remains unchanged as compatibility evidence.

No public API was added after the T123 freeze. T124–T127 implemented, hardened, documented, packaged, and qualified this exact surface.

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

After normalizing line endings to LF, the final reviewed 1.12 fingerprint is:

```text
eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.12.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the public API snapshot independently for every supported target framework and verifies this exact fingerprint.

## Compatibility meaning

The 1.12 surface is additive over the stable `1.0.0` compatibility floor and preserves all existing public signatures and enum numeric values. Existing 1.11 placement behavior remains unchanged when `SourceRectangle` and `ZIndex` are omitted.

Any later public API drift requires a separately reviewed later-release baseline rather than modifying this historical 1.12 record.
