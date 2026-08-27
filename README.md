# Icod.Terminal

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It is intended to sit between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

Version `0.1.0` is released. T01-T12 are complete, including the tag-controlled publication gate and three-host package validation.

The `0.2.0` line is active. T13 is complete, and T14 advances the development version to `0.2.0-alpha.2` while freezing the typed mouse/focus/paste event model and bounded input-decoder policy. Rich protocol enablement and decoding remain later 0.2 tranches.

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

Install the package from NuGet.org:

```text
dotnet add package Icod.Terminal --version 0.1.0
```

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

## Build

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

Both scripts support `clean`, `restore`, `build`, `test`, and `pack`. Running either script without an argument performs the complete sequence.

## Development roadmap

See [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md) for the architectural boundaries, `0.1.0` acceptance gates, and the path toward the stable `1.0.0` contract. The completed T02 extraction matrix is recorded in [`docs/T02-Extraction-Inventory-and-Contract-Classification.md`](docs/T02-Extraction-Inventory-and-Contract-Classification.md), the T03 low-level contract is documented in [`docs/T03-Endpoint-Observation-and-Native-Mode-Parity.md`](docs/T03-Endpoint-Observation-and-Native-Mode-Parity.md), the T04 semantic mode contract is documented in [`docs/T04-Semantic-Input-Mode-Policy.md`](docs/T04-Semantic-Input-Mode-Policy.md), the T05 session ownership contract is documented in [`docs/T05-TerminalSession-Lifecycle-and-Ownership.md`](docs/T05-TerminalSession-Lifecycle-and-Ownership.md), the T06 identity/output contract is documented in [`docs/T06-Terminal-Identity-TermInfo-and-Output-Setup.md`](docs/T06-Terminal-Identity-TermInfo-and-Output-Setup.md), the T07 lifecycle contract is documented in [`docs/T07-Live-Dimensions-and-Lifecycle-Events.md`](docs/T07-Live-Dimensions-and-Lifecycle-Events.md), and the T08 input contract is documented in [`docs/T08-Input-Byte-Stream-and-Key-Event-Decoder.md`](docs/T08-Input-Byte-Stream-and-Key-Event-Decoder.md).

The T09 presentation-lease contract is documented in [`docs/T09-Reversible-Terminal-Presentation-Leases.md`](docs/T09-Reversible-Terminal-Presentation-Leases.md). The T10 lifecycle-participant integration is recorded in [`docs/T10-DCurses-Lifecycle-Participant-Integration.md`](docs/T10-DCurses-Lifecycle-Participant-Integration.md), the completed T11 ProcPs acceptance is recorded in [`docs/T11-ProcPs-Acceptance.md`](docs/T11-ProcPs-Acceptance.md), the T12B public API/consumer review is recorded in [`docs/T12B-Public-API-and-Consumer-Contract.md`](docs/T12B-Public-API-and-Consumer-Contract.md), the completed T12C package gate is recorded in [`docs/T12C-Package-and-Fresh-Consumer-Validation.md`](docs/T12C-Package-and-Fresh-Consumer-Validation.md), and final release closure is recorded in [`docs/T12D-0.1.0-Release-Closure.md`](docs/T12D-0.1.0-Release-Closure.md).

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License v3.0 or later. See `LICENSE`.
