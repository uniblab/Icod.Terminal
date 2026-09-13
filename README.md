# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is a managed, cross-platform live-terminal session and terminal-control library for .NET. It sits between immutable terminal capability data from `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.13.0` is the current stable line. It adds bounded relative persistent-raster placement ownership with immutable parentage, signed terminal-cell offsets, a portable depth ceiling of 8, descendant-first graph cleanup, and separate raster-resource versus parent-placement lifetime ownership.

The stable `1.0.0` compatibility floor remains unchanged. Existing 1.12 current-cursor persistent placement, source cropping, and signed z-order remain compatible when the relative APIs are unused. Version 1.13 adds no public protocol ids/backend selector, reparenting, scene graph, automatic raster replay, or production dependency.

## Installation

```text
dotnet add package Icod.Terminal --version 1.13.0
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

Version 1.11 added:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

This remains the persistent ownership capability in 1.13. It is distinct from ordinary `RasterGraphics`: verified Sixel may satisfy ephemeral raster display, while persistent resource ownership requires the reviewed persistent-capable Kitty Graphics path.

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

Version 1.11 established opaque persistent resource/placement ownership. Version 1.12 added bounded source-pixel cropping and signed z-order. Version 1.13 adds relative placement while keeping resource ownership independent from parent-placement lifetime.

An ordinary placement still uses the terminal's current cursor location:

```csharp
TerminalControlResult<TerminalRasterResource> parentResourceResult =
	await session.CreateRasterResourceAsync( image );

await using TerminalRasterResource parentResource =
	parentResourceResult.GetRequiredValue();

TerminalControlResult<TerminalRasterPlacement> parentResult =
	await parentResource.CreatePlacementAsync(
		new TerminalRasterPlacementOptions {
			Columns = 24,
			ZIndex = -1
		}
	);

await using TerminalRasterPlacement parent =
	parentResult.GetRequiredValue();
```

A second independently owned resource can create a placement relative to that parent:

```csharp
TerminalControlResult<TerminalRasterResource> childResourceResult =
	await session.CreateRasterResourceAsync( image );

await using TerminalRasterResource childResource =
	childResourceResult.GetRequiredValue();

TerminalControlResult<TerminalRasterPlacement> childResult =
	await childResource.CreateRelativePlacementAsync(
		parent,
		columnOffset: 2,
		rowOffset: -1,
		new TerminalRasterPlacementOptions {
			SourceRectangle = new TerminalRasterSourceRectangle(
				0,
				0,
				1,
				1
			),
			Columns = 12,
			Rows = 6,
			ZIndex = 1
		}
	);

await using TerminalRasterPlacement child =
	childResult.GetRequiredValue();

TerminalControlMutationResult update = await child.UpdateRelativeAsync(
	columnOffset: -3,
	rowOffset: 2,
	new TerminalRasterPlacementOptions {
		Columns = 10,
		Rows = 5,
		ZIndex = 2
	}
);
```

Parentage is immutable. Relative offsets are signed terminal-cell offsets, and the portable relative-depth ceiling is 8. `UpdateAsync(...)` preserves the established positioning mode; on a relative placement it retains the immutable parent and last acknowledged offsets while replacing common geometry. `UpdateRelativeAsync(...)` changes offsets and common geometry but never the parent.

Resource ownership and parent-placement lifetime are separate axes. Disposing a parent placement removes its relative descendant placements deepest-first, but a raster resource used by one of those descendants remains independently owned until separately disposed or invalidated.

Resource and placement identity remains opaque. The public API does not expose Kitty image ids, image numbers, placement ids, parent numeric identities, raw APC commands, or backend selection. There is no public reparent API.

`Columns` and `Rows` remain independently optional in `1..16384`. `SourceRectangle` is measured in source-image pixels and must fit completely within the resource. `ZIndex` accepts the full signed 32-bit range. Relative offsets do not create absolute screen-coordinate layout, pixel-within-cell positioning, or scene ownership.

Persistent identities remain session-generation scoped. Explicit invalidation and lifecycle generation changes stale the complete graph. Stale cleanup is local-only; the library does not retain hidden raster copies for replay or re-upload.

Live ownership remains bounded to 256 persistent resources and 4096 placements per session. Relative placement adds only the portable depth-8 graph bound; it does not raise either capacity ceiling.

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

Relative persistent placement adds a bounded local lifetime graph but does not add another terminal reader or transaction manager. Current subtree cleanup is descendant-before-parent and uses each placement's own owning-resource identity.

### Truthful capability evidence

Static description evidence and generation-scoped live observations remain distinct. Timeout is not automatically unsupported truth; endpoint unavailability is separate from support knowledge; negative evidence for one backend does not erase an independent viable alternate.

### Bounded protocol and graphics state

Terminal-controlled input is untrusted. Control-frame parsing, query state, semantic-event buffering, raster dimensions/storage, protocol payloads, persistent resource registries, placement registries, and relative placement depth have explicit ceilings.

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
- backend-neutral persistent raster resources and placements with generation-scoped ownership, source-pixel cropping, signed z-order, and bounded immutable-parent relative placement;
- optional consumer-owned TermInfo lifecycle planning integration without adding Inspection to the production package graph.

The library deliberately does not expose generic raw vendor dispatch as the ordinary extension model.

## Samples

The [`samples`](samples/README.md) directory contains focused examples. Recommended starting points include:

- `Icod.Terminal.Sample` — session construction and event reading;
- `Icod.Terminal.RichInput.Sample` — text, keys, paste, focus, mouse, lifecycle, and semantic events;
- `Icod.Terminal.CapabilityPlanning.Sample` — inspect-first semantic planning plus optional explicit verification;
- `Icod.Terminal.Query.Sample` — bounded terminal queries;
- `Icod.Terminal.RasterGraphics.Sample` — backend-neutral ephemeral raster display;
- `Icod.Terminal.PersistentRaster.Sample` — ordinary and relative persistent placement, signed offsets, crop/z-order, immutable parentage, and deterministic subtree/resource cleanup without protocol ids/backend branching;
- `Icod.Terminal.TermInfoPersistentRaster.Sample` — static TermInfo lifecycle plan, optional live Terminal verification, caller-owned replan, and persistent execution;
- focused state, color, notification, prompt, and shell-integration samples described in the sample catalog.

Focused sample verifiers build newer semantic/raster examples on every supported target framework during repository validation.

## Security and privacy

Terminal protocol traffic is external input/output. `Icod.Terminal` validates and bounds semantic protocol data and avoids generic raw vendor-command/event APIs as the normal extension mechanism.

`InspectCapability(...)` emits no terminal traffic. `VerifyCapabilityAsync(...)` is explicit precisely because it may send bounded queries whose responses can reveal terminal/environment characteristics. `Verified` is support evidence, not authentication.

Persistent raster acknowledgement correlation establishes transaction ownership, not trust. A terminal may independently evict stored image data; correlated missing-resource responses invalidate only the affected certainty rather than triggering hidden replay. Relative parent-loss classification invalidates the affected placement subtree without automatically declaring the parent raster resource missing.

Kitty direct transfer remains the reviewed persistent transport. Version 1.13 does not silently use file, temporary-file, or shared-memory transport and does not retain arbitrary source images after successful creation. Source rectangles select already-owned source pixels; relative placement adds no filesystem or external-memory transport.

Several APIs intentionally publish caller-supplied metadata such as filesystem locations, hyperlinks, clipboard contents, notifications, shell metadata, command lines, and raster pixels. Applications decide what is appropriate to disclose.

See [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md).

## Compatibility

Stable `1.0.0` remains the compatibility floor. Versions 1.1–1.4 added compatible semantic protocol surfaces; 1.5 and 1.6 normalized internal control/query infrastructure; 1.7 introduced backend-neutral raster display; 1.8 added Kitty Graphics beneath that surface; 1.9 added protocol-neutral semantic events; 1.10 added semantic capability planning; 1.11 added opaque persistent raster resource/placement ownership; 1.11.1 qualified the optional TermInfo persistent-raster lifecycle integration boundary; 1.12 added bounded source-pixel cropping and signed z-order; and 1.13 adds bounded immutable-parent relative placement ownership while preserving ordinary current-cursor placement when the new APIs are unused.

The final 1.13 public API fingerprint is:

```text
c9dc8b86dc1e8beed7161f1f5a122dce67a9187d3f4ee0b85ad5b49f09bd0da9
```

See [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md). Consumers upgrading from the pre-1.0 line should also review [`docs/Migration-to-1.0.md`](docs/Migration-to-1.0.md).

## Documentation

Start with:

- [1.13.0 release notes](docs/releases/1.13.0.md)
- [Persistent Raster Ownership](docs/Persistent-Raster-Ownership.md)
- [Capability Inspection and Planning](docs/Capability-Inspection-and-Planning.md)
- [1.13.0 development roadmap](Icod.Terminal-1.13.0-Development-Roadmap.md)
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