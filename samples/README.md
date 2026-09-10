# Icod.Terminal Samples

The sample projects are small, focused repository consumers built through project references. They demonstrate the supported 1.x usage model; package-only compatibility is validated separately by the consumers under `tools/`, which restore only the freshly packed NuGet artifact.

All samples target `net8.0`, `net9.0`, and `net10.0`.

## At a glance

| Goal | Sample |
| --- | --- |
| Open a session, inspect identity/endpoints, and read an event | `Icod.Terminal.Sample` |
| Inspect rich terminal input | `Icod.Terminal.RichInput.Sample` |
| Run bounded terminal queries | `Icod.Terminal.Query.Sample` |
| Observe or temporarily own terminal colors | `Icod.Terminal.Color.Sample` |
| Display a backend-neutral raster | `Icod.Terminal.RasterGraphics.Sample` |
| Own cursor style, synchronized output, progress, or pointer shape | focused state samples below |
| Publish title/location/prompt/shell metadata | focused metadata samples below |
| Emit notifications, hyperlinks, or clipboard operations | focused output samples below |

## Sample rules

The examples follow the permanent 1.x contracts:

- a live `TerminalSession` owns the authoritative input reader;
- ordinary application and terminal-control output uses session-managed APIs;
- `TerminalSession.Output` is an advanced borrowed transport, not the normal application-output path;
- scoped terminal state uses `await using` / `DisposeAsync()` for deterministic cleanup;
- exact restoration is claimed only when the library first observed or captured a truthful baseline;
- metadata publication is explicit because paths, user/host identities, shell metadata, clipboard contents, notifications, command lines, and hyperlinks may disclose information outside the application.

## Start here

### `Icod.Terminal.Sample`

Minimal session example covering identity selection, endpoint observations, application text, a timed event read, and disposal-driven restoration of session-owned terminal state.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

### `Icod.Terminal.RichInput.Sample`

Interactive inspector for text, keys, bracketed paste, focus, mouse, and negotiated modern keyboard reporting. Traditional keyboard decoding remains the compatibility fallback.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

### `Icod.Terminal.Query.Sample`

Demonstrates explicit bounded Primary/Secondary DA, DSR, CPR, DECRQSS, and XTGETTCAP queries through the session's single response-correlation path.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

Timeout is not treated as proof that a terminal lacks support.

## Raster graphics

### `Icod.Terminal.RasterGraphics.Sample`

Generates a small RGB24 gradient in memory and passes it to the public backend-neutral `DisplayRasterAsync(...)` operation.

```text
dotnet run --project samples/Icod.Terminal.RasterGraphics.Sample/Icod.Terminal.RasterGraphics.Sample.csproj -f net10.0
```

The sample does not select Sixel or Kitty Graphics, emit raw DCS/APC traffic, inspect terminal branding, load image files, or add an image-decoder dependency. The normal evidence-driven router may use verified Kitty Graphics or verified Sixel internally.

`packaging/VerifyRasterGraphicsSample.ps1` builds this sample on every supported TFM during repository validation.

## Queries and reversible state

### `Icod.Terminal.Color.Sample`

Typed palette/dynamic-color observation and optional exact-restoration ownership.

```text
dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0

dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0 -- --mutate
```

### `Icod.Terminal.CursorStyle.Sample`

Typed DECSCUSR cursor-style observation, mutation, and scoped restoration.

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

OSC 22 pointer-shape mutation, scoped ownership, nested fallback, terminal-policy reset, and bounded pointer queries.

```text
dotnet run --project samples/Icod.Terminal.PointerShape.Sample/Icod.Terminal.PointerShape.Sample.csproj -f net10.0
```

## Semantic metadata

### `Icod.Terminal.Title.Sample`

Semantic OSC 0/1/2 icon/window title operations.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

### `Icod.Terminal.Location.Sample`

Portable OSC 7 current-location publication plus the explicit Windows Terminal/ConEmu OSC 9;9 compatibility form.

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows-osc9 C:\work\repo
```

OSC 7 remains the preferred portable location API. The sample does not inspect the process current directory or automatically emit multiple vendor protocols.

### `Icod.Terminal.SemanticPrompt.Sample`

Portable typed OSC 133 prompt/command-region metadata.

```text
dotnet run --project samples/Icod.Terminal.SemanticPrompt.Sample/Icod.Terminal.SemanticPrompt.Sample.csproj -f net10.0
```

### `Icod.Terminal.VsCodeShellIntegration.Sample`

Typed VS Code OSC 633 rich-command metadata, current directory, prompt/input/output boundaries, explicit command-line publication, and command completion.

```text
dotnet run --project samples/Icod.Terminal.VsCodeShellIntegration.Sample/Icod.Terminal.VsCodeShellIntegration.Sample.csproj -f net10.0 -- /srv/repo "dotnet test" optional-nonce
```

All potentially sensitive metadata is supplied explicitly. The sample does not inspect process arguments, environment variables, shell history, or the process current directory. OSC 133 and OSC 7 remain the preferred portable semantic APIs where applicable.

`packaging/VerifyVsCodeShellIntegrationSample.ps1` builds this sample on every supported TFM during repository validation.

### `Icod.Terminal.ITerm2ShellIntegration.Sample`

Typed iTerm2 OSC 1337 shell-integration and semantic-history metadata.

```text
dotnet run --project samples/Icod.Terminal.ITerm2ShellIntegration.Sample/Icod.Terminal.ITerm2ShellIntegration.Sample.csproj -f net10.0 -- /srv/repo alice host.example.test bash 20 branch main
```

Add `--clear-captured-output` only when intentionally demonstrating the destructive clear operation. User-variable Base64 is protocol framing, not confidentiality.

## Notifications and interactive output

### `Icod.Terminal.Notification.Sample`

Demonstrates explicit OSC 9, OSC 777, and Kitty OSC 99 desktop-notification surfaces without terminal-brand routing. The interactive mode also demonstrates opt-in activation/button and close reporting through the same public `TerminalSession.ReadEventAsync(...)` event stream used for ordinary terminal input.

```text
dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- "Build complete"

dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- --titled "Build" "Compilation complete"

dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- --kitty "Build" "Compilation complete"

dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- --kitty-interactive build-42 "Build" "Compilation complete"
```

Interactive mode uses the caller-supplied identifier, requests activation/button and close reports, adds fixed `Acknowledge` and `Dismiss` sample buttons, and waits up to 30 seconds for a matching typed notification event. The event identifier and button number are validated but unauthenticated terminal-controlled input. A timeout does not prove that the terminal lacks support.

Successful emission does not prove that the desktop displayed a notification. Notification text may be retained by the terminal or operating environment.

### `Icod.Terminal.Hyperlink.Sample`

Bounded OSC 8 hyperlink output and scoped hyperlink ownership.

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

### `Icod.Terminal.Clipboard.Sample`

Explicit OSC 52 clipboard/selection writes and privacy-sensitive reads.

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -f net10.0 -- "copied text"
```

Clipboard reads are never automatic. Terminal-side policy may ignore or deny them, and a timeout is not permanent unsupported evidence.

## Choosing a sample

For a general terminal-aware application, a useful progression is:

```text
Icod.Terminal.Sample
    -> Icod.Terminal.RichInput.Sample
    -> Icod.Terminal.Query.Sample
    -> one focused state/output sample relevant to the application
```

Applications interested in raster output can go directly from the basic session sample to `Icod.Terminal.RasterGraphics.Sample`. Shell integrations should prefer portable semantic APIs first, then use vendor-specific samples only when intentionally targeting those protocols.

Higher-level full-screen applications normally consume these contracts through `Icod.DCurses` rather than reimplementing cells, windows, or refresh policy directly.
