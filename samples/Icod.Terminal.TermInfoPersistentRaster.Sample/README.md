# Icod.Terminal.TermInfoPersistentRaster.Sample

This sample demonstrates the current loose-coupling integration contract between `Icod.TermInfo.Inspection 1.14.0` and `Icod.Terminal` persistent-raster execution.

The integration pattern was introduced in Icod.Terminal 1.11.1 for lifecycle planning. The current sample retains that boundary, continues to demonstrate the advanced-placement planner introduced in Icod.TermInfo 1.12 for source rectangles and signed z-order, and now also consumes the Icod.TermInfo 1.14 advisory raster-backend evidence and selection layer.

The responsibilities remain separate:

- `Icod.TermInfo.Inspection` reads the static `TerminalDescription`, classifies persistent-raster lifecycle, advanced-placement, and raster-backend availability evidence, and produces advisory semantic plans;
- `Icod.Terminal` owns the live terminal session, endpoint availability, capability verification, internal routing, resource ownership, placement ownership, concrete crop/z-order execution values, acknowledgements, protocol commitment, and cleanup;
- the application owns evidence bridges and explicit backend preference policy.

The executable flow is:

```text
open TerminalSession
    -> inspect session.Terminal through TermInfo Inspection
    -> create persistent lifecycle request
    -> lifecycle plan
    -> if Indeterminate and endpoint usable, VerifyCapabilityAsync(PersistentRasterGraphics)
    -> convert only a conclusive Terminal result to caller-owned Verified lifecycle evidence
    -> reclassify / replan lifecycle
    -> require source-rectangle + signed-z-order placement semantics
    -> inspect static TermInfo placement evidence
    -> advanced-placement plan
    -> if static placement evidence is Unknown, add caller-owned Declared evidence for the Icod.Terminal placement contract
    -> reclassify / replan placement
    -> preserve the original static lifecycle/placement context for Sixel
    -> obtain live PersistentRasterGraphics evidence when necessary
    -> caller-map only that conclusive live persistent result to Kitty Graphics availability
    -> build separate Sixel and Kitty backend candidates
    -> plan once without ranking to expose ambiguity/verification requirements
    -> plan again with explicit caller policy: Kitty Graphics, then Sixel
    -> require the reviewed Kitty persistent-raster route to be selected
    -> execute concrete crop/z-order values through Terminal
    -> dispose placement/resource
```

The advanced-placement evidence step is intentionally different from live lifecycle verification. `PersistentRasterGraphics` remains the coarse live Terminal capability; the sample does not pretend that one live capability observation is a separate source-rectangle or z-order probe. When static placement evidence is merely absent/unknown, the application may explicitly contribute the semantics guaranteed by the Terminal execution contract and let TermInfo reclassify that caller-owned evidence.

The backend-planning step is also deliberately caller-owned. A live `PersistentRasterGraphics` result may be mapped to Kitty Graphics availability here because Icod.Terminal's reviewed persistent-raster route is Kitty-based. Ordinary `RasterGraphics` does not identify Kitty versus Sixel, and `UnicodeRasterPlaceholders` is not treated as TermInfo 1.14 persistent lifecycle/placement evidence. Sixel retains its own static backend evidence plus the unstrengthened static lifecycle and placement context; Kitty receives only the evidence the application can justify from Terminal's live persistent-raster contract.

TermInfo's backend planner is advisory. It does not replace Icod.Terminal's production routing layer or perform terminal I/O. Explicit preference belongs to the application, so the sample names Kitty-first policy openly rather than introducing hidden ranking inside either library.

TermInfo never carries the concrete source rectangle or signed z-order value. Those execution values remain application/Terminal-owned. The current Icod.TermInfo 1.14 integration still does **not** plan Terminal's relative-placement parent graph or Unicode-placeholder semantics; immutable parentage, signed relative cell offsets, subtree lifetime, lifecycle observation, virtual placement ownership, and placeholder-cell encoding remain Terminal runtime concerns.

Run the sample with, for example:

```text
dotnet run --project samples/Icod.Terminal.TermInfoPersistentRaster.Sample/Icod.Terminal.TermInfoPersistentRaster.Sample.csproj -f net10.0
```

The sample intentionally does **not** use terminal-brand heuristics, raw graphics commands, caller-supplied protocol-private numeric identities, or direct protocol dispatch. If lifecycle planning remains indeterminate/impossible, if the required interactive endpoint is unavailable, if advanced placement is contradicted/unsupported, if backend planning cannot select the reviewed route under caller policy, or if Terminal cannot establish a usable live persistent-raster route, the sample reports that semantic outcome and exits without pretending the operation succeeded.

`Icod.TermInfo.Inspection` remains a dependency of this sample only. It is not a production dependency of the `Icod.Terminal` package, and `RasterBackendPlanner` is not used by Icod.Terminal's production router.
