# Persistent Raster Ownership

This document is the permanent 1.x authority for `Icod.Terminal` persistent terminal-resident raster resources and placements.

Version 1.11 established opaque resource/placement ownership, acknowledged transactions, bounded registries, generation-scoped certainty, and deterministic cleanup. Version 1.12 added bounded source-pixel cropping and signed z-order. Version 1.13 added bounded relative placement ownership with immutable parentage while preserving resource ownership as a separate lifetime axis. Version 1.14 added side-effect-free observation of Terminal's local ownership certainty and semantic loss/release reason. Version 1.15 added virtual placements and independently renderable Unicode-placeholder cells. Version 1.16 adds resource-owned animation sequences, opaque frame tokens, positive timing, terminal-driven playback, and a distinct sequence-certainty axis.

Historical tranche and versioned-roadmap documents explain how the design was developed and qualified. This document defines the supported semantic contract consumers should rely on.

## 1. Scope

`Icod.Terminal` exposes two distinct raster intents:

```text
DisplayRasterAsync(...)
    ephemeral raster display

CreateRasterResourceAsync(...)
    persistent terminal-resident resource ownership
        -> ordinary current-cursor placement ownership
        -> relative placement ownership
        -> acknowledged placement update
        -> virtual placeholder ownership and typed cell output
        -> resource-owned animation frames and playback control
        -> side-effect-free ownership/sequence observation
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

Versions 1.13 and 1.14 do not add another capability enum for relative placement or lifecycle observation. Relative placement and ownership-state observation are local contracts layered on the reviewed persistent ownership capability.

Virtual placeholder presentation and persistent animation remain distinct semantic capabilities:

```text
TerminalCapability.UnicodeRasterPlaceholders = 10
TerminalCapability.PersistentRasterAnimation = 11
```

Persistent raster support does not silently imply either capability. Animation inspection is side-effect free, and explicit verification does not invent a durable-state probe when no safe bounded probe exists. A successful correlated frame append can establish current-generation live animation evidence for that operation.

## 3. Public ownership surface

Resource creation is opaque:

```csharp
TerminalControlResult<TerminalRasterResource> result =
	await session.CreateRasterResourceAsync( image );

await using TerminalRasterResource resource = result.GetRequiredValue();
```

A resource can create one or more ordinary current-cursor placements:

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

Version 1.13 additionally permits a resource to create a placement relative to an existing current placement:

```csharp
TerminalControlResult<TerminalRasterPlacement> result =
	await childResource.CreateRelativePlacementAsync(
		parentPlacement,
		columnOffset: 2,
		rowOffset: -1,
		new TerminalRasterPlacementOptions {
			Columns = 20,
			Rows = 8,
			ZIndex = 1
		}
	);

await using TerminalRasterPlacement childPlacement = result.GetRequiredValue();
```

Relative offsets can be replaced while parentage remains immutable:

```csharp
TerminalControlMutationResult result = await childPlacement.UpdateRelativeAsync(
	columnOffset: -3,
	rowOffset: 2,
	new TerminalRasterPlacementOptions {
		Columns = 16,
		Rows = 6,
		ZIndex = 2
	}
);
```

Version 1.14 adds the same synchronous ownership snapshot property to resource and placement handles:

```csharp
TerminalRasterOwnershipState resourceState = resource.OwnershipState;
TerminalRasterOwnershipState placementState = placement.OwnershipState;
```

The public types do not expose Kitty image ids, image numbers, placement ids, parent numeric identities, session generation ids, raw APC command dictionaries, or a backend selector. There is no public reparenting operation and no mutable public parent property.

## 4. Two independent ownership axes — 1.13+

Persistent relative placement has two simultaneous relationships:

```text
TerminalRasterResource
    -> owns the placement's resource/storage membership

TerminalRasterPlacement parent
    -> owns the relative child's placement-lifetime subtree
```

A relative placement may therefore place Resource B while being a child of a placement belonging to Resource A.

These axes are intentionally independent:

- deleting or disposing a parent placement removes its relative child placements and descendants;
- removing a placement does not automatically dispose the raster resource used by that placement;
- a child raster resource may remain current and be used for another placement after one relative placement dies with its parent;
- disposing a raster resource removes that resource's placements and every relative descendant placement whose lifetime depends on those placements, even when a descendant placement belongs to another still-live resource;
- descendant raster resources remain independently owned unless separately disposed or invalidated.

Version 1.14 makes this separation directly observable. A descendant placement released by parent disposal can report `Released / AncestorReleased` while its independently owned raster resource remains `Current / None`.

## 5. Placement positioning and depth

Ordinary `CreatePlacementAsync(...)` establishes a depth-0 placement at the terminal's current cursor location. Persistent placement does not move the text cursor.

Relative `CreateRelativePlacementAsync(...)` establishes a child position from one immutable current parent. `columnOffset` and `rowOffset` are signed terminal-cell offsets, not source pixels, absolute screen coordinates, or pixel-within-cell offsets.

The portable public depth ceiling is:

```text
8
```

Depth rules are:

- ordinary placement: depth 0;
- direct relative child: parent depth + 1;
- creation that would produce depth 9 is rejected locally before output;
- the limit is independent of a terminal implementation's potentially larger private ceiling.

Parentage is immutable for the complete lifetime of a relative placement. Public construction can reference only an already-existing current parent, and no public operation can subsequently reparent a child. That makes cycles impossible through the supported public API. Internal graph invariants still validate ancestry defensively and fail closed on inconsistent state.

The complete signed `int` domain is retained for both relative offsets.

## 6. Cell extents and shared placement geometry

`TerminalRasterPlacementOptions` remains the single common placement-geometry contract for ordinary and relative placements.

`Columns` and `Rows` are nullable `int` values.

- `null` means protocol/default behavior for that dimension;
- each supplied value must be in `1..16384`;
- either dimension may be supplied independently;
- the backend may derive an unspecified dimension where its protocol supports that behavior.

Relative placement does not introduce a second options type. Source crop, cell extents, and z-order are valid common geometry for both positioning modes.

## 7. Source rectangle — 1.12+

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
- every present rectangle is revalidated by placement options, including `default(TerminalRasterSourceRectangle)` values that bypass the public constructor;
- the right and bottom edges must not exceed the actual source resource dimensions;
- `SourceRectangle == null` means the full source image;
- a present rectangle is emitted as all four crop fields together rather than partially;
- validation occurs before placement output commitment.

Resource bookkeeping therefore retains immutable source width/height metadata. It does **not** retain source pixel data.

Source cropping selects source pixels only. It does not define terminal screen position or clipping/layout policy for `Icod.DCurses`.

## 8. Signed z-order — 1.12+

`TerminalRasterPlacementOptions.ZIndex` is nullable signed `int`.

- every value from `int.MinValue` through `int.MaxValue` is accepted;
- negative values are preserved;
- `null` means backend/default stacking order;
- wire formatting uses invariant signed decimal representation.

Z-order expresses signed stacking order for one placement. It does not change parentage or create a general scene-composition model.

## 9. Update semantics

`UpdateAsync(...)` replaces the common placement geometry. It is not a patch against prior `TerminalRasterPlacementOptions`.

A caller that wants to retain a prior crop, extent, or z-order must supply it again in the update options.

The established positioning mode is preserved:

- on an ordinary placement, `UpdateAsync(...)` uses current-cursor positioning exactly as before 1.13;
- on a relative placement, `UpdateAsync(...)` preserves the immutable parent and the last acknowledged relative offsets while replacing common geometry.

`UpdateRelativeAsync(...)` is valid only for a relative placement. It replaces signed column/row offsets plus common geometry while preserving the same immutable parent.

No update API converts an ordinary placement to relative placement, converts a relative placement to ordinary current-cursor placement, or changes parentage.

## 10. Resource creation and acknowledgement

Persistent resource creation is acknowledged before the library publishes a usable public handle.

Internally, the session allocates a private nonzero image number for upload correlation. A successful acknowledgement must correlate that image number and return a nonzero terminal-assigned image id. Future placement and cleanup operations use that private identity.

If upload cannot establish reliable acknowledgement, no public resource is returned. The library does not publish an object whose terminal-side existence is ambiguous.

## 11. Placement acknowledgement and deterministic encoding

Placement creation and update reuse the existing authoritative query/input architecture. There is no graphics-specific reader.

A placement acknowledgement belongs to the operation only when the expected private child image id and child placement id correlate. Wrong identities do not satisfy another operation.

Correlated responses remain untrusted. Framing, numeric overflow, duplicate fields, response size, and semantic status are validated before success is accepted.

Ordinary current-cursor placement retains the reviewed 1.11/1.12 encoding and byte behavior when relative APIs are unused:

```text
Ga=p,i=<child-image-id>,p=<child-placement-id>,C=1[,x=...[,y=...[,w=...[,h=...]]]][,c=...][,r=...][,z=...]
```

Relative placement adds private parent identity and signed cell-offset fields in deterministic order while keeping child correlation authoritative:

```text
Ga=p,i=<child-image-id>,p=<child-placement-id>,C=1,
     P=<parent-image-id>,Q=<parent-placement-id>,H=<column-offset>,V=<row-offset>
     [,x=...[,y=...[,w=...[,h=...]]]][,c=...][,r=...][,z=...]
```

Updates commit requested relative offsets locally only after a successful acknowledgement. A rejected or abandoned update retains the last acknowledged offsets.

## 12. Local validation and no-output rejection

Locally knowable invalid relative operations are rejected before terminal output.

This includes:

- null parent;
- disposed parent handle;
- parent from a different `TerminalSession`;
- stale parent generation;
- stale child resource;
- attempted depth 9;
- `UpdateRelativeAsync(...)` on an ordinary placement;
- invalid common geometry;
- cancellation observed before commitment.

The registry also defensively rejects inconsistent ancestry rather than emitting traffic from corrupt internal graph state.

## 13. Timeout and late-response ownership

Persistent placement queries use the same bounded query manager as other terminal requests.

A timeout does not allow a stale acknowledgement from an earlier placement identity to complete a later operation. A later request may proceed according to ordinary query scheduling, but only a response correlated to that later private child image/placement identity may satisfy it.

Late-response handling remains bounded and does not create a second graphics-specific input path.

## 14. Cancellation and committed output

Arguments, options, ownership, endpoint/capability state, and caller cancellation are checked before commitment where possible.

The output gate may be cancelled before the first frame commits. Once persistent graphics output commits, ordinary caller cancellation does not intentionally truncate the logical transaction.

After partial committed output, the library does not blindly replay, switch to Sixel, or invent certainty about terminal state.

A transport failure remains a transport failure and is surfaced to the caller under the established ownership rules.

## 15. Bounded ownership

Session bookkeeping remains bounded independently of the terminal's own storage limits:

```text
maximum live persistent resources   256
maximum live persistent placements 4096
maximum relative placement depth       8
```

A full local registry returns controlled `Unavailable` before protocol output.

Private identities are nonzero, avoid live collisions, allocate monotonically where possible, and have explicit wraparound handling. Relative placement does not change the resource or placement capacity ceilings.

The registry stores ownership/lifecycle metadata only. After successful resource creation, the library does not retain an arbitrary hidden copy of the source raster image.

## 16. Generation-scoped certainty

Persistent terminal identities are valid only for the session generation in which they were established.

The following invalidate current terminal-resident certainty:

- explicit `TerminalSession.InvalidateState()`;
- managed suspend/resume generation changes;
- other lifecycle transitions that invalidate terminal state knowledge.

After invalidation:

- existing resource and placement handles observe `Stale / SessionStateLost` until their wrappers are disposed;
- the complete local relative graph is stale;
- placement update returns controlled `Unavailable` before output;
- new placement creation from a stale resource or stale parent returns controlled `Unavailable` before output;
- disposal releases local ownership without emitting stale numeric identifiers and changes that public wrapper to `Disposed / ExplicitDisposal`;
- no automatic re-upload or rebind occurs;
- no hidden source-image cache is consulted.

## 17. Terminal-negative responses — 1.13+

Terminal-resident graphics storage is external state. Correlated negative responses are classified narrowly enough to avoid inventing certainty or invalidating unrelated ownership.

### `ENOPARENT`

A correlated `ENOPARENT` means the terminal no longer recognizes the referenced parent placement relationship. Local certainty for that parent placement subtree is invalidated and affected reachable wrappers observe `Stale / ParentPlacementLost`. The raster resources behind that subtree are not automatically declared missing merely because a parent placement is lost.

### `ECYCLE`

The supported public API cannot construct a cycle because parentage is immutable and selected only at creation from an existing current placement. A correlated `ECYCLE` is therefore treated as a controlled failure; it does not justify broad unrelated resource invalidation and does not manufacture a lifecycle loss reason.

### `ETOODEEP`

The portable local depth limit is 8 and deeper creation is rejected before output. A correlated `ETOODEEP` from an otherwise reviewed operation remains a controlled failure rather than evidence that unrelated resources are missing and does not manufacture a lifecycle loss reason.

### `ENOENT`

A well-formed correlated `ENOENT` retains the established missing-resource/identity certainty behavior where it denotes a missing image/resource identity. The affected resource and dependent placement certainty observe `Stale / ResourceMissing`; unrelated resources are not invalidated merely by association through a relative graph.

Malformed correlated responses, wrong child identities, late responses, timeout, and transport failures retain the established bounded transaction-manager semantics and do not by themselves manufacture lifecycle-loss state.

Correlation establishes transaction ownership, not terminal authenticity.

## 18. Placement disposal and subtree cleanup

Placement disposal is locally idempotent.

For a current ordinary placement with no relative descendants, the first disposal performs the established targeted placement cleanup and the disposed public wrapper observes `Disposed / ExplicitDisposal`.

For a current parent with relative descendants, disposal:

1. prevents new mutation through the disposed handle;
2. releases the complete local descendant subtree exactly once;
3. publishes descendant placement state as `Released / AncestorReleased` while their wrappers remain reachable;
4. emits terminal placement deletes deepest-first / descendant-before-parent while identities remain current;
5. uses each placement's own owning raster-resource image identity, including across resources;
6. surfaces/aggregates cleanup transport failures according to the existing restoration model;
7. never restores local ownership merely so a later disposal can retry uncertain cleanup.

A later disposal of a child handle already released by parent cascading is harmless, does not double-release protocol identity, and changes that child wrapper to `Disposed / ExplicitDisposal`.

A stale placement/subtree performs local cleanup only.

## 19. Resource disposal

Resource disposal is locally idempotent and owns its direct placements.

For a current resource, disposal:

1. prevents new placements from that resource;
2. closes/releases the resource's direct placements;
3. publishes those direct placements as `Released / ResourceReleased` while still-reachable wrappers remain undisposed;
4. cascades through every relative descendant placement whose lifetime depends on those placements, publishing other-resource descendants as `Released / AncestorReleased`;
5. emits placement cleanup deepest-first, using each placement's own owning-resource image identity;
6. attempts terminal deletion of the disposed resource data after dependent placement cleanup;
7. releases local ownership even when cleanup transport fails;
8. aggregates multiple cleanup failures where necessary.

Other raster resources referenced by descendant placements remain independently owned unless separately disposed or invalidated.

An already-stale resource emits no stale terminal identifiers. Its public wrapper becomes `Disposed / ExplicitDisposal` when explicitly disposed.

## 20. Session teardown

Current-generation persistent graphics participate in ordinary session teardown.

The session drains committed transactions, deletes current placement graphs deepest-first before current resource data, aggregates persistent cleanup failures with the existing restoration model, and then continues final terminal restoration.

Already-stale persistent state receives local-only cleanup.

## 21. Security and privacy boundary

Persistent raster traffic is external terminal I/O.

Stable guarantees include:

- no caller-supplied raw Kitty image/placement identifiers;
- no public parent numeric identity, session generation id, or generic public persistent Kitty command builder;
- bounded image dimensions/storage inherited from `TerminalRasterImage`;
- bounded resource/placement registries and relative depth;
- bounded correlated response parsing;
- source-rectangle validation against immutable source dimensions before output;
- direct Kitty transfer only;
- no file, temporary-file, or shared-memory transport chosen silently;
- no retained arbitrary source-image cache after creation;
- no hidden replay after lifecycle uncertainty or partial commitment;
- observation reads local lifecycle state only and emits no terminal traffic;
- correlation establishes routing ownership, not terminal authenticity.

A successful acknowledgement proves only that a well-formed correlated response was received under the protocol contract. It does not authenticate the terminal, multiplexer, remote endpoint, host, desktop session, or user.

## 22. Backend neutrality

The public surface speaks in resource, source rectangle, cell extent, z-order, immutable parent placement, signed relative-cell-offset, and semantic lifecycle-certainty terms rather than Kitty protocol vocabulary.

Persistent ownership is not emulated through Sixel. Such emulation would require retaining/redrawing image data and would materially change lifecycle ownership.

The implementation may use Kitty Graphics internally, but callers plan against `PersistentRasterGraphics`, not terminal brand, `TERM`, APC framing, or numeric image identities. Reading `OwnershipState` does not select or probe a backend.

## 23. Explicit exclusions after 1.14

The persistent ownership contract does not include:

- passive remote `ExistsAsync()` / `VerifyExistsAsync()` semantics;
- terminal-authenticated object existence;
- mutating reconciliation probes presented as inspection;
- public backend ids or Kitty numeric identities;
- public parent numeric identities or session generation ids;
- caller-selected graphics backend;
- Sixel persistent-resource emulation;
- automatic replay/re-upload/rebind;
- retained source-image cache;
- reparenting or mutable parentage;
- Unicode placeholder / virtual placement;
- absolute screen-coordinate placement;
- pixel-within-cell positioning;
- animation/frame lifecycle;
- scene-graph, cell, window, damage, or layout ownership;
- image-file decoding/transcoding;
- Kitty file/temp-file/shared-memory transfer;
- PTY/ConPTY hosting.

Relative placement is supported as of 1.13 and lifecycle observation as of 1.14, but both deliberately remain bounded ownership contracts rather than entry points to the excluded scene-layout or reconciliation features.

## 24. Lifecycle observation — 1.14

Every public persistent resource and placement wrapper exposes:

```csharp
public TerminalRasterOwnershipState OwnershipState { get; }
```

The immutable snapshot contains:

```text
Status: Current | Stale | Released | Disposed
Reason: None | SessionStateLost | ResourceMissing | ParentPlacementLost |
        AncestorReleased | ResourceReleased | ExplicitDisposal
```

The state/reason pair is one atomic local observation. Callers do not observe torn combinations such as `Current / ResourceMissing` or `Released / None`.

The valid semantic combinations are:

```text
Current  / None
Stale    / SessionStateLost
Stale    / ResourceMissing
Stale    / ParentPlacementLost
Released / AncestorReleased
Released / ResourceReleased
Disposed / ExplicitDisposal
```

`Current` means Terminal's present local ownership model still regards the handle as current. It does **not** authenticate the terminal and does not prove that a subsequent remote operation must succeed. The reviewed backend provides no truthful passive arbitrary-object existence query, so 1.14 does not manufacture one.

Observation is synchronous and bounded. Reading it does not:

- write terminal bytes;
- register or allocate a terminal query;
- acquire the output gate;
- verify a capability;
- mutate registry ownership;
- trigger cleanup;
- replay/re-upload raster data;
- select or expose a graphics backend.

Underlying lifecycle transitions are monotonic. A stale or released internal ownership state never becomes current again, including after late acknowledgements. Explicit wrapper disposal takes precedence for that wrapper and reports `Disposed / ExplicitDisposal` without resurrecting the underlying ownership.

The canonical two-axis example is:

```text
Resource A      Current / None
  Placement A1 Current / None
Resource B      Current / None
  Placement B1 Current / None, relative to A1

Dispose A1
  Placement A1 Disposed / ExplicitDisposal
  Placement B1 Released / AncestorReleased
  Resource B    Current / None
```

Resource B can then create a fresh ordinary current placement if no independent evidence invalidated it.

## 25. Compatibility

Version 1.14 is additive over the stable `1.0.0` compatibility floor and the published 1.13 surface.

When lifecycle observation is not read:

- existing public signatures and enum numeric values remain unchanged;
- ordinary and relative persistent placement behavior and wire bytes remain unchanged;
- 1.12 source cropping and signed z-order semantics remain unchanged;
- 1.13 immutable parentage, signed relative offsets, and depth-8 graph behavior remain unchanged;
- resource/placement capacity ceilings remain 256 / 4096;
- one authoritative query/input path remains the acknowledgement authority;
- no new production dependency is introduced.

The final 1.14 public API fingerprint is:

```text
2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
```

## 26. Unicode raster placeholders — 1.15

Version 1.15 extends the same persistent ownership domain with an opaque virtual-placement handle:

```text
TerminalRasterResource
    +-- TerminalRasterPlacement
    +-- TerminalRasterPlaceholder
            +-- TerminalRasterPlaceholderCell
```

`TerminalRasterPlaceholder` represents acknowledged terminal-side virtual placement state. It does not represent a window, an absolute screen position, a DCurses cell, clipping state, damage state, scrolling policy, or layout. A placeholder owns only its bounded virtual-placement lifetime and semantic row/column extent.

Creation is resource-scoped:

```csharp
TerminalControlResult<TerminalRasterPlaceholder> result =
	await resource.CreatePlaceholderAsync(
		new TerminalRasterPlaceholderOptions {
			Columns = 24,
			Rows = 8
		}
	);

await using TerminalRasterPlaceholder placeholder = result.GetRequiredValue();
```

Both dimensions are required and bounded to `1..256`.

The semantic capability is distinct from ordinary persistent resource ownership:

```text
TerminalCapability.UnicodeRasterPlaceholders = 10
```

`InspectCapability(...)` remains side-effect free. Version 1.15 does not fabricate a placeholder-specific live probe. A successful correlated placeholder-creation acknowledgement supplies live evidence for that semantic operation.

## 27. Semantic placeholder cells and current-cursor output

`GetCell(row, column)` returns an immutable semantic token associated privately with one placeholder:

```csharp
TerminalRasterPlaceholderCell cell = placeholder.GetCell( row, column );
```

The public token exposes only zero-based semantic `Row` and `Column`. It has no public constructor and exposes no image id, placement id, generation id, backend id, APC dictionary, placeholder codepoint, combining-mark table, or SGR packing helper.

Every token is independently renderable. No cell depends on the previously emitted left neighbor or on raster-order emission. This permits callers to clip, sparsely redraw, reorder, or horizontally scroll cells without reconstructing private protocol identity.

Typed output is current-cursor text output:

```csharp
await session.WriteRasterPlaceholderCellAsync( cell );
await session.WriteRasterPlaceholderCellsAsync( cells );
```

Bulk output preserves caller ordering. Terminal does not move the cursor or choose absolute screen coordinates. The caller owns cursor movement, clipping, scrolling, layout, damage, and redraw order.

Internally each cell carries complete private image identity, complete virtual-placement identity, explicit row, explicit column, and the image high byte. Private foreground and underline color channels used for identity are terminated for each independently encoded cell with selective foreground/underline resets equivalent to SGR 39 and SGR 59. Background color and unrelated rendition attributes are not reset solely for placeholder identity.

Before output, Terminal validates that the token belongs to the current session/generation and that its owning placeholder remains current. Stale, released, disposed, cross-session, and unregistered-generation tokens fail before private identity is emitted.

## 28. Virtual placeholder as immutable relative parent

A current virtual placeholder may act as the immutable parent of an ordinary physical placement:

```csharp
TerminalControlResult<TerminalRasterPlacement> childResult =
	await childResource.CreateRelativePlacementFromPlaceholderAsync(
		placeholder,
		columnOffset: 2,
		rowOffset: -1,
		options
	);
```

The placeholder itself cannot be relative. The physical child retains the existing common placement geometry contract, including crop/extents/z-order, and the existing signed offset/depth rules.

A virtual root counts against the portable relative depth limit. Therefore the first physical child under a placeholder is effective depth 1 and the complete graph still stops at depth 8.

Placeholder lifetime and raster-resource lifetime remain separate axes. Explicitly disposing/releasing a current placeholder releases dependent physical descendants; those placements observe established ancestor-release semantics while their independently owned raster resources remain current unless separately released or invalidated. Correlated loss of the placeholder's owning resource stales dependent cross-resource physical descendants as `ParentPlacementLost` without falsely declaring their child resources missing.

## 29. 1.15 bounds, failures, and security boundary

Version 1.15 keeps one bounded ownership domain:

```text
maximum live persistent resources                    256
maximum live physical + virtual placements          4096
maximum relative placement depth                       8
placeholder columns                                 1..256
placeholder rows                                    1..256
private virtual-placement identity            1..0x00FFFFFF
```

Virtual placement identities are private, nonzero, live-collision-safe, monotonic where practical, and wrap-safe. They share the existing 4096 placement capacity rather than creating a second registry.

Placeholder creation reuses the authoritative bounded query/input transaction manager. Wrong acknowledgement identities, malformed responses, timeout, late responses, and transport failure do not manufacture `ResourceMissing`, `ParentPlacementLost`, or other unsupported lifecycle truth. A correlated missing-resource response follows the established narrow resource-loss semantics. After committed transport failure there is no blind retry, hidden backend switch, or raster replay/re-upload.

Failure while writing visible placeholder text remains ordinary committed text-output failure and does not by itself prove that the virtual placement ceased to exist.

The 1.15 public API remains backend-neutral. Protocol-private image ids, virtual-placement ids, placeholder encoding details, raw APC graphics construction, and backend selection stay internal. Higher-level renderers such as `Icod.DCurses` continue to own cells, windows, screen coordinates, clipping, scrolling, damage, layout, and refresh policy.

The frozen cross-TFM 1.15 public API fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```


## 30. Persistent raster animation — 1.16

Every `TerminalRasterResource` owns exactly one `TerminalRasterAnimation` controller:

```csharp
TerminalRasterAnimation animation = resource.Animation;
TerminalRasterAnimationFrame root = animation.RootFrame;
```

Accessing `Animation`, `RootFrame`, or `State` performs no terminal I/O. The controller is subordinate to the resource, has no public constructor, and is not independently disposable. Resource disposal remains the final authority for terminal-resident image and frame data.

The resource's original pixels are the root frame. Additional frames are published as opaque `TerminalRasterAnimationFrame` tokens only after a successful correlated full-frame append. Frame tokens have no public constructor and expose no frame number, image identity, generation identity, or protocol command fields.

Animation changes the current pixels of the same resource. It does not create another placement graph. Existing physical placements and virtual placeholders continue to refer to the resource; disposing a placement or placeholder does not delete animation frames or change animation ownership.

## 31. Full-frame, timing, and playback contract

`AddFrameAsync(...)` accepts one bounded `TerminalRasterImage` whose intrinsic width and height exactly match the owning resource. Version 1.16 does not expose partial-frame transfer, delta editing, composition, or retained source-frame replay.

Frame duration is a positive exact whole number of milliseconds in `1..int.MaxValue`. Fractional milliseconds, zero, negative values, and larger values are rejected before output. Duration control and current-frame selection accept only known opaque tokens owned by that animation.

The semantic playback operations are:

```text
StopAsync(...)
RunLoadingAsync(...)
RunAsync(...)
SelectFrameAsync(...)
SetFrameDurationAsync(...)
```

Loading mode waits at the known sequence tail so a caller can append later frames. `RunAsync()` uses indefinite looping by default. A non-null `RepeatCount` means additional traversals after the first and must be in `1..int.MaxValue - 1`. Raw animation-state, frame-selection, timing, and loop encodings remain internal.

## 32. Sequence certainty and failure behavior

Animation sequence certainty is independent of raster-resource ownership:

```text
TerminalRasterAnimationStatus.Current
TerminalRasterAnimationStatus.SequenceUncertain
TerminalRasterAnimationStatus.Stale
TerminalRasterAnimationStatus.Released
TerminalRasterAnimationStatus.OwnerDisposed
```

A committed append whose final outcome is ambiguous may have advanced the terminal's frame tail. Terminal therefore publishes no guessed token, performs no blind retry, and transitions the controller to `SequenceUncertain / FrameSequenceAmbiguous`.

In `SequenceUncertain` state, new appends and run modes that depend on a known tail return controlled unavailability. Stop remains available, and timing/selection of already-known frame tokens remain valid because their identities were previously established. Sequence uncertainty does not falsely stale the otherwise current raster resource or its placements/placeholders.

Wrong identities, malformed replies, timeouts, late replies, storage pressure, and transport failures remain bounded by the authoritative graphics-response transaction path. A correlated resource-missing result follows the established narrow resource-loss semantics; controlled playback failure does not poison the known frame sequence.

## 33. Animation capacity and lifecycle

Animation bookkeeping is session scoped and bounded:

```text
maximum known animation frames, including roots   4096
maximum pending append per animation                  1
```

The frame ceiling is shared across animations in one session and counts root frames plus acknowledged additions; pending append reservations also consume capacity while active. Capacity exhaustion is reported before new frame output.

Session generation loss or resource-missing evidence makes the animation stale. Intentional internal resource release makes it released. Explicit disposal of the resource wrapper makes it owner-disposed. These transitions are monotonic and observable through `TerminalRasterAnimation.State`.

Terminal animation states that are stale, released, or owner-disposed are pruned from capacity accounting under the registry lock. A sequence-uncertain animation retains its known frames and capacity because its acknowledged tokens remain meaningful and the terminal-side tail may still exist.

## 34. Animation security and compatibility boundary

Animation transfer uses the existing reviewed direct persistent-raster transport, serialized output gate, authoritative input/query reader, and correlated acknowledgement parser. It does not add file, temporary-file, shared-memory, decoder, or second-reader paths.

All locally knowable invalid arguments, cross-animation tokens, disposed handles, stale ownership, dimension mismatches, duration errors, loop errors, and capacity failures are rejected before private protocol identity is emitted. Once logical output commits, cancellation does not intentionally truncate the transaction, and failure does not trigger replay or backend switching.

The 1.16 surface is additive. Existing persistent resource, physical placement, relative placement, lifecycle-observation, and virtual-placeholder behavior is unchanged when animation APIs are unused. `Icod.DCurses` and other callers continue to own screen coordinates, cells, clipping, damage, layout, refresh policy, and higher-level animation timelines.
