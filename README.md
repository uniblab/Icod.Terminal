# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.12.0` is the current stable release. It adds semantic OSC 133 shell-integration / semantic-prompt markers with explicit prompt, command-input, command-output, successful-completion, and abort operations.

The release preserves the existing live-session, rich-input, active-query, OSC 0/1/2 title, OSC 7 current-location, OSC 8 hyperlink, OSC 9;4 progress, OSC 22 pointer-shape, OSC 52 clipboard, cursor-style, presentation, and synchronized-output contracts.

## Installation

```text
dotnet add package Icod.Terminal --version 0.12.0
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

await session.BeginPromptAsync();
await session.WriteTextAsync( "demo> " );
await session.BeginCommandInputAsync();
await session.WriteTextAsync( "echo hello\r\n" );
await session.BeginCommandOutputAsync();
await session.WriteTextAsync( "hello\r\n" );
await session.FinishCommandAsync( 0 );
```

The session borrows process-standard endpoints, owns only terminal state transitions it applies, and restores or resets captured/session-owned state during `DisposeAsync()`.

## 0.12 OSC 133 semantic prompt integration

### Public semantic operations

The stable 0.12 public delta is exactly five methods on `TerminalSession`:

```csharp
await session.BeginPromptAsync();
await session.BeginCommandInputAsync();
await session.BeginCommandOutputAsync();
await session.FinishCommandAsync( 0 );
await session.AbortCommandAsync();
```

The portable wire mapping is:

```text
BeginPromptAsync()        -> ESC ] 133 ; A ESC \
BeginCommandInputAsync()  -> ESC ] 133 ; B ESC \
BeginCommandOutputAsync() -> ESC ] 133 ; C ESC \
FinishCommandAsync(n)     -> ESC ] 133 ; D ; n ESC \
AbortCommandAsync()       -> ESC ] 133 ; D ESC \
```

Outbound OSC 133 uses canonical ST termination (`ESC \\`).

### Completion versus abort

`FinishCommandAsync(...)` accepts `byte`, so the portable completion-status domain is exactly `0..255`.

```csharp
await session.FinishCommandAsync( 0 );
```

means a completed command with explicit status zero.

```csharp
await session.AbortCommandAsync();
```

emits bare `D` and carries no status. The two forms are intentionally distinct; there is no nullable status API that conflates them.

### Independent-call semantics

The five OSC 133 operations are independently callable. `Icod.Terminal` does not keep an in-memory A -> B -> C -> D shell-history state machine and therefore does not reject a marker merely because earlier markers were not observed through the same session.

Applications that own a normal prompt/command lifecycle will usually emit A, B, C, then either `D;status` or bare D. That ordering remains application policy rather than retained session state.

This posture supports prompt redraws, interruption recovery, starting integration in the middle of an interaction, multiplexers, subshells, and nested REPLs without inventing false shell history inside the library.

### Serialization, cancellation, and failure

OSC 133 marker writes participate in the same session-owned output serialization domain as application text and existing terminal-control output.

- known redirected output is rejected;
- caller cancellation is observed before commit;
- cancellation while queued for output emits nothing;
- each complete frame is constructed before commit;
- committed marker writes use `CancellationToken.None`;
- marker methods do not implicitly flush;
- successful completion proves emission only, not terminal recognition.

A failed committed marker write propagates to the caller. The library does not emit a compensating finish/abort marker, does not fabricate command history, and does not poison a synthetic command-region state because no such state exists.

### Lifecycle posture

OSC 133 markers are transient annotations rather than library-owned terminal modes.

Managed suspend, resume, and session disposal therefore emit no automatic OSC 133 marker. There is no OSC 133 lease to restore, invalidate, or clean up.

### Composition

The 0.12 suite proves deterministic composition with:

- ordinary application text;
- OSC 0/1/2 title operations;
- OSC 7 current-location publication;
- OSC 8 hyperlinks;
- OSC 9;4 progress;
- OSC 22 pointer shape;
- OSC 52 clipboard operations;
- DECSCUSR cursor style;
- reversible presentation state;
- reversible input-protocol leases;
- DEC private mode 2026 synchronized output;
- active terminal queries;
- real downstream `Icod.DCurses` refresh output.

The public contract is frozen in [`docs/Public-API-Baseline-0.12.md`](docs/Public-API-Baseline-0.12.md). Composition and downstream acceptance are recorded in [`docs/T126-OSC-133-Composition-and-DCurses-Acceptance.md`](docs/T126-OSC-133-Composition-and-DCurses-Acceptance.md).

## 0.11 terminal mouse-pointer shape

0.11 added semantic OSC 22 pointer control with 30 CSS-compatible shapes, explicit set/reset, identity-aware scoped ownership, and bounded Kitty-compatible pointer queries.

```csharp
await session.SetPointerShapeAsync(
	TerminalPointerShape.Crosshair
);
await session.ResetPointerShapeAsync();

await using TerminalPointerShapeLease pointer =
	await session.AcquirePointerShapeAsync(
		TerminalPointerShape.Pointer
	);
```

See [`docs/Public-API-Baseline-0.11.md`](docs/Public-API-Baseline-0.11.md).

## 0.10 terminal progress

0.10 added scoped semantic OSC 9;4 progress ownership.

```csharp
await using TerminalProgressLease progress =
	await session.AcquireProgressAsync();

await progress.ReportAsync( 1, 3 );
await progress.SetIndeterminateAsync();
```

See [`docs/Public-API-Baseline-0.10.md`](docs/Public-API-Baseline-0.10.md).

## 0.9 synchronized output

0.9 added scoped DEC private mode 2026 synchronized output.

```csharp
await using TerminalSynchronizedOutputLease synchronized =
	await session.AcquireSynchronizedOutputAsync();
```

See [`docs/Public-API-Baseline-0.9.md`](docs/Public-API-Baseline-0.9.md).

## 0.8 cursor style

0.8 added DECSCUSR cursor-style mutation, explicit observation, and truthful scoped restoration.

```csharp
await session.SetCursorStyleAsync(
	TerminalCursorStyle.SteadyUnderline
);
```

See [`docs/Public-API-Baseline-0.8.md`](docs/Public-API-Baseline-0.8.md).

## Earlier semantic terminal operations

### 0.7 OSC 52 clipboard and selections

```csharp
await session.WriteClipboardAsync(
	TerminalClipboardSelection.Clipboard,
	"copied text"
);
```

See [`docs/Public-API-Baseline-0.7.md`](docs/Public-API-Baseline-0.7.md).

### 0.6 OSC 8 hyperlinks

```csharp
await session.WriteHyperlinkAsync(
	"example",
	"https://example.com/"
);
```

See [`docs/Public-API-Baseline-0.6.md`](docs/Public-API-Baseline-0.6.md).

### 0.5 OSC 7 current-location publication

```csharp
await session.PublishCurrentLocationAsync(
	"/usr/local/src",
	TerminalLocationPathStyle.Posix
);
```

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
```

Responses are routed through the same session-owned input path used by ordinary text, keys, mouse, focus, paste, and lifecycle events. There is no second public response reader.

## Rich input and reversible presentation

Rich input remains on `TerminalSession.ReadEventAsync`. Reporting protocols are enabled only through reversible session-owned leases. Presentation state such as alternate screen, keypad mode, and cursor visibility is separately owned by `TerminalPresentationLease`.

## Samples

Focused samples include:

- [`Icod.Terminal.SemanticPrompt.Sample`](samples/Icod.Terminal.SemanticPrompt.Sample/) — 0.12 OSC 133 semantic prompt/command markers;
- [`Icod.Terminal.PointerShape.Sample`](samples/Icod.Terminal.PointerShape.Sample/) — 0.11 OSC 22 pointer shape;
- [`Icod.Terminal.Progress.Sample`](samples/Icod.Terminal.Progress.Sample/) — 0.10 OSC 9;4 progress;
- [`Icod.Terminal.SynchronizedOutput.Sample`](samples/Icod.Terminal.SynchronizedOutput.Sample/) — 0.9 synchronized output;
- [`Icod.Terminal.CursorStyle.Sample`](samples/Icod.Terminal.CursorStyle.Sample/) — 0.8 cursor style;
- [`Icod.Terminal.Clipboard.Sample`](samples/Icod.Terminal.Clipboard.Sample/) — OSC 52 clipboard;
- [`Icod.Terminal.Hyperlink.Sample`](samples/Icod.Terminal.Hyperlink.Sample/) — OSC 8 hyperlinks;
- [`Icod.Terminal.Location.Sample`](samples/Icod.Terminal.Location.Sample/) — OSC 7 location;
- [`Icod.Terminal.Title.Sample`](samples/Icod.Terminal.Title.Sample/) — OSC title operations;
- [`Icod.Terminal.Query.Sample`](samples/Icod.Terminal.Query.Sample/) — active queries;
- [`Icod.Terminal.RichInput.Sample`](samples/Icod.Terminal.RichInput.Sample/) — rich input;
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

Both scripts support `clean`, `restore`, `build`, `test`, `pack`, and `validate`.

Distribution validation builds/tests the complete solution, runs real downstream `Icod.DCurses` synchronized-output, terminal-progress, pointer-shape, and semantic-prompt acceptance, packs the NuGet artifacts, verifies package structure/XML documentation, and runs fresh package-only consumers.

The 0.8 cursor-style, 0.9 synchronized-output, 0.10 terminal-progress, 0.11 pointer-shape, and 0.12 semantic-prompt package consumers are required to restore and run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.12.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all four real downstream `Icod.DCurses` acceptance gates green;
4. retained 0.8/0.9/0.10/0.11 and new 0.12 XML documentation/package-only smoke gates green on all supported TFMs;
5. merge to `main`;
6. Release distribution validation green on the resulting exact `main` commit;
7. only then create tag `v0.12.0`.

The tag workflow rebuilds and retests the tagged solution, reruns all four downstream DCurses acceptance gates, selects the exact package matching the tag, reruns historical and 0.12 package verification, and only then publishes to NuGet.org and GitHub Packages.

## Development roadmap

The 0.12 milestone is documented in [`Icod.Terminal-0.12.0-Development-Roadmap.md`](Icod.Terminal-0.12.0-Development-Roadmap.md), with tranche records T120–T127 under `docs/`.

The completed 0.11 pointer-shape milestone is documented in [`Icod.Terminal-0.11.0-Development-Roadmap.md`](Icod.Terminal-0.11.0-Development-Roadmap.md).

The completed protocol-closure sequence through 0.9 is documented in [`Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md`](Icod.Terminal-0.4.0-to-0.9.0-Protocol-Closure-Roadmap.md).

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
