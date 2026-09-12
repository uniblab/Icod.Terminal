# C112 — Persistent Raster Capability Integration

**Release:** `1.11.0-alpha.1`  
**Tranche:** C112  
**Status:** accepted  
**Accepted head:** `67923d7f3364bc693e52bdedd59e848d0db5672e`  
**Qualification workflow:** `#1553 / 34623710345`

## Accepted contract

C112 adds the public semantic capability `TerminalCapability.PersistentRasterGraphics = 9` without changing the frozen numeric values `0..8`.

Persistent raster capability is intentionally distinct from ordinary raster display capability:

- `RasterGraphics` may be satisfied by verified Sixel or verified Kitty Graphics;
- `PersistentRasterGraphics` is satisfied only by the reviewed persistent-capable Kitty Graphics backend;
- verified Sixel alone never implies persistent-raster support;
- capability inspection remains side-effect free;
- explicit persistent-raster verification reuses the existing bounded Kitty Graphics support probe and does not probe Sixel.

The N156 semantic backend registry contains a Kitty-only candidate for `PersistentRasterGraphics`, and N157 contains the corresponding explicit Kitty-only routing policy. Redirected/noninteractive output remains unavailable without probe traffic.

## API baseline

The provisional 1.11 public API baseline includes exactly the additive capability value introduced by C112. Existing `TerminalCapability` numeric assignments remain unchanged.

The normalized 1.11 public API fingerprint at C112 is:

```text
c037c3088a86c93da6f74b8e1deb3769ff9f43252d2fb871562b0519f333bd18
```

## Qualification

Exact head `67923d7f3364bc693e52bdedd59e848d0db5672e` passed pull-request workflow `#1553 / 34623710345` across:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate / public API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- Validated package artifact.

C112 is therefore closed. C113 may build the bounded session-owned persistent resource/placement registry on this qualified capability boundary.
