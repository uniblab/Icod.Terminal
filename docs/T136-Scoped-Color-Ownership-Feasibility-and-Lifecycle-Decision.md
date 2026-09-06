# T136 — Scoped Color Ownership Feasibility and Lifecycle Decision

**Release:** `0.13.0`  
**Tranche:** `T136`  
**Development version:** `0.13.0-alpha.7`  
**Status:** Implemented; exact-head validation pending

---

## 1. Question

T136 evaluates whether indexed palette entries and/or dynamic colors should gain public scoped ownership leases in 0.13 using query-before-mutate restoration.

The answer is deliberately conservative:

> **0.13 SHALL NOT expose public palette or dynamic-color restoration leases.**

The existing mutation/query/reset APIs remain unscoped.

---

## 2. What is feasible

For an ordinary uninterrupted terminal session, a truthful restoration sequence is technically possible:

1. query the current palette/dynamic color;
2. retain the returned `TerminalColor` as the baseline;
3. mutate to the requested owned value;
4. on final release, explicitly write the observed baseline color.

This is materially stronger than OSC 104/110–119 reset, which returns to terminal policy/default and is not exact restoration.

Nested ownership could also be represented with identity-aware stacked values similar to existing pointer/cursor managers.

---

## 3. Why lifecycle-safe ownership is not frozen for 0.13

The difficulty is suspend/resume truthfulness.

The current `TerminalSession` lifecycle order is intentionally:

1. suspend active query transactions;
2. prepare lifecycle participants;
3. restore/suspend lower terminal state;
4. on resume, reapply lower terminal state;
5. resume lifecycle participants;
6. re-enable active query transactions.

A color owner that wants to preserve truth across suspension cannot safely assume its pre-suspend observed baseline still describes the terminal after resume. External shell/terminal configuration may have changed while the process was suspended.

To be truthful, the color owner would need to:

1. observe the post-resume current color;
2. retain that new value as the eventual restoration baseline;
3. only then reapply the owned color.

But active queries are deliberately unavailable while lifecycle participants are being resumed. Therefore a lifecycle participant cannot perform the required post-resume observation without changing core lifecycle ordering.

Re-enabling queries before all lifecycle participants finish would be a repository-wide lifecycle semantic change, not a local color feature. T136 does not make that change merely to enable leases.

---

## 4. Why reset-based leases are rejected

A lease which releases with:

```text
OSC 104
OSC 110–119
```

would only request terminal policy/default state.

It would not restore the value observed before acquisition and therefore would violate the T130 truthfulness requirement.

Such a lease is not included.

---

## 5. Why retaining the old pre-suspend baseline is rejected

Replaying the pre-suspend baseline after resume is also not truthful enough.

The terminal or shell may legitimately change palette/default colors during the suspension interval. Restoring the stale pre-suspend baseline at final release could overwrite those external changes.

Therefore T136 does not claim that the pre-suspend observation remains authoritative after resume.

---

## 6. Frozen 0.13 public behavior

`SetPaletteColorAsync(...)`, `SetPaletteColorsAsync(...)`, and `SetDynamicColorAsync(...)` are explicit **unscoped mutations**.

They:

- emit requested state;
- do not create ownership records;
- do not capture a restoration baseline automatically;
- do not reset/replay colors during `InvalidateState()`;
- do not reset/replay colors during suspend/resume;
- do not reset/replay colors during session disposal.

Observation remains explicit through `QueryPaletteColorAsync(...)` and `QueryDynamicColorAsync(...)`.

Callers which require a local uninterrupted query/mutate/restore transaction may compose those public primitives themselves, but `Icod.Terminal 0.13` does not market that composition as a lifecycle-safe lease.

---

## 7. Tests

T136 adds lifecycle tests proving:

- palette mutation is not automatically reset or replayed by disposal;
- dynamic-color mutation is not automatically reset or replayed by disposal;
- suspend/resume does not emit color reset/replay traffic for unscoped mutations;
- `InvalidateState()` emits no color traffic.

These tests protect the absence of synthetic ownership state.

---

## 8. Future path

A future release may revisit scoped color ownership if `TerminalSession` gains a reviewed lifecycle phase in which post-resume active observation is available before higher-level state is reapplied.

That work would need to define:

- post-resume baseline re-observation;
- nested owner ordering;
- partial query/mutation failure rollback;
- cleanup retry semantics;
- invalidation semantics;
- disposal ordering;
- interaction with downstream `Icod.DCurses` color ownership.

It should be treated as a lifecycle architecture enhancement rather than an incidental OSC feature.

---

## 9. T136 decision

0.13 keeps color control and observation powerful but explicit.

The release does **not** add palette or dynamic-color leases. This avoids both reset masquerading as restoration and stale pre-suspend baseline replay.

T137 can therefore focus on composition, lifecycle non-interference, and real downstream `Icod.DCurses` observation acceptance without carrying an unsound ownership abstraction.
