# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.16.0` adds two deliberately bounded OSC 9 semantic operations while retaining all earlier public contracts:

- legacy desktop notification through `SendNotificationAsync(...)`;
- explicit Windows Terminal/ConEmu current-directory compatibility through `PublishWindowsCurrentDirectoryCompatibilityAsync(...)`.

OSC 7 remains the preferred portable current-location protocol. Existing OSC 9;4 progress remains byte-for-byte unchanged.

## Installation

```text
dotnet add package Icod.Terminal --version 0.16.0
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

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns live endpoint observation, terminal modes, input, dimensions, lifecycle, terminal identity, output setup, active terminal-query routing, and semantic terminal-output operations. `Icod.DCurses` owns cells, windows, virtual-screen state, and refresh/diff policy.

## Safe OSC 9 notifications

0.16 adds:

```csharp
await session.SendNotificationAsync(
	"Build complete"
);
```

The wire form is:

```text
OSC 9;Build complete ST
```

The operation is emission-oriented: successful completion means the complete request was written. It does not prove the desktop displayed a notification.

Notification text must be well-formed Unicode. It is encoded as strict UTF-8. C0 controls, DEL, and C1 controls are rejected rather than escaped, so BEL/ESC/ST framing injection is impossible without changing printable text semantics. Empty notification text is allowed. The maximum OSC payload is 4,096 bytes including `9;`; oversize input is rejected before output and never truncated.

Notification publication can expose text through desktop notification history, lock screens, screen sharing, remote sessions, multiplexers, or terminal logs. `Icod.Terminal` does not automatically discover, classify, or redact sensitive information.

## Windows current-directory compatibility

0.16 also adds an explicitly secondary compatibility API:

```csharp
await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
	"C:\\work\\repo"
);
```

which emits:

```text
OSC 9;9;C:\work\repo ST
```

This is a Windows Terminal/ConEmu compatibility mechanism. The portable/default API remains OSC 7:

```csharp
await session.PublishCurrentLocationAsync(
	"/work/repo",
	TerminalLocationPathStyle.Posix
);
```

The library never substitutes OSC 9;9 for OSC 7 and never emits both automatically. Callers that intentionally need both must call both APIs explicitly.

For OSC 9;9, the caller supplies the Windows filesystem path. `Icod.Terminal` performs no `wslpath`/`cygpath` translation, separator normalization, `Path.GetFullPath`, filesystem lookup, symlink/reparse resolution, process-CWD discovery, terminal-brand detection, or environment-variable inspection. The path must be non-empty, well-formed Unicode without C0/DEL/C1 controls, and fit within the 32,768-byte encoded OSC payload bound.

## OSC 9 safety boundary

0.16 does **not** expose OSC 9 generically. Vendor commands capable of blocking, displaying modal UI, executing macros/processes, disclosing environment data, or changing emulator behavior are deliberately unavailable.

Excluded examples include ConEmu-family `9;1`, `9;2`, `9;5`, `9;6`, `9;7`, `9;8`, and `9;10`. Duplicate title/prompt operations are also excluded because OSC 0/1/2 and OSC 133 already own those semantics. There is no public `WriteOsc9Async(...)` or generic OSC builder.

## Ordering and lifecycle semantics

The two new operations use the same `TerminalSession` output serialization domain as existing terminal output.

- pre-commit cancellation emits nothing;
- committed frames are one non-cancellable transport write;
- concurrent operations serialize as whole frames;
- no implicit flush;
- transport failure propagates without compensating traffic;
- later independent calls remain usable;
- no automatic session-open OSC 9 emission;
- no lifecycle lease, suspend reset, resume replay, `InvalidateState()` output, or disposal synthesis.

The existing active-query reader/router remains unchanged.

## OSC 133 semantic prompt markers

The portable API remains unchanged:

```csharp
await session.BeginPromptAsync();
await session.BeginCommandInputAsync();
await session.BeginCommandOutputAsync();
await session.FinishCommandAsync( 0 );

// A cancelled/aborted region is a bare D marker, not status zero.
await session.AbortCommandAsync();
```

0.15 typed prompt metadata and `cmdline_url` command-output metadata remain available unchanged. See [`docs/Public-API-Baseline-0.15.md`](docs/Public-API-Baseline-0.15.md).

## Lifecycle-safe colors

The 0.14 query-before-mutate scoped color ownership/restoration APIs remain unchanged:

```csharp
TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

await using TerminalPaletteColorLease palette =
	await session.AcquirePaletteColorAsync(
		1,
		TerminalColor.FromRgb8( 255, 64, 64 ),
		timeout
	);
```

See [`docs/Public-API-Baseline-0.14.md`](docs/Public-API-Baseline-0.14.md).

## Downstream Icod.DCurses acceptance

The real `Icod.DCurses 0.1.0` acceptance path runs on net8/net9/net10. The retained semantic-prompt acceptance now also proves:

```text
OSC 9 notification
[real CursesSession.RefreshAsync output]
OSC 9;9 Windows CWD compatibility
[real CursesSession.RefreshAsync output]
OSC 9 notification
```

using only public `TerminalSession` APIs on the same session owned by `CursesSession`. No raw OSC shortcut or side-channel writer is used.

The stable 0.16 public contract is frozen in [`docs/Public-API-Baseline-0.16.md`](docs/Public-API-Baseline-0.16.md).

## Previous release highlights

- **0.15** — typed OSC 133 extended semantic metadata.
- **0.14** — lifecycle-safe scoped color ownership and exact restoration.
- **0.13** — observable OSC 4/104 and selected OSC 10–19 terminal colors.
- **0.12** — portable OSC 133 semantic prompt/command-region markers.
- **0.11** — OSC 22 pointer shape.
- **0.10** — OSC 9;4 progress.
- **0.9** — DEC private mode 2026 synchronized output.
- **0.8** — DECSCUSR cursor style with observation/scoped restoration.
- **0.7** — OSC 52 clipboard/selections.
- **0.6** — OSC 8 hyperlinks.
- **0.5** — OSC 7 current location.
- **0.4** — OSC 0/1/2 title operations.

## Build and validation

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

Distribution validation builds/tests the solution, runs real downstream `Icod.DCurses` acceptance, packs the NuGet artifact, verifies package structure/XML documentation, and runs fresh package-only consumers.

The retained 0.8–0.15 package contracts plus the new 0.16 safe OSC 9 contract run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.16.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all real downstream `Icod.DCurses` acceptance gates green;
4. retained 0.8–0.15 plus new 0.16 XML/package-only smoke gates green on all supported TFMs;
5. merge to `main`;
6. Release validation green on the exact resulting `main` commit;
7. only then create tag `v0.16.0`.

The tagged workflow reruns build/tests, downstream acceptance, exact package selection, historical package contracts, and the 0.16 safe OSC 9 package contract before publication to NuGet.org and GitHub Packages.

## Development roadmap

The 0.16 milestone is documented in [`Icod.Terminal-0.16.0-Development-Roadmap.md`](Icod.Terminal-0.16.0-Development-Roadmap.md), with tranche records T160–T167 under `docs/`.

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
