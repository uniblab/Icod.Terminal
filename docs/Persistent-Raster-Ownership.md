# Persistent Raster Ownership

This document is the permanent 1.x authority for `Icod.Terminal` persistent terminal-resident raster resources and placements. Version 1.11 established the ownership domain; version 1.12 adds bounded source-pixel cropping and signed z-order without changing the underlying ownership/lifecycle model.

Historical tranche and versioned-roadmap documents explain how the design was developed and qualified. This document defines the supported semantic contract consumers should rely on.

## 1. Scope

`Icod.Terminal` exposes two distinct raster intents:

```text
DisplayRasterAsync(...)
    ephemeral raster display

CreateRasterResourceAsync(...)
    persistent terminal-resident resource ownership
        -> placement ownership
        -> placement update
        -> deterministic disposal
```

Persistent ownership is not a scene graph, virtual screen, window system, image database, or layout engine. `Icod.DCurses` remains responsible for cells, windows, clipping/layout policy, damage, and refresh strategy.

## 2. Semantic capability

Persistent ownership is represented by:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

It is intentionally distinct from ordinary `RasterGraphics`. A terminal may support ephemeral raster output without supporting persistent terminal-resident ownership.

`InspectCapability(...)` remains side-effect free. `VerifyCapabilityAsync(...)` is explicit and may issue only the reviewed bounded support probe for this semantic capability.

## 3. Public ownership surface

Resource creation is opaque:

```csharp
TerminalControlResult<TerminalRasterResource> result =
	await session.CreateRasterResourceAsync( image );

await using TerminalRasterResource resource = result.GetRequiredValue();
```

A resource can create one or more placements:

```csharp
TerminalControlResult<TerminalRasterPlacement> result =
	await resource.CreatePlacementAsync(
		new TerminalRasterPlacementOptions {
			SourceRectangle = new TerminalRasterSourceRectangle(
				0,
				0,
				320,
				180
			),
			Columns = 40,
			ZIndex = -1
		}
	);

await using TerminalRasterPlacement placement = result.GetRequiredValue();
```

A placement can be replaced at the current cursor location while retaining the same private placement identity:

```csharp
TerminalControlMutationResult result = await placement.UpdateAsync(
	new TerminalRasterPlacementOptions {
		SourceRectangle = new TerminalRasterSourceRectangle(
			20,
			10,
			300,
			160
		),
		Columns = 32,
		Rows = 16,
		ZIndex = 2
	}
);
```

The public types do not expose Kitty image ids, image numbers, placement ids, raw APC command dictionaries, or a backend selector.

## 4. Placement position and cell extent

`Columns` and `Rows` are nullable `int` values.

- `null` means protocol/default behavior for that dimension;
- each supplied value must be in `1..16384`;
- either dimension may be supplied independently;
- the backend may derive an unspecified dimension where its protocol supports that behavior.

Placement position is the terminal's current cursor location. Persistent placement does not move the text cursor.

To reposition an existing placement, move the terminal cursor through ordinary terminal semantics and call `UpdateAsync(...)`.

Version 1.12 does not add public absolute-cell or screen-pixel placement coordinates.

## 5. Source rectangle — 1.12

`TerminalRasterSourceRectangle` selects a bounded region of the owned source raster in source-image pixel coordinates.

```csharp
TerminalRasterSourceRectangle rectangle = new(
	x,
	y,
	width,
	height
);
```

Contract:

- `X` and `Y` are zero-based and non-negative;
- `Width` and `Height` are positive;
- scalar values respect `TerminalRasterImage.MaximumDimension`;
- the right and bottom edges must not exceed the actual source resource dimensions;
- `SourceRectangle == null` means the full source image;
- a present rectangle is emitted as all four crop fields together rather than partially;
- validation occurs before placement output commitment.

Resource bookkeeping therefore retains immutable source width/height metadata. It does **not** retain source pixel data.

Source cropping selects source pixels only. It does not define terminal screen position, relative placement graphs, or clipping/layout policy for `Icod.DCurses`.

## 6. Signed z-order — 1.12

`TerminalRasterPlacementOptions.ZIndex` is nullable signed `int`.

- every value from `int.MinValue` through `int.MaxValue` is accepted;
- negative values are preserved;
- `null` means backend/default stacking order;
- wire formatting uses invariant signed decimal representation.

Z-order is relative stacking intent for one placement. It does not create parent/child placement identity, graph lifetime ownership, cycle detection, or a general scene-composition model.

## 7. Complete replacement semantics

`UpdateAsync(...)` replaces the placement at the current cursor using the newly supplied options. It is not a patch against prior `TerminalRasterPlacementOptions`.

A caller that wants to retain a prior crop, extent, or z-order must supply it again in the update options.

## 8. Resource creation and acknowledgement

Persistent resource creation is acknowledged before the library publishes a usable public handle.

Internally, the session allocates a private nonzero image number for upload correlation. A successful acknowledgement must correlate that image number and return a nonzero terminal-assigned image id. Future placement and cleanup operations use that private identity.

If upload cannot establish reliable acknowledgement, no public resource is returned. The library does not publish an object whose terminal-side existence is ambiguous.

## 9. Placement acknowledgement and shared transaction

Placement creation and update reuse the existing authoritative query/input architecture. There is no graphics-specific reader.

A placement acknowledgement belongs to the operation only when the expected private terminal image id and placement id correlate. Wrong identities do not satisfy another operation.

Correlated responses remain untrusted. Framing, numeric overflow, duplicate fields, response size, and semantic status are validated before success is accepted.

The reviewed deterministic persistent placement encoding is:

```text
Ga=p,i=<id>,p=<id>,C=1[,x=...[,y=...[,w=...[,h=...]]]][,c=...][,r=...][,z=...]
```

Create and update share this encoder/transaction. Existing 1.11 bytes remain unchanged when `SourceRectangle` and `ZIndex` are omitted.

## 10. Timeout and late-response ownership

Persistent placement queries use the same bounded query manager as other terminal requests.

A timeout does not allow a stale acknowledgement from an earlier placement identity to complete a later operation. A later request may proceed according to ordinary query scheduling, but only a response correlated to that later private image/placement identity may satisfy it.

Late-response handling remains bounded and does not create a second graphics-specific input path.

## 11. Cancellation and committed output

Arguments, options, ownership, endpoint/capability state, and caller cancellation are checked before commitment where possible.

The output gate may be cancelled before the first frame commits. Once persistent graphics output commits, ordinary caller cancellation does not intentionally truncate the logical transaction.

After partial committed output, the library does not blindly replay, switch to Sixel, or invent certainty about terminal state.

A transport failure remains a transport failure and is surfaced to the caller.

## 12. Bounded ownership

Session bookkeeping is bounded independently of the terminal's own storage limits:

```text
maximum live persistent resources   256
maximum live persistent placements 4096
```

A full local registry returns controlled `Unavailable` before protocol output.

Private identities are nonzero, avoid live collisions, allocate monotonically where possible, and have explicit wraparound handling.

The registry stores ownership/lifecycle metadata only. After successful resource creation, the library does not retain an arbitrary hidden copy of the source raster image.

## 13. Generation-scoped certainty

Persistent terminal identities are valid only for the session generation in which they were established.

The following invalidate current terminal-resident certainty:

- explicit `TerminalSession.InvalidateState()`;
- managed suspend/resume generation changes;
- other lifecycle transitions that invalidate terminal state knowledge.

After invalidation:

- existing resource and placement handles are stale;
- placement update returns controlled `Unavailable` before output;
- new placement creation from a stale resource returns controlled `Unavailable` before output;
- disposal releases local ownership without emitting stale numeric identifiers;
- no automatic re-upload or rebind occurs;
- no hidden source-image cache is consulted.

## 14. Terminal eviction and `ENOENT`

Terminal-resident graphics storage is external state. The terminal may evict resources according to its own quota or policy even while the local handle is otherwise current.

A well-formed correlated `ENOENT` response invalidates the library's certainty for the affected resource/placement. The operation returns controlled `Unavailable`, and later operations do not emit stale identifiers.

Other well-formed terminal-negative responses remain controlled failures.

This is not automatic replay. The caller decides whether to establish capability and create a new resource.

## 15. Placement disposal

Placement disposal is locally idempotent.

For a current placement, the first disposal:

1. prevents new mutation;
2. releases local placement ownership exactly once;
3. attempts one quiet targeted terminal placement delete;
4. surfaces transport failure to the direct caller;
5. never restores local ownership merely so a later disposal can retry uncertain cleanup.

A stale placement performs local cleanup only.

## 16. Resource disposal

Resource disposal is locally idempotent and owns its children.

For a current resource, disposal:

1. prevents new child placements;
2. closes/releases child placement ownership;
3. attempts child placement deletion before resource-data deletion;
4. attempts terminal resource-data deletion;
5. releases local ownership even when cleanup transport fails;
6. aggregates multiple cleanup failures where necessary.

An already-stale resource emits no stale terminal identifiers.

## 17. Session teardown

Current-generation persistent graphics participate in ordinary session teardown.

The session drains committed transactions, deletes current placements before current resource data, aggregates persistent cleanup failures with the existing restoration model, and then continues final terminal restoration.

Already-stale persistent state receives local-only cleanup.

## 18. Security and privacy boundary

Persistent raster traffic is external terminal I/O.

Stable guarantees include:

- no caller-supplied raw Kitty image/placement identifiers;
- no generic public persistent Kitty command builder;
- bounded image dimensions/storage inherited from `TerminalRasterImage`;
- bounded resource/placement registries;
- bounded correlated response parsing;
- source-rectangle validation against immutable source dimensions before output;
- direct Kitty transfer only;
- no file, temporary-file, or shared-memory transport chosen silently;
- no retained arbitrary source-image cache after creation;
- no hidden replay after lifecycle uncertainty or partial commitment;
- correlation establishes routing ownership, not terminal authenticity.

A successful acknowledgement proves only that a well-formed correlated response was received under the protocol contract. It does not authenticate the terminal, multiplexer, remote endpoint, host, desktop session, or user.

## 19. Backend neutrality

The public surface speaks in resource, source rectangle, cell extent, z-order, and placement semantics rather than Kitty protocol vocabulary.

Persistent ownership is not emulated through Sixel. Such emulation would require retaining/redrawing image data and would materially change lifecycle ownership.

The implementation may use Kitty Graphics internally, but callers plan against `PersistentRasterGraphics`, not terminal brand, `TERM`, APC framing, or numeric image identities.

## 20. Explicit exclusions after 1.12

The persistent ownership contract does not include:

- public backend ids or Kitty numeric identities;
- caller-selected graphics backend;
- Sixel persistent-resource emulation;
- automatic replay/re-upload/rebind;
- retained source-image cache;
- Unicode placeholder/virtual placement;
- relative placement or parent placement identity;
- placement chains / graph cycle handling;
- absolute screen-pixel placement or Terminal-owned layout;
- animation/frame lifecycle;
- scene-graph ownership;
- image-file decoding/transcoding;
- Kitty file/temp-file/shared-memory transfer;
- PTY/ConPTY hosting;
- cells/windows/damage/layout ownership.

Source rectangles and signed z-order are supported as of 1.12; they are deliberately bounded additions to one placement rather than entry points to the excluded scene-graph features.
