# Icod.Terminal Samples

The sample projects are repository consumers built through project references. Release packages are validated separately by the package-verification harnesses under `tools/`; those consumers restore only the freshly produced NuGet artifact and run on `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.SemanticPrompt.Sample

`Icod.Terminal.SemanticPrompt.Sample` is the focused 0.12 OSC 133 semantic-prompt demonstration.

```text
dotnet run --project samples/Icod.Terminal.SemanticPrompt.Sample/Icod.Terminal.SemanticPrompt.Sample.csproj -f net10.0
```

It demonstrates the stable public surface:

```csharp
await session.BeginPromptAsync();
await session.BeginCommandInputAsync();
await session.BeginCommandOutputAsync();
await session.FinishCommandAsync( 0 );

await session.BeginPromptAsync();
await session.BeginCommandInputAsync();
await session.AbortCommandAsync();
```

`FinishCommandAsync( 0 )` emits explicit successful completion and is distinct from `AbortCommandAsync()`, which emits bare OSC 133 `D` with no status.

The sample interleaves normal `WriteTextAsync(...)` output with markers. It deliberately performs no shell or terminal-emulator detection and does not claim OSC 133 support merely because the write succeeds.

## Icod.Terminal.PointerShape.Sample

Focused 0.11 OSC 22 pointer-shape demonstration covering explicit set/reset, scoped ownership, nested restoration, and bounded Kitty-compatible pointer queries.

```text
dotnet run --project samples/Icod.Terminal.PointerShape.Sample/Icod.Terminal.PointerShape.Sample.csproj -f net10.0
```

## Icod.Terminal.Progress.Sample

Focused 0.10 OSC 9;4 progress demonstration covering determinate, indeterminate, attention, and scoped ownership.

```text
dotnet run --project samples/Icod.Terminal.Progress.Sample/Icod.Terminal.Progress.Sample.csproj -f net10.0
```

## Icod.Terminal.SynchronizedOutput.Sample

Focused 0.9 DEC private mode 2026 demonstration.

```text
dotnet run --project samples/Icod.Terminal.SynchronizedOutput.Sample/Icod.Terminal.SynchronizedOutput.Sample.csproj -f net10.0
```

## Icod.Terminal.CursorStyle.Sample

Focused 0.8 DECSCUSR cursor-style observation and truthful scoped-restoration demonstration.

```text
dotnet run --project samples/Icod.Terminal.CursorStyle.Sample/Icod.Terminal.CursorStyle.Sample.csproj -f net10.0 -- SteadyUnderline
```

## Icod.Terminal.Clipboard.Sample

Focused 0.7 OSC 52 clipboard/selection demonstration.

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -f net10.0 -- "copied text"
```

## Icod.Terminal.Hyperlink.Sample

Focused 0.6 OSC 8 hyperlink demonstration.

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

## Icod.Terminal.Location.Sample

Focused 0.5 OSC 7 current-location demonstration.

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src
```

## Icod.Terminal.Title.Sample

Focused 0.4 OSC 0/1/2 title demonstration.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

## Icod.Terminal.Query.Sample

Explicit active-query demonstration for Primary/Secondary DA, DSR, CPR, DECRQSS, and XTGETTCAP.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

## Icod.Terminal.RichInput.Sample

Interactive rich-input event inspector using reversible bracketed-paste, focus, and mouse protocol ownership when available.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

## Icod.Terminal.Sample

Minimal live-session example covering terminal identity, dimensions, application text, and captured-state restoration.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

All sample projects target `net8.0`, `net9.0`, and `net10.0`.
