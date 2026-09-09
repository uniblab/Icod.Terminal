# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is the managed, cross-platform live-terminal session layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.8.0` is the current development release line. It builds on the completed 1.7 DCS/Sixel raster release and develops **Kitty Graphics over APC** as the second backend beneath the existing backend-neutral raster API.

The 1.8 program is designed to preserve the 1.7 public raster surface while adding:

- a canonical bounded APC construction layer;
- a typed Kitty Graphics control-data and response grammar;
- direct raw RGB24/RGBA32 transfer with bounded Base64 chunks;
- internal Indexed8 adaptation without changing the public raster model;
- committed multi-frame APC graphics output through the existing session-output boundary;
- protocol-defined Kitty capability probing through the authoritative multi-family query path;
- deterministic evidence-driven routing which prefers verified Kitty Graphics and retains verified Sixel as fallback.

The stable 1.0 architecture, ownership, lifecycle, input/query, restoration, security, and compatibility guarantees remain the floor for the 1.x line. The public `TerminalRasterImage`, `TerminalRasterColor`, `TerminalRasterPixelFormat`, and `TerminalSession.DisplayRasterAsync(...)` contract introduced by 1.7 remains the compatibility anchor.

Release and design documents:

- [Icod.Terminal 1.8.0 release notes](docs/releases/1.8.0.md)
- [1.8.0 development roadmap](Icod.Terminal-1.8.0-Development-Roadmap.md)
- [A180 APC construction contract](docs/A180-APC-Construction-Contract-and-Reference-Freeze.md)
- [Icod.Terminal 1.7.0 release notes](docs/releases/1.7.0.md)
- [1.7.0 development roadmap](Icod.Terminal-1.7.0-Development-Roadmap.md)
- [D170 DCS construction contract](docs/D170-DCS-Construction-Contract-and-Reference-Freeze.md)
- [D171 existing DCS reconciliation](docs/D171-Existing-DCS-Reconciliation.md)
- [D172 Sixel grammar and codec](docs/D172-Sixel-Grammar-and-Codec-Contract.md)
- [D173 common raw raster model](docs/D173-Common-Raw-Raster-Model.md)
- [D174 deterministic Sixel quantization](docs/D174-Deterministic-Sixel-Palette-and-Quantization.md)
- [D175 Sixel encoder](docs/D175-Sixel-Encoder.md)
- [D176 committed streaming graphics output](docs/D176-Committed-Streaming-Graphics-Output.md)
- [D177 Sixel capability evidence](docs/D177-Sixel-Capability-Evidence-and-Live-Observation.md)
- [D178 first semantic raster-display operation](docs/D178-First-Semantic-Raster-Display-Operation.md)
- [D179 1.7.0 hardening/package/documentation closure](docs/D179-1.7.0-Hardening-Package-and-Documentation-Closure.md)
- [Public API Baseline 1.7](docs/Public-API-Baseline-1.7.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Migration to 1.0](docs/Migration-to-1.0.md)
- [Architecture](docs/Architecture.md)
- [Control-language normalization and graphics roadmap](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)
- [Changelog](CHANGELOG.md)

## Installation

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

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns the live terminal conversation: endpoint observation, terminal modes, input decoding, lifecycle, active query routing, semantic output, protocol framing/routing, raster output, and scoped/reversible terminal state. `Icod.DCurses` owns the higher-level virtual-screen/curses presentation model.

PTY/process hosting remains orthogonal to this package.

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

For a curses-style virtual screen, prefer `Icod.DCurses` rather than rebuilding windows/cells/diff policy directly over `TerminalSession`.

## Raster graphics in 1.7

The public raster API is deliberately semantic and backend-neutral.

### Raw raster formats

```text
Rgb24      tightly packed R G B
Rgba32     tightly packed R G B A
Indexed8   one-byte palette indices + RGBA8 palette
```

Create an immutable owned raster snapshot with the factory matching your storage:

```csharp
TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
	2,
	1,
	[
		255, 0, 0,
		0, 0, 255
	]
);
```

The constructor/factory path copies caller storage, so asynchronous output cannot observe later mutation of the supplied buffers.

The public model exposes dimensions, pixel format, pixel count, and typed per-pixel color inspection. It intentionally does **not** expose mutable backing buffers or Sixel command data.

### Displaying a raster

```csharp
TerminalControlMutationResult result = await session.DisplayRasterAsync( image );
if ( !result.Succeeded ) {
	Console.Error.WriteLine( result.Message );
}
```

In version 1.7, the semantic raster operation is implemented by Sixel only. The session requires verified Sixel capability evidence before committing graphics output. If such evidence is not already available, it may issue the bounded Primary Device Attributes probe used by D177.

Primary DA attribute `4` verifies the Sixel backend. A valid DA response without `4`, or probe silence/timeout, remains uncertainty rather than fabricated proof that Sixel is unsupported.

The Sixel backend preserves fully transparent and fully opaque raster semantics. Fractional alpha remains valid backend-neutral raster data, but version 1.7 returns controlled `Unsupported` when Sixel cannot preserve it rather than silently compositing against an invented background.

### Raster bounds

The raw raster contract is bounded:

```text
maximum dimension       16,384
maximum pixels          16 Mi
maximum owned pixel data 64 MiB
maximum indexed palette 256 entries
```

Quantization uses bounded work state and deterministic output. Sixel payloads are emitted as bounded segments rather than one complete encoded-image allocation.

### Committed graphics output

Sixel output participates in the normal session output gate. Caller cancellation is honored before commitment. Once the DCS prefix has committed, ordinary caller cancellation is no longer allowed to truncate the control string; the transaction continues through its final ST and flush unless the transport itself fails.

Transport failure after commitment is surfaced to the caller. The library does not retry a partially written image or guess whether a terminator reached the terminal.

Version 1.7 deliberately does not expose:

- a generic raw DCS or Sixel writer;
- a public Sixel-backend selector;
- Sixel color-register manipulation;
- image placement/scaling options that cannot yet be preserved across backends;
- PNG/JPEG/GIF decoding;
- animation or persistent image identifiers.

Version 1.8 develops Kitty Graphics behind this same semantic raster intent rather than replacing the public image model.

## Core 1.x guarantees

### One authoritative input path

A live `TerminalSession` owns the only authoritative input reader for its transport. Ordinary consumers use:

- `ReadEventAsync(...)` for application input and lifecycle events;
- typed query operations for terminal responses.

Do not run a competing `Console.Read*`, stream read, or retained custom-transport read on the same live terminal conversation.

### Bounded query routing

Queries share the same incremental input/router domain as application input. The contract includes bounded parser state, ambiguity-aware query serialization, finite caller timeouts, pre-emission versus post-emission cancellation semantics, bounded late-response ownership, and lifecycle query generations.

A timeout is not automatically proof that the terminal does not support a feature.

### Reversible ownership

Scoped state uses leases where overlapping ownership matters. The 1.x documentation distinguishes:

1. **exact restoration** — a captured/observed external baseline is replayed exactly;
2. **terminal-policy reset** — control returns to terminal policy without claiming the exact previous value;
3. **Icod-owned nested state** — an outer library-owned value can be restored even when the pre-Icod state is not observable;
4. **ephemeral metadata** — explicit output with no lifecycle replay/restoration state.

`TerminalSession.DisposeAsync()` remains final cleanup/restoration authority for session-owned state. Version 1.7 additionally ensures teardown drains a committed Sixel transaction before output-state restoration proceeds.

### Output serialization boundary

Use session semantic operations for ordinary terminal output. `TerminalSession.Output` remains available only as an advanced borrowed transport and is outside normal session serialization when used directly by callers.

`WriteTerminalStringAsync(...)` is intended for already-resolved terminfo capability strings; it is not a recommendation to construct arbitrary OSC/CSI/DCS/APC/vendor traffic manually.

## Control-language normalization

Version 1.5 separates five layers which had previously been easy to conflate:

```text
semantic intent
    -> capability/evidence resolution
    -> protocol backend selection
    -> control family framing
    -> dialect codec / wire transport
```

The normalized control-family vocabulary is:

```text
CSI  ESC [
DCS  ESC P
OSC  ESC ]
APC  ESC _
PM   ESC ^
SOS  ESC X
ST   ESC \
```

Version 1.7 uses that architecture directly: Sixel is classified as a DCS dialect, while `RasterGraphics` is the semantic operation. Version 1.8 applies the same separation to Kitty Graphics as an APC dialect. The library does not expose generic control-family framing merely because internal normalization exists.

Examples:

```text
DesktopNotification
    -> OSC 9
    -> OSC 777
    -> OSC 99

RasterGraphics
    -> Kitty Graphics / APC (1.8 in development)
    -> Sixel / DCS
```

A terminal/vendor name is not itself a capability. Static TermInfo advertisement and generation-scoped live evidence are distinct. `InvalidateState()` expires live probe/protocol-response conclusions while retaining immutable selected profile/TermInfo evidence.

## Other semantic terminal features

The stable surface also includes:

- application text and resolved terminfo capability output;
- terminal titles (OSC 0/1/2);
- current-location publication (OSC 7);
- hyperlinks (OSC 8);
- clipboard/selection operations and explicit reads (OSC 52);
- cursor style observation/ownership (DECSCUSR/DECRQSS);
- synchronized output (DEC private mode 2026);
- terminal progress (OSC 9;4);
- terminal pointer shape (OSC 22);
- typed Kitty desktop notifications and notification queries (OSC 99);
- portable semantic prompt/command metadata (OSC 133);
- typed VS Code shell integration (OSC 633);
- typed titled desktop notifications (OSC 777);
- typed iTerm2 shell-integration/semantic-history metadata (OSC 1337);
- indexed palette and selected dynamic terminal colors (OSC 4/104, 10–14, 17, 19 and resets);
- negotiated modern keyboard reporting;
- bracketed paste, focus, and mouse input protocols;
- bounded safe OSC 9 notification and Windows-CWD compatibility operations.

The safe OSC 9 subset intentionally excludes host-affecting vendor commands for sleep/blocking UI, GUI macros, process launch, environment disclosure, and emulator mutation.

## Modern keyboard reporting

Modern keyboard reporting is opt-in through the compound input-protocol lease. Traditional keyboard decoding remains the compatibility floor.

The public modes are semantic:

```text
Disambiguated
EventTypes
AllKeys
```

Kitty support is negotiated before reversible ownership is acquired. xterm `modifyOtherKeys` remains decode-only and is not blindly activated.

## Security and privacy

Semantic APIs validate and bound terminal protocol data before commitment where the contract permits it. The library deliberately avoids generic raw vendor-command APIs as the ordinary extension mechanism.

Terminal traffic and query responses are untrusted external input. Successful byte transmission does not prove terminal-side application unless the protocol provides and the library receives an explicit correlated response.

Raster-specific security properties include bounded dimensions/storage/work state, verified capability gating, deterministic quantization, pre-commit validation, committed-frame integrity, no silent retry of partial output, and no automatic image-file decoding.

For 1.8, Kitty Graphics direct transfer is preferred specifically to avoid introducing hidden filesystem, temporary-file, or shared-memory side effects merely for performance. Those transmission modes remain outside the initial release contract.

Several existing operations disclose caller-supplied metadata by design, including clipboard contents, filesystem locations, hyperlinks, shell metadata, notification text/metadata, and rich input events. The library does not automatically discover or redact secrets; applications decide what is appropriate to publish.

See [Security and Privacy](docs/Security-and-Privacy.md).

## Compatibility policy

Stable `1.0.0` remains the compatibility floor. Versions 1.1–1.4 added compatible OSC 633, OSC 777, OSC 1337, and OSC 99 surfaces. Versions 1.5 and 1.6 were internal architecture releases and retained the 1.4 public fingerprint. Version 1.7 intentionally added the compatible raster surface and advanced the current public API baseline to:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Version 1.8 begins from that public surface and is intended to implement Kitty Graphics beneath it without source-breaking changes. Historical baselines remain checked in unchanged. See [Public API Baseline 1.7](docs/Public-API-Baseline-1.7.md) and [Compatibility and Versioning](docs/Compatibility-and-Versioning.md).

For stable 1.x:

- patch releases fix/harden the documented contract without intentionally breaking it;
- minor releases may add compatible API/semantic features with an intentional baseline update;
- ordinary removals, renames, signature breaks, enum renumbering, or incompatible ownership/security/restoration changes require a new major release.

Vendor runtime EOL alone is not sufficient reason to drop net8.0 or net9.0; a concrete security/toolchain/maintenance blocker is required.

## Platform support

The built-in `SystemTerminalControlProvider` supports:

- Windows;
- Linux;
- macOS.

Other hosts receive controlled `Unsupported` results from the built-in provider. Custom implementations may be supplied through `ITerminalControlProvider`, `ITerminalInput`, and `ITerminalOutput`.

## Permanent documentation

Primary permanent authorities include:

- [Architecture](docs/Architecture.md)
- [Terminal Session and Ownership](docs/Terminal-Session-and-Ownership.md)
- [Lifecycle and Restoration](docs/Lifecycle-and-Restoration.md)
- [Input and Events](docs/Input-and-Events.md)
- [Queries and Responses](docs/Queries-and-Responses.md)
- [Presentation and Reversible State](docs/Presentation-and-Reversible-State.md)
- [Semantic Output Protocols](docs/Semantic-Output-Protocols.md)
- [Control-Language Normalization and Graphics Roadmap](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)
- [A180–A189 1.8 roadmap](Icod.Terminal-1.8.0-Development-Roadmap.md)
- [D170–D179 1.7 contracts](Icod.Terminal-1.7.0-Development-Roadmap.md)
- [Security and Privacy](docs/Security-and-Privacy.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Public API Baseline 1.7](docs/Public-API-Baseline-1.7.md)
- [Migration to 1.0](docs/Migration-to-1.0.md)

Historical T-series, N150–N159, C160–C165, 0.x baselines, and earlier stable baselines remain available as design/release evidence.

## Samples

Repository samples are indexed by task in [`samples/README.md`](samples/README.md). They cover session basics, rich input, queries, scoped presentation/state, colors, titles, location, hyperlinks, clipboard, semantic prompt metadata, notifications, and shell metadata.

## Build and validation

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

PR validation runs Windows/Linux/macOS runtime/source validation, the current machine public-API fingerprint, one portable package candidate, and four parallel package-contract shards retaining historical and stable 1.x contracts.

The semantic package shard compiles and runs fresh NuGet-only consumers on `net8.0`, `net9.0`, and `net10.0`, including the 1.7 raster consumer, and verifies generated XML documentation for the corresponding public APIs.

The Stable 1.x release-line shard retains the current `Icod.DCurses` integration/ownership package-boundary witness. These downstream checks complement rather than replace Terminal's own API, invariant, unit/hardening, and package gates.

After merge, Release distribution validation runs Windows/Linux/macOS x64/ARM64 runtime jobs plus the portable package and package-contract shards.

## Release process

`1.8.0` is publishable only after:

1. the exact final 1.8 PR head passes the complete Staging matrix;
2. the PR is explicitly merged;
3. the resulting `main` head passes Release distribution validation;
4. tagging/publication is explicitly authorized.

The tag-triggered workflow requires curated `docs/releases/1.8.0.md` release notes and re-runs public API, hardening, historical package, stable release-line package, semantic package consumers, and downstream compatibility gates before publication.

Tagging triggers publication; no release tag should be created merely because a PR is green.

## Development roadmap

Current release status is tracked in [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md). The detailed current release program is [`Icod.Terminal-1.8.0-Development-Roadmap.md`](Icod.Terminal-1.8.0-Development-Roadmap.md). Completed 1.7, 1.6, and 1.5 programs remain preserved in their versioned roadmaps.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and ncurses.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

The published `Icod.Terminal` library package and the C# sources compiled into the library are licensed under **LGPL-3.0-or-later**.

Repository executable/test programs—including `Icod.Terminal.Tests`, samples, package smoke tests, validation utilities, and downstream acceptance tools—are licensed under **GPL-3.0-or-later**. Their GPL license does not change the LGPL license of the reusable `Icod.Terminal` library they consume.

The root [`LICENSE`](LICENSE) contains the LGPLv3 terms and the incorporated GPLv3 terms. See [Licensing](docs/Licensing.md) for the project-by-project policy and source-header requirements.