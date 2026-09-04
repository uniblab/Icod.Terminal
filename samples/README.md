# Icod.Terminal Samples

The sample projects are repository consumers built through project references.
The release package itself is validated separately by `tools/package-smoke`,
`tools/package-title-smoke`, `tools/package-location-smoke`,
`tools/package-hyperlink-smoke`, and `tools/package-clipboard-smoke`. The focused
package consumers use only the freshly produced NuGet artifact and run under
`net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.Clipboard.Sample

`Icod.Terminal.Clipboard.Sample` is the focused 0.7 OSC 52 clipboard/selection demonstration.

Run it in an interactive terminal with optional explicit text to write:

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -f net10.0 -- "copied text"
```

The sample explicitly writes strict UTF-8 text to the ordinary terminal clipboard:

```csharp
await session.WriteClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	text
);
```

It then explicitly requests the same selection with a short timeout:

```csharp
byte[] payload = await session.ReadClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	TimeSpan.FromMilliseconds( 750 )
);
```

The sample deliberately demonstrates the security boundary rather than hiding it:

- opening the session does not read clipboard data;
- every read is an explicit API call with a caller-visible timeout;
- the query returns bytes rather than assuming a text encoding;
- the sample chooses UTF-8 only when displaying its own demonstration result;
- write completion proves protocol emission, not terminal-side acceptance;
- read timeout does not prove lack of OSC 52 support because terminal policy may disable clipboard queries;
- no OS-native clipboard API, shell utility, automatic synchronization, or background monitoring is involved.

The project targets `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.Sample

`Icod.Terminal.Sample` is the intentionally minimal live-session example. It
opens the process terminal, requests cbreak/no-echo input policy, reports the
selected terminal identity and current dimensions, writes through
`TerminalSession`, and restores the captured session state on disposal.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

## Icod.Terminal.Hyperlink.Sample

`Icod.Terminal.Hyperlink.Sample` is the focused 0.6 OSC 8 hyperlink demonstration.
It requires the target URI and visible link text to be supplied explicitly; an
optional third argument supplies the OSC 8 `id` value.

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

The sample demonstrates both bounded output and explicit scoped state:

```csharp
await session.WriteHyperlinkAsync(
	"example link",
	"https://example.com/",
	"example-1"
);

await using TerminalHyperlinkLease hyperlink =
	await session.AcquireHyperlinkAsync(
		"https://example.com/",
		"example-1"
	);

await session.WriteTextAsync( "linked text" );
```

It also demonstrates one nested scope so strict-LIFO restoration is visible.
Active logical hyperlink scopes close physically before managed suspension and
re-enter after successful terminal/session restoration.

The project targets `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.Title.Sample

`Icod.Terminal.Title.Sample` is the focused 0.4 OSC title demonstration.

```csharp
await session.SetTitleAsync( "Icod.Terminal — OSC 0" );
await session.SetIconNameAsync( "Icod.Terminal icon" );
await session.SetWindowTitleAsync( "Icod.Terminal — OSC 2" );
```

Successful completion proves emission only; the sample does not query or restore a prior title.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

## Icod.Terminal.Location.Sample

`Icod.Terminal.Location.Sample` is the focused 0.5 OSC 7 current-location demonstration.
It requires callers to supply path grammar and absolute path explicitly and never
publishes `Environment.CurrentDirectory` automatically.

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows C:\Development\Icod

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- unc \\server\share\project
```

An optional third argument supplies an explicit authority for POSIX or Windows-drive paths.

## Icod.Terminal.RichInput.Sample

`Icod.Terminal.RichInput.Sample` is the interactive 0.2 event inspector. It requests
one reversible compound input-protocol lease for bracketed paste, focus reporting,
and button mouse tracking when supported.

All activity is consumed from `TerminalSession.ReadEventAsync`. Bracketed paste is
shown as separate Begin, bounded Data, and End events.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

## Icod.Terminal.Query.Sample

`Icod.Terminal.Query.Sample` is the explicit 0.3 active-query demonstration.
Opening its `TerminalSession` remains passive; the sample explicitly issues Primary
DA, Secondary DA, DSR, CPR, DECRQSS SGR, and XTGETTCAP `TN` operations with short
caller-visible deadlines before returning to ordinary input/lifecycle consumption.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```
