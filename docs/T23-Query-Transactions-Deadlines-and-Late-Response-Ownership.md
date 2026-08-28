# T23 — Query Transactions, Deadlines, and Late-Response Ownership

**Project:** `Icod.Terminal`
**Release line:** `0.3.0`
**Development version:** `0.3.0-alpha.3`
**Tranche:** T23 — query transactions, deadlines, and late-response ownership
**Predecessor:** T22 — bounded response framing and single-reader demultiplexing
**Status:** Complete

---

## 1. Purpose

T23 turns the T22 response-framing seam into a session-owned transaction
substrate.

The tranche adds no public DA, DSR, CPR, DECRQSS, or XTGETTCAP operation. It
instead establishes the internal machinery those protocol APIs will use:

- bounded asynchronous query queueing;
- one ambiguity-sensitive transaction on the wire at a time;
- monotonic caller deadlines;
- normal caller cancellation;
- bounded post-cancellation/post-timeout response ownership;
- autonomous response progress without a second raw terminal reader;
- lifecycle interruption and re-entry behavior;
- coordinated session-generated control output.

T24 can therefore add typed CSI query APIs without reopening the transaction
lifetime contract.

---

## 2. Single-Reader Input Coordination

`TerminalInputDecoder` remains the only object which calls
`ITerminalInput.ReadAsync`.

T23 adds `TerminalInputCoordinator` above the decoder. The coordinator combines
two demands against that one decoder:

- application demand from the existing `TerminalSession.ReadEventAsync` path;
- internal response demand while an armed query expectation exists.

The coordinator does not create a second byte reader.

When no query is active, application demand remains one-event-at-a-time. The
coordinator satisfies that demand when it publishes one application event, so a
fast producer cannot pre-drain a second transport read merely because the caller
has not yet resumed from the first event. A caller timeout or cancellation can
still leave the same single pending terminal read used by the 0.2 contract.

When a query is active, the coordinator may continue decoding past unrelated
application events so the expected response can be reached even when the
application is not simultaneously calling `ReadEventAsync`.

Unrelated application events are retained in order for the established session
event path.

---

## 3. Deferred Application-Event Bound

The internal deferred-event queue has a T23 implementation capacity of 256
decoded events.

The value is internal policy, not public API.

When that queue is full and no application read is waiting, the single input
pump applies backpressure and stops requesting additional terminal bytes until
an application event is consumed. T23 does not silently drop application events
and does not grow an unbounded queue.

A query whose response is physically behind more retained application input than
the bounded queue can accept may therefore reach its caller-visible timeout.
That is preferable to unbounded memory growth or loss of ordered application
input.

---

## 4. Query Queue and Request Bounds

The internal query manager accepts at most 32 outstanding transactions total,
including the transaction which currently owns the wire slot.

A request payload is limited to 4096 bytes.

Both limits are implementation policy for the 0.3 prerelease line and remain
internal.

Multiple callers may enqueue work concurrently, but the worker emits
ambiguity-sensitive terminal transactions strictly one at a time.

No later transaction is emitted until the current wire transaction has:

- completed successfully;
- been safely drained after caller abandonment;
- expired under bounded late-response ownership;
- or been terminated with the session.

---

## 5. Conversational Endpoint Availability

Active query transactions require interactive input and output observations from
the same terminal platform.

When both POSIX observations provide terminal pathnames, T23 requires those
pathnames to identify the same device. When either pathname is unavailable, the
session does not invent an identity test it cannot support.

Windows console observations are directional: the normal input side reports
`CONIN$` while the normal output side reports `CONOUT$`. Those aliases are not
compared as if they were competing device pathnames; matching
`WindowsConsole` observations are treated as the conversational input/output
pair of the attached process console.

This follows the T21 rule that a query is unavailable when the session **knows**
the endpoints are different without falsely rejecting a valid directional pair.

---

## 6. Caller Lifetime Versus Wire Lifetime

T23 implements the contract frozen in T21.

Before request emission:

- caller cancellation completes the caller task as cancelled;
- caller timeout completes the caller task with `TimeoutException`;
- no request bytes are emitted.

After request emission is committed:

- caller cancellation still completes the caller task immediately as cancelled;
- caller timeout still completes the caller task immediately with
  `TimeoutException`;
- neither outcome revokes bytes which may already have reached the terminal;
- the transaction manager retains the matching response expectation internally.

The query worker therefore owns the wire transaction independently from the
caller task.

This is the mechanism which allows normal async/await semantics without
misattributing a response from an abandoned transaction to a later ambiguous
query while the bounded ownership period remains active.

---

## 7. Caller Timeout

T23 uses the session's `Icod.Timing.IMonotonicClock` for caller-visible query
timeouts.

The internal T23 transaction seam accepts a nonnegative finite timeout no
greater than one minute. A zero timeout therefore expires before transmission
unless the transaction has already completed synchronously, and it does not
create a special unbounded wait.

The one-minute ceiling is internal policy rather than a public compatibility
promise. Concrete T24+ APIs may expose narrower defaults while remaining within
the same bounded transaction substrate.

A timeout begins when the transaction is queued, not when it finally reaches the
wire. Queueing time and time waiting for session-generated control output both
count against the caller's requested operation deadline.

If the timeout expires before emission is committed, the queued transaction is
skipped and emits no bytes.

---

## 8. Late-Response Ownership

The default internal late-response ownership interval is one second.

The hard T23 ceiling is ten seconds. The interval must be positive; tests may use
a shorter positive interval through an internal overload.

When an issued transaction is abandoned because of caller cancellation,
caller-visible timeout, lifecycle interruption, or an output failure after
emission may have begun, its late-response ownership interval begins
immediately.

The matching expectation remains active until one of the following occurs:

1. the matching response arrives and is consumed;
2. the late-response ownership interval expires;
3. the session is disposed.

The next ambiguity-sensitive request is not emitted while that expectation still
owns the wire slot.

When the bounded interval expires, the expectation is removed and the slot is
released. A response arriving after that point is no longer owned by the old
transaction and is subject to the normal expectation-driven input rules. T23
therefore bounds contamination protection rather than pretending a terminal
protocol without transaction identifiers can provide unlimited certainty.

---

## 9. Registration, Arming, and Pre-Existing Input

T23 distinguishes registration of a response expectation from arming it for
consumption.

The query worker registers the expectation before taking the control-output gate
so the input coordinator knows which transaction will own the next compatible
response, but the expectation remains unarmed while the request is still queued
for output.

The transaction commits to emission only after it has acquired the shared
control-output gate and rechecked pre-emission cancellation/timeout state.
Immediately after that commit, the worker arms the expectation and invokes the
request write while still holding the control-output gate. Cancellation or
timeout observed before the commit prevents transmission; once committed, the
transaction follows the post-emission ownership rules.

Arming snapshots the number of bytes already retained in the decoder buffer.
Those pre-existing bytes are temporarily protected from the newly armed
expectation. As ordinary decoding consumes them, the protected count falls to
zero; only bytes which follow that protected prefix can satisfy the expectation.

Response-shaped bytes already buffered before arming therefore remain ordinary
application input even when their syntax matches the newly emitted query. This
narrows the unavoidable ambiguity window to bytes observed at or after the
request-emission boundary rather than allowing stale buffered input to satisfy a
future query.

---

## 10. Coordinator-Facing Decode Results

T22's normal `TerminalInputDecoder.ReadAsync` method continues to return an
application event and therefore may keep reading after it consumes an expected
response.

T23 adds an internal coordinator-facing decode result which can report either:

- one decoded application input event; or
- that an expected response was routed.

The input coordinator can therefore stop query-driven reading immediately after
the response is consumed instead of starting another raw read merely to obtain a
subsequent application event.

The existing direct decoder behavior and public session event contract remain
unchanged.

---

## 11. Session-Generated Control Output

T23 adds one internal session control-output gate.

The following session-generated control traffic participates in that gate:

- query request emission and flush;
- presentation-state transitions and rollback;
- rich-input protocol transitions and rollback;
- best-effort baseline restoration for those state managers.

The gate is held across each multi-write transition and its flush, so a query
request cannot be inserted into the middle of a presentation or input-protocol
state transition.

Ordinary caller application output is not automatically serialized behind this
gate. T23 preserves the T21 distinction between session-generated control
traffic and application output.

---

## 12. Suspend and Resume

Before terminal state is released for managed suspension, the query manager is
placed in suspended state and advances its lifecycle generation.

Suspension:

- rejects newly requested queries while suspended;
- invalidates transactions queued under the previous generation before they can
  be emitted;
- interrupts the caller task for an already issued transaction;
- retains that issued transaction's response expectation for its remaining
  bounded late-response ownership period;
- prevents a pre-suspend response from later becoming a successful current query
  result.

The late-response interval remains monotonic and bounded across suspension. It is
not extended indefinitely merely because the process was suspended.

After successful session-state re-entry, new transactions may be accepted under
the new lifecycle generation. An earlier ambiguity-sensitive wire slot must
still resolve or expire before the single worker can emit a later request.

---

## 13. Disposal

Session disposal closes the query manager before the lifecycle input token is
cancelled.

Queued and active caller tasks are completed with `ObjectDisposedException`.

An active response expectation is removed, the query worker is stopped, and no
transaction survives session teardown.

Disposal does not wait for the ordinary late-response ownership interval because
the terminal session itself is ending.

---

## 14. Compatibility

T23 adds no public query API member.

The public `TerminalSession.ReadEventAsync` surface remains unchanged.

The session input implementation now routes through the demand-driven
`TerminalInputCoordinator`, but the 0.2 caller contract remains:

- one underlying terminal read path;
- caller timeout does not cancel an already pending terminal read;
- caller cancellation remains a `TerminalEventKind.Cancelled` event for
  `ReadEventAsync`;
- text, keyboard, mouse, focus, paste, and lifecycle events remain ordered.

Query cancellation is a separate future public API contract and continues to use
normal `OperationCanceledException` semantics as frozen by T21.

---

## 15. Validation

T23 tests cover:

- query completion without an application `ReadEventAsync` loop;
- one physical input read at a time;
- one ordinary application read does not pre-drain a second transport read;
- unrelated application input before and after a response;
- cancellation before transmission with no emitted request;
- caller cancellation after transmission;
- timeout after transmission remaining distinct from cancellation;
- a queued timeout producing no later emission;
- late-response consumption before the next query is emitted;
- bounded late-response expiration releasing the serialized slot;
- response-shaped bytes buffered before arming remaining application input;
- suspension interrupting an issued query and retaining its drain slot;
- query emission waiting for the shared control-output gate;
- query response waits releasing the shared control-output gate after emission;
- queries requested while the session is already suspended being rejected before
  a transaction manager can be created;
- `CONIN$`/`CONOUT$` remaining a valid Windows-console conversational pair;
- disposal terminating an issued query.

The existing T08/T14-T18/T22 tests remain the regression gate for ordinary input
semantics.

---

## 16. Gate Result

T23 is accepted when:

1. active queries make progress without requiring an application
   `ReadEventAsync` loop;
2. the decoder remains the only raw terminal reader;
3. deferred application input is bounded and ordered;
4. queued ambiguity-sensitive transactions are serialized;
5. cancellation or timeout before emission produces no request bytes;
6. cancellation or timeout after emission completes the caller independently
   from the wire lifetime;
7. late responses are consumed during bounded ownership;
8. the next ambiguous query is held until the prior wire slot resolves or
   expires;
9. session-generated control traffic is serialized;
10. suspend/re-entry cannot turn a stale pre-suspend response into a successful
    later query;
11. disposal terminates all transaction ownership;
12. no public unfinished protocol API is introduced.

**Gate T23: complete.**

The next tranche is **T24 — CSI Device, Status, and Cursor Queries**.
