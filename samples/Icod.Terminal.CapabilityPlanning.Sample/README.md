# Capability inspection and planning sample

This sample uses Terminal-owned APIs to report static screen advertisement, concrete plan availability, and the twelve current capability snapshots. It does not commit the example plans. Opening a session still captures and configures terminal input and restores it on exit; default reporting makes no explicit support queries or presentation-mode acquisitions.

## Prerequisites and commands

Run from the repository root with the .NET 10 SDK. Select `net8.0`, `net9.0`, or `net10.0` and install the matching runtime; building this checkout still requires the newer SDK. The sample references the checked-out library. Normal execution requires interactive standard input/output, while help and invalid-argument handling can run in CI or with redirected streams.

```sh
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample -f net10.0 -- --help
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample -f net10.0
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample -f net10.0 -- --verify
```

`-h` is also accepted. If normal execution reports that an endpoint is unavailable, run it in an interactive terminal rather than an IDE output pane or pipeline. The scripted verification below covers unavailable endpoints without requiring an interactive session.

| Mode | Behavior |
| --- | --- |
| Default | Profile facts, parameter-specific plans, twelve status rows, application fallback decision. |
| `--verify` | The same report, then the existing keyboard, raster, and persistent-raster support paths, followed by fresh inspection. |
| `--help` / `-h` | Usage only; no session is opened. |

Exit codes are 0 for success/help, 1 for a runtime failure, 2 for an unknown argument, and 130 for cancellation. Ctrl+C requests cancellation. Session disposal runs before the CLI reports cancellation or a runtime error. Protocol or transport errors remain errors; silence is not automatically unsupported.

Read advertisement as representation presence, including empty or malformed representations. The concrete plan is a separate result: available (possibly zero bytes), unavailable, or rejected. The sample requests a cursor plan even when absolute positioning is absent, so home/relative and row/column alternatives remain usable. It reports erase, character shifts, line shifts, and scroll-region facts without emitting those operations.

A capability row separates support, endpoint availability, evidence kind, and current usability. These are immutable snapshots, not a terminal identity or a guarantee of physical execution. Re-inspection after verification can differ from the earlier snapshot. Old-generation replies cannot update current evidence after invalidation; a later explicit verification may establish fresh knowledge.

The three live paths retain one-second **per-query** deadlines. Aggregate raster verification can perform sequential queries; scheduling and response ownership also affect duration. The other nine capabilities remain inspection-only. Sixel alone cannot verify persistent raster support. Verification does not upload resources, append animation frames, set the clipboard, or enable input reporting.

## Reading the output

The following is an abbreviated illustration using the synthetic profile from the package test host. It is not a recording from a physical terminal or a promise of what your terminal will report; profile facts, byte counts, and live results vary.

```text
Cursor: absolute=False, home=True, row=False, column=False, carriage-return=False
Concrete plan — Cursor to (2,3), current unknown: available, 6 bytes
Concrete plan — Erase to end of line: available, 0 bytes
Concrete plan — Insert one character: unavailable
RasterGraphics: support=Unknown, endpoint=Available, evidence=None, usable=False
Fallback decision: use text or another non-raster presentation path.
```

| Output | Interpretation |
| --- | --- |
| Absolute addressing absent, cursor plan available | The planner can use home and relative movement. Do not reject planning based solely on `SupportsAbsoluteCursorAddressing`. |
| Available, 0 bytes | The description contains an empty erase representation. This is a valid plan, with no bytes to emit; it does not prove that a physical erase occurred. |
| Unavailable plan | No plan exists for these arguments and this profile. Choose a fallback rather than inventing control sequences. |
| `Unknown` with `Available` endpoint | The endpoint can participate, but support knowledge is inconclusive. Silence after verification may leave this row unchanged. |

With a scripted successful reply, opt-in verification can produce:

```text
Verified result: RasterGraphics: support=Verified, endpoint=Available, evidence=LiveObservation, usable=True
```

That observation allows the sample to select its raster presentation path; the requested raster operation still needs to succeed. It says nothing about persistent resources, placeholders, or animation by itself.

The test host also exercises unavailable output, for example:

```text
RasterGraphics: support=Unknown, endpoint=Unavailable, evidence=None, usable=False
```

Endpoint availability and support are separate: an unavailable endpoint can also coexist with advertised or verified support. Verification skips query traffic when its required endpoint is unavailable. The standalone CLI normally requires interactive endpoints and may reject session creation before printing such a report.

## Re-inspecting after lifecycle changes

This embedding example assumes an open `session` and a caller-owned `cancellationToken`. An application that knows terminal state was lost can invalidate its assumptions and request a new snapshot:

```csharp
TerminalCapabilityStatus before = session.InspectCapability(
    TerminalCapability.RasterGraphics
);

// Call only when state really became uncertain, such as after an external reset.
// Managed lifecycle re-entry already performs its own invalidation.
session.InvalidateState();

TerminalCapabilityStatus current = session.InspectCapability(
    TerminalCapability.RasterGraphics
);
// 'before' is unchanged; 'current' reflects the new evidence generation.
if ( current.Support == TerminalCapabilitySupport.Unknown
    && current.EndpointAvailability == TerminalCapabilityEndpointAvailability.Available ) {
    current = await session.VerifyCapabilityAsync(
        TerminalCapability.RasterGraphics, cancellationToken
    );
}
// Choose presentation from current.IsUsable; handle operation failures separately.
```

Static advertisement survives invalidation, so the new support result can be `Advertised` rather than `Unknown`. The example chooses to verify only unknown support; an application may also explicitly verify advertised support when stronger knowledge is useful. Invalidation affects session state and generation-scoped ownership, so do not call it merely to refresh a status row. Old replies cannot establish evidence for the new generation. This snippet is an application integration pattern; the reporting CLI does not deliberately invalidate the session.

## Noninteractive verification

With the .NET 10 SDK and all three runtimes installed, build the sample and run headless help on each framework:

```sh
pwsh -NoProfile -File packaging/VerifyCapabilityPlanningSample.ps1 -Configuration Staging
```

To execute the actual report routine against a fresh package, including scripted successful replies and unavailable output:

```sh
pwsh -NoProfile -File packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/capability-candidate -Configuration Staging
pwsh -NoProfile -File packaging/VerifyCapabilityPlanningPackage.ps1 -ArtifactDirectory artifacts/capability-candidate -Configuration Staging
```

Use a dedicated artifact directory: the build script recreates that directory. `pwsh` invokes PowerShell 7; Windows PowerShell 5.1 users can substitute `powershell`. Package restore requires access to the configured NuGet sources or cached dependencies.

Automated tests and the fresh-package consumer execute the same [report routine](CapabilityPlanningExample.cs) with scripted sessions. Their controlled transport rejects unexpected traffic and checks alternative cursor planning, a zero-byte plan, all twelve status rows, the three live verification paths, and the other nine inspection-only results. These checks do not certify a physical emulator. See the [capability guide](../../docs/Capability-Inspection-and-Planning.md) for the full contract.
