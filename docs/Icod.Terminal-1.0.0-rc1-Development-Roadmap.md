# Icod.Terminal 1.0.0-rc1 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `1.0.0-rc1`  
**Predecessor:** `0.18.0` — Hardening and Invariant Closure  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** contract freeze, public-API regret audit, permanent documentation, compatibility policy, and 1.0 release-candidate proof  
**Status:** T190–T197 complete; rc1 merged to `main` and qualified by the six-runner Release distribution matrix

---

## 1. Release intent

`1.0.0-rc1` is the point where `Icod.Terminal` stops being documented primarily as a sequence of pre-1.0 feature releases and becomes documented as one coherent terminal/session platform.

The governing rule is:

> freeze, reconcile, document, and prove the existing contract before adding anything new.

No new terminal protocol is planned for rc1. `1.0.0` itself remains reserved for stable release closure after the release-candidate contract has had time for consumer feedback.

---

## 2. Release rules

During rc1:

- preserve stable 0.18 behavior unless a documented 1.0 regret correction requires change;
- do not add raw escape-sequence or arbitrary vendor-command surfaces;
- prefer semantic APIs and truthful reversible ownership;
- retain exact restoration rather than guessed defaults;
- retain bounded parsing/query ownership;
- keep Windows/POSIX differences observable rather than fabricated;
- retain `net8.0`, `net9.0`, and `net10.0` unless a concrete security/maintenance constraint requires reconsideration;
- retain historical package gates until equivalent 1.x coverage demonstrably subsumes them;
- validate real downstream integration without treating an early downstream as exhaustive proof of the Terminal contract;
- treat stale current documentation as a release defect.

---

## 3. Tranche status

### T190 — contract and regret audit — complete

Workflow #911.

One D-class correction was justified before 1.0: public `TerminalSession.Input` was removed while `ITerminalInput` remains the custom-transport injection seam. No additional API redesign was justified.

Records:

- `docs/T190-1.0-Contract-and-Regret-Audit-Freeze.md`;
- `docs/T190-Public-API-Regret-Audit.md`.

### T191 — permanent architecture and ownership — complete

Workflow #918 at `c16949d94cb8471b9b776ec4c5dec4a715ea10de`.

Authorities:

- `docs/Architecture.md`;
- `docs/Terminal-Session-and-Ownership.md`;
- `docs/Lifecycle-and-Restoration.md`.

### T192 — permanent input/query/protocol semantics — complete

Workflow #924 at `528ef6a59aff5ec654bc59571018f1ef70cd92e5`.

Authorities:

- `docs/Input-and-Events.md`;
- `docs/Queries-and-Responses.md`;
- `docs/Modern-Keyboard-Security-and-Compatibility.md`.

### T193 — permanent output/presentation/security semantics — complete

Workflow #925 at `7e62057ff6c0c8ba64e820d37823fc78cdad1674`.

Authorities:

- `docs/Presentation-and-Reversible-State.md`;
- `docs/Semantic-Output-Protocols.md`;
- `docs/Security-and-Privacy.md`.

T193 permanently distinguishes exact restoration, terminal-policy reset, Icod-owned nested state without an observable external baseline, and ephemeral metadata. It also freezes the bounded safe OSC 9 subset and hazardous-command exclusions as a 1.x safety decision.

### T194 — public API/XML/sample regret closure — complete

Workflow #947 at `86ff0cfc923314aca35bcf8fb731a97bd0ade526`.

The rc1 public API was captured independently under net8.0/net9.0/net10.0 and frozen as:

```text
633 lines
77 exported public types
32 public enums / 242 values
124 public methods
144 public properties
13 public constructors
SHA-256 8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

Authorities/gates:

- `docs/Public-API-Baseline-1.0-rc1.md`;
- `docs/Public-API-Baseline-1.0-rc1.sha256`;
- `packaging/GeneratePublicApiBaseline.ps1`;
- `packaging/VerifyPublicApiBaseline.ps1`.

T194 also made public XML version-neutral, froze current enum numeric values, audited samples after the `TerminalSession.Input` correction, and rewrote `samples/README.md` as a task-oriented 1.x guide.

### T195 — compatibility, migration, and support policy — complete

Workflow #952 at `676aaff4ac552eeb3e824fb7b6027ef241b3c486`.

Authorities:

- `docs/Compatibility-and-Versioning.md`;
- `docs/Migration-to-1.0.md`.

T195 freezes conservative SemVer expectations, enum-value stability, deprecation policy, net8/net9/net10 support, Windows/Linux/macOS built-in provider support, evidence-based protocol support semantics, migration from `TerminalSession.Input`, and direct-vs-DCurses ownership guidance.

### T196 — package metadata and documentation artifact closure — complete

Workflow #954 at exact head `09ef20a2b0eee2fe3202459a906e837001c7ba6d`.

T196:

- updates package release notes to `1.0.0-rc1`;
- rewrites the root/package README for the rc1/1.x contract;
- preserves the original long-form development roadmap under `docs/history/` and makes the root roadmap a current index;
- adds a fresh NuGet-only rc1 package consumer on net8/net9/net10;
- verifies current nuspec release notes, packed README, generated XML docs, removed `TerminalSession.Input`, retained injection/output seams, representative 1.x APIs, and enum anchors;
- integrates the rc1 package gate into PR and full Release distribution validation;
- retains all package-only gates from 0.8 through 0.18.

Record:

- `docs/T196-Package-Metadata-and-Documentation-Artifact-Closure.md`.

### T197 — final downstream RC acceptance and release-candidate closure — complete

Substantive validation: workflow #956 at exact head `039927f555f8f109a31423825dc0e2191a5c2ac6`.

Final PR closure validation: workflow #957 at exact head `254ceea67f5ca01070af15d928faba630d31af79`.

T197 adds a fresh-package downstream compatibility witness by running the existing eight-cycle DCurses hardening soak unchanged against:

```text
freshly packed Icod.Terminal 1.0.0-rc1
+
published Icod.DCurses 0.1.0
```

on net8.0, net9.0, and net10.0.

The DCurses gate is intentionally treated as a real compatibility witness for the integration paths its current `0.1.0` release exercises, not as exhaustive proof of every `Icod.Terminal` feature. Terminal's own frozen API, unit/hardening tests, invariant coverage, exact package verification, retained 0.8–0.18 package contracts, and fresh rc1 package contract remain the primary release evidence for the full 1.x surface.

The complete PR audit found no additional production feature, test, sample, package, documentation, or API correction that justifies expanding rc1.

Record:

- `docs/T197-Final-Downstream-RC-Acceptance-and-Release-Candidate-Closure.md`.

---

## 4. Post-merge rc1 qualification

PR #32 merged to `main` as:

`852d84722c6d9b5f91b5c6dcf9176f26ded78982`

Release workflow run `34163501979` completed successfully on all configured distribution runners:

- Windows x64;
- Windows ARM64;
- Linux x64;
- Linux ARM64;
- macOS x64;
- macOS ARM64.

That run executed `VerifyDistribution.ps1`, including the frozen API baseline, repository tests, real DCurses integration/soak, exact package verification, retained 0.8–0.18 package contracts, fresh rc1 package contract, and package-boundary DCurses witness.

A later publication-only hardening commit must pass the same `main` Release matrix before a tag is authorized; this roadmap does not need another mutation merely to insert that later run number.

---

## 5. Release-publication hardening

Before tagging rc1, the release path is being normalized so the tag-triggered workflow is not weaker than the qualified `main` distribution path.

Release-publication requirements include:

- curated `docs/releases/<version>.md` GitHub Release notes;
- a root `CHANGELOG.md`;
- concise NuGet `PackageReleaseNotes` linking to the curated notes and migration guide;
- immutable tag-pinned package documentation links;
- tag-time frozen public-API verification;
- tag-time DCurses hardening soak;
- tag-time 0.18 hardening, rc1 package, and package-boundary downstream gates;
- no fallback to sparse auto-generated GitHub Release notes when curated notes are missing.

These are publication/documentation controls, not a new runtime feature tranche.

---

## 6. Permanent documentation set

```text
docs/Architecture.md
docs/Terminal-Session-and-Ownership.md
docs/Lifecycle-and-Restoration.md
docs/Input-and-Events.md
docs/Queries-and-Responses.md
docs/Modern-Keyboard-Security-and-Compatibility.md
docs/Presentation-and-Reversible-State.md
docs/Semantic-Output-Protocols.md
docs/Security-and-Privacy.md
docs/Public-API-Baseline-1.0-rc1.md
docs/Compatibility-and-Versioning.md
docs/Migration-to-1.0.md
CHANGELOG.md
docs/releases/1.0.0-rc1.md
```

Historical T-series, 0.x public API baselines, and the original long-form roadmap remain preserved as design/release evidence.

---

## 7. Frozen API/compatibility decisions

- `ITerminalInput`, `ITerminalOutput`, and `ITerminalControlProvider` injection remain public;
- a live `TerminalSession` does not expose its raw input transport;
- `TerminalSession.Output` remains an advanced caller-owned escape hatch outside session serialization;
- `ReadEventAsync(...)` is the canonical application-input path;
- typed queries are the canonical response-correlation path;
- semantic protocol APIs remain bounded/typed rather than generic vendor dispatch;
- the rc1 machine fingerprint freezes the current exported surface and enum values;
- compatible additions require deliberate minor-release review and baseline update;
- ordinary breaking removals/renames/signature changes belong to a new major;
- package compatibility does not fabricate terminal-protocol support evidence;
- no new protocol work enters rc1.

---

## 8. Qualified baseline

```text
Published predecessor: v0.18.0
PR #32 final head:      254ceea67f5ca01070af15d928faba630d31af79
Merged main SHA:        852d84722c6d9b5f91b5c6dcf9176f26ded78982
Version:                1.0.0-rc1
PackageVersion:         1.0.0-rc1
AssemblyVersion:        1.0.0.0
TargetFrameworks:       net8.0;net9.0;net10.0
T190 validation:        workflow #911
T191 validation:        workflow #918
T192 validation:        workflow #924
T193 validation:        workflow #925
T194 capture:           workflow #926
T194 validation:        workflow #947
T194 API fingerprint:   8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
T195 validation:        workflow #952
T196 validation:        workflow #954
T197 substantive gate:  workflow #956
T197 final PR gate:     workflow #957
Merged Release gate:    run 34163501979 — green on all six runners
```

**Next:** merge the release-publication hardening PR only after its exact PR head is green, then require the resulting `main` commit to pass the same six-runner Release matrix. Create `v1.0.0-rc1` only with explicit publication authorization.
