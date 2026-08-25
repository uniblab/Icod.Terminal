# Icod.Terminal

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It is intended to sit between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

The project is under initial `0.1.0` development. T01 established the repository/package foundation, T02 froze the extraction boundary, T03 established low-level endpoint/native-mode parity, T04 added semantic input policy, T05 established the reversible `TerminalSession` lifecycle, T06 bound sessions to explicit `Icod.TermInfo` terminal identities and capability-safe output, T07 added live dimensions and lifecycle re-entry, and T08 now provides incremental UTF-8/TermInfo key decoding, bounded Escape ambiguity, and unified input/lifecycle reads with deadlines and caller cancellation.

The first functional milestone is driven by the terminal requirements of `watch`, `slabtop`, and `top` as they migrate into `Icod.ProcPs`.

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

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License v3.0 or later. See `LICENSE`.
