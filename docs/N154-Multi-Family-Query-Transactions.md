# N154 — Multi-Family Query Transactions

**Release:** `Icod.Terminal 1.5.0`  
**Tranche:** N154  
**Status:** implementation complete; exact-head validation pending

## Purpose

N154 generalizes one logical active terminal query so it can accept a bounded set of response control families without adding a second transport reader or embedding a future vendor protocol directly in the input decoder.

The motivating acceptance case is the future Kitty Graphics support probe:

```text
APC graphics query response     -> positive completion
CSI Primary DA response         -> synchronization / unsupported barrier
```

N154 supplies the transaction primitive only. It does not implement Kitty Graphics, APC graphics parsing, or a public graphics capability API.

## Preserved architecture

The authoritative path remains:

```text
TerminalSession
    -> TerminalQueryTransactionManager
        -> TerminalInputCoordinator
            -> TerminalInputDecoder
                -> one borrowed ITerminalInput
```

No response family obtains a separate `ReadAsync()` path.

Existing single-family query APIs remain adapters over the same transaction manager. Their public behavior and return types are unchanged.

## Response plan

One internal `TerminalQueryResponsePlan` contains a bounded set of `TerminalQueryResponseRule` instances.

Each rule identifies:

- one `ITerminalResponseMatcher`;
- therefore one normalized control family;
- one frame-size bound;
- one disposition:
  - `Completion`; or
  - `Barrier`.

The 1.5 contract allows at most six rules and at most one rule per control family. A plan must contain at least one `Completion` rule.

The one-rule-per-family constraint is intentional for this release. It keeps framing selection deterministic while proving compound-family ownership. A future release may review richer same-family predicate sets if a real protocol requires them.

## Completion and barrier semantics

A `Completion` frame means the requested protocol supplied the logical query result.

A `Barrier` frame means a reviewed synchronization response completed the logical transaction without producing the primary protocol result. The transaction returns both the exact frame and its disposition to the internal caller.

N154 does not assign universal meaning to a barrier. The future protocol dialect decides what the barrier means. For the planned Kitty Graphics probe, a correlated CSI Primary DA barrier can establish that the terminal processed input beyond the graphics query without returning the graphics response.

## Family framing

`TerminalResponseFrameKind` now covers the normalized six-family set:

```text
CSI
DCS
OSC
APC
PM
SOS
```

It remains an internal compatibility representation. `TerminalControlFamily` is the normalized family vocabulary.

`TerminalResponseFrameKinds` provides the explicit mapping between them.

The decoder does not guess a dialect from the family. It asks the active response plan which families are accepted, frames the candidate using `TerminalResponseFramer`, then invokes that family's matcher.

## Resource bounds

Every response rule has a protocol frame bound, but the effective framing bound is always:

```text
min(rule frame limit, decoder maximum buffered bytes)
```

This preserves the pre-N154 decoder safety invariant. A response plan cannot cause a small bounded decoder to read beyond its configured capacity merely because a protocol normally permits larger frames.

The response-plan rule count is bounded at six.

Existing OSC 52 queries retain their larger reviewed OSC 52 frame allowance through the single-family compatibility adapter. Other legacy matchers retain the established default response-frame bound.

## Oversized correlated responses

Oversized-response ownership remains matcher-driven.

A rule participates in oversized correlated handling only when its matcher implements `ICorrelatedTerminalResponseMatcher` and recognizes the buffered prefix. N154 generalizes bounded discard/resynchronization across normalized families while retaining OSC BEL compatibility only for OSC.

Uncorrelated oversized candidates fall back to ordinary input decoding instead of being claimed merely because they resemble the active family.

## Cancellation, timeout, and late response ownership

N154 does not create a second transaction lifetime implementation.

The existing `TerminalQueryTransaction` semantics remain authoritative:

- caller cancellation before emission prevents the write;
- caller timeout begins at queue entry;
- cancellation or timeout after emission does not immediately abandon wire ownership;
- bounded late-response ownership is retained;
- suspension interrupts the caller while preserving bounded emitted-wire ownership;
- disposal terminates outstanding ownership;
- query request emission continues to share the session control-output gate.

Both single-family and multi-family queries use the same transaction class and ambiguity gate.

## Application input preservation

A frame is removed from the input buffer only after:

1. its outer family is one accepted by the response plan;
2. complete framing succeeds within the effective bound; and
3. that family's matcher accepts the exact frame.

Otherwise ordinary input decoding remains authoritative.

The N154 synthetic acceptance test explicitly proves ordinary application text remains readable while an APC/CSI compound query is pending.

## Synthetic acceptance probe

N154 tests a protocol-neutral stand-in for the later graphics probe:

```text
request:
    APC-like query bytes + CSI DA request bytes

accepted outcomes:
    APC frame -> Completion
    CSI frame -> Barrier
```

The test proves:

- either family can complete the one logical transaction;
- the returned frame retains its exact family and bytes;
- the disposition is preserved;
- only one input reader is active;
- unrelated application input is not stolen.

This is intentionally synthetic. No Kitty-specific branch exists in `TerminalInputDecoder`.

## Compatibility

N154 is internal infrastructure.

It does not add public API and does not intentionally alter any released 1.0–1.4 explicit wire operation. Existing matcher-based `ExecuteQueryAsync(...)` calls are wrapped as one-rule `Completion` plans.

The public API fingerprint therefore remains expected to match the frozen 1.4 baseline unless a later, separately reviewed 1.5 tranche intentionally adds public semantic routing APIs.

## Exit gate

N154 is complete when:

1. the historical single-family query suite remains green;
2. small-decoder bounded fallback behavior remains green;
3. the synthetic APC-completion/CSI-barrier transaction passes on all target frameworks;
4. application input remains live during the compound query;
5. APC/PM/SOS routed frames are accepted by the N153 structural model;
6. package/public API validation shows no unintended public change; and
7. Windows, Linux, and macOS Staging validation is green on one exact head.
