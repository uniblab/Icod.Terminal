# Icod.Terminal 1.0.0-rc1 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `1.0.0-rc1`  
**Predecessor:** `0.18.0` — Hardening and Invariant Closure  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** contract freeze, public-API regret audit, permanent documentation, compatibility policy, and 1.0 release-candidate proof  
**Status:** T190–T191 complete and green; T192 implementation complete, exact-head validation pending

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

The repository must therefore provide durable, non-versioned documentation for:

- architecture and layer boundaries;
- terminal session ownership and endpoint semantics;
- platform mode capture/application/restoration;
- lifecycle, suspend/resume, invalidation, and disposal;
- input events and rich-input lease ownership;
- active query routing, ambiguity, timeout, cancellation, and late-response ownership;
- presentation ownership and screen-local state;
- semantic output protocols and their safety/compatibility boundaries;
- failure, rollback, and state-uncertainty semantics;
- supported platforms/TFMs and compatibility policy;
- security/privacy considerations;
- downstream integration expectations;
- package/versioning support promises.

All public API and XML documentation must agree with those permanent documents.

---

## 3. Release rules

During rc1:

- preserve all stable 0.18 behavioral guarantees unless a documented 1.0 regret correction requires change;
- do not add raw escape-sequence or arbitrary vendor-command surfaces;
- prefer semantic APIs and reversible ownership;
- retain exact restoration rather than guessed defaults;
- retain bounded parsing/query ownership;
- keep Windows/POSIX differences observable rather than fabricated;
- retain `net8.0`, `net9.0`, and `net10.0` unless a concrete security/maintenance constraint requires reconsideration;
- retain all 0.8–0.18 package gates until equivalent 1.0 umbrella gates demonstrably subsume them;
- validate real `Icod.DCurses` throughout the release candidate;
- treat stale documentation as a release defect.

---

## 4. Tranches

### T190 — 1.0 contract and regret-audit freeze — complete

Green at workflow #911.

T190 classified the accumulated public surface using the A–E regret framework and found one justified D-class correction before 1.0: a live `TerminalSession` no longer publicly exposes its borrowed raw input transport. `ITerminalInput` remains public for custom transport injection, but session-owned input now has one authoritative public read path through `ReadEventAsync(...)` and typed query operations.

The correction preserved all historical package/downstream gates. The audit found no sufficient reason to redesign the control-provider/native-snapshot layer, typed query/result contracts, semantic protocol APIs, reversible ownership leases, modern keyboard model, or the advanced caller-owned raw output transport.

Comprehensive current enum numeric values and exact public-surface freezing are assigned to T194.

Records:

- `docs/T190-1.0-Contract-and-Regret-Audit-Freeze.md`;
- `docs/T190-Public-API-Regret-Audit.md`.

### T191 — permanent architecture and ownership documentation — complete

Green at exact head `c16949d94cb8471b9b776ec4c5dec4a715ea10de`, workflow #918.

Permanent authorities:

- `docs/Architecture.md`;
- `docs/Terminal-Session-and-Ownership.md`;
- `docs/Lifecycle-and-Restoration.md`.

T191 consolidates architecture boundaries, transport/state ownership, one-reader semantics, lifecycle composition, lock/order principles, exact restoration, participant ordering, invalidation, rollback uncertainty, and disposal authority.

Record:

- `docs/T191-Permanent-Architecture-and-Ownership-Documentation.md`.

### T192 — permanent input, query, and protocol semantics

Implementation complete; exact-head validation pending.

Permanent authorities:

- `docs/Input-and-Events.md`;
- `docs/Queries-and-Responses.md`;
- `docs/Modern-Keyboard-Security-and-Compatibility.md` refreshed as a 1.x authority.

T192 consolidates:

- text/key/mouse/focus/paste event semantics;
- one authoritative live-session input reader;
- traditional vs Kitty/xterm compatibility behavior;
- rich-input lease composition;
- bounded framing/buffering;
- query routing and response correlation;
- timeout/cancellation commit boundaries;
- bounded late-response ownership;
- suspend/resume generations;
- lifecycle observation through the same ambiguity domain;
- unsupported/unsafe activation behavior.

Record:

- `docs/T192-Permanent-Input-Query-and-Protocol-Semantics.md`.

### T193 — permanent output, presentation, and security semantics

Consolidate semantic-output and reversible presentation contracts:

- titles, location, hyperlinks, clipboard;
- cursor style, synchronized output, progress, pointer shape;
- semantic prompt/OSC 133;
- terminal colors and scoped ownership;
- safe OSC 9 subset;
- presentation leases and screen-local keyboard composition;
- privacy/security consequences of terminal metadata and desktop integration;
- explicit exclusions for hazardous or unverifiable vendor operations.

### T194 — public API/XML/sample regret closure

Reconcile source documentation and examples against the permanent contract.

Required work:

- audit every public/protected member's XML documentation for 1.0 semantics;
- correct stale or pre-1.0-only wording;
- audit exception/cancellation/lifecycle statements;
- freeze the exact current public surface and every public enum numeric value as the 1.x baseline;
- audit all samples for current best practice and ownership cleanup;
- add samples only where an important public workflow is not otherwise taught;
- remove or rewrite misleading examples;
- make `samples/README.md` a durable sample index rather than a release-history index where practical.

Any final public API correction discovered here must be small, justified, and covered by migration notes.

### T195 — compatibility policy, migration, and support contract

Create permanent consumer-facing compatibility guidance:

- source/binary compatibility expectations after 1.0;
- semantic-versioning policy;
- supported TFMs/platform policy;
- deprecation/obsoletion policy;
- terminal capability vs emulator-specific behavior policy;
- migration guidance from pre-1.0 package versions, including removal of public `TerminalSession.Input`;
- guidance for direct consumers vs `Icod.DCurses` consumers;
- security-reporting and compatibility caveats where repository conventions support them.

### T196 — package metadata and documentation artifact closure

Make package/repository metadata tell the same 1.0 story.

Required work:

- replace stale `PackageReleaseNotes` from older releases;
- audit package description/tags/README/license/icon/repository metadata;
- verify generated XML documentation on all TFMs;
- verify documentation links packaged in README remain valid;
- add a 1.0-rc1 package contract that validates the frozen public surface and permanent documentation markers from the freshly packed NuGet;
- retain historical package gates until final 1.0 closure.

### T197 — downstream RC acceptance and stable-candidate closure

Run the frozen contract through real downstream consumers and release infrastructure.

Required proof:

- Windows/Linux/macOS PR matrix green;
- real `Icod.DCurses` focused acceptances green;
- repeated DCurses hardening soak green;
- fresh package-only consumers green on net8/net9/net10;
- no accidental public API drift against the rc1 baseline;
- Release distribution validation green across configured x64/ARM64 runners after merge;
- rc1 documentation contains no stale "in progress" authorities.

Tag/publish only when explicitly authorized.

---

## 5. Permanent documentation target set

By rc1 closure the repository will have durable authorities equivalent to:

```text
docs/Architecture.md
docs/Terminal-Session-and-Ownership.md
docs/Lifecycle-and-Restoration.md
docs/Input-and-Events.md
docs/Queries-and-Responses.md
docs/Presentation-and-Reversible-State.md
docs/Semantic-Output-Protocols.md
docs/Security-and-Privacy.md
docs/Compatibility-and-Versioning.md
docs/Migration-to-1.0.md
docs/Public-API-Baseline-1.0-rc1.md
```

Historical `Txxx` and `Public-API-Baseline-0.x` documents remain design/release records, but they are no longer intended to be required reading for the supported 1.0 contract.

---

## 6. Frozen T190 decisions

The first 1.0 regret audit freezes these decisions:

- custom `ITerminalInput`/`ITerminalOutput` and `ITerminalControlProvider` injection remain public;
- a live `TerminalSession` does **not** return its raw input transport publicly;
- `TerminalSession.Output` remains an advanced caller-owned escape hatch outside session serialization and must be documented as such;
- `ReadEventAsync(...)` remains the canonical session-owned input path;
- typed query APIs remain the canonical response-correlation path;
- `WriteTerminalStringAsync(...)` remains the low-level already-resolved terminfo-string boundary, not a generic protocol recommendation;
- semantic protocol APIs remain bounded and typed rather than generic vendor dispatch;
- lifecycle, presentation, rich-input, and color ownership models remain supportable as 1.x contracts;
- current enum values, rather than superseded early-pre-1.0 layouts, will be frozen comprehensively in T194;
- no new protocol work enters rc1.

---

## 7. Current baseline

```text
Published predecessor: v0.18.0
Main baseline SHA:     c0f0ff482c8bb0d358c4fcb38e454d717b1b6f1c
VersionPrefix:         1.0.0
VersionSuffix:         rc1
Version:               1.0.0-rc1
PackageVersion:        1.0.0-rc1
AssemblyVersion:       1.0.0.0
TargetFrameworks:      net8.0;net9.0;net10.0
T190 validation:       workflow #911
T191 validation:       workflow #918
T192 validation:       pending exact current head
```

**Next:** validate the exact T192 documentation head. If green, close T192 and begin T193 permanent output, presentation, and security documentation.
