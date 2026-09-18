# Icod.Terminal 1.18.0 Development Roadmap

**Release:** `1.18.0`
**Theme:** Unknown-rendition baseline recovery for Terminal-only screen consumers
**Status:** T180-T184 accepted; stable `1.18.0` candidate closure in progress
**Candidate identity:** `1.18.0`
**Stable compatibility floor:** `1.0.0`
**Current published line:** `1.17.0`; `1.17.1` is the documentation-only patch baseline

## Release objective

Version 1.18 closes the single Terminal contract gap found by the `Icod.DCurses 2.0` decoupling readiness gate. A renderer that does not know the terminal's physical rendition state must be able to request one safe, opaque plan that restores every rendition axis exposed by the selected profile's enter/select evidence.

The governing rule is:

> Terminal may claim a rendition baseline only when it can unconditionally restore every rendition axis exposed by the selected profile that Terminal can enter or select; otherwise no plan is available.

The dependency direction remains:

```text
Icod.DCurses -> Icod.Terminal -> Icod.TermInfo
```

`Icod.TermInfo` remains Terminal's private capability-data, expansion, padding, and cost authority. No TermInfo type, capability identifier, terminal string, or expansion program enters the new public contract.

## Design and implementation authorities

- [`docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md`](docs/superpowers/specs/2026-09-18-1.18.0-rendition-baseline-design.md)
- [`docs/superpowers/plans/2026-09-18-1.18.0-rendition-baseline.md`](docs/superpowers/plans/2026-09-18-1.18.0-rendition-baseline.md)
- [`Icod.DCurses 2.0 PR #32`](https://github.com/uniblab/Icod.DCurses/pull/32)

## Accepted implementation record

T180-T184 are complete on PR #63. The implementation adds only `PlanRenditionBaseline()` to the public surface and retains production `Icod.TermInfo 1.15.0` / `Icod.Timing 1.0.0`.

- T180 recorded the `CS1061` package/source RED witness before the method existed.
- T181 implemented unknown-state restoration with all-or-nothing availability.
- T182 qualified attribute-only, color-only, combined, specific-exit, unsafe, empty, reset-only, padding, deterministic, and side-effect-free behavior.
- T183 qualified same-session ordering, foreign-session rejection, stale output epochs, and pre-commit cancellation.
- T184 froze identical API snapshots at `48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d`, verified the deliberate mismatch rejection, and passed the candidate package, XML, dependency, published DCurses 1.6.0, and TermInfo-free future-renderer consumers across all three target frameworks.

The retained DCurses PR #32 package witness remains governed by its published-package resume criterion. It resumes after `Icod.Terminal 1.18.0` is published; no unpublished project reference or raw TermInfo workaround is introduced.

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

The method returns `null` if any profile-exposed enterable/selectable rendition axis cannot be restored safely from an unknown state. This obligation is computed from entry/selection evidence before current normalization suppresses unsafe requests. A valid zero-byte plan is allowed only when the profile exposes no enterable attribute and no selectable color axis; reset capabilities alone do not create reachable state.

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
- exact profile-evidence, safety, nullability, ordering, ownership, and zero-byte rules;
- a package-level failing witness matching the DCurses T2001 blocker, with representative enter/select capabilities added to its synthetic profile so the `<sgr0><op>` expectation is semantically reachable.

## T181 — Baseline planner

Implement `PlanRenditionBaseline()` in `TerminalScreenPlanner` using existing private capability interpretation and opaque plan construction.

Acceptance covers:

- attribute-only profiles using global attribute reset;
- multi-attribute profiles without a global reset using every required safe specific exit in stable order;
- color-only profiles using original-color-pair restoration;
- combined profiles emitting attribute reset before color restoration;
- correct `Rendition` kind and one affected line;
- side-effect-free planning followed by exact-byte transaction commitment.

## T182 — Safety and cost hardening

Prove that availability describes unconditional recovery rather than a best-effort partial reset.

Acceptance covers:

- `null` when any enterable attribute lacks both a global reset and a safe specific exit;
- complete specific-exit coverage and stable underline, standout, italic, then strikeout ordering when no global reset exists;
- `null` when foreground or background selection is available without original-color-pair restoration;
- a zero-byte plan only for a profile with no enterable attribute and no selectable color axis, including a reset-only profile;
- exact padding-sensitive byte cost after capability expansion;
- deterministic segment ordering, overflow safety, and repeated-call stability;
- no dependence on a caller-supplied or retained physical rendition state.

## T183 — Ownership and transaction qualification

Exercise the new plan through the existing transaction boundary without inventing another output or state domain.

Acceptance covers same-session commitment, foreign-session rejection, stale-epoch rejection, cancellation before commitment, serialized output, and unchanged cleanup/failure behavior. Planning itself must perform no writes, flushes, epoch changes, or lifecycle mutation.

## T184 — Package and downstream acceptance

Freeze identical public API/XML snapshots for `net8.0`, `net9.0`, and `net10.0`; update the active baseline selector, package smoke, and the local future-DCurses compile/package consumer. That local consumer and the eventual DCurses production boundary must have no direct `Icod.TermInfo` reference. Separately, compile and run the DCurses T2001 exact-byte witness against the candidate package using its already reviewed test-only TermInfo fixture exception. The retained downstream witness's synthetic profile must add representative enter/select capabilities before it can truthfully expect `<sgr0><op>`; its public API call, transaction path, and exact-byte assertion remain unchanged.

Published `Icod.DCurses 1.6.0` compatibility remains a separate required witness. Production dependencies remain `Icod.TermInfo 1.15.0` and `Icod.Timing 1.0.0` unless an independently justified qualification issue requires a later reviewed change.

## T185 — Stable closure

Complete cross-platform, package, public-API, XML-documentation, dependency, security, and exact-head artifact qualification. Synchronize README, changelog, compatibility authority, release notes, and the main roadmap.

Tagging, release creation, and publication remain explicit maintainer actions after the stable candidate is accepted.

Current closure work removes the prerelease suffix, synchronizes consumer/release/architecture/security documentation, runs the complete exact-head workflow, records its artifact/test evidence, and stops before merge, tag, GitHub Release, or publication.

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
