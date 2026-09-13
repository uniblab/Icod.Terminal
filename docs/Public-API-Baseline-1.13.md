# Icod.Terminal Public API Baseline — 1.13.0

**Release:** `1.13.0`  
**Status:** T132 additive API freeze candidate  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the reviewed additive public surface candidate for `Icod.Terminal 1.13.0` relative persistent-raster placement ownership. The historical 1.12 baseline remains unchanged as compatibility evidence.

T132 freezes only the public semantic shape. Relative Kitty transport integration, graph hardening, cleanup, protocol-error classification, samples, package qualification, and stable release closure remain later 1.13 tranches.

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

The semantic contract frozen by T132 is:

- parentage is selected during relative creation and is immutable for the complete placement lifetime;
- `columnOffset` and `rowOffset` are signed terminal-cell offsets;
- `TerminalRasterPlacementOptions` remains the single common crop/extents/z-order contract;
- `CreatePlacementAsync(...)` remains the current-cursor creation API;
- `UpdateAsync(...)` preserves the placement's established positioning mode;
- `UpdateRelativeAsync(...)` can change offsets and common geometry but cannot change parentage;
- no public parent property, reparenting operation, terminal image id, placement id, backend selector, or raw protocol field is added.

T132 intentionally fails closed for otherwise-valid relative transport operations until T134 integrates the reviewed private Kitty parent/offset fields. This development-tranche behavior emits no relative placement output and does not create local handles for terminal state that was never committed.

## Machine fingerprint

The deterministic reflection snapshot produced by the T132 API candidate is identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the reviewed T132 candidate fingerprint is:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.13.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the public API snapshot independently for every supported target framework and verifies this exact candidate fingerprint.

The T132 drift witness was workflow `#1682 / 34732156431`: all three generated framework snapshots agreed on this hash while the package candidate deliberately rejected the still-configured historical 1.12 fingerprint.

## Compatibility meaning

The 1.13 candidate surface is additive over the stable `1.0.0` compatibility floor and published 1.12 surface. Existing public signatures and enum numeric values remain unchanged.

When relative APIs are not used, 1.12 current-cursor placement, source cropping, signed z-order, lifecycle, capacity, and package dependency behavior remain unchanged.

Any later 1.13 public API drift requires explicit regret-gate review and a deliberate replacement of this candidate fingerprint before stable release closure. The historical 1.12 baseline must not be modified.
