# C105–C108 — Capability Lifecycle, Samples, Hardening, and API Freeze

- **Release:** `Icod.Terminal 1.10.0`
- **Tranches:** C105–C108
- **Status:** accepted
- **Validated feature head:** `f5272594f31188e4c3f096e61e226c7d550319b6`
- **Acceptance workflow:** `#1518 / 34594556128`
- **Final public API fingerprint:** `ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb`

## C105 — lifecycle and ownership qualification

C105 qualified the public capability-planning surface against the existing session lifecycle and authoritative query ownership model.

The test matrix proves:

- generation-scoped live evidence expires when session state is invalidated;
- valid static description evidence survives lifecycle-generation changes;
- concurrent `InspectCapability(...)` calls remain deterministic and emit no terminal bytes;
- pre-cancelled `VerifyCapabilityAsync(...)` emits no terminal traffic;
- verification does not bypass suspended query ownership;
- verification does not reopen closed query ownership;
- unavailable endpoints do not trigger raster probe traffic;
- capabilities without a reviewed support probe remain inspection-only.

No new production synchronization model was required. The existing session/query architecture already provides the required ownership boundaries.

## C106 — consumer sample and downstream acceptance

C106 adds `Icod.Terminal.CapabilityPlanning.Sample`.

The sample demonstrates:

- side-effect-free semantic inspection by default;
- optional explicit bounded verification via `--verify`;
- planning from `TerminalCapabilityStatus.IsUsable`;
- no terminal-brand branching;
- no `TERM` heuristics;
- no protocol-family branching;
- no explicit Kitty/Sixel/backend selection;
- no direct `Icod.TermInfo` dependency or evidence identity.

`packaging/VerifyCapabilityPlanningSample.ps1` builds the sample on `net8.0`, `net9.0`, and `net10.0` as part of runtime validation.

The existing `Icod.DCurses` acceptance witnesses and eight-cycle hardening soak remain green.

## C107 — adversarial and package hardening

C107 adds semantic-evidence and repeated-use hardening for:

- repeated generation invalidation;
- repeated verification of already-decisive evidence without repeated probe traffic;
- invalid public capability values rejected before output;
- multi-backend evidence where negative evidence for one backend does not erase an independent viable alternate.

The last case intentionally follows the established resolver contract. For example, `ClipboardWrite` may have an advertised TermInfo-backed path even when live OSC 52 evidence is unsupported. The semantic operation therefore remains advertised while at least one independent reviewed path remains viable.

C107 also adds a fresh NuGet-only capability-planning consumer. That consumer has exactly one direct package dependency:

```text
Icod.Terminal
```

It does not directly pin `Icod.TermInfo` or `Icod.Timing`. NuGet resolves the transitive package graph, and the smoke consumer compiles/runs on all three supported TFMs.

This is the intended dependency-compatibility witness for the 1.10 loose-coupling policy.

## C108 — final public API freeze

C105–C107 required no further public API additions beyond the C104 surface.

The final 1.10 public API fingerprint is therefore:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

The final baseline is recorded in:

- `docs/Public-API-Baseline-1.10.md`
- `docs/Public-API-Baseline-1.10.sha256`

The permanent semantic contract is recorded in:

- `docs/Capability-Inspection-and-Planning.md`

The public planning model remains dependency- and protocol-neutral.

## Acceptance evidence

Workflow `#1518 / 34594556128` passed:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate / public API baseline;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- validated package artifact.

Linux reported:

```text
net8.0   1822 passed / 0 failed
net9.0   1822 passed / 0 failed
net10.0  1822 passed / 0 failed
```

The semantic/hardening package shard passed the new fresh NuGet capability-planning consumer on every supported TFM.

## Result

C105–C108 are accepted. The remaining 1.10 work is C109 release closure: stable package identity, final changelog/README/release metadata, exact-head Staging qualification, and maintainer handoff for merge/tag/publication.
