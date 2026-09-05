# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.9.0` is the current stable release. `0.10.0-alpha.7` is the current release candidate and adds scoped OSC 9;4 terminal-progress ownership with completed/total stage reporting, indeterminate progress, normal/error/attention states, nested restoration, lifecycle-safe cleanup, and downstream `Icod.DCurses` refresh acceptance.

The 0.10 release candidate preserves the existing live-session, rich-input, active-query, OSC 0/1/2 title, OSC 7 current-location, OSC 8 hyperlink, OSC 52 clipboard, cursor-style, and synchronized-output contracts.

## Installation

Current stable package:

```text
dotnet add package Icod.Terminal --version 0.9.0
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

await using TerminalProgressLease progress =
	await session.AcquireProgressAsync();

await progress.ReportAsync( 1, 3 );
await session.WriteTextAsync( "stage one complete\r\n" );

await progress.SetIndeterminateAsync();
await session.WriteTextAsync( "finishing work\r\n" );
```

The session borrows process-standard endpoints, owns only terminal state transitions it applies, and restores captured state during `DisposeAsync()`.

## 0.10 terminal progress

### Semantic OSC 9;4 ownership

Terminal progress is exposed through a scoped semantic lease rather than a raw OSC writer:

```csharp
await using TerminalProgressLease progress =
	await session.AcquireProgressAsync();
```

Acquisition itself emits no progress frame. Callers report work naturally as completed/total values:

```csharp
await progress.ReportAsync( 1, 10 );
await progress.ReportAsync( 2, 10 );
```

`Icod.Terminal` converts those values to the canonical OSC 9;4 percentage internally. For example, `1 / 3` becomes 33 percent and `2 / 3` becomes 67 percent using integer-only nearest-percentage rounding with exact halves upward.

The canonical emitted protocol form is:

```text
ESC ] 9 ; 4 ; state ; progress BEL
```

### Progress states

Normal determinate progress is the default overload:

```csharp
await progress.ReportAsync( 7, 10 );
```

Error and attention determinate states are explicit:

```csharp
await progress.ReportAsync(
	TerminalProgressState.Error,
	7,
	10
);

await progress.ReportAsync(
	TerminalProgressState.Attention,
	7,
	10
);
```

`Attention` is the neutral semantic name for OSC 9;4 wire state 4: Windows Terminal describes that state as warning while ConEmu describes it as paused.

Indeterminate progress is separate from determinate reporting:

```csharp
await progress.SetIndeterminateAsync();
```

The library emits canonical state 3 with progress value 0.

### Nesting and restoration

Progress ownership is identity-aware and may be released out of order. A newly acquired owner which has not yet reported a value does not mask an existing lower owner's visible progress. Once an inner owner reports, it controls physical progress until it releases or a newer reported owner takes control.

If a controlling owner releases, the newest remaining reported owner is restored. Final release emits the canonical clear state. Successful repeated disposal is idempotent, while failed cleanup retains ownership so the same lease can retry.

### Lifecycle and support posture

Managed suspension clears physical progress while retaining logical owners. Resume restores the current controlling logical value only when one remains. Releasing every owner while suspended prevents re-entry.

`TerminalSession.InvalidateState()` marks progress physical state untrusted and the next controlled transition re-establishes the logical state before proceeding. Session disposal performs authoritative cleanup before synchronized output performs its final leave operation.

Successful progress API completion proves that the complete OSC 9;4 frame was emitted. It does not prove that the terminal recognizes or renders progress. `Icod.Terminal` does not infer support from operating system, `TERM`, emulator identity, or environment variables and performs no automatic support probe.

The reviewed 0.10 API is frozen in [`docs/Public-API-Baseline-0.10.md`](docs/Public-API-Baseline-0.10.md). Downstream composition and `Icod.DCurses` acceptance are recorded in [`docs/T106-Progress-Composition-and-DCurses-Acceptance.md`](docs/T106-Progress-Composition-and-DCurses-Acceptance.md).

## 0.9 synchronized output

### Semantic lease

Synchronized output is exposed only through a scoped semantic lease:

```csharp
await using TerminalSynchronizedOutputLease synchronized =
	await session.AcquireSynchronizedOutputAsync();
```

The first logical owner emits canonical seven-bit DEC private mode 2026 enable:

```text
ESC [ ? 2 0 2 6 h
```

The final logical owner emits:

```text
ESC [ ? 2 0 2 6 l
```

followed by one output flush.

Nested acquisitions share the same physical terminal mode request. Because every owner requests the same boolean synchronized-output state, nested leases are identity-aware rather than strict-LIFO and may be disposed out of order. Non-final releases emit nothing and do not flush.

### Truthful support posture

Successful acquisition proves only that any required begin frame was emitted and logical ownership was established. It does not prove that the attached terminal implements or continues honoring private mode 2026.

`Icod.Terminal` does not infer synchronized-output support from `TERM`, operating system, terminal-emulator identity, or environment variables, and ordinary acquisition does not perform an automatic DECRQM query.

The terminal may independently stop deferring presentation because of its own timeout or implementation limits. The lease therefore guarantees protocol ownership and cleanup, not an unlimited terminal-side atomic transaction.

### Composition and lifecycle

Synchronized output is a terminal-side presentation-timing bracket, not an application-side byte buffer. Existing operations retain their normal framing and flush behavior inside the lease, including text, OSC title/location/hyperlink/clipboard operations, progress, cursor style, presentation state, and explicit active terminal queries.

Managed suspension physically leaves synchronized output and flushes before suspension while retaining logical ownership. Resume re-enters mode 2026 only if logical owners remain. Releasing all owners while suspended is logical-only and prevents re-entry.

Final-release write or flush failures retain cleanup ownership so the same lease can retry. Session disposal remains authoritative best-effort cleanup.

The reviewed 0.9 API is frozen in [`docs/Public-API-Baseline-0.9.md`](docs/Public-API-Baseline-0.9.md). T96 downstream acceptance is recorded in [`docs/T96-Synchronized-Output-Integration-Compatibility-and-DCurses-Acceptance.md`](docs/T96-Synchronized-Output-Integration-Compatibility-and-DCurses-Acceptance.md).

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

- [`Icod.Terminal.Progress.Sample`](samples/Icod.Terminal.Progress.Sample/) — 0.10 OSC 9;4 determinate/indeterminate progress ownership;
- [`Icod.Terminal.SynchronizedOutput.Sample`](samples/Icod.Terminal.SynchronizedOutput.Sample/) — 0.9 scoped synchronized output;
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

Both scripts support `clean`, `restore`, `build`, `test`, `pack`, and `validate`. Distribution validation builds/tests the complete solution—including the focused 0.10 progress sample—runs real downstream `Icod.DCurses` synchronized-output and terminal-progress acceptance, packs the NuGet artifacts, verifies package structure/XML documentation, and runs fresh package-only consumers.

The 0.8 cursor-style, 0.9 synchronized-output, and 0.10 terminal-progress package consumers are all required to restore and run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Stable 0.10 release readiness requires:

1. cumulative `0.10.0-alpha.7` validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. both real downstream `Icod.DCurses` synchronized-output and terminal-progress acceptance gates green;
4. retained 0.8/0.9 and new 0.10 XML documentation/package-only smoke gates green on all supported TFMs;
5. stable-version metadata and documentation closure;
6. exact stable PR head green;
7. merge to `main`;
8. Release distribution validation green on the resulting exact `main` commit;
9. only then create tag `v0.10.0`.

The tag workflow rebuilds and retests the tagged solution, reruns both downstream DCurses acceptance gates, selects the exact package matching the tag, reruns package verification including the 0.8, 0.9, and 0.10 public contracts, and only then publishes to NuGet.org and GitHub Packages.

## Development roadmap

The 0.10 milestone is documented in [`Icod.Terminal-0.10.0-Development-Roadmap.md`](Icod.Terminal-0.10.0-Development-Roadmap.md), with tranche records T100–T107 under `docs/`.

The completed protocol-closure sequence through 0.9 is documented in [`Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md`](Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md).

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
