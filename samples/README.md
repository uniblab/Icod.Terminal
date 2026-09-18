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
| Create/update/observe/dispose terminal-resident raster ownership with source crops, z-order, and relative parent/child placement ownership | [`Icod.Terminal.PersistentRaster.Sample`](Icod.Terminal.PersistentRaster.Sample/README.md) |
| Render semantic virtual-raster cells with caller-owned cursor/layout control | [`Icod.Terminal.RasterPlaceholder.Sample`](Icod.Terminal.RasterPlaceholder.Sample/README.md) |
| Stream and control terminal-resident animation frames through backend-neutral semantics | [`Icod.Terminal.RasterAnimation.Sample`](Icod.Terminal.RasterAnimation.Sample/README.md) |
| Combine TermInfo lifecycle, advanced-placement, and explicit raster-backend planning with live Terminal execution | `Icod.Terminal.TermInfoPersistentRaster.Sample` |
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
- persistent `OwnershipState` snapshots report Terminal's local certainty and do not pretend to authenticate remote terminal existence;
- placeholder samples keep cursor position, clipping, redraw order, and screen layout in caller ownership;
- ordinary Terminal raster samples do not branch on Kitty/Sixel/backend ids and do not teach hidden replay; the optional TermInfo integration sample may use Inspection's semantic backend identities only for explicit caller-owned planning, never raw protocol dispatch;
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

Demonstrates the persistent-raster ownership model using semantic APIs only: 1.12 source-pixel cropping and signed z-order, 1.13 immutable-parent relative placement ownership, and 1.14 side-effect-free ownership-state observation.

```text
dotnet run --project samples/Icod.Terminal.PersistentRaster.Sample/Icod.Terminal.PersistentRaster.Sample.csproj -f net10.0
```

The sample:

1. explicitly verifies `TerminalCapability.PersistentRasterGraphics`;
2. creates a first opaque `TerminalRasterResource` and an ordinary current-cursor parent placement;
3. creates a second independently owned raster resource;
4. creates a relative child placement from the second resource using signed terminal-cell offsets from the first placement plus crop/extents/z-order geometry;
5. observes the resources and placements as `Current / None` without issuing extra terminal traffic;
6. calls ordinary `UpdateAsync(...)` on the relative child to replace common geometry while preserving its immutable parent and last acknowledged relative offsets;
7. calls `UpdateRelativeAsync(...)` to replace signed offsets and common geometry without reparenting;
8. explicitly disposes the parent placement and observes the parent wrapper as `Disposed / ExplicitDisposal`, the relative child as `Released / AncestorReleased`, and Resource B as still `Current / None`;
9. creates a fresh ordinary placement from that surviving child resource and observes it as current, proving resource ownership is independent from relative-placement lifetime;
10. uses `await using` so all remaining placement/resource cleanup is deterministic and repeated disposal is harmless.

`TerminalRasterSourceRectangle` coordinates are measured in source pixels and select which part of the owned raster resource participates in one placement. `ZIndex` expresses signed stacking order. Relative `columnOffset` / `rowOffset` values are measured in terminal cells, parentage is immutable, and the portable relative-depth ceiling is 8. Resource ownership and parent-placement lifetime are separate axes: deleting a parent removes its relative-placement subtree but does not by itself dispose descendant raster resources.

`UpdateAsync(...)` preserves the placement's established positioning mode. On a relative placement it retains the immutable parent and acknowledged offsets while replacing common crop/extents/z-order geometry. `UpdateRelativeAsync(...)` changes the offsets as well as common geometry but never reparents the placement.

`OwnershipState` is a synchronous immutable snapshot. `Current` means Terminal still has local ownership certainty; it is not proof that the terminal has independently retained the object. `Stale` records lost terminal certainty, `Released` records placement lifetime ended by another owner, and `Disposed` records explicit disposal of that public wrapper. The reason travels in the same snapshot so status/reason cannot tear across concurrent reads.

These options do not turn `Icod.Terminal` into a scene-layout engine. Version 1.14 does not add reparenting, passive terminal-side existence probes, absolute screen-coordinate layout, Unicode placeholder placements, animation/frame ownership, automatic composition policy, or replay/re-upload caching.

The sample does not mention Kitty, Sixel, image ids, image numbers, placement ids, session generation ids, or terminal brand. It also does not imply that resources are replayed after lifecycle invalidation.

`packaging/VerifyPersistentRasterSample.ps1` enforces those backend-neutral source rules and builds the sample on every supported TFM.

See `docs/Persistent-Raster-Ownership.md` for the permanent ownership contract.

### `Icod.Terminal.RasterPlaceholder.Sample`

Demonstrates the 1.15 virtual-placement and semantic placeholder-cell abstraction while keeping screen layout in caller ownership.

```text
dotnet run --project samples/Icod.Terminal.RasterPlaceholder.Sample/Icod.Terminal.RasterPlaceholder.Sample.csproj -f net10.0
```

The sample verifies persistent raster ownership, inspects `UnicodeRasterPlaceholders` without inventing a probe, creates an opaque resource and placeholder, then uses `GetCell(...)`, typed single/bulk placeholder output, and ordinary TermInfo `CursorAddress` expansion to render a complete 4x2 grid. It then performs a sparse one-cell redraw, emits a deliberately reordered cell sequence, and creates a physical placement relative to the virtual placeholder.

The distinction is intentional: placeholder row/column coordinates identify cells inside the virtual raster, while terminal screen coordinates, cursor movement, clipping, redraw ordering, and layout remain application responsibilities. Successful placeholder creation publishes live semantic evidence for that transaction; the sample does not fabricate stronger support knowledge beforehand.

`packaging/VerifyRasterPlaceholderSample.ps1` rejects protocol-private identity/backend/raw-control literals and builds the sample on every supported TFM.

See [`Icod.Terminal.RasterPlaceholder.Sample/README.md`](Icod.Terminal.RasterPlaceholder.Sample/README.md) for the full responsibility boundary.

### `Icod.Terminal.TermInfoPersistentRaster.Sample`

The loose-coupling pattern introduced in 1.11.1 remains intact. The current executable sample consumes `Icod.TermInfo.Inspection 1.15.0`, continues to demonstrate persistent lifecycle planning and the advanced-placement planner introduced in TermInfo 1.12, and exercises the advisory raster-backend planner introduced in TermInfo 1.14.

```text
dotnet run --project samples/Icod.Terminal.TermInfoPersistentRaster.Sample/Icod.Terminal.TermInfoPersistentRaster.Sample.csproj -f net10.0
```

The sample first inspects `session.Terminal`, builds the semantic persistent-lifecycle plan, and asks Terminal for live verification only when that lifecycle plan is indeterminate and the endpoint is available. Only a conclusive Terminal live result becomes caller-owned `Verified` lifecycle evidence before reclassification and replanning.

After lifecycle success, the sample requires both source-rectangle and signed-z-order semantics through `PersistentRasterPlacementRequest`. Static Inspection evidence is planned first. If those advanced semantics are merely unknown, the application adds explicit caller-owned `Declared` evidence for the Icod.Terminal placement contract and replans before execution.

The 1.14 step then keeps separate backend contexts. Sixel retains its own static availability plus the original unstrengthened lifecycle/placement profiles. A conclusive live `PersistentRasterGraphics` result is caller-mapped to Kitty Graphics availability because Icod.Terminal's reviewed persistent-raster route is Kitty-based, and only the Kitty candidate receives the strengthened lifecycle/placement evidence. The sample plans first without ranking and then supplies explicit Kitty-first caller preference. TermInfo remains advisory; Icod.Terminal still owns actual routing and protocol commitment.

This distinction is intentional: ordinary `RasterGraphics` does not identify Kitty versus Sixel, `PersistentRasterGraphics` is not misrepresented as a source-rectangle or z-order probe, and `UnicodeRasterPlaceholders` is not fed into TermInfo 1.14 as persistent lifecycle/placement evidence. The current TermInfo 1.14 integration does not plan Terminal's relative-placement or virtual-placeholder graphs.

`Icod.TermInfo.Inspection` remains a sample-only dependency. The production `Icod.Terminal` package does not acquire an Inspection or Source dependency. The sample uses Inspection's semantic Sixel/Kitty backend identities only for explicit application planning and never exposes raw graphics commands, terminal-brand heuristics, caller-supplied protocol-private numeric identities, or direct protocol dispatch.

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
    -> Icod.Terminal.PersistentRaster.Sample           (terminal-resident ownership + lifecycle observation)
    -> Icod.Terminal.RasterPlaceholder.Sample          (virtual placement + caller-owned text-grid rendering)
    -> Icod.Terminal.RasterAnimation.Sample            (terminal-driven animation + frame lifecycle)
    -> Icod.Terminal.TermInfoPersistentRaster.Sample   (TermInfo lifecycle/placement/backend planning + Terminal execution)
```

Higher-level full-screen applications normally consume these contracts through `Icod.DCurses` rather than reimplementing cells, windows, layout, or refresh policy directly.
