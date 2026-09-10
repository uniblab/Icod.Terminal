# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is a managed, cross-platform live-terminal session and terminal-control library for .NET. It sits between immutable terminal capability data from `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.8.0` is the current published stable release. It completed the 1.5–1.8 control-language and raster program by adding **Kitty Graphics over APC** as a second backend beneath the backend-neutral raster API introduced with Sixel in 1.7.

This branch prepares the `1.8.1` maintenance release. The maintenance scope is deliberately non-feature-bearing: documentation cleanup, improved sample coverage, and validation of those samples. No public API or terminal-runtime behavior change is intended.

## Installation

For the published stable package:

```text
dotnet add package Icod.Terminal --version 1.8.0
```

The package targets:

```text
net8.0
net9.0
net10.0
```

and depends on `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`.

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
terminal applications
```

- `Icod.TermInfo` owns immutable terminal capability data and terminfo expansion.
- `Icod.Terminal` owns the live terminal conversation: endpoint observation, native modes, input decoding, lifecycle, active query routing, capability evidence, semantic terminal output, protocol framing/routing, raster output, and scoped/reversible terminal state.
- `Icod.DCurses` owns higher-level cells, windows, virtual-screen state, refresh/diff policy, and curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## Quick start

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync( "Terminal session ready.\r\n" );

TerminalEvent terminalEvent = await session.ReadEventAsync(
	TimeSpan.FromSeconds( 1 )
);
```

A live `TerminalSession` owns the authoritative input reader for its transport. Use `ReadEventAsync(...)` and typed query APIs rather than introducing a competing `Console.Read*` or stream reader on the same terminal conversation.

For a curses-style virtual screen, prefer `Icod.DCurses` rather than rebuilding cell/window/refresh policy directly over `TerminalSession`.

## Raster graphics

The public raster API is semantic and backend-neutral:

```csharp
TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
	2,
	1,
	[
		255, 0, 0,
		0, 0, 255
	]
);

TerminalControlMutationResult result = await session.DisplayRasterAsync( image );
```

Supported public storage forms are:

```text
Rgb24      tightly packed R G B
Rgba32     tightly packed R G B A
Indexed8   one-byte palette indices + RGBA8 palette
```

The session resolves the semantic operation through verified capability evidence:

```text
RasterGraphics
    -> verified Kitty Graphics / APC
    -> verified Sixel / DCS
```

Kitty Graphics is preferred when verified; verified Sixel remains the fallback. Terminal brand, `TERM`, operating-system identity, and caller preference are not treated as capability proof.

The raster contract deliberately does not expose raw DCS/Sixel or APC/Kitty dispatch, backend selection, persistent Kitty image identifiers, placement/scaling, source rectangles, z-order, Unicode placeholders, deletion, animation, or image-file decoding/transcoding.

## Core 1.x guarantees

### One authoritative input path

Application input, lifecycle traffic, and terminal query responses share one coordinated input domain. Queries are bounded, correlation-aware, and do not create competing readers.

### Bounded protocol handling

Terminal-controlled input is untrusted. Control-frame parsing, query state, raster dimensions/storage, and protocol payloads have explicit resource ceilings. A timeout is not automatically interpreted as proof that a capability is unsupported.

### Reversible ownership

Scoped state uses leases where overlapping ownership matters. Documentation distinguishes exact restoration, terminal-policy reset, Icod-owned nested state, and ephemeral metadata/output rather than treating them as interchangeable.

`TerminalSession.DisposeAsync()` remains final cleanup/restoration authority for session-owned state.

### Serialized semantic output

Ordinary terminal-aware output should use session semantic operations. `TerminalSession.Output` is an advanced borrowed transport and is outside normal session serialization when used directly by callers.

Committed multi-frame graphics operations do not intentionally truncate after commitment, and a partial transport failure is surfaced without blind replay or automatic backend switching.

## Feature highlights

The stable 1.x surface includes:

- application text and resolved terminfo capability output;
- terminal input, lifecycle events, bracketed paste, focus, mouse, and negotiated modern keyboard reporting;
- bounded Primary/Secondary DA, DSR, CPR, DECRQSS, XTGETTCAP, color, pointer, clipboard, and notification queries;
- terminal titles (OSC 0/1/2) and current location (OSC 7);
- hyperlinks (OSC 8) and clipboard/selection operations (OSC 52);
- cursor-style observation/ownership (DECSCUSR/DECRQSS);
- synchronized output (DEC private mode 2026);
- terminal progress (OSC 9;4) and pointer shape (OSC 22);
- desktop notifications through the reviewed OSC 9, OSC 777, and Kitty OSC 99 surfaces;
- portable semantic prompt/command metadata (OSC 133);
- typed VS Code shell integration (OSC 633);
- typed iTerm2 shell-integration/semantic-history metadata (OSC 1337);
- indexed palette and selected dynamic terminal colors (OSC 4/104, 10–14, 17, 19 and resets);
- backend-neutral raw raster display with verified Sixel and Kitty Graphics backends.

## Samples

The [`samples`](samples/README.md) directory contains focused, buildable examples grouped by consumer goal. Recommended starting points are:

- `Icod.Terminal.Sample` — session construction and basic event reading;
- `Icod.Terminal.RichInput.Sample` — text, keys, paste, focus, mouse, and modern keyboard reporting;
- `Icod.Terminal.Query.Sample` — bounded terminal queries;
- `Icod.Terminal.RasterGraphics.Sample` — backend-neutral raster display without image-decoder dependencies;
- `Icod.Terminal.SemanticPrompt.Sample` — portable prompt/command metadata;
- `Icod.Terminal.VsCodeShellIntegration.Sample` and `Icod.Terminal.ITerm2ShellIntegration.Sample` — explicit vendor-specific shell-integration examples.

Focused sample verifiers build the newer protocol and raster examples on every supported target framework during repository validation.

## Security and privacy

Terminal protocol traffic is external input/output and must be treated accordingly. `Icod.Terminal` validates and bounds semantic protocol data before commitment where the contract permits it and deliberately avoids generic raw vendor-command APIs as the normal extension mechanism.

Several APIs intentionally publish caller-supplied metadata, including filesystem locations, hyperlinks, clipboard contents, notification text, shell metadata, and command lines. The library does not automatically discover or redact secrets; applications decide what is appropriate to disclose to the terminal.

Kitty Graphics uses direct transfer in 1.8. File, temporary-file, and shared-memory graphics transports are intentionally excluded, avoiding hidden filesystem or IPC side effects merely for performance.

See [Security and Privacy](docs/Security-and-Privacy.md).

## Compatibility

Stable `1.0.0` remains the compatibility floor. Versions 1.1–1.4 added compatible semantic protocol surfaces; 1.5 and 1.6 normalized internal control-language/query infrastructure; 1.7 introduced the public raster contract; and 1.8 added Kitty Graphics beneath that unchanged raster surface.

Version 1.8 retained the 1.7 public API fingerprint:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

The `1.8.1` maintenance work is intended to retain that public surface unchanged.

See [Compatibility and Versioning](docs/Compatibility-and-Versioning.md).

## Documentation

Start with:

- [Current development roadmap](Icod.Terminal-Development-Roadmap.md)
- [1.8.0 release notes](docs/releases/1.8.0.md)
- [1.8.0 development roadmap](Icod.Terminal-1.8.0-Development-Roadmap.md)
- [Control-language normalization and graphics roadmap](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)
- [Architecture](docs/Architecture.md)
- [Input and Events](docs/Input-and-Events.md)
- [Security and Privacy](docs/Security-and-Privacy.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Samples](samples/README.md)
- [Changelog](CHANGELOG.md)

Historical development records remain in the repository for design evidence, while the root README and current roadmap are intentionally maintained as concise consumer/contributor entry points.

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License, version 3 or later. Sample applications are licensed under the GNU General Public License, version 3 or later, as stated in their source headers.
