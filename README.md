# Icod.Terminal

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It is intended to sit between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

The project is under initial `0.1.0` development. T01 establishes the repository, package, solution, tests, sample, and build/CI foundations. The live terminal contracts begin with the subsequent extraction and endpoint/mode tranches.

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

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns live endpoint observation, terminal modes, input, dimensions, lifecycle, and reversible presentation-state mechanisms. `Icod.DCurses` owns cells, windows, virtual-screen state, and refresh/diff policy. A future `Icod.Pty` package remains an adjacent concern rather than a prerequisite.

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

See [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md) for the architectural boundaries, `0.1.0` acceptance gates, and the path toward the stable `1.0.0` contract.

## License

`Icod.Terminal` is licensed under the GNU Lesser General Public License v3.0 or later. See `LICENSE`.
