# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.15.0` adds typed OSC 133 extended semantic metadata while preserving the portable OSC 133 marker API introduced in 0.12 byte-for-byte.

The new surface supports:

- typed primary/secondary prompt metadata;
- shell no-redraw declaration (`redraw=0`);
- special cursor-key declaration (`special_key=1`);
- absolute/relative prompt click metadata (`click_events=1|2`);
- typed command-line publication through `C;cmdline_url=...`;
- strict UTF-8 byte percent encoding and bounded payload construction.

The 0.14 lifecycle-safe color ownership/restoration surface remains unchanged.

## Installation

```text
dotnet add package Icod.Terminal --version 0.15.0
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

## OSC 133 semantic prompt markers

The portable API remains the simplest and broadest path:

```csharp
await session.BeginPromptAsync();
await session.BeginCommandInputAsync();
await session.BeginCommandOutputAsync();
await session.FinishCommandAsync( 0 );

// A cancelled/aborted region is a bare D marker, not status zero.
await session.AbortCommandAsync();
```

These continue to emit bare `A`, `B`, `C`, `D;status`, and bare `D` forms exactly as in 0.12.

### Extended prompt metadata

0.15 adds typed prompt options:

```csharp
TerminalSemanticPromptOptions prompt = new(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
);

await session.BeginPromptAsync( prompt );
```

That example emits parameters in canonical order:

```text
OSC 133;A;redraw=0;special_key=1;k=s;click_events=2 ST
```

`default(TerminalSemanticPromptOptions)` is valid and equivalent to the portable bare `A` method.

`click_events=1` is the broader cross-terminal tier documented by Kitty and Contour. `redraw=0`, `special_key=1`, `k=s`, and `click_events=2` are narrower Kitty-documented extensions. `Icod.Terminal` does not infer support from terminal brand.

### Command-line metadata

Command-line publication is explicit caller intent:

```csharp
TerminalSemanticCommandOutputOptions command = new(
	"printf 'café 😀'"
);

await session.BeginCommandOutputAsync( command );
```

0.15 publishes command text only as `cmdline_url`. Shell-specific `%q` `cmdline=` is deliberately not exposed.

The semantic distinction is:

```text
CommandLine == null  -> bare C
CommandLine == ""    -> C;cmdline_url=
otherwise            -> C;cmdline_url=<encoded-value>
```

The encoder validates well-formed Unicode, converts with strict UTF-8, emits only RFC 3986 unreserved bytes literally (`A-Z a-z 0-9 - . _ ~`), and percent-encodes every other UTF-8 byte as uppercase `%HH`. The maximum encoded OSC 133 payload is 65,536 bytes; oversize input is rejected before output and is never truncated.

### Command-line privacy

Command lines can contain passwords, bearer tokens, private paths, host names, environment values, or other sensitive information. Terminal emulators and shell-integration/history features may retain or expose published metadata.

Percent encoding protects OSC framing; it does **not** provide confidentiality. `Icod.Terminal` does not inspect shell history/process command lines, infer sensitivity, or automatically redact secrets. Applications should publish command-line metadata only when that disclosure is appropriate.

### OSC 133 lifecycle and ordering

Semantic markers remain ephemeral output metadata:

- no session-open automatic OSC 133 emission;
- no background OSC 133 listener/support cache;
- no lifecycle lease or resume replay;
- no synthetic completion/abort on disposal;
- no OSC 133 traffic from `InvalidateState()`;
- no implicit shell command-region state machine.

All marker calls use the existing `TerminalSession` output serialization domain. Pre-commit cancellation emits nothing; committed frames are written as one non-cancellable transport write and are not implicitly flushed.

## Lifecycle-safe colors

0.14 introduced query-before-mutate scoped ownership for indexed palette colors and seven selected dynamic-color identities. That contract remains stable in 0.15.

```csharp
TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

await using TerminalPaletteColorLease palette =
	await session.AcquirePaletteColorAsync(
		1,
		TerminalColor.FromRgb8( 255, 64, 64 ),
		timeout
	);

await using TerminalDynamicColorLease cursor =
	await session.AcquireDynamicColorAsync(
		TerminalDynamicColor.TextCursor,
		TerminalColor.FromRgb8( 64, 255, 64 ),
		timeout
	);
```

The first owner queries the real external color before mutation. Final release explicitly replays that observed value. OSC 104 and OSC 110–119 remain terminal-policy resets and are not used as restoration.

The ownership exclusion remains manager-family-wide: any active palette lease blocks unscoped palette set/reset operations; any active dynamic-color lease blocks unscoped dynamic set/reset operations. Explicit observation remains allowed.

## Downstream Icod.DCurses acceptance

The real `Icod.DCurses 0.1.0` acceptance suite runs on net8/net9/net10.

For 0.15, the semantic-prompt acceptance retains the portable OSC 133 sequence and additionally runs a typed extended sequence through an owned `TerminalSession`, requiring real `CursesSession.RefreshAsync()` output between semantic boundaries. No raw OSC shortcut or side-channel writer is used.

The stable public contracts are recorded in:

- [`docs/Public-API-Baseline-0.15.md`](docs/Public-API-Baseline-0.15.md) — OSC 133 extended semantic metadata;
- [`docs/Public-API-Baseline-0.14.md`](docs/Public-API-Baseline-0.14.md) — lifecycle-safe color ownership/restoration.

## Previous release highlights

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

## Samples

Focused samples include:

- [`Icod.Terminal.SemanticPrompt.Sample`](samples/Icod.Terminal.SemanticPrompt.Sample/) — portable and typed extended OSC 133 metadata, including command-line privacy guidance;
- [`Icod.Terminal.Color.Sample`](samples/Icod.Terminal.Color.Sample/) — observable colors plus lifecycle-safe scoped ownership;
- pointer shape, progress, synchronized output, cursor style, clipboard, hyperlink, location, title, active-query, rich-input, and minimal live-session samples.

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

Distribution validation builds/tests the solution, runs real downstream `Icod.DCurses` acceptance, packs the NuGet artifact, verifies package structure/XML documentation, and runs fresh package-only consumers.

The retained 0.8–0.14 package contracts and the new 0.15 OSC 133 extended semantic-metadata contract run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.15.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all real downstream `Icod.DCurses` acceptance gates green;
4. retained 0.8–0.14 plus new 0.15 XML/package-only smoke gates green on all supported TFMs;
5. merge to `main`;
6. Release validation green on the resulting exact `main` commit;
7. only then create tag `v0.15.0`.

The tagged workflow reruns build/tests, downstream acceptance, exact package selection, historical package contracts, and the 0.15 semantic-metadata package contract before publication to NuGet.org and GitHub Packages.

## Development roadmap

The 0.15 milestone is documented in [`Icod.Terminal-0.15.0-Development-Roadmap.md`](Icod.Terminal-0.15.0-Development-Roadmap.md), with tranche records T150–T157 under `docs/`.

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
