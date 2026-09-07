# T180 — Hardening Contract and Audit Freeze

**Release:** `Icod.Terminal 0.18.0`  
**Development version:** `0.18.0-alpha.1`  
**Base:** stable `0.17.0` merged to `main` at `f1123a139d71ef5e468d2de4bcff073f6d0efdd2`  
**Purpose:** freeze the hardening scope before production changes begin

## 1. Contract

0.18 is a hardening release. The default decision for any proposed change is therefore:

> strengthen an existing invariant, test, failure path, parser bound, lifecycle rule, concurrency guarantee, or compatibility contract rather than add a new feature.

No new terminal protocol is planned for this line.

## 2. Audit domains

The hardening audit is organized around the following risk domains.

| Domain | Primary risk | Required evidence before stable release |
| --- | --- | --- |
| Incremental input parsing | unbounded buffering, ambiguous recovery, malformed-sequence poisoning | bounded malformed/incomplete-input tests and deterministic recovery |
| Query routing | response theft, stale waiter state, interleaved ordinary input loss | adversarial interleaving/timeout/cancellation coverage |
| Output commitment | partial frames, incorrect cancellation semantics | explicit pre/post-commit tests for representative operations |
| Lease ownership | stranded terminal state, incorrect downgrade/restore | overlapping lease and failed-transition tests |
| Believed state | state advanced despite failed physical mutation | failure-injection assertions after every multi-step transition class |
| Composition/locking | deadlock or cross-manager interleaving | documented lock graph plus forced-contention tests |
| Lifecycle reentry | replay of stale state or incorrect observation ordering | suspend/resume/invalidation scenarios with active leases/queries |
| Disposal | duplicate output, stale lease fault, incomplete cleanup | idempotent disposal and owner-transfer cleanup tests |
| Transport failures | poisoned later operations, masked uncertainty | read/write failure injection and recovery-contract assertions |
| Platform modes | incomplete restoration or divergent semantics | Windows/POSIX exceptional-path coverage |
| Package/API compatibility | accidental public breakage | retained 0.8–0.17 package gates plus new 0.18 baseline |
| Downstream integration | local invariants fail under real full-screen composition | real `Icod.DCurses` mixed-operation and repeated-cycle acceptance |

## 3. Frozen invariants to preserve

The audit starts from these already established contracts:

- traditional keyboard input remains the compatibility floor;
- Kitty keyboard reporting is explicitly negotiated and lease-owned;
- xterm `modifyOtherKeys` remains decode-only;
- input/presentation mutation ordering uses the session composition domain;
- active terminal queries own correlated responses without stealing ordinary input;
- scoped terminal state is restored only when the library can prove ownership/restoration semantics;
- public query traffic remains suspended during managed lifecycle reentry except for sanctioned internal observation windows;
- disposal and invalidation do not invent terminal state;
- output operations serialize complete frames through the session output domain;
- net8/net9/net10 remain supported targets;
- real `Icod.DCurses` acceptance is part of the release contract.

## 4. T180 exit criteria

T180 is complete when:

1. the repository risk inventory is reviewed against current source/tests;
2. each material gap is assigned to T181–T185 or explicitly documented as an accepted limitation;
3. 0.18 non-goals are frozen so hardening does not drift into feature work;
4. the repository builds/tests green at `0.18.0-alpha.1` before behavioral hardening begins;
5. the roadmap and PR body identify the first executable hardening tranche.

## 5. Initial observations

The first static sweep found no `TODO`, `FIXME`, `HACK`, or `NotImplementedException` markers on `main`. This is useful but not dispositive: the dominant 0.18 risks are behavioral and temporal rather than visibly unfinished code.

The highest-value early hardening targets are therefore:

1. parser/query-router adversarial bounds and recovery;
2. cancellation/commit-point and failure-injection coverage;
3. lifecycle/composition forced-contention tests;
4. repeated ownership/disposal cycles through real downstream DCurses;
5. platform restoration under exceptional exits.

## 6. No-feature rule

During T180–T185, discovering an unsupported protocol or emulator behavior is not by itself a reason to implement it. New support should normally be deferred beyond 0.18 unless the omission exposes a correctness or safety defect in an already-public contract.
