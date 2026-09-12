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
| Create/update/dispose terminal-resident raster ownership | `Icod.Terminal.PersistentRaster.Sample` |
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

Demonstrates the 1.11 persistent-raster ownership model using semantic APIs only.

```text
dotnet run --project samples/Icod.Terminal.PersistentRaster.Sample/Icod.Terminal.PersistentRaster.Sample.csproj -f net10.0
```

The sample:

1. explicitly verifies `TerminalCapability.PersistentRasterGraphics`;
2. creates a `TerminalRasterImage` in memory;
3. creates an opaque `TerminalRasterResource`;
4. creates a placement with a cell-column extent;
5. updates the same placement at the current cursor;
6. uses `await using` so placement/resource cleanup is deterministic.

It does not mention Kitty, Sixel, image ids, image numbers, placement ids, or terminal brand. It also does not imply that resources are replayed after lifecycle invalidation.

`packaging/VerifyPersistentRasterSample.ps1` enforces those backend-neutral source rules and builds the sample on every supported TFM.

See `docs/Persistent-Raster-Ownership.md` for the permanent ownership contract.

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
    -> Icod.Terminal.RasterGraphics.Sample          (ephemeral display)
    -> Icod.Terminal.PersistentRaster.Sample        (terminal-resident ownership)
```

Higher-level full-screen applications normally consume these contracts through `Icod.DCurses` rather than reimplementing cells, windows, layout, or refresh policy directly.
