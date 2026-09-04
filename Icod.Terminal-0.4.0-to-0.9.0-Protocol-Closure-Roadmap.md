# Icod.Terminal 0.4.0 through 0.9.0 Protocol Closure Roadmap

**Project:** `Icod.Terminal`  
**Repository:** `https://github.com/uniblab/Icod.Terminal`  
**Predecessor:** `0.3.0` — released and published  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Runtime dependencies at roadmap creation:** `Icod.TermInfo 1.4.1`; `Icod.Timing 1.0.0`  
**Roadmap span:** `0.4.0` through `0.9.0`  
**Purpose:** close the operational terminal-protocol substrate opened by `0.3.0` before defining the next broad development tranche at `0.10.0`

---

## 1. Executive Decision

The `0.4.0` through `0.9.0` development sequence SHALL be reorganized as a deliberate protocol-closure ladder:

| Version | Primary protocol family | Principal outcome |
| --- | --- | --- |
| `0.4.0` | OSC 0 / 1 / 2 | Window/icon title operations and the first safe outbound OSC framing contract |
| `0.5.0` | OSC 7 | Current-working-directory URI publication and reusable URI payload policy |
| `0.6.0` | OSC 8 | Hyperlink operations, OSC parameters, and scoped begin/end output state |
| `0.7.0` | OSC 52 | Clipboard/selection operations, base64 payloads, security policy, and bidirectional OSC transaction support where enabled |
| `0.8.0` | Cursor Style | DECSCUSR-style cursor-shape control, state/query integration where available, and CSI-intermediate output support |
| `0.9.0` | Synchronized output | DEC private synchronized-output mode, nesting/lease semantics, flush boundaries, and deterministic release |

The previous roadmap themes assigned to `0.5.0` through `0.9.0` are not cancelled. They are deferred until the operational protocol substrate is complete enough to support them cleanly.

`0.10.0` SHALL remain intentionally unassigned until `0.9.0` evidence is available. Candidate post-closure work includes modern keyboard protocols, endpoint/transport expansion, generic protocol extensibility, platform/lifecycle hardening, and eventual public-contract freeze.

---

## 2. Why This Sequence Is Stronger

Version `0.3.0` established active terminal conversations:

- one session-owned input path;
- bounded request/response transactions;
- response correlation;
- cancellation and monotonic deadlines;
- late-response ownership;
- typed CSI and DCS query operations;
- a reusable foundation for later live-terminal protocols.

The next releases should consume and extend that substrate rather than immediately branching into unrelated feature areas.

The proposed sequence deliberately increases protocol complexity one dimension at a time:

```text
0.3.0  active query/response routing
   |
   v
0.4.0  safe outbound OSC framing
   |
   v
0.5.0  structured URI payloads
   |
   v
0.6.0  parameterized + scoped OSC state
   |
   v
0.7.0  bidirectional/security-sensitive OSC
   |
   v
0.8.0  CSI state mutation + status integration
   |
   v
0.9.0  nested transactional output state
   |
   v
0.10.0 broad next-stage foundation
```

This progression is intentionally architectural, not merely chronological.

---

## 3. Cross-Cutting Protocol Rules

The following rules apply to every release in this sequence.

### 3.1 Semantic APIs, not arbitrary escape injection

Public APIs SHOULD expose terminal operations rather than raw escape-string assembly.

The library MAY introduce internal reusable OSC/CSI/DCS emitters, but a public general-purpose `SendOsc(...)`, `SendCsi(...)`, or equivalent extension surface SHALL NOT be frozen merely to reduce implementation duplication.

Generic protocol extensibility remains a later design decision.

### 3.2 Protocol bytes remain serialized through the session output path

Operational sequences SHALL use the same session-owned output coordination as other terminal control operations so that protocol frames are not accidentally interleaved with one another or with partial application output.

Where a protocol introduces begin/end state, ownership and nesting rules SHALL be explicit.

### 3.3 Payload injection must be impossible by construction

User-controlled string payloads SHALL NOT be concatenated directly into terminal control sequences.

Protocol-specific encoders SHALL reject or encode payload data capable of terminating or injecting control sequences, including ESC, BEL, C0 controls, OSC string terminators, and other forbidden code units appropriate to the protocol.

UTF-8 output policy SHALL remain deterministic.

### 3.4 Capability uncertainty remains observable

A terminal accepting a control sequence and a terminal being known to support that sequence are different facts.

APIs SHALL distinguish, where meaningful:

- unsupported by declared capability/profile;
- support unknown but operation may be emitted optimistically;
- positively observed/queried support;
- operation emitted successfully;
- query refused, timed out, or returned malformed data.

No release SHALL equate `TERM` naming with proof of operational-protocol support.

### 3.5 Query support builds on `0.3.0`

Where a later protocol has a meaningful response/query form, it SHALL use the single `TerminalSession` input/router architecture rather than adding a private reader loop.

New response frame families MAY extend the router, but ordinary keyboard, mouse, paste, focus, and lifecycle traffic must continue to be preserved.

### 3.6 State restoration must be truthful

A scoped API SHALL restore previous state only when previous state is known or when the protocol defines a reliable reset/default operation that the contract explicitly promises.

The library SHALL NOT claim exact restoration merely because an inverse-looking escape sequence exists.

---

# 4. Version 0.4.0 — OSC 0 / 1 / 2

## Theme

Window/icon title operations and safe outbound OSC framing.

## Architectural contribution

`0.4.0` establishes the smallest useful OSC output substrate without exposing a generic public OSC extension API.

It should prove:

- deterministic OSC introducer/terminator framing;
- UTF-8 payload encoding;
- control-sequence injection prevention;
- session-output serialization;
- explicit semantic distinction among OSC 0, OSC 1, and OSC 2;
- unsupported/unknown/emitted result semantics where useful.

Possible semantic operations include:

- set icon and window title — OSC 0;
- set icon name — OSC 1;
- set window title — OSC 2.

Exact public names remain subject to the dedicated `0.4.0` milestone roadmap and API review.

## Scope boundary

`0.4.0` SHALL NOT introduce OSC 7, OSC 8, OSC 52, title-query stacks, arbitrary public OSC emission, or a terminal-emulator title model.

---

# 5. Version 0.5.0 — OSC 7

## Theme

Current working directory / location publication.

## Architectural contribution

`0.5.0` adds structured URI payload handling on top of the safe OSC transport established by `0.4.0`.

The tranche should establish:

- deterministic `file://`-style URI construction policy where appropriate;
- hostname and path treatment;
- percent-encoding rules;
- Windows versus POSIX path behavior;
- caller-supplied versus process-current-directory policy;
- no implicit environment disclosure without an explicit API invocation.

The library should prefer a semantic current-location operation over a raw OSC 7 payload API.

---

# 6. Version 0.6.0 — OSC 8

## Theme

Hyperlinks.

## Architectural contribution

OSC 8 introduces both structured parameters and paired begin/end output state.

The tranche should establish:

- URI payload reuse from `0.5.0`;
- OSC 8 parameter encoding;
- optional hyperlink identifiers;
- begin/end hyperlink operations;
- a scoped/lease API if it can be made deterministic;
- nesting/interleaving policy;
- safe termination on disposal/failure;
- clear separation between terminal hyperlink state and `Icod.DCurses` presentation policy.

This is the first release in the sequence where a scoped operational-output abstraction is likely to be justified.

---

# 7. Version 0.7.0 — OSC 52

## Theme

Clipboard and selection operations.

## Architectural contribution

OSC 52 is the security boundary of the OSC series and the first release that may require bidirectional OSC response routing.

The tranche should establish:

- explicit selection-target representation;
- bounded base64 encoding/decoding;
- payload-size limits;
- write versus read/query policy;
- explicit opt-in for clipboard reads or other potentially sensitive operations;
- structured cancellation/timeout behavior for queries;
- OSC response framing on the session-owned input path;
- late-response handling compatible with the `0.3.0` transaction model;
- no accidental capture of unrelated OSC traffic.

Security-sensitive behavior SHALL be documented independently of terminal support detection.

The release MAY deliberately support write-only OSC 52 first if read/query behavior cannot yet satisfy the security and routing contract. Any such limitation must be explicit in the release matrix rather than silently approximated.

---

# 8. Version 0.8.0 — Cursor Style

## Theme

Operational cursor-shape/style control.

## Architectural contribution

This release deliberately moves beyond OSC and exercises the CSI/DCS side of the operational protocol substrate.

The tranche should establish:

- typed cursor-style values rather than raw numeric parameters;
- DECSCUSR-style CSI emission where appropriate;
- CSI intermediate-byte encoding in the output layer;
- query/restore support through existing status-query mechanisms where reliable;
- DECRQSS integration where applicable;
- reset/default behavior;
- scoped restoration only when the previous value is actually known;
- distinction between cursor style and cursor visibility.

The public API SHALL not expose a fake universal cursor-style capability when a terminal does not support the operation.

---

# 9. Version 0.9.0 — Synchronized Output

## Theme

Transactional synchronized terminal updates.

## Architectural contribution

`0.9.0` is the closure release for this sequence because synchronized output combines several previously established concepts:

- protocol capability uncertainty;
- CSI private-mode emission;
- begin/end state;
- nesting;
- session output serialization;
- flush boundaries;
- deterministic cleanup;
- optional status/query integration;
- interaction with higher-level presentation code such as `Icod.DCurses`.

The tranche should establish:

- typed synchronized-output mode acquisition;
- DEC private mode 2026 behavior where targeted;
- nested/ref-counted lease semantics or an equally deterministic ownership model;
- guaranteed best-effort release during disposal and failure paths;
- explicit output-flush policy at begin/end boundaries;
- no accidental persistence of synchronized mode after ordinary session cleanup;
- capability/query handling, potentially including DECRQM-family support if required by the final contract;
- downstream `Icod.DCurses` acceptance demonstrating that full-screen refresh can use the feature without owning private escape sequences.

`0.9.0` SHALL NOT silently make synchronized output mandatory for ordinary terminal writes.

---

## 10. What This Gives 0.10.0

After `0.9.0`, `Icod.Terminal` should possess a substantially broader protocol substrate than it had at `0.3.0`:

- active query/response routing;
- bounded response ownership and deadlines;
- safe OSC emission;
- URI payload encoding;
- parameterized OSC operations;
- scoped operational state;
- security-sensitive payload controls;
- OSC response routing;
- CSI intermediate/private-mode output;
- DCS/status-query reuse;
- truthful state restoration;
- nested operational leases;
- output serialization and synchronization boundaries.

That base is intentionally useful to several possible `0.10.0+` directions without prematurely selecting one:

- CSI-u / Kitty-style keyboard negotiation;
- broader terminal feature negotiation;
- endpoint/transport expansion;
- generic but bounded protocol extensibility;
- modern shell-integration protocols;
- lifecycle/platform hardening;
- performance/fuzzing/security consolidation;
- public API regret audit and eventual 1.0 contract planning.

The `0.10.0` milestone SHOULD therefore be planned only after the `0.9.0` closure audit identifies which abstractions have actually proven reusable.

---

## 11. Roadmap Governance

Each release in this sequence SHOULD receive its own milestone roadmap before implementation begins.

That roadmap SHALL identify:

- exact protocol references and compatibility target(s);
- supported and deliberately unsupported forms;
- semantic public API proposal;
- internal reusable primitives introduced by the tranche;
- input/output ownership rules;
- injection/security policy;
- capability and query semantics;
- resource limits;
- cancellation/deadline behavior where applicable;
- cross-platform behavior;
- deterministic unit/fixture tests;
- package-only consumer validation;
- downstream acceptance consumers;
- stable release gate.

The protocol-closure sequence itself may be revised only when implementation evidence shows that a dependency must move earlier or later. Convenience alone is not sufficient reason to merge the releases back into one broad operational-protocol milestone.
