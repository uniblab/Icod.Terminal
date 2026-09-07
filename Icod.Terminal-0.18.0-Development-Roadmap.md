# Icod.Terminal 0.18.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.18.0`  
**Development version:** `0.18.0`  
**Predecessor:** `0.17.0` — Modern Keyboard Contracts and Protocols  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** hardening, invariants, failure semantics, parser bounds, lifecycle/concurrency correctness, and downstream/package closure  
**Status:** T180–T186 complete; exact stable PR-head validation pending

---

## 1. Release intent

`0.18.0` is a hardening release. It does not intentionally expand the public feature surface. The release strengthens accumulated 0.4–0.17 terminal-control, observation, lifecycle, input, presentation, query, color, semantic-output, and modern-keyboard contracts against hostile timing, malformed input, partial failures, cancellation, concurrency, lifecycle interruption, and platform variation.

Traditional behavior remains the compatibility floor. Existing public enum numeric values, package contracts, and previously frozen wire semantics remain intact.

---

## 2. Hardening principles

0.18 work follows these rules:

- prefer invariant strengthening over new surface area;
- preserve state truthfulness after every failed transition;
- make cancellation boundaries explicit and test pre-commit vs post-commit behavior;
- bound parser buffering and malformed-sequence recovery;
- prove lock ordering and deadlock resistance under adversarial concurrency;
- treat lifecycle suspend/resume/disposal as first-class state transitions;
- exercise failure injection at transport/output/query boundaries;
- preserve exact restoration where the library claims ownership;
- avoid blind activation of terminal state that cannot be observed/restored;
- retain package-only validation for every release contract from 0.8 onward;
- validate real `Icod.DCurses` integration throughout.

---

## 3. Hardening tranches

### T180 — hardening contract and audit freeze — `0.18.0-alpha.1`

Complete and green at workflow #859.

Record: `docs/T180-Hardening-Contract-and-Audit-Freeze.md`.

### T181 — parser and query-router hardening — `0.18.0-alpha.2`

Complete and green at workflow #860.

T181 froze bounded parser/query behavior, stale-response ownership, suspend/resume generation invalidation, and lifecycle observation ordering without changing public query semantics.

Record: `docs/T181-Parser-and-Query-Router-Hardening.md`.

### T182 — lifecycle, composition, and concurrency hardening — `0.18.0-alpha.3`

Complete and green at workflow #874.

T182 found and corrected a real public-acquisition race. New input-protocol or presentation ownership now re-checks lifecycle and teardown availability inside the shared state-composition gate before either manager can mutate state.

Authoritative order:

```text
state composition
    -> lifecycle/teardown availability
        -> manager gate
            -> control output
```

Record: `docs/T182-Lifecycle-Composition-and-Concurrency-Hardening.md`.

### T183 — failure injection, cancellation, and rollback hardening — `0.18.0-alpha.4`

Complete and green at workflow #879.

T183 proves severe transition+rollback and lifecycle-reentry+rollback double-failure paths preserve all errors, do not retain ghost ownership, and leave state invalid/unknown rather than falsely advanced.

Record: `docs/T183-Failure-Injection-Cancellation-and-Rollback-Hardening.md`.

### T184 — platform and terminal-mode hardening — `0.18.0-alpha.5`

Complete and green at workflow #882.

T184 freezes platform symmetry and exact restoration:

- POSIX apply/restore uses `AfterOutputDrained`;
- Windows console apply/restore uses `Immediately`;
- final cleanup restores the exact captured baseline;
- redirected/non-interactive endpoint policy remains truthful;
- initialization and restoration failures remain explicit.

Record: `docs/T184-Platform-and-Terminal-Mode-Hardening.md`.

### T185 — downstream soak and integration hardening — `0.18.0-alpha.6`

Complete and green at workflow #889.

A new real-`Icod.DCurses` soak runs eight complete ownership cycles per supported TFM and verifies rich-input acquisition, Kitty negotiation, full-screen handoff, refresh/input decoding, exact teardown, stale-lease safety, and one native apply/restore pair per cycle.

Record: `docs/T185-Downstream-Soak-and-Integration-Hardening.md`.

### T186 — compatibility, documentation, package, and stable closure — `0.18.0`

Implementation complete. The package-contract candidate is green at workflow #898; exact stable PR-head validation remains the final PR gate.

T186 freezes:

- no public API signature delta from 0.17;
- `docs/Public-API-Baseline-0.18.md` as the 0.18 API/behavior baseline;
- a fresh-package 0.18 hardening consumer on net8/net9/net10;
- the 0.18 package gate in both PR and Release distribution validation;
- all retained 0.8–0.17 package contracts;
- the real DCurses hardening soak in PR and Release validation.

Record: `docs/T186-Compatibility-Package-and-Stable-Closure.md`.

---

## 4. Stable release testing matrix

The stable candidate must prove:

- no regression in retained 0.8–0.17 package contracts;
- parser memory remains bounded under adversarial incomplete/malformed input;
- malformed escape/control traffic recovers deterministically;
- active queries do not consume unrelated input or each other's replies;
- timeout/cancellation leaves query routing usable for later operations;
- concurrent leases reconcile deterministically;
- session composition prevents cross-manager interleaving violations;
- lifecycle suspend/resume restores only state the library owns and can truthfully re-establish;
- invalidation never treats stale believed state as authoritative;
- multi-step failures surface rollback failures rather than mask uncertainty;
- stale lease disposal remains safe/idempotent after owner/session cleanup;
- Windows/Linux/macOS PR validation remains green;
- net8/net9/net10 fresh package-only consumers remain green;
- real `Icod.DCurses` acceptance and soak remain green;
- after merge, Release distribution validation is green on the six configured x64/ARM64 runners.

---

## 5. Explicit non-goals

0.18 does not add new terminal protocols, terminal-brand heuristics, raw escape-sequence APIs, keyboard remapping policy, global hotkeys, IME control, graphics/image protocols, arbitrary vendor OSC expansion, or broader PTY/process-management surface.

---

## 6. Stable candidate state

```text
VersionPrefix:    0.18.0
VersionSuffix:    <empty>
Version:          0.18.0
PackageVersion:   0.18.0
AssemblyVersion:  0.18.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next:** validate the exact stable PR head. If green, review PR #31 for merge readiness. After merge, validate the exact resulting `main` commit under Release. Tag `v0.18.0` only with explicit authorization because tagging triggers publication.
