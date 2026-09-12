# Icod.Terminal 1.11.1 Development Roadmap

**Release:** `1.11.1`  
**Theme:** TermInfo 1.11 ↔ Terminal 1.11 persistent-raster integration contract  
**Status:** planning complete; implementation intentionally paused pending maintainer questions  
**Development version:** target `1.11.1`; package metadata not yet advanced  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.11.0`  
**TermInfo Runtime dependency:** `Icod.TermInfo 1.11.0`

## Why this patch release exists

`Icod.Terminal 1.11.0` completed the live persistent-raster ownership/runtime layer: capability verification, acknowledged terminal-resident resource upload, placement creation/update, lifecycle invalidation, deterministic cleanup, and opaque public resource/placement handles.

`Icod.TermInfo 1.11.0` independently completed the protocol-neutral planning layer: persistent-raster lifecycle evidence, provenance, classification, semantic planning, description/database-set composition, and version-3 machine-readable profile/plan documents through `Icod.TermInfo.Inspection`.

Both releases intentionally preserve a loose architectural boundary:

```text
Icod.TermInfo.Inspection
    static/advisory evidence
    classification
    uncertainty / contradiction
    semantic planning

consumer-owned integration
    optional live verification
    evidence strengthening
    reclassification / replanning

Icod.Terminal
    live endpoint ownership
    bounded protocol verification
    persistent resource identity
    placement identity
    generation invalidation
    terminal-side execution / cleanup
```

Version `1.11.1` exists to prove that boundary end-to-end through an executable sample and deterministic contract tests without making Inspection or Source production dependencies of `Icod.Terminal`.

The implementation plan is:

[`docs/superpowers/plans/2026-09-12-1.11.1-terminfo-persistent-raster-integration.md`](docs/superpowers/plans/2026-09-12-1.11.1-terminfo-persistent-raster-integration.md)

The runtime ownership authority remains:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

The TermInfo semantic authority is:

```text
https://github.com/uniblab/Icod.TermInfo/blob/main/docs/1.11.0-PERSISTENT-RASTER-LIFECYCLE-GUIDE.md
```

## Release objectives

Version 1.11.1 must:

1. demonstrate TermInfo 1.11 static persistent-raster lifecycle inspection and planning over the exact `TerminalDescription` selected by a live `TerminalSession`;
2. preserve `Icod.TermInfo.Inspection` as an optional consumer/sample/test dependency rather than a production `Icod.Terminal` dependency;
3. prove an initially indeterminate TermInfo plan can be strengthened by Terminal-owned live verification and caller-owned `Verified` lifecycle evidence;
4. prove the strengthened profile can be reclassified/replanned deterministically before runtime execution;
5. prove verified non-support becomes an impossible semantic plan without attempting persistent-raster execution;
6. demonstrate the successful plan executing through the existing opaque `TerminalRasterResource` / `TerminalRasterPlacement` API only;
7. keep all Kitty/private protocol identities and command details out of the integration contract;
8. preserve the exact 1.11.0 public API and stable 1.0 compatibility floor;
9. preserve the production package graph: Runtime `Icod.TermInfo 1.11.0` plus `Icod.Timing 1.0.0`, with no new Inspection/Source production dependency;
10. qualify the complete integration contract on Windows, Linux, macOS and `net8.0`, `net9.0`, `net10.0`.

## Frozen integration direction

The intended consumer flow is:

```text
TerminalSession.Terminal
    -> PersistentRasterLifecycleInspector.Inspect(...)
    -> PersistentRasterLifecyclePlanner.Plan(...)

if plan == Indeterminate
    -> TerminalSession.VerifyCapabilityAsync(PersistentRasterGraphics)
    -> consumer maps Terminal live result to Verified lifecycle evidence
    -> PersistentRasterLifecycleClassifier.Classify(...)
    -> PersistentRasterLifecyclePlanner.Plan(...)

if plan == Success
    -> TerminalSession.CreateRasterResourceAsync(...)
    -> TerminalRasterResource.CreatePlacementAsync(...)
    -> TerminalRasterPlacement.UpdateAsync(...)
    -> dispose placement/resource

if plan == Impossible
    -> do not attempt persistent-raster execution
```

The TermInfo request used by the core integration contract covers:

```text
persistent resource upload
acknowledged upload
one placement
placement update
placement deletion
resource deletion
```

The sample may explain ordinary raster separately, but persistent and ephemeral intent must not be mixed into one TermInfo lifecycle request.

## Consumer-owned verified evidence mapping

Terminal's public `PersistentRasterGraphics` capability is deliberately coarser than TermInfo's lifecycle profile. For this integration contract, a consumer-owned bridge may interpret a **Terminal live verification result** as follows:

```text
RasterGraphics == Verified
    -> RasterDisplay positive Verified evidence

PersistentRasterGraphics == Verified
    -> PersistentUpload positive Verified evidence
    -> AcknowledgedUpload positive Verified evidence
    -> PlacementCreation positive Verified evidence
    -> MultiplePlacements positive Verified evidence
    -> PlacementUpdate positive Verified evidence
    -> PlacementDeletion positive Verified evidence
    -> ResourceDeletion positive Verified evidence

PersistentRasterGraphics == Unsupported
    -> the seven persistent lifecycle subjects above receive negative Verified evidence
```

This mapping is sample/test integration policy, not a new TermInfo or Terminal production API contract.

`Unknown`, `Advertised`, or endpoint unavailability must not be promoted to caller-owned `Verified` support/non-support.

## Tranche roadmap

```text
T1111-A  integration dependency boundary and dedicated contract-test project   planned
T1111-B  consumer-owned live-evidence mapping contract                         planned
T1111-C  static-plan → live-verification → replan contract tests                planned
T1111-D  protocol-neutral executable integration sample                         planned
T1111-E  package/downstream qualification and 1.11.1 release closure            planned
```

Implementation is intentionally paused before `T1111-A` pending maintainer discussion.

## T1111-A — integration dependency boundary

Create a dedicated integration-contract test project which references `Icod.Terminal` plus `Icod.TermInfo.Inspection 1.11.0` without adding Inspection or Source to `Icod.Terminal.csproj`.

Acceptance must prove:

- the production package graph is unchanged;
- the test-only integration project builds on all three TFMs;
- the project is part of normal solution/CI verification;
- package verification continues to reject unexpected production dependencies.

## T1111-B — consumer-owned live-evidence mapping

Define and test the integration-only translation from Terminal's live semantic capability results to TermInfo `PersistentRasterLifecycleEvidenceKind.Verified` assertions.

Acceptance must cover:

- positive persistent verification;
- verified persistent non-support;
- ordinary raster verification;
- no promotion of `Unknown` or `Advertised` status to `Verified` evidence;
- endpoint unavailability remaining distinct from terminal non-support;
- deterministic provenance labels and ordinals;
- no protocol/backend/private-id leakage.

## T1111-C — plan/verify/replan contract

Add deterministic integration tests proving:

1. an ordinary terminal description with no lifecycle declarations produces an `Indeterminate` persistent request;
2. Terminal-owned live verification strengthens knowledge through caller-owned evidence;
3. reclassification/replanning becomes `Success` when live support is verified;
4. verified non-support replans to `Impossible`;
5. a description carrying the exact Icod extended Boolean lifecycle declarations can produce a successful static plan without planning I/O;
6. planning itself never performs terminal I/O or allocates Terminal runtime identities.

## T1111-D — executable integration sample

Create a non-packable sample under:

```text
samples/Icod.Terminal.TermInfoPersistentRaster.Sample/
```

The sample must be readable as executable architecture documentation and show:

```text
selected TerminalDescription
    -> TermInfo Inspection profile
    -> TermInfo lifecycle request / plan
    -> optional Terminal live verification
    -> caller-owned Verified evidence
    -> reclassify / replan
    -> Terminal persistent resource
    -> placement create/update/dispose
```

The sample must not expose Kitty image ids, placement ids, raw APC commands, or backend-selection policy in its user-facing flow.

When execution is unavailable or the semantic plan is not successful, the sample must report the semantic state and avoid pretending the persistent operation ran.

CI must compile the sample for all supported TFMs; deterministic contract tests own simulated live execution coverage.

## T1111-E — qualification and release closure

After T1111-A through T1111-D are accepted:

- advance the release metadata to stable `1.11.1`;
- preserve `Icod.TermInfo 1.11.0` and `Icod.Timing 1.0.0` production dependencies;
- preserve the stable `1.0.0` compatibility floor;
- require the normalized public API fingerprint to remain:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

Any public API change is a stop/review condition, not an automatic baseline update.

Final qualification must include:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- package candidate/public API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- validated package artifact;
- dedicated TermInfo/Terminal integration tests on all TFMs;
- integration sample builds on all TFMs.

## Explicit exclusions

Version 1.11.1 does **not** include:

- a production dependency on `Icod.TermInfo.Inspection`;
- a production dependency on `Icod.TermInfo.Source`;
- new public Terminal planning/profile/evidence types;
- direct ingestion of all eight TermInfo lifecycle dimensions into Terminal's coarse capability model;
- an adapter NuGet package between TermInfo and Terminal;
- changes to the persistent-raster wire protocol or runtime ownership semantics frozen by 1.11.0;
- a table-driven rewrite of `TerminalTermInfoSemanticEvidence` unless scope is explicitly widened after maintainer review;
- scene/window/cell/layout policy belonging to `Icod.DCurses`.

## Approved future follow-up after 1.11.1

The next independent internal cleanup is to table-drive `TerminalTermInfoSemanticEvidence` so exact TermInfo implementations and metadata-backed backend advertisements are described through reviewed immutable contracts instead of duplicated conditionals/switches.

That work must preserve behavior, package dependencies, public API, and existing evidence semantics and should be reviewed independently from this integration patch.

Static lifecycle-evidence ingestion and any adapter package remain later architectural decisions.

## Release gate

The repository's established progression remains:

```text
written contract
    -> focused RED tests
        -> minimal GREEN integration
            -> sample/documentation closure
                -> full Windows/Linux/macOS + package qualification
                    -> exact-head acceptance
```

No implementation begins until the maintainer's pending questions are resolved.
