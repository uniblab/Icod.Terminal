# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It is intended to sit between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.6.1` is the current stable maintenance release candidate. It retains the complete 0.6 OSC 8 hyperlink and lifecycle surface while updating the `Icod.TermInfo` runtime dependency to `1.10.0`.

The stable 0.6 public surface adds:

```csharp
await session.WriteHyperlinkAsync(
	"project documentation",
	"https://example.com/docs",
	"docs-1"
);

await using TerminalHyperlinkLease hyperlink =
	await session.AcquireHyperlinkAsync(
		"https://example.com/docs",
		"docs-1"
	);

await session.WriteTextAsync( "linked text" );
```

The public API does not expose raw OSC selector numbers, arbitrary OSC 8 parameter dictionaries, generic escape-sequence construction, URI activation, or automatic URL detection.

The first functional milestone remains intact: `watch`, `slabtop`, and `top` operate through `Icod.DCurses` over the shared `Icod.Terminal` / `Icod.TermInfo` stack.

## Architecture

```text
Icod.TermInfo
      ^
      |
Icod.Terminal
      ^
      |
Icod.DCurses
      ^
      |
watch / slabtop / top
```

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns live endpoint observation, terminal modes, input, dimensions, lifecycle, terminal identity, output setup, reversible presentation-state mechanisms, active terminal-query routing, and semantic terminal-output operations. `Icod.DCurses` owns cells, windows, virtual-screen state, and refresh/diff policy. A future `Icod.Pty` package remains an adjacent concern rather than a prerequisite.

`Icod.Timing` supplies the monotonic elapsed-time and cancellable-delay primitives used by Terminal's relative event timeouts and Escape-sequence ambiguity windows.

## Installation

The stable 0.6 maintenance release installs as:

```text
dotnet add package Icod.Terminal --version 0.6.1
```

The package targets `net8.0`, `net9.0`, and `net10.0` and depends on `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`.

## Quick start

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.SetWindowTitleAsync( "my terminal app" );
await session.PublishCurrentLocationAsync(
	"/usr/local/src",
	TerminalLocationPathStyle.Posix
);

await session.WriteHyperlinkAsync(
	"project documentation",
	"https://example.com/docs"
);

TerminalEvent terminalEvent = await session.ReadEventAsync(
	TimeSpan.FromSeconds( 1 )
);
```

The session borrows process-standard endpoints, owns only the terminal state transitions it applies, and restores its captured baseline during `DisposeAsync()`.

Applications which genuinely need complete native mode observation, serialization, or custom endpoint/control backends may use the lower-level public contracts; ordinary interactive applications should prefer `TerminalSession`.

## 0.6 OSC 8 hyperlinks

`0.6` adds two semantic hyperlink patterns.

For one bounded string of application text:

```csharp
await session.WriteHyperlinkAsync(
	"example",
	"https://example.com/",
	"example-1"
);
```

For streaming or structured output:

```csharp
await using TerminalHyperlinkLease hyperlink =
	await session.AcquireHyperlinkAsync(
		"https://example.com/",
		"example-1"
	);

await session.WriteTextAsync( "first part" );
await session.WriteTextAsync( " and second part" );
```

The URI is caller-supplied absolute, already URI-encoded ASCII text. `Icod.Terminal` validates generic RFC 3986 syntax without routing the value through browser/WHATWG normalization. Percent escapes are preserved while hexadecimal digits normalize to uppercase. Raw spaces, raw non-ASCII text, malformed Unicode, malformed `%HH`, C0, DEL, and C1 controls, relative references, malformed authorities, and scoped IPv6 zone identifiers are rejected before output.

The URI payload is bounded to 2083 bytes. The only public OSC 8 parameter semantic is optional `id`, restricted to RFC 3986 unreserved ASCII and 128 bytes. Null and empty identifiers canonicalize to an omitted `id` parameter.

The library does not impose a fixed URI-scheme allow-list. Consumers handling untrusted targets remain responsible for application-level scheme and trust policy. `Icod.Terminal` does not fetch, resolve, open, launch, or otherwise activate the URI.

Hyperlink scopes are strictly LIFO:

```text
Acquire A   -> begin A
Acquire B   -> begin B
Dispose B   -> restore A
Dispose A   -> close
```

Out-of-order disposal fails without output or tracked-state mutation. Failed release remains retryable. Session disposal performs final best-effort cleanup of outstanding library-owned hyperlink state.

Active logical hyperlink scopes also participate in the managed terminal lifecycle. Before a catchable suspension, `Icod.Terminal` emits one canonical OSC 8 close while retaining the logical lease stack. After successful terminal/session re-entry it re-emits the innermost active hyperlink. Thus shell/job-control activity does not inherit library-owned hyperlink state, while nested lease ownership resumes exactly where it left off. A failed hyperlink re-entry prevents the session from claiming valid restored state.

Successful completion means the requested bytes were written. It does not prove that the terminal recognizes OSC 8, displays a hyperlink affordance, permits activation, or can reach the target. Terminal identity and `TERM` are not fabricated into proof of support.

The reviewed 0.6 API delta is recorded in [`docs/Public-API-Baseline-0.6.md`](docs/Public-API-Baseline-0.6.md).

## 0.5 OSC current-location publication

`0.5` adds semantic OSC 7 publication through:

```csharp
await session.PublishCurrentLocationAsync(
	path,
	TerminalLocationPathStyle.Posix,
	authority
);
```

`TerminalLocationPathStyle` exposes `Posix`, `WindowsDrive`, and `WindowsUnc` so path interpretation is explicit and deterministic rather than inferred from the machine running the application.

The library emits only `file:` URIs for OSC 7. Local paths use canonical forms such as `file:///usr/src` and `file:///C:/src`; UNC paths map to authority form such as `file://server/share/dir`. Path text is treated as native path data, not pre-escaped URI text: for example, a literal filename component `%20` becomes `%2520`.

Only RFC 3986 unreserved ASCII bytes remain literal inside path segments; other bytes are percent-encoded from strict UTF-8 using uppercase hexadecimal. The encoded URI payload is bounded to 16384 bytes. C0, DEL, and C1 control characters in the native path are rejected before URI construction rather than percent-encoded.

Publication is explicit and privacy-sensitive. `Icod.Terminal` does not automatically publish `Environment.CurrentDirectory`, derive host names from the environment, monitor directory changes, or republish a location during disposal.

Explicit authorities support ASCII DNS names, IPv4 literals, and bracketed unscoped IPv6 literals. Userinfo, ports, path/query/fragment data, internationalized host names, literal `%` authority text, and scoped IPv6 zone identifiers are rejected in 0.5.

The reviewed 0.5 API delta is recorded in [`docs/Public-API-Baseline-0.5.md`](docs/Public-API-Baseline-0.5.md).

## 0.4 OSC title operations

`0.4` adds three semantic title methods:

```csharp
await session.SetTitleAsync( "both" );
await session.SetIconNameAsync( "icon" );
await session.SetWindowTitleAsync( "window" );
```

These map to OSC 0, OSC 1, and OSC 2. The wire contract uses the 7-bit `ESC ]` OSC introducer and `ESC \\` String Terminator. Title text is validated before output and the operations participate in session-owned output ordering without implicit flush.

The reviewed 0.4 API delta is recorded in [`docs/Public-API-Baseline-0.4.md`](docs/Public-API-Baseline-0.4.md).

## 0.3 active terminal queries

`0.3` added explicit typed terminal interrogation to `TerminalSession`. Opening a session does not send DA, DSR, CPR, DECRQSS, XTGETTCAP, or any other probe.

```csharp
TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

TerminalPrimaryDeviceAttributes primary =
	await session.QueryPrimaryDeviceAttributesAsync( timeout );

TerminalCursorPosition cursor =
	await session.QueryCursorPositionAsync( timeout );

TerminalStatusStringResponse sgr =
	await session.QueryStatusStringAsync(
		TerminalStatusStringKind.SelectGraphicRendition,
		timeout
	);

TerminalCapabilityObservation terminalName =
	await session.QueryLiveCapabilityAsync(
		"TN",
		timeout
	);
```

The public query families cover Primary/Secondary Device Attributes, standard DSR, Cursor Position Report, fixed DECRQSS status-string requests, and single-name XTGETTCAP live capability observations.

Responses are routed through the same session-owned input path used by ordinary text, keys, mouse, focus, paste, and lifecycle-aware event consumption. There is no public raw response reader or caller-extensible query-protocol registration surface.

The reviewed 0.3 additions are recorded in [`docs/Public-API-Baseline-0.3.md`](docs/Public-API-Baseline-0.3.md).

## 0.2 rich input

Rich input remains on the same `TerminalSession.ReadEventAsync` path. Reporting protocols are enabled only through reversible session-owned leases:

```csharp
TerminalControlResult<TerminalInputProtocolLease> protocolResult =
	await session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true,
			FocusReporting = true,
			MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
		}
	);
```

Nested leases are supported, bracketed paste is framed as bounded Begin/Data/End events, and session disposal remains authoritative cleanup.

The reviewed 0.2 additions are recorded in [`docs/Public-API-Baseline-0.2.md`](docs/Public-API-Baseline-0.2.md).

## 0.1 consumer contract

The original live-session contract remains intact:

- input is always an interactive terminal; output may be redirected only when explicitly permitted;
- canonical/cbreak/raw are semantic requests mapped separately to POSIX and Windows host models;
- unknown POSIX terminal names fall back safely rather than silently becoming xterm;
- input decoding is incremental and the Escape-prefix ambiguity window is bounded;
- `Available`, `Unavailable`, `Unsupported`, and `Failed` remain distinct low-level outcomes;
- session cleanup restores captured state and does not close borrowed caller/process endpoints;
- PTY/ConPTY creation and child-process hosting belong to a future adjacent `Icod.Pty` package.

The reviewed 0.1 contract is recorded in [`docs/Public-API-Baseline-0.1.md`](docs/Public-API-Baseline-0.1.md).

## Target frameworks

The library targets:

- `net8.0`;
- `net9.0`;
- `net10.0`.

The codebase uses C# 13 and supports the terminal-control implementations provided for Windows, Linux, and macOS.

## Samples

The repository contains six deliberately different interactive samples:

- [`Icod.Terminal.Sample`](samples/Icod.Terminal.Sample/) — minimal session, identity, size, output, and restoration;
- [`Icod.Terminal.RichInput.Sample`](samples/Icod.Terminal.RichInput.Sample/) — focus, paste, mouse, modified keys, lifecycle, and reversible input-protocol leases;
- [`Icod.Terminal.Query.Sample`](samples/Icod.Terminal.Query.Sample/) — explicit CSI/DCS query families;
- [`Icod.Terminal.Title.Sample`](samples/Icod.Terminal.Title.Sample/) — semantic OSC 0/1/2 title operations;
- [`Icod.Terminal.Location.Sample`](samples/Icod.Terminal.Location.Sample/) — explicit OSC 7 current-location publication;
- [`Icod.Terminal.Hyperlink.Sample`](samples/Icod.Terminal.Hyperlink.Sample/) — bounded and scoped OSC 8 hyperlinks with nested restoration.

See [`samples/README.md`](samples/README.md) for run instructions and expected behavior.

## Build

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

Both scripts support `clean`, `restore`, `build`, `test`, `pack`, and `validate`. Running either script without an argument performs the complete sequence, including Debug package validation.

## Development roadmap

The `0.6.1` maintenance release is documented in [`docs/0.6.1-Dependency-Refresh.md`](docs/0.6.1-Dependency-Refresh.md).

The `0.6.0` milestone is documented in [`Icod.Terminal-0.6.0-Development-Roadmap.md`](Icod.Terminal-0.6.0-Development-Roadmap.md), with completed development records in T44–T51, the reviewed public API delta in [`docs/Public-API-Baseline-0.6.md`](docs/Public-API-Baseline-0.6.md), and package/release closure in [`docs/T51-0.6.0-Package-Consumer-and-Release-Closure.md`](docs/T51-0.6.0-Package-Consumer-and-Release-Closure.md).

The completed `0.5.0` milestone is documented in [`Icod.Terminal-0.5.0-Development-Roadmap.md`](Icod.Terminal-0.5.0-Development-Roadmap.md), with final release closure in [`docs/T43-0.5.0-Package-Consumer-and-Release-Closure.md`](docs/T43-0.5.0-Package-Consumer-and-Release-Closure.md).

The protocol-closure sequence is recorded in [`Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md`](Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md).

Earlier milestone roadmaps and tranche records remain under the repository root and `docs/` directory.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and ncurses.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

Licensed under the GNU Lesser General Public License v3.0 or later. See `LICENSE`.
