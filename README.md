# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It is intended to sit between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.3.0` is the current stable release line. It retains the 0.1 live-session
foundation and 0.2 rich-input contract while adding bounded, expectation-driven
active terminal queries over the same session-owned input path.

The stable 0.3 API includes typed Primary and Secondary Device Attributes,
Device Status Report, Cursor Position Report, DECRQSS status-string queries, and
XTGETTCAP live capability observations. Session opening remains passive: active
interrogation occurs only when a caller explicitly invokes a query method.

The first functional milestone remains intact: `watch`, `slabtop`, and `top` operate through `Icod.DCurses` over the shared `Icod.Terminal` / `Icod.TermInfo` stack.

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

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns live endpoint observation, terminal modes, input, dimensions, lifecycle, terminal identity, output setup, and reversible presentation-state mechanisms. `Icod.DCurses` owns cells, windows, virtual-screen state, and refresh/diff policy. A future `Icod.Pty` package remains an adjacent concern rather than a prerequisite.

`Icod.Timing` supplies the monotonic elapsed-time and cancellable-delay primitives used by Terminal's relative event timeouts and Escape-sequence ambiguity windows.

## Installation

The stable 0.3 release installs as:

```text
dotnet add package Icod.Terminal --version 0.3.0
```

The package targets `net8.0`, `net9.0`, and `net10.0` and depends on
`Icod.TermInfo 1.4.1` and `Icod.Timing 1.0.0`.

## Quick start

The ordinary application entry point is `TerminalSession`:

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
    new TerminalSessionOptions {
        InputMode = TerminalInputMode.CBreak,
        EchoInput = false
    }
);

TerminalEvent terminalEvent = await session.ReadEventAsync(
    TimeSpan.FromSeconds( 1 )
);
```

The session borrows process-standard endpoints, owns only the terminal state transitions it applies, and restores its captured baseline during `DisposeAsync()`.

Applications which genuinely need complete native mode observation, serialization, or custom endpoint/control backends may use the lower-level public contracts; ordinary interactive applications should prefer `TerminalSession`.

## 0.2 rich input

Rich input remains on the same `TerminalSession.ReadEventAsync` path. Reporting
protocols are enabled only through reversible session-owned leases:

```csharp
TerminalControlResult<TerminalInputProtocolLease> protocolResult =
    await session.AcquireInputProtocolsAsync(
        new TerminalInputProtocolOptions {
            BracketedPaste = true,
            FocusReporting = true,
            MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
        }
    );

if ( protocolResult.IsAvailable ) {
    await using TerminalInputProtocolLease protocols =
        protocolResult.GetRequiredValue();

    TerminalEvent terminalEvent = await session.ReadEventAsync();
    TerminalInputEvent? input = terminalEvent.Input;

    if ( TerminalInputEventKind.Mouse == input?.Kind ) {
        TerminalMouseEvent mouse = input.Mouse!;
        // mouse.Column and mouse.Row are zero-based terminal-cell coordinates.
    }
}
```

The lease owns only the requested terminal reporting protocols. Nested leases are
supported; the last relevant lease restores the prior protocol state, and
session disposal remains authoritative cleanup.

Bracketed paste is framed rather than accumulated as one unbounded string:
applications receive `Begin`, one or more bounded `Data` events, then `End`.
Paste Data chunk boundaries are transport/decoder boundaries and are not
semantic line boundaries.

Decoder policy is configured per session:

```csharp
new TerminalSessionOptions {
    InputDecoderOptions = new TerminalInputDecoderOptions {
        EscapeSequenceTimeout = TimeSpan.FromMilliseconds( 50 ),
        MaximumBufferedBytes = TerminalSession.MaximumBufferedInputBytes,
        PasteChunkBytes = 4096
    }
};
```

The defaults preserve the 0.1 Escape-ambiguity and buffer policy. Modified
traditional navigation/editing/function-key sequences normalize into
`TerminalKey` plus `TerminalKeyModifiers`; no second keyboard protocol is
required.

The reviewed 0.2 additions are recorded in
[`docs/Public-API-Baseline-0.2.md`](docs/Public-API-Baseline-0.2.md).

## 0.1 consumer contract

The T12B audit found no breaking public-API correction required before `0.1.0`. The reviewed behavior and API surface are recorded in:

- [`docs/T12B-Public-API-and-Consumer-Contract.md`](docs/T12B-Public-API-and-Consumer-Contract.md);
- [`docs/Public-API-Baseline-0.1.md`](docs/Public-API-Baseline-0.1.md).

Important `0.1.x` rules include:

- input is always an interactive terminal; output may be redirected only when explicitly permitted;
- canonical/cbreak/raw are semantic requests mapped separately to POSIX and Windows host models;
- unknown POSIX terminal names fall back safely rather than silently becoming xterm;
- input decoding is incremental and the Escape-prefix ambiguity window is bounded;
- `Available`, `Unavailable`, `Unsupported`, and `Failed` remain distinct low-level outcomes;
- session cleanup restores captured state and does not close borrowed caller/process endpoints;
- PTY/ConPTY creation and child-process hosting belong to a future adjacent `Icod.Pty` package.

The `0.1.x` runtime dependencies are `Icod.TermInfo 1.0.0` and `Icod.Timing 1.0.0`. `Icod.DCurses` and `Icod.ProcPs` are consumers, not runtime dependencies of this package.

## Target frameworks

The library targets:

- `net8.0`;
- `net9.0`;
- `net10.0`.

The codebase uses C# 13 and supports the terminal-control implementations provided for Windows, Linux, and macOS.

## Active terminal queries

`0.3` adds explicit typed terminal interrogation to `TerminalSession`. Opening a
session does not send DA, DSR, CPR, DECRQSS, XTGETTCAP, or any other probe.

A caller chooses which requests to issue and supplies the caller-visible timeout:

```csharp
TimeSpan timeout = TimeSpan.FromMilliseconds( 750 );

TerminalPrimaryDeviceAttributes primary =
	await session.QueryPrimaryDeviceAttributesAsync( timeout );

TerminalCursorPosition cursor =
	await session.QueryCursorPositionAsync( timeout );

TerminalStatusStringResponse sgr =
	await session.QueryStatusStringAsync(
		TerminalStatusStringKind.SelectGraphicRendition,
		timeout
	);

TerminalCapabilityObservation terminalName =
	await session.QueryLiveCapabilityAsync(
		"TN",
		timeout
	);
```

The public query families are:

- Primary Device Attributes;
- Secondary Device Attributes;
- standard ECMA-48 Device Status Report;
- standard Cursor Position Report;
- fixed DECRQSS status-string requests;
- single-name XTGETTCAP live capability observations.

CPR rows and columns are one-based, matching the wire protocol. XTGETTCAP values
remain exact decoded bytes because terminal capability values may contain ESC or
other control bytes.

Caller cancellation uses ordinary `OperationCanceledException` semantics and a
caller-visible deadline uses `TimeoutException`. Once request bytes have been
emitted, the session retains bounded internal ownership long enough to consume
or expire a late ambiguous response; a cancelled/timed-out query therefore
cannot contaminate the next serialized query.

All responses are routed through the same session-owned input path used by
ordinary text, keys, mouse, focus, paste, and lifecycle-aware event consumption.
There is no public raw response reader or caller-extensible query protocol
registration surface.

The reviewed 0.3 additions are recorded in
[`docs/Public-API-Baseline-0.3.md`](docs/Public-API-Baseline-0.3.md).

## Samples

The repository contains three deliberately different interactive samples:

- [`Icod.Terminal.Sample`](samples/Icod.Terminal.Sample/) is the minimal session,
  identity, size, output, and restoration example;
- [`Icod.Terminal.RichInput.Sample`](samples/Icod.Terminal.RichInput.Sample/) is
  the 0.2 live event inspector for focus, bracketed paste, mouse input, modified
  keys, lifecycle events, and reversible input-protocol leases;
- [`Icod.Terminal.Query.Sample`](samples/Icod.Terminal.Query.Sample/) explicitly
  issues the 0.3 CSI/DCS query families while reversible presentation and
  rich-input leases are active, then returns to the unified event loop.

See [`samples/README.md`](samples/README.md) for run instructions and expected
behavior.

## Build

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

Both scripts support `clean`, `restore`, `build`, `test`, `pack`, and `validate`.
Running either script without an argument performs the complete sequence,
including Debug package validation.

## Development roadmap

The completed `0.3.0` milestone is documented in
[`Icod.Terminal-0.3.0-Development-Roadmap.md`](Icod.Terminal-0.3.0-Development-Roadmap.md).
The completed T21 foundation contract is recorded in
[`docs/T21-0.3-Foundation-and-Contract-Reset.md`](docs/T21-0.3-Foundation-and-Contract-Reset.md).
The completed T22 framing/demultiplexing tranche is recorded in
[`docs/T22-Response-Framing-and-Single-Reader-Demultiplexing.md`](docs/T22-Response-Framing-and-Single-Reader-Demultiplexing.md).
The completed T23 transaction/lifetime tranche is recorded in
[`docs/T23-Query-Transactions-Deadlines-and-Late-Response-Ownership.md`](docs/T23-Query-Transactions-Deadlines-and-Late-Response-Ownership.md).
The completed T24 CSI query family is recorded in
[`docs/T24-CSI-Query-Family.md`](docs/T24-CSI-Query-Family.md).
The completed T25 DECRQSS/DCS query tranche is recorded in
[`docs/T25-DECRQSS.md`](docs/T25-DECRQSS.md).
The completed T26 XTGETTCAP live-capability tranche is recorded in
[`docs/T26-XTGETTCAP.md`](docs/T26-XTGETTCAP.md).
The T27 integration and acceptance record is maintained in
[`docs/T27-Query-Integration-and-Probe-Acceptance.md`](docs/T27-Query-Integration-and-Probe-Acceptance.md).
The completed T28A release-candidate gate is recorded in
[`docs/T28A-0.3-Release-Candidate-Gate.md`](docs/T28A-0.3-Release-Candidate-Gate.md),
the stable T28B closure is recorded in
[`docs/T28B-0.3.0-Release-Closure.md`](docs/T28B-0.3.0-Release-Closure.md),
and the reviewed 0.3 public API delta is published in
[`docs/Public-API-Baseline-0.3.md`](docs/Public-API-Baseline-0.3.md).
The completed `0.2.0` milestone remains in
[`Icod.Terminal-0.2.0-Development-Roadmap.md`](Icod.Terminal-0.2.0-Development-Roadmap.md).

See [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md) for the architectural boundaries, `0.1.0` acceptance gates, and the path toward the stable `1.0.0` contract. The completed T02 extraction matrix is recorded in [`docs/T02-Extraction-Inventory-and-Contract-Classification.md`](docs/T02-Extraction-Inventory-and-Contract-Classification.md), the T03 low-level contract is documented in [`docs/T03-Endpoint-Observation-and-Native-Mode-Parity.md`](docs/T03-Endpoint-Observation-and-Native-Mode-Parity.md), the T04 semantic mode contract is documented in [`docs/T04-Semantic-Input-Mode-Policy.md`](docs/T04-Semantic-Input-Mode-Policy.md), the T05 session ownership contract is documented in [`docs/T05-TerminalSession-Lifecycle-and-Ownership.md`](docs/T05-TerminalSession-Lifecycle-and-Ownership.md), the T06 identity/output contract is documented in [`docs/T06-Terminal-Identity-TermInfo-and-Output-Setup.md`](docs/T06-Terminal-Identity-TermInfo-and-Output-Setup.md), the T07 lifecycle contract is documented in [`docs/T07-Live-Dimensions-and-Lifecycle-Events.md`](docs/T07-Live-Dimensions-and-Lifecycle-Events.md), and the T08 input contract is documented in [`docs/T08-Input-Byte-Stream-and-Key-Event-Decoder.md`](docs/T08-Input-Byte-Stream-and-Key-Event-Decoder.md).

The T09 presentation-lease contract is documented in [`docs/T09-Reversible-Terminal-Presentation-Leases.md`](docs/T09-Reversible-Terminal-Presentation-Leases.md). The T10 lifecycle-participant integration is recorded in [`docs/T10-DCurses-Lifecycle-Participant-Integration.md`](docs/T10-DCurses-Lifecycle-Participant-Integration.md), the completed T11 ProcPs acceptance is recorded in [`docs/T11-ProcPs-Acceptance.md`](docs/T11-ProcPs-Acceptance.md), the T12B public API/consumer review is recorded in [`docs/T12B-Public-API-and-Consumer-Contract.md`](docs/T12B-Public-API-and-Consumer-Contract.md), the completed T12C package gate is recorded in [`docs/T12C-Package-and-Fresh-Consumer-Validation.md`](docs/T12C-Package-and-Fresh-Consumer-Validation.md), and final 0.1 release closure is recorded in [`docs/T12D-0.1.0-Release-Closure.md`](docs/T12D-0.1.0-Release-Closure.md).

The 0.2 rich-input implementation is recorded tranche-by-tranche in T13-T19.
The downstream acceptance result is in
[`docs/T19-DCurses-Rich-Input-Acceptance.md`](docs/T19-DCurses-Rich-Input-Acceptance.md),
the reviewed 0.2 public API delta is in
[`docs/Public-API-Baseline-0.2.md`](docs/Public-API-Baseline-0.2.md), the
release-candidate gate is in
[`docs/T20A-0.2-Release-Candidate-Gate.md`](docs/T20A-0.2-Release-Candidate-Gate.md),
and stable release closure is recorded in
[`docs/T20B-0.2.0-Release-Closure.md`](docs/T20B-0.2.0-Release-Closure.md).

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and ncurses.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

Licensed under the GNU Lesser General Public License v3.0 or later. See `LICENSE`.
