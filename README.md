# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.8.0` is the current stable release. It adds typed DECSCUSR cursor-style control, explicit DECRQSS cursor-style observation, xterm-compatible bar cursor styles, and truthful strict-LIFO scoped restoration based on an actually observed previous semantic style.

The release preserves the existing live-session, rich-input, active-query, OSC 0/1/2 title, OSC 7 current-location, OSC 8 hyperlink, and OSC 52 clipboard contracts.

## Installation

```text
dotnet add package Icod.Terminal --version 0.8.0
```

The package targets `net8.0`, `net9.0`, and `net10.0` and depends on `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`.

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

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns live endpoint observation, terminal modes, input, dimensions, lifecycle, terminal identity, output setup, reversible presentation state, active terminal-query routing, and semantic terminal-output operations. `Icod.DCurses` owns cells, windows, virtual-screen state, and refresh/diff policy.

`Icod.Timing` supplies monotonic elapsed-time and cancellable-delay primitives used by input ambiguity windows and active query transactions.

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
await session.WriteClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	"copied text"
);
await session.SetCursorStyleAsync(
	TerminalCursorStyle.SteadyBar
);
```

The session borrows process-standard endpoints, owns only terminal state transitions it applies, and restores captured state during `DisposeAsync()`.

## 0.8 cursor style

### Semantic styles

`TerminalCursorStyle` exposes exactly six semantic styles:

```csharp
TerminalCursorStyle.BlinkingBlock
TerminalCursorStyle.SteadyBlock
TerminalCursorStyle.BlinkingUnderline
TerminalCursorStyle.SteadyUnderline
TerminalCursorStyle.BlinkingBar
TerminalCursorStyle.SteadyBar
```

They map to DECSCUSR parameters 1 through 6. Outbound frames use canonical seven-bit CSI:

```text
ESC [ Ps SP q
```

Bar styles use the xterm-compatible DECSCUSR extension. Successful write completion proves only that the full frame was emitted; it does not prove that the terminal recognized or applied the style.

### Explicit setter

```csharp
await session.SetCursorStyleAsync(
	TerminalCursorStyle.SteadyUnderline
);
```

The setter participates in session-owned output ordering, validates before emission, rejects known redirected output, and does not implicitly flush.

### Explicit observation

```csharp
TerminalCursorStyleObservation observation =
	await session.QueryCursorStyleAsync(
		TimeSpan.FromMilliseconds( 750 )
	);

if ( observation.IsSupported ) {
	Console.WriteLine( observation.Style );
}
```

Observation reuses the existing DECRQSS `SP q` query path. An explicit negative DECRQSS response returns `IsSupported == false`. Timeout remains `TimeoutException`; malformed or unknown positive state remains `FormatException`. A timeout is not treated as proof of unsupported behavior.

Inbound omitted, `0`, and `1` state normalize to `BlinkingBlock`; recognized values 2 through 6 map directly. xterm parameter `7` is not exposed as a generic semantic style or restoration primitive.

### Truthful scoped restoration

```csharp
await using TerminalCursorStyleLease lease =
	await session.AcquireCursorStyleAsync(
		TerminalCursorStyle.SteadyBar,
		TimeSpan.FromMilliseconds( 750 )
	);

await session.WriteTextAsync( "work while the leased style is active" );
```

The outermost lease first observes the actual current semantic cursor style. No mutation occurs unless that observation succeeds. Nested leases use strict LIFO ownership and restore the immediately preceding session-owned style. The outermost release restores the actually observed pre-lease style.

Exact restoration never means guessing a reset. `Icod.Terminal` does not use DECSCUSR parameter `0`, hard-code a block cursor, or emit xterm parameter `7` as a substitute for observed prior state.

Active cursor-style leases also participate in managed suspend/resume. Before suspension, the observed baseline is restored. After successful re-entry, the innermost active logical style is re-applied. Releasing a lease while suspended updates logical ownership without emitting extra cursor-style bytes.

### Cursor style is not cursor visibility

Cursor shape/blink policy and cursor visibility remain separate public concepts. `TerminalCursorStyle` does not hide or show the cursor. `TerminalCursorVisibility` remains part of reversible `TerminalPresentationLease` state.

The reviewed 0.8 API is frozen in [`docs/Public-API-Baseline-0.8.md`](docs/Public-API-Baseline-0.8.md). T86 integration acceptance is recorded in [`docs/T86-Cursor-Style-Integration-Compatibility-and-Regression-Acceptance.md`](docs/T86-Cursor-Style-Integration-Compatibility-and-Regression-Acceptance.md).

## Earlier semantic terminal operations

### 0.7 OSC 52 clipboard and selections

```csharp
await session.WriteClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	"copied text"
);

byte[] payload = await session.ReadClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	TimeSpan.FromMilliseconds( 750 )
);
```

Reads are always explicit. Opening, probing, suspending, resuming, or disposing a session never initiates a clipboard read. The decoded payload ceiling is 65,536 bytes and text writes use strict UTF-8 without BOM.

See [`docs/Public-API-Baseline-0.7.md`](docs/Public-API-Baseline-0.7.md).

### 0.6 OSC 8 hyperlinks

```csharp
await session.WriteHyperlinkAsync(
	"example",
	"https://example.com/"
);

await using TerminalHyperlinkLease hyperlink =
	await session.AcquireHyperlinkAsync(
		"https://example.com/"
	);
```

Hyperlink scopes are strict LIFO and participate in managed suspend/resume cleanup.

See [`docs/Public-API-Baseline-0.6.md`](docs/Public-API-Baseline-0.6.md).

### 0.5 OSC 7 current-location publication

```csharp
await session.PublishCurrentLocationAsync(
	"/usr/local/src",
	TerminalLocationPathStyle.Posix
);
```

Path grammar is explicit; the library does not automatically publish `Environment.CurrentDirectory`.

See [`docs/Public-API-Baseline-0.5.md`](docs/Public-API-Baseline-0.5.md).

### 0.4 OSC title operations

```csharp
await session.SetTitleAsync( "both" );
await session.SetIconNameAsync( "icon" );
await session.SetWindowTitleAsync( "window" );
```

See [`docs/Public-API-Baseline-0.4.md`](docs/Public-API-Baseline-0.4.md).

## Active terminal queries

Opening a session does not interrogate the terminal. Queries are explicit and bounded:

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
```

Responses are routed through the same session-owned input path used by ordinary text, keys, mouse, focus, paste, and lifecycle events. There is no second public response reader.

## Rich input and reversible presentation

Rich input remains on `TerminalSession.ReadEventAsync`. Reporting protocols are enabled only through reversible session-owned leases.

```csharp
TerminalControlResult<TerminalInputProtocolLease> protocols =
	await session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true,
			FocusReporting = true,
			MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
		}
	);
```

Presentation state such as alternate screen, keypad mode, and cursor visibility is separately owned by `TerminalPresentationLease`.

## Samples

The repository contains focused samples for each major public family:

- [`Icod.Terminal.CursorStyle.Sample`](samples/Icod.Terminal.CursorStyle.Sample/) — 0.8 cursor-style observation and truthful scoped restoration;
- [`Icod.Terminal.Clipboard.Sample`](samples/Icod.Terminal.Clipboard.Sample/) — 0.7 OSC 52 clipboard writes and reads;
- [`Icod.Terminal.Hyperlink.Sample`](samples/Icod.Terminal.Hyperlink.Sample/) — 0.6 OSC 8 bounded and scoped hyperlinks;
- [`Icod.Terminal.Location.Sample`](samples/Icod.Terminal.Location.Sample/) — 0.5 OSC 7 location publication;
- [`Icod.Terminal.Title.Sample`](samples/Icod.Terminal.Title.Sample/) — 0.4 OSC 0/1/2 title operations;
- [`Icod.Terminal.Query.Sample`](samples/Icod.Terminal.Query.Sample/) — active query families;
- [`Icod.Terminal.RichInput.Sample`](samples/Icod.Terminal.RichInput.Sample/) — rich input and protocol leases;
- [`Icod.Terminal.Sample`](samples/Icod.Terminal.Sample/) — minimal live session.

See [`samples/README.md`](samples/README.md) for run instructions.

## Build and validation

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

Both scripts support `clean`, `restore`, `build`, `test`, `pack`, and `validate`. Distribution validation builds/tests the solution, packs the NuGet artifacts, verifies package structure and XML documentation, and runs fresh package-only consumers.

The 0.8 cursor-style package consumer is additionally required to restore and run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Stable release readiness requires:

1. PR validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. cursor-style XML documentation and package-only smoke green on all supported TFMs;
4. merge to `main`;
5. Release distribution validation green on the `main` architecture matrix;
6. only then create tag `v0.8.0`.

The tag workflow rebuilds and retests the tagged commit, selects the exact package matching the tag, reruns package verification including the 0.8 cursor-style contract, and only then publishes to NuGet.org and GitHub Packages.

## Development roadmap

The 0.8 milestone is documented in [`Icod.Terminal-0.8.0-Development-Roadmap.md`](Icod.Terminal-0.8.0-Development-Roadmap.md), with tranche records T80–T87 under `docs/`.

The broader protocol-closure plan remains in [`Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md`](Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md). `0.9.0` remains reserved for synchronized output and nested transactional output state.

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
