# T27 — Query Integration and Probe Acceptance

**Project:** `Icod.Terminal`
**Release line:** `0.3.0`
**Development version:** `0.3.0-alpha.7`
**Tranche:** T27 — Query Integration and Probe Acceptance
**Status:** Complete — repository matrix and downstream DCurses acceptance passed

---

## 1. Purpose

T27 moves the 0.3 query work out of protocol-isolated tests and proves that
active terminal conversations coexist with the rest of a real
`TerminalSession`.

T24-T26 already provide explicit typed CSI and DCS operations. T27 does not add a
second reader, a raw-protocol escape hatch, or another transaction manager.

---

## 2. No Aggregate Probe API

T27 deliberately does **not** add a convenience probe-composition API.

The existing explicit operations already compose naturally:

- Primary Device Attributes;
- Secondary Device Attributes;
- standard Device Status Report;
- Cursor Position Report;
- DECRQSS status strings;
- XTGETTCAP live capability observations.

An aggregate probe object would immediately have to freeze policy for:

- which requests run;
- request ordering;
- per-probe versus aggregate deadlines;
- partial success;
- unsupported results;
- malformed correlated responses;
- cancellation;
- whether future query families silently become part of the aggregate.

Concrete T24-T26 experience does not demonstrate a stable abstraction worth that
additional public contract.

Consumers can compose the explicit async operations according to their own
policy, while the session continues to own serialization and late-response
safety.

T28's API-regret review may revisit this decision if integration evidence changes.

---

## 3. Explicit Query Sample

T27 adds:

`samples/Icod.Terminal.Query.Sample`

The sample opens a normal live session and then **explicitly** issues:

- Primary DA;
- Secondary DA;
- standard DSR;
- CPR;
- DECRQSS for SGR;
- XTGETTCAP for `TN`.

The sample also attempts a reversible presentation lease and a reversible
rich-input lease before issuing the probes. Missing lease capabilities are
reported as controlled availability results rather than guessed.

Each query uses a short caller-visible deadline so unsupported live terminals do
not make the sample hang indefinitely.

After the probes, the sample waits for one ordinary unified input/lifecycle event
to demonstrate that active interrogation has not replaced the normal event loop.

Running the sample is itself the explicit opt-in to probing. Merely opening a
`TerminalSession` still sends no interrogation request.

---

## 4. Integrated Test Coverage

T27 adds an integration fixture which combines the query stack with existing
session mechanisms rather than testing another protocol parser in isolation.

The fixture verifies:

- opening an injected session performs no automatic interrogation and starts no
  raw input read merely for probing;
- CPR remains pending while ordinary UTF-8 application input is delivered through
  `ReadEventAsync`;
- CSI, DECRQSS, and XTGETTCAP transactions operate while both presentation and
  rich-input leases are active;
- a resize lifecycle signal is delivered through the unified event loop without
  terminating an unrelated outstanding query;
- suspension interrupts caller-visible success;
- resume restores session-owned state;
- an interrupted transaction retains its ambiguity-sensitive wire slot until its
  late response is consumed;
- a later query emits only after that ownership is resolved;
- caller cancellation retains late-response ownership;
- caller-visible timeout retains late-response ownership;
- disposal terminates an outstanding query while presentation and rich-input
  leases are still owned;
- all integrated cases retain the single-reader invariant.

The tests use explicit write-notification synchronization rather than fixed
`Task.Yield()` scheduler-spin budgets.

---

## 5. Package-Only Consumer

`tools/package-smoke` is extended so package validation now exercises the public
0.3 query API through an installed `Icod.Terminal` package rather than through a
project reference.

Its injected terminal transport now provides deterministic responses to:

- CPR — one CSI transaction;
- DECRQSS SGR — one DCS transaction;
- XTGETTCAP `TN` — the second public DCS family.

The smoke consumer still verifies rich input, output, terminfo capability use,
terminal-mode serialization, deterministic restoration, and disposal.

The scripted response bytes are published only after the corresponding request
write is observed, preventing pre-buffered response-shaped bytes from bypassing
the T23 expectation-arming contract.

---

## 6. Session Opening Remains Passive

T27 does not change `TerminalSession.OpenAsync`.

No DA, DSR, CPR, DECRQSS, XTGETTCAP, or other terminal interrogation is sent on
session creation.

Active observations occur only because a caller invokes one of the explicit
query methods.

This remains a frozen 0.3 contract.

---

## 7. Lifecycle Acceptance

The integrated lifecycle tests exercise query behavior across:

- resize;
- suspension;
- automatic test resume;
- cancellation;
- monotonic timeout;
- bounded late-response consumption;
- disposal.

Presentation and rich-input leases are deliberately active in the disruption
tests so re-entry is tested in the same session that owns query state.

A pre-disruption response is never reported as a successful post-resume result.

---

## 8. Downstream DCurses Acceptance

`Icod.DCurses` remains a consumer and is not added as an `Icod.Terminal`
dependency.

The final downstream acceptance consumed the published
`Icod.Terminal 0.3.0-alpha.8` candidate from the DCurses Alpha-22 compatibility
build. Alpha.8 superseded alpha.7 as the accepted 0.3 candidate after the
release-candidate race correction.

The downstream check must confirm that DCurses:

- builds without private access to the response router;
- keeps using `TerminalSession` as its terminal owner;
- does not add a second raw input reader;
- requires no private CSI/DCS parser to coexist with 0.3 queries;
- retains its existing presentation and input behavior.

No source change to DCurses is expected merely to accommodate the additive 0.3
query API.

---

## 9. Validation Matrix

The repository-side acceptance target remains:

- `net8.0`;
- `net9.0`;
- `net10.0`;

on:

- Windows;
- Ubuntu;
- macOS.

The normal repository build/test/package validation should compile the new query
sample, run the new integration tests, and run the extended package-only smoke
consumer.

The corrected alpha.8 candidate passed the repository matrix on Windows, Ubuntu,
and macOS, and the downstream DCurses Alpha-22 compatibility build passed
against the published package.

---

## 10. Gate State

**Gate T27 is complete.**

The corrected alpha.8 candidate passed the normal Windows/Ubuntu/macOS
repository matrix for `net8.0`, `net9.0`, and `net10.0`.

The DCurses Alpha-22 compatibility build consumed the published alpha.8 package
with `Icod.TermInfo 1.3.0`, retained `TerminalSession` ownership, and required no
second raw input reader or private CSI/DCS parser.

The integrated query contract is therefore accepted for stable 0.3.
