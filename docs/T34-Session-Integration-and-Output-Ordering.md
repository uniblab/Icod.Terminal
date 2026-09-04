# T34 — Session Integration and Output Ordering

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Development version:** `0.4.0-alpha.6`  
**Tranche:** T34  
**Theme:** Session-owned OSC/title integration, ordering, concurrency, flush, and close semantics  
**Status:** Implemented; CI validation pending

---

## 1. Purpose

T34 integrates the OSC 0/1/2 semantic operations from T31–T33 with the existing session-owned output coordination model.

T29 froze the rule that an OSC title frame must never be interleaved with another terminal-control sequence or ordinary application output. T30 made each title frame a complete single `ITerminalOutput.WriteAsync` submission. T34 now defines when that submission may occur relative to other session-owned output.

The objective is not to turn `TerminalSession` into a general-purpose output multiplexer. It is to make output produced **through session APIs** deterministic and safe around title operations and existing session control traffic.

---

## 2. Shared session output gate

The existing session control-output semaphore remains the serialization point for session-owned terminal-control transactions.

T34 extends that coordination to:

- OSC 0 `SetTitleAsync(...)`;
- OSC 1 `SetIconNameAsync(...)`;
- OSC 2 `SetWindowTitleAsync(...)`;
- ordinary application text written through `WriteTextAsync(...)`.

Therefore a title frame cannot overlap with:

- another title frame;
- ordinary `WriteTextAsync(...)` output;
- query request emission while the query manager owns the control-output gate;
- presentation-state transitions while the presentation manager owns the gate;
- input-protocol transitions while the input-protocol manager owns the gate.

The borrowed `ITerminalOutput` object remains publicly exposed and caller-owned. Code which writes directly through `session.Output` deliberately bypasses session ordering policy and therefore remains the caller's synchronization responsibility.

---

## 3. Application-text ordering

`WriteTextAsync(...)` now acquires the shared session-output lease before calling the borrowed output service.

The application text is encoded before the lease is acquired. Once the lease is held, the resulting byte write runs without concurrent session-owned title/control output.

Caller cancellation remains ordinary .NET cancellation for application text. Unlike the bounded OSC frame writer, `WriteTextAsync(...)` does not claim that arbitrary application output is an indivisible protocol frame after transmission begins.

---

## 4. OSC title ordering

All three title operations acquire the session-output lease before invoking the internal T30 writer.

The T30 writer still performs its own complete validation before transmission and still submits one complete OSC frame in one output call.

T34 therefore gives title operations two layers of protection:

1. **frame integrity** — T30 constructs and submits one validated complete frame;
2. **session ordering** — T34 prevents another session-owned output operation from entering the borrowed output service while that frame is being emitted.

No title operation acquires an input-side response transaction.

---

## 5. Cancellation semantics

For title operations:

- cancellation before waiting for the output lease emits nothing;
- cancellation while waiting for the output lease emits nothing;
- cancellation observed immediately after the lease is acquired but before the writer commits transmission emits nothing;
- once the validated OSC frame is committed to the borrowed output service, T30 continues to use its non-cancellation-driven frame write so ordinary caller cancellation does not deliberately abandon the control string mid-frame.

For ordinary `WriteTextAsync(...)`, the caller token remains attached to the underlying write.

---

## 6. Flush policy

OSC 0/1/2 title operations do **not** implicitly flush after each frame.

Rationale:

- a title operation is an output operation, not a request/response synchronization point;
- forcing a flush for every title mutation would impose unnecessary transport costs;
- query transactions and state transitions already retain their own explicit flush semantics where protocol/lifecycle correctness requires them;
- session disposal performs the final output flush before restoring host terminal state.

This is an explicit 0.4.0 policy rather than an accidental omission.

---

## 7. Disposal and closed-session behavior

When `DisposeAsync()` begins, the session atomically stops accepting new public session-owned application/title output.

A new `WriteTextAsync(...)`, `SetTitleAsync(...)`, `SetIconNameAsync(...)`, or `SetWindowTitleAsync(...)` operation which begins after that close boundary fails with `ObjectDisposedException`.

An operation which already holds the output lease is allowed to finish. Internal restoration/cleanup operations continue to use the control-output path so disposal can complete deterministic protocol cleanup after public output admission has closed.

This distinction prevents a disposal race in which new title/application traffic continually enters behind restoration work.

---

## 8. Presentation and input-protocol interaction

Presentation and input-protocol managers already serialize their reversible transition batches using `AcquireControlOutputAsync(...)`.

Because T34 title/application operations use the same semaphore, they wait until a transition batch releases ownership. A title frame therefore cannot appear in the middle of a presentation or rich-input state transition.

The transition managers retain their existing transactional rollback and flush rules; T34 does not rewrite those state machines.

---

## 9. Raw terminal-string scope

`WriteTerminalStringAsync(...)` remains the lower-level terminfo terminal-string primitive used by existing transition managers while they already own the control-output lease.

T34 does not recursively acquire the session-output semaphore inside this method because doing so would deadlock existing transition batches which correctly hold the same gate around multiple capability writes.

Consequently:

- higher-level session managers continue to provide ordered terminal-string output by holding the control-output lease around their transition batches;
- external callers using the low-level `WriteTerminalStringAsync(...)` primitive remain responsible for coordinating that raw protocol output with other concurrent session traffic.

This limitation is explicit for the 0.4.0 API audit in T35 rather than hidden behind an unsafe re-entrant locking heuristic.

---

## 10. Test coverage

T34 adds deterministic in-memory tests proving:

- an OSC title frame waits until an in-progress `WriteTextAsync(...)` operation releases the output lease;
- session-owned writes do not overlap in the borrowed output implementation;
- `WriteTextAsync(...)` waits while an existing control-output lease is held;
- title operations do not flush implicitly;
- disposal performs the final flush;
- disposed sessions reject new application/title output;
- existing T29–T33 byte-exact title tests remain authoritative for frame content.

Tests do not mutate the CI runner terminal.

---

## 11. T34 completion gate

T34 is complete when:

- OSC 0/1/2 title operations participate in session output ordering;
- ordinary `WriteTextAsync(...)` participates in the same ordering domain;
- title operations cannot interleave with existing query, presentation, or input-protocol control-output ownership;
- title operations retain T30 complete-frame validation/emission semantics;
- title operations have an explicit no-implicit-flush policy;
- new public application/title output is rejected once disposal begins;
- deterministic concurrency tests demonstrate the ordering contract;
- no public generic OSC/raw escape API is introduced.

T35 can now audit the public API and documentation with the implementation ordering contract established rather than provisional.
