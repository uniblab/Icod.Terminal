# Icod.Terminal Public API Baseline — 1.11.0

**Release:** `1.11.0`  
**Status:** C112 additive capability freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the first intentional public API addition for `Icod.Terminal 1.11.0`. Historical baselines remain unchanged.

C112 adds exactly one public enum value:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

All existing `TerminalCapability` numeric values `0..8` remain unchanged. No public Kitty image id, image number, placement id, backend selector, raw APC writer, persistent-resource handle, or placement handle is introduced by C112.

## Machine fingerprint

The deterministic reflection snapshot is required to remain identical across `net8.0`, `net9.0`, and `net10.0`.

After normalizing line endings to LF, the C112 fingerprint is:

```text
c037c3088a86c93da6f74b8e1deb3769ff9f43252d2fb871562b0519f333bd18
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.11.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the snapshot independently for every supported target framework and verifies this fingerprint.

## Semantic meaning

`PersistentRasterGraphics` is intentionally distinct from ordinary `RasterGraphics`.

```text
RasterGraphics
    may be satisfied by verified Sixel or verified Kitty Graphics

PersistentRasterGraphics
    may be satisfied only by the reviewed persistent-capable Kitty Graphics path
```

Verified Sixel therefore does not imply persistent-resource support. Verified Kitty Graphics may satisfy both semantic capabilities.

`TerminalSession.InspectCapability(...)` remains side-effect free. `TerminalSession.VerifyCapabilityAsync(...)` verifies `PersistentRasterGraphics` only through the existing bounded Kitty Graphics support probe and does not probe Sixel for that semantic capability.

## Compatibility rule

C112 is additive over the stable 1.0 compatibility floor. The new enum member is appended at numeric value `9`; no existing public type/member is removed or renumbered.

Later 1.11 tranches may add the separately approved persistent-resource and placement object surface. Any such addition must intentionally replace this interim 1.11 fingerprint with a newly reviewed 1.11 fingerprint rather than mutating a historical baseline silently.
