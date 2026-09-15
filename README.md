# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is a managed, cross-platform live-terminal session and terminal-control library for .NET. It sits between immutable terminal capability data from `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.14.0` is the current stable line. It adds side-effect-free persistent-raster lifecycle observability through one atomic, backend-neutral ownership snapshot with `Current`, `Stale`, `Released`, and `Disposed` states plus semantic loss/release reasons.

The stable `1.0.0` compatibility floor remains unchanged. Existing 1.13 relative-placement ownership, 1.12 source cropping/signed z-order, and earlier current-cursor persistent placement behavior remain compatible. The published 1.14 package uses `Icod.TermInfo 1.13.0` and `Icod.Timing 1.0.0`.

The active `1.15.0` development line adds Unicode placeholder / virtual raster placement support and advances the direct development dependency to `Icod.TermInfo 1.14.0`. Optional TermInfo integration tests and samples use `Icod.TermInfo.Inspection 1.14.0`; Inspection remains outside the production package graph.

## Support the Project

`Icod.Terminal` and its ecosystem packages (`Icod.TermInfo` and `Icod.DCurses`) are built and maintained by a solo developer. If these packages save you or your team time, please consider supporting their continued development and maintenance.

[![GitHub Sponsors](https://img.shields.io/badge/GitHub-Sponsor?logo=githubsponsors)](https://github.com/sponsors/uniblab)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support?logo=kofi)](https://ko-fi.com/TimothyBruce)
[![PayPal](https://img.shields.io/badge/PayPal-Support?logo=paypal)](https://paypal.me/uniblab)

## Installation

```text
dotnet add package Icod.Terminal --version 1.14.0
```

The package targets:

```text
net8.0
net9.0
net10.0
```

The published 1.14 production dependency graph is:

```text
Icod.TermInfo 1.13.0
Icod.Timing   1.0.0
```

The active 1.15 development graph is:

```text
Icod.TermInfo 1.14.0
Icod.Timing   1.0.0
```

`Icod.Terminal.csproj` is the package authority for direct NuGet dependencies. Tests, samples, and auxiliary tools may add qualification-only dependencies such as `Icod.TermInfo.Inspection 1.14.0`, but those do not widen the production package graph.

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
- `Icod.Terminal` owns the live terminal conversation: endpoint observation, modes, input decoding, lifecycle, active queries, unsolicited semantic events, semantic capability evidence/planning, semantic output, raster output, persistent raster resource/placement ownership and lifecycle certainty, protocol framing/routing, and reversible/scoped terminal state.
- `Icod.DCurses` owns higher-level cells, windows, virtual-screen state, layout, refresh/diff policy, damage, and curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

`Icod.TermInfo.Inspection` may be used by consumers for richer static planning, but it remains an optional consumer/test/sample dependency rather than a dependency of the `Icod.Terminal` package itself. Inspection's raster-backend planner is advisory caller policy and is not used by Icod.Terminal's production router.

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

This remains the persistent ownership capability. It is distinct from ordinary `RasterGraphics`: verified Sixel may satisfy ephemeral raster display, while persistent resource ownership requires the reviewed persistent-capable Kitty Graphics path.

The 1.15 development line additionally defines:

```text
TerminalCapability.UnicodeRasterPlaceholders = 10
```

This semantic capability represents virtual-placement ownership plus typed Unicode-placeholder cell rendering; it is not inferred merely from ordinary raster or persistent-raster support.

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

Version 1.11 established opaque persistent resource/placement ownership. Version 1.12 added bounded source-pixel cropping and signed z-order. Version 1.13 added relative placement while keeping resource ownership independent from parent-placement lifetime. Version 1.14 makes Terminal's current ownership certainty observable without exposing private identities or inventing a passive terminal-side existence query.

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

Resource ownership and parent-placement lifetime are separate axes. Disposing a parent placement removes its relative descendant placements deepest-first, but a raster resource used by one of those descendants remains independently owned until separately disposed or invalidated. The persistent-raster sample demonstrates this concretely by creating a new ordinary placement from the surviving child resource after the parent cascade.

### Persistent ownership observation — 1.14

Both opaque handle types expose one synchronous immutable snapshot:

```csharp
TerminalRasterOwnershipState resourceState = childResource.OwnershipState;
TerminalRasterOwnershipState placementState = child.OwnershipState;
```

The status vocabulary is:

```text
Current
Stale
Released
Disposed
```

and the semantic reason vocabulary is:

```text
None
SessionStateLost
ResourceMissing
ParentPlacementLost
AncestorReleased
ResourceReleased
ExplicitDisposal
```

`Current` means Icod.Terminal still owns current local certainty for that handle. It is deliberately **not** terminal-authenticated proof that the remote object still exists at the instant of observation. A correlated missing-resource response can move a resource and affected placements to `Stale / ResourceMissing`; correlated parent-loss evidence can move an affected placement subtree to `Stale / ParentPlacementLost` without falsely declaring its raster resources missing.

`Released` is distinct from `Stale`. If a relative placement dies because its parent was explicitly disposed, that child wrapper reports `Released / AncestorReleased` while an independently owned raster resource behind the child can remain `Current / None` and create another placement. Explicitly disposing the wrapper itself then reports `Disposed / ExplicitDisposal`.

Reading `OwnershipState` emits no terminal bytes, registers no query, acquires no output ownership, triggers no cleanup, and performs no replay/re-upload. State/reason are observed atomically and lifecycle transitions do not resurrect stale, released, or disposed ownership.

Resource and placement identity remains opaque. The public API does not expose Kitty image ids, image numbers, placement ids, parent numeric identities, session generation ids, raw APC commands, or backend selection. There is no public reparent API.

`Columns` and `Rows` remain independently optional in `1..16384`. `SourceRectangle` is measured in source-image pixels and must fit completely within the resource. `ZIndex` accepts the full signed 32-bit range. Relative offsets do not create absolute screen-coordinate layout, pixel-within-cell positioning, or scene ownership.

Persistent identities remain session-generation scoped. Explicit invalidation and lifecycle generation changes stale the complete graph. Stale cleanup is local-only; the library does not retain hidden raster copies for replay or re-upload.

Live ownership remains bounded to 256 persistent resources and 4096 placements per session. Relative placement adds only the portable depth-8 graph bound; it does not raise either capacity ceiling.

See [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md).

### Unicode raster placeholders — 1.15 development

The 1.15 development line adds opaque virtual placements and semantic current-cursor placeholder cells while keeping protocol identity and encoding private. `TerminalRasterPlaceholder` reuses the persistent ownership vocabulary; `TerminalRasterPlaceholderCell` exposes semantic row/column coordinates only. Applications and higher-level renderers own screen cursor position, clipping, scrolling, damage, and redraw order.

A physical placement may use a current virtual placeholder as immutable relative parent through `CreateRelativePlacementFromPlaceholderAsync(...)`, while the virtual placeholder itself is not relative. Virtual and physical placements share the same bounded 4096-placement ownership budget.

See [`samples/Icod.Terminal.RasterPlaceholder.Sample`](samples/Icod.Terminal.RasterPlaceholder.Sample/README.md) and [`docs/Public-API-Baseline-1.15.md`](docs/Public-API-Baseline-1.15.md).

### TermInfo persistent-raster lifecycle, placement, and backend integration

Version 1.11.1 introduced the loose-coupling pattern between TermInfo lifecycle planning and Terminal-owned live verification/execution. The current executable integration sample uses `Icod.TermInfo.Inspection 1.14.0`. It retains the lifecycle boundary, continues to use the advanced-placement planner introduced in TermInfo 1.12, and now exercises TermInfo 1.14's advisory raster-backend evidence and selection layer without making Inspection a production dependency:

```text
session.Terminal
    -> TermInfo lifecycle inspection / plan
    -> if Indeterminate, optionally VerifyCapabilityAsync(PersistentRasterGraphics)
    -> caller-owned Verified lifecycle evidence
    -> reclassify / replan lifecycle
    -> TermInfo advanced-placement inspection / plan
    -> if placement semantics are Unknown, caller-owned Terminal-contract evidence
    -> reclassify / replan placement
    -> preserve separate backend contexts
    -> caller-map conclusive live PersistentRasterGraphics evidence to Kitty availability
    -> build Sixel and Kitty candidates
    -> TermInfo backend plan without hidden ranking
    -> explicit caller Kitty-first preference
    -> if Kitty selected and live route usable, execute Terminal geometry values
```

The lifecycle evidence bridge promotes only conclusive live observations. `Unknown`, `Advertised`, unrelated capabilities, and endpoint unavailability remain distinct and are not converted into verified support or non-support. The advanced-placement bridge is deliberately separate: the coarse `PersistentRasterGraphics` observation is not mislabeled as a source-rectangle or z-order probe.

Backend availability is another independent axis. The sample maps live `PersistentRasterGraphics` to Kitty availability only because Icod.Terminal's reviewed persistent implementation is Kitty-based. Ordinary `RasterGraphics` does not identify a concrete backend, and `UnicodeRasterPlaceholders` is not treated as TermInfo 1.14 lifecycle/placement evidence. Sixel retains its own static backend evidence and unstrengthened semantic context. `RasterBackendPlanner` remains caller-side advisory policy; Terminal's production routing and commitment logic is unchanged.

TermInfo owns semantic evidence, classification, and advisory planning; the application and Terminal own concrete geometry values, acknowledgements, execution, relative-parent lifetime, lifecycle observation, virtual-placeholder ownership, and protocol commitment. TermInfo 1.14 does not plan Terminal's relative-placement or Unicode-placeholder graphs.

See [`samples/Icod.Terminal.TermInfoPersistentRaster.Sample`](samples/Icod.Terminal.TermInfoPersistentRaster.Sample/README.md) for current executable documentation and [`docs/releases/1.11.1.md`](docs/releases/1.11.1.md) for the historical integration-patch contract.

## Core 1.x guarantees

### One authoritative input path

Application input, semantic events, lifecycle traffic, terminal query responses, capability verification, and persistent-raster acknowledgements share one coordinated session. No feature opens a competing terminal reader.

### Deterministic ownership

Active query responses have first refusal on matching framed traffic. Recognized unsolicited semantic reports are classified next, followed by ordinary application input. A frame is never intentionally double-delivered.

Relative persistent placement adds a bounded local lifetime graph but does not add another terminal reader or transaction manager. Current subtree cleanup is descendant-before-parent and uses each placement's own owning-resource identity. Lifecycle observation reads already-owned local state only and introduces no additional protocol path.

### Truthful capability evidence

Static description evidence and generation-scoped live observations remain distinct. Timeout is not automatically unsupported truth; endpoint unavailability is separate from support knowledge; negative evidence for one backend does not erase an independent viable alternate.

### Bounded protocol and graphics state

Terminal-controlled input is untrusted. Control-frame parsing, query state, semantic-event buffering, raster dimensions/storage, protocol payloads, persistent resource registries, placement registries, relative placement depth, virtual placeholder dimensions/identity, and lifecycle observation state have explicit bounds.

### Reversible and generation-scoped ownership

Scoped reversible terminal state uses leases where overlapping ownership matters. `TerminalSession.DisposeAsync()` remains final cleanup/restoration authority.

Persistent raster resources, physical placements, and virtual placeholders are generation-scoped terminal-resident ownership rather than exactly restorable state. The session cleans current identity, invalidates lost certainty, and does not silently retain/replay source images.

### Committed output integrity

Committed multi-frame graphics operations do not intentionally truncate after commitment. Partial transport failure is surfaced without blind replay or automatic backend switching.

## Feature highlights

The stable 1.x surface plus the current additive 1.15 development work includes:

- protocol-neutral semantic capability inspection and explicit bounded verification;
- application text and resolved terminfo capability output;
- terminal input, lifecycle events, bracketed paste, focus, mouse, and negotiated modern keyboard reporting;
- unsolicited protocol-neutral semantic events;
- bounded device/status/cursor/style/color/clipboard/notification queries;
- titles, current location, hyperlinks, clipboard operations, cursor style, synchronized output, progress, pointer shape, notifications, prompt/shell metadata, and terminal colors;
- backend-neutral ephemeral raster display through verified Sixel and Kitty Graphics;
- backend-neutral persistent raster resources and placements with generation-scoped ownership, source-pixel cropping, signed z-order, bounded immutable-parent relative placement, and side-effect-free lifecycle certainty observation;
- opaque virtual raster placeholders plus typed self-contained placeholder-cell output under caller-owned text-grid layout;
- optional consumer-owned TermInfo lifecycle, advanced-placement, and explicit raster-backend planning integration without adding Inspection to the production package graph.

The library deliberately does not expose generic raw vendor dispatch as the ordinary extension model.

## Samples

The [`samples`](samples/README.md) directory contains focused examples. Recommended starting points include:

- `Icod.Terminal.Sample` — session construction and event reading;
- `Icod.Terminal.RichInput.Sample` — text, keys, paste, focus, mouse, lifecycle, and semantic events;
- `Icod.Terminal.CapabilityPlanning.Sample` — inspect-first semantic planning plus optional explicit verification;
- `Icod.Terminal.Query.Sample` — bounded terminal queries;
- `Icod.Terminal.RasterGraphics.Sample` — backend-neutral ephemeral raster display;
- [`Icod.Terminal.PersistentRaster.Sample`](samples/Icod.Terminal.PersistentRaster.Sample/README.md) — ordinary and relative persistent placement, both relative update modes, signed offsets, crop/z-order, immutable parentage, observable parent-cascade release, and independent descendant-resource reuse without protocol ids/backend branching;
- [`Icod.Terminal.RasterPlaceholder.Sample`](samples/Icod.Terminal.RasterPlaceholder.Sample/README.md) — virtual placement ownership, typed current-cursor placeholder cells, sparse/out-of-order rendering, and physical placement relative to a virtual parent;
- `Icod.Terminal.TermInfoPersistentRaster.Sample` — Inspection 1.14 lifecycle/placement/backend planning, optional live Terminal verification, explicit caller evidence/preference, and concrete persistent execution;
- focused state, color, notification, prompt, and shell-integration samples described in the sample catalog.

Focused sample verifiers build newer semantic/raster examples on every supported target framework during repository validation.

## Security and privacy

Terminal protocol traffic is external input/output. `Icod.Terminal` validates and bounds semantic protocol data and avoids generic raw vendor-command/event APIs as the normal extension mechanism.

`InspectCapability(...)` emits no terminal traffic. `VerifyCapabilityAsync(...)` is explicit precisely because it may send bounded queries whose responses can reveal terminal/environment characteristics. `Verified` is support evidence, not authentication.

Persistent raster acknowledgement correlation establishes transaction ownership, not trust. A terminal may independently evict stored image data; correlated missing-resource responses invalidate only the affected certainty rather than triggering hidden replay. Relative parent-loss classification invalidates the affected placement subtree without automatically declaring the parent raster resource missing. `OwnershipState` reports those local semantic conclusions without issuing another probe or claiming terminal authentication.

Kitty direct transfer remains the reviewed persistent transport. The library does not silently use file, temporary-file, or shared-memory transport and does not retain arbitrary source images after successful creation. Source rectangles select already-owned source pixels; relative placement, lifecycle observation, and virtual-placeholder presentation add no filesystem or external-memory transport.

TermInfo Inspection backend planning is advisory evidence/policy. It does not authenticate a terminal or replace Terminal's live routing/commitment checks. Mapping Terminal observations into backend evidence is application-owned and must not strengthen unrelated capabilities.

Several APIs intentionally publish caller-supplied metadata such as filesystem locations, hyperlinks, clipboard contents, notifications, shell metadata, command lines, and raster pixels. Applications decide what is appropriate to disclose.

See [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md).

## Compatibility

Stable `1.0.0` remains the compatibility floor. Versions 1.1–1.4 added compatible semantic protocol surfaces; 1.5 and 1.6 normalized internal control/query infrastructure; 1.7 introduced backend-neutral raster display; 1.8 added Kitty Graphics beneath that surface; 1.9 added protocol-neutral semantic events; 1.10 added semantic capability planning; 1.11 added opaque persistent raster resource/placement ownership; 1.11.1 qualified the optional TermInfo persistent-raster lifecycle integration boundary; 1.12 added bounded source-pixel cropping and signed z-order; 1.13 added bounded immutable-parent relative placement ownership; 1.14 added side-effect-free lifecycle certainty observation; and the current 1.15 development line adds opaque Unicode-placeholder virtual placement while retaining the stable compatibility floor.

The final 1.14 public API fingerprint is:

```text
2a23205217183a602f8fc454c49b47d278ebdc26b5e358c0384ed0d692405696
```

The frozen 1.15 development API fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```

See [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md). Consumers upgrading from the pre-1.0 line should also review [`docs/Migration-to-1.0.md`](docs/Migration-to-1.0.md).

## Documentation

Start with:

- [1.14.0 release notes](docs/releases/1.14.0.md)
- [Persistent Raster Ownership](docs/Persistent-Raster-Ownership.md)
- [Capability Inspection and Planning](docs/Capability-Inspection-and-Planning.md)
- [1.15.0 public API baseline](docs/Public-API-Baseline-1.15.md)
- [1.15.0 development roadmap](Icod.Terminal-1.15.0-Development-Roadmap.md)
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
