# Icod.Terminal 1.14 Public API Baseline

This document records the final additive public API for the `Icod.Terminal 1.14.0` persistent-raster lifecycle-observability release.

The stable compatibility floor remains `1.0.0`. The complete 1.13 public surface remains available and unchanged. The predecessor 1.13 public API fingerprint is:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

The deterministic 1.14 public API snapshot is identical across `net8.0`, `net9.0`, and `net10.0`. After LF normalization, its SHA-256 fingerprint is:

```text
2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
```

The machine-readable fingerprint is stored in `docs/Public-API-Baseline-1.14.sha256` and is verified by `packaging/VerifyPublicApiBaseline.ps1`.

## Public additions over 1.13

Version 1.14 adds one backend-neutral ownership-observation vocabulary:

```csharp
public enum TerminalRasterOwnershipStatus {
    Current,
    Stale,
    Released,
    Disposed
}

public enum TerminalRasterOwnershipLossReason {
    None,
    SessionStateLost,
    ResourceMissing,
    ParentPlacementLost,
    AncestorReleased,
    ResourceReleased,
    ExplicitDisposal
}

public readonly record struct TerminalRasterOwnershipState(
    TerminalRasterOwnershipStatus Status,
    TerminalRasterOwnershipLossReason LossReason
);
```

`TerminalRasterResource` gains:

```csharp
public TerminalRasterOwnershipState OwnershipState { get; }
```

`TerminalRasterPlacement` gains:

```csharp
public TerminalRasterOwnershipState OwnershipState { get; }
```

No other public API is required by the 1.14 design.

## Semantic contract

`OwnershipState` is synchronous, bounded, and side-effect free. Reading it does not write terminal bytes, register a query, acquire the output gate, verify a capability, trigger cleanup, replay raster data, or select a graphics backend.

`Current` reports Icod.Terminal's current local ownership certainty. It is not authentication and is not authoritative proof that terminal-side storage or placement still exists at the instant of observation.

`Stale` means terminal-resident certainty has been lost. `Released` means a placement's local lifetime ended because another owner released its placement relationship. `Disposed` means the public wrapper itself was explicitly disposed by its caller.

Status and reason are returned in one immutable snapshot so callers do not observe a torn status/reason pair.

## Identity boundary

The 1.14 public additions expose no:

- Kitty image id;
- Kitty image number;
- Kitty placement id;
- parent protocol identity;
- session generation number;
- backend selector or raw graphics command surface.

The existing opaque persistent-raster identity boundary remains intact.

## Compatibility

Existing resource creation, ordinary and relative placement creation, placement update, cleanup, acknowledgement correlation, capacity limits, relative depth, and wire encoding remain unchanged when lifecycle state is not inspected.

Version 1.14 does not add terminal-side existence probes, replay/re-upload, hidden source-image caching, reparenting, Unicode placeholder placement, animation, absolute layout, pixel-within-cell positioning, scene/window/cell ownership, image decoding/transcoding, or PTY/ConPTY hosting.
