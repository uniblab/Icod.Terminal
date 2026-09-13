# Icod.Terminal.PersistentRaster.Sample

This sample demonstrates the backend-neutral persistent-raster ownership model in `Icod.Terminal 1.13.0`. It combines the opaque resource/placement ownership introduced in 1.11, the source-pixel cropping and signed z-order added in 1.12, and the immutable-parent relative placement ownership added in 1.13.

Run the sample with, for example:

```text
dotnet run --project samples/Icod.Terminal.PersistentRaster.Sample/Icod.Terminal.PersistentRaster.Sample.csproj -f net10.0
```

The executable flow is:

```text
verify PersistentRasterGraphics
    -> create Resource A
    -> create an ordinary current-cursor placement from Resource A
    -> create independently owned Resource B
    -> create a Resource B placement relative to Resource A's placement
    -> UpdateAsync common crop/extents/z-order while preserving parent + offsets
    -> UpdateRelativeAsync signed cell offsets + common geometry while preserving parent
    -> dispose the parent placement
    -> descendant placement is removed by the parent cascade
    -> Resource B remains independently owned
    -> create a fresh ordinary placement from Resource B
    -> dispose remaining placements/resources deterministically
```

The coordinate systems are intentionally distinct:

- `TerminalRasterSourceRectangle` is measured in source-image pixels;
- `Columns` and `Rows` describe terminal-cell extents;
- relative `columnOffset` and `rowOffset` values are signed terminal-cell offsets from the immutable parent placement;
- `ZIndex` is a signed stacking value rather than a coordinate.

Resource ownership and placement-parent lifetime are separate axes. Disposing a parent placement removes its relative-placement subtree deepest-first, but that cascade does not by itself dispose raster resources used by descendant placements. The sample proves this by creating a new ordinary placement from Resource B after its relative child placement has been removed by the parent cascade.

`UpdateAsync(...)` preserves the placement's established positioning mode. For a relative placement, it retains the immutable parent and last acknowledged relative offsets while replacing common crop/extents/z-order geometry. `UpdateRelativeAsync(...)` may replace the signed offsets and common geometry, but it does not reparent the placement.

Parentage is immutable and the portable maximum relative-placement depth is 8. The sample uses only a shallow parent/child graph because the depth boundary is a validation contract rather than an application pattern to imitate.

The sample explicitly verifies `TerminalCapability.PersistentRasterGraphics` before creating terminal-resident resources. If that semantic capability is not currently usable, or if a later resource/placement operation cannot be completed, the sample reports the semantic failure and exits nonzero instead of selecting a terminal brand or graphics backend itself.

The sample deliberately does **not** demonstrate or expose:

- terminal-brand detection or backend selection;
- raw Kitty/Sixel commands or protocol-private image/placement identities;
- reparenting;
- absolute screen-coordinate layout or scene-graph ownership;
- pixel-within-cell placement;
- Unicode placeholder placements;
- animation/frame lifecycle;
- hidden source-image replay or automatic re-upload after generation invalidation.

Persistent resources and placements remain session-generation scoped. `await using` / `DisposeAsync()` provides deterministic cleanup while identity remains current; loss of terminal certainty invalidates stale ownership rather than reviving or replaying it.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the complete sample catalog.
