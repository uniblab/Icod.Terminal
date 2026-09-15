# Icod.Terminal.RasterPlaceholder.Sample

This sample demonstrates the backend-neutral Unicode raster-placeholder model introduced in `Icod.Terminal 1.15.0`.

Run it with, for example:

```text
dotnet run --project samples/Icod.Terminal.RasterPlaceholder.Sample/Icod.Terminal.RasterPlaceholder.Sample.csproj -f net10.0
```

The sample keeps the 1.15 responsibility boundary explicit:

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
7. uses ordinary TermInfo `CursorAddress` expansion plus `WriteTerminalStringAsync(...)` for caller-owned cursor movement;
8. writes the complete grid with typed bulk placeholder output;
9. moves back to one grid location and redraws one cell sparsely;
10. emits three semantic cells in a deliberately non-raster order;
11. creates a physical placement relative to the virtual placeholder;
12. releases placement, placeholder, and resource ownership deterministically through `await using`.

Every placeholder cell is self-contained. The sample does not depend on the previously emitted cell, hidden cursor history, or raster-order emission.

The sample intentionally contains no terminal-brand branch, graphics-backend selection, public numeric raster identity, raw graphics control frame, placeholder codepoint literal, or copied combining-mark table. Those details remain private implementation concerns of `Icod.Terminal`.

`TerminalRasterPlaceholder` is not a window or screen-coordinate object. Its row/column values identify cells within the virtual raster only. The caller remains responsible for deciding where those cells appear on the terminal screen.

A successful `CreatePlaceholderAsync(...)` acknowledgement is authoritative for that creation transaction and provides live semantic evidence. Before creation, `InspectCapability(TerminalCapability.UnicodeRasterPlaceholders)` may remain unknown because 1.15 does not fabricate a placeholder-specific verification query.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
