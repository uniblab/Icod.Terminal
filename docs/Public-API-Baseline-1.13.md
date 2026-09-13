# Icod.Terminal Public API Baseline — 1.13.0

**Release:** `1.13.0`  
**Status:** final additive stable baseline  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the final reviewed additive public surface for `Icod.Terminal 1.13.0` relative persistent-raster placement ownership. The historical 1.12 baseline remains unchanged as compatibility evidence.

The 1.13 implementation, graph hardening, cleanup, protocol-error classification, samples, fresh-package qualification, and downstream stable-1.x qualification have completed without requiring any public surface beyond the T132 candidate.

## Public additions over 1.12

`TerminalRasterResource` gains explicit relative-placement creation:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreateRelativePlacementAsync(
	TerminalRasterPlacement parent,
	int columnOffset,
	int rowOffset,
	TerminalRasterPlacementOptions? options = null,
	CancellationToken cancellationToken = default
);
```

`TerminalRasterPlacement` gains explicit relative-offset replacement:

```csharp
public ValueTask<TerminalControlMutationResult> UpdateRelativeAsync(
	int columnOffset,
	int rowOffset,
	TerminalRasterPlacementOptions? options = null,
	CancellationToken cancellationToken = default
);
```

The final semantic contract is:

- parentage is selected during relative creation and is immutable for the complete placement lifetime;
- `columnOffset` and `rowOffset` are signed terminal-cell offsets;
- the portable relative-depth ceiling is 8;
- resource ownership and parent-placement subtree lifetime remain separate ownership axes;
- `TerminalRasterPlacementOptions` remains the single common crop/extents/z-order contract;
- `CreatePlacementAsync(...)` remains the current-cursor creation API;
- `UpdateAsync(...)` preserves the placement's established positioning mode;
- `UpdateRelativeAsync(...)` can change offsets and common geometry but cannot change parentage;
- no public parent property, reparenting operation, terminal image id, placement id, backend selector, or raw protocol field is added.

## Machine fingerprint

The deterministic reflection snapshot is identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the final 1.13 fingerprint is:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.13.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the public API snapshot independently for every supported target framework and verifies this exact fingerprint.

The original T132 drift witness was workflow `#1682 / 34732156431`: all three generated framework snapshots agreed on this hash while the package candidate deliberately rejected the then-configured historical 1.12 fingerprint.

The complete T138 sample/package/XML/downstream qualification retained the same hash on exact head `86bb11e7ff31dd03d5228fe1049f39c3d6cb7d28` in workflow `#1702 / 34766762349`.

## Compatibility meaning

The 1.13 surface is additive over the stable `1.0.0` compatibility floor and published 1.12 surface. Existing public signatures and enum numeric values remain unchanged.

When relative APIs are not used, 1.12 current-cursor placement, source cropping, signed z-order, lifecycle, capacity, and package dependency behavior remain unchanged.

Historical public API baseline documents and fingerprints remain immutable. Any future public surface change belongs to a later version and must receive its own compatibility review and baseline.
