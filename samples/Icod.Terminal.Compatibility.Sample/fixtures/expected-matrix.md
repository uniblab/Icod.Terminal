# Icod.Terminal 1.26.0 compatibility matrix

Results qualify only the exact scenario revision and environment recorded below. Missing or silent behavior is not inferred.

## Lane qualification summary

A lane with no reviewed evidence is explicitly `NotRun`. When evidence exists, every unrecorded scenario remains `NotRun`.

| Terminal lane | Reviewed evidence |
| --- | --- |
| Windows Terminal | `NotRun` |
| Apple Terminal | 1 reviewed result; other scenarios `NotRun` |
| iTerm2 | `NotRun` |
| Kitty | 1 reviewed result; other scenarios `NotRun` |
| WezTerm | `NotRun` |
| Ghostty | `NotRun` |
| Alacritty | `NotRun` |
| GNOME Terminal/VTE | `NotRun` |
| Konsole | `NotRun` |
| XTerm | `NotRun` |
| VS Code | `NotRun` |

## Reviewed results

| Terminal lane | Scenario | Outcome | Environment |
| --- | --- | --- | --- |
| Apple Terminal | `query.dimensions/v1` | `NotRun` | [1] |
| Kitty via WSL 2 | `identity.session/v1` | `Pass` | [2] |

## Environments

1. Apple Terminal 2.14; macOS 15.6; direct; Icod.Terminal 1.26.0-alpha at `0123456789abcdef0123456789abcdef01234567`; observed 2026-10-04T16:00:00.0000000+00:00. No reviewed live result exists.
2. Kitty 0.32.2; Ubuntu 24.04; WSL 2; Icod.Terminal 1.26.0-alpha at `0123456789abcdef0123456789abcdef01234567`; observed 2026-10-04T16:00:00.0000000+00:00. Session opened and reported bounded identity data.

## Outcome legend

- `Pass`: the exact scenario revision's requested behavior was observed.
- `Fail`: execution completed but contradicted the scenario.
- `Inconclusive`: permission, policy, timeout, environment, or ambiguity prevented a conclusion.
- `NotRun`: no accepted live result exists.
- `NotApplicable`: the scenario has no meaningful application to the terminal lane.
