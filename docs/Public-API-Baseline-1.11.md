# Icod.Terminal Public API Baseline — 1.11.0

**Release:** `1.11.0`  
**Status:** C115 placement-creation freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional public API additions accumulated for `Icod.Terminal 1.11.0`. Historical baselines remain unchanged.

C112 added exactly one public enum value:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

C114 added the first opaque persistent-resource ownership surface approved by C110:

```csharp
public sealed class TerminalRasterResource : IAsyncDisposable {
    public ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreatePlacementAsync(
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );

    public ValueTask DisposeAsync();
}

public sealed partial class TerminalSession {
    public ValueTask<TerminalControlResult<TerminalRasterResource>> CreateRasterResourceAsync(
        TerminalRasterImage image,
        CancellationToken cancellationToken = default
    );
}
```

C115 adds the approved opaque placement-creation surface:

```csharp
public sealed class TerminalRasterPlacementOptions {
    public int? Columns { get; set; }
    public int? Rows { get; set; }
}

public sealed class TerminalRasterPlacement : IAsyncDisposable {
    public ValueTask DisposeAsync();
}
```

All existing `TerminalCapability` numeric values `0..8` remain unchanged. C115 exposes no Kitty image id, image number, placement id, backend selector, raw APC writer, registry, cursor coordinate, or mutable raster storage. Construction of `TerminalRasterResource` and `TerminalRasterPlacement` remains internal to the owning session.

`TerminalRasterPlacementOptions.Columns` and `.Rows` are independently optional and are validated when a placement operation begins. Each supplied value is bounded to `1..16384`. Placement occurs at the terminal's current cursor position and the reviewed Kitty path emits `C=1`, so placement creation does not move the text cursor.

The approved placement-update and deterministic terminal-delete surface is intentionally not present in this C115 snapshot; C116 will add it under its own RED/GREEN gate and will require another intentional 1.11 fingerprint update.

## Machine fingerprint

The deterministic reflection snapshot is required to remain identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the C115 fingerprint is:

```text
e88c867e252c4acc24e3c718ddbc75ac52e167537c7f1495e8aec37649a372f3
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

`TerminalRasterResource.CreatePlacementAsync(...)` performs no backend selection and exposes no protocol identity. It reserves one bounded session-owned placement, emits one serialized Kitty placement frame at the current cursor, rolls the reservation back on failure, and returns only an opaque placement handle.

## Compatibility rule

C115 remains additive over the stable 1.0 compatibility floor. The enum member remains appended at numeric value `9`; no existing public type/member is removed or renumbered.

Later 1.11 tranches may add only the separately approved placement-update/disposal surface. Each intentional addition must replace this interim 1.11 fingerprint with a newly reviewed fingerprint rather than mutating a historical baseline silently.
