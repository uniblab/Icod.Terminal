# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is a managed, cross-platform live-terminal session and terminal-control library for .NET. It combines immutable terminal capability data from `Icod.TermInfo` with live endpoint observation, input, queries, semantic output, screen-operation planning, and terminal-state ownership for higher-level consumers such as TUI libraries, command-line tools, monitors, editors, pagers, and REPLs.

## Status

Current published stable release: `Icod.Terminal 1.17.1`.

Version `1.17.1` corrects the packaged README and release metadata for the 1.17 line. It makes no runtime or public-API change from `1.17.0`.

This source prepares the stable `1.18.0` candidate. Version 1.18 adds `TerminalScreenPlanner.PlanRenditionBaseline()`, allowing a Terminal-only renderer to establish the normalized default rendition safely when the physical starting state is unknown. The operation returns no plan when any profile-exposed rendition axis cannot be restored unconditionally.

Version 1.17 adds Terminal-owned dimensions, an immutable semantic terminal profile, side-effect-free screen-operation planning, and bounded session-bound output transactions. These contracts provide the Terminal-side boundary required for a later `Icod.DCurses 2.0` release to remove its direct `Icod.TermInfo` dependency.

The stable `1.0.0` compatibility floor remains unchanged. Version 1.18 retains the complete 1.17 screen-planning/transaction surface, 1.16 animation, 1.15 virtual-placeholder, and every earlier stable 1.x contract. The 1.18 public API fingerprint is `48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d`.

See the [1.18.0 release notes](docs/releases/1.18.0.md) and [changelog](CHANGELOG.md) for release-specific details. Until 1.18.0 is published, installation guidance below continues to name the published 1.17.1 package.

## Support the Project

`Icod.Terminal` and its ecosystem packages (`Icod.TermInfo` and `Icod.DCurses`) are built and maintained by a solo developer. If these packages save you or your team time, please consider supporting their continued development and maintenance.

[![GitHub Sponsors](https://img.shields.io/badge/GitHub-Sponsor?logo=githubsponsors)](https://github.com/sponsors/uniblab)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support?logo=kofi)](https://ko-fi.com/TimothyBruce)
[![PayPal](https://img.shields.io/badge/PayPal-Support?logo=paypal)](https://paypal.me/uniblab)

## Architecture

`Icod.Terminal` is the live-session layer of the Icod terminal stack. The intended `Icod.DCurses 2.0` dependency direction is:

```text
higher-level terminal applications
             |
        Icod.DCurses
             |
        Icod.Terminal
             |
        Icod.TermInfo
             |
       terminal / console
```

- `Icod.TermInfo` owns immutable terminal capability data, compiled terminfo acquisition, and capability expansion.
- `Icod.Terminal` owns the live terminal conversation: endpoint observation, native modes, input decoding, lifecycle, active queries, semantic events, capability evidence/planning, semantic output, raster execution, persistent raster resource/placement/placeholder/animation ownership, and reversible/scoped terminal state.
- `Icod.DCurses` owns higher-level cells, windows, pads, retained presentation state, layout, clipping, scrolling, refresh/diff policy, damage, and curses-style interaction abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

Published `Icod.DCurses 1.6.0` is the compatibility baseline and still directly references both `Icod.Terminal` and `Icod.TermInfo`. The planned 2.0 migration removes only the direct DCurses-to-TermInfo edge; `Icod.Terminal` continues to use TermInfo internally.

The direct production dependency graph is intentionally small:

```text
Icod.Terminal
├── Icod.TermInfo 1.15.0
└── Icod.Timing   1.0.0
```

`Icod.TermInfo.Inspection 1.15.0` is used only by optional integration tests and samples. It is not a production dependency of `Icod.Terminal`. Its raster-backend planner remains caller-side advisory policy rather than part of Terminal's production router.

See [`docs/Architecture.md`](docs/Architecture.md) for the permanent architecture contract.

## Quick Start

Install the currently published package:

```text
dotnet add package Icod.Terminal --version 1.17.1
```

Open a managed terminal session, write application text, and read through the authoritative event path:

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

A live `TerminalSession` owns the authoritative input reader for its transport. Use `ReadEventAsync(...)` and the typed query APIs rather than introducing a competing `Console.Read*` or stream reader on the same terminal conversation.

For curses-style cells, windows, layout, clipping, scrolling, and refresh/damage policy, prefer `Icod.DCurses` rather than rebuilding those responsibilities directly over `TerminalSession`.

Version 1.17 screen operations are planned without output and then committed through one session-bound transaction:

```csharp
TerminalProfile profile = session.Profile;
TerminalControlResult<TerminalDimensions> dimensions = session.GetDimensions();

TerminalScreenOperationPlan? home = session.Screen.PlanCursorMove(
	null,
	new TerminalScreenPosition( 0, 0 )
);

if ( home is TerminalScreenOperationPlan plan ) {
	TerminalScreenOutputTransaction output =
		session.CreateScreenOutputTransaction();
	output.Add( plan );
	output.WriteText( "Ready" );
	await output.CommitAsync();
}
```

When a renderer cannot trust its current physical rendition, version 1.18 can establish a safe baseline before emitting retained content:

```csharp
TerminalScreenOperationPlan? baseline =
	session.Screen.PlanRenditionBaseline();

if ( baseline is TerminalScreenOperationPlan plan ) {
	TerminalScreenOutputTransaction output =
		session.CreateScreenOutputTransaction();
	output.Add( plan );
	await output.CommitAsync();
}
```

A `null` result means at least one rendition axis exposed by the selected profile cannot be restored unconditionally from unknown state; callers must not substitute a claimed known default.

`Profile` contains immutable selected-profile facts, while `GetDimensions()` reports the current Terminal-owned size result. A plan is opaque and session-bound; creating it emits nothing, and the transaction preserves ordering under one output gate and flush boundary. Retained cells, layout, Unicode width, clipping, damage, and repaint policy remain caller-owned.

## Feature Inventory

The root README describes the current product by capability rather than by the release in which each feature first appeared.

- **Live terminal session and lifecycle** — terminal endpoint observation; native terminal-mode capture/mutation; deterministic session disposal; lifecycle invalidation; scoped/reversible state where exact restoration is supportable.
- **Unified input and events** — one authoritative reader for text, keys, bracketed paste, focus, mouse, lifecycle observations, active query responses, and unsolicited protocol-neutral semantic events.
- **Typed terminal queries** — bounded cursor, status, style, color, clipboard, notification, pointer, and related observations integrated with the same input/query authority.
- **Semantic capability planning** — side-effect-free `InspectCapability(...)`, explicit bounded `VerifyCapabilityAsync(...)`, separate support/evidence/endpoint-availability state, and no terminal-brand heuristics as capability truth.
- **Semantic terminal output** — application text, titles, current location, hyperlinks, clipboard operations, notifications, cursor style, synchronized output, progress, pointer shape, prompt/shell metadata, and terminal color operations.
- **Semantic screen planning** — Terminal-owned dimensions/profile facts plus opaque costed cursor, rendition, ACS, erase, shift, scroll, and region plans; retained-screen comparison and layout remain caller-owned.
- **Session-bound screen transactions** — bounded ordered composition of screen plans, application text, strict hyperlinks, and raster-placeholder cells under one output gate and flush boundary, with optional synchronized framing.
- **Reversible presentation state** — scoped ownership for terminal features whose prior state can be observed truthfully and restored deterministically.
- **Backend-neutral ephemeral raster display** — bounded `TerminalRasterImage` data with verified Sixel and Kitty Graphics routing behind one semantic `DisplayRasterAsync(...)` surface.
- **Persistent raster ownership** — opaque terminal-resident resources and placements; source-pixel cropping; signed z-order; immutable-parent relative placement; generation-scoped ownership; deterministic descendant-first cleanup; no hidden raster replay.
- **Persistent ownership observation** — atomic `Current`, `Stale`, `Released`, and `Disposed` snapshots with semantic loss/release reasons and no passive terminal-side existence fiction.
- **Unicode raster placeholders** — opaque virtual placements, semantic row/column cell tokens, self-contained current-cursor output, and physical placement relative to a virtual parent without exposing Kitty numeric identities or placeholder encoding.
- **Persistent raster animation** — one resource-owned controller, opaque root/appended frame tokens, exact positive timing, selection, loading-mode streaming, finite/indefinite playback, bounded sequence tracking, and no hidden source-frame replay.
- **Optional TermInfo planning integration** — consumer-owned lifecycle, placement, runtime-evidence, and raster-backend planning through `Icod.TermInfo.Inspection` without widening the production dependency graph or transferring live routing authority away from Terminal.

## Raster Ownership at a Glance

The current persistent-raster ownership model is:

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    |       ordinary or relative physical placement
    |
    +-- TerminalRasterPlaceholder
    |       virtual placement
    |       |
    |       +-- TerminalRasterPlaceholderCell
    |               semantic text-grid token
    |
    +-- TerminalRasterAnimation
            resource-owned frame sequence
            |
            +-- TerminalRasterAnimationFrame
                    opaque known-frame token
```

Portable ownership bounds are explicit:

```text
live persistent resources                    256
physical + virtual placements               4096
relative-placement depth                       8
placeholder rows                            1..256
placeholder columns                         1..256
session-wide known animation frames, roots      4096
```

Placeholder cell output is current-cursor text output. Animation changes the current pixels of the same resource without creating another placement graph. Terminal owns protocol-private image/placement/frame identity and encoding; the caller owns screen coordinates, clipping, scrolling, redraw order, damage, layout, and higher-level animation policy.

See [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md) for lifecycle, capacity, failure, cleanup, relative-placement, and placeholder guarantees.

## Platforms and Targets

The package targets:

```text
net8.0
net9.0
net10.0
```

The repository uses C# 13. Release qualification covers Windows, Linux, and macOS.

`Icod.Terminal` is a managed library but intentionally uses native platform facilities where terminal modes or console behavior require them. Protocol and feature availability still depends on the terminal endpoint attached to the process; operating-system identity or terminal brand is not treated as sufficient support proof.

## Design Boundaries and Guarantees

- One live session owns one authoritative terminal input/query/event conversation; features do not install competing readers.
- Static terminal-description evidence and generation-scoped live evidence remain distinct. Timeout or silence does not automatically become `Unsupported` truth.
- Public APIs describe semantic operations rather than exposing generic raw CSI/DCS/OSC/APC vendor dispatch or caller-manufactured graphics identities.
- Capability routing is evidence-driven rather than terminal-brand driven. Ordinary raster, persistent raster, Unicode-placeholder, and persistent-animation capabilities remain distinct.
- Persistent resources, physical placements, virtual placeholders, animations, and frame tokens are generation-scoped terminal-resident ownership, not exactly restorable state. The library does not retain hidden source images or frames for automatic replay/re-upload.
- Committed graphics operations do not intentionally truncate after commitment. Transport/protocol failure is surfaced without blind replay or speculative backend switching.
- Terminal-controlled responses and unsolicited reports are external input. Correlation grants routing ownership, not authenticity or trust.
- Graphics, parser, query, event, registry, placeholder, and protocol work is explicitly bounded.
- File, temporary-file, and shared-memory Kitty transfers are not silently selected for persistent ownership; the reviewed persistent path uses direct terminal transfer.
- `Icod.Terminal` does not own higher-level cells, windows, virtual-screen state, scene composition, clipping, damage, scrolling, or layout. Those concerns belong above the live-session layer, normally in `Icod.DCurses`.
- PTY/ConPTY child-process hosting, terminal emulation, image-file decoding/transcoding, and process ownership remain separate concerns.

Security and privacy details are maintained in [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md).

## Samples and Documentation

The [`samples`](samples/README.md) directory contains focused examples for session construction, rich input, bounded queries, semantic capability planning, terminal colors and reversible state, notifications and metadata, backend-neutral raster display, persistent raster ownership, Unicode raster placeholders, persistent raster animation, and optional TermInfo planning integration.

Recommended documentation entry points:

- [`docs/releases/1.17.1.md`](docs/releases/1.17.1.md) — 1.17 packaged-README and metadata correction;
- [`docs/releases/1.17.0.md`](docs/releases/1.17.0.md) — Terminal-owned screen-planning and output-transaction release notes;
- [`docs/releases/1.17.0-alpha.1.md`](docs/releases/1.17.0-alpha.1.md) — historical 1.17 prerelease notes;
- [`CHANGELOG.md`](CHANGELOG.md) — release-by-release feature history;
- [`docs/Architecture.md`](docs/Architecture.md) — permanent layer and ownership boundaries;
- [`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md) — persistent resource, physical/virtual placement, lifecycle, animation, and frame-sequence contract;
- [`samples/Icod.Terminal.RasterAnimation.Sample`](samples/Icod.Terminal.RasterAnimation.Sample) — backend-neutral 1.16 animation walkthrough;
- [`docs/Capability-Inspection-and-Planning.md`](docs/Capability-Inspection-and-Planning.md) — semantic capability evidence and verification model;
- [`docs/Input-and-Events.md`](docs/Input-and-Events.md) — authoritative input/event routing;
- [`docs/Queries-and-Responses.md`](docs/Queries-and-Responses.md) — bounded query/response ownership and correlation;
- [`docs/Presentation-and-Reversible-State.md`](docs/Presentation-and-Reversible-State.md) — reversible/scoped terminal state;
- [`docs/Security-and-Privacy.md`](docs/Security-and-Privacy.md) — trust, disclosure, and protocol-security boundary;
- [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md) — stable 1.x compatibility and release policy;
- [`docs/Migration-to-1.0.md`](docs/Migration-to-1.0.md) — guidance for pre-1.0 consumers;
- [`docs/Public-API-Baseline-1.16.md`](docs/Public-API-Baseline-1.16.md) — final 1.16 API additions and fingerprint;
- [`docs/Public-API-Baseline-1.17.md`](docs/Public-API-Baseline-1.17.md) — final 1.17 API additions and fingerprint;
- [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md) — current and longer-range development direction.

Release notes, public-API baselines, tranche records, implementation plans, and historical roadmaps remain in the repository as engineering evidence. They are intentionally not repeated in this README.

## Compatibility and Versioning

Stable `1.0.0` remains the compatibility floor. The package supports `net8.0`, `net9.0`, and `net10.0`; compatible 1.x releases add semantic capabilities and public members without silently repurposing established signatures, enum values, lifecycle guarantees, or protocol-neutral behavior.

The final 1.17 public API fingerprint is:

```text
c0a051a925d551e526343ef59d8c47d75e41868d84235fa30bfa7debe1b3ceb9
```

Public API, package, target-framework, release-qualification, and compatibility policy is maintained in [`docs/Compatibility-and-Versioning.md`](docs/Compatibility-and-Versioning.md). Consumers upgrading from the pre-1.0 line should also review [`docs/Migration-to-1.0.md`](docs/Migration-to-1.0.md).

This README is maintained as a current product and contributor entry point. Release-by-release chronology belongs in [`CHANGELOG.md`](CHANGELOG.md), curated release notes, versioned roadmaps, public-API baselines, and release audits rather than accumulating here.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and `ncurses`.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License, version 3 or later. Sample applications are licensed under the GNU General Public License, version 3 or later, as stated in their source headers.

See `LICENSE` and the per-project/source declarations for the applicable terms.
