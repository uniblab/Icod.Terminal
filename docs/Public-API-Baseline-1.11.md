# Icod.Terminal Public API Baseline — 1.11.0

**Release:** `1.11.0`  
**Status:** final C119 public API freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the final intentional additive public surface for `Icod.Terminal 1.11.0`. Historical baselines remain unchanged.

Version 1.11 adds exactly one semantic capability value and one opaque persistent-raster ownership domain above the internal Kitty Graphics identifiers used by the implementation.

## Final public additions

The capability vocabulary gains:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

Existing values `0..8` remain unchanged.

The persistent resource surface is:

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

The placement surface is:

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

Construction of resource and placement handles remains library-owned. No public image id, image number, placement id, backend selector, raw APC control dictionary, registry, pixel-coordinate placement, or mutable raster storage is introduced.

`Columns` and `Rows` are independently optional and each supplied value is bounded to `1..16384`. Placement creation and update use the terminal's current cursor location while the reviewed backend uses no-cursor-movement semantics.

## Final machine fingerprint

The deterministic reflection snapshot is identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the final 1.11 fingerprint is:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.11.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the public API snapshot independently for every supported target framework and verifies this exact fingerprint.

## Semantic capability distinction

`PersistentRasterGraphics` is intentionally separate from ordinary `RasterGraphics`.

```text
RasterGraphics
    may be satisfied by verified Sixel or verified Kitty Graphics

PersistentRasterGraphics
    is satisfied only by the reviewed persistent-capable Kitty Graphics path
```

Verified Sixel therefore does not imply persistent terminal-resident resource support. The public API does not expose which concrete backend satisfied a semantic capability.

`TerminalSession.InspectCapability(...)` remains side-effect free. `TerminalSession.VerifyCapabilityAsync(...)` uses the reviewed bounded Kitty support path for `PersistentRasterGraphics`; it does not add broad terminal fingerprinting.

## Ownership and lifecycle meaning

`CreateRasterResourceAsync(...)` requires current verified persistent-raster capability and publishes a public resource only after a correlated terminal acknowledgement establishes terminal-side identity.

A resource may own multiple placements. Placement creation and update retain opaque internal identity and use the existing query/input authority for correlated acknowledgement. A well-formed terminal `ENOENT` for an object believed current invalidates that terminal-resident certainty and produces controlled `Unavailable` semantics.

Persistent identities are session-generation scoped. Explicit invalidation and lifecycle generation changes make existing handles stale. Version 1.11 does not retain arbitrary raster payloads for hidden re-upload and does not automatically replay terminal-resident resources after suspend/resume uncertainty.

Disposal is locally idempotent. Current placements are cleaned before their resource data; stale handles perform local cleanup only and do not emit stale protocol identifiers.

## Bounded compatibility contract

The internal live ownership ceilings are:

```text
256  persistent raster resources per session
4096 persistent raster placements per session
```

These are library bookkeeping bounds, not claims about terminal storage quotas. The terminal may independently evict stored image data.

Version 1.11 remains additive over the stable `1.0.0` compatibility floor. It removes or renumbers no existing public member. C117–C118 hardening, package-only consumption, lifecycle tests, downstream acceptance, and final release closure required no additional public API beyond the C116 freeze recorded by this final baseline.

The permanent ownership contract is `docs/Persistent-Raster-Ownership.md` and the broader compatibility authority remains `docs/Compatibility-and-Versioning.md`.
