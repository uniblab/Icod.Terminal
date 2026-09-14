# Icod.Terminal.PersistentRaster.Sample

This sample demonstrates the backend-neutral persistent-raster ownership model in `Icod.Terminal 1.14.0`. It combines the opaque resource/placement ownership introduced in 1.11, the source-pixel cropping and signed z-order added in 1.12, immutable-parent relative placement ownership from 1.13, and the side-effect-free lifecycle observability added in 1.14.

Run the sample with, for example:

```text
dotnet run --project samples/Icod.Terminal.PersistentRaster.Sample/Icod.Terminal.PersistentRaster.Sample.csproj -f net10.0
```

The executable flow is:

```text
verify PersistentRasterGraphics
    -> create Resource A                              Current / None
    -> create an ordinary placement from Resource A Current / None
    -> create independently owned Resource B         Current / None
    -> create a Resource B placement relative to A  Current / None
    -> UpdateAsync common crop/extents/z-order while preserving parent + offsets
    -> UpdateRelativeAsync signed cell offsets + common geometry while preserving parent
    -> dispose the parent placement                  Disposed / ExplicitDisposal
    -> descendant placement is removed by cascade   Released / AncestorReleased
    -> Resource B remains independently owned        Current / None
    -> explicitly dispose the released child wrapper Disposed / ExplicitDisposal
    -> create a fresh ordinary placement from B      Current / None
    -> dispose remaining placements/resources deterministically
```

`OwnershipState` is a synchronous, bounded snapshot of **Icod.Terminal's local ownership certainty**. Reading it emits no terminal traffic and does not verify remote existence. In particular, `Current` means that the handle remains current under the library's present knowledge; it is not authentication and is not proof that terminal-side storage could not have been independently evicted since the last correlated operation.

The four public states are:

- `Current` — local ownership remains current and no evidence has invalidated the required terminal identity;
- `Stale` — terminal-resident certainty has been lost, for example through session-state invalidation or correlated missing-resource/parent evidence;
- `Released` — a placement wrapper is still reachable but its local placement lifetime ended because another owner released the relevant resource or ancestor relationship;
- `Disposed` — that public wrapper itself was explicitly disposed by its caller.

The associated `TerminalRasterOwnershipLossReason` explains the semantic reason without exposing protocol-private ids or raw backend error text. Status and reason are returned together in one immutable `TerminalRasterOwnershipState` snapshot.

The coordinate systems remain intentionally distinct:

- `TerminalRasterSourceRectangle` is measured in source-image pixels;
- `Columns` and `Rows` describe terminal-cell extents;
- relative `columnOffset` and `rowOffset` values are signed terminal-cell offsets from the immutable parent placement;
- `ZIndex` is a signed stacking value rather than a coordinate.

Resource ownership and placement-parent lifetime remain separate axes. Disposing a parent placement removes its relative-placement subtree deepest-first, but that cascade does not by itself dispose raster resources used by descendant placements. Version 1.14 makes the distinction directly observable: the sample sees the explicitly disposed parent wrapper as `Disposed / ExplicitDisposal`, the still-reachable relative child wrapper as `Released / AncestorReleased`, and Resource B as `Current / None`. It then creates a new ordinary placement from Resource B.

`UpdateAsync(...)` preserves the placement's established positioning mode. For a relative placement, it retains the immutable parent and last acknowledged relative offsets while replacing common crop/extents/z-order geometry. `UpdateRelativeAsync(...)` may replace the signed offsets and common geometry, but it does not reparent the placement.

Parentage is immutable and the portable maximum relative-placement depth is 8. The sample uses only a shallow parent/child graph because the depth boundary is a validation contract rather than an application pattern to imitate.

The sample explicitly verifies `TerminalCapability.PersistentRasterGraphics` before creating terminal-resident resources. If that semantic capability is not currently usable, or if a later resource/placement operation cannot be completed, the sample reports the semantic failure and exits nonzero instead of selecting a terminal brand or graphics backend itself.

The sample deliberately does **not** demonstrate or expose:

- passive remote `ExistsAsync()` or terminal-authenticated existence;
- terminal-brand detection or backend selection;
- raw Kitty/Sixel commands or protocol-private image/placement/generation identities;
- reparenting;
- absolute screen-coordinate layout or scene-graph ownership;
- pixel-within-cell placement;
- Unicode placeholder placements;
- animation/frame lifecycle;
- hidden source-image replay or automatic re-upload after generation invalidation.

Persistent resources and placements remain session-generation scoped. `await using` / `DisposeAsync()` provides deterministic cleanup while identity remains current; loss of terminal certainty publishes stale ownership rather than reviving or replaying it.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the complete sample catalog.
