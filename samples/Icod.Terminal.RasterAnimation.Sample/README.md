# Icod.Terminal.RasterAnimation.Sample

This sample demonstrates the backend-neutral persistent-raster animation model introduced in `Icod.Terminal 1.16.0` and bounded composition between known frames added in 1.22.0.

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

1. verifies `PersistentRasterGraphics` with its reviewed live probe, inspects `PersistentRasterAnimation`, and proceeds when graphics is usable and animation is not known unsupported; the animation capability has no passive probe, so an initially unknown status is expected;
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

The composition step lives in `RasterAnimationCompositionExample.ComposeAsync`. The automated scripted-terminal test invokes this same sample step with a two-by-two resource, verifies the emitted control frame, and withholds the acknowledgement until the operation is waiting. A returned unsuccessful result is reported with its status and the program exits. A timeout, cancellation, or transport exception currently propagates out of the executable sample; `await using` still disposes its owned resource. Such an exception after output may mean that destination pixels changed without a trustworthy acknowledgement. A consuming application can handle that uncertain path explicitly:

```csharp
try {
    TerminalControlMutationResult result = await animation.ComposeFrameAsync(
        animation.RootFrame,
        thirdFrame,
        new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
        destinationX: 1,
        destinationY: 1,
        TerminalRasterFrameCompositionMode.Replace
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

The catch treats precommit and committed exceptions conservatively. It does not claim that an exception proves the destination changed, and it never blindly replays a possibly applied composition.

`PersistentRasterAnimation` has no reviewed passive support query. The preflight verifies persistent raster graphics, checks for a usable graphics endpoint and no known animation rejection, and then attempts real acknowledged animation operations. An unknown animation status does not falsely stop a fresh session; successful controls establish live animation evidence. The resource creation, frame append, and composition results are checked separately. Verifying persistent raster graphics alone does not prove animation or composition support. See the [capability walkthrough](../Icod.Terminal.CapabilityPlanning.Sample/README.md) for the capabilities with live support paths.

The program contains no terminal-brand branch, graphics-backend selection, public numeric image/frame identity, raw control dictionary, retained source-frame replay cache, image-file decoder, or screen-layout policy. Returned failures are reported; exceptions propagate after resource cleanup. Neither path automatically retries composition.

The animation controller is resource-owned and is not independently disposable. Disposing the resource remains final terminal-side cleanup authority for the resource, its frames, and its placements.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
