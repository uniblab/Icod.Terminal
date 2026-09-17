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

1. verifies the semantic `PersistentRasterAnimation` capability;
2. creates one persistent resource from an in-memory RGB24 root image;
3. obtains the resource-owned animation controller and opaque root-frame token without I/O;
4. assigns the root frame a positive duration;
5. appends two acknowledged full-size frames;
6. creates one ordinary placement through the existing presentation API;
7. starts loading-mode playback and appends another frame while loading;
8. stops playback and explicitly selects a known frame;
9. runs normal playback with one additional traversal;
10. runs normal playback indefinitely, then stops it explicitly;
11. releases placement and resource ownership deterministically with `await using`.

The program contains no terminal-brand branch, graphics-backend selection, public numeric image/frame identity, raw control dictionary, retained source-frame replay cache, image-file decoder, or screen-layout policy. A failed or ambiguous operation is reported and is never automatically retried.

The animation controller is resource-owned and is not independently disposable. Disposing the resource remains final terminal-side cleanup authority for the resource, its frames, and its placements.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
