# Icod.Terminal 1.0.0-rc1 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `1.0.0-rc1`  
**Predecessor:** `0.18.0` — Hardening and Invariant Closure  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** contract freeze, public-API regret audit, permanent documentation, compatibility policy, and 1.0 release-candidate proof  
**Status:** T190–T193 complete and green; T194 in progress

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

Durable authorities now exist for architecture, session ownership, lifecycle/restoration, input/events, query routing, modern keyboard compatibility, presentation/reversible state, semantic output protocols, and security/privacy. T195 will add compatibility/versioning and migration authorities; T194/T196 reconcile source XML, samples, README/package metadata, and machine-verifiable public/package baselines against those documents.

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

### T194 — public API/XML/sample regret closure — in progress

Current work:

1. generate a deterministic reflection snapshot of the exported public API on net8.0, net9.0, and net10.0;
2. prove all three TFMs expose exactly the same surface;
3. capture that result as the checked-in 1.x public API baseline and turn generation into verification;
4. freeze every current public enum numeric value through that same baseline;
5. remove release-number wording from public XML documentation where it describes a permanent contract;
6. reconcile exception/cancellation/restoration XML against T191–T193;
7. audit samples for the one-authoritative-reader/output-ownership rules;
8. rewrite `samples/README.md` as a task-oriented 1.x index rather than release chronology;
9. add samples only if an important 1.x workflow remains untaught.

No final API correction enters T194 without a new D-class justification.

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
docs/Compatibility-and-Versioning.md                # T195
docs/Migration-to-1.0.md                            # T195
docs/Public-API-Baseline-1.0-rc1.md                 # T194
```

Historical `Txxx` and `Public-API-Baseline-0.x` documents remain design/release evidence rather than required reading for the supported 1.x contract.

---

## 6. Frozen T190 decisions

- custom `ITerminalInput`/`ITerminalOutput` and `ITerminalControlProvider` injection remain public;
- a live `TerminalSession` does not return its raw input transport publicly;
- `TerminalSession.Output` remains an advanced caller-owned escape hatch outside session serialization;
- `ReadEventAsync(...)` is the canonical session-owned input path;
- typed query APIs are the canonical response-correlation path;
- `WriteTerminalStringAsync(...)` is the advanced already-resolved terminfo-string boundary, not a generic protocol recommendation;
- semantic protocol APIs remain bounded and typed rather than generic vendor dispatch;
- current public enum values will be frozen as the 1.x baseline rather than resurrecting superseded early-pre-1.0 layouts;
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
T194 snapshot:         capture pending
```

**Next:** run the T194 reflection snapshot generator on all supported TFMs, capture the identical output, commit it as the 1.x baseline, and convert the generator into an enforcing verification gate.
