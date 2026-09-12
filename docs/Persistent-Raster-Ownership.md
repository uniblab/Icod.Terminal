# Persistent Raster Ownership

This document is the permanent 1.x authority for `Icod.Terminal` persistent terminal-resident raster resources and placements introduced in version `1.11.0`.

Historical C110–C119 tranche documents explain how the design was developed and qualified. This document defines the supported semantic contract consumers should rely on.

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

Persistent ownership is not a scene graph, virtual screen, window system, or image database. `Icod.DCurses` remains responsible for cells, windows, clipping/layout policy, damage, and refresh strategy.

## 2. Semantic capability

Persistent raster ownership is represented by:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

It is intentionally distinct from ordinary `RasterGraphics`.

```text
RasterGraphics
    may be usable through verified Kitty Graphics or verified Sixel

PersistentRasterGraphics
    is usable only through the reviewed persistent-capable Kitty Graphics path
```

A terminal may therefore support ordinary raster output without supporting this ownership domain.

`TerminalSession.InspectCapability(...)` remains side-effect free. `TerminalSession.VerifyCapabilityAsync(...)` is explicit and may issue only the reviewed bounded support probe for this semantic capability.

## 3. Public ownership surface

The public resource model is opaque:

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
			Columns = 24
		}
	);

await using TerminalRasterPlacement placement = result.GetRequiredValue();
```

A placement can be replaced at the current cursor location while preserving the same private placement identity:

```csharp
TerminalControlMutationResult result = await placement.UpdateAsync(
	new TerminalRasterPlacementOptions {
		Columns = 16,
		Rows = 8
	}
);
```

The public types do not expose Kitty image ids, image numbers, placement ids, raw APC command dictionaries, or a backend selector.

## 4. Placement size and position

`TerminalRasterPlacementOptions.Columns` and `.Rows` are nullable `int` values.

- `null` means use protocol/default behavior for that dimension;
- each supplied value must be in `1..16384`;
- either dimension may be supplied independently;
- the backend derives an unspecified dimension while preserving image aspect ratio where the protocol supports that behavior.

Placement position is the terminal's current cursor location. `Icod.Terminal` does not add public pixel-coordinate or absolute-cell-coordinate placement state in 1.11.

The reviewed backend requests placement without moving the text cursor.

To reposition an existing placement, move the terminal cursor through ordinary terminal semantics and call `UpdateAsync(...)`.

## 5. Resource creation and acknowledgement

Persistent resource creation is acknowledged before the library publishes a usable public handle.

Internally, the session allocates a private nonzero image number for upload correlation. A successful acknowledgement must correlate that image number and return a nonzero terminal-assigned image id. Future placement and cleanup operations use that private terminal image identity.

The public handle contains no protocol identity that callers can manufacture or reuse.

If upload cannot establish a reliable acknowledgement, no public resource is returned. The library does not publish an object whose terminal-side existence is ambiguous.

## 6. Placement acknowledgement and query ownership

Placement creation and update reuse the existing authoritative query/input architecture. There is no graphics-specific input reader.

A placement acknowledgement is owned by the active transaction only when the expected terminal image id and placement id correlate. Wrong identities do not satisfy another operation.

Correlated terminal responses remain untrusted. The implementation validates framing, numeric overflow, duplicate fields, and bounded response sizes before accepting semantic success.

Malformed correlated responses are failures rather than capability evidence.

## 7. Cancellation and committed output

Arguments, options, current ownership, endpoint/capability state, and caller cancellation are checked before commitment where possible.

The output gate may be cancelled before the first frame commits. Once persistent graphics output commits, ordinary caller cancellation does not intentionally truncate the logical transaction.

After partial committed output, the library does not blindly replay, switch to Sixel, or invent certainty about terminal state.

A transport failure remains a transport failure and is surfaced to the caller.

## 8. Bounded ownership

Session bookkeeping is bounded independently of the terminal's own storage limits:

```text
maximum live persistent resources   256
maximum live persistent placements 4096
```

A full local registry returns controlled `Unavailable` before protocol output.

Private resource/image-number and placement identities are nonzero, avoid live collisions, allocate monotonically where possible, and have explicit wraparound handling.

The registry stores ownership/lifecycle bookkeeping only. After successful resource creation, the library does not retain an arbitrary hidden copy of the source raster image.

## 9. Generation-scoped certainty

Persistent terminal identities are valid only for the session generation in which they were established.

The following invalidate existing terminal-resident certainty:

- explicit `TerminalSession.InvalidateState()`;
- managed suspend/resume generation changes;
- other session lifecycle transitions that invalidate terminal state knowledge.

After invalidation:

- existing resource and placement handles are stale;
- placement update returns controlled `Unavailable` before output;
- new placement creation from a stale resource returns controlled `Unavailable` before output;
- disposal releases local ownership but does not emit stale numeric identifiers;
- the library does not automatically re-upload or rebind the raster;
- no hidden source-image cache is consulted because 1.11 does not retain one for replay.

A caller that needs persistent graphics again must establish current capability and create a new resource explicitly.

## 10. Terminal eviction and `ENOENT`

Terminal-resident image storage is external state. The terminal may evict resources according to its own quota or policy even while the local handle remains otherwise current.

A well-formed correlated Kitty Graphics `ENOENT` response for a resource/placement the session believed current invalidates that ownership certainty. The operation returns controlled `Unavailable` semantics and later operations do not emit stale identifiers.

Other well-formed negative terminal responses remain controlled `Failed` results with bounded diagnostic text where applicable.

This behavior is not automatic replay. The caller remains responsible for deciding whether to create a new resource.

## 11. Placement disposal

Placement disposal is locally idempotent.

For a current placement, the first disposal:

1. prevents new mutation;
2. releases local placement ownership exactly once;
3. attempts one quiet targeted terminal placement delete;
4. surfaces transport failure to the direct caller;
5. never restores local ownership merely so a later disposal can retry uncertain cleanup.

A stale placement performs local cleanup only.

## 12. Resource disposal

Resource disposal is locally idempotent and owns its children.

For a current resource, disposal:

1. prevents new child placements;
2. closes/releases child placement ownership;
3. attempts child placement deletion before resource-data deletion;
4. attempts the terminal resource-data delete;
5. releases local ownership even when cleanup transport fails;
6. aggregates multiple cleanup transport failures where necessary.

Disposing an already stale resource emits no stale terminal identifiers.

## 13. Session teardown

Current-generation persistent graphics participate in ordinary session teardown.

The session drains committed transactions, deletes current placements before current resource data, aggregates persistent cleanup failures with the existing cleanup/restoration model, and then continues final terminal restoration according to the broader session contract.

If persistent state was already invalidated, teardown performs local ownership cleanup only.

## 14. Security and privacy boundary

Persistent raster traffic is external terminal I/O.

Stable guarantees include:

- no caller-supplied raw Kitty image/placement identifiers;
- no generic public persistent Kitty command builder;
- bounded image dimensions/storage inherited from `TerminalRasterImage`;
- bounded resource/placement registries;
- bounded correlated response parsing;
- direct Kitty transfer only;
- no file, temporary-file, or shared-memory transport chosen silently;
- no retained arbitrary source-image cache after creation;
- no hidden replay after lifecycle uncertainty or partial commitment;
- correlation establishes routing ownership, not terminal authenticity.

A successful acknowledgement proves only that a well-formed correlated response was received under the protocol contract. It does not authenticate the emulator, multiplexer, remote endpoint, host, desktop session, or user.

## 15. Backend neutrality

The persistent public surface intentionally speaks in resource and placement semantics rather than Kitty protocol vocabulary.

Version 1.11 does not emulate persistence through Sixel. Such emulation would require the library to retain and redraw image data and would change the ownership/lifecycle contract substantially.

The implementation may use Kitty Graphics internally, but callers should plan against `PersistentRasterGraphics`, not terminal brand, `TERM`, APC framing, or numeric image identifiers.

## 16. Explicit exclusions

The 1.11 persistent ownership contract does not include:

- public backend ids or Kitty numeric identities;
- caller-selected graphics backend;
- Sixel persistent-resource emulation;
- automatic replay/re-upload/rebind;
- retained source-image cache;
- source rectangles;
- z-order;
- Unicode placeholder placement;
- relative placement;
- pixel-coordinate placement;
- animation;
- scene-graph ownership;
- image-file decoding/transcoding;
- Kitty file/temp-file/shared-memory transfer;
- PTY/ConPTY hosting;
- cells/windows/damage/layout ownership.

Later releases may add narrowly reviewed functionality without weakening this base ownership contract.

## 17. Sample and related authorities

A focused backend-neutral example is available at:

```text
samples/Icod.Terminal.PersistentRaster.Sample/
```

Related permanent authorities are:

- `Architecture.md`;
- `Security-and-Privacy.md`;
- `Compatibility-and-Versioning.md`;
- `Capability-Inspection-and-Planning.md`;
- `Terminal-Session-and-Ownership.md`;
- `Lifecycle-and-Restoration.md`.

The final 1.11 API surface is frozen by `Public-API-Baseline-1.11.md` and its SHA-256 fingerprint.
