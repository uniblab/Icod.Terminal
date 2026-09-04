# Icod.Terminal Samples

The sample projects are repository consumers built through project references.
The release package itself is validated separately by `tools/package-smoke`,
`tools/package-title-smoke`, `tools/package-location-smoke`, and
`tools/package-hyperlink-smoke`. The OSC 8 package smoke consumes only the
freshly produced NuGet package and runs on `net8.0`, `net9.0`, and `net10.0`.

## Icod.Terminal.Sample

`Icod.Terminal.Sample` is the intentionally minimal live-session example. It
opens the process terminal, requests cbreak/no-echo input policy, reports the
selected terminal identity and current dimensions, writes through
`TerminalSession`, and restores the captured session state on disposal.

Run it with a selected target framework, for example:

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

## Icod.Terminal.Hyperlink.Sample

`Icod.Terminal.Hyperlink.Sample` is the focused 0.6 OSC 8 hyperlink demonstration.
It requires the target URI and visible link text to be supplied explicitly; an
optional third argument supplies the OSC 8 `id` value.

For example:

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

The sample demonstrates both ordinary bounded output:

```csharp
await session.WriteHyperlinkAsync(
	"example link",
	"https://example.com/",
	"example-1"
);
```

and explicit scoped state:

```csharp
await using TerminalHyperlinkLease hyperlink =
	await session.AcquireHyperlinkAsync(
		"https://example.com/",
		"example-1"
	);

await session.WriteTextAsync( "linked text" );
```

It also demonstrates one nested scope so the strict-LIFO restoration model is
visible: entering the inner scope changes the active hyperlink, disposing the
inner scope restores the outer hyperlink, and disposing the outer scope emits the
canonical OSC 8 close frame.

The sample deliberately does not imply more than the protocol contract provides:

- the URI is caller-supplied absolute, already URI-encoded ASCII text;
- successful completion proves bytes were emitted, not that the terminal rendered
  or activated the hyperlink;
- `Icod.Terminal` does not open, fetch, resolve, or validate reachability of the URI;
- applications which accept untrusted URI targets remain responsible for their own
  scheme/trust policy;
- no generic raw OSC API or arbitrary OSC 8 parameter dictionary is exposed;
- session disposal remains the final cleanup authority for an outstanding
  library-owned hyperlink scope.

The project also targets `net8.0` and `net9.0`.

## Icod.Terminal.Title.Sample

`Icod.Terminal.Title.Sample` is the focused 0.4 OSC title demonstration.

It emits the three semantic public title operations in sequence:

```csharp
await session.SetTitleAsync( "Icod.Terminal — OSC 0" );
await session.SetIconNameAsync( "Icod.Terminal icon" );
await session.SetWindowTitleAsync( "Icod.Terminal — OSC 2" );
```

The sample deliberately teaches the 0.4 ownership boundary rather than implying
stronger behavior than the protocol provides:

- successful completion means the complete OSC frame was written to the session
  output; it does not prove that the terminal emulator applied the title;
- the previous terminal title is not queried;
- session disposal therefore does **not** restore the previous title;
- callers should use the semantic operations rather than synthesizing raw OSC
  0/1/2 frames themselves.

Run it in a real interactive terminal where title changes are visible:

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

The project also targets `net8.0` and `net9.0`.

## Icod.Terminal.Location.Sample

`Icod.Terminal.Location.Sample` is the focused 0.5 OSC 7 current-location demonstration.

The sample requires the caller to provide the native path grammar and absolute path explicitly. This is intentional: current-location publication can disclose directory and host information, so the sample does not read or publish `Environment.CurrentDirectory` automatically.

Examples:

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows C:\Development\Icod

dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- unc \\server\share\project
```

An optional third argument supplies an explicit authority for POSIX or Windows-drive paths:

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /srv/project example.com
```

The sample teaches the 0.5 contract:

- publication occurs only because the caller explicitly invoked the semantic operation;
- the native path is converted to a canonical `file:` URI by `Icod.Terminal`;
- URI escaping is performed by the library rather than by the caller;
- C0, DEL, and C1 control characters in native paths are rejected rather than percent-encoded;
- explicit authorities are restricted to unscoped host forms; IPv6 zone identifiers and literal `%` authority text are rejected in 0.5;
- successful completion means the complete OSC 7 frame was written, not that the terminal necessarily used the location;
- disposal does not republish or restore location metadata.

The project also targets `net8.0` and `net9.0`.

## Icod.Terminal.RichInput.Sample

`Icod.Terminal.RichInput.Sample` is the interactive 0.2 event inspector.

It requests one reversible compound input-protocol lease for:

- bracketed paste;
- focus reporting;
- button mouse tracking.

If the selected terminal does not advertise that complete protocol contract, the
sample reports the controlled `TerminalControlResult` and continues with the
ordinary event loop.

When reporting is available, try:

- typing ordinary text;
- arrows, Home/End, Page Up/Page Down, Insert/Delete, and function keys;
- Shift/Alt/Control modified keys supported by the terminal profile;
- clicking mouse buttons or using the wheel;
- moving focus away from and back to the terminal;
- pasting text.

All activity is consumed from `TerminalSession.ReadEventAsync`. Mouse coordinates
are displayed as zero-based terminal-cell coordinates. Bracketed paste is shown
as separate Begin, bounded Data, and End events so the framing contract remains
visible.

Exit with `q`, `Q`, Escape, end-of-input, an interrupt, or a termination event.
The sample releases its rich-input protocol lease before disposing the session.

Run it in a real interactive terminal:

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

The project also targets `net8.0` and `net9.0`.

## Icod.Terminal.Query.Sample

`Icod.Terminal.Query.Sample` is the explicit 0.3 active-query demonstration.

Opening its `TerminalSession` remains passive. The sample then deliberately
requests presentation and rich-input leases when available and explicitly issues
Primary DA, Secondary DA, DSR, CPR, DECRQSS SGR, and XTGETTCAP `TN` operations.

Each probe has a short caller-visible deadline so a terminal which does not
implement a particular query family is reported without hanging the sample.
After probing, the sample returns to `ReadEventAsync` for one ordinary
input/lifecycle event.

Run it in a real interactive terminal:

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

The project also targets `net8.0` and `net9.0`.
