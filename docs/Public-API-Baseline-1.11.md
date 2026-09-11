# Icod.Terminal Public API Baseline — 1.11.0

**Release:** `1.11.0`  
**Status:** C114 persistent-resource creation freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional public API additions accumulated for `Icod.Terminal 1.11.0`. Historical baselines remain unchanged.

C112 added exactly one public enum value:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

C114 adds the first opaque persistent-resource ownership surface approved by C110:

```csharp
public sealed class TerminalRasterResource : IAsyncDisposable {
    public ValueTask DisposeAsync();
}

public sealed partial class TerminalSession {
    public ValueTask<TerminalControlResult<TerminalRasterResource>> CreateRasterResourceAsync(
        TerminalRasterImage image,
        CancellationToken cancellationToken = default
    );
}
```

All existing `TerminalCapability` numeric values `0..8` remain unchanged. C114 exposes no Kitty image id, image number, placement id, backend selector, raw APC writer, registry, or mutable raster storage. Construction of `TerminalRasterResource` remains internal to the session.

The approved placement surface is intentionally not present in this C114 snapshot; C115/C116 will add it under their own RED/GREEN gates and will require another intentional 1.11 fingerprint update.

## Machine fingerprint

The deterministic reflection snapshot is required to remain identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the C114 fingerprint is:

```text
fbef4700d29613be7f6224a5349426c731412a851127bd529fc190ac3a562aeb
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.11.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the snapshot independently for every supported target framework and verifies this fingerprint.

## Semantic meaning

`PersistentRasterGraphics` remains intentionally distinct from ordinary `RasterGraphics`.

```text
RasterGraphics
    may be satisfied by verified Sixel or verified Kitty Graphics

PersistentRasterGraphics
    may be satisfied only by the reviewed persistent-capable Kitty Graphics path
```

Verified Sixel therefore does not imply persistent-resource support. Verified Kitty Graphics may satisfy both semantic capabilities.

`TerminalSession.InspectCapability(...)` remains side-effect free. `TerminalSession.VerifyCapabilityAsync(...)` verifies `PersistentRasterGraphics` only through the existing bounded Kitty Graphics support probe and does not probe Sixel for that semantic capability.

`CreateRasterResourceAsync(...)` does not perform hidden capability probing. It requires current verified persistent-raster capability, reserves bounded session ownership before output, emits an acknowledged direct Kitty upload through the existing query authority, and publishes an opaque resource only after a correlated successful acknowledgement.

## Compatibility rule

C114 remains additive over the stable 1.0 compatibility floor. The enum member remains appended at numeric value `9`; no existing public type/member is removed or renumbered.

Later 1.11 tranches may add only the separately approved placement/update surface. Each intentional addition must replace this interim 1.11 fingerprint with a newly reviewed fingerprint rather than mutating a historical baseline silently.
