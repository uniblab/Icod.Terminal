# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is a managed, cross-platform live-terminal session and terminal-control library for .NET. It sits between immutable terminal capability data from `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.10.0` is the current stable feature line. It adds protocol-neutral semantic capability inspection and explicit bounded verification while preserving the existing one-reader, lifecycle, routing, restoration, and security contracts.

The stable `1.0.0` compatibility floor remains unchanged. `Icod.Terminal` treats successful restore/build of its declared package graph as dependency compatibility evidence rather than making one resolved `Icod.TermInfo` or `Icod.Timing` version a second test-level compatibility contract.

## Installation

Install the stable package with:

```text
dotnet add package Icod.Terminal --version 1.10.0
```

The package targets:

```text
net8.0
net9.0
net10.0
```

`Icod.Terminal` declares its direct NuGet requirements in package metadata. Tests, samples, and auxiliary verification tools do not independently pin those transitive dependencies merely to duplicate the package declaration.

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
- `Icod.Terminal` owns the live terminal conversation: endpoint observation, native modes, input decoding, lifecycle, active query routing, unsolicited semantic events, semantic capability evidence/planning, semantic output, protocol framing/routing, raster output, and scoped/reversible terminal state.
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

## Semantic capability inspection and planning

Version 1.10 exposes a deliberately small semantic planning vocabulary. Callers ask about operations such as raster graphics or modern keyboard reporting without learning which protocol backend, terminal description source, or terminal brand is involved.

Side-effect-free inspection reads only current session knowledge:

```csharp
TerminalCapabilityStatus raster = session.InspectCapability(
	TerminalCapability.RasterGraphics
);

if ( raster.IsUsable ) {
	// Offer or use the raster presentation path.
} else {
	// Choose a non-raster path.
}
```

`TerminalCapabilityStatus` keeps support, endpoint availability, evidence lifetime, and current usability separate. Public evidence is intentionally only:

```text
None
StaticDescription
LiveObservation
```

When stronger evidence is worth terminal traffic, verification is explicit:

```csharp
if ( raster.Support == TerminalCapabilitySupport.Unknown
	&& raster.EndpointAvailability
		== TerminalCapabilityEndpointAvailability.Available ) {
	raster = await session.VerifyCapabilityAsync(
		TerminalCapability.RasterGraphics,
		cancellationToken
	);
}
```

Version 1.10 performs live verification only for capabilities with existing reviewed bounded probes. Initially those are `KeyboardReporting` and `RasterGraphics`. Other capabilities return current inspection status rather than inventing probe traffic.

See [Capability Inspection and Planning](docs/Capability-Inspection-and-Planning.md) for the permanent contract.

## Unsolicited semantic events

Version 1.9 extended the unified event model with `TerminalEventKind.Semantic` while preserving all existing enum numeric values.

Incoming framed traffic is assigned in this order:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

The first semantic family is interactive notification reporting. A semantic notification event is available through:

```csharp
if ( terminalEvent.Kind == TerminalEventKind.Semantic ) {
	TerminalNotificationEvent? notification =
		terminalEvent.Semantic?.Notification;
}
```

The reviewed notification event kinds are:

```text
Activated
ButtonActivated
Closed
CloseTrackingUnavailable
```

There is no second `ReadSemanticEventAsync(...)` path. Semantic events share the same bounded application-event ordering domain as ordinary input, and query responses retain first ownership of matching terminal traffic.

## Interactive Kitty notifications

`SendKittyNotificationAsync(...)` supports explicit opt-in reporting through `KittyNotificationOptions`:

```csharp
await session.SendKittyNotificationAsync(
	"Build",
	"Compilation complete",
	new KittyNotificationOptions {
		Identifier = "build-42",
		ReportActivation = true,
		ReportClose = true,
		Buttons = [ "Acknowledge", "Dismiss" ]
	}
);
```

Reporting requires a caller-supplied identifier. Button reports are one-based. Notification identifiers and reported interactions are validated but unauthenticated terminal-controlled input; applications must not use them as an authorization boundary.

## Raster graphics

The public raster API remains semantic and backend-neutral:

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

Supported public storage forms are `Rgb24`, `Rgba32`, and `Indexed8` plus an RGBA8 palette. The session resolves raster output through verified Kitty Graphics/APC first and verified Sixel/DCS as fallback.

The raster contract deliberately does not expose raw DCS/Sixel or APC/Kitty dispatch, backend selection, persistent Kitty image identifiers, placement/scaling, source rectangles, z-order, Unicode placeholders, deletion, animation, or image-file decoding/transcoding.

## Core 1.x guarantees

### One authoritative input path

Application input, semantic events, lifecycle traffic, and terminal query responses share one coordinated session. Queries and explicit capability verification reuse that same bounded, correlation-aware path. No feature opens a competing terminal reader.

### Deterministic ownership

Active query responses have first refusal on matching framed traffic. Recognized unsolicited semantic reports are classified next, followed by ordinary application input. A frame is never intentionally double-delivered as both a query response and a semantic event.

### Truthful capability evidence

Static description evidence and generation-scoped live observation remain distinct. Timeout is not automatically `Unsupported`; endpoint unavailability is not rewritten as lack of terminal support; negative evidence for one backend does not erase an independent viable alternate.

### Bounded protocol handling

Terminal-controlled input is untrusted. Control-frame parsing, query state, semantic-event buffering, raster dimensions/storage, and protocol payloads have explicit resource ceilings. Malformed/oversized owned semantic reports recover boundedly rather than leaking hostile bytes into ordinary text.

### Reversible ownership

Scoped state uses leases where overlapping ownership matters. `TerminalSession.DisposeAsync()` remains final cleanup/restoration authority for session-owned state. Ephemeral notifications and semantic observations are not replayed as reversible state across suspend/resume.

### Serialized semantic output

Ordinary terminal-aware output should use session semantic operations. `TerminalSession.Output` is an advanced borrowed transport outside normal session serialization when used directly by callers.

Committed multi-frame graphics operations do not intentionally truncate after commitment, and partial transport failure is surfaced without blind replay or automatic backend switching.

## Feature highlights

The stable 1.x surface includes:

- protocol-neutral semantic capability inspection and explicit bounded verification;
- application text and resolved terminfo capability output;
- terminal input, lifecycle events, bracketed paste, focus, mouse, and negotiated modern keyboard reporting;
- unsolicited protocol-neutral semantic events, beginning with interactive notification reports;
- bounded Primary/Secondary DA, DSR, CPR, DECRQSS, XTGETTCAP, color, pointer, clipboard, and notification queries;
- terminal titles (OSC 0/1/2) and current location (OSC 7);
- hyperlinks (OSC 8) and clipboard/selection operations (OSC 52);
- cursor-style observation/ownership (DECSCUSR/DECRQSS);
- synchronized output (DEC private mode 2026);
- terminal progress (OSC 9;4) and pointer shape (OSC 22);
- desktop notifications through OSC 9, OSC 777, and typed Kitty OSC 99, including opt-in interactive reporting;
- portable semantic prompt/command metadata (OSC 133);
- typed VS Code shell integration (OSC 633);
- typed iTerm2 shell-integration/semantic-history metadata (OSC 1337);
- indexed palette and selected dynamic terminal colors (OSC 4/104, 10–14, 17, 19 and resets);
- backend-neutral raw raster display with verified Sixel and Kitty Graphics backends.

## Samples

The [`samples`](samples/README.md) directory contains focused, buildable examples grouped by consumer goal. Recommended starting points are:

- `Icod.Terminal.Sample` — session construction and basic event reading;
- `Icod.Terminal.RichInput.Sample` — text, keys, paste, focus, mouse, lifecycle, and semantic events;
- `Icod.Terminal.CapabilityPlanning.Sample` — inspect-first semantic planning plus optional explicit verification;
- `Icod.Terminal.Query.Sample` — bounded terminal queries;
- `Icod.Terminal.Notification.Sample` — OSC 9/777/99 notification output plus `--kitty-interactive` semantic-event reporting;
- `Icod.Terminal.RasterGraphics.Sample` — backend-neutral raster display without image-decoder dependencies;
- `Icod.Terminal.SemanticPrompt.Sample` — portable prompt/command metadata;
- `Icod.Terminal.VsCodeShellIntegration.Sample` and `Icod.Terminal.ITerm2ShellIntegration.Sample` — explicit vendor-specific shell-integration examples.

Focused sample verifiers build capability planning and newer protocol/raster examples on every supported target framework during repository validation.

## Security and privacy

Terminal protocol traffic is external input/output and must be treated accordingly. `Icod.Terminal` validates and bounds semantic protocol data and deliberately avoids generic raw vendor-command/event APIs as the normal extension mechanism.

`InspectCapability(...)` emits no terminal traffic. `VerifyCapabilityAsync(...)` is explicit precisely because it may send bounded queries whose responses can reveal terminal/environment characteristics. A `Verified` result is support evidence, not authentication of the terminal, desktop session, or user.

Notification interaction reports can be fabricated by the terminal path. An identifier is correlation data, not authentication. Successful notification emission does not prove that the desktop displayed the notification, and a later typed report does not prove a trusted user action.

Several APIs intentionally publish caller-supplied metadata, including filesystem locations, hyperlinks, clipboard contents, notification text, shell metadata, and command lines. The library does not automatically discover or redact secrets; applications decide what is appropriate to disclose to the terminal.

See [Security and Privacy](docs/Security-and-Privacy.md).

## Compatibility

Stable `1.0.0` remains the compatibility floor. Versions 1.1–1.4 added compatible semantic protocol surfaces; 1.5 and 1.6 normalized internal control-language/query infrastructure; 1.7 introduced the public raster contract; 1.8 added Kitty Graphics beneath that unchanged raster surface; 1.9 added the protocol-neutral semantic-event envelope plus interactive Kitty notification request options; and 1.10 adds the protocol-neutral capability-planning surface.

The final 1.10 public API fingerprint is:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

See [Compatibility and Versioning](docs/Compatibility-and-Versioning.md). Consumers upgrading from the pre-1.0 line should also review [Migration to 1.0](docs/Migration-to-1.0.md).

## Documentation

Start with:

- [1.10.0 release notes](docs/releases/1.10.0.md)
- [Capability Inspection and Planning](docs/Capability-Inspection-and-Planning.md)
- [1.10.0 development roadmap](Icod.Terminal-1.10.0-Development-Roadmap.md)
- [Current development roadmap](Icod.Terminal-Development-Roadmap.md)
- [Architecture](docs/Architecture.md)
- [Input and Events](docs/Input-and-Events.md)
- [Queries and Responses](docs/Queries-and-Responses.md)
- [Security and Privacy](docs/Security-and-Privacy.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Migration to 1.0](docs/Migration-to-1.0.md)
- [Samples](samples/README.md)
- [Changelog](CHANGELOG.md)

Historical release and tranche records remain in the repository for design evidence, while the root README and current roadmap are maintained as concise consumer/contributor entry points.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and `ncurses`.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License, version 3 or later. Sample applications are licensed under the GNU General Public License, version 3 or later, as stated in their source headers.