# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.18.0` is a hardening release with **no public API surface delta from 0.17**.

The release strengthens existing contracts around:

- bounded parser and query-router behavior;
- suspend/resume generation and stale-response ownership;
- state-composition lock ordering;
- lifecycle/teardown rejection of new terminal-state acquisition;
- transactional rollback and explicit uncertainty after double failures;
- exact POSIX/Windows native-mode restoration;
- redirected/non-interactive endpoint truthfulness;
- repeated real `Icod.DCurses` ownership/disposal cycles;
- fresh-package validation of the 0.18 teardown barrier.

No new terminal protocol surface is added.

## Installation

```text
dotnet add package Icod.Terminal --version 0.18.0
```

The package targets `net8.0`, `net9.0`, and `net10.0` and depends on `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`.

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

`Icod.TermInfo` remains the immutable terminal-capability authority. `Icod.Terminal` owns live endpoint observation, terminal modes, input, dimensions, lifecycle, terminal identity, output setup, active terminal-query routing, semantic terminal-output operations, and reversible rich-input protocol ownership. `Icod.DCurses` owns cells, windows, virtual-screen state, and refresh/diff policy.

## 0.18 hardening guarantees

### State composition and lifecycle

Public input-protocol and presentation acquisition share one session composition domain.

The frozen order is:

```text
state composition
    -> lifecycle/teardown availability
        -> manager gate
            -> control output
```

New terminal-state ownership is rejected while the session is suspending, suspended, re-entering, or disposing. Cleanup paths remain permitted so lease disposal, rollback, lifecycle reentry, and final restoration cannot deadlock behind the availability barrier.

### Parser and query routing

The incremental decoder remains bounded. Active query transactions retain bounded late-response ownership after timeout/cancellation so stale replies cannot satisfy later queries.

Suspend invalidates queued old-generation queries before emission. Internal post-resume observation uses the same ambiguity gate and routing machinery rather than bypassing query ownership.

### Failure and rollback semantics

Multi-step terminal mutations either restore the prior truthful state or surface uncertainty explicitly.

If a transition fails and rollback fails too, both failures are preserved. The library does not silently claim the requested or baseline state is active.

Lifecycle reentry follows the same rule: failed reapply plus failed baseline rollback leaves `IsStateValid == false`, terminates the lifecycle pump, and preserves final disposal as the last restoration authority.

### Platform restoration

POSIX input-mode application and restoration use `TerminalModeApplyTiming.AfterOutputDrained`.

Windows console input-mode application and restoration use `TerminalModeApplyTiming.Immediately`.

Final cleanup restores the exact captured baseline snapshot rather than synthesizing an approximation.

## Modern keyboard reporting

Modern keyboard reporting remains opt-in through the existing compound input-protocol lease:

```csharp
TerminalControlResult<TerminalInputProtocolLease> result =
	await session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true,
			FocusReporting = true,
			MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents,
			KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
		}
	);
```

The three public reporting modes map to exact Kitty flag sets:

```text
Disambiguated = 5
EventTypes    = 7
AllKeys       = 31
```

Kitty support is negotiated before ownership is acquired. If the requested reversible contract is unavailable, the call returns an unavailable result instead of silently enabling another protocol.

Traditional keyboard input remains the compatibility floor. xterm `modifyOtherKeys` remains decode-only and is never blindly activated.

## OSC 9 safe subset

`Icod.Terminal` intentionally exposes only the bounded semantic OSC 9 forms already frozen before 0.18:

- legacy notification: `OSC 9;<message> ST`;
- progress: `OSC 9;4;<state>;<value> BEL`;
- Windows/ConEmu current-directory compatibility: `OSC 9;9;<cwd> ST`.

Hazardous or redundant OSC 9 subcommands remain excluded. OSC 7 remains the preferred portable current-location protocol, and OSC 133 remains the semantic prompt/command-region protocol.

## Downstream Icod.DCurses acceptance

The real `Icod.DCurses 0.1.0` acceptance matrix runs on `net8.0`, `net9.0`, and `net10.0`.

In addition to the historical focused gates, 0.18 adds a repeated hardening soak. Each TFM runs eight complete ownership cycles covering:

```text
TerminalSession open
rich-input acquisition + Kitty negotiation
DCurses full-screen entry
refresh + modern/focus/paste input decoding
DCurses/TerminalSession teardown
stale-lease disposal
exact native mode restoration
```

The soak is part of both PR validation and Release distribution validation.

## Security and compatibility boundary

0.18 does not expose:

- raw terminal escape-sequence writers;
- generic Kitty flag integers;
- raw Kitty private-use key codes;
- blind xterm activation/deactivation;
- global hotkeys or OS keyboard hooks;
- scan-code contracts;
- IME control;
- keyboard remapping/binding policy;
- terminal-brand-triggered automatic activation;
- generic or hazardous OSC 9 execution/control commands.

## Previous release highlights

- **0.17** — modern keyboard contracts and negotiated Kitty ownership.
- **0.16** — bounded safe OSC 9 notification and Windows-CWD compatibility APIs.
- **0.15** — typed OSC 133 extended semantic metadata.
- **0.14** — lifecycle-safe scoped color ownership and exact restoration.
- **0.13** — observable OSC 4/104 and selected OSC 10–19 terminal colors.
- **0.12** — portable OSC 133 semantic prompt/command-region markers.
- **0.11** — OSC 22 pointer shape.
- **0.10** — OSC 9;4 progress.
- **0.9** — DEC private mode 2026 synchronized output.
- **0.8** — DECSCUSR cursor style with observation/scoped restoration.
- **0.7** — OSC 52 clipboard/selections.
- **0.6** — OSC 8 hyperlinks.
- **0.5** — OSC 7 current location.
- **0.4** — OSC 0/1/2 title operations.

## Build and validation

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

Distribution validation builds/tests the solution, runs real downstream `Icod.DCurses` acceptance and the 0.18 soak, packs the NuGet artifact, verifies package structure/XML documentation, and runs fresh package-only consumers.

The retained 0.8–0.17 package contracts plus the new 0.18 hardening package contract run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.18.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all real downstream `Icod.DCurses` acceptance and soak gates green;
4. retained 0.8–0.17 plus new 0.18 package-only gates green on all supported TFMs;
5. merge to `main`;
6. Release validation green on the exact resulting `main` commit across the configured x64/ARM64 runners;
7. only then create tag `v0.18.0`.

Tagging triggers publication, so the release tag must not be created until explicitly authorized.

## Development roadmap

The 0.18 milestone is documented in [`Icod.Terminal-0.18.0-Development-Roadmap.md`](Icod.Terminal-0.18.0-Development-Roadmap.md), with tranche records T180–T186 under `docs/`.

See also [`docs/Public-API-Baseline-0.18.md`](docs/Public-API-Baseline-0.18.md).

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
