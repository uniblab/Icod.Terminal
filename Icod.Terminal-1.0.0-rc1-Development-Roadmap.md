# Icod.Terminal 1.0.0-rc1 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `1.0.0-rc1`  
**Predecessor:** `0.18.0` — Hardening and Invariant Closure  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** contract freeze, public-API regret audit, permanent documentation, compatibility policy, and 1.0 release-candidate proof  
**Status:** T190–T193 complete and green; T194 implementation complete, exact-head validation pending

---

## 1. Release intent

`1.0.0-rc1` is the point where `Icod.Terminal` stops being documented primarily as a sequence of pre-1.0 feature releases and becomes documented as one coherent terminal/session platform.

The default rule for this release is:

> freeze, reconcile, document, and prove the existing contract before adding anything new.

No new terminal protocol is planned for rc1. New API is allowed only when the public-API regret audit demonstrates that the existing shape would be materially harder or less safe to support after 1.0 without a final pre-1.0 correction.

`1.0.0` itself is reserved for release closure after the rc1 contract has survived final downstream and package validation.

---

## 2. RC1 success criteria

The release candidate is complete only when a new consumer can understand the 1.0 contract without reading versioned tranche notes from 0.1 through 0.18.

Durable authorities now exist for architecture, session ownership, lifecycle/restoration, input/events, query routing, modern keyboard compatibility, presentation/reversible state, semantic output protocols, security/privacy, and the frozen rc1 public API. T195 will add compatibility/versioning and migration authorities; T196 will reconcile README/package metadata and package-only rc1 documentation artifacts.

---

## 3. Release rules

During rc1:

- preserve stable 0.18 behavior unless a documented 1.0 regret correction requires change;
- do not add raw escape-sequence or arbitrary vendor-command surfaces;
- prefer semantic APIs and truthful reversible ownership;
- retain exact restoration rather than guessed defaults;
- retain bounded parsing/query ownership;
- keep Windows/POSIX differences observable rather than fabricated;
- retain `net8.0`, `net9.0`, and `net10.0` unless a concrete security/maintenance constraint requires reconsideration;
- retain all 0.8–0.18 package gates until equivalent 1.0 umbrella gates demonstrably subsume them;
- validate real `Icod.DCurses` throughout the release candidate;
- treat stale documentation as a release defect.

---

## 4. Tranche status

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

T193 permanently distinguishes exact restoration, terminal-policy reset, Icod-owned nested state without an observable external baseline, and ephemeral metadata. It also freezes the bounded safe OSC 9 subset and hazardous-command exclusions as a 1.x safety decision.

### T194 — public API/XML/sample regret closure — implementation complete

The exact post-cleanup head must still pass the full PR gate before T194 is closed.

Public API capture:

- capture head: `510f9738f216064f80193eba1936583b5552170e`;
- capture validation: workflow #926;
- net8.0/net9.0/net10.0 snapshots identical;
- normalized snapshot: 633 lines;
- exported public types: 77;
- public enums: 32 / 242 values;
- public methods: 124;
- public properties: 144;
- public constructors: 13;
- frozen SHA-256: `8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5`.

Permanent/machine authorities:

- `docs/Public-API-Baseline-1.0-rc1.md`;
- `docs/Public-API-Baseline-1.0-rc1.sha256`;
- `packaging/GeneratePublicApiBaseline.ps1`;
- `packaging/VerifyPublicApiBaseline.ps1`.

The verifier regenerates the reflection surface on all three TFMs, normalizes line endings, fails on TFM divergence, and fails on any drift from the frozen fingerprint. It is wired into PR validation and full distribution validation.

T194 also:

- freezes every current public enum numeric value through the machine baseline;
- removes release-number wording from consumer-visible XML where it described permanent query/resource/protocol semantics;
- confirms no additional D-class API correction is justified;
- audits the existing sample set after removal of public `TerminalSession.Input`;
- rewrites `samples/README.md` around consumer tasks and ownership rules rather than release chronology;
- adds no new sample because the important 1.x workflows are already represented.

Record:

- `docs/T194-Public-API-XML-and-Sample-Regret-Closure.md`.

### T195 — compatibility, migration, and support policy

Create:

- `docs/Compatibility-and-Versioning.md`;
- `docs/Migration-to-1.0.md`.

Freeze source/binary compatibility, semantic-versioning, TFM/platform support, deprecation policy, capability-vs-emulator behavior, pre-1.0 migration, and direct-vs-DCurses consumption guidance.

### T196 — package metadata and documentation artifact closure

Replace stale package release notes; audit package description/tags/README/license/icon/repository metadata; verify XML docs on all TFMs; add a package-only 1.0-rc1 contract; retain historical package gates.

### T197 — downstream RC acceptance and stable-candidate closure

Require exact Windows/Linux/macOS PR green, real DCurses acceptances/soak, fresh package-only consumers on all supported TFMs, no accidental public API drift, Release x64/ARM64 distribution validation after merge, and no stale in-progress rc1 authorities.

Tag/publish only when explicitly authorized.

---

## 5. Permanent documentation set

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
docs/Compatibility-and-Versioning.md                # T195
docs/Migration-to-1.0.md                            # T195
```

Historical `Txxx` and `Public-API-Baseline-0.x` documents remain design/release evidence rather than required reading for the supported 1.x contract.

---

## 6. Frozen T190/T194 API decisions

- custom `ITerminalInput`/`ITerminalOutput` and `ITerminalControlProvider` injection remain public;
- a live `TerminalSession` does not return its raw input transport publicly;
- `TerminalSession.Output` remains an advanced caller-owned escape hatch outside session serialization;
- `ReadEventAsync(...)` is the canonical session-owned input path;
- typed query APIs are the canonical response-correlation path;
- `WriteTerminalStringAsync(...)` is the advanced already-resolved terminfo-string boundary, not a generic protocol recommendation;
- semantic protocol APIs remain bounded and typed rather than generic vendor dispatch;
- the exact current exported API and every current public enum value are frozen by the rc1 machine fingerprint;
- no additional pre-1.0 API correction was justified by T194;
- no new protocol work enters rc1.

---

## 7. Current baseline

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
T194 API fingerprint:  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
T194 final validation: pending exact current head
```

**Next:** validate the exact completed T194 head. If green, close T194 and begin T195 compatibility/versioning and migration documentation.
