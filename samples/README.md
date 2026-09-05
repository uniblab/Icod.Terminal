# Icod.Terminal Samples

The sample projects are repository consumers built through project references.
The release package itself is validated separately by the package-verification
harnesses under `tools/`. Focused package consumers use only the freshly produced
NuGet artifact and run under `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.CursorStyle.Sample

`Icod.Terminal.CursorStyle.Sample` is the focused 0.8 DECSCUSR cursor-style demonstration.

Run it in an interactive terminal. The optional argument is any `TerminalCursorStyle`
name; the default is `SteadyBar`.

```text
dotnet run --project samples/Icod.Terminal.CursorStyle.Sample/Icod.Terminal.CursorStyle.Sample.csproj -f net10.0 -- SteadyUnderline
```

The sample demonstrates explicit observation first:

```csharp
TerminalCursorStyleObservation observation =
	await session.QueryCursorStyleAsync(
		TimeSpan.FromMilliseconds( 750 )
	);
```

It then acquires a truthful scoped lease:

```csharp
await using TerminalCursorStyleLease lease =
	await session.AcquireCursorStyleAsync(
		TerminalCursorStyle.SteadyUnderline,
		TimeSpan.FromMilliseconds( 750 )
	);
```

The outermost lease observes the terminal's actual semantic cursor style before
mutation and restores that observed style on disposal. If the terminal explicitly
reports DECRQSS cursor-style observation as unsupported, the sample does not acquire
a lease and does not change cursor style. A query timeout is reported as a timeout,
not misinterpreted as proof of unsupported behavior.

Cursor style and cursor visibility are separate concepts. This sample changes
shape/blink policy only; it does not hide or show the cursor.

## Icod.Terminal.Clipboard.Sample

`Icod.Terminal.Clipboard.Sample` is the focused 0.7 OSC 52 clipboard/selection demonstration.

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -f net10.0 -- "copied text"
```

It explicitly writes strict UTF-8 text and then requests the same terminal-managed
selection with a caller-visible timeout. Opening the session does not read clipboard
data, and read timeout does not prove lack of OSC 52 support.

## Icod.Terminal.Hyperlink.Sample

`Icod.Terminal.Hyperlink.Sample` is the focused 0.6 OSC 8 hyperlink demonstration.
It requires the target URI and visible link text explicitly; an optional third
argument supplies the OSC 8 `id` value.

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

The sample demonstrates both bounded hyperlink output and strict-LIFO scoped
hyperlink restoration.

## Icod.Terminal.Location.Sample

`Icod.Terminal.Location.Sample` is the focused 0.5 OSC 7 current-location demonstration.
It requires callers to supply path grammar and absolute path explicitly and never
publishes `Environment.CurrentDirectory` automatically.

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows C:\Development\Icod

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- unc \\server\share\project
```

## Icod.Terminal.Title.Sample

`Icod.Terminal.Title.Sample` is the focused 0.4 OSC title demonstration.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

It demonstrates semantic OSC 0/1/2 operations without exposing raw OSC construction.

## Icod.Terminal.Query.Sample

`Icod.Terminal.Query.Sample` is the explicit 0.3 active-query demonstration.
Opening its `TerminalSession` remains passive; the sample explicitly issues Primary
DA, Secondary DA, DSR, CPR, DECRQSS SGR, and XTGETTCAP `TN` operations with short
caller-visible deadlines before returning to ordinary input/lifecycle consumption.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

## Icod.Terminal.RichInput.Sample

`Icod.Terminal.RichInput.Sample` is the interactive 0.2 event inspector. It requests
one reversible compound input-protocol lease for bracketed paste, focus reporting,
and button mouse tracking when supported.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

## Icod.Terminal.Sample

`Icod.Terminal.Sample` is the intentionally minimal live-session example. It opens
the process terminal, requests cbreak/no-echo input policy, reports the selected
terminal identity and current dimensions, writes through `TerminalSession`, and
restores captured state on disposal.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

All sample projects target `net8.0`, `net9.0`, and `net10.0`.
