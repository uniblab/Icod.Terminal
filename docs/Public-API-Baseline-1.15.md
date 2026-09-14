# Icod.Terminal 1.15 Public API Baseline

This document records the additive public API selected for the `Icod.Terminal 1.15.0` Unicode Placeholder and Virtual Raster Placement release.

The stable compatibility floor remains `1.0.0`. The complete 1.14 public surface remains available and unchanged. The predecessor 1.14 public API fingerprint is:

```text
2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
```

T150 generated identical public API snapshots on `net8.0`, `net9.0`, and `net10.0`. The frozen 1.15 fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```

The machine-readable fingerprint is stored separately in `docs/Public-API-Baseline-1.15.sha256` and is enforced by the package/public-API gate for the remainder of 1.15 development.

## Public additions over 1.14

### Semantic capability

`TerminalCapability` adds exactly one value while preserving all previously released numeric values:

```csharp
TerminalCapability.UnicodeRasterPlaceholders = 10
```

This capability represents virtual persistent-raster placement plus Unicode-placeholder cell rendering. It is distinct from both ordinary `RasterGraphics` and `PersistentRasterGraphics`.

### Placeholder options

```csharp
public sealed class TerminalRasterPlaceholderOptions {
	public int Columns { get; init; }
	public int Rows { get; init; }
}
```

Both dimensions are required by the semantic contract and are valid only in `1..256`.

### Opaque placeholder handle

```csharp
public sealed class TerminalRasterPlaceholder : IAsyncDisposable {
	public int Columns { get; }
	public int Rows { get; }
	public TerminalRasterOwnershipState OwnershipState { get; }

	public TerminalRasterPlaceholderCell GetCell(
		int row,
		int column
	);

	public ValueTask DisposeAsync();
}
```

The type has no public constructor. Instances are created only through an owning `TerminalRasterResource` after an acknowledged virtual-placement transaction succeeds.

### Placeholder cell token

```csharp
public readonly struct TerminalRasterPlaceholderCell {
	public int Row { get; }
	public int Column { get; }
}
```

The type has no public constructor. A token is created only by `TerminalRasterPlaceholder.GetCell(...)` so its private association with one placeholder remains opaque.

### Placeholder creation

`TerminalRasterResource` adds:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlaceholder>>
	CreatePlaceholderAsync(
		TerminalRasterPlaceholderOptions options,
		CancellationToken cancellationToken = default
	);
```

### Placeholder as relative-placement parent

`TerminalRasterResource` adds a distinctly named virtual-parent operation:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlacement>>
	CreateRelativePlacementFromPlaceholderAsync(
		TerminalRasterPlaceholder parent,
		int columnOffset,
		int rowOffset,
		TerminalRasterPlacementOptions? options = null,
		CancellationToken cancellationToken = default
	);
```

The virtual placeholder itself is not relative. This method allows an ordinary physical raster placement to use a current virtual placeholder as its immutable relative parent.

The existing `CreateRelativePlacementAsync(TerminalRasterPlacement, ...)` method is deliberately **not overloaded** with another reference-type parent. This preserves source compatibility for existing calls such as `CreateRelativePlacementAsync(null!, ...)`, which would otherwise become ambiguous at compile time.

### Typed current-cursor cell emission

`TerminalSession` adds:

```csharp
public ValueTask WriteRasterPlaceholderCellAsync(
	TerminalRasterPlaceholderCell cell,
	CancellationToken cancellationToken = default
);

public ValueTask WriteRasterPlaceholderCellsAsync(
	ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
	CancellationToken cancellationToken = default
);
```

These methods render semantic placeholder tokens at the caller-controlled current text cursor position. They do not own absolute screen coordinates, clipping, scrolling, window layout, or damage tracking.

## Semantic contract

Placeholder dimensions are bounded to `1..256` in each axis.

Every `TerminalRasterPlaceholderCell` is independently renderable. The implementation may not require identity or row/column inheritance from a previously emitted cell.

Placeholder ownership reuses `TerminalRasterOwnershipState`; no second lifecycle enum family is introduced.

A current placeholder reports `Current / None`. Session-state loss, resource-missing evidence, resource release, and explicit wrapper disposal reuse the established 1.14 lifecycle vocabulary.

Virtual placements count against the existing combined persistent placement ceiling rather than establishing a second unbounded registry.

## Identity and encoding boundary

The 1.15 public additions expose no:

- Kitty image id;
- Kitty image number;
- Kitty placement id or virtual-placement id;
- session generation number;
- `U+10EEEE` constant/helper;
- row/column combining-diacritic table;
- SGR identity-color packing helper;
- APC command dictionary or raw Kitty command builder;
- public backend selector.

The public token vocabulary is semantic: placeholder handle, row, column, ownership state, and current-cursor typed emission.

## Compatibility and non-goals

Version 1.15 remains additive over the stable `1.0.0` compatibility floor and complete 1.14 surface.

Existing persistent resources, ordinary placements, relative placements, crop/z-order geometry, lifecycle observation, query routing, cleanup, capacity ceilings, and no-replay semantics remain unchanged when the new placeholder APIs are unused.

Version 1.15 does not add animation/frame lifecycle, absolute raster positioning, pixel-within-cell positioning, scene/window/cell ownership, automatic redraw, hidden source-raster replay, Sixel placeholder emulation, image decoding/transcoding, or PTY/ConPTY hosting.
