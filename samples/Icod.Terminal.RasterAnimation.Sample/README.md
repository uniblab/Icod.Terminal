# Icod.Terminal.RasterAnimation.Sample

This sample demonstrates the backend-neutral persistent-raster animation model introduced in `Icod.Terminal 1.16.0`.

Run it with, for example:

```text
dotnet run --project samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -f net10.0
```

The sample keeps the ownership boundary explicit:

```text
application / higher-level renderer
    owns source frames, placement intent, and playback policy

Icod.Terminal
    owns opaque terminal image/frame identity, acknowledged transfer,
    frame-sequence certainty, serialized playback control, and cleanup
```

The executable flow:

1. obtains the current `PersistentRasterAnimation` status through `VerifyCapabilityAsync` and checks `IsUsable`; this capability is inspection-only, so the call emits no live support probe;
2. creates one persistent resource from an in-memory RGB24 root image;
3. obtains the resource-owned animation controller and opaque root-frame token without I/O;
4. assigns the root frame a positive duration;
5. appends two acknowledged full-size frames;
6. composes a one-pixel region of the root into the third frame, awaiting its acknowledgement;
7. creates one ordinary placement through the existing presentation API;
8. starts loading-mode playback and appends another frame while loading;
9. stops playback and explicitly selects a known frame;
10. runs normal playback with one additional traversal;
11. runs normal playback indefinitely, then stops it explicitly;
12. releases placement and resource ownership deterministically with `await using`.

The composition step lives in `RasterAnimationCompositionExample.ComposeAsync`. The automated scripted-terminal test invokes this same sample step with a two-by-two resource, verifies the emitted control frame, and withholds the acknowledgement until the operation is waiting. A failure reports its status and exits without guessing whether a committed composition changed the destination pixels. Recreate the resource and frames before trying again after an ambiguous outcome; do not replay a possibly applied composition.

`PersistentRasterAnimation` has no reviewed passive support query. The initial call returns current inspection knowledge; it does not upload a test frame or establish live animation support. An unknown or unavailable result causes the sample to report that animation is not currently usable and exit nonzero. Even when `IsUsable` is true, subsequent resource and animation operations can fail and are checked separately. Verifying ordinary or persistent raster graphics alone does not verify animation. See the [capability walkthrough](../Icod.Terminal.CapabilityPlanning.Sample/README.md) for the three capabilities with live support paths.

The program contains no terminal-brand branch, graphics-backend selection, public numeric image/frame identity, raw control dictionary, retained source-frame replay cache, image-file decoder, or screen-layout policy. A failed or ambiguous operation is reported and is never automatically retried.

The animation controller is resource-owned and is not independently disposable. Disposing the resource remains final terminal-side cleanup authority for the resource, its frames, and its placements.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
