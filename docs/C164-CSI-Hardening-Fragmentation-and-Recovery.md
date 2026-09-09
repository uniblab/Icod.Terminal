# C164 — CSI Hardening, Fragmentation, and Recovery

## Status

Complete.

C164 qualifies the `Icod.Terminal 1.6.0` CSI foundation against resource boundaries, hostile syntax, fragmented transport reads, malformed correlated responses, and oversized correlated-response recovery.

## Accepted checkpoint

```text
3f5e1eccf2f655f25faf665c25262ad7a31df999
```

Pull-request Staging workflow:

```text
34393525555
```

Workflow #1201 passed:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- Validated package artifact.

## Grammar boundary qualification

C164 adds deterministic coverage for the exact CSI syntax ceilings rather than testing only ordinary-sized frames:

- maximum raw parameter-byte length;
- maximum top-level parameter count;
- maximum subparameter count;
- maximum accepted numeric value and maximum-plus-one rejection;
- mixed private bytes, semicolon parameters, colon subparameters, and empty components;
- CAN (`0x18`) and SUB (`0x1A`) invalidation.

The tests retain the C160/C161 distinction between structural grammar and dialect semantics: grammar can preserve a byte sequence that a later typed protocol parser legitimately rejects.

## Fragmentation qualification

A real terminal-pixel response:

```text
CSI 4 ; 800 ; 1200 t
```

is delivered through `TerminalSession` at every possible two-chunk split point. Every split completes through the authoritative session query path and the transport observes at most one concurrent reader.

This proves that the geometry path does not depend on favorable read chunking and does not introduce a second terminal reader.

## Malformed correlated-response recovery

A selector-4 geometry response with invalid dimensions is deliberately correlated to the outstanding query and fails with `FormatException`. A subsequent selector-6 geometry query on the same session then succeeds.

Malformed correlated CSI therefore remains transaction-owned rather than poisoning later query state or leaking into ordinary application input.

## Oversized correlated CSI recovery

C164 extends the geometry response matcher with `ICorrelatedTerminalResponseMatcher` support for both seven-bit and eight-bit selector-specific prefixes.

That lets the existing bounded response router identify an oversized selector-4 geometry response as belonging to the active query before complete framing is possible. The response is rejected deterministically, drained through its CSI final byte, and the decoder resynchronizes before the next query.

The end-to-end regression fills the complete 4,096-byte CSI frame ceiling, supplies the terminating final byte after the ceiling, verifies `FormatException`, then successfully executes a selector-6 cell-pixel query on the same session while retaining one-reader ownership.

## Query-transaction deadline hardening

During C164 validation, macOS/net9 exposed a latent race in `LateResponseOwnershipExpiryReleasesWireSlot`.

The production transaction manager previously began timeout late-response ownership from the time the timeout continuation happened to execute. Under scheduler delay, that could extend the ambiguity slot beyond the logical caller deadline.

C164 corrects the production semantics instead of lengthening a timeout or adding scheduler-dependent test yields:

- the transaction records its monotonic timeout-start timestamp and timeout duration;
- timeout ownership is measured from the logical timeout deadline;
- provider-defined timestamp units remain respected by using `IMonotonicClock.GetElapsedTime(...)` rather than timestamp arithmetic;
- cancellation, suspension, disposal, and other explicit interruption paths continue to measure late ownership from the actual interruption timestamp.

The exact-head macOS Staging job, including net8.0, net9.0, and net10.0 tests, passes with the corrected semantics.

## Release conclusions

C164 adds no public API and changes no released CSI wire request. It hardens internal parsing/query ownership semantics needed by later DCS/Sixel and APC/Kitty Graphics work.

The next tranche is C165 release acceptance, package verification, documentation synchronization, and final exact-head Staging validation.