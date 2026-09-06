# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.14.0` adds lifecycle-safe scoped ownership and exact restoration for the terminal colors introduced in 0.13.

The stable color surface includes:

- OSC 4 indexed-palette set/query and lifecycle-safe scoped ownership;
- OSC 104 indexed-palette terminal-policy reset;
- OSC 10 / 110 default foreground;
- OSC 11 / 111 default background;
- OSC 12 / 112 text cursor;
- OSC 13 / 113 mouse foreground;
- OSC 14 / 114 mouse background;
- OSC 17 / 117 highlight background;
- OSC 19 / 119 highlight foreground;
- lifecycle-safe scoped ownership for all seven selected dynamic-color identities.

OSC 15/16/18 and resets 115/116/118 remain deliberately excluded as Tektronix-specific dynamic colors.

## Installation

```text
dotnet add package Icod.Terminal --version 0.14.0
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

## Quick start — lifecycle-safe colors

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

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

The first owner queries the real external color before mutation. Final release explicitly replays that observed value. Reset sequences are not used as restoration.

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

## Scoped ownership contract

Both ownership APIs are query-before-mutate:

```csharp
ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);

ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

The ownership model is identity-aware and ordered:

- the first owner captures the exact external baseline;
- later owners for the same identity nest without re-querying merely because of nesting;
- owners may be disposed out of order;
- releasing the controlling owner reapplies the next active owner;
- releasing the final owner restores the external baseline explicitly;
- different palette indices and dynamic-color identities remain independent;
- failed restoration retains logical cleanup responsibility so disposal can be retried.

`TerminalSession.InvalidateState()` marks physical color state uncertain without discarding logical ownership or the current lifecycle-epoch baseline.

Before managed suspend, active scoped colors restore their external baselines. After resume, old observations are no longer treated as authoritative: retained owners use the session's internal observation phase to establish fresh external baselines before owned colors are reapplied.

Session disposal is the final cleanup owner. Active color leases do not need to be disposed first, and late lease disposal after successful session cleanup is a no-op.

## Indexed palette — OSC 4 / 104

Unscoped set and observation remain available:

```csharp
await session.SetPaletteColorAsync(
	1,
	TerminalColor.FromRgb8( 255, 64, 64 )
);

TerminalColor color = await session.QueryPaletteColorAsync(
	1,
	TimeSpan.FromMilliseconds( 750 )
);
```

Bounded multi-entry mutation remains available through `SetPaletteColorsAsync(...)`.

Terminal-policy reset remains explicit:

```csharp
await session.ResetPaletteColorAsync( 1 );
await session.ResetPaletteColorsAsync( [ 1, 2, 3 ] );
await session.ResetPaletteAsync();
```

Unscoped palette mutation/reset is rejected while scoped palette ownership is active so it cannot silently invalidate the exact-restoration contract. Explicit observation remains allowed.

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

The common/core interoperability tier is OSC 10/11/12. OSC 13/14/17/19 are the extended xterm tier and may have lower support across terminal implementations.

Unscoped set/query/reset remain available, but set/reset are rejected while scoped dynamic-color ownership is active.

## Reset is not restoration

OSC 104 and OSC 110–119 request terminal policy/defaults. They do not mean “restore the value that existed before this application changed it.”

0.14 scoped leases restore by explicit set-form replay:

```text
palette: OSC 4 ; index ; observed-color ST
dynamic: OSC Ps ; observed-color ST
```

This distinction is part of the stable 0.14 contract.

## Query semantics

Color observation and baseline acquisition reuse the existing session-owned active-query transaction/router.

- opening a session performs no automatic global color probing;
- no second response reader is introduced;
- each query has an explicit finite timeout;
- caller cancellation remains distinct from timeout;
- correlated malformed color replies fail with `FormatException`;
- timeout is not converted into permanent unsupported state;
- unrelated application input remains on the normal session path.

Canonical outbound colors use `rgb:rrrr/gggg/bbbb` with four lowercase hexadecimal digits per channel. Inbound observations accept the documented strict `rgb:` and hash forms while rejecting unrelated color syntaxes.

## Downstream Icod.DCurses acceptance

The T146 downstream acceptance proves that `Icod.DCurses 0.1.0` can operate over a `TerminalSession` with active scoped palette/dynamic-color ownership, consume those owned colors through its normal RGB style path, render normally, and trigger exact baseline restoration when it disposes the `TerminalSession` it owns.

Current `Icod.DCurses` uses 8-bit `CursesColor.Rgb`, so downstream precision adaptation is explicit. `Icod.Terminal` itself preserves 16-bit channels.

The stable public contract is recorded in [`docs/Public-API-Baseline-0.14.md`](docs/Public-API-Baseline-0.14.md). T146 composition/downstream acceptance is recorded in [`docs/T146-Composition-and-DCurses-Scoped-Color-Acceptance.md`](docs/T146-Composition-and-DCurses-Scoped-Color-Acceptance.md).

## Previous release highlights

- **0.13** — observable OSC 4/104 and selected OSC 10–19 terminal colors. See [`docs/Public-API-Baseline-0.13.md`](docs/Public-API-Baseline-0.13.md).
- **0.12** — OSC 133 semantic prompt/command-region markers.
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

- [`Icod.Terminal.Color.Sample`](samples/Icod.Terminal.Color.Sample/) — observable colors plus lifecycle-safe scoped ownership;
- semantic prompt, pointer shape, progress, synchronized output, cursor style, clipboard, hyperlink, location, title, active-query, rich-input, and minimal live-session samples.

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

The retained 0.8–0.13 package contracts and the new 0.14 lifecycle-safe color ownership contract run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.14.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all real downstream `Icod.DCurses` acceptance gates green;
4. retained 0.8–0.13 plus new 0.14 XML/package-only smoke gates green on all supported TFMs;
5. merge to `main`;
6. Release validation green on the resulting exact `main` commit;
7. only then create tag `v0.14.0`.

The tagged workflow reruns build/tests, downstream acceptance, exact package selection, historical package contracts, and the 0.14 ownership package contract before publication to NuGet.org and GitHub Packages.

## Development roadmap

The 0.14 milestone is documented in [`Icod.Terminal-0.14.0-Development-Roadmap.md`](Icod.Terminal-0.14.0-Development-Roadmap.md), with tranche records T140–T147 under `docs/`.

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
