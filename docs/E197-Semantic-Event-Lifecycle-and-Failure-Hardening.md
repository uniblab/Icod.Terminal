# E197 — Semantic Event Lifecycle and Failure Hardening

**Release:** `1.9.0`  
**Tranche:** `E197`  
**Result:** accepted without production runtime changes  
**Behavioral checkpoint:** `0c5015f3e17c1c05b24283b4da9fa477caac27f6`  
**Validation:** pull-request workflow `#1431` / `34519873201`

## Purpose

E197 qualifies unsolicited semantic events against the existing `TerminalSession` lifecycle, cancellation, disposal, query late-response, and end-of-input contracts.

The important architectural question was whether 1.9 needed a second lifecycle mechanism for semantic events. The answer is no. E192–E194 widened the existing bounded application-event domain and preserved the existing authoritative reader, so the established session semantics already apply to semantic observations.

E197 therefore adds regression evidence rather than a second queue, reader, replay database, notification lifetime manager, or special semantic-event cancellation path.

## Qualified invariants

The E197 matrix proves the following contracts.

### Caller cancellation preserves fragmented semantic input

Cancellation of one `ReadEventAsync(...)` wait cancels that caller wait only. It does not cancel the decoder's session-owned read or abandon a partially accumulated semantic report.

The regression synchronizes after the first OSC 99 report fragment has been consumed and the decoder has issued the next underlying transport read. The caller is then canceled, receives `TerminalEventKind.Cancelled`, and a later fragment completes the same report for delivery on the next event read.

### Queued semantic events survive suspend/resume exactly once

An already-decoded semantic event remains an observed application event across a managed suspend/resume transition. Suspending may invalidate generation-scoped state/capability beliefs and interrupt active query callers, but it does not retroactively discard or replay a semantic observation already accepted from the byte stream.

### Notifications are not reversible session state

A notification sent with activation/button reporting and close reporting enabled is not replayed on resume and is not automatically closed on `TerminalSession.DisposeAsync()`.

The library does not retain hidden notification state merely because reporting was requested.

### Disposal unblocks pending event waits

Session disposal cancels session-lifetime input machinery. A pending `ReadEventAsync()` waiting for future application input therefore terminates through the existing shutdown path rather than hanging indefinitely.

This is distinct from caller cancellation, which returns a `Cancelled` event while leaving the underlying session-owned read intact.

### End-of-input remains ordinary input semantics

If a semantic report is decoded immediately before the transport reaches end-of-input, the semantic event is delivered first and the subsequent event is `TerminalInputEventKind.EndOfInput` inside `TerminalEventKind.Input`.

No lifecycle or semantic termination event is fabricated.

### Late correlated OSC 99 responses remain query-owned

A Kitty OSC 99 query response arriving after the caller-visible query timeout remains owned by the bounded late-response transaction. It is not reclassified as an unsolicited notification event merely because the original caller has already observed `TimeoutException`.

A following unrelated unsolicited OSC 99 report is still delivered normally through the semantic event lane.

## Test coverage

E197 adds `TerminalSemanticEventLifecycleHardeningTests` with six focused regressions:

```text
CallerCancellationPreservesFragmentedSemanticReport
QueuedSemanticEventSurvivesSuspendResumeWithoutReplay
SuspendResumeAndDisposalDoNotReplayOrAutoCloseNotification
DisposalUnblocksPendingSemanticEventWait
SemanticEventPrecedesEndOfInputWithoutFabricatingLifecycleEvent
LateTimedOutQueryResponseDoesNotLeakIntoSemanticLane
```

The test transport exposes deterministic read/write synchronization so cancellation and lifecycle races are qualified at known ownership points instead of relying on arbitrary sleeps.

## Verification evidence

On Linux, the exact E197 behavioral checkpoint built with:

```text
0 warnings
0 errors
```

and completed:

```text
net8.0   1,776 passed, 0 failed, 0 skipped
net9.0   1,776 passed, 0 failed, 0 skipped
net10.0  1,776 passed, 0 failed, 0 skipped
```

The complete pull-request workflow passed on Windows, Linux, and macOS. The package candidate, all four package contract shards, the validated package artifact, notification sample verification, and existing `Icod.DCurses` acceptance/soak witnesses also remained green.

## Production-code decision

No production runtime change is justified by E197.

All six new semantic-specific lifecycle regressions pass against the E196 runtime. Adding a special semantic lifecycle queue, replay manager, close-on-dispose behavior, or cancellation mechanism would duplicate existing architecture and weaken the one-reader/one-ordering-domain contract.

E197 therefore freezes the proven behavior in tests and permanent documentation only.

## Security consequence

Lifecycle survival does not make a semantic event more trustworthy. An already-decoded activation, button, close, or `untracked` report remains validated but unauthenticated terminal-controlled input before and after suspend/resume.

No lifecycle transition converts terminal event identifiers into authenticated host-notification identities.

## Next gate

E198 is the adversarial-hardening and downstream/package/sample acceptance tranche. It must stress split points, concatenation, boundary/overflow cases, malformed metadata/payloads, oversized drain/recovery, identifier collisions, repeated cancellation/timeouts, bounded queue pressure, interleaving, and repeated lifecycle cycles while retaining package-only and current `Icod.DCurses` witnesses.
