# Icod.Terminal Public API Baseline — 1.11.0

**Release:** `1.11.0`  
**Status:** C116 placement-update and deterministic-cleanup freeze  
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

C115 added the approved opaque placement-creation surface, and C116 adds the approved semantic placement-replacement method:

```csharp
public sealed class TerminalRasterPlacementOptions {
    public int? Columns { get; set; }
    public int? Rows { get; set; }
}

public sealed class TerminalRasterPlacement : IAsyncDisposable {
    public ValueTask<TerminalControlMutationResult> UpdateAsync(
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );

    public ValueTask DisposeAsync();
}
```

All existing `TerminalCapability` numeric values `0..8` remain unchanged. C116 exposes no Kitty image id, image number, placement id, backend selector, raw APC writer, registry, cursor coordinate, or mutable raster storage. Construction of `TerminalRasterResource` and `TerminalRasterPlacement` remains internal to the owning session.

`TerminalRasterPlacementOptions.Columns` and `.Rows` are independently optional and are validated when a placement operation begins. Each supplied value is bounded to `1..16384`. Placement creation and replacement occur at the terminal's current cursor position and the reviewed Kitty path emits `C=1`, so these graphics operations do not move the text cursor.

`TerminalRasterPlacement.UpdateAsync(...)` reuses the placement's private resource/placement identity pair and returns the existing `TerminalControlMutationResult` vocabulary. Disposal remains represented solely by `IAsyncDisposable`; C116 changes its implementation to deterministic one-shot terminal cleanup without adding another public cleanup API.

## Machine fingerprint

The deterministic reflection snapshot is required to remain identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the C116 fingerprint is:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
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

`TerminalRasterPlacement.UpdateAsync(...)` serializes replacement through the same session output gate and reuses the same private image/placement identity pair. Placement disposal removes local ownership exactly once and attempts one quiet targeted placement delete while current. Resource disposal closes child ownership first, attempts quiet child deletes, then attempts the hard image-data delete; cleanup failures do not restore local protocol ownership for retry.

## Compatibility rule

C116 remains additive over the stable 1.0 compatibility floor. The enum member remains appended at numeric value `9`; no existing public type/member is removed or renumbered.

C117 and later 1.11 tranches must not expand this public surface except through a separately reviewed API-regret decision. Lifecycle invalidation, teardown ordering, and hardening should remain behavioral/internal additions against this frozen C116 public ownership contract.
