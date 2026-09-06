# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.13.0` is the current stable release. It adds typed, observable terminal-color control for the indexed palette and the useful non-Tektronix xterm dynamic-color family.

The release covers:

- OSC 4 indexed palette mutation/query;
- OSC 104 indexed palette reset;
- OSC 10 / 110 default foreground;
- OSC 11 / 111 default background;
- OSC 12 / 112 text cursor;
- OSC 13 / 113 mouse foreground;
- OSC 14 / 114 mouse background;
- OSC 17 / 117 highlight background;
- OSC 19 / 119 highlight foreground.

OSC 15/16/18 and resets 115/116/118 remain deliberately excluded as Tektronix-specific dynamic colors.

## Installation

```text
dotnet add package Icod.Terminal --version 0.13.0
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

## Quick start — observable terminal colors

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

TerminalColor paletteRed = await session.QueryPaletteColorAsync(
	1,
	timeout
);

TerminalColor foreground = await session.QueryDynamicColorAsync(
	TerminalDynamicColor.DefaultForeground,
	timeout
);
```

`TerminalColor` preserves normalized 16-bit RGB channels:

```csharp
TerminalColor color = new(
	0x1234,
	0x5678,
	0x9abc
);

TerminalColor fromBytes = TerminalColor.FromRgb8(
	0x12,
	0x34,
	0x56
);
```

`FromRgb8(...)` expands bytes by multiplication by 257, so `0x12` becomes `0x1212`.

## Indexed palette — OSC 4 / 104

Single mutation:

```csharp
await session.SetPaletteColorAsync(
	1,
	TerminalColor.FromRgb8( 255, 64, 64 )
);
```

Bounded multi-entry mutation:

```csharp
await session.SetPaletteColorsAsync(
	[
		new TerminalPaletteColor( 1, TerminalColor.FromRgb8( 255, 0, 0 ) ),
		new TerminalPaletteColor( 2, TerminalColor.FromRgb8( 0, 255, 0 ) )
	]
);
```

Observation:

```csharp
TerminalColor color = await session.QueryPaletteColorAsync(
	1,
	TimeSpan.FromMilliseconds( 750 )
);
```

Terminal-policy reset:

```csharp
await session.ResetPaletteColorAsync( 1 );
await session.ResetPaletteColorsAsync( [ 1, 2, 3 ] );
await session.ResetPaletteAsync();
```

Bulk OSC 4 mutation is bounded to 256 distinct entries, rejects duplicates and empty collections, validates before commitment, and emits one complete frame.

## Dynamic colors — OSC 10–14, 17, 19

The semantic identities are:

```csharp
TerminalDynamicColor.DefaultForeground
TerminalDynamicColor.DefaultBackground
TerminalDynamicColor.TextCursor
TerminalDynamicColor.MouseForeground
TerminalDynamicColor.MouseBackground
TerminalDynamicColor.HighlightBackground
TerminalDynamicColor.HighlightForeground
```

Set, observe, and reset all use one semantic API family:

```csharp
await session.SetDynamicColorAsync(
	TerminalDynamicColor.TextCursor,
	TerminalColor.FromRgb8( 64, 255, 64 )
);

TerminalColor cursor = await session.QueryDynamicColorAsync(
	TerminalDynamicColor.TextCursor,
	TimeSpan.FromMilliseconds( 750 )
);

await session.ResetDynamicColorAsync(
	TerminalDynamicColor.TextCursor
);
```

The common/core interoperability tier is OSC 10/11/12. OSC 13/14/17/19 are documented as the extended xterm tier and may have lower support across terminal implementations.

## Color encoding and observation

Canonical outbound colors use exactly:

```text
rgb:rrrr/gggg/bbbb
```

with four lowercase hexadecimal digits per channel and ST (`ESC \\`) OSC termination.

Inbound color observations accept strict equal-width 1–4 digit `rgb:` components plus `#RGB`, `#RRGGBB`, `#RRRGGGBBB`, and `#RRRRGGGGBBBB`.

The two shorthand grammars intentionally normalize differently:

- `rgb:` components scale to the complete 16-bit range;
- hash components supply the most-significant bits and zero-fill the remaining low bits.

Named colors, `rgbi:`, CSS color syntax, alpha forms, mixed-width `rgb:` components, surrounding whitespace, and trailing junk are rejected.

## Query semantics

Color observation uses the existing session-owned active-query transaction/router.

- opening a session performs no automatic color probing;
- no second response reader is introduced;
- each query has an explicit finite timeout;
- caller cancellation remains distinct from timeout;
- correlated malformed color replies fail with `FormatException`;
- successful observations are not cached as authoritative terminal state;
- a timeout is not converted into a permanent “unsupported” capability result.

## Reset is not restoration

OSC 104 and OSC 110–119 request the terminal's configured/default policy. They are not exact restoration of a color previously observed by this library.

0.13 therefore deliberately exposes no palette-color or dynamic-color lease. Color mutation is unscoped: `InvalidateState()`, managed suspend/resume, and `DisposeAsync()` do not automatically query, reset, or replay color values.

A future lifecycle-safe color lease would need a truthful baseline and post-resume re-observation before reapplying owned state. 0.13 does not alter the core lifecycle/query ordering merely to simulate that guarantee.

## Downstream Icod.DCurses observation

The T137 downstream acceptance proves that `Icod.DCurses 0.1.0` can consume typed 16-bit `TerminalColor` observations without parsing raw OSC or opening another input path.

Current `Icod.DCurses` uses 8-bit `CursesColor.Rgb`, so the acceptance performs an explicit downstream precision adaptation and then renders observed colors through `setrgbf` / `setrgbb` capabilities. `Icod.Terminal` itself does not discard the observed 16-bit precision.

Color-distance metrics, nearest-palette selection, contrast/accessibility policy, and theme inference remain higher-level responsibilities.

The stable public contract is recorded in [`docs/Public-API-Baseline-0.13.md`](docs/Public-API-Baseline-0.13.md). Composition/downstream acceptance is recorded in [`docs/T137-Color-Composition-and-DCurses-Observation-Acceptance.md`](docs/T137-Color-Composition-and-DCurses-Observation-Acceptance.md).

## Previous release highlights

- **0.12** — OSC 133 semantic prompt/command-region markers. See [`docs/Public-API-Baseline-0.12.md`](docs/Public-API-Baseline-0.12.md).
- **0.11** — OSC 22 pointer shape. See [`docs/Public-API-Baseline-0.11.md`](docs/Public-API-Baseline-0.11.md).
- **0.10** — OSC 9;4 progress. See [`docs/Public-API-Baseline-0.10.md`](docs/Public-API-Baseline-0.10.md).
- **0.9** — DEC private mode 2026 synchronized output. See [`docs/Public-API-Baseline-0.9.md`](docs/Public-API-Baseline-0.9.md).
- **0.8** — DECSCUSR cursor style with observation/scoped restoration. See [`docs/Public-API-Baseline-0.8.md`](docs/Public-API-Baseline-0.8.md).
- **0.7** — OSC 52 clipboard/selections.
- **0.6** — OSC 8 hyperlinks.
- **0.5** — OSC 7 current location.
- **0.4** — OSC 0/1/2 title operations.

## Samples

Focused samples include:

- [`Icod.Terminal.Color.Sample`](samples/Icod.Terminal.Color.Sample/) — 0.13 observable palette/dynamic colors;
- [`Icod.Terminal.SemanticPrompt.Sample`](samples/Icod.Terminal.SemanticPrompt.Sample/) — 0.12 OSC 133;
- [`Icod.Terminal.PointerShape.Sample`](samples/Icod.Terminal.PointerShape.Sample/) — 0.11 OSC 22;
- [`Icod.Terminal.Progress.Sample`](samples/Icod.Terminal.Progress.Sample/) — 0.10 OSC 9;4;
- [`Icod.Terminal.SynchronizedOutput.Sample`](samples/Icod.Terminal.SynchronizedOutput.Sample/) — 0.9;
- [`Icod.Terminal.CursorStyle.Sample`](samples/Icod.Terminal.CursorStyle.Sample/) — 0.8;
- clipboard, hyperlink, location, title, active-query, rich-input, and minimal live-session samples.

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

Distribution validation builds/tests the solution, runs real downstream `Icod.DCurses` synchronized-output, progress, pointer-shape, semantic-prompt, and color-observation acceptance, packs the NuGet artifact, verifies package structure/XML documentation, and runs fresh package-only consumers.

The 0.8 through 0.13 package contracts restore and run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.13.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all five real downstream `Icod.DCurses` acceptance gates green;
4. retained 0.8–0.12 plus new 0.13 XML/package-only smoke gates green on all supported TFMs;
5. merge to `main`;
6. Release distribution validation green on the resulting exact `main` commit;
7. only then create tag `v0.13.0`.

The tagged workflow reruns build/tests, downstream acceptance, exact package selection, historical package contracts, and the 0.13 color package contract before publication to NuGet.org and GitHub Packages.

## Development roadmap

The 0.13 milestone is documented in [`Icod.Terminal-0.13.0-Development-Roadmap.md`](Icod.Terminal-0.13.0-Development-Roadmap.md), with tranche records T130–T138 under `docs/`.

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
