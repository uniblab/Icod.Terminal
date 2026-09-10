# Icod.Terminal 1.10.0 Development Roadmap

**Release:** `1.10.0`  
**Theme:** semantic capability inspection and planning  
**Status:** C100 dependency-decoupling and validation hygiene active  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.9.0`

## Why this release exists

Versions 1.5 through 1.9 built a mature internal model for terminal capability evidence, semantic operation routing, live observation, endpoint availability, and lifecycle invalidation. Higher-level consumers still cannot inspect that knowledge through a small public semantic contract without either performing protocol-specific queries themselves or learning implementation details that should remain internal to `Icod.Terminal`.

Version 1.10 exposes a deliberately reduced semantic planning surface while preserving the existing separation between static `Icod.TermInfo` descriptions and the live terminal conversation owned by `Icod.Terminal`.

The architectural objective is:

> Let callers ask what semantic terminal operations are presently known to be usable, and explicitly request bounded verification when live probing is necessary, without exposing protocol backends, routing preference tables, raw evidence ledgers, or terminal-brand heuristics.

Before that public work begins, C100 removes unnecessary dependency-version coupling from active validation so the release is developed against package contracts rather than duplicated version literals.

## Dependency-coupling policy

`Icod.Terminal` necessarily declares NuGet dependency requirements in its package project. Those declarations are package metadata, not test policy.

For active tests, samples, package smoke consumers, and verification tools:

1. do not independently pin `Icod.TermInfo`, `Icod.Timing`, or other transitive runtime dependencies merely to reproduce the package project's version declaration;
2. do not treat one exact resolved dependency version as part of the `Icod.Terminal` behavioral contract;
3. successful restore/build against the package graph is sufficient dependency compatibility evidence unless a concrete incompatibility is being diagnosed;
4. package verification may require the expected dependency **identity and shape** but should not duplicate exact dependency-version constants;
5. historical release notes and historical tranche records may retain exact versions because those values are evidence about what was shipped at that time;
6. no new abstraction layer is introduced merely to hide ordinary NuGet dependencies.

This policy is especially important for `Icod.TermInfo`: the library should remain loosely coupled to the public contracts it consumes rather than to a particular release number.

## Public capability-planning direction

The intended public distinction remains:

```text
inspect
    side-effect free
    reports what this TerminalSession currently knows

verify / prepare
    explicit and bounded
    may perform a live terminal probe when the semantic operation requires one
```

The public surface should describe semantic operations and support knowledge, not protocol mechanics.

Candidate questions include:

```text
Can raster graphics presently be used?
Can synchronized output presently be used?
Can modern keyboard reporting presently be acquired?
Is clipboard read/write presently known to be usable?
Is the output endpoint unavailable or redirected?
Is the current answer verified live, advertised statically, unsupported, or still unknown?
```

Callers should not need to ask:

```text
Is Kitty preferred over Sixel?
Which internal backend id won?
What numeric routing preference was assigned?
Which raw probe frame produced this evidence?
```

Those remain implementation details.

## Design guardrails

1. Inspection is side-effect free.
2. Live probing is never hidden behind a property getter or ordinary inspection call.
3. Capability state is semantic, not terminal-brand based.
4. Static TermInfo evidence remains valid static evidence; live observations may strengthen or override it according to existing internal rules.
5. Lifecycle generation changes continue to invalidate generation-scoped live evidence.
6. The public model does not expose the internal backend registry or preference table.
7. The public model does not expose arbitrary raw capability names as the primary semantic API.
8. No second terminal reader is introduced.
9. Verification remains bounded by timeout/cancellation and the existing query coordinator.
10. Existing 1.x APIs and enum numeric values remain stable unless an additive minor-version change is deliberately reviewed and baselined.

## Tranche plan

```text
C100  dependency-decoupling and validation hygiene                 active
C101  semantic capability vocabulary and API regret gate          planned
C102  side-effect-free capability inspection model                planned
C103  session inspection integration and evidence projection      planned
C104  explicit bounded verification / preparation                 planned
C105  lifecycle, invalidation, and concurrent-query semantics     planned
C106  samples and downstream planning acceptance                  planned
C107  adversarial/package hardening                               planned
C108  public API/documentation/compatibility freeze               planned
C109  1.10.0 release closure                                      planned
```

## C100 — dependency-decoupling and validation hygiene

Audit tests, samples, tools, packaging scripts, and workflows for exact dependency-version assumptions that are not required by the package declaration itself.

Required result:

- test projects continue to consume the repository project rather than pinning `Icod.TermInfo` independently;
- fresh package consumers restore the packed `Icod.Terminal` artifact and let NuGet resolve its declared dependency graph normally;
- the package verifier checks expected dependency IDs and per-TFM grouping without duplicating exact dependency-version constants;
- active documentation describes dependency resolution without implying that validation requires one exact resolved `Icod.TermInfo` release;
- historical documents remain unchanged;
- build/restore remains the compatibility proof.

C100 does not change the `Icod.Terminal` public runtime API.

## C101 — semantic capability vocabulary and API regret gate

Inventory the internal semantic operations and support/evidence states. Select the smallest public vocabulary that is stable enough to support higher-level planning without publishing backend or protocol internals.

The tranche must answer:

- which semantic capabilities are appropriate for public inspection;
- whether support state and evidence source should be one type or separate types;
- how endpoint unavailability differs from semantic unsupported state;
- which data is stable enough for public enums versus opaque/extensible identifiers;
- what information must remain internal.

No live probing API should be committed until this vocabulary passes the regret gate.

## C102 — side-effect-free capability inspection model

Add the immutable public result types needed to report what the session already knows. Inspection must not emit terminal bytes, acquire presentation state, or perform hidden queries.

## C103 — session inspection integration and evidence projection

Project the existing internal capability/evidence ledger into the reduced public model. Preserve internal routing details while proving static, live, unsupported, unknown, and unavailable cases.

## C104 — explicit bounded verification / preparation

Add an explicit async path for semantic capabilities where a caller wants stronger evidence and a bounded live probe exists. Reuse the authoritative query router and existing timeout/cancellation semantics.

Verification must not become a generic raw-query escape hatch.

## C105 — lifecycle, invalidation, and concurrent-query semantics

Qualify suspend/resume generation changes, stale live evidence, concurrent inspection, concurrent verification, cancellation, timeout, disposal, and query ownership.

## C106 — samples and downstream planning acceptance

Add a focused sample that demonstrates planning without protocol branching. A higher-level consumer should be able to choose a behavior from semantic capability knowledge without checking terminal brand or backend identity.

Retain current `Icod.DCurses` acceptance as a downstream witness.

## C107 — adversarial/package hardening

Exercise malformed observations, conflicting evidence, lifecycle churn, repeated verification, redirected endpoints, fresh NuGet-only consumers, supported TFMs, and bounded-resource guarantees.

## C108 — public API/documentation/compatibility freeze

Freeze the final 1.10 public API fingerprint and align Architecture, capability/evidence documentation, Security/Privacy, Compatibility/Versioning, README, samples, and XML documentation.

## C109 — release closure

Set final 1.10 package identity, changelog/release notes, run the complete unchanged-head Staging qualification, and hand the PR back to the maintainer for merge/tag/publication.

Merge, tagging, GitHub Release creation, and NuGet publication remain maintainer actions.

## Explicit exclusions

Version 1.10 does not expose the internal backend registry, routing preference scores, raw evidence ledger, arbitrary protocol frames, terminal-brand heuristics, persistent raster resources/placements, image codecs, PTY/ConPTY hosting, or DCurses virtual-screen policy.

Persistent raster resource/placement lifecycle remains the planned 1.11 track after capability planning is stable.
