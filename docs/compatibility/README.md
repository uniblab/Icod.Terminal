# Terminal Compatibility Evidence

This directory contains versioned, reviewable evidence for Icod.Terminal behavior. Compatibility claims are scoped to an exact package or source commit, terminal and version, operating system, optional transport and version, and scenario revision.

## Evidence classes

| Evidence class | Meaning |
| --- | --- |
| Library implementation | A public Icod.Terminal semantic operation exists and is covered by automated tests. |
| Static advertisement | A terminal description advertises a capability. This is planning evidence, not a live observation. |
| Automated observation | A bounded protocol query or typed input event was observed by the acceptance sample. |
| Operator observation | A person confirmed the scenario's stated visible or desktop effect. |
| Mediated transport | The scenario ran through an exact WSL, ConPTY, SSH, tmux, or other intermediary and qualifies only that path. |

These classes do not promote one another. Successful output does not prove display, branding does not prove support, silence does not prove failure, and a direct result does not qualify a mediated path.

## Outcomes

- `Pass`: the exact scenario revision's success condition was observed.
- `Fail`: execution completed and contradicted that condition.
- `Inconclusive`: permission, policy, timeout, environment, or ambiguity prevented a conclusion.
- `NotRun`: no reviewed live result exists.
- `NotApplicable`: the scenario has no meaningful application to the lane.

Timeout and permission denial are `Inconclusive`. Every absent live result remains `NotRun`; the matrix never infers positive or negative support from missing evidence.

## Identities and privacy

Primary lanes use the stable identifiers `windows-terminal`, `apple-terminal`, `iterm2`, `kitty`, `wezterm`, `ghostty`, `alacritty`, `gnome-terminal-vte`, `konsole`, and `xterm`. `vscode` is a companion OSC 633 lane. A mediated result records both transport name and exact transport version.

Accepted reports contain bounded labels and notes. They do not contain raw clipboard data, key streams, command lines, environment dumps, arbitrary reply bytes, shell history, current working directories, or implicit user/host identity. Live scenarios explain side effects and obtain an interactive choice before execution.

## Versioning

Each release owns a matrix such as `1.26.0.md` and evidence files below `evidence/<release>/`. Scenario identifiers carry explicit revisions. Changing a success condition, operation sequence, or evidence interpretation requires a new scenario revision; existing results retain their original meaning.
