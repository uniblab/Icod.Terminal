# C115 — Persistent Raster Placement Creation

**Release:** `1.11.0-alpha.1`  
**Tranche:** C115  
**Status:** accepted  
**Accepted head:** `40fae141010bf6e27a7516776af03d886c2537fb`  
**Qualification workflow:** `#1565 / 34665854426`

## Accepted contract

C115 adds opaque placement creation and multi-placement ownership above one acknowledged persistent raster resource.

The accepted surface provides:

- public `TerminalRasterPlacementOptions` with nullable `Columns` and `Rows`;
- public opaque `TerminalRasterPlacement : IAsyncDisposable`;
- `TerminalRasterResource.CreatePlacementAsync(...)` returning `TerminalControlResult<TerminalRasterPlacement>`;
- current-cursor placement with Kitty `C=1`, so the graphics operation does not move the text cursor;
- independently optional `Columns` and `Rows`, each bounded to `1..16384`;
- multiple placements per resource with distinct private nonzero placement identities;
- pre-cancellation and option validation before placement output;
- disposed parent resources rejecting new placement creation with `ObjectDisposedException`;
- registry exhaustion or loss of local ownership projected as controlled `Unavailable` before a public placement is returned;
- no public image id, image number, placement id, backend selector, X/Y coordinates, or raw Kitty control builder.

C115 deliberately leaves placement update/reposition and terminal-side placement/resource deletion to C116. Placement disposal remains local-only at this checkpoint.

## TDD evidence

The RED checkpoint was commit `f2ff800693b815f584eea951794135ee8150ad3e`. Runtime compilation failed across `net8.0`, `net9.0`, and `net10.0` because `TerminalRasterPlacementOptions`, `TerminalRasterPlacement`, and `TerminalRasterResource.CreatePlacementAsync(...)` did not yet exist. The RED build reported zero warnings.

The GREEN implementation was committed at `471b8444ba1452c5d4f4340f72d77f7cee7262aa`. The approved additive public surface changed the deterministic 1.11 API fingerprint to:

```text
e88c867e252c4acc24e3c718ddbc75ac52e167537c7f1495e8aec37649a372f3
```

The fingerprint/documentation adjustment was committed at `40fae141010bf6e27a7516776af03d886c2537fb`.

## Qualification

Exact head `40fae141010bf6e27a7516776af03d886c2537fb` passed pull-request workflow `#1565 / 34665854426` across:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate / public API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- Validated package artifact.

C115 is therefore closed. C116 may add semantic placement replacement plus deterministic terminal-side placement/resource cleanup on this qualified ownership surface.
