# Icod.Terminal 0.3.0 Development Roadmap

**Project:** `Icod.Terminal`
**Repository:** `https://github.com/uniblab/Icod.Terminal`
**Release line:** `0.3.0`
**Predecessor:** `0.2.0` — released publicly as `v0.2.0`
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`
**Language:** C# 13
**Runtime dependencies:** `Icod.TermInfo 1.3.0`; `Icod.Timing 1.0.0`
**Theme:** Active terminal query/response routing and probe foundation
**Stable contract target:** `1.0.0`
**Current development version:** `0.3.0`
**Status:** T27 and T28A accepted; T28B stable release commit prepared
**Current tranche:** T28B — Stable Release Closure

---

## 1. Release Objective

`Icod.Terminal 0.3.0` SHALL add active terminal conversations to the live-session
foundation established by `0.1.0` and the rich-input path completed by `0.2.0`.

The release SHALL provide reusable mechanisms for:

- issuing bounded active terminal requests;
- recognizing corresponding terminal responses on the same input stream used
  for ordinary application input;
- correlating responses with the transaction which caused them;
- preserving unrelated keyboard, mouse, focus, paste, and lifecycle traffic;
- applying monotonic deadlines and normal asynchronous cancellation semantics;
- safely handling responses which arrive after a caller has stopped waiting;
- supporting CSI-based reports such as device attributes, device status, and
  cursor position;
- supporting DCS-based transactions such as DECRQSS and XTGETTCAP;
- providing a foundation for later operational and negotiated protocols without
  introducing protocol-specific reader loops.

The release SHALL NOT turn `Icod.Terminal` into a terminal emulator, PTY package,
or general-purpose protocol-extension framework.

---

## 2. Architectural Continuity from 0.2

The architectural boundaries established by `0.1.0` and `0.2.0` remain in force.

### 2.1 `Icod.TermInfo`

`Icod.TermInfo` remains the immutable terminal-capability authority.

Active query replies MAY report live facts which differ from static capability
data. Such replies are live session observations and SHALL NOT mutate the
session's `TerminalDescription` in place.

### 2.2 `Icod.Terminal`

`Icod.Terminal` owns the live conversation with the terminal:

- query emission;
- response framing;
- response correlation;
- deadlines;
- cancellation behavior;
- late-response ownership;
- interaction with ordinary input;
- lifecycle cleanup;
- bounded resource policy.

### 2.3 `Icod.DCurses`

`Icod.DCurses` remains a presentation consumer.

It MAY use query results where a later presentation feature needs them, but it
SHALL NOT add a private response reader, private CPR parser, or competing
terminal query loop.

### 2.4 `Icod.Pty`

PTY/ConPTY creation and child-process hosting remain outside the runtime
contract.

---

## 3. Core 0.3 Design Rules

### 3.1 Public queries use normal async/await

The public query API SHALL use ordinary .NET asynchronous methods.

Callers SHALL NOT be required to create reader threads, register callbacks,
poll a router, or perform cleanup after cancellation.

Caller cancellation SHALL use normal .NET cancellation semantics. A caller
deadline SHALL remain distinguishable from caller cancellation.

For the 0.3 line:

- cancellation requested through a supplied `CancellationToken` SHALL complete
  the query operation as cancelled and surface through
  `OperationCanceledException` semantics;
- expiration of a caller-visible query timeout SHALL surface as
  `TimeoutException`;
- a protocol-defined negative reply is a terminal response and SHALL be
  represented by that protocol's typed result rather than by cancellation or
  timeout.

The concrete query tranches SHALL determine the most useful typed return values.
T21 does not introduce an abstract public `TerminalQueryResult<T>` merely to
model internal routing state.

### 3.2 One terminal input reader

Exactly one session-owned subsystem SHALL consume terminal input bytes.

A query implementation SHALL NOT call `ITerminalInput.ReadAsync` independently
while `TerminalSession.ReadEventAsync` has a competing reader.

All response recognition SHALL be integrated with the existing incremental input
path or a single replacement/coordinator which preserves the same ownership
invariant.

### 3.3 Response interpretation is expectation-driven

A byte sequence which can represent both application input and a terminal
response SHALL be interpreted as a response only while a compatible transaction
is outstanding.

This is particularly important for cursor-position reports and other CSI forms
which can overlap syntactically with traditional keyboard input.

When no matching transaction is outstanding, existing `0.2.x` input semantics
SHALL remain authoritative.

### 3.4 Ambiguity-sensitive transactions are serialized

Query families which do not carry a transaction identifier SHALL NOT have
multiple ambiguous requests active on the terminal wire at the same time.

Multiple callers MAY queue asynchronous requests, but the session SHALL
serialize issuance whenever responses cannot be correlated independently from
wire data.

The serialized ownership period includes bounded late-response cleanup required
after the caller stops waiting.

### 3.5 Cancellation and late responses

The following architectural contract is frozen for 0.3:

> Once query bytes have been emitted, caller cancellation does not revoke the
> transaction. The caller may stop waiting immediately, but the session retains
> bounded internal ownership of the outstanding response until it is consumed,
> expires, or the session is terminated. Query families lacking transaction
> identifiers remain serialized across this drain period so late responses
> cannot be misattributed to later requests or ordinary application input.

The caller-facing contract is:

> Terminal queries SHALL follow normal asynchronous caller semantics. Before a
> request is emitted, cancellation prevents transmission. Once a request has
> been emitted, caller cancellation or timeout does not retract the wire request.
> `Icod.Terminal` SHALL retain bounded internal ownership sufficient to recognize
> and discard a late response before permitting an ambiguous subsequent query.
> This cleanup is an implementation detail and SHALL NOT require a second input
> reader or additional work by the caller.

A cancelled or timed-out query SHALL NOT release an ambiguity-sensitive query
slot merely because the caller's await has completed. The slot is released only
when the outstanding wire transaction has been resolved, safely discarded,
expired under bounded policy, or terminated with the session.

### 3.6 All routing remains bounded

The query subsystem SHALL define explicit bounds for:

- incomplete CSI frames;
- incomplete DCS frames;
- numeric parameter count and magnitude;
- DCS payload length;
- queued/pending query count;
- application input retained by any demultiplexing queue;
- caller-visible response deadlines;
- internal late-response ownership;
- protocol-specific capability-name and value sizes.

The exact numeric defaults are implementation policy until proven by T22-T27
and are not frozen as public compatibility constants by T21. Every
implementation value SHALL nevertheless have a finite default and a finite hard
ceiling.

Malformed or hostile input SHALL NOT create unbounded buffering, matcher
retention, retry loops, or abandoned transaction state.

### 3.7 Ordinary application input remains live

A pending query SHALL NOT create a second application input path.

Keyboard, text, mouse, focus, paste, and lifecycle events which are not consumed
as the expected response SHALL remain available through the established
`TerminalSession` event model in deterministic order.

The implementation SHALL define bounded backpressure behavior rather than
silently dropping application input merely because a query is pending.

### 3.8 Probing is explicit

Opening a `TerminalSession` SHALL NOT automatically interrogate the terminal.

Active queries write protocol bytes and may have observable side effects on
unusual endpoints. Applications or higher-level libraries SHALL explicitly
request the probes they need.

A later convenience probe operation MAY compose typed 0.3 queries, but it SHALL
remain opt-in.

### 3.9 Session-owned control output is serialized

Query request bytes SHALL participate in session-owned serialization with other
library-generated control output.

A query frame SHALL NOT be interleaved byte-for-byte with presentation-lease,
input-protocol, or other session control transitions.

This rule does not require ordinary application output to become globally
serialized behind every terminal query unless implementation evidence shows that
a stronger rule is necessary.

### 3.10 Live query results do not replace terminfo

Protocol request bytes defined by a terminal conversation protocol are part of
that protocol implementation; they are not a second general-purpose terminal
capability database.

Where `Icod.TermInfo` already supplies the appropriate capability string,
`Icod.Terminal` SHOULD use it. Where a standardized active query is not modeled
as a terminfo capability, `Icod.Terminal` MAY implement that protocol request
directly.

XTGETTCAP replies and similar observations SHALL remain live query results and
SHALL NOT mutate the immutable `TerminalDescription`.

### 3.11 Public protocol extensibility remains deferred

Version `0.3.0` SHALL gain experience with several built-in protocols before
freezing a general public extension API.

Internal matcher/framing abstractions MAY be designed for later reuse, but a
public arbitrary-query registration mechanism remains deferred to the planned
protocol-extensibility milestone unless concrete implementation evidence
requires an earlier reconsideration.

### 3.12 Lifecycle disruption does not preserve successful correlation

Session disposal terminates all queued and active query work.

Suspend/resume or another lifecycle disruption SHALL NOT cause a pre-disruption
response to be reported later as a successful current query result. T23 SHALL
provide bounded cleanup/re-entry behavior so stale pre-disruption replies cannot
become ordinary application input or satisfy a later ambiguous query.

---

## 4. Protocol Reference Hierarchy

Implementation and tests SHALL distinguish standardized control functions from
vendor/private extensions.

The initial authority hierarchy is:

1. **ECMA-48, Fifth Edition (June 1991)** for standardized CSI, DA, DSR, CPR,
   DCS, and related control-function syntax:
   `https://ecma-international.org/publications-and-standards/standards/ecma-48/`
2. **Digital VT420 documentation** for DEC-private protocol semantics such as
   DECRQSS/DECRPSS:
   `https://vt100.net/docs/vt420-uu/chapter9.html`
3. **XTerm Control Sequences** for xterm-family behavior, compatibility details,
   and xterm extensions such as XTGETTCAP:
   `https://invisible-island.net/xterm/ctlseqs/ctlseqs.html`
4. `Icod.TermInfo` for the immutable capability/profile data already modeled by
   the Icod terminal-description layer.

When an xterm extension differs from a standardized or documented DEC control,
the implementation SHALL identify that distinction rather than silently treating
"xterm" as a synonym for "terminal".

T22-T26 tests SHOULD preserve literal protocol fixtures from these references in
small, named test cases. Ordinary unit tests SHALL not require a live xterm,
VT-series terminal, or interactive CI runner.

---

## 5. Protocol Scope

### 5.1 CSI query family

The first active-query family SHOULD include:

- primary device attributes;
- secondary device attributes;
- device status reports required by the supported terminal set;
- cursor-position reports;
- DEC-private cursor/status variants where they materially improve supported
  terminal behavior.

The parser SHALL preserve private markers and distinguish response families
strictly enough that unrelated CSI input is not consumed accidentally.

### 5.2 DCS query family

Version `0.3.0` SHALL establish bounded DCS response framing for:

- DECRQSS;
- XTGETTCAP.

The implementation SHALL explicitly define its policy for 7-bit escape forms and
8-bit C1 forms rather than allowing host text decoding to decide accidentally.

### 5.3 Explicitly deferred beyond 0.3

The following remain outside the 0.3 release gate:

- terminal title operations;
- OSC 8 hyperlinks;
- OSC 52 clipboard/selection;
- synchronized output;
- cursor-style/color operational protocols;
- Kitty/CSI-u keyboard negotiation;
- graphics protocols;
- PTY/ConPTY creation;
- terminal emulation;
- general public protocol-extension registration.

Those remain later-roadmap concerns unless implementation evidence requires a
deliberate roadmap revision.

---

## 6. Development Version Plan

| Tranche | Planned version | Principal outcome |
| --- | --- | --- |
| T21 | `0.3.0-alpha.1` | Contract reset, cancellation/lifecycle rules, API direction |
| T22 | `0.3.0-alpha.2` | Bounded response framing and single-reader demultiplexing |
| T23 | `0.3.0-alpha.3` | Query manager, deadlines, serialization, late-response ownership |
| T24 | `0.3.0-alpha.4` | CSI device/status/cursor query family |
| T25 | `0.3.0-alpha.5` | DECRQSS and reusable DCS transaction support |
| T26 | `0.3.0-alpha.6` | XTGETTCAP live capability queries |
| T27 | `0.3.0-alpha.7` | Integration, explicit probe composition, consumer acceptance |
| T28A | `0.3.0-alpha.8` | API regret review and release-candidate package gate |
| T28B | `0.3.0` | Stable release closure and tag-controlled publication |

`<Version>` and `<PackageVersion>` SHALL advance together at each implementation
tranche. `AssemblyVersion` is `0.3.0.0` throughout the 0.3 line unless a
documented compatibility reason requires a deliberate change.

---

# 7. T21 — 0.3 Foundation and Contract Reset

**Status:** Complete in `0.3.0-alpha.1`.

T21 establishes the 0.3 contract before changing the input pipeline.

Completed work:

- reconciled the master roadmap and README with the public `0.2.0` release;
- advanced `<Version>` and `<PackageVersion>` to `0.3.0-alpha.1`;
- advanced `<AssemblyVersion>` to `0.3.0.0`;
- retained `net8.0`, `net9.0`, and `net10.0`;
- retained runtime dependencies `Icod.TermInfo 1.3.0` and `Icod.Timing 1.0.0`;
- froze the one-reader invariant;
- froze expectation-driven response interpretation;
- froze ambiguity-sensitive query serialization;
- froze the cancellation and late-response contracts in section 3.5;
- froze normal cancellation as `OperationCanceledException` semantics and
  caller-visible query timeout as `TimeoutException`;
- froze explicit probing rather than automatic interrogation at session open;
- froze bounded resource-policy requirements without prematurely exposing
  numeric constants as public API;
- froze the `Icod.TermInfo`/live-query ownership boundary;
- deferred public arbitrary protocol extensibility;
- established the protocol reference hierarchy in section 4.

T21 intentionally adds no concrete terminal query and no public arbitrary
response-matcher surface.

**Gate T21: complete.** The architecture is written strongly enough that T22 can
refactor the input path without inventing cancellation, correlation, timeout, or
probe policy during implementation.

**T21 completion record:**
[`docs/T21-0.3-Foundation-and-Contract-Reset.md`](docs/T21-0.3-Foundation-and-Contract-Reset.md).

---

# 8. T22 — Bounded Response Framing and Single-Reader Demultiplexing

**Status:** Complete in `0.3.0-alpha.2`.

T22 introduces the internal machinery needed to recognize expected terminal
responses without breaking the 0.2 input contract.

Completed work:

- retained exactly one underlying terminal input reader;
- introduce an internal response expectation/matcher abstraction;
- add bounded incremental CSI framing;
- add bounded incremental DCS framing sufficient for later DECRQSS and
  XTGETTCAP work;
- preserve arbitrary fragmentation across reads;
- preserve several frames/events arriving in one read;
- route expected response candidates before ambiguous keyboard fallback;
- leave unclaimed input on the normal terminal event path;
- preserve mouse, focus, bracketed paste, traditional modified keys, UTF-8, and
  lifecycle behavior;
- define bounded deferred-input/backpressure behavior while a response
  expectation is active;
- test syntactic collisions such as CPR-like bytes versus traditional key input;
- keep the new framing/matcher types internal.

T22 SHOULD use an internal synthetic-response harness rather than exposing
unfinished public query methods merely to test the router.

**Gate T22: complete.** Scripted input can interleave ordinary application
input, rich input, CSI response candidates, and DCS response candidates while
the existing decoder consumes only a frame accepted by the active expectation.
Unclaimed input remains on the established application event path, and the
session lifecycle path remains unchanged.

**T22 completion record:**
[`docs/T22-Response-Framing-and-Single-Reader-Demultiplexing.md`](docs/T22-Response-Framing-and-Single-Reader-Demultiplexing.md).

---

# 9. T23 — Query Transactions, Deadlines, and Late-Response Ownership

**Status:** Complete in `0.3.0-alpha.3`.

T23 turns the T22 framing layer into a reusable session-owned transaction
manager while keeping concrete terminal-query APIs internal until T24.

Completed work:

- queues bounded asynchronous query requests;
- serializes ambiguity-sensitive requests across their complete wire/drain
  lifetime;
- uses `Icod.Timing` monotonic time for caller deadlines and late-response
  ownership;
- prevents pre-transmission cancellation or timeout from emitting query bytes;
- separates caller completion from wire ownership after transmission;
- consumes matching late responses during bounded post-cancellation/post-timeout
  ownership;
- blocks later ambiguous transmission until prior ownership resolves or expires;
- preserves exactly one raw terminal reader through a demand-driven input
  coordinator;
- retains unrelated application input in a bounded ordered deferred queue;
- distinguishes expectation registration from arming so stale buffered input
  cannot satisfy a newly emitted query;
- serializes query emission with presentation and rich-input protocol control
  transitions;
- invalidates callers across suspend/re-entry without converting stale responses
  into later successful results;
- terminates transaction ownership during session disposal;
- covers cancellation, timeout, late-response, expiry, buffered-input, control
  output, suspension, disposal, and single-reader behavior in synthetic tests.

No public DA, DSR, CPR, DECRQSS, XTGETTCAP, or arbitrary matcher API is added by
T23.

**Gate T23: complete.** Cancellation and timeout races cannot cause a late
response to be returned as ordinary application input or attributed to a later
ambiguity-sensitive query while the bounded ownership contract remains active.

**T23 completion record:**
[`docs/T23-Query-Transactions-Deadlines-and-Late-Response-Ownership.md`](docs/T23-Query-Transactions-Deadlines-and-Late-Response-Ownership.md).

---

# 10. T24 — CSI Device, Status, and Cursor Queries

**Status:** Complete in `0.3.0-alpha.4`.

T24 adds the first concrete public active-query family on top of T22/T23.

Completed work:

- adds typed Primary Device Attributes queries;
- adds typed Secondary Device Attributes queries;
- adds standard ECMA-48 Device Status Report queries with status values 0-4
  preserved as protocol results;
- adds standard Cursor Position Report queries with explicitly one-based public
  row/column coordinates;
- emits conservative 7-bit CSI requests while accepting 7-bit and 8-bit CSI
  response introducers;
- strictly preserves CSI private markers and response-family final bytes;
- bounds numeric response parameters to 32 values and 1,000,000 per value;
- rejects empty, non-decimal, excessive, or semantically invalid correlated
  parameters deterministically with `FormatException`;
- keeps all response matcher/parser machinery internal rather than exposing raw
  CSI or arbitrary-query registration publicly;
- proves CPR-shaped modified function-key input remains ordinary input when no
  CPR transaction is active;
- proves text, key, focus, mouse, and bracketed-paste events remain ordered and
  deliverable while a CPR query is pending;
- retains the T23 single-reader, cancellation, timeout, late-response, lifecycle,
  and control-output contracts unchanged.

DEC-private DSR variants are not exposed merely for protocol completeness; the
initial public surface remains the common Primary DA, Secondary DA, standard DSR,
and standard CPR operations. Private variants can be added when a concrete
consumer justifies a stable typed contract.

**Gate T24: complete.** Common CSI queries work through normal async/await while
preserving the 0.2 input stream and the T23 cancellation/late-response contract.

**T24 completion record:** [`docs/T24-CSI-Query-Family.md`](docs/T24-CSI-Query-Family.md).

---

# 11. T25 — DECRQSS and DCS Transaction Support

**Status:** Complete in `0.3.0-alpha.5`.

T25 validates the router against a structurally different response family.

Completed work:

- adds `QueryStatusStringAsync` as a typed public DECRQSS operation;
- exposes a fixed `TerminalStatusStringKind` set instead of accepting arbitrary
  caller-supplied request/control bytes;
- emits conservative 7-bit DECRQSS requests;
- accepts both 7-bit and 8-bit DCS introducers and string terminators;
- parses positive `DCS 1 $ r ... ST` and negative `DCS 0 $ r ST` DECRPSS
  responses into `TerminalStatusStringResponse`;
- bounds request identifiers to 16 bytes and returned status strings to 1024
  bytes while retaining the T22 complete-frame ceiling;
- treats clearly correlated malformed DECRPSS frames as `FormatException`
  rather than leaking them into ordinary input;
- verifies returned positive status strings match the requested control
  function;
- rejects unrelated DCS response families at the matcher boundary;
- preserves ordinary application input while DECRQSS is pending;
- exercises fragmentation, 8-bit C1 framing, cancellation, timeout, late
  responses, disposal, and single-reader behavior through the DCS family;
- keeps parameterized xterm-specific DECRQSS extensions deferred until a typed
  consumer contract justifies them.

The public API therefore prevents callers from injecting arbitrary unframed
terminal control data through a nominal DECRQSS operation.

**Gate T25: complete.** A typed DECRQSS transaction demonstrates that the common
router is not CSI-specific and that DCS framing remains bounded under malformed
or hostile input.

**T25 completion record:** [`docs/T25-DECRQSS.md`](docs/T25-DECRQSS.md).

---

# 12. T26 — XTGETTCAP Live Capability Queries

**Status:** Complete in `0.3.0-alpha.6`.

T26 adds live terminal capability interrogation without weakening the
`Icod.TermInfo` boundary.

Completed work:

- adds `QueryLiveCapabilityAsync` as a typed single-name XTGETTCAP operation;
- adds immutable `TerminalCapabilityObservation` results with the requested name,
  typed support state, and exact decoded capability-value bytes;
- requires non-empty printable non-space ASCII capability names no longer than
  64 bytes;
- hex-encodes names before emission so caller punctuation cannot become DCS
  framing or separators;
- emits conservative 7-bit XTGETTCAP requests and accepts both 7-bit and 8-bit
  DCS/ST response forms;
- accepts uppercase and lowercase hexadecimal response digits;
- distinguishes positive byte-valued replies from typed negative/unsupported
  replies;
- bounds encoded names to 128 bytes and decoded returned values to 1024 bytes,
  while retaining the T22 complete-frame ceiling;
- rejects odd/non-hex fields, mismatched returned names, duplicate/multiple
  name-value pairs, malformed validity parameters, and oversized values with
  deterministic `FormatException`;
- intentionally uses one capability name per transaction rather than exposing
  xterm's multi-name partial-result semantics;
- keeps returned observations independent from immutable `TerminalDescription`
  and `Icod.TermInfo` capability data;
- preserves ordinary input delivery and the session single-reader invariant;
- exercises fragmentation, cancellation, timeout, late-response ownership, and
  disposal through the XTGETTCAP DCS family.

The single-name policy is deliberate for 0.3: current xterm stops processing a
multi-name request at the first invalid name, which would force a partial-result
contract. T28 may reconsider batching if a concrete consumer justifies that
additional public complexity.

**Gate T26: complete.** Callers can explicitly obtain bounded live XTGETTCAP
observations without mutating or replacing the session's `Icod.TermInfo`
description.

**T26 completion record:** [`docs/T26-XTGETTCAP.md`](docs/T26-XTGETTCAP.md).

---

# 13. T27 — Query Integration and Probe Acceptance

**Status:** Implementation complete in `0.3.0-alpha.7`; acceptance execution
pending.

T27 proves that the 0.3 query substrate works as part of a real interactive
session rather than only as isolated protocol parsers.

Implemented work:

- exercises CSI, DECRQSS, and XTGETTCAP queries while presentation and rich-input
  leases are active;
- exercises normal `ReadEventAsync` consumption concurrently with a pending
  query;
- exercises resize, suspend/resume, cancellation, timeout, bounded late-response
  ownership, and disposal in integrated session fixtures;
- adds `Icod.Terminal.Query.Sample` as an explicit interactive query/probe sample;
- proves injected session opening performs no automatic interrogation;
- deliberately adds no aggregate probe-composition API because explicit query
  composition has not demonstrated a stable aggregate policy worth freezing;
- extends package-only consumer validation through CPR, DECRQSS, and XTGETTCAP
  transactions whose responses are injected only after the request write;
- retains the single-reader, control-output serialization, and immutable
  `TerminalDescription` boundaries.

Acceptance evidence completed before T28B:

- the corrected alpha.8 candidate passed the repository matrix for `net8.0`,
  `net9.0`, and `net10.0` on Windows, Ubuntu, and macOS;
- the published `0.3.0-alpha.8` package was consumed by the DCurses Alpha-22
  downstream compatibility build with no second reader or private CSI/DCS
  parser required.

**Gate T27: complete.** The integrated query stack and downstream presentation
consumer both preserve the one-reader and ownership boundaries.

**T27 acceptance record:**
[`docs/T27-Query-Integration-and-Probe-Acceptance.md`](docs/T27-Query-Integration-and-Probe-Acceptance.md).

---

# 14. T28 — 0.3 API, Package, and Release Gate

T28 closes the release in two subtranches.

## 14.1 T28A — Release-Candidate Gate

**Status:** Complete in `0.3.0-alpha.8`.

T28A freezes the intended stable 0.3 query API without changing production
query behavior.

Completed work:

- reviews every public type and member added by 0.3 and finds no breaking
  correction required before stable release;
- publishes [`docs/Public-API-Baseline-0.3.md`](docs/Public-API-Baseline-0.3.md);
- documents caller cancellation, caller-visible timeout, bounded late-response
  ownership, the one-reader invariant, explicit probing, supported query
  families, and resource bounds;
- adds README examples for CSI, DECRQSS, and XTGETTCAP operations;
- extends the package verifier to require XML documentation entries for every
  public 0.3 query type and method while retaining Source Link, symbols, package
  contents, assembly identity, and exact dependency-closure checks;
- extends the isolated package-only consumer through Primary DA, Secondary DA,
  DSR, CPR, DECRQSS, and XTGETTCAP;
- adds a dedicated `0.3.0` branch Release workflow for Windows, Ubuntu, and
  macOS using `net8.0`, `net9.0`, and `net10.0`;
- adds no public aggregate probe type, public raw response event, public matcher,
  or caller-extensible protocol registration API.

Acceptance evidence:

- corrected alpha.8 `main` validation passed on Windows, Ubuntu, and macOS;
- the annotated `v0.3.0-alpha.8` release workflow passed and published the
  prerelease package, symbols, and checksums;
- package-only validation passed for `net8.0`, `net9.0`, and `net10.0`;
- DCurses Alpha-22 built and tested against published `Icod.Terminal
  0.3.0-alpha.8` and `Icod.TermInfo 1.3.0` without downstream terminal-reader
  or response-parser changes.

**Gate T28A: complete.** No unresolved public-API regret remains and the stable
release closure may proceed.

**T28A completion record:**
[`docs/T28A-0.3-Release-Candidate-Gate.md`](docs/T28A-0.3-Release-Candidate-Gate.md).

## 14.2 T28B — Stable Release Closure

**Status:** Stable release commit prepared; final `main` validation and
tag-controlled publication pending.

T28B promotes the accepted release candidate to stable `0.3.0` without changing
production behavior.

Completed in the release commit:

- set `<Version>` and `<PackageVersion>` to `0.3.0`;
- retain `<AssemblyVersion>0.3.0.0</AssemblyVersion>`;
- finalize stable package release notes;
- update README and milestone state for the stable 0.3 contract;
- add the T28B release-closure record.

Remaining publication steps:

- merge the stable release commit to `main`;
- require the ordinary `main` Release validation to pass;
- create the matching annotated `v0.3.0` tag only from that validated commit;
- publish NuGet.org, GitHub Packages, symbols, checksums, and GitHub Release only
  through the tag-controlled release workflow.

**Release gate `0.3.0`:** active terminal queries use one bounded session-owned
input path, ambiguous responses are expectation-driven and safely correlated,
caller cancellation follows ordinary async semantics without revoking emitted
wire transactions, late responses cannot contaminate later ambiguous queries
while bounded ownership remains active, CSI and DCS query families are
supported, and the package passes the complete three-host package-only gate.

**T28B completion record:**
[`docs/T28B-0.3.0-Release-Closure.md`](docs/T28B-0.3.0-Release-Closure.md).

---

## 15. Testing Strategy

### 15.1 Pure framing tests

Every response family SHALL be tested with:

- byte-by-byte fragmentation;
- arbitrary chunk boundaries;
- several frames in one read;
- incomplete prefixes at deadline;
- malformed terminators;
- oversized fields;
- numeric overflow attempts;
- cancellation;
- end-of-input.

### 15.2 Correlation and ambiguity tests

Tests SHALL deliberately interleave:

- ordinary text;
- Escape;
- traditional modified keys;
- CPR-like sequences;
- mouse reports;
- focus reports;
- bracketed paste;
- CSI responses;
- DCS responses.

A response matcher SHALL consume only data justified by the currently
outstanding transaction.

### 15.3 Transaction race tests

Tests SHALL cover cancellation and timeout:

- before queueing;
- while queued;
- immediately before write;
- after partial/complete write;
- before response;
- while response is fragmented;
- simultaneously with response completion;
- during late-response ownership;
- during session disposal;
- during suspend/resume.

### 15.4 Package-only consumer tests

The package-only consumer SHALL continue to use injected endpoints so the full
query contract can be validated identically on non-interactive CI runners.

---

## 16. Resource and Safety Constraints

The 0.3 router SHALL remain safe under malformed or malicious terminal input.

At minimum:

- all response frames are bounded;
- all pending-query structures are bounded;
- numeric parsing is checked;
- hexadecimal decoding is checked;
- no caller-provided query argument can inject an arbitrary unvalidated control
  frame;
- no cancelled query leaves an unbounded matcher alive;
- no timeout creates an unbounded late-response quarantine;
- no response parser can loop forever on an invalid prefix;
- no query implementation creates a second terminal reader.

---

## 17. Compatibility Policy

`0.3.0` remains pre-1.0, but existing `0.2.0` consumers SHOULD continue to
compile and behave as before unless a concrete defect requires correction.

Applications which never call a 0.3 query API SHOULD observe the same keyboard,
mouse, focus, paste, lifecycle, presentation-lease, and input-protocol behavior
as in 0.2.

The introduction of response routing SHALL NOT silently reinterpret ambiguous
input merely because the router exists. Response semantics become active only
when a compatible query transaction is outstanding.

---

## 18. Completion Definition

`Icod.Terminal 0.3.0` is complete when:

1. active queries use normal async/await;
2. exactly one session-owned input path consumes terminal bytes;
3. response interpretation is expectation-driven;
4. ambiguous query families are serialized safely;
5. pre-send cancellation emits nothing;
6. post-send cancellation or timeout releases the caller without revoking the
   emitted wire request;
7. bounded internal ownership prevents late responses from contaminating later
   ambiguous queries or ordinary input while that ownership remains active;
8. ordinary text, keyboard, mouse, focus, paste, and lifecycle events remain
   usable around pending queries;
9. CSI device/status/cursor queries are implemented;
10. DECRQSS is implemented over bounded DCS framing;
11. XTGETTCAP returns bounded live observations without mutating
    `TerminalDescription`;
12. session opening performs no automatic interrogation;
13. the public 0.3 API passes regret review;
14. package-only consumers pass for `net8.0`, `net9.0`, and `net10.0` on Windows,
    Ubuntu, and macOS;
15. stable publication occurs only through the matching `v0.3.0` tag-controlled
    release workflow.

At that point the project may proceed to `0.4.0`, whose theme remains
operational terminal protocols built on the common query/router/session
foundation.
