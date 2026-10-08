# Icod.Terminal.RasterAnimation.Sample

This sample demonstrates the backend-neutral persistent-raster animation model introduced in `Icod.Terminal 1.16.0`, bounded composition between known frames added in 1.22.0, caller-supplied partial frame replacement added in 1.23.0, the geometry/planning contracts added in 1.24.0, and the transaction-confirmation contract added in 1.28.0.

## Current live-test evidence and limits

Graphics development reopened on 2026-10-06 for the bounded 1.28 published-spec
transaction and downstream ATLAS work. Unpatched stable Kitty 0.49.2 on WSL2
reproduces the missing acknowledgement for the documented
chunked animation-frame continuation first observed on 0.32.2: the silent upload
creates a frame, but its final continuation does not return the required ACK.
Bounded transfer waits cannot make that response arrive.

On 2026-10-08, the maintainer's source-built Kitty at
`96693f4c090e9477b51ffa46aed4abdcef52d037`, containing upstream fix `b493a63`,
completed both the default animation sample and all 1/4/16/64 tile-atlas
transaction workloads. The small sample visibly animated, cleaned up and exited
0. The atlas recording showed a changing horizontal strip because the tested
sample emitted all 64 placeholder cells consecutively. The sample now explicitly
writes eight rows and leaves the reports below the grid. A follow-up recording on
Terminal commit `70c6cacd2d26c3f962db513a23035f2570060cea` confirms that grid,
changing colors, completed 1/4/16/64 workloads, cleanup and exit status `0`; it
captures SDK `10.0.112` and a pre-run UTC timestamp. This source build still reports
version `0.49.2`; identify it by source commit, not by the version string alone. Exact
runtime/configuration provenance and the full DCurses ATLAS workload remain
separate acceptance gates.

After the 1.27 DA1 correction, the separate persistent-raster ownership sample
completed on Kitty 0.49.2 and briefly displayed its generated colors before cleanup.
That result establishes basic resource and placement operations only. A resource
probe, placement success or scripted-terminal test does not establish successful
live animation upload, composition, selection or playback in this environment.

The 1.28 transaction work keeps the published continuation grammar and does not
repeat the private image id as a Kitty 0.49.2 workaround. Do not treat a timeout
as proof that no pixels changed, or infer persistent support from ordinary
Kitty/Sixel output. See the
[versioned transaction compatibility record](../../docs/Kitty-Graphics-Transaction-Compatibility-1.28.md)
and [the hold record and reproducer](../../docs/Graphics-Development-Hold.md).

Run it with, for example:

```text
dotnet run --project samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -f net10.0
```

For deterministic, noninteractive confirmation terminology without opening a
terminal, pass `--headless-transcript`:

```text
dotnet run --project samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -f net10.0 -- --headless-transcript
```

That transcript distinguishes `Unspecified`, `OutputCommitted`, and
`ProtocolAcknowledged` and marks live qualification `NotRun`. It never claims
rendering. On the interactive paths, each successful mutation prints its status
and confirmation strength; definite failures print a fallback reason without a
confirmation, and cleanup prints its own outcome.

Pass `--tile-atlas` to run the 1.24 tile-presentation witness:

```text
dotnet run --project samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -f net10.0 -- --tile-atlas
```

That path prefers a direct cell-pixel query. If it times out, it explicitly attempts exact derivation from a terminal-pixel query and the current character dimensions. It never rounds. It checks the local planning ceilings before allocating a generated 8-by-8 atlas, reads the accepted resource's intrinsic geometry, creates a placeholder grid, and runs 1-, 4-, 16-, and 64-region damage workloads against two reusable caller-managed frames. The application writes eight placeholder cells per row with an explicit carriage return and line feed, in one screen-output transaction; the final line break keeps operation reports below the grid. Every region update is acknowledged before the completed back frame is selected; only then are the front/back references swapped. This remains a caller-owned two-frame witness built from Terminal primitives, not a Terminal-owned atlas API, remote atomicity, or gapless display.

The planning snapshot is local and advisory: it does not reserve capacity or report terminal memory, and the later create/append result remains authoritative. The preflight accepts focused RGB24 operation evidence that is unknown but presently usable. After the acknowledged regional work, the sample re-inspects that same operation and requires `Verified` support with `LiveObservation` evidence. That generation-scoped result does not prove composition, RGBA32 replacement, physical rendering, or future success.

The interactive path exercises the 1-, 4-, 16-, and 64-region workloads but does not present them as a benchmark. Controlled package-only measurements of operation count, update-frame bytes, acknowledgement and total latency, CPU, and allocations are recorded in the [1.24 tile-atlas measurement report](../../docs/Raster-Tile-Atlas-Measurement-1.24.md).

If geometry, planning, resource creation, placeholders, frame creation, an update, or selection is unavailable, the path prints a text-fallback reason and stops raster work. The generated colors and damage list are test presentation data. They are not a tile-map, camera, scene, asset decoder, or game rule.

The sample keeps the ownership boundary explicit:

```text
application / higher-level renderer
    owns tiles, source frames, layout, damage, placement intent,
    text fallback, front/back policy, and playback policy

Icod.Terminal
    owns live queries, local admission ceilings/counts, opaque terminal
    image/frame identity, acknowledged transfer, lifecycle certainty,
    serialized playback control, and cleanup
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
12. releases placement and resource ownership deterministically and reports the resource-cleanup outcome.

The partial replacement and composition steps live in `RasterAnimationCompositionExample.UpdateRegionAsync` and `ComposeAsync`. Automated scripted-terminal tests invoke both sample steps with a two-by-two resource, verify each emitted control frame, and withhold acknowledgement until the operation is waiting. A returned unsuccessful result is reported with its status and fallback reason before the program exits. A timeout, cancellation, or transport exception is reported as ambiguous because output may have committed; the executable does not retry and still attempts deterministic resource cleanup. A consuming application can handle that uncertain path explicitly:

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

The catch treats precommit and committed exceptions conservatively. It does not claim that an exception proves the destination changed, and it never blindly replays a possibly applied replacement. `OutputCommitted` likewise proves only the complete write-and-flush boundary. `ProtocolAcknowledged` adds a correlated successful protocol response. Neither value establishes that pixels were rendered; that remains a separate consented operator observation.

`PersistentRasterAnimation` has no reviewed passive support query. The preflight verifies persistent raster graphics, checks for a usable graphics endpoint and no known animation rejection, and then attempts real acknowledged animation operations. An unknown animation status does not falsely stop a fresh session; successful controls establish live animation evidence. The resource creation, frame append, and composition results are checked separately. Verifying persistent raster graphics alone does not prove animation or composition support. See the [capability walkthrough](../Icod.Terminal.CapabilityPlanning.Sample/README.md) for the capabilities with live support paths.

The program contains no terminal-brand branch, graphics-backend selection, public numeric image/frame identity, raw control dictionary, retained source-frame or delta cache, or image-file decoder. Its small placeholder-grid layout remains application-owned. Returned failures and ambiguous transport exceptions are reported, cleanup is reported separately, and neither path automatically retries composition.

The animation controller is resource-owned and is not independently disposable. Disposing the resource remains final terminal-side cleanup authority for the resource, its frames, and its placements.

See [`../../docs/Persistent-Raster-Ownership.md`](../../docs/Persistent-Raster-Ownership.md) for the permanent ownership contract and [`../README.md`](../README.md) for the sample catalog.
