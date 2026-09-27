# Icod.Terminal.RasterPlaceholder.Sample

This sample demonstrates the backend-neutral Unicode raster-placeholder model using semantic screen planning and output transactions. The caller chooses coordinates; Terminal selects the advertised cursor route, including the bounded alternatives added in 1.19.

Run it with, for example:

```text
dotnet run --project samples/Icod.Terminal.RasterPlaceholder.Sample/Icod.Terminal.RasterPlaceholder.Sample.csproj -f net10.0
```

The sample keeps the responsibility boundary explicit:

```text
application / higher-level renderer
    owns cursor position, clipping, redraw order, and layout

Icod.Terminal
    owns virtual-placeholder lifetime, semantic cell tokens,
    protocol-private identity, encoding, acknowledgement, and cleanup
```

The executable flow:

1. verifies `PersistentRasterGraphics` so a terminal-resident raster resource can be created;
2. inspects `UnicodeRasterPlaceholders` without inventing a probe;
3. creates one opaque `TerminalRasterResource`;
4. creates one `TerminalRasterPlaceholder` with a 4x2 semantic cell grid;
5. confirms that successful creation publishes usable live placeholder evidence;
6. generates semantic cell tokens with `GetCell(...)`;
7. uses preflighted `PlanCursorMove(null, target)` plans for caller-selected screen positions, without assuming a known current cursor;
8. adds each row's cursor plan and typed bulk placeholder cells to one screen transaction and commits the complete grid;
9. commits a fresh transaction containing a cursor plan and one sparse cell redraw;
10. commits another cursor-and-cell transaction with three semantic cells in a deliberately non-raster order;
11. creates a physical placement relative to the virtual placeholder;
12. releases placement, placeholder, and resource ownership deterministically through `await using`.

Every placeholder cell is self-contained. The sample does not depend on the previously emitted cell, hidden cursor history, or raster-order emission.

All required cursor plans are checked before creating raster resources. A missing route produces a diagnostic rather than requiring one particular underlying capability. A plan can be reused within its owning session, but every transaction is newly created immediately before building that output group. Cursor movement and its associated cells cannot be interleaved by other coordinated session output during commitment. Creation/acknowledgement of raster resources and placements remains a separate operation.

The sample draws at fixed caller-selected positions (the grid begins at zero-based row 4, column 8). It needs an interactive terminal with usable persistent raster/placeholder support and sufficient space; it does not implement clipping or resize-driven layout. Resource cleanup is deterministic through `await using`, but a committed failure may leave partial output and is not automatically replayed. The sample imports Terminal only; TermInfo expansion and raw terminal-string output are intentionally absent.

The sample intentionally contains no terminal-brand branch, graphics-backend selection, public numeric raster identity, raw graphics control frame, placeholder codepoint literal, or copied combining-mark table. Those details remain private implementation concerns of `Icod.Terminal`.

`TerminalRasterPlaceholder` is not a window or screen-coordinate object. Its row/column values identify cells within the virtual raster only. The caller remains responsible for deciding where those cells appear on the terminal screen.

A successful `CreatePlaceholderAsync(...)` acknowledgement is authoritative for that creation transaction and provides live semantic evidence. Before creation, `InspectCapability(TerminalCapability.UnicodeRasterPlaceholders)` may remain unknown because 1.15 does not fabricate a placeholder-specific verification query.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
