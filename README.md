# Icod.Terminal

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It is intended to sit between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

The project is completing the `0.1.0` release gate. T01-T09 established the live-terminal foundation, T10 rebased `Icod.DCurses` onto `Icod.Terminal` and added the higher-layer lifecycle-participant seam required for clean suspend/resume behavior, and T11 completed architectural acceptance with `watch`, `slabtop`, and `top`. T12 now covers the final public-API, documentation, package, CI, and release checks.

The first functional milestone has therefore been met: `watch`, `slabtop`, and `top` operate through `Icod.DCurses` over the shared `Icod.Terminal` / `Icod.TermInfo` stack. Remaining `0.1.0` work is release hardening rather than consumer-driven terminal-mechanism development.

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

## Target frameworks

The library targets:

- `net8.0`;
- `net10.0`.

The codebase uses C# 13.

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

The T09 presentation-lease contract is documented in [`docs/T09-Reversible-Terminal-Presentation-Leases.md`](docs/T09-Reversible-Terminal-Presentation-Leases.md). The T10 lifecycle-participant integration is recorded in [`docs/T10-DCurses-Lifecycle-Participant-Integration.md`](docs/T10-DCurses-Lifecycle-Participant-Integration.md), and the completed T11 ProcPs acceptance is recorded in [`docs/T11-ProcPs-Acceptance.md`](docs/T11-ProcPs-Acceptance.md).

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License v3.0 or later. See `LICENSE`.
