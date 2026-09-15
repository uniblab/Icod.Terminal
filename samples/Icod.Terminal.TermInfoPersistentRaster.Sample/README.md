# Icod.Terminal.TermInfoPersistentRaster.Sample

This sample demonstrates the current loose-coupling integration contract between `Icod.TermInfo.Inspection 1.14.0` and `Icod.Terminal` persistent-raster execution.

The integration pattern was introduced in Icod.Terminal 1.11.1 for lifecycle planning. The current sample retains that boundary and consumes the current Inspection 1.14 package while continuing to demonstrate the advanced-placement planner introduced in Icod.TermInfo 1.12 for source rectangles and signed z-order.

The responsibilities remain separate:

- `Icod.TermInfo.Inspection` reads the static `TerminalDescription`, classifies persistent-raster lifecycle and advanced-placement evidence, and produces semantic plans;
- `Icod.Terminal` owns the live terminal session, endpoint availability, capability verification, routing, resource ownership, placement ownership, concrete crop/z-order execution values, acknowledgements, and cleanup;
- the application owns the evidence bridges used to strengthen an indeterminate plan before replanning.

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
    -> on lifecycle Success and placement Satisfied, execute concrete crop/z-order values through Terminal
    -> dispose placement/resource
```

The advanced-placement evidence step is intentionally different from live lifecycle verification. `PersistentRasterGraphics` remains the coarse live Terminal capability; the sample does not pretend that one live capability observation is a separate source-rectangle or z-order probe. When static placement evidence is merely absent/unknown, the application may explicitly contribute the semantics guaranteed by the Terminal execution contract and let TermInfo reclassify that caller-owned evidence.

TermInfo never carries the concrete source rectangle or signed z-order value. Those execution values remain application/Terminal-owned. The current Icod.TermInfo 1.14 integration still does **not** plan Terminal's relative-placement parent graph; immutable parentage, signed relative cell offsets, subtree lifetime, lifecycle observation, and Unicode placeholder ownership remain Terminal runtime concerns.

Run the sample with, for example:

```text
dotnet run --project samples/Icod.Terminal.TermInfoPersistentRaster.Sample/Icod.Terminal.TermInfoPersistentRaster.Sample.csproj -f net10.0
```

The sample intentionally does **not** expose terminal-brand checks, backend selection, raw graphics commands, or protocol-private numeric identities. If lifecycle planning remains indeterminate/impossible, if the required interactive endpoint is unavailable, if advanced placement is contradicted/unsupported, or if Terminal cannot establish a usable live persistent-raster route, the sample reports that semantic outcome and exits without pretending the operation succeeded.

`Icod.TermInfo.Inspection` remains a dependency of this sample only. It is not a production dependency of the `Icod.Terminal` package.
