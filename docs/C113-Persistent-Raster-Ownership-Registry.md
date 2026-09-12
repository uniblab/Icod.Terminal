# C113 — Persistent Raster Ownership Registry

**Release:** `1.11.0-alpha.1`  
**Tranche:** C113  
**Status:** accepted  
**Accepted head:** `ad48f0307f7e20714e3ad4533a98efd5ad9ec100`  
**Qualification workflow:** `#1556 / 34625127402`

## Accepted contract

C113 introduces the internal, transport-free ownership substrate for persistent raster resources and placements.

The registry is deliberately bounded and session-oriented:

- at most 256 live resources;
- at most 4096 live placements across the session;
- resource image numbers and placement ids are private, nonzero 32-bit identities;
- allocation advances monotonically, wraps from `uint.MaxValue` to `1`, and never collides with a currently live identity;
- multiple placements may belong to one resource;
- resource release closes and unregisters all children;
- resource and placement local release is idempotent;
- generation is stamped into every reserved state object;
- one synchronization boundary protects allocation, counts, ownership, and release;
- registry/state objects retain no `TerminalRasterImage` or pixel byte payload.

C113 performs no terminal I/O and exposes no new public API. Capacity exhaustion is a local reservation failure for C114+ to project to a controlled public result before protocol output begins.

## TDD evidence

The RED checkpoint was commit `64f9e5d71c50796efc3b889666c80677e1dbbf8e`. Runtime compilation failed across the target frameworks because `TerminalPersistentRasterRegistry`, `TerminalPersistentRasterResourceState`, and `TerminalPersistentRasterPlacementState` did not yet exist. The failure was therefore attributable to the missing C113 production substrate rather than test syntax or an unrelated regression.

The GREEN implementation was committed atomically at `ad48f0307f7e20714e3ad4533a98efd5ad9ec100`.

## Qualification

Exact head `ad48f0307f7e20714e3ad4533a98efd5ad9ec100` passed pull-request workflow `#1556 / 34625127402` across:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate / public API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- Validated package artifact.

The provisional public API fingerprint remains unchanged from C112 because C113 is internal-only.

C113 is therefore closed. C114 may build public resource creation and acknowledged upload on this qualified bounded ownership substrate.
