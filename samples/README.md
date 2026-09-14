# Icod.Terminal Samples

The sample projects are small, focused repository consumers built through project references. They demonstrate the supported 1.x usage model. Fresh package-only compatibility is validated separately by consumers under `tools/`.

All samples target `net8.0`, `net9.0`, and `net10.0`.

## At a glance

| Goal | Sample |
| --- | --- |
| Open a session and read an event | `Icod.Terminal.Sample` |
| Inspect rich input, lifecycle, and semantic events | `Icod.Terminal.RichInput.Sample` |
| Run bounded terminal queries | `Icod.Terminal.Query.Sample` |
| Plan from semantic capability knowledge | `Icod.Terminal.CapabilityPlanning.Sample` |
| Observe or temporarily own terminal colors | `Icod.Terminal.Color.Sample` |
| Display a backend-neutral ephemeral raster | `Icod.Terminal.RasterGraphics.Sample` |
| Create/update/dispose terminal-resident raster ownership with source crops, z-order, and relative parent/child placement ownership | [`Icod.Terminal.PersistentRaster.Sample`](Icod.Terminal.PersistentRaster.Sample/README.md) |
| Combine TermInfo lifecycle and advanced-placement planning with live Terminal execution | `Icod.Terminal.TermInfoPersistentRaster.Sample` |
| Own cursor style, synchronized output, progress, or pointer shape | focused state samples |
| Publish title/location/prompt/shell metadata | focused metadata samples |
| Emit notifications and observe interactive semantic events | `Icod.Terminal.Notification.Sample` |
| Emit hyperlinks or clipboard operations | focused output samples |

## Sample rules

The examples follow the permanent 1.x contracts:

- a live `TerminalSession` owns the authoritative input reader;
- ordinary application and terminal-control output uses session-managed APIs;
- `TerminalSession.Output` is an advanced borrowed transport, not the normal application-output path;
- scoped terminal state uses `await using` / `DisposeAsync()` for deterministic cleanup;
- exact restoration is claimed only when the library first observed or captured a truthful baseline;
- persistent raster identities are opaque and generation-scoped rather than exactly restorable state;
- persistent samples do not branch on Kitty/Sixel/backend ids and do not teach hidden replay;
- metadata publication is explicit because paths, user/host identities, shell metadata, clipboard contents, notifications, command lines, hyperlinks, and raster content may disclose information outside the application;
- event-loop samples remain nonfatal when a later compatible 1.x release introduces an unfamiliar outer event kind.

## Start here

### `Icod.Terminal.Sample`

Minimal session construction, endpoint/identity observation, application text, one timed event read, and disposal-driven restoration.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

### `Icod.Terminal.RichInput.Sample`

Interactive inspector for text, keys, bracketed paste, focus, mouse, lifecycle, unsolicited semantic events, and negotiated modern keyboard reporting.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

### `Icod.Terminal.Query.Sample`

Demonstrates explicit bounded terminal queries through the same authoritative session stream used for application input and semantic/lifecycle events.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

Timeout is not treated as proof that a terminal lacks support.

### `Icod.Terminal.CapabilityPlanning.Sample`

Demonstrates protocol-neutral capability inspection and optional explicit verification.

```text
dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample/Icod.Terminal.CapabilityPlanning.Sample.csproj -f net10.0

dotnet run --project samples/Icod.Terminal.CapabilityPlanning.Sample/Icod.Terminal.CapabilityPlanning.Sample.csproj -f net10.0 -- --verify
```

The sample does not inspect terminal brand, `TERM`, protocol family, backend identity, or `Icod.TermInfo` provenance.

## Raster graphics

### `Icod.Terminal.RasterGraphics.Sample`

Generates a small RGB24 gradient in memory and sends it through the backend-neutral ephemeral `DisplayRasterAsync(...)` API.

```text
dotnet run --project samples/Icod.Terminal.RasterGraphics.Sample/Icod.Terminal.RasterGraphics.Sample.csproj -f net10.0
```

The normal evidence-driven router may use verified Kitty Graphics or verified Sixel internally. The sample performs no backend selection and emits no raw DCS/APC traffic.

`packaging/VerifyRasterGraphicsSample.ps1` builds the sample on every supported TFM.

### `Icod.Terminal.PersistentRaster.Sample`

Demonstrates the persistent-raster ownership model using semantic APIs only, including 1.12 source-pixel cropping and signed z-order plus 1.13 immutable-parent relative placement ownership.

```text
dotnet run --project samples/Icod.Terminal.PersistentRaster.Sample/Icod.Terminal.PersistentRaster.Sample.csproj -f net10.0
```

The sample:

1. explicitly verifies `TerminalCapability.PersistentRasterGraphics`;
2. creates a first opaque `TerminalRasterResource` and an ordinary current-cursor parent placement;
3. creates a second independently owned raster resource;
4. creates a relative child placement from the second resource using signed terminal-cell offsets from the first placement plus crop/extents/z-order geometry;
5. calls ordinary `UpdateAsync(...)` on the relative child to replace common geometry while preserving its immutable parent and last acknowledged relative offsets;
6. calls `UpdateRelativeAsync(...)` to replace signed offsets and common geometry without reparenting;
7. explicitly disposes the parent placement, demonstrating descendant-placement cleanup while the child's raster resource remains independently owned;
8. creates a fresh ordinary placement from that surviving child resource, proving resource ownership is independent from relative-placement lifetime;
9. uses `await using` so all remaining placement/resource cleanup is deterministic and repeated disposal is harmless.

`TerminalRasterSourceRectangle` coordinates are measured in source pixels and select which part of the owned raster resource participates in one placement. `ZIndex` expresses signed stacking order. Relative `columnOffset` / `rowOffset` values are measured in terminal cells, parentage is immutable, and the portable relative-depth ceiling is 8. Resource ownership and parent-placement lifetime are separate axes: deleting a parent removes its relative-placement subtree but does not by itself dispose descendant raster resources.

`UpdateAsync(...)` preserves the placement's established positioning mode. On a relative placement it retains the immutable parent and acknowledged offsets while replacing common crop/extents/z-order geometry. `UpdateRelativeAsync(...)` changes the offsets as well as common geometry but never reparents the placement.

These options do not turn `Icod.Terminal` into a scene-layout engine. Version 1.13 does not add reparenting, absolute screen-coordinate layout, Unicode placeholder placements, animation/frame ownership, or automatic composition policy.

The sample does not mention Kitty, Sixel, image ids, image numbers, placement ids, or terminal brand. It also does not imply that resources are replayed after lifecycle invalidation.

`packaging/VerifyPersistentRasterSample.ps1` enforces those backend-neutral source rules and builds the sample on every supported TFM.

See `docs/Persistent-Raster-Ownership.md` for the permanent ownership contract.

### `Icod.Terminal.TermInfoPersistentRaster.Sample`

The loose-coupling pattern introduced in 1.11.1 remains intact, but the current executable sample now consumes `Icod.TermInfo.Inspection 1.12.0` so it can demonstrate both persistent lifecycle planning and the additive 1.12 advanced-placement planner.

```text
dotnet run --project samples/Icod.Terminal.TermInfoPersistentRaster.Sample/Icod.Terminal.TermInfoPersistentRaster.Sample.csproj -f net10.0
```

The sample first inspects `session.Terminal`, builds the semantic persistent-lifecycle plan, and asks Terminal for live verification only when that lifecycle plan is indeterminate and the endpoint is available. Only a conclusive Terminal live result becomes caller-owned `Verified` lifecycle evidence before reclassification and replanning.

After lifecycle success, the sample requires both source-rectangle and signed-z-order semantics through `PersistentRasterPlacementRequest`. Static Inspection evidence is planned first. If those advanced semantics are merely unknown, the application adds explicit caller-owned `Declared` evidence for the Icod.Terminal 1.13 placement contract and replans before executing concrete crop/z-order values through Terminal.

This distinction is intentional: `PersistentRasterGraphics` is the coarse live Terminal capability and is not misrepresented as a separate source-rectangle or z-order probe. TermInfo owns semantic evidence/classification/planning; Terminal and the application own concrete geometry values, acknowledgements, and execution. Icod.TermInfo 1.12 also does not plan the relative-parent graph added by Terminal 1.13; relative placement ownership remains a Terminal runtime concern.

`Icod.TermInfo.Inspection` remains a sample-only dependency. The production `Icod.Terminal` package does not acquire an Inspection or Source dependency, and the sample does not expose raw graphics commands, terminal-brand branches, backend ids, or protocol-private numeric identities.

See `Icod.Terminal.TermInfoPersistentRaster.Sample/README.md` for the complete responsibility boundary and failure behavior.

## Reversible state and color

### `Icod.Terminal.Color.Sample`

Typed palette/dynamic-color observation and optional exact-restoration ownership.

```text
dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0

dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0 -- --mutate
```

### `Icod.Terminal.CursorStyle.Sample`

Typed cursor-style observation, mutation, and scoped restoration.

```text
dotnet run --project samples/Icod.Terminal.CursorStyle.Sample/Icod.Terminal.CursorStyle.Sample.csproj -f net10.0 -- SteadyUnderline
```

### `Icod.Terminal.SynchronizedOutput.Sample`

Scoped DEC private mode 2026 synchronized-output ownership.

```text
dotnet run --project samples/Icod.Terminal.SynchronizedOutput.Sample/Icod.Terminal.SynchronizedOutput.Sample.csproj -f net10.0
```

### `Icod.Terminal.Progress.Sample`

Scoped terminal progress, including determinate, indeterminate, attention/error, nesting, and cleanup behavior.

```text
dotnet run --project samples/Icod.Terminal.Progress.Sample/Icod.Terminal.Progress.Sample.csproj -f net10.0
```

### `Icod.Terminal.PointerShape.Sample`

Pointer-shape mutation, scoped ownership, reset, and bounded pointer queries.

```text
dotnet run --project samples/Icod.Terminal.PointerShape.Sample/Icod.Terminal.PointerShape.Sample.csproj -f net10.0
```

## Semantic metadata

### `Icod.Terminal.Title.Sample`

Semantic icon/window title operations.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

### `Icod.Terminal.Location.Sample`

Portable current-location publication plus the explicit Windows compatibility form.

### `Icod.Terminal.SemanticPrompt.Sample`

Portable typed prompt/command-region metadata.

### `Icod.Terminal.VsCodeShellIntegration.Sample`

Typed VS Code shell-integration metadata. Potentially sensitive metadata is supplied explicitly.

### `Icod.Terminal.ITerm2ShellIntegration.Sample`

Typed iTerm2 shell-integration and semantic-history metadata. User-variable Base64 is protocol framing, not confidentiality.

## Notifications and interactive output

### `Icod.Terminal.Notification.Sample`

Demonstrates desktop-notification output plus explicit opt-in interaction reporting through the unified event stream.

```text
dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- "Build complete"
```

Notification identifiers and button reports are validated but unauthenticated terminal-controlled input.

### `Icod.Terminal.Hyperlink.Sample`

Bounded hyperlink output and scoped hyperlink ownership.

### `Icod.Terminal.Clipboard.Sample`

Explicit clipboard/selection writes and privacy-sensitive reads. Clipboard reads are never automatic.

## Choosing a sample

For a general terminal-aware application:

```text
Icod.Terminal.Sample
    -> Icod.Terminal.RichInput.Sample
    -> Icod.Terminal.CapabilityPlanning.Sample
    -> Icod.Terminal.Query.Sample
    -> one focused feature sample
```

For graphics:

```text
Icod.Terminal.CapabilityPlanning.Sample
    -> Icod.Terminal.RasterGraphics.Sample             (ephemeral display)
    -> Icod.Terminal.PersistentRaster.Sample           (terminal-resident ownership)
    -> Icod.Terminal.TermInfoPersistentRaster.Sample   (TermInfo lifecycle/placement planning + Terminal execution)
```

Higher-level full-screen applications normally consume these contracts through `Icod.DCurses` rather than reimplementing cells, windows, layout, or refresh policy directly.
