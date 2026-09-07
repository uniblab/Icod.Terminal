# Icod.Terminal 0.18.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.18.0`  
**Development version:** `0.18.0-alpha.1`  
**Predecessor:** `0.17.0` — Modern Keyboard Contracts and Protocols  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** hardening, invariants, failure semantics, parser bounds, lifecycle/concurrency correctness, and downstream/package closure  
**Status:** T180 audit/contract freeze in progress

---

## 1. Release intent

`0.18.0` is a hardening release. It SHALL NOT intentionally expand the public feature surface unless a narrowly scoped API correction is required to repair a proven correctness or safety defect.

The objective is to make the accumulated 0.4–0.17 terminal-control, observation, lifecycle, input, presentation, query, color, semantic-output, and modern-keyboard contracts withstand hostile timing, malformed input, partial failures, cancellation, concurrency, lifecycle interruption, and platform variation.

Traditional behavior remains the compatibility floor. Existing public enum numeric values, package contracts, and previously frozen wire semantics remain intact unless a defect is demonstrated and documented.

---

## 2. Hardening principles

0.18 work follows these rules:

- prefer invariant strengthening over new surface area;
- preserve state truthfulness after every failed transition;
- make cancellation boundaries explicit and test pre-commit vs post-commit behavior;
- bound parser buffering and malformed-sequence recovery;
- prove lock ordering and deadlock resistance under adversarial concurrency;
- treat lifecycle suspend/resume/disposal as first-class state transitions;
- exercise failure injection at every transport/output/query boundary where practical;
- preserve exact restoration where the library claims ownership;
- avoid blind activation of terminal state that cannot be observed/restored;
- retain package-only validation for every release contract from 0.8 onward;
- validate real `Icod.DCurses` integration throughout, not only at release closure.

---

## 3. Hardening tranches

### T180 — hardening contract and audit freeze — `0.18.0-alpha.1`

Inventory the existing architecture and freeze the 0.18 hardening scope. Produce an explicit defect/risk matrix covering:

- incremental input parser bounds and malformed recovery;
- active query routing and response correlation;
- cancellation and output commitment boundaries;
- lease reconciliation and restoration semantics;
- state invalidation and believed-state truthfulness;
- session composition and manager lock ordering;
- lifecycle suspend/resume/reentry ordering;
- disposal idempotence and stale-lease behavior;
- transport read/write failure propagation;
- Windows/POSIX mode restoration and platform parity;
- public API/package compatibility;
- real downstream `Icod.DCurses` integration.

No production behavior change is required for T180 unless the audit uncovers a release-blocking defect that cannot safely wait for the next tranche.

### T181 — parser and query-router hardening — `0.18.0-alpha.2`

Strengthen incremental decoding and active-query routing under fragmentation, coalescing, malformed prefixes, oversized payloads, ambiguous CSI/OSC/DCS traffic, interleaved ordinary input, cancellation, and response timeouts.

Required outcomes include bounded buffering, deterministic recovery, no accidental query-response theft, and tests that exercise repeated malformed input without unbounded retained state.

### T182 — lifecycle, composition, and concurrency hardening — `0.18.0-alpha.3`

Stress the session-wide composition domain and manager interactions under concurrent acquisition/release, presentation transitions, query activity, lifecycle suspend/resume, invalidation, and disposal.

Required outcomes include a documented lock graph, adversarial tests for deadlock resistance, and proof that no manager can advance believed state past an uncommitted or failed physical transition.

### T183 — failure injection, cancellation, and rollback hardening — `0.18.0-alpha.4`

Add systematic injected failures around transport writes/reads, query delimiters, multi-step restoration, screen/keyboard choreography, colors, rich-input leases, and semantic output.

Every multi-step transition must either restore the prior truthful state or surface a failure that makes uncertainty explicit. Pre-commit cancellation must emit nothing; post-commit cancellation must not synthesize partial compensating traffic unless the owning contract explicitly requires rollback.

### T184 — platform and terminal-mode hardening — `0.18.0-alpha.5`

Exercise Windows console/terminal and POSIX termios paths for open/close, CBreak/raw-like modes, echo, resize/lifecycle behavior, redirected/non-interactive endpoints, unsupported capabilities, and restoration after exceptional exit paths.

The goal is behavioral parity where contracts are portable and explicit platform-specific behavior where they are not.

### T185 — downstream soak and integration hardening — `0.18.0-alpha.6`

Expand real `Icod.DCurses` acceptance into longer mixed-operation scenarios combining full-screen refresh, rich input, queries, synchronized output, pointer shape, colors, semantic prompt markers, progress, notifications/current-location output, modern keyboard reporting, lifecycle reentry, and deterministic disposal.

Add repeated-cycle/soak-style tests where practical to expose leaked ownership, stranded terminal state, parser accumulation, or stale session state.

### T186 — compatibility, documentation, and release closure — `0.18.0`

Freeze the 0.18 public API/package baseline, document all hardening guarantees and any intentionally unresolved limitations, retain every historical package gate, add a 0.18 hardening package contract, and require exact stable PR-head plus exact-main Release validation before tag.

---

## 4. Required testing matrix

By stable closure, 0.18 SHALL prove at minimum:

- no regression in all retained 0.8–0.17 package contracts;
- parser memory remains bounded under adversarial incomplete/malformed input;
- malformed escape/control traffic recovers deterministically;
- active queries do not consume unrelated input or each other's replies;
- timeout/cancellation leaves query routing usable for later operations;
- concurrent leases reconcile deterministically;
- session composition prevents cross-manager interleaving violations;
- lock-order tests do not deadlock under forced contention;
- lifecycle suspend/resume restores only state the library owns and can truthfully re-establish;
- invalidation never causes stale believed state to be treated as authoritative;
- multi-step failures surface rollback failure rather than silently masking uncertainty;
- stale lease disposal remains safe and idempotent after owner/session cleanup;
- transport exceptions propagate without corrupting unrelated later state where recovery is contractually allowed;
- Windows/Linux/macOS remain green;
- x64 and ARM64 Release distribution validation remain green where hosted runners are available;
- net8/net9/net10 fresh package-only consumers remain green;
- real `Icod.DCurses` acceptance remains green throughout the hardening line.

---

## 5. Explicit non-goals

0.18 does not plan new terminal protocols, terminal-brand heuristics, raw escape-sequence APIs, keyboard remapping policy, global hotkeys, IME control, new graphics/image protocols, arbitrary vendor OSC expansion, or broader PTY/process-management surface.

A discovered correctness defect may justify a narrowly scoped compatibility-preserving API adjustment, but such a change must be documented as a hardening correction rather than feature expansion.

---

## 6. Current development state

```text
VersionPrefix:    0.18.0
VersionSuffix:    alpha.1
Version:          0.18.0-alpha.1
PackageVersion:   0.18.0-alpha.1
AssemblyVersion:  0.18.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next:** complete T180 audit/contract freeze and convert the risk inventory into executable hardening gates.
