# Icod.Terminal 1.0.0-rc1 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `1.0.0-rc1`  
**Predecessor:** `0.18.0` — Hardening and Invariant Closure  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** contract freeze, public-API regret audit, permanent documentation, compatibility policy, and 1.0 release-candidate proof  
**Status:** T190–T196 complete and green; T197 implementation complete, exact-head validation pending

---

## 1. Release intent

`1.0.0-rc1` is the point where `Icod.Terminal` stops being documented primarily as a sequence of pre-1.0 feature releases and becomes documented as one coherent terminal/session platform.

The default rule is:

> freeze, reconcile, document, and prove the existing contract before adding anything new.

No new terminal protocol is planned for rc1. `1.0.0` itself remains reserved for stable release closure after the release-candidate contract has survived downstream/package validation.

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
- validate real `Icod.DCurses` throughout;
- treat stale current documentation as a release defect.

---

## 3. Tranche status

### T190 — contract and regret audit — complete

Workflow #911. One D-class correction: public `TerminalSession.Input` was removed while `ITerminalInput` remains the custom-transport injection seam. No additional API redesign was justified.

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

T194 also made public XML version-neutral and rewrote `samples/README.md` as a task-oriented 1.x guide.

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

### T197 — final downstream RC acceptance and release-candidate closure — implementation complete

Exact-head validation pending.

T197 closes the remaining artifact-boundary gap by running the **existing eight-cycle DCurses hardening soak unchanged** against:

```text
freshly packed Icod.Terminal 1.0.0-rc1
+
published Icod.DCurses 0.1.0
```

on net8.0, net9.0, and net10.0.

Added:

- `tools/dcurses-rc1-package-acceptance/Icod.Terminal.DCursesRc1PackageAcceptance.csproj`;
- `tools/dcurses-rc1-package-acceptance/Program.cs` (same program as the project-reference hardening soak);
- `packaging/VerifyDCursesRc1Package.ps1`.

The gate is wired into PR Staging and full Release distribution validation after the rc1 package-only contract.

Record:

- `docs/T197-Final-Downstream-RC-Acceptance-and-Release-Candidate-Closure.md`.

After the substantive T197 head is green, perform the final complete-PR audit. Any status/documentation closure commit must receive its own exact-head validation before PR #32 is called merge-ready.

---

## 4. Permanent documentation set

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
```

Historical T-series, 0.x public API baselines, and the original long-form roadmap remain preserved as design/release evidence.

---

## 5. Frozen API/compatibility decisions

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

## 6. Current baseline

```text
Published predecessor: v0.18.0
Main baseline SHA:     c0f0ff482c8bb0d358c4fcb38e454d717b1b6f1c
Version:               1.0.0-rc1
PackageVersion:        1.0.0-rc1
AssemblyVersion:       1.0.0.0
TargetFrameworks:      net8.0;net9.0;net10.0
T190 validation:       workflow #911
T191 validation:       workflow #918
T192 validation:       workflow #924
T193 validation:       workflow #925
T194 capture:          workflow #926
T194 validation:       workflow #947
T194 API fingerprint:  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
T195 validation:       workflow #952
T196 validation:       workflow #954
T197 validation:       pending exact current head
```

**Next:** validate the exact T197 implementation head. If green, perform the final rc1 audit and closure-only status update, then validate that exact final head before merge readiness.
