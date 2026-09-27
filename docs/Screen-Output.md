# Semantic screen output

`TerminalSession.Screen` creates opaque, side-effect-free screen-operation plans. `CreateScreenOutputTransaction()` collects those plans and application text for a single serialized commit. Terminal interprets capabilities and owns output framing; your renderer owns cells, layout, damage, physical-state assumptions, and repaint policy.

The [screen-output sample](../samples/Icod.Terminal.ScreenOutput.Sample/Program.cs) and its [frame routine](../samples/Icod.Terminal.ScreenOutput.Sample/ScreenOutputExample.cs) demonstrate this boundary. The package acceptance harness executes the same frame routine with a recording transport.

## Inspect and plan

Read `session.GetDimensions().GetRequiredValue()` for positive cell dimensions and `session.Profile.Screen` for semantic profile facts. A profile is capability evidence, not live verification that the terminal has accepted a command. Your application decides text width, clipping, wrapping, and whether an operation is appropriate for the retained screen.

Start with `PlanRenditionBaseline()` whenever the physical rendition is unknown. Only a non-null baseline establishes that Terminal can restore every rendition axis exposed by this profile. Do not silently skip a missing baseline and then claim the current rendition is default.

```csharp
TerminalScreenPlanner planner = session.Screen;
TerminalScreenOperationPlan? baseline = planner.PlanRenditionBaseline();
TerminalScreenOperationPlan? cursor = planner.PlanCursorMove(
    null, new TerminalScreenPosition(0, 0));
if (!baseline.HasValue || !cursor.HasValue) {
    // Select an application fallback or report that this renderer is unavailable.
    return;
}

TerminalScreenOutputTransaction frame = session.CreateScreenOutputTransaction();
frame.Add(baseline.Value);
frame.Add(cursor.Value);
frame.WriteText("Hello");
await frame.CommitAsync(cancellationToken);
```

An unavailable plan (`null`) means the profile cannot supply the requested safe operation. A valid zero-byte plan is different: it represents an operation that requires no emitted bytes. Preserve this distinction instead of using byte count as an availability test.

Normalize desired rendition through `NormalizeRendition(...)` before retaining it as your intended physical state. Use `PlanRenditionTransition(current, target)` only when the supplied current state is known. Use `PlanRenditionReset(current)` for known-state restoration and `PlanRenditionBaseline()` for unknown-state recovery.

## Cursor-route expansion in 1.19

`PlanCursorMove(current, target)` still returns the least-cost complete advertised route. In addition to existing absolute, row/column, home, and relative routes, it considers:

- Home, relative down, and relative right, including an unknown starting position.
- Carriage return and relative right when the caller knows the cursor is already on the target row.

Every required movement must be available. Padding is handled by Terminal, complete byte costs are compared, and existing candidates retain preference on equal-cost ties. Repeated fallback sources remain bounded. The caller must supply coordinates appropriate to the terminal's current coordinate/mode context; Terminal does not invent a screen layout or clamp the target.

Cursor visibility remains owned by presentation leases. It is not a new screen-plan operation in 1.19.

## Construct and commit

Create a fresh transaction after deciding what to emit and before adding plans/text. Build it from one caller; concurrent mutation of a builder is not supported. Plans must belong to the same session. Keep text, hyperlinks, and raster-placeholder tokens within the existing semantic APIs.

Transactions retain at most 65,536 items and 64 MiB of counted payload. Rejected additions do not partially mutate the builder. Planner repeated-source expansion is separately bounded at 1,048,576 characters. These limits bound a transaction; applications decide when to split a larger refresh into multiple commits.

Commit is single-use, including a pre-cancelled attempt. Creating a transaction captures an output epoch. Intervening session-owned output can make it stale, in which case commitment rejects it before writing. A dimension read itself does not acquire the output gate. Resize/resume handling can involve other lifecycle operations, so refresh your observations and physical-state assumptions rather than assuming every event preserves a pending transaction.

Do not write directly to borrowed `session.Output` while relying on transaction ordering. The historical low-level `WriteTerminalStringAsync(...)` escape hatch also requires caller coordination. Prefer the session's coordinated semantic output methods.

Synchronized output is optional and must respect its existing capability and ownership contract. A transaction that opens synchronized framing or hyperlinks reserves the corresponding managers. Conflicting leases or pending cleanup reject commitment. Ordinary unframed output can run inside existing scopes without taking their ownership.

## Cancellation and failure

Cancellation before output commitment prevents emission. After commitment begins, caller cancellation does not intentionally truncate the logical output or its required cleanup. Hyperlink closes, synchronized end frames, and flushes are attempted under the existing cleanup rules; independent failures are surfaced in attempt order.

A failed committed transaction may have emitted a prefix. It is not a rollback operation. Mark your retained physical-state assumptions unknown, resolve the cause, and choose a fresh repaint or fallback. Never retry the consumed builder or automatically replay the failed byte stream. Even a fresh baseline is usable only when `PlanRenditionBaseline()` supplies one; Terminal cannot repair an unavailable transport or guarantee recovery from arbitrary terminal parser corruption.

Dispose the session/presentation leases through `await using` to attempt owned cleanup. Cleanup failures remain observable and should be reported by the host application.

## Run and verify the example

Run on an interactive terminal; the example writes at the top-left and restores its text rendition:

```sh
dotnet run --project samples/Icod.Terminal.ScreenOutput.Sample -f net10.0
```

For noninteractive smoke verification, build a candidate package and run the downstream verifier. It hosts the same sample frame routine in a synthetic terminal, checks exact bytes, executes missing-baseline rejection, and runs DCurses 1.6.0/2.2.0 compatibility workloads:

```sh
pwsh -NoProfile -File packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/screen-candidate -Configuration Staging
pwsh -NoProfile -File packaging/VerifyDCursesPackage.ps1 -ArtifactDirectory artifacts/screen-candidate -Configuration Staging
```

The synthetic host alone uses transitive TermInfo types to construct test descriptions and implement the legacy control-provider fixture. The controlled renderer and sample frame routine use Terminal types only. The verifier restores the candidate package in a temporary directory and keeps its existing source/reference checks on the controlled renderer.
