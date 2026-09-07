# T181 — Parser and Query-Router Hardening

**Release:** `Icod.Terminal 0.18.0-alpha.2`  
**PR:** #31  
**Baseline:** `0.18.0-alpha.1`, workflow #859  
**Adversarial validation:** workflow #860

## Scope

T181 hardens the existing incremental input decoder and active-query transaction machinery without adding new public protocol surface.

The tranche focuses on:

- bounded incremental parser state;
- malformed and oversized response recovery;
- ambiguity-sensitive query serialization;
- cancellation/timeout after wire emission;
- late-response ownership;
- suspend/resume generation invalidation;
- internal post-resume lifecycle observation queries;
- coexistence with ordinary application input.

## Query ownership invariants

The existing query manager deliberately separates caller lifetime from wire ownership.

Before emission, cancellation or timeout prevents the request from being written. After emission, the caller may complete by cancellation, timeout, suspend interruption, or disposal while the transaction continues to own a possible late response for a bounded interval.

That late ownership is necessary to prevent a stale reply from satisfying a later query.

Suspend increments the transaction generation and interrupts callers. A queued transaction from an older generation is rejected before emission after the ambiguity gate becomes available.

Post-resume lifecycle observation does not bypass the ambiguity gate. The internal observation window temporarily resumes the same transaction manager while public queries remain rejected. This preserves one authoritative ordering domain for stale responses and new observation traffic.

## New adversarial coverage

`TerminalQueryLifecycleHardeningTests` adds two focused regressions.

### Lifecycle observation waits for stale ownership

An emitted query is suspended while still owning a possible late response. An internal lifecycle observation query is then started.

The test proves:

1. the stale caller is interrupted;
2. the observation request is not emitted while the old transaction owns the wire slot;
3. the stale response is consumed by the old transaction;
4. only then is the lifecycle observation request emitted;
5. the observation response is correlated to the observation transaction.

### Old-generation queued query never emits

A second public query is queued behind an active query before suspend. Suspend invalidates both callers. The lifecycle observation window is then opened.

The test proves:

1. the active old-generation query retains only its bounded late ownership;
2. the queued old-generation request never reaches the wire;
3. after the stale response releases the ambiguity gate, the lifecycle observation request is the next physical query;
4. no old-generation request can reappear after resume.

These tests freeze the generation/ambiguity contract that later lifecycle and concurrency work depends on.

## Parser and response-framing audit

The decoder already centralizes input buffering through `TerminalInputDecoderOptions.MaximumBufferedBytes`, bounded by `TerminalSession.MaximumBufferedInputBytes`.

Active response framing uses the smaller of:

- the protocol-specific framing ceiling; and
- the decoder instance's `MaximumBufferedBytes`.

For correlated oversized responses, the decoder:

- reports a deterministic `FormatException` to the active response expectation;
- clears the correlated buffered candidate;
- for oversized OSC traffic, drains toward a valid OSC terminator using a separately bounded discard interval;
- fails explicitly if resynchronization cannot be achieved within that bound.

The audit found no production-code defect requiring a T181 semantic change. Existing parser bounds and query-router behavior are retained and now have stronger regression coverage.

## Validation

Workflow #860 passed on Windows, Linux, and macOS. Linux also passed exact package validation and every retained 0.8–0.17 package contract.

## Closure

T181 is complete at `0.18.0-alpha.2`.

No public API or wire protocol was added or changed.

Next: T182 — lifecycle, composition, and concurrency hardening.
