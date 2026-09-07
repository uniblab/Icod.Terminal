# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v0.3.0/icod_tui_toolchain.jpg)

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`0.17.0` adds modern keyboard contracts while preserving traditional terminal input as the default/fallback.

The release adds:

- negotiated Kitty progressive keyboard reporting through the existing rich-input lease;
- key press/repeat/release phases;
- expanded modifier identities;
- shifted/base-layout key identities and associated text;
- semantic Kitty functional-key mapping;
- decode-only xterm `modifyOtherKeys` compatibility;
- screen-local Kitty ownership that composes safely with alternate-screen presentation;
- lifecycle re-detection/re-establishment and deterministic cleanup.

No raw Kitty control API or blind xterm activation API is exposed.

## Installation

```text
dotnet add package Icod.Terminal --version 0.17.0
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

## Modern keyboard reporting

Modern keyboard reporting is opt-in through the existing compound input-protocol lease:

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

Overlapping keyboard leases reconcile to the strongest active mode while retaining only one physical library-owned Kitty stack entry on the current managed screen.

## Modern key events

`TerminalInputEvent` can now report:

```csharp
TerminalKeyEventPhase? KeyPhase
Rune? ShiftedCharacter
Rune? BaseLayoutCharacter
string? AssociatedText
```

`TerminalKeyEventPhase` is `Press`, `Repeat`, or `Release`.

`TerminalKeyModifiers` retains Shift/Control/Alt and adds Super, Hyper, Meta, CapsLock, and NumLock.

Traditional keyboard input remains compatible: ordinary traditional keys normalize to `Press`, and ordinary text remains `TerminalInputEventKind.Text`.

Known Kitty functional keys map to semantic `TerminalKey` values. Unknown Kitty private-use functional codes map to `TerminalKey.Unrecognized`; raw Kitty private-use integers are not exposed as public API.

## xterm compatibility

0.17 decodes the frozen supported `modifyOtherKeys` forms, but does not enable or disable xterm `modifyOtherKeys` automatically.

That is deliberate: the library cannot prove restoration of arbitrary pre-existing xterm state. xterm-derived events therefore remain decode-only compatibility and do not acquire Kitty-only semantics such as repeat/release phases, associated text, or modern modifiers that were not represented on the wire.

## Screen-local ownership

Kitty keyboard state is screen-local. When a managed presentation lease moves between the main and alternate screens, `Icod.Terminal` performs:

```text
Kitty pop
screen switch
Kitty push
```

Paste/focus/mouse ownership is not unnecessarily cycled during that handoff.

Input and presentation mutations share one session composition serialization domain with the frozen lock order:

```text
state composition -> manager -> control output
```

so concurrent keyboard lease changes cannot interleave inside the screen handoff.

## Lifecycle behavior

Managed suspend restores owned rich-input state before control is handed away.

Managed resume re-detects Kitty support before re-establishing a requested keyboard mode. If support cannot be re-established, the manager fails closed rather than pretending the requested state is active.

Session disposal restores owned protocol state exactly once. Stale leases disposed after owner-driven cleanup are idempotent and emit no duplicate terminal traffic.

## Security and compatibility boundary

0.17 does not expose:

- raw keyboard control-sequence writers;
- generic Kitty flag integers;
- raw Kitty private-use key codes;
- blind xterm activation/deactivation;
- global hotkeys or OS keyboard hooks;
- scan-code contracts;
- IME control;
- keyboard remapping/binding policy;
- synthetic repeat/release events for protocols that do not report them;
- terminal-brand-triggered automatic activation.

Modern keyboard parsing remains bounded and incremental. Malformed frames recover without unbounded buffering, and active terminal-query response correlation still runs before ordinary input decoding.

See [`docs/Modern-Keyboard-Security-and-Compatibility.md`](docs/Modern-Keyboard-Security-and-Compatibility.md) and [`docs/Public-API-Baseline-0.17.md`](docs/Public-API-Baseline-0.17.md).

## Rich-input sample

`samples/Icod.Terminal.RichInput.Sample` now requests Kitty `AllKeys` together with bracketed paste, focus, and button mouse reporting.

If Kitty negotiation is unavailable, the sample falls back to traditional keyboard input and separately attempts the existing paste/focus/mouse lease. When modern keyboard reporting is available, it displays phase, modifiers, shifted character, base-layout character, and associated text.

## Downstream Icod.DCurses acceptance

The real `Icod.DCurses 0.1.0` acceptance path runs on net8/net9/net10 and proves through public APIs:

```text
acquire paste/focus/mouse + Kitty AllKeys
Kitty pop
enter alternate screen
Kitty push
real CursesSession.RefreshAsync output
modern Kitty key + focus + paste events
Kitty pop
exit alternate screen
Kitty push
final TerminalSession rich-input cleanup
```

The acceptance also verifies DCurses ownership transfer of an existing `TerminalSession` and idempotent stale-lease disposal after owner-driven cleanup.

## Previous release highlights

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

Distribution validation builds/tests the solution, runs real downstream `Icod.DCurses` acceptance, packs the NuGet artifact, verifies package structure/XML documentation, and runs fresh package-only consumers.

The retained 0.8–0.16 package contracts plus the new 0.17 modern-keyboard contract run from the freshly produced NuGet artifact on `net8.0`, `net9.0`, and `net10.0`.

## Release process

Publishing 0.17.0 requires:

1. exact stable PR-head validation green on Windows, Linux, and macOS;
2. exact Staging package verification green;
3. all real downstream `Icod.DCurses` acceptance gates green;
4. retained 0.8–0.16 plus new 0.17 XML/package-only smoke gates green on all supported TFMs;
5. merge to `main`;
6. Release validation green on the exact resulting `main` commit;
7. only then create tag `v0.17.0`.

The tagged/release distribution path reruns build/tests, downstream acceptance, exact package selection, historical package contracts, and the 0.17 modern-keyboard package contract before publication.

## Development roadmap

The 0.17 milestone is documented in [`Icod.Terminal-0.17.0-Development-Roadmap.md`](Icod.Terminal-0.17.0-Development-Roadmap.md), with tranche records T170–T176 under `docs/`.

## License

`Icod.Terminal` is licensed under `LGPL-3.0-or-later`. See [`LICENSE`](LICENSE).
