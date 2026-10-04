# Icod.Terminal 1.26.0 Development Roadmap

**Goal:** Publish a versioned, evidence-backed compatibility matrix and a public-only executable acceptance sample for the established nongraphics Icod.Terminal surface.

**Status:** Planning. This documentation PR selects the scope and freezes its implementation and qualification gates. Implementation begins only after this planning branch is accepted.

**Release theme:** Terminal Compatibility Qualification.

**Design authority:** [`docs/superpowers/specs/2026-10-04-1.26.0-terminal-compatibility-qualification-design.md`](docs/superpowers/specs/2026-10-04-1.26.0-terminal-compatibility-qualification-design.md)

**Implementation authority:** [`docs/superpowers/plans/2026-10-04-1.26.0-terminal-compatibility-qualification.md`](docs/superpowers/plans/2026-10-04-1.26.0-terminal-compatibility-qualification.md)

**Baseline:** Stable `1.25.0` source merged through [PR #78](https://github.com/uniblab/Icod.Terminal/pull/78) at `7896879a31e8672683bbc6b7c4cf5f99fafc789a`; publication remains a separate maintainer action. Production dependencies are `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`.

**Compatibility:** Preserve the stable 1.0.0 compatibility floor, .NET 8/9/10 targets, existing public signatures and enum values, one authoritative live input/query path, and managed Windows/Linux/macOS behavior.

## Feature menu and selection

The post-1.25 nongraphics compatibility menu is:

| Option | Feature or feature set | Decision |
| --- | --- | --- |
| 1 | Versioned compatibility matrix and executable acceptance sample | **Selected for 1.26.0** |
| 2 | Appearance queries and change events, including reviewed DEC 2031 behavior | Deferred |
| 3 | Negotiated in-band resize notifications through DEC private mode 2048 | Deferred |
| 4 | MIME-aware, permission-reporting OSC 5522 clipboard support | Deferred |
| 5 | Selected typed OSC 21 color control and observation | Deferred |
| 6 | OSC 1337 `ReportCellSize` as an additional geometry-query backend | Deferred |

None of these options inherently requires an Icod.TermInfo API or parser change. Version 1.26 keeps Icod.TermInfo at 1.17.0 and opens a separate TermInfo change only if qualification proves a concrete built-in profile or capability-data defect.

## Release decision

Icod.Terminal already exposes the protocol families needed for broad nongraphics terminal applications, including OSC 0/1/2, 7, 8, 9, 9;4, 22, 52, 99, 133, 633, 777, and a reviewed OSC 1337 subset, along with bounded queries and rich input. The next need is reliable cross-terminal evidence rather than another protocol surface.

Version 1.26 therefore adds no public runtime API. It supplies a repeatable acceptance executable, a closed evidence schema, deterministic matrix generation, fresh-package verification, and reviewed live results. Concrete failures found during qualification are recorded without expanding this release automatically; a defect fix that is small and preserves scope may be reviewed separately, while a new protocol remains a later release decision.

## Primary qualification lanes

The primary ten terminal emulators are:

1. Windows Terminal;
2. Apple Terminal;
3. iTerm2;
4. Kitty;
5. WezTerm;
6. Ghostty;
7. Alacritty;
8. GNOME Terminal/VTE;
9. Konsole;
10. XTerm.

VS Code's embedded terminal is an additional OSC 633 integration lane. `tmux`, WSL, ConPTY, SSH, and similar layers are recorded as transports or intermediaries with exact versions; a direct pass never qualifies a mediated path.

## Governing rules

- A checked-in result identifies the exact Terminal source/package, terminal version, OS, transport, scenario revision, UTC time, outcome, and bounded note.
- The only outcomes are `Pass`, `Fail`, `Inconclusive`, `NotRun`, and `NotApplicable`.
- Timeout, denied permission, policy suppression, and ambiguous visible behavior are inconclusive rather than proof of unsupported behavior.
- Successful frame emission does not prove physical or desktop behavior; operator-visible confirmation is recorded separately from automated protocol observation.
- The sample uses only public Icod.Terminal APIs and the authoritative `TerminalSession` reader.
- Side-effecting scenarios explain their effects and obtain an explicit interactive choice before execution.
- CI verifies headless commands, evidence validation, deterministic rendering, package consumption, and existing tests. CI does not fabricate live terminal evidence.
- Graphics protocols and raster behavior remain outside the matrix while the graphics-development hold is active.

## Work sequence

### T2600 — Stable baseline, scope, and API-regret gate

- [ ] Record the exact merged 1.25.0 source, final publication state when available, package/symbol hashes, API fingerprint, dependencies, and test baseline.
- [ ] Freeze the ten primary lanes, the VS Code companion lane, transport identity rules, scenario groups, outcome vocabulary, and privacy exclusions.
- [ ] Prove that the selected work requires no new public Icod.Terminal API and no Icod.TermInfo change.
- [ ] Establish a `1.26.0-alpha` development identity after this planning PR is accepted.

**Acceptance:** The release has one bounded evidence objective and does not become a protocol-expansion or graphics release.

### T2601 — Evidence schema and deterministic matrix contract

- [ ] Add the bounded JSON evidence model with schema version, exact environment identity, scenario revision, outcome, and note.
- [ ] Reject missing identities, unknown outcomes, control characters, oversized fields, duplicate terminal/scenario keys, invalid UTC timestamps, and unrecognized scenario revisions.
- [ ] Add deterministic ordering and Markdown rendering for `docs/compatibility/1.26.0.md`.
- [ ] Add fixtures proving byte-identical rendering and a repository check that fails when the checked-in matrix is stale.

**Acceptance:** Reviewed evidence has one canonical representation, and the matrix is reproducible rather than hand-maintained prose.

### T2602 — Acceptance sample shell and headless contract

- [ ] Create `Icod.Terminal.Compatibility.Sample` with `--help`, `--list-scenarios`, `--describe`, `--run`, `--run-all`, and matrix-rendering commands.
- [ ] Parse and validate every argument before opening a terminal session or creating a report.
- [ ] Make help, scenario listing, descriptions, and fixture rendering executable without interactive terminal endpoints.
- [ ] Build and run the headless contract on .NET 8, 9, and 10.

**Acceptance:** CI can verify the complete command and report contract without claiming live terminal behavior.

### T2603 — Identity and bounded-query scenarios

- [ ] Record operator-supplied terminal/version/OS/transport labels and safe session identity observations without interpreting branding as support.
- [ ] Exercise dimensions, primary/secondary DA, DSR, cursor position, DECRQSS, XTGETTCAP, color, clipboard, and pointer queries through existing bounded APIs where applicable.
- [ ] Keep timeout, malformed response, unavailable endpoint, cancellation, and unsupported response distinct.
- [ ] Verify query replies remain owned by the common input router and cannot become ordinary application input.

**Acceptance:** Query evidence reports exactly what was observed and never turns silence into a support conclusion.

### T2604 — Metadata, prompt, and presentation scenarios

- [ ] Exercise OSC 0/1/2 titles, OSC 7 current location, OSC 8 hyperlinks, OSC 133 markers, OSC 633 methods, and the reviewed OSC 1337 metadata subset through public semantic methods.
- [ ] Exercise cursor style, synchronized output, pointer shape, palette colors, and dynamic colors only where the existing API supplies an exact bounded operation and restoration contract.
- [ ] Present success conditions before emission and record automated versus operator-visible observations separately.
- [ ] Dispose every acquired lease deterministically and fail restoration-promising scenarios when cleanup fails.

**Acceptance:** The sample qualifies existing semantic operations without raw escape strings or terminal-name branches.

### T2605 — Notification, progress, and clipboard scenarios

- [ ] Exercise OSC 9, OSC 777, OSC 99, OSC 9;4, and OSC 52 using fixed nonsecret values and the existing typed APIs.
- [ ] Require an explicit interactive choice before desktop notification, progress, clipboard read/write, or other external-state effects.
- [ ] Keep write completion, response observation, and visible confirmation as separate evidence fields.
- [ ] Exclude raw clipboard contents and notification payloads from result files.

**Acceptance:** Side effects are intentional, bounded, and auditable; permission denial or desktop policy produces `Inconclusive`.

### T2606 — Input and lifecycle scenarios

- [ ] Exercise text, key, bracketed-paste, focus, mouse, modern keyboard phase, resize, suspend/resume, timeout, and end-of-input events through `ReadEventAsync(...)`.
- [ ] Verify the sample never opens a competing reader and remains compatible with unfamiliar future event kinds.
- [ ] Record traditional versus negotiated modern keyboard observations without equating absence of a release event with failure.
- [ ] Exercise lease cleanup after cancellation, endpoint loss, and disposal.

**Acceptance:** Interactive evidence preserves the session's single-reader and lifecycle ownership rules.

### T2607 — Report writing, privacy, and failure hardening

- [ ] Write reports to a temporary sibling, validate the completed document, and atomically rename it without overwriting by default.
- [ ] Bound every identity/note collection and reject C0, DEL, and C1 controls.
- [ ] Cover partial writes, existing targets, cancellation, malformed evidence, duplicate evidence, unknown scenario revisions, and cleanup failures.
- [ ] Verify reports exclude raw clipboard data, key streams, command lines, environment dumps, and arbitrary terminal replies.

**Acceptance:** A failed or interrupted run cannot leave a valid-looking result or silently replace prior evidence.

### T2608 — Sample, documentation, and fresh-package gates

- [ ] Add the sample walkthrough, matrix interpretation guide, contribution procedure, privacy notes, and links from README and the sample index.
- [ ] Build a fresh-package smoke consumer for every headless command on net8/net9/net10 with no source-project reference.
- [ ] Add matrix freshness, schema, XML, API, dependency, license, and documentation-link checks.
- [ ] Confirm that public API snapshots remain identical to 1.25 on all target frameworks.

**Acceptance:** A package consumer can run the acceptance workflow without source internals, and documentation cannot overstate the recorded evidence.

### T2609 — Live terminal qualification

- [ ] Run the approved scenario groups on each primary terminal at an exact version and record the host and transport identities.
- [ ] Run the OSC 633 subset in the VS Code companion lane and at least one mediated transport lane without treating it as a primary emulator result.
- [ ] Review every generated report before accepting it into the evidence directory and regenerate the matrix.
- [ ] Record unavailable environments as `NotRun`; do not invent positive or negative conclusions to fill cells.

**Acceptance:** Every matrix cell traces to accepted evidence or explicitly remains unqualified.

### T2610 — Cross-platform qualification and stable closure

- [ ] Run the complete Windows/Linux/macOS runtime, source-integration, sample, package, API, matrix, and artifact workflows at one exact head.
- [ ] Record all failures and same-head reruns, exact test counts, source SHA, workflow IDs, package/symbol hashes, API fingerprint, and dependency versions.
- [ ] Set stable metadata only after the implementation and matrix are accepted, then rerun the exact stable candidate.
- [ ] Present the candidate for maintainer review. Merge, tag, GitHub release, NuGet publication, and later evidence updates remain separate actions.

**Acceptance:** One stable candidate publishes truthful, reproducible compatibility evidence while preserving all 1.x contracts.

## Explicit non-goals

Version 1.26 does not add OSC 21, OSC 5522, DEC mode 2031, DEC mode 2048, OSC 1337 `ReportCellSize`, a generic raw escape API, automatic terminal-brand routing, host-native fallbacks, remote test orchestration, telemetry, or any graphics feature. It does not require Icod.TermInfo changes. Findings may justify later work, but they do not expand this release automatically.

## Evidence log

| Checkpoint | Source or run | Result |
| --- | --- | --- |
| Stable source baseline | `7896879a31e8672683bbc6b7c4cf5f99fafc789a` / PR #78 | Stable 1.25.0 source merged with graphics limits retained; publication identity to be recorded at T2600 |
| Planning baseline | Current environment | `dotnet` and PowerShell are unavailable locally; documentation structure and links are checked locally, while repository CI remains the executable verifier |
| Planning branch | `feature/1.26-compatibility-roadmap` | Main roadmap, 1.26 roadmap, design, and implementation plan prepared for review |
