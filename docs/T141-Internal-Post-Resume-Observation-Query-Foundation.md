# T141 — Internal Post-Resume Observation Query Foundation

**Release:** `0.14.0`  
**Tranche:** `T141`  
**Development version:** `0.14.0-alpha.2`  
**Predecessor:** T140 lifecycle-safe color ownership and resume-observation contract  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T141 implements the lifecycle/query seam frozen by T140 without exposing a new public query phase.

The problem is specific but architectural: observation-dependent session-owned state must be able to re-observe the live terminal after resume before it reapplies retained logical ownership, while ordinary public callers and higher-layer lifecycle participants must continue to see terminal queries as unavailable until lifecycle re-entry is complete.

---

## 2. Internal query window

`TerminalSession` now tracks a distinct internal lifecycle observation query window.

The normal public query gate remains suspended throughout resume. During the internal observation window:

- `TerminalQueryTransactionManager` is temporarily resumed;
- public `ExecuteQueryAsync(...)` entry points still reject because `queryTransactionsSuspended` remains true;
- only `ExecuteLifecycleObservationQueryAsync(...)` may enter the live transaction manager;
- the same one-reader input coordinator, response expectations, ambiguity gate, timeout behavior, late-response ownership, and output serialization remain authoritative;
- the window is always closed in `finally` before ordinary participant resume begins.

No second reader, private color input loop, or alternate response router is introduced.

---

## 3. Observation-dependent core participants

T141 adds the internal `ITerminalObservedLifecycleParticipant` marker/contract.

It extends the existing lifecycle participant contract with:

```csharp
ValueTask RefreshAfterTerminalResumeAsync(
	CancellationToken cancellationToken = default
);
```

This interface is internal. It is intended only for session-owned managers whose restoration contract requires live re-observation before ordinary resume.

Existing public `ITerminalSessionLifecycleParticipant` behavior does not change.

---

## 4. Resume ordering

The effective resume sequence is now:

```text
reacquire output/native host state
reapply requested input mode
resume presentation state
resume input-protocol state
open internal lifecycle observation query window
refresh all observation-dependent session-owned participants
close internal observation query window
resume ordinary lifecycle participants in registration order
resume public terminal queries
mark session state valid
publish Resumed
```

If no observation-dependent participant exists, no query window is opened and the prior resume path remains effectively unchanged.

---

## 5. Failure semantics

Observation refresh failures are collected across all observation-dependent participants and surfaced through the existing lifecycle restoration exception path.

The query window closes even when refresh fails.

Ordinary lifecycle participant resume does not run after a failed observation phase. Control returns to the existing lifecycle re-entry rollback path, which suspends Terminal-owned presentation/input-protocol state and restores the host baseline as far as possible.

The internal query manager retains its existing bounded late-response ownership. If an internal observation times out, an ambiguity-sensitive later public query cannot overtake the still-owned late-response drain because it uses the same transaction manager and ambiguity gate.

---

## 6. Public contract preservation

T141 intentionally adds no public type or method.

Public queries remain unavailable:

- from the moment suspend begins;
- throughout host/presentation/input-protocol re-entry;
- throughout the internal observation phase;
- throughout ordinary `ITerminalSessionLifecycleParticipant.ResumeAfterTerminalSuspendAsync(...)` callbacks.

Only after those phases complete does `ResumeQueryTransactions()` reopen the normal public query path.

---

## 7. Validation

The lifecycle regression suite now proves:

- ordinary participants still prepare in reverse registration order and resume in registration order;
- public queries remain rejected during ordinary participant resume;
- an internal observation-dependent participant runs its refresh before ordinary resume;
- the same refresh callback cannot use the public color-query API;
- the internal lifecycle observation query entry point is admitted during the observation window;
- observation-dependent core preparation still participates in the existing reverse-order suspend discipline;
- existing lifecycle behavior remains unchanged when no observation-dependent participant is registered.

---

## 8. T142 handoff

T141 deliberately implements no color lease.

T142 may now build indexed-palette ownership on a tested lifecycle primitive:

1. first owner observes the current OSC 4 value before mutation;
2. managed suspend explicitly restores that observed external baseline;
3. after resume the palette manager receives the internal observation phase;
4. it obtains a fresh OSC 4 baseline for the new lifecycle epoch;
5. it reapplies the effective retained owner;
6. ordinary lifecycle participants and public queries resume only afterward.

This foundation remains internal unless later implementation evidence demonstrates a broader public need.
