# T144 — Lifecycle Failure and Color-Ownership Concurrency Hardening

**Release line:** `Icod.Terminal 0.14.0`  
**Development version:** `0.14.0-alpha.5`  
**Status:** Implemented; exact-head validation pending  
**Scope:** failure injection and concurrency hardening only; no new public API or protocol family

## Purpose

T140–T143 established the contract and implementation for lifecycle-safe indexed-palette and dynamic-color ownership. T144 hardens those paths against cancellation, lifecycle transitions, output failure, late query responses, cross-manager concurrency, and session shutdown.

The tranche does not broaden the 0.14 feature surface. Its purpose is to prove that the ownership model remains truthful when operations do not complete along the happy path.

## Cancellation and query ownership

T144 verifies:

- pre-cancelled scoped palette acquisition emits no query or mutation;
- caller cancellation after a baseline query has been emitted propagates as cancellation rather than timeout or unsupported state;
- the query transaction retains bounded late-response ownership after caller cancellation;
- a late correlated reply from the cancelled transaction does not poison a later color acquisition;
- later color queries still serialize through the existing ambiguity-sensitive transaction gate.

No color manager introduces a second input reader or bypasses the active-query router.

## Suspend during acquisition

The critical lock-order case is now covered explicitly.

When managed suspend begins while the first-owner baseline query is outstanding:

1. `TerminalSession` suspends the query transaction manager;
2. the in-flight caller is interrupted with `InvalidOperationException`;
3. the acquisition unwinds the color-manager gate without retaining an owner or mutation;
4. lifecycle preparation can subsequently acquire that manager gate;
5. suspend completes without deadlock;
6. resume can complete with no phantom color owner.

This confirms that lifecycle does not wait on a color-manager gate while leaving the query which owns that gate indefinitely live.

## Restoration failure and retry ownership

T144 injects output failure during final palette and dynamic-color release.

The required behavior is:

- the failed restoration is surfaced to the lease disposer;
- the logical owner remains retained;
- physical state is marked uncertain;
- a later disposal attempt first re-establishes the retained owned color when necessary;
- it then retries exact external-baseline restoration;
- ownership is released only after restoration succeeds.

Reset controls are not used as fallback cleanup.

## Cross-manager concurrency

Concurrent palette and dynamic-color acquisition is exercised against one shared terminal transport.

The test proves:

- each manager may hold its own bookkeeping gate;
- both still share the single active-query ambiguity gate;
- query emission remains serialized;
- response correlation remains identity-specific;
- output mutation remains serialized through the session output domain;
- both acquisitions and releases complete without deadlock.

The intended lock relationship remains:

```text
color-manager gate
    -> active-query transaction (when observation is required)
        -> query/output serialization

color-manager gate
    -> control/session output serialization (for mutation/restoration)
```

Lifecycle first suspends the query router before waiting for manager preparation, which breaks the important suspend-vs-query wait cycle.

## Session disposal

T144 verifies disposal with both palette and dynamic-color owners still outstanding.

Session cleanup:

- stops accepting new ordinary session output;
- restores palette and dynamic external baselines through the cleanup output path;
- invalidates the corresponding lease owner references;
- allows subsequent lease disposal to complete as a no-op;
- leaves no scoped ownership active after successful session cleanup.

## Coverage added

`tests/Icod.Terminal.Tests/src/Lifecycle/TerminalColorOwnershipHardeningTests.cs` covers:

- pre-cancelled palette acquisition with zero output;
- suspend interruption of an outstanding first-owner palette query;
- cancellation after query emission plus safe later acquisition;
- failed palette restoration followed by successful disposal retry;
- failed dynamic-color restoration followed by successful disposal retry;
- concurrent palette/dynamic acquisition and release;
- session disposal with both ownership families active.

Earlier T141–T143 tests continue to cover malformed observations, lifecycle re-observation, partial multi-identity/index re-entry rollback, nested/out-of-order ownership, exact 16-bit replay, and ordinary/public query exclusion during lifecycle re-entry.

## Result

No new architectural seam was required by T144. The T140/T141 lifecycle-query ordering and the separate palette/dynamic managers remain suitable for 0.14.

The next tranche, T145, should therefore be an evidence-based protocol-closure audit. It should add protocol work only if T140–T144 exposed a concrete missing protocol form needed to strengthen an existing ownership or lifecycle guarantee. Otherwise T145 should explicitly record that no additional protocol is justified.
