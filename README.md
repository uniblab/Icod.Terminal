# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is the managed, cross-platform live-terminal session layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.1.0` is the current 1.x release line and the first additive minor release after the stable 1.0 contract.

Version 1.1 adds typed, bounded VS Code OSC 633 shell integration without changing the meaning of the existing 1.0 APIs. OSC 633 remains a distinct vendor-specific protocol family rather than an alias for the portable OSC 133 semantic-prompt API. The new surface covers prompt start/end, pre-execution, command completion, exact command-line publication with VS Code escaping and optional nonce, and the stable documented `Cwd`, `IsWindows`, `ContinuationPrompt`, and `HasRichCommandDetection` properties.

The stable 1.0 architecture, ownership, lifecycle, input/query, restoration, security, and compatibility guarantees remain the compatibility floor for the 1.x line.

Full release notes and concise release history:

- [Icod.Terminal 1.1.0 release notes](docs/releases/1.1.0.md)
- [Changelog](CHANGELOG.md)

## Installation

```text
dotnet add package Icod.Terminal --version 1.1.0
```

The package targets:

```text
net8.0
net9.0
net10.0
```

and depends on `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`.

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

`Icod.TermInfo` is the immutable terminal-capability authority. `Icod.Terminal` owns the live terminal conversation: endpoint observation, terminal modes, input decoding, lifecycle, active query routing, semantic output, and scoped/reversible terminal state. `Icod.DCurses` owns the higher-level virtual-screen/curses presentation model.

PTY/process hosting remains orthogonal to this package.

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

For a curses-style virtual screen, prefer `Icod.DCurses` rather than rebuilding windows/cells/diff policy directly over `TerminalSession`.

## Core 1.x guarantees

### One authoritative input path

A live `TerminalSession` owns the only authoritative input reader for its transport. Ordinary consumers use:

- `ReadEventAsync(...)` for application input and lifecycle events;
- typed query operations for terminal responses.

Do not run a competing `Console.Read*`, stream read, or retained custom-transport read on the same live terminal conversation.

### Bounded query routing

Queries share the same incremental input/router domain as application input. The contract includes bounded parser state, ambiguity-aware query serialization, finite caller timeouts, pre-emission versus post-emission cancellation semantics, bounded late-response ownership, and lifecycle query generations.

A timeout is not automatically proof that the terminal does not support a feature.

### Reversible ownership

Scoped state uses leases where overlapping ownership matters. The 1.x documentation distinguishes:

1. **exact restoration** — a captured/observed external baseline is replayed exactly;
2. **terminal-policy reset** — control returns to terminal policy without claiming the exact previous value;
3. **Icod-owned nested state** — an outer library-owned value can be restored even when the pre-Icod state is not observable;
4. **ephemeral metadata** — explicit output with no lifecycle replay/restoration state.

`TerminalSession.DisposeAsync()` remains final cleanup/restoration authority for session-owned state.

### Output serialization boundary

Use session semantic operations for ordinary terminal output. `TerminalSession.Output` remains available only as an advanced borrowed transport and is outside normal session serialization when used directly by callers.

`WriteTerminalStringAsync(...)` is intended for already-resolved terminfo capability strings; it is not a recommendation to construct arbitrary OSC/CSI/vendor traffic manually.

## Semantic terminal features

The supported semantic surface includes:

- application text and resolved terminfo capability output;
- terminal titles (OSC 0/1/2);
- current-location publication (OSC 7);
- hyperlinks (OSC 8);
- clipboard/selection operations and explicit reads (OSC 52);
- cursor style observation/ownership (DECSCUSR/DECRQSS);
- synchronized output (DEC private mode 2026);
- terminal progress (OSC 9;4);
- terminal pointer shape (OSC 22);
- portable semantic prompt/command metadata (OSC 133);
- typed VS Code shell integration (OSC 633);
- indexed palette and selected dynamic terminal colors (OSC 4/104, 10–14, 17, 19 and resets);
- negotiated modern keyboard reporting;
- bracketed paste, focus, and mouse input protocols;
- bounded safe OSC 9 notification and Windows-CWD compatibility operations.

The safe OSC 9 subset intentionally excludes host-affecting vendor commands for sleep/blocking UI, GUI macros, process launch, environment disclosure, and emulator mutation.

### VS Code OSC 633

The 1.1 OSC 633 API is explicitly vendor-specific and typed. It provides semantic operations for `A`, `B`, `C`, `D`, and `E` command detection plus the stable documented `P` properties `Cwd`, `IsWindows`, `ContinuationPrompt`, and `HasRichCommandDetection`.

Command-line, `Cwd`, and `ContinuationPrompt` values use VS Code's protocol escaping before strict UTF-8 framing. Optional nonces are explicit bounded caller input for the command-line and current-directory forms that define them. The library does not expose a generic raw OSC 633 writer, does not infer VS Code support from terminal identity, and does not expose the unfinalized `F`/`G`, `H`/`I`, `SetMark`, or `EnvJson`/`EnvSingle*` extensions.

See [`docs/VsCode-Osc633-Shell-Integration.md`](docs/VsCode-Osc633-Shell-Integration.md).

## Modern keyboard reporting

Modern keyboard reporting is opt-in through the compound input-protocol lease. Traditional keyboard decoding remains the compatibility floor.

The public modes are semantic:

```text
Disambiguated
EventTypes
AllKeys
```

Kitty support is negotiated before reversible ownership is acquired. xterm `modifyOtherKeys` remains decode-only and is not blindly activated.

## Security and privacy

Semantic APIs validate and bound terminal protocol data before commitment where the contract permits it. The library deliberately avoids a generic raw vendor-command API as the ordinary extension mechanism.

Several operations disclose caller-supplied metadata by design:

- clipboard contents;
- current filesystem locations;
- hyperlinks;
- OSC 133 command-line metadata;
- OSC 633 command-line/current-directory/continuation-prompt metadata and optional nonce;
- desktop notification text;
- keyboard/mouse/focus/paste input.

The library does not automatically discover or redact secrets. Applications remain responsible for deciding what data is appropriate to publish.

## Compatibility policy

Stable `1.0.0` remains the compatibility floor. Version `1.1.0` intentionally adds compatible public methods and receives its own machine-frozen public-API fingerprint across net8.0/net9.0/net10.0; the 1.0 fingerprint remains retained as historical compatibility evidence. Existing public enum numeric values remain part of the stable 1.x contract.

For the stable 1.x line:

- patch releases fix/harden the documented contract without intentionally breaking it;
- minor releases may add compatible API/semantic features with an intentional baseline update;
- ordinary removals, renames, signature breaks, enum renumbering, or incompatible ownership/security/restoration changes require a new major release.

Vendor runtime EOL alone is not sufficient reason to drop net8.0 or net9.0; a concrete security/toolchain/maintenance blocker is required.

## Platform support

The built-in `SystemTerminalControlProvider` supports:

- Windows;
- Linux;
- macOS.

Other hosts receive controlled `Unsupported` results from the built-in provider. Custom implementations may be supplied through `ITerminalControlProvider`, `ITerminalInput`, and `ITerminalOutput`.

## Permanent documentation

The permanent 1.x authorities include:

- [Architecture](docs/Architecture.md)
- [Terminal Session and Ownership](docs/Terminal-Session-and-Ownership.md)
- [Lifecycle and Restoration](docs/Lifecycle-and-Restoration.md)
- [Input and Events](docs/Input-and-Events.md)
- [Queries and Responses](docs/Queries-and-Responses.md)
- [Modern Keyboard Security and Compatibility](docs/Modern-Keyboard-Security-and-Compatibility.md)
- [Presentation and Reversible State](docs/Presentation-and-Reversible-State.md)
- [Semantic Output Protocols](docs/Semantic-Output-Protocols.md)
- [VS Code OSC 633 Shell Integration](docs/VsCode-Osc633-Shell-Integration.md)
- [Security and Privacy](docs/Security-and-Privacy.md)
- [Licensing](docs/Licensing.md)
- [Public API Baseline 1.0](docs/Public-API-Baseline-1.0.md)
- [Public API Baseline 1.1](docs/Public-API-Baseline-1.1.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Migration to 1.0](docs/Migration-to-1.0.md)

Historical T-series, 0.x baselines, and the rc1 baseline remain available as design/release evidence.

## Samples

Repository samples are indexed by task in [`samples/README.md`](samples/README.md). They cover session basics, rich input, queries, scoped presentation/state, colors, titles, location, hyperlinks, clipboard, semantic prompt metadata, and notifications.

## Build and validation

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

PR validation runs Windows/Linux/macOS runtime/source validation, the current machine public-API fingerprint, one portable package candidate, and four parallel package-contract shards retaining contracts from 0.8 through the stable 1.x release line. The package-candidate gate also verifies the exact project-appropriate GPL/LGPL header template for every tracked `.cs` and `.csproj` file.

The 1.1 semantic package shard additionally compiles and runs a fresh NuGet-only OSC 633 consumer on `net8.0`, `net9.0`, and `net10.0` and verifies generated XML documentation for every new public member.

The repository also runs current `Icod.DCurses 0.1.0` integration/ownership acceptance, including a package-boundary soak against the freshly packed Terminal artifact. Because DCurses is still an early downstream, these checks are **compatibility witnesses for the integration paths it currently exercises**, not exhaustive proof of every `Icod.Terminal` 1.x contract. Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full surface.

After merge, Release distribution validation runs six Windows/Linux/macOS x64/ARM64 runtime jobs plus the single portable package/four-shard package contract.

## Release process

`1.1.0` is publishable only after the exact 1.1 PR head is green, the merge result passes Release distribution validation, and publication is explicitly authorized.

The tag-triggered workflow requires curated `docs/releases/<version>.md` release notes and re-runs the public API, hardening, historical package, stable release-line package, and current downstream compatibility gates before publication. It does not fall back to generic auto-generated GitHub notes.

Tagging triggers publication; no release tag should be created merely because a PR is green.

## Development roadmap

Current release status is tracked in [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md). The completed rc1 program remains preserved in `Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and `ncurses`.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

The published `Icod.Terminal` library package and the C# sources compiled into the library are licensed under **LGPL-3.0-or-later**.

Repository executable/test programs—including `Icod.Terminal.Tests`, samples, package smoke tests, validation utilities, and downstream acceptance tools—are licensed under **GPL-3.0-or-later**. Their GPL license does not change the LGPL license of the reusable `Icod.Terminal` library they consume.

The root [`LICENSE`](LICENSE) contains the LGPLv3 terms and the incorporated GPLv3 terms. See [Licensing](docs/Licensing.md) for the project-by-project policy and source-header requirements.
