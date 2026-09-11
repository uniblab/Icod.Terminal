# Icod.Terminal 1.10.0 Development Roadmap

**Release:** `1.10.0`  
**Theme:** semantic capability inspection and planning  
**Status:** C100–C109 complete; release candidate awaiting maintainer merge/tag/publication  
**Development version:** `1.10.0`  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.9.0`

## Why this release exists

Versions 1.5 through 1.9 built a mature internal model for terminal capability evidence, semantic operation routing, live observation, endpoint availability, and lifecycle invalidation. Higher-level consumers still could not inspect that knowledge through a small public semantic contract without either performing protocol-specific queries themselves or learning implementation details that should remain internal to `Icod.Terminal`.

Version 1.10 exposes a deliberately reduced semantic planning surface while preserving the existing separation between static terminal descriptions and the live terminal conversation owned by `Icod.Terminal`.

The architectural objective is:

> Let callers ask what semantic terminal operations are presently known to be usable, and explicitly request bounded verification when live probing is necessary, without exposing protocol backends, routing preference tables, raw evidence ledgers, terminal-brand heuristics, or dependency-specific implementation details.

## Dependency-coupling policy

`Icod.Terminal` necessarily declares NuGet dependency requirements in its package project. Those declarations are package metadata, not test policy.

For active tests, samples, package smoke consumers, and verification tools:

1. do not independently pin `Icod.TermInfo`, `Icod.Timing`, or other transitive runtime dependencies merely to reproduce the package project's version declaration;
2. do not treat one exact resolved dependency version as part of the `Icod.Terminal` behavioral contract;
3. successful restore/build against the package graph is sufficient dependency compatibility evidence unless a concrete incompatibility is being diagnosed;
4. package verification may require the expected dependency **identity and shape** but should not duplicate exact dependency-version constants;
5. historical release notes and historical tranche records may retain exact versions because those values are evidence about what was shipped at that time;
6. no new abstraction layer is introduced merely to hide ordinary NuGet dependencies.

This policy is especially important for `Icod.TermInfo`: the library remains coupled to the public contracts it consumes rather than to a particular release number.

## C100 acceptance

C100 is complete on exact head:

```text
69fb3eee3ad7a36f3b3ef56c2cff14cac5665ed3
```

Acceptance workflow:

```text
#1480 / 34537262477
```

The workflow completed successfully across Windows, Linux, macOS, package candidate, all package-contract shards, and the validated package artifact.

C100 established:

- `Icod.Terminal.csproj` is the sole package authority for direct runtime-dependency versions;
- auxiliary projects under `/tests`, `/samples`, and `/tools` do not directly pin `Icod.TermInfo` or `Icod.Timing`;
- package verification checks dependency identity and per-TFM grouping without duplicating exact dependency-version constants;
- the fresh package smoke consumer references only packed `Icod.Terminal` and lets NuGet resolve its transitive dependency graph;
- active docs describe the loose-coupling policy;
- historical release/tranche documents remain unchanged;
- restore/build is the dependency-compatibility proof.

C100 changes no public runtime API.

## C101–C104 result

C101 froze a deliberately reduced, dependency-neutral public planning vocabulary in:

[`docs/C101-Semantic-Capability-Vocabulary-and-API-Regret-Gate.md`](docs/C101-Semantic-Capability-Vocabulary-and-API-Regret-Gate.md)

The initial public capability set is:

```text
ClipboardRead
ClipboardWrite
CursorStyle
SynchronizedOutput
KeyboardReporting
MouseReporting
FocusReporting
BracketedPaste
RasterGraphics
```

Support, endpoint availability, and evidence lifetime are separate dimensions. Public evidence is intentionally projected as only:

```text
None
StaticDescription
LiveObservation
```

`Icod.TermInfo`, OSC, CSI, DCS, APC, concrete backends, raw capability names, routing scores, and terminal-brand heuristics remain private implementation detail.

C102 added the immutable public value model:

```text
TerminalCapability
TerminalCapabilitySupport
TerminalCapabilityEndpointAvailability
TerminalCapabilityEvidenceKind
TerminalCapabilityStatus
```

C103 added the side-effect-free public session operation:

```csharp
public TerminalCapabilityStatus InspectCapability(
    TerminalCapability capability
);
```

Inspection reads existing session knowledge only. It emits no terminal bytes and performs no hidden live probe. Support is resolved independently from endpoint availability so that, for example, an advertised output capability can remain `Advertised` while its endpoint is `Unavailable` and `IsUsable` is false.

C104 added the explicit bounded verification operation:

```csharp
public ValueTask<TerminalCapabilityStatus> VerifyCapabilityAsync(
    TerminalCapability capability,
    CancellationToken cancellationToken = default
);
```

Verification attempts to strengthen evidence only for capabilities with an already-reviewed bounded probe. The initial probe set is `KeyboardReporting` and `RasterGraphics`. Other capabilities return their current inspection status without invented protocol traffic. Unavailable endpoints and already-decisive live evidence also short-circuit without probing.

The reviewed 1.10 public API snapshot is identical across `net8.0`, `net9.0`, and `net10.0`, with final fingerprint:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

The final 1.10 baseline is maintained in:

- [`docs/Public-API-Baseline-1.10.md`](docs/Public-API-Baseline-1.10.md)
- `docs/Public-API-Baseline-1.10.sha256`

## Public capability-planning direction

The public distinction is:

```text
inspect
    side-effect free
    reports what this TerminalSession currently knows

verify
    explicit and bounded
    attempts to strengthen current knowledge only where a reviewed live probe exists
```

The public surface describes semantic operations and support knowledge, not protocol mechanics or dependency implementation.

Callers should not need to ask:

```text
Is Kitty preferred over Sixel?
Which internal backend id won?
What numeric routing preference was assigned?
Which raw probe frame produced this evidence?
Did this static evidence specifically come from Icod.TermInfo?
```

Those remain implementation details.

## Design guardrails

1. Inspection is side-effect free.
2. Live probing is never hidden behind a property getter or ordinary inspection call.
3. Capability state is semantic, not terminal-brand based.
4. Static terminal-description evidence remains valid static evidence; live observations may strengthen or override it according to existing internal rules.
5. Lifecycle generation changes continue to invalidate generation-scoped live evidence.
6. The public model does not expose the internal backend registry or preference table.
7. The public model does not expose arbitrary raw capability names as the primary semantic API.
8. The public model does not expose `Icod.TermInfo`, OSC, CSI, DCS, APC, or vendor names as evidence categories.
9. No second terminal reader is introduced.
10. Verification reuses only reviewed bounded probes and the existing authoritative query coordinator.
11. A capability without a reviewed support probe is not assigned fabricated verification traffic merely to satisfy the public API.
12. Existing 1.x APIs and enum numeric values remain stable unless an additive minor-version change is deliberately reviewed and baselined.

## Tranche result

```text
C100  dependency-decoupling and validation hygiene                 complete
C101  semantic capability vocabulary and API regret gate          complete
C102  side-effect-free capability inspection model                complete
C103  session inspection integration and evidence projection      complete
C104  explicit bounded verification / preparation                 complete
C105  lifecycle, invalidation, and concurrent-query semantics     complete
C106  samples and downstream planning acceptance                  complete
C107  adversarial/package hardening                               complete
C108  public API/documentation/compatibility freeze               complete
C109  1.10.0 release closure                                      complete
```

## C105–C108 acceptance

C105–C108 are accepted as recorded in:

[`docs/C105-C108-Capability-Lifecycle-Samples-Hardening-and-API-Freeze.md`](docs/C105-C108-Capability-Lifecycle-Samples-Hardening-and-API-Freeze.md)

The accepted feature head was:

```text
f5272594f31188e4c3f096e61e226c7d550319b6
```

Acceptance workflow:

```text
#1518 / 34594556128
```

These tranches established lifecycle/invalidation/concurrency qualification, the capability-planning sample, downstream/package acceptance, adversarial hardening, and the final public API freeze.

## C109 — release closure

C109 closes the development line as a stable `1.10.0` release candidate.

Release-facing state now records:

- repository/package identity `1.10.0` with no prerelease suffix;
- stable package release notes in `Icod.Terminal.csproj`;
- stable 1.10 README installation and capability-planning guidance;
- final `CHANGELOG.md` 1.10 entry;
- curated `docs/releases/1.10.0.md` release notes;
- permanent Architecture and Security/Privacy capability-planning contracts;
- `docs/Compatibility-and-Versioning.md` promoted to the 1.10 baseline and 1.10 enum/API contract;
- final public API fingerprint `ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb`;
- this versioned roadmap closed and the long-range roadmap handed to 1.11 persistent raster resource/placement work.

The final C109 exact-head Staging workflow is the final acceptance witness for merge readiness. Merge, tagging, GitHub Release creation, and NuGet publication remain maintainer actions.

## Explicit exclusions

Version 1.10 does not expose the internal backend registry, routing preference scores, raw evidence ledger, arbitrary protocol frames, dependency-specific evidence identities, terminal-brand heuristics, persistent raster resources/placements, image codecs, PTY/ConPTY hosting, or DCurses virtual-screen policy.

Persistent raster resource/placement lifecycle is the next planned 1.11 development track.
