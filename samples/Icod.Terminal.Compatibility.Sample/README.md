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

Two safe operator launchers perform these headless checks and then capture only `identity.session` and `query.dimensions` as separate reports. They do not overwrite evidence or exercise clipboard, notifications, metadata, or input scenarios.

```text
Run-WindowsTerminal.cmd
run-kitty-wsl.sh
```

Run the Windows launcher from a Windows Terminal session. It obtains the installed package version through `Get-AppxPackage`; if that query is unavailable, it records `unknown` and continues. Run the Kitty launcher from a Kitty session inside WSL; it requires Kitty's `KITTY_WINDOW_ID` session marker and asks once for the exact WSL version unless `WSL_VERSION` is already set. Both preserve the generated files in a new temporary directory and print its report paths.

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

## Environment awareness (1.27 development candidate)

The three revision-1 scenarios are `query.appearance`, `environment.appearance-reporting`, and `environment.in-band-resize`. Querying appearance enables no reporting. Both reporting scenarios ask consent before negotiation; only a successful acquisition prompts a theme/palette change or resize. Resize must first receive the required initial report, then observe changed character or pixel dimensions. Repeated appearance values remain valid palette observations. Escape or Q exits an observation; each wait is bounded and reporting leases are disposed before evidence is returned. Caller cancellation uses the same cleanup path.

Explicit private-mode states 0/4 produce `Unavailable`; silence, endpoint loss and incomplete observation produce `Inconclusive`; correlated malformed responses or failed cleanup produce `Fail`. A reporting result does not replace native resize or synchronous geometry. After an unavailable result, use `input.text-key` and `lifecycle.resize-suspend` to record native fallback separately.

From the repository root, inside the indicated terminal:

```cmd
samples\Icod.Terminal.Compatibility.Sample\Run-Environment-WindowsTerminal.cmd
```

```sh
sh samples/Icod.Terminal.Compatibility.Sample/run-environment-kitty-wsl.sh
```

Use a clean checkout so the recorded commit identifies the tested source. These launchers collect exact version identity and save review-only JSON under a temporary directory. The Windows launcher asks for the active terminal version from Settings / About; selecting the first installed stable/preview package could identify a different terminal. The Kitty/WSL launcher records the Kitty command version and asks for the exact WSL version. Terminal branding is not support evidence. Publication of the alpha is not required: the source sample uses the candidate project. The fresh-package gate verifies the same scenarios against a newly packed package.

Git attributes give text files an explicit LF checkout policy across Windows and WSL, including when Windows Git uses `core.autocrlf=true`. EditorConfig uses the same policy. Reviewed JSON reports retain their exact submitted bytes. EditorConfig alone does not control checkout conversion. A shared checkout without these attributes can appear clean to Windows Git but modified to WSL Git across Markdown, C#, project, solution, and other text files. If an older Windows checkout produced `sh\r: No such file or directory`, manually converting the scripts may then trigger the launcher's clean-checkout guard. That guard reports local modifications, not a wrong branch; the changed paths are printed before the launcher stops.

For an existing shared checkout, use Windows Git for network operations when the SSH key is available only in Windows. The stash preserves local changes, including untracked files. Do not reapply line-ending-only edits after updating; saved content edits remain in the stash for review.

From the repository root in Windows:

```cmd
git stash push -u -m "Preserve local changes before line-ending fix"
git pull --ff-only
```

Then refresh tracked files in WSL. This is entirely local and needs no SSH key. The `:/` pathspec selects the whole repository even when invoked from the sample directory; restoring from HEAD rewrites an older CRLF checkout under the current attributes, including exact historical JSON bytes. Use the repository root for the relative launcher path shown below. `checkout-index --all --force` is insufficient for this existing-checkout recovery.

```sh
git restore --source=HEAD --worktree -- :/
git status --short
sh samples/Icod.Terminal.Compatibility.Sample/run-environment-kitty-wsl.sh
```

Render the separate development matrix with:

```sh
dotnet run --project samples/Icod.Terminal.Compatibility.Sample -c Staging -f net10.0 -- --render-matrix docs/compatibility/evidence/1.27.0 /tmp/terminal-1.27-matrix.md --release-version 1.27.0
```

Use an output path that does not already exist. The legacy render command defaults to 1.26.0 so historical fixtures remain byte-identical. The 1.27 matrix records the reviewed Windows Terminal query as Inconclusive and both reporting modes as Unavailable; other untested lanes remain NotRun.
