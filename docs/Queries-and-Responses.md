# Queries and Responses

This document is the permanent 1.x contract for active terminal queries, response correlation, ambiguity ownership, cancellation, timeout, lifecycle interaction, and malformed-response handling.

The public API exposes typed semantic queries. The query transaction manager, raw response matchers, and wire framing machinery remain internal implementation details.

## 1. One terminal conversation

Terminal queries share the same physical input stream used for application input.

A query therefore is not merely "write bytes, then read bytes." `TerminalSession` must coordinate:

1. request output;
2. an expected response shape;
3. ordinary user input that may arrive before, during, or after the response;
4. other queries which could receive an indistinguishable response;
5. caller cancellation or timeout;
6. suspend/resume and session disposal.

All built-in queries use the session's authoritative input coordinator and response router. Callers must not create a competing raw reader over the same input transport.

## 2. Public query families

The 1.x query surface includes typed operations in these protocol families:

- CSI device attributes and status reports;
- cursor-position reporting;
- DECRQSS status-string queries;
- XTGETTCAP live capability observation;
- OSC 4 palette-color observation;
- dynamic-color observation for the supported OSC 10–14, 17, and 19 semantic colors;
- OSC 22 pointer-shape observation;
- OSC 52 clipboard/selection observation;
- internal lifecycle observation required to re-establish negotiated state after resume.

The presence of a protocol family does not create a generic public CSI/OSC/DCS query builder. Public operations remain typed and bounded.

## 3. Query availability

A public query is available only when the session can support a bidirectional live-terminal conversation for that operation.

Queries may be rejected before emission when, for example:

- the session is disposing or disposed;
- lifecycle state has been released for suspend;
- public queries are deliberately unavailable during a lifecycle re-entry phase;
- an endpoint configuration cannot support the required query conversation;
- the query queue/resource bounds are exhausted.

Rejection before emission does not send a partial or speculative request.

Successful request transmission proves only that the query bytes were emitted. The returned typed result proves that a correlated response was received and parsed according to that query's contract.

## 4. Ambiguity-sensitive serialization

Terminal response protocols commonly lack caller-generated transaction IDs. Two outstanding requests can therefore expect response frames that are indistinguishable on the wire.

`Icod.Terminal` serializes the physical ambiguity-sensitive query slot. A later query does not emit until the previous transaction has relinquished ownership of any response that could still belong to it.

This is stronger than serializing only the caller-visible tasks. The wire ownership may outlive the caller task after cancellation, timeout, suspend interruption, or another caller-visible completion condition.

The query queue is bounded. The current 1.x implementation accepts at most 32 pending transactions and rejects additional requests rather than allowing unbounded queue growth.

## 5. Request bounds

A terminal query request is bounded before output.

The internal request ceiling is 4096 bytes. Built-in semantic requests are substantially smaller; the ceiling exists as a defensive transaction invariant rather than an invitation to expose arbitrary user-built requests.

Public caller-visible query timeouts must be nonnegative and no greater than one minute.

Protocol-specific response framing can impose stricter limits than the session's global undecoded-input ceiling.

## 6. The emission commit boundary

Every active query has a meaningful commit point: the request has either not been emitted, or ownership of an emitted request has begun.

### Before commit

Before emission:

- caller cancellation prevents the request from being written;
- timeout before the request obtains the ambiguity/output slot prevents emission;
- suspend-generation invalidation prevents an old queued request from appearing after resume;
- disposal prevents new request transmission.

No compensating terminal traffic is necessary because no request was committed.

### After commit

After emission:

- the caller may time out or cancel;
- suspend may interrupt the caller;
- disposal may interrupt the caller;
- output failure may make the request state uncertain.

These conditions do **not** automatically make a possible response safe for the next query.

The transaction retains bounded late-response ownership so a stale reply cannot satisfy a later query.

## 7. Caller lifetime versus wire lifetime

The caller-visible task and the physical response-ownership lifetime are intentionally separate.

Consider:

```text
request A emitted
caller A times out
late response A arrives
request B wants the same response shape
```

If B were emitted immediately after caller A timed out, response A could be misidentified as response B.

Instead, A continues to own its possible response for a bounded interval. If the late response arrives, it is consumed by A's stale ownership and does not leak into ordinary input or a later transaction. If no response arrives within the bounded ownership period, the ambiguity slot is released.

This behavior is a core 1.x correctness guarantee.

## 8. Response routing precedes ordinary input decoding

When an active response expectation exists, the input coordinator checks whether buffered input belongs to the correlated terminal response before presenting the same bytes as ordinary application input.

Only a frame matching the active expectation is routed to that transaction. Unrelated text, keys, mouse/focus/paste input, and unrelated protocol-looking data continue through normal input decoding according to the parser contract.

The public API does not expose raw `TerminalResponseFrame` objects.

## 9. Malformed correlated responses

A response that is clearly correlated to the active query but malformed is not silently treated as ordinary application input.

The caller receives a deterministic parse failure, normally `FormatException` for the typed public query contracts.

Examples include:

- invalid parameter grammar;
- a syntactically correlated frame with invalid semantic values;
- an oversized correlated response;
- invalid encoding or payload structure for the specific protocol.

This prevents a malformed terminal response from being mistaken for unrelated user input while also preventing indefinite parser accumulation.

## 10. Oversized responses and resynchronization

Response framing is bounded by the smaller applicable resource limits, including the decoder instance's maximum buffered bytes and the protocol-specific response ceiling.

For a correlated response that exceeds its permitted bound, the active transaction fails deterministically.

Where a framed protocol such as OSC requires draining toward a terminator before the ordinary input stream can be trusted again, resynchronization itself is bounded. The library does not discard an unbounded stream while searching indefinitely for a terminator.

If bounded resynchronization cannot restore a trustworthy boundary, failure remains explicit rather than pretending the parser is synchronized.

## 11. Cancellation and timeout results

Typed query methods follow ordinary async exception semantics rather than returning `TerminalEventKind.Cancelled`/`Timeout`.

Public query callers should expect:

- `OperationCanceledException` when their cancellation token cancels the query;
- `TimeoutException` when the caller-visible response deadline expires;
- `FormatException` for a correlated malformed response where documented;
- `InvalidOperationException` when the live session state cannot permit the query;
- `ObjectDisposedException` when disposal invalidates an outstanding transaction.

The exact public method XML documentation remains authoritative for method-specific exceptions.

The important common rule is that caller completion after request emission does not erase bounded stale-response ownership.

## 12. Suspend/resume generations

Suspend/resume introduces a query generation boundary.

When suspend begins:

- public query issuance is suspended;
- the query generation advances;
- active/queued old-generation callers are interrupted;
- an already-emitted transaction may retain only its bounded late-response ownership;
- an old-generation request that was still queued must never emit after resume.

A transaction rechecks its generation after obtaining the ambiguity slot and before emission. This prevents a request queued before suspend from appearing physically during a later terminal generation.

## 13. Lifecycle observation uses the same ambiguity domain

Some session-owned state must be re-observed after resume, notably negotiated terminal capabilities whose truth cannot safely be assumed across suspension.

Internal lifecycle observation does not bypass or reset the public query router.

Instead:

1. the old generation is invalidated;
2. already-emitted stale ownership is honored;
3. a controlled internal observation window uses the same input coordinator and ambiguity gate;
4. public queries remain unavailable during the lifecycle-only observation phase;
5. normal public query availability returns only after lifecycle re-entry is complete.

Therefore a late pre-suspend response cannot be stolen by a post-resume capability probe.

## 14. Lifecycle participant restrictions

Public terminal queries are deliberately unavailable while ordinary `ITerminalSessionLifecycleParticipant.ResumeAfterTerminalSuspendAsync(...)` callbacks execute.

Participants must not call public query APIs from that callback.

Session-owned observation-dependent components use a separate internal lifecycle phase so they can refresh live observations without widening the public participant contract or allowing arbitrary re-entrant queries.

## 15. Query output ordering

Request emission participates in the session's control-output ordering domain.

A committed request and its required flush are serialized against other session-owned control traffic so another presentation/input/state mutation cannot splice bytes into the request's transmission boundary.

Direct caller writes through the advanced borrowed `TerminalSession.Output` property are outside session serialization and remain caller responsibility. Such writes must not be used to synthesize a second query path.

## 16. Output failure

If request emission fails after the transaction has committed, the caller receives the output failure.

The transaction does not assume that zero bytes reached the terminal merely because the output abstraction reported failure. It retains the response expectation for the bounded ownership period where necessary, because partial or uncertain transport failure can still produce a response.

This follows the general 1.x rule that uncertain physical state is surfaced conservatively rather than converted into known absence.

## 17. Clipboard and other sensitive queries

A terminal query can cause data to cross a trust boundary.

Clipboard queries in particular may request content from terminal/desktop selection state. Applications must treat returned clipboard text as external input and must consider whether querying it is appropriate for their security/privacy model.

Likewise, device attributes, colors, capabilities, and other observations can reveal terminal-environment details. `Icod.Terminal` returns requested observations; it does not automatically redact application-requested query results.

See `Security-and-Privacy.md` once T193 establishes the consolidated security authority.

## 18. Explicit non-contracts

The 1.x query system does not promise:

- arbitrary caller-defined raw terminal queries;
- multiple physically ambiguous responses outstanding in parallel;
- terminal-brand inference as proof of support;
- infinite late-response ownership;
- unbounded request, response, queue, or resynchronization buffers;
- reuse of an old-generation request after suspend/resume;
- delivery of correlated terminal responses as ordinary input events;
- a guarantee that every emulator implements every typed query.

Future query families must use the same bounded correlation, emission-commit, stale-response ownership, and lifecycle-generation model unless a new protocol provides a stronger transaction identity that can be integrated without weakening these guarantees.
