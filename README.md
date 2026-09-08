# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/main/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal session layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.0.0-rc1` is the release candidate for the intended 1.x contract.

The rc1 line is a **contract freeze and permanent-documentation release**, not a new terminal-protocol wave. It consolidates the accumulated 0.x behavior into durable 1.x authorities, freezes the exported API/enum layout, defines compatibility and migration policy, and validates the package through Terminal-owned package/invariant gates plus current downstream compatibility checks.

The one intentional pre-1.0 API correction is that a live `TerminalSession` no longer exposes its raw input transport through `TerminalSession.Input`. `ITerminalInput` remains public for custom transport injection. Application input now has one authoritative live-session path through `ReadEventAsync(...)` and typed query operations.

Full release notes and concise release history:

- [Icod.Terminal 1.0.0-rc1 release notes](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/releases/1.0.0-rc1.md)
- [Changelog](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/CHANGELOG.md)

## Installation

```text
dotnet add package Icod.Terminal --version 1.0.0-rc1
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
- semantic prompt/command metadata (OSC 133);
- indexed palette and selected dynamic terminal colors (OSC 4/104, 10–14, 17, 19 and resets);
- negotiated modern keyboard reporting;
- bracketed paste, focus, and mouse input protocols;
- bounded safe OSC 9 notification and Windows-CWD compatibility operations.

The safe OSC 9 subset intentionally excludes host-affecting vendor commands for sleep/blocking UI, GUI macros, process launch, environment disclosure, and emulator mutation.

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
- desktop notification text;
- keyboard/mouse/focus/paste input.

The library does not automatically discover or redact secrets. Applications remain responsible for deciding what data is appropriate to publish.

## Compatibility policy

The rc1 public API is machine-frozen across net8.0/net9.0/net10.0. Existing public enum numeric values are part of that baseline.

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

The links below are intentionally pinned to the `v1.0.0-rc1` tag so the documentation bundled with the rc1 package cannot silently drift as `main` advances:

- [Architecture](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Architecture.md)
- [Terminal Session and Ownership](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Terminal-Session-and-Ownership.md)
- [Lifecycle and Restoration](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Lifecycle-and-Restoration.md)
- [Input and Events](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Input-and-Events.md)
- [Queries and Responses](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Queries-and-Responses.md)
- [Modern Keyboard Security and Compatibility](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Modern-Keyboard-Security-and-Compatibility.md)
- [Presentation and Reversible State](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Presentation-and-Reversible-State.md)
- [Semantic Output Protocols](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Semantic-Output-Protocols.md)
- [Security and Privacy](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Security-and-Privacy.md)
- [Public API Baseline](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Public-API-Baseline-1.0-rc1.md)
- [Compatibility and Versioning](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Compatibility-and-Versioning.md)
- [Migration to 1.0](https://github.com/uniblab/Icod.Terminal/blob/v1.0.0-rc1/docs/Migration-to-1.0.md)

Historical T-series and 0.x public-API baselines remain available as design/release evidence.

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

PR validation runs Windows/Linux/macOS builds and tests, the frozen 1.0 public-API fingerprint, exact Staging package verification, retained package-only contracts from 0.8 through 0.18, and the fresh 1.0 release-candidate package contract.

The repository also runs current `Icod.DCurses 0.1.0` integration/ownership acceptance, including a package-boundary soak against the freshly packed Terminal artifact. Because DCurses is still an early downstream, these checks are **compatibility witnesses for the integration paths it currently exercises**, not exhaustive proof of every `Icod.Terminal` 1.x contract. Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full surface.

After merge, Release distribution validation runs the same distribution contract across the configured Windows/Linux/macOS x64/ARM64 matrix.

## Release process

`1.0.0-rc1` is publishable only after the exact PR head is green, the merge result passes Release distribution validation, and publication is explicitly authorized.

The tag-triggered workflow requires curated `docs/releases/<version>.md` release notes and re-runs the frozen API, hardening, historical package, rc1 package, and current downstream compatibility gates before publication. It does not fall back to generic auto-generated GitHub notes.

Tagging triggers publication; no release tag should be created merely because a PR is green.

## Development roadmap

Current rc1 work is tracked in [`Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`](Icod.Terminal-1.0.0-rc1-Development-Roadmap.md).

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
