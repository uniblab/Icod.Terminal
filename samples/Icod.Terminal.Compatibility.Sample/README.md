# Icod.Terminal Compatibility Sample

This executable qualifies existing nongraphics `Icod.Terminal` behavior without inferring support from terminal branding. It can list and describe scenarios without opening a terminal, run one or all scenarios in an interactive terminal, write bounded JSON evidence, and regenerate a deterministic Markdown matrix.

The sample uses only public `Icod.Terminal` APIs. Graphics and raster protocols are deliberately outside the 1.26 matrix.

## Build and headless commands

Run from the repository root with the .NET 10 SDK and the selected target runtime installed:

```sh
dotnet run --project samples/Icod.Terminal.Compatibility.Sample -f net10.0 -- --help
dotnet run --project samples/Icod.Terminal.Compatibility.Sample -f net10.0 -- --list-scenarios
dotnet run --project samples/Icod.Terminal.Compatibility.Sample -f net10.0 -- --describe clipboard.osc52
```

These commands do not open a `TerminalSession`. The package acceptance gate runs them on `net8.0`, `net9.0`, and `net10.0` against a freshly produced package.

## Live execution

Live commands require interactive standard input and output. Supply exact labels; the sample never treats an environment variable or product name as proof of support.

```sh
dotnet run --project samples/Icod.Terminal.Compatibility.Sample -f net10.0 -- \
  --run query.dimensions \
  --terminal kitty \
  --terminal-version 0.32.2 \
  --os Ubuntu \
  --os-version 24.04 \
  --source-commit 0123456789abcdef0123456789abcdef01234567 \
  --transport WSL \
  --transport-version 2 \
  --output evidence.json
```

Use `--run-all` instead of `--run <scenario>` to visit the complete catalog. An existing output file is preserved unless `--overwrite` is explicit. Reports are written to a validated temporary sibling and moved into place only after complete serialization and validation.

Primary terminal identifiers are:

```text
windows-terminal
apple-terminal
iterm2
kitty
wezterm
ghostty
alacritty
gnome-terminal-vte
konsole
xterm
```

`vscode` is the companion OSC 633 lane. WSL, ConPTY, SSH, tmux, and similar layers are transports and must be recorded with exact versions rather than substituted for the terminal identity.

## Scenario catalog

| Scenario | Observation |
| --- | --- |
| `identity.session/v1` | Opens the public session and records bounded session/endpoint availability. |
| `query.dimensions/v1` | Queries terminal and cell pixel dimensions. |
| `query.device-attributes/v1` | Queries primary and secondary device attributes. |
| `query.status-cursor/v1` | Queries device status and cursor position. |
| `query.decrqss/v1` | Queries the SGR DEC status string. |
| `query.xtgettcap/v1` | Queries the XTGETTCAP terminal name capability. |
| `query.color/v1` | Queries palette and dynamic colors. |
| `query.pointer-shape/v1` | Queries explicit pointer-shape support. |
| `metadata.titles-location/v1` | Emits fixed OSC 0/1/2 titles and OSC 7 location metadata. |
| `semantic.hyperlinks-regions/v1` | Emits a fixed OSC 8 hyperlink and OSC 133 prompt/command regions. |
| `semantic.vscode-shell/v1` | Emits a fixed typed OSC 633 lifecycle for the VS Code lane. |
| `semantic.iterm2-shell/v1` | Emits a fixed reviewed OSC 1337 metadata subset for the iTerm2 lane. |
| `notifications/v1` | Emits fixed OSC 9, OSC 777, and OSC 99 notification requests. |
| `progress/v1` | Runs and clears a scoped OSC 9;4 progress lifecycle. |
| `clipboard.osc52/v1` | Performs a consented round trip of one fixed nonsecret OSC 52 value. |
| `input.text-key/v1` | Distinguishes typed text and key events. |
| `input.paste-focus-mouse/v1` | Distinguishes bracketed-paste phases, focus, and mouse events. |
| `input.modern-keyboard/v1` | Negotiates and records actual modern keyboard phases. |
| `lifecycle.resize-suspend/v1` | Observes typed resize, suspending, and resumed events. |
| `presentation.cursor-sync/v1` | Temporarily owns cursor style and synchronized output. |
| `presentation.pointer-colors/v1` | Temporarily owns pointer shape, palette color, and dynamic color. |

The runner announces each success condition before execution. Every side-effecting scenario asks through `TerminalSession.ReadEventAsync(...)` before its first effect. Declining records `NotRun`. Permission denial, policy suppression, silence, or timeout records `Inconclusive`; it is not proof that a terminal lacks support.

Scoped scenarios dispose their leases before they can pass. A cleanup failure is a failure for a scenario that promised restoration. Title, location, hyperlink, shell metadata, clipboard, and notification protocols cannot truthfully restore every external effect; the prompt states that boundary before execution.

## Evidence and privacy

The closed outcomes are `Pass`, `Fail`, `Inconclusive`, `NotRun`, and `NotApplicable`. Automated protocol/input observations and operator-visible confirmations are separate fields. Successful output completion alone cannot produce a visible-behavior `Pass`.

Reports contain exact package/source, terminal, operating-system, optional transport, scenario revision, UTC time, outcome, observation flags, and one bounded note. They exclude raw clipboard data, typed keys, command lines, environment dumps, arbitrary terminal replies, current working directories, and implicit user/host identities. The sample uses fixed test values for every disclosure-capable operation.

Review generated JSON before adding it under [`docs/compatibility/evidence/1.26.0`](../../docs/compatibility/evidence/1.26.0/README.md). A result qualifies only its exact scenario revision and environment.

## Regenerate the matrix

The accepted evidence directory may contain zero or more reviewed `*.json` files. With none, every primary lane and the VS Code companion lane remains explicitly `NotRun`.

```sh
dotnet run --project samples/Icod.Terminal.Compatibility.Sample -f net10.0 -- \
  --render-matrix \
  docs/compatibility/evidence/1.26.0 \
  /tmp/Icod.Terminal-1.26.0-compatibility.md

cmp docs/compatibility/1.26.0.md /tmp/Icod.Terminal-1.26.0-compatibility.md
```

The output path must not already exist. Regeneration validates every evidence file, rejects duplicates, applies stable lane/environment ordering, and never manufactures live results.
