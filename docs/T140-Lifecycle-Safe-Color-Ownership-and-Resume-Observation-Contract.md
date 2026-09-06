# T140 — Lifecycle-Safe Color Ownership and Resume-Observation Contract

**Release:** `0.14.0`  
**Tranche:** `T140`  
**Development version:** `0.14.0-alpha.1`  
**Status:** Contract frozen; T141 implementation next

---

## 1. Purpose

T140 resolves the architectural problem deliberately left open by T136.

`Icod.Terminal 0.13.0` can explicitly observe, mutate, and reset indexed palette and dynamic colors, but color mutation is unscoped because lifecycle-safe exact restoration could not be claimed truthfully under the then-current resume ordering.

The central 0.13 ordering was:

```text
reacquire output/native state
reapply input mode
resume presentation state
resume input-protocol state
resume lifecycle participants
resume public query transactions
publish Resumed
```

A lifecycle participant therefore could not issue a terminal query while resuming. Moving public query availability earlier would solve the color problem superficially, but would silently broaden the public lifecycle contract: arbitrary higher-layer participants and callers could then initiate active queries while the session was only partially re-entered.

T140 rejects that shortcut.

---

## 2. Frozen design decision

0.14 SHALL introduce a **distinct internal post-resume observation phase** for session-owned state that requires a fresh terminal observation before it can be reapplied truthfully.

The ordinary public query path remains suspended until normal lifecycle re-entry is complete.

The intended resume phases are:

```text
Phase 1 — native/session host re-entry
    reacquire output mode/setup
    reapply captured input-mode policy

Phase 2 — non-observational Terminal-owned state
    resume presentation state
    resume input-protocol state

Phase 3 — internal observation window
    permit only session-owned observation-dependent managers to issue bounded queries
    establish fresh external baselines for the new lifecycle epoch
    reapply retained logical ownership only after observation succeeds

Phase 4 — ordinary lifecycle participants
    invoke ITerminalSessionLifecycleParticipant resume callbacks

Phase 5 — public query availability
    resume ordinary public query transactions

Phase 6 — publish live state
    mark lifecycle re-entry complete
    mark state valid
    publish Resumed
```

The exact internal method names are left to T141, but this phase separation is frozen.

---

## 3. Public lifecycle-participant contract remains narrow

`ITerminalSessionLifecycleParticipant` remains a higher-layer lifecycle hook rather than an observation-capable query hook.

The public contract is now explicit:

- participant preparation runs before Terminal releases host/presentation state;
- participant resume runs only after Terminal has restored the ordinary host/presentation/input-protocol substrate;
- **public terminal queries remain unavailable during participant resume callbacks**;
- a participant SHALL NOT call public `TerminalSession` query methods from `ResumeAfterTerminalSuspendAsync(...)`;
- `Resumed` is published only after participant resume and public-query restoration are complete.

T140 adds a regression test proving `QueryPaletteColorAsync(...)` is rejected during an ordinary participant resume callback.

This preserves the behavioral meaning that higher-layer participants do not observe a half-entered session.

---

## 4. Internal observation phase is not a second query subsystem

T141 SHALL NOT add:

- a second terminal input reader;
- a second response router;
- a background color listener;
- a raw protocol path bypassing `TerminalInputCoordinator`;
- unbounded or identifier-free concurrent queries;
- general public query availability during lifecycle re-entry.

The internal observation phase SHALL reuse the existing:

- one-reader input coordinator;
- response framing;
- expectation-driven matching;
- ambiguity serialization;
- output serialization;
- bounded timeout and late-response ownership.

The architectural difference is authorization/lifecycle phase, not transport ownership.

---

## 5. Lifecycle epoch model

T140 freezes the concept of a **color baseline lifecycle epoch**.

### 5.1 Initial epoch

For one color identity, the first scoped owner may be acquired only after the terminal reports a valid baseline color.

That observation establishes the external baseline for the current lifecycle epoch.

### 5.2 Same-epoch nesting

While the session remains in the same lifecycle epoch:

- the external baseline is retained;
- nested owners do not re-query merely because another owner is acquired;
- the logical owner stack determines the requested effective color;
- final release restores the epoch's external baseline explicitly.

### 5.3 Suspend terminates baseline authority

Before managed suspend, the current epoch's external baseline is replayed to return the terminal to the external environment.

After the process resumes, the old baseline remains historical information but is **not authoritative for restoration in the new epoch**.

A shell, multiplexer, terminal, remote peer, or user action may have changed the color while the process was stopped.

### 5.4 Resume establishes a new epoch

If logical color ownership survives suspend, the internal observation phase must:

1. query the currently visible external color after resume;
2. validate the correlated reply;
3. record that observation as the new epoch baseline;
4. only then reapply the effective retained owner color.

A retained pre-suspend baseline SHALL NOT be substituted if the new observation fails.

---

## 6. Query-before-mutate acquisition

For a color identity with no current owner:

```text
validate request
acquire ownership manager gate
query baseline
parse typed TerminalColor
construct full mutation frame
commit mutation
create/activate lease
```

If baseline observation fails for any reason before mutation commitment:

- no color mutation is retained;
- no external baseline is invented;
- no lease is returned;
- no reset sequence is emitted as compensation.

If query succeeds but the subsequent mutation write fails, T142/T143 must preserve enough manager state to distinguish:

- baseline successfully known;
- requested mutation not proven successful;
- no active owner unless mutation commitment semantics justify one.

T144 will fault-inject this boundary.

---

## 7. Exact restoration semantics

A scoped color owner restores by explicit color replay.

Indexed palette:

```text
OSC 4 ; index ; baseline-color ST
```

Dynamic color:

```text
OSC Ps ; baseline-color ST
```

where `Ps` is the exact dynamic-color identity 10, 11, 12, 13, 14, 17, or 19.

The following are **not restoration**:

```text
OSC 104
OSC 104 ; index
OSC 110
OSC 111
OSC 112
OSC 113
OSC 114
OSC 117
OSC 119
```

They remain public terminal-policy reset operations and are not used internally to discharge an exact-restoration obligation.

---

## 8. Nested ownership model

T140 freezes identity-aware ordered ownership.

For one palette index or one dynamic-color identity:

```text
external baseline = B
outer requests A  -> emit A
inner requests C  -> emit C
outer releases    -> no output; C is still effective
inner releases    -> emit B
```

If the inner owner releases first:

```text
external baseline = B
outer requests A  -> emit A
inner requests C  -> emit C
inner releases    -> emit A
outer releases    -> emit B
```

Rules:

- ownership identity is the palette index or dynamic-color semantic identity;
- different identities may be owned independently;
- out-of-order release is supported;
- the most recently acquired active owner for one identity is effective;
- final release restores the external baseline for the current lifecycle epoch;
- releasing an inactive/non-effective owner cannot restore over a still-active owner.

Palette and dynamic colors may use separate managers sharing one internal ownership/lifecycle helper; T141 may choose the implementation shape, but the public semantics above are fixed.

---

## 9. Invalidation semantics

`TerminalSession.InvalidateState()` means physical terminal state is no longer trusted.

For color ownership it SHALL:

- retain logical owners;
- retain the current epoch baseline as historical/current-epoch restoration data until an epoch transition occurs;
- mark the last emitted effective color as physically uncertain;
- avoid automatic querying merely because invalidation was called;
- require the next safe manager transition to re-establish the effective logical request as needed.

Invalidation alone does not create a new lifecycle epoch because no external ownership handoff has necessarily occurred.

Managed suspend/resume does create a new baseline epoch.

---

## 10. Suspend ordering

Before host input mode/output setup are released, color managers with active owners must return their identities to the current external baseline.

The preferred relative ordering is:

```text
higher/application presentation prepares
other session-owned transient/reversible control state prepares
scoped color owners restore external baselines
input protocols/presentation leave as currently defined
flush
release output mode
restore host input baseline
suspend
```

T141/T142 may refine the precise placement relative to existing core managers based on lock/output ordering, but the color baseline must be restored while the output path remains usable.

A color restoration failure during suspend is a lifecycle failure and participates in the existing aggregate restoration path. The session must not claim it returned the terminal baseline when it did not.

---

## 11. Resume ordering and failure

Observation-dependent color re-entry occurs after the terminal input/output substrate needed for queries is operational, but before ordinary public lifecycle participants resume.

For each active color identity:

```text
query fresh baseline
if success:
    record new epoch baseline
    replay effective logical owner color
if query fails:
    do not reuse old baseline
    do not reapply owned color
    fail lifecycle re-entry
```

T140 deliberately chooses **fail re-entry** rather than silently dropping ownership or fabricating restoration state.

Rollback after such a failure must make a best effort to return the session to the host baseline using only state that is still truthful for the failed re-entry path. T141/T144 will define and fault-inject the exact rollback sequence.

---

## 12. Public acquisition result semantics

T140 freezes the following direction:

- color-query timeout remains `TimeoutException`;
- caller cancellation remains cancellation;
- malformed correlated color reply remains `FormatException`;
- transport/session failures remain failures;
- a write completing successfully means emission, not proof the terminal applied the color;
- no response is **not** converted into a permanent unsupported capability result.

Therefore the scoped acquisition APIs should return leases directly rather than wrapping query timeout in `TerminalControlResult<T>` merely to imitate static terminfo capability discovery.

Provisional T142/T143 public shape:

```csharp
ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);

ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

This API direction is frozen for implementation review, subject only to concrete evidence from T141/T142 that a controlled result is needed for a condition other than active-query nonresponse.

---

## 13. Lease surface direction

Separate lease types are preferred because palette and dynamic identity are semantically distinct.

Expected immutable informational properties:

```text
TerminalPaletteColorLease.Index
TerminalPaletteColorLease.Color

TerminalDynamicColorLease.Kind
TerminalDynamicColorLease.Color
```

The lease objects SHALL implement `IAsyncDisposable` because restoration requires asynchronous serialized output and may fail.

Successful disposal releases logical ownership and performs any required effective-color transition.

A failed restoration/reapply SHALL NOT silently mark cleanup complete. The manager retains cleanup responsibility when retry remains meaningful. Exact public retry behavior will be frozen in T142/T143 after implementation evidence.

---

## 14. Lock-order contract

T141–T144 must preserve a single understandable dependency direction.

Managers may serialize their own logical state, then use session query/output serialization, but lifecycle code must avoid holding a session-global lifecycle monitor while awaiting a manager which in turn needs that same lifecycle monitor.

No implementation may hold an output lease while waiting for a terminal query response; query request emission itself owns output only for the bounded write/flush step.

The hardening gate must specifically test for cycles among:

```text
lifecycle transition
color manager gate
query ambiguity gate
response expectation/input coordinator
session output gate
```

---

## 15. Existing manager compatibility

T140 audits the current lifecycle classes and freezes that observation-independent behavior must remain behaviorally unchanged:

- presentation state;
- input protocol state;
- OSC 8 hyperlink ownership;
- cursor-style ownership;
- synchronized-output ownership;
- OSC 9;4 progress ownership;
- OSC 22 pointer-shape ownership;
- higher-layer `ITerminalSessionLifecycleParticipant` callbacks.

Existing managers SHALL NOT be forced through a query phase merely because color ownership needs one.

The internal observation phase is opt-in for session-owned managers that can justify it.

---

## 16. T141 implementation target

T141 SHALL implement an internal lifecycle-observation facility with these properties:

1. inaccessible to ordinary public callers;
2. usable only from the lifecycle pump during the post-resume observation phase;
3. reuses the existing query transaction/router/input coordinator;
4. preserves bounded ambiguity and late-response ownership;
5. leaves ordinary public query transactions logically suspended;
6. permits deterministic registration/order of observation-dependent core participants;
7. propagates observation/reapply failures into lifecycle re-entry failure;
8. provides rollback hooks without inventing a color-specific lifecycle subsystem;
9. does not alter `ITerminalSessionLifecycleParticipant` public signatures;
10. adds tests proving public queries remain rejected until re-entry completes.

---

## 17. T140 gate

T140 is complete when:

- the lifecycle/query ordering above is documented and accepted;
- the lifecycle epoch model is frozen;
- query-before-mutate and explicit replay restoration are frozen;
- nested/out-of-order ownership semantics are frozen;
- invalidation and suspend/resume semantics are distinct and frozen;
- the public acquisition error model is frozen;
- the public participant contract explicitly keeps queries unavailable during resume;
- a regression test enforces that public boundary;
- T141 has a concrete internal implementation target.

**T140 is complete in this form.**
