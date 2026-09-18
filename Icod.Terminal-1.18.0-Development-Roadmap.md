# Icod.Terminal 1.18.0 Development Roadmap

**Release:** `1.18.0`
**Theme:** Unknown-rendition baseline recovery for Terminal-only screen consumers
**Status:** Planning approved; implementation not started
**Planned development identity:** `1.18.0-alpha.1`
**Stable compatibility floor:** `1.0.0`
**Current published line:** `1.17.0`; `1.17.1` is the documentation-only patch baseline

## Release objective

Version 1.18 closes the single Terminal contract gap found by the `Icod.DCurses 2.0` decoupling readiness gate. A renderer that does not know the terminal's physical rendition state must be able to request one safe, opaque plan that restores every rendition axis Terminal itself could have changed.

The governing rule is:

> Terminal may claim a rendition baseline only when it can unconditionally restore every rendition axis reachable through its own semantic planner; otherwise no plan is available.

The dependency direction remains:

```text
Icod.DCurses -> Icod.Terminal -> Icod.TermInfo
```

`Icod.TermInfo` remains Terminal's private capability-data, expansion, padding, and cost authority. No TermInfo type, capability identifier, terminal string, or expansion program enters the new public contract.

## Design and implementation authorities

- [`docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md`](docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md)
- [`docs/superpowers/plans/2026-09-18-1.18.0-rendition-baseline.md`](docs/superpowers/plans/2026-09-18-1.18.0-rendition-baseline.md)
- [`Icod.DCurses 2.0 PR #32`](https://github.com/uniblab/Icod.DCurses/pull/32)

## Public contract

Add one method to the existing session-bound planner:

```csharp
public TerminalScreenOperationPlan? PlanRenditionBaseline();
```

The method represents an unknown physical rendition, not a known current `TerminalScreenRendition`. It is side-effect free and returns an opaque `Rendition` plan owned by the same planner/session.

The returned plan:

- restores all enterable text attributes to their normalized default through a global reset or a complete stable sequence of safe specific exits;
- restores foreground and background colors to terminal defaults whenever Terminal can select colors;
- emits the global attribute reset before original-color restoration;
- includes capability expansion, padding, exact encoded-byte cost, and affected-line accounting;
- remains valid only through the existing same-session transaction ownership rules.

The method returns `null` if any Terminal-reachable rendition axis cannot be restored safely from an unknown state. A valid zero-byte plan is allowed only when the planner cannot change any rendition axis at all.

Existing `PlanRenditionTransition(...)`, `PlanRenditionReset(current)`, normalization, operation-plan opacity, and transaction semantics remain unchanged.

## Tranche sequence

```text
T180  contract freeze, reference snapshot, API-regret gate, and alpha identity
T181  unknown-state rendition-baseline planner and focused red-green tests
T182  capability ordering, padding, cost, zero-byte, and nullability hardening
T183  session ownership, transaction commitment, and adversarial qualification
T184  package/API/XML/docs and DCurses 2.0 package-only acceptance
T185  release-candidate qualification and stable 1.18.0 closure
```

## T180 — Contract freeze and development identity

Record the 1.17.1 baseline, establish `1.18.0-alpha.1`, and freeze the single additive public signature and its XML contract before implementation.

Acceptance requires:

- no change to existing 1.x signatures or behavior;
- no raw TermInfo or escape-sequence exposure;
- explicit distinction between unknown physical state and known-current reset;
- exact safety, nullability, ordering, ownership, and zero-byte rules;
- a package-level failing witness matching the DCurses T2001 blocker.

## T181 — Baseline planner

Implement `PlanRenditionBaseline()` in `TerminalScreenPlanner` using existing private capability interpretation and opaque plan construction.

Acceptance covers:

- attribute-only profiles using global attribute reset;
- color-only profiles using original-color-pair restoration;
- combined profiles emitting attribute reset before color restoration;
- correct `Rendition` kind and one affected line;
- side-effect-free planning followed by exact-byte transaction commitment.

## T182 — Safety and cost hardening

Prove that availability describes unconditional recovery rather than a best-effort partial reset.

Acceptance covers:

- `null` when any enterable attribute lacks both a global reset and a safe specific exit;
- `null` when foreground or background selection is available without original-color-pair restoration;
- a zero-byte plan only for a profile with no Terminal-reachable rendition changes;
- exact padding-sensitive byte cost after capability expansion;
- deterministic segment ordering, overflow safety, and repeated-call stability;
- no dependence on a caller-supplied or retained physical rendition state.

## T183 — Ownership and transaction qualification

Exercise the new plan through the existing transaction boundary without inventing another output or state domain.

Acceptance covers same-session commitment, foreign-session rejection, stale-epoch rejection, cancellation before commitment, serialized output, and unchanged cleanup/failure behavior. Planning itself must perform no writes, flushes, epoch changes, or lifecycle mutation.

## T184 — Package and downstream acceptance

Freeze identical public API/XML snapshots for `net8.0`, `net9.0`, and `net10.0`; update package smoke and the future-DCurses screen-contract consumer; and compile the DCurses T2001 package-only witness against the candidate package without a direct `Icod.TermInfo` reference.

Published `Icod.DCurses 1.6.0` compatibility remains a separate required witness. Production dependencies remain `Icod.TermInfo 1.15.0` and `Icod.Timing 1.0.0` unless an independently justified qualification issue requires a later reviewed change.

## T185 — Stable closure

Complete cross-platform, package, public-API, XML-documentation, dependency, security, and exact-head artifact qualification. Synchronize README, changelog, compatibility authority, release notes, and the main roadmap.

Tagging, release creation, and publication remain explicit maintainer actions after the stable candidate is accepted.

## Explicit non-goals

Version 1.18 does not add:

- retained physical rendition state or automatic state tracking;
- desired-versus-physical screen comparison or repaint policy;
- Terminal-owned cells, windows, layout, clipping, damage, or Unicode width;
- public raw capabilities, terminal strings, or generic control-sequence builders;
- a replacement for `PlanRenditionReset(current)`;
- transaction, output-epoch, hyperlink, raster, input, query, or lifecycle redesign;
- new raster-animation, PTY, process-hosting, or widget features;
- a production dependency update without separate evidence and review.

## Release gates

The stable candidate must pass:

- focused red-green exact-byte rendition-baseline tests;
- the full unit and TermInfo-integration suites on all target frameworks;
- Windows, Linux, and macOS workflow matrices;
- package/public API/XML/license validation;
- fresh package-only screen-contract and DCurses 2.0 readiness consumers;
- published DCurses 1.6 compatibility and hardening;
- exact-head artifact and independent review gates.
