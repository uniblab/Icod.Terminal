# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is a managed, cross-platform live-terminal session and terminal-control library for .NET. It sits between immutable terminal capability data from `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.12.0` is the current stable line. It extends the backend-neutral persistent terminal-resident raster placement API with bounded source-pixel rectangles and signed z-order while preserving opaque identities, acknowledged create/update, generation-scoped ownership, deterministic cleanup, and the existing production dependency graph.

The stable `1.0.0` compatibility floor remains unchanged. `Icod.Terminal` continues to preserve one authoritative live input path, bounded query/protocol handling, lifecycle-aware ownership, deterministic cleanup, and protocol-neutral public planning surfaces. Version 1.12 adds no public backend selector, scene graph, automatic replay, or production dependency.

## Installation

```text
dotnet add package Icod.Terminal --version 1.12.0
```

The package targets:

```text
net8.0
net9.0
net10.0
```

`Icod.Terminal.csproj` is the package authority for direct NuGet dependencies. Tests, samples, and auxiliary tools do not independently pin transitive runtime versions merely to duplicate package metadata.

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
- `Icod.Terminal` owns the live terminal conversation: endpoint observation, modes, input decoding, lifecycle, active queries, unsolicited semantic events, semantic capability evidence/planning, semantic output, raster output, persistent raster resource/placement ownership, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns higher-level cells, windows, virtual-screen state, layout, refresh/diff policy, damage, and curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

`Icod.TermInfo.Inspection` may be used by consumers for richer static planning, but it remains an optional consumer/test/sample dependency rather than a dependency of the `Icod.Terminal` package itself.

See [`docs/Architecture.md`](docs/Architecture.md).

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

## Semantic capability planning

The stable planning API asks about semantic operations rather than protocol backends, terminal brands, or dependency provenance.

Side-effect-free inspection reads current session knowledge only:

```csharp
TerminalCapabilityStatus status = session.InspectCapability(
	TerminalCapability.RasterGraphics
);
```

When stronger evidence is worth bounded terminal traffic, verification is explicit:

```csharp
status = await session.VerifyCapabilityAsync(
	TerminalCapability.RasterGraphics,
	cancellationToken
);
```

`TerminalCapabilityStatus` keeps support, endpoint availability, evidence lifetime, and current usability separate. Silence is not automatically `Unsupported`, and an unavailable endpoint does not erase truthful support knowledge.

Version 1.11 adds:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

This is distinct from ordinary `RasterGraphics`: verified Sixel may satisfy ephemeral raster display, while persistent resource ownership requires the reviewed persistent-capable Kitty Graphics path.

See [`docs/Capability-Inspection-and-Planning.md`](docs/Capability-Inspection-and-Planning.md).

## Raster graphics

### Ephemeral display

The public raw-raster API remains backend-neutral:

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

Supported public storage forms are `Rgb24`, `Rgba32`, and `Indexed8` with an RGBA8 palette. The session can resolve ordinary raster display through verified Kitty Graphics or verified Sixel without exposing backend selection publicly.

### Persistent resources and placements

Version 1.11 established a separate ownership model for terminal-resident raster data; version 1.12 adds bounded source-pixel cropping and relative z-order to the existing placement options:

```csharp
TerminalCapabilityStatus capability = await session.VerifyCapabilityAsync(
	TerminalCapability.PersistentRasterGraphics
);

if ( capability.IsUsable ) {
	TerminalControlResult<TerminalRasterResource> resourceResult =
		await session.CreateRasterResourceAsync( image );

	await using TerminalRasterResource resource =
		resourceResult.GetRequiredValue();

	TerminalControlResult<TerminalRasterPlacement> placementResult =
		await resource.CreatePlacementAsync(
			new TerminalRasterPlacementOptions {
				SourceRectangle = new TerminalRasterSourceRectangle(
					0,
					0,
					1,
					1
				),
				Columns = 24,
				ZIndex = -1
			}
		);

	await using TerminalRasterPlacement placement =
		placementResult.GetRequiredValue();

	TerminalControlMutationResult update = await placement.UpdateAsync(
		new TerminalRasterPlacementOptions {
			SourceRectangle = new TerminalRasterSourceRectangle(
				1,
				0,
				1,
				1
			),
			Columns = 16,
			ZIndex = 1
		}
	);
}
```

Resource and placement identity is opaque. The public API does not expose Kitty image ids, image numbers, placement ids, raw APC commands, or backend selection.

`Columns` and `Rows` are independently optional and each supplied value is bounded to `1..16384`. `SourceRectangle` is measured in source-image pixels, must fit completely within the resource, and is validated before placement output. `ZIndex` is nullable and accepts the full signed 32-bit range. Placement still uses the terminal's current cursor location and does not move the text cursor; source cropping and z-order do not create an absolute layout or scene-graph contract.

Persistent identities are session-generation scoped. Explicit invalidation and lifecycle generation changes stale existing handles. Version 1.12 does not retain hidden raster copies for automatic replay or re-upload after suspend/resume uncertainty.

The library bounds live ownership to 256 persistent resources and 4096 placements per session. Current cleanup deletes placements before resource data; stale cleanup is local-only and never emits stale numeric protocol identities.

See [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

### TermInfo persistent-raster lifecycle integration

Version 1.11.1 demonstrates how a consumer can combine `Icod.TermInfo.Inspection 1.11.0` static lifecycle planning with Terminal-owned live verification without making Inspection a production dependency:

```text
session.Terminal
    -> TermInfo lifecycle inspection
    -> plan
    -> if Indeterminate, optionally VerifyCapabilityAsync(PersistentRasterGraphics)
    -> caller-owned Verified lifecycle evidence
    -> reclassify / replan
    -> if Success and live Terminal state is usable, execute resource/placement operations
```

The consumer-owned evidence bridge promotes only conclusive live observations. `Unknown`, `Advertised`, unrelated capabilities, and endpoint unavailability remain distinct and are not converted into verified support or non-support.

See [`samples/Icod.Terminal.TermInfoPersistentRaster.Sample`](samples/Icod.Terminal.TermInfoPersistentRaster.Sample/README.md) for executable documentation and [`docs/releases/1.11.1.md`](docs/releases/1.11.1.md) for the integration-patch contract.

## Core 1.x guarantees

### One authoritative input path

Application input, semantic events, lifecycle traffic, terminal query responses, capability verification, and persistent-raster acknowledgements share one coordinated session. No feature opens a competing terminal reader.

### Deterministic ownership

Active query responses have first refusal on matching framed traffic. Recognized unsolicited semantic reports are classified next, followed by ordinary application input. A frame is never intentionally double-delivered.

### Truthful capability evidence

Static description evidence and generation-scoped live observations remain distinct. Timeout is not automatically unsupported truth; endpoint unavailability is separate from support knowledge; negative evidence for one backend does not erase an independent viable alternate.

### Bounded protocol and graphics state

Terminal-controlled input is untrusted. Control-frame parsing, query state, semantic-event buffering, raster dimensions/storage, protocol payloads, persistent resource registries, and placement registries have explicit ceilings.

### Reversible and generation-scoped ownership

Scoped reversible terminal state uses leases where overlapping ownership matters. `TerminalSession.DisposeAsync()` remains final cleanup/restoration authority.

Persistent raster resources are different from exactly restorable state: they are terminal-resident, generation-scoped objects. The session cleans them while identity is current, invalidates them when terminal certainty is lost, and does not silently retain/replay source images.

### Committed output integrity

Committed multi-frame graphics operations do not intentionally truncate after commitment. Partial transport failure is surfaced without blind replay or automatic backend switching.

## Feature highlights

The stable 1.x surface includes:

- protocol-neutral semantic capability inspection and explicit bounded verification;
- application text and resolved terminfo capability output;
- terminal input, lifecycle events, bracketed paste, focus, mouse, and negotiated modern keyboard reporting;
- unsolicited protocol-neutral semantic events;
- bounded device/status/cursor/style/color/clipboard/notification queries;
- titles, current location, hyperlinks, clipboard operations, cursor style, synchronized output, progress, pointer shape, notifications, prompt/shell metadata, and terminal colors;
- backend-neutral ephemeral raster display through verified Sixel and Kitty Graphics;
- backend-neutral persistent raster resources and placements with bounded generation-scoped ownership, source-pixel cropping, and signed z-order;
- optional consumer-owned TermInfo lifecycle planning integration without adding Inspection to the production package graph.

The library deliberately does not expose generic raw vendor dispatch as the ordinary extension model.

## Samples

The [`samples`](samples/README.md) directory contains focused examples. Recommended starting points include:

- `Icod.Terminal.Sample` — session construction and event reading;
- `Icod.Terminal.RichInput.Sample` — text, keys, paste, focus, mouse, lifecycle, and semantic events;
- `Icod.Terminal.CapabilityPlanning.Sample` — inspect-first semantic planning plus optional explicit verification;
- `Icod.Terminal.Query.Sample` — bounded terminal queries;
- `Icod.Terminal.RasterGraphics.Sample` — backend-neutral ephemeral raster display;
- `Icod.Terminal.PersistentRaster.Sample` — verify, create, crop/place, update z-order, and dispose persistent raster ownership without protocol ids/backend branching;
- `Icod.Terminal.TermInfoPersistentRaster.Sample` — static TermInfo lifecycle plan, optional live Terminal verification, caller-owned replan, and persistent execution;
- focused state, color, notification, prompt, and shell-integration samples described in the sample catalog.

Focused sample verifiers build newer semantic/raster examples on every supported target framework during repository validation.

## Security and privacy

Terminal protocol traffic is external input/output. `Icod.Terminal` validates and bounds semantic protocol data and avoids generic raw vendor-command/event APIs as the normal extension mechanism.

`InspectCapability(...)` emits no terminal traffic. `VerifyCapabilityAsync(...)` is explicit precisely because it may send bounded queries whose responses can reveal terminal/environment characteristics. `Verified` is support evidence, not authentication.

Persistent raster acknowledgement correlation establishes transaction ownership, not trust. A terminal may independently evict stored image data; correlated `ENOENT` invalidates local terminal-resident certainty rather than triggering hidden replay.

Kitty direct transfer remains the reviewed persistent transport. Version 1.12 does not silently use file, temporary-file, or shared-memory transport and does not retain arbitrary source images after successful creation. Source rectangles select already-owned source pixels; they do not create a new filesystem or external-memory transport.

Several APIs intentionally publish caller-supplied metadata such as filesystem locations, hyperlinks, clipboard contents, notifications, shell metadata, command lines, and raster pixels. Applications decide what is appropriate to disclose.

See [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md).

## Compatibility

Stable `1.0.0` remains the compatibility floor. Versions 1.1–1.4 added compatible semantic protocol surfaces; 1.5 and 1.6 normalized internal control/query infrastructure; 1.7 introduced backend-neutral raster display; 1.8 added Kitty Graphics beneath that surface; 1.9 added protocol-neutral semantic events; 1.10 added semantic capability planning; 1.11 added opaque persistent raster resource/placement ownership; 1.11.1 qualified the optional TermInfo persistent-raster lifecycle integration boundary; and 1.12 adds bounded source-pixel cropping and signed z-order without widening ownership into scene layout.

The final 1.12 public API fingerprint is:

```text
eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

See [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md). Consumers upgrading from the pre-1.0 line should also review [`docs/Migration-to-1.0.md`](docs/Migration-to-1.0.md).

## Documentation

Start with:

- [1.12.0 release notes](docs/releases/1.12.0.md)
- [Persistent Raster Ownership](docs/Persistent-Raster-Ownership.md)
- [Capability Inspection and Planning](docs/Capability-Inspection-and-Planning.md)
- [1.12.0 development roadmap](Icod.Terminal-1.12.0-Development-Roadmap.md)
- [Current development roadmap](Icod.Terminal-Development-Roadmap.md)
- [Architecture](docs/Architecture.md)
- [Input and Events](docs/Input-and-Events.md)
- [Queries and Responses](docs/Queries-and-Responses.md)
- [Security and Privacy](docs/Security-and-Privacy.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Migration to 1.0](docs/Migration-to-1.0.md)
- [Samples](samples/README.md)
- [Changelog](CHANGELOG.md)

Historical release and tranche records remain in the repository for design evidence, while this README and the current roadmap are maintained as consumer/contributor entry points.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and `ncurses`.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License, version 3 or later. Sample applications are licensed under the GNU General Public License, version 3 or later, as stated in their source headers.