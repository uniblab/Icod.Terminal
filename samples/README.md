# Icod.Terminal Samples

The sample projects are repository consumers built through project references. Release packages are validated separately by the package-verification harnesses under `tools/`; those consumers restore only the freshly produced NuGet artifact and run on `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.Notification.Sample

`Icod.Terminal.Notification.Sample` is the focused 0.16 legacy OSC 9 desktop-notification demonstration.

```text
dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- "Build complete"
```

The sample publishes only the notification text supplied explicitly on the command line:

```csharp
await session.SendNotificationAsync( message );
```

Successful completion means the complete OSC 9 request was emitted; it does not prove the desktop displayed a notification.

Notification text can appear in desktop notification history, lock screens, screen sharing, remote/multiplexed sessions, or terminal logs. Do not pass credentials, bearer tokens, customer data, private paths, or other sensitive values unless that disclosure is appropriate.

The 0.16 implementation rejects malformed Unicode and every C0/DEL/C1 control character, uses strict UTF-8 and ST termination, and rejects payloads larger than 4,096 OSC bytes before waiting for terminal output.

The focused sample is built separately by `packaging/VerifyNotificationSample.ps1` on `net8.0`, `net9.0`, and `net10.0` as part of PR, distribution, and tagged-release validation.

## Icod.Terminal.Color.Sample

`Icod.Terminal.Color.Sample` is the focused 0.14 terminal-color demonstration. It covers the 0.13 observation API and the new lifecycle-safe scoped ownership contract, and is included in the root solution so the normal repository build matrix compiles it on every supported configuration.

```text
dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0
```

By default it performs observation only:

```csharp
TerminalColor palette = await session.QueryPaletteColorAsync(
	1,
	timeout
);

TerminalColor foreground = await session.QueryDynamicColorAsync(
	TerminalDynamicColor.DefaultForeground,
	timeout
);
```

The returned values preserve 16-bit RGB channel precision. Timeout, malformed correlated replies, and unavailable active-query conditions are reported as sample output rather than terminating with an unhandled exception.

Scoped mutation is deliberately opt-in:

```text
dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0 -- --mutate
```

That mode acquires a palette-color lease and a text-cursor dynamic-color lease:

```csharp
await using TerminalPaletteColorLease paletteLease =
	await session.AcquirePaletteColorAsync(
		1,
		TerminalColor.FromRgb8( 255, 64, 64 ),
		timeout
	);

await using TerminalDynamicColorLease cursorLease =
	await session.AcquireDynamicColorAsync(
		TerminalDynamicColor.TextCursor,
		TerminalColor.FromRgb8( 64, 255, 64 ),
		timeout
	);
```

The first owner observes the exact external baseline before mutation. Leaving the scope restores those observed baselines explicitly. OSC 104 and OSC 112 remain terminal-policy reset APIs and are intentionally not used as restoration.

All terminal input and output in the sample goes through `TerminalSession`; it does not mix `Console.ReadLine()` with the session-owned input/query path.

## Icod.Terminal.SemanticPrompt.Sample

`Icod.Terminal.SemanticPrompt.Sample` is the focused 0.12/0.15 OSC 133 semantic-prompt demonstration.

```text
dotnet run --project samples/Icod.Terminal.SemanticPrompt.Sample/Icod.Terminal.SemanticPrompt.Sample.csproj -f net10.0
```

It demonstrates prompt, command-input, command-output, explicit completion, abort markers, typed extended prompt metadata, and explicit command-line metadata with ordinary application text.

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

`Icod.Terminal.Location.Sample` demonstrates both the preferred portable OSC 7 location publication and the explicit 0.16 OSC 9;9 Windows-current-directory compatibility form.

Preferred OSC 7 example:

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src
```

Explicit Windows Terminal/ConEmu compatibility example:

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows-osc9 C:\work\repo
```

OSC 7 remains the preferred/default current-location protocol. The sample does not detect terminal brand, translate WSL/Cygwin paths, inspect the process current directory, or emit both protocols automatically. Applications that deliberately need both protocols must call both public APIs themselves.

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
