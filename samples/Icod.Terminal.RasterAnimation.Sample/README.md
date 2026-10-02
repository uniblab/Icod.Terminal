# Icod.Terminal.RasterAnimation.Sample

This sample demonstrates the backend-neutral persistent-raster animation model introduced in `Icod.Terminal 1.16.0`, bounded composition between known frames added in 1.22.0, caller-supplied partial frame replacement added in 1.23.0, and the geometry/planning contracts added in 1.24.0.

Run it with, for example:

```text
dotnet run --project samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -f net10.0
```

Pass `--tile-atlas` to run the 1.24 tile-presentation witness:

```text
dotnet run --project samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -f net10.0 -- --tile-atlas
```

That path prefers a direct cell-pixel query. If it times out, it explicitly attempts exact derivation from a terminal-pixel query and the current character dimensions. It never rounds. It checks the local planning ceilings before allocating a generated 8-by-8 atlas, reads the accepted resource's intrinsic geometry, creates a placeholder grid, and runs 1-, 4-, 16-, and 64-region damage workloads against two reusable known frames. Every region update is acknowledged before the completed back frame is selected; only then are the front/back references swapped. This is ordered two-frame presentation, not remote atomicity or gapless display.

If geometry, planning, resource creation, placeholders, frame creation, an update, or selection is unavailable, the path prints a text-fallback reason and stops raster work. The generated colors and damage list are test presentation data. They are not a tile-map, camera, scene, asset decoder, or game rule.

The sample keeps the ownership boundary explicit:

```text
application / higher-level renderer
    owns source frames, placement intent, and playback policy

Icod.Terminal
    owns opaque terminal image/frame identity, acknowledged transfer,
    frame-sequence certainty, serialized playback control, and cleanup
```

The executable flow:

1. verifies `PersistentRasterGraphics` with its reviewed live probe, inspects `PersistentRasterAnimation`, and proceeds when graphics is usable and animation is not known unsupported; the animation capability has no passive probe, so an initially unknown status is expected;
2. creates one persistent resource from an in-memory RGB24 root image;
3. obtains the resource-owned animation controller and opaque root-frame token without I/O;
4. assigns the root frame a positive duration;
5. appends two acknowledged full-size frames;
6. replaces one pixel in the third frame from an immutable caller-owned RGBA32 region, awaiting its acknowledgement;
7. composes a different one-pixel region of the root into the third frame;
8. creates one ordinary placement through the existing presentation API;
9. starts loading-mode playback and appends another frame while loading;
10. stops playback and explicitly selects a known frame;
11. runs finite and indefinite normal playback;
12. releases placement and resource ownership deterministically with `await using`.

The partial replacement and composition steps live in `RasterAnimationCompositionExample.UpdateRegionAsync` and `ComposeAsync`. Automated scripted-terminal tests invoke both sample steps with a two-by-two resource, verify each emitted control frame, and withhold acknowledgement until the operation is waiting. A returned unsuccessful result is reported with its status and the program exits. A timeout, cancellation, or transport exception currently propagates out of the executable sample; `await using` still disposes its owned resource. Such an exception after output may mean that destination pixels changed without a trustworthy acknowledgement. A consuming application can handle that uncertain path explicitly:

```csharp
try {
    TerminalRasterImage patch = TerminalRasterImage.CreateRgba32(
        1, 1, [ 32, 224, 160, 255 ]
    );
    TerminalControlMutationResult result = await animation.UpdateFrameRegionAsync(
        thirdFrame, patch, destinationX: 0, destinationY: 1
    );
    if ( !result.Succeeded ) {
        // Report result.Status and result.Message; do not retry automatically.
        return 1;
    }
} catch ( Exception error ) when (
    error is TimeoutException or IOException or OperationCanceledException
) {
    // The operation may have committed. Leave this resource's frame pixels behind.
    // Recreate the resource and its frames from caller-owned art before another attempt.
    return 1; // The enclosing await using disposes the owned raster resource.
}
```

The catch treats precommit and committed exceptions conservatively. It does not claim that an exception proves the destination changed, and it never blindly replays a possibly applied replacement.

`PersistentRasterAnimation` has no reviewed passive support query. The preflight verifies persistent raster graphics, checks for a usable graphics endpoint and no known animation rejection, and then attempts real acknowledged animation operations. An unknown animation status does not falsely stop a fresh session; successful controls establish live animation evidence. The resource creation, frame append, and composition results are checked separately. Verifying persistent raster graphics alone does not prove animation or composition support. See the [capability walkthrough](../Icod.Terminal.CapabilityPlanning.Sample/README.md) for the capabilities with live support paths.

The program contains no terminal-brand branch, graphics-backend selection, public numeric image/frame identity, raw control dictionary, retained source-frame or delta cache, image-file decoder, or screen-layout policy. Returned failures are reported; exceptions propagate after resource cleanup. Neither path automatically retries composition.

The animation controller is resource-owned and is not independently disposable. Disposing the resource remains final terminal-side cleanup authority for the resource, its frames, and its placements.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
