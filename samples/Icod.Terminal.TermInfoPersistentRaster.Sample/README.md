# Icod.Terminal.TermInfoPersistentRaster.Sample

This sample demonstrates the loose-coupling integration contract between `Icod.TermInfo.Inspection 1.11.0` and `Icod.Terminal` persistent-raster execution.

The sample keeps the responsibilities separate:

- `Icod.TermInfo.Inspection` reads the static `TerminalDescription`, classifies persistent-raster lifecycle evidence, and produces a semantic lifecycle plan;
- `Icod.Terminal` owns the live terminal session, endpoint availability, capability verification, routing, resource ownership, placement ownership, and cleanup;
- the application owns the small bridge that converts a conclusive live `TerminalCapabilityStatus` into `Verified` lifecycle evidence before asking TermInfo to reclassify and replan.

The executable flow is:

```text
open TerminalSession
    -> inspect session.Terminal through TermInfo Inspection
    -> create persistent lifecycle request
    -> plan
    -> if Indeterminate and endpoint usable, VerifyCapabilityAsync(PersistentRasterGraphics)
    -> convert the conclusive Terminal result to caller-owned Verified evidence
    -> reclassify and replan
    -> on Success, create TerminalRasterResource
    -> create TerminalRasterPlacement
    -> update placement
    -> dispose placement/resource
```

Run it with, for example:

```text
dotnet run --project samples/Icod.Terminal.TermInfoPersistentRaster.Sample/Icod.Terminal.TermInfoPersistentRaster.Sample.csproj -f net10.0
```

The sample intentionally does **not** expose terminal-brand checks, backend selection, raw graphics commands, or protocol-private numeric identities. If static planning remains indeterminate or impossible, if the required interactive endpoint is unavailable, or if Terminal cannot establish a usable live persistent-raster route, the sample reports that semantic outcome and exits without pretending the operation succeeded.

`Icod.TermInfo.Inspection` is a dependency of this sample only. It is not a production dependency of the `Icod.Terminal` package.
