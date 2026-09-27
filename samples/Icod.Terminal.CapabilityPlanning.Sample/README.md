# Capability inspection and planning sample

This sample uses Terminal-owned APIs to report static screen advertisement, concrete plan availability, and the twelve current capability snapshots. It does not commit the example plans. Opening a session still captures and configures terminal input and restores it on exit; default reporting makes no explicit support queries or presentation-mode acquisitions.

```sh
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample -f net10.0 -- --help
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample -f net10.0
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample -f net10.0 -- --verify
```

`-h` is also accepted. Help and invalid-argument handling require no terminal. Normal execution requires interactive input/output. Use .NET 8, 9, or 10 by selecting the corresponding framework.

| Mode | Behavior |
| --- | --- |
| Default | Profile facts, parameter-specific plans, twelve status rows, application fallback decision. |
| `--verify` | The same report, then the existing keyboard, raster, and persistent-raster support paths, followed by fresh inspection. |
| `--help` / `-h` | Usage only; no session is opened. |

Exit codes are 0 for success/help, 1 for a runtime failure, 2 for an unknown argument, and 130 for cancellation. Ctrl+C requests cancellation. Session disposal runs before the CLI reports cancellation or a runtime error. Protocol or transport errors remain errors; silence is not automatically unsupported.

Read advertisement as representation presence, including empty or malformed representations. The concrete plan is a separate result: available (possibly zero bytes), unavailable, or rejected. The sample requests a cursor plan even when absolute positioning is absent, so home/relative and row/column alternatives remain usable. It reports erase, character shifts, line shifts, and scroll-region facts without emitting those operations.

A capability row separates support, endpoint availability, evidence kind, and current usability. These are immutable snapshots, not a terminal identity or a guarantee of physical execution. Re-inspection after verification can differ from the earlier snapshot. Old-generation replies cannot update current evidence after invalidation; a later explicit verification may establish fresh knowledge.

The three live paths retain one-second **per-query** deadlines. Aggregate raster verification can perform sequential queries; scheduling and response ownership also affect duration. The other nine capabilities remain inspection-only. Sixel alone cannot verify persistent raster support. Verification does not upload resources, append animation frames, set the clipboard, or enable input reporting.

Automated tests and the fresh-package consumer execute the same [report routine](CapabilityPlanningExample.cs) with scripted sessions. Their controlled transport rejects unexpected traffic and qualifies available and unavailable output without a physical emulator. See the [capability guide](../../docs/Capability-Inspection-and-Planning.md) for the full contract.
