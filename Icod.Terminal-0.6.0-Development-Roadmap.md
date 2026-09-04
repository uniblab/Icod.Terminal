# Icod.Terminal 0.6.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Predecessor:** `0.5.0` — OSC 7 current-location publication and deterministic `file:` URI policy  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 8 hyperlinks, structured OSC parameters, and scoped begin/end output state  
**Successor:** `0.7.0` — OSC 52 clipboard/selection operations

---

## 1. Release objective

`Icod.Terminal 0.6.0` SHALL add semantic OSC 8 hyperlink operations on top of the safe OSC framing, deterministic URI handling, and session-owned output ordering established by `0.4.0` and `0.5.0`.

OSC 8 has the logical wire form:

```text
OSC 8 ; params ; URI ST
```

and hyperlink termination uses an empty URI:

```text
OSC 8 ; ; ST
```

The release is intentionally about **hyperlink semantics**, not arbitrary OSC construction. Ordinary callers should not assemble OSC 8 frames, escape parameter values, or manage begin/end sequences manually unless using an explicitly low-level internal path.

Architecturally, 0.6 is the first milestone in the protocol-closure ladder that introduces paired operational output state. It therefore must settle ownership, nesting, failure, disposal, and interleaving semantics strongly enough to be reused by later scoped terminal operations.

---

## 2. Continuity from 0.4 and 0.5

The following established rules remain authoritative:

- `TerminalSession` owns semantic live-terminal operations;
- OSC output uses canonical 7-bit `ESC ]` introduction and `ESC \\` ST termination;
- user-controlled data cannot inject or terminate control sequences;
- UTF-8 behavior is deterministic;
- known redirected output rejects semantic OSC operations;
- successful output proves emission, not terminal-side recognition or application;
- terminal identity alone is not fabricated into proof of OSC support;
- protocol output participates in session-owned output serialization;
- direct writes through borrowed `session.Output` remain caller-synchronized;
- no generic public `SendOsc(...)` or `WriteEscape(...)` API is introduced;
- URI/path encoding proven by 0.5 should be reused where its contract fits, but OSC 8 must not be artificially limited to `file:` URIs because hyperlinks legitimately target broader URI schemes.

The last point is important: 0.5 established deterministic **file-location** URI construction, not a universal hyperlink URI contract. T44 must decide what URI forms OSC 8 accepts rather than silently routing arbitrary hyperlink input through `System.Uri` or the OSC 7 file encoder.

---

## 3. OSC 8 contract questions to freeze

Before production OSC 8 implementation, the milestone SHALL pin protocol references and explicitly decide:

- canonical outbound OSC 8 framing;
- URI input representation;
- accepted URI schemes and whether the library validates scheme syntax versus scheme policy;
- UTF-8 and percent-encoding ownership for hyperlink URIs;
- treatment of already-escaped URI text;
- URI length/resource limits;
- parameter grammar;
- support for the standardized/de-facto `id` parameter;
- parameter escaping/rejection rules;
- maximum identifier length;
- empty identifier semantics;
- whether unknown OSC 8 parameters are ever exposed publicly;
- begin/end operation semantics;
- scoped/lease API shape;
- nesting policy;
- interaction between nested hyperlinks with equal or different targets;
- whether ordinary text may be written while a hyperlink lease is active;
- output ordering and atomicity boundaries;
- behavior when a write fails between begin and end;
- disposal behavior with an active hyperlink;
- cancellation behavior during acquisition/release;
- redirected-output behavior;
- capability uncertainty and optimistic emission policy;
- separation from `Icod.DCurses` cell/presentation policy.

No implementation detail shall choose these contracts accidentally.

---

## 4. Security and injection boundary

OSC 8 contains two caller-controlled domains: parameter data and URI data. They require separate validation.

At minimum:

- ESC, BEL, C0, DEL, C1, and OSC/ST injection paths must be impossible;
- parameter delimiters such as `:` and `;` must be treated according to the pinned OSC 8 parameter grammar rather than concatenated blindly;
- malformed Unicode must be rejected before output;
- the complete begin/end frame must be validated before its first byte is written;
- resource limits must be enforced before transmission;
- hyperlink URI handling must not decode and then re-interpret attacker-controlled escape-looking text unexpectedly.

Fuzz/property-style testing SHOULD be considered for parameter and URI encoders once deterministic fixtures are established.

---

## 5. Scoped state model

OSC 8 is paired output state: a begin frame changes the interpretation of following terminal text until an end frame closes the hyperlink.

`0.6.0` SHOULD therefore provide a scoped semantic abstraction if it can satisfy deterministic ownership rules. A likely conceptual form is:

```csharp
await using TerminalHyperlinkLease hyperlink =
    await session.AcquireHyperlinkAsync( uri, options );

await session.WriteTextAsync( "linked text" );
```

The exact public names and types are **not frozen** by this roadmap.

The scoped abstraction must define:

- whether nested leases are supported;
- whether nesting is ref-counted for identical state or represented as a stack;
- whether inner disposal restores the outer hyperlink by re-emitting it;
- what happens if release output fails;
- how session disposal closes active hyperlink state;
- whether a lease may outlive ordinary application writes but never the session;
- whether direct borrowed-output writes are outside the lease ordering guarantee.

The library must not claim to restore terminal state that it cannot know. OSC 8 state created by `Icod.Terminal` may be tracked and restored within the library's own ownership domain; pre-existing hyperlink state not created by the session is unknown.

---

## 6. Proposed tranche sequence

### T44 — OSC 8 contract and reference freeze

Deliverables:

- pinned protocol references and compatibility notes;
- exact OSC 8 begin/end wire forms;
- URI acceptance/encoding policy;
- parameter grammar and `id` policy;
- resource limits;
- capability/endpoint semantics;
- scoped-state ownership and nesting contract;
- cancellation/failure/disposal policy;
- explicit non-goals.

**Gate T44:** no production OSC 8 implementation until URI, parameter, begin/end, nesting, and cleanup semantics are written down.

### T45 — reusable hyperlink URI and parameter encoding

Deliverables:

- deterministic hyperlink URI validation/encoding primitive;
- OSC 8 parameter encoder;
- optional identifier representation if justified by T44;
- strict Unicode/control validation;
- encoded-size enforcement;
- host-independent fixtures;
- reuse of 0.5 URI primitives only where semantics genuinely match.

**Gate T45:** caller-controlled URI and parameter data can be converted to deterministic safe payload components without terminal I/O.

### T46 — OSC 8 writer integration

Deliverables:

- canonical OSC 8 begin frame;
- canonical OSC 8 end frame;
- validation-before-write behavior;
- complete-frame single-write semantics where practical;
- cancellation-before-transmission behavior;
- no implicit flush unless T44 establishes a justified boundary;
- output-failure propagation.

**Gate T46:** byte-exact begin/end frames are proven and invalid inputs emit zero partial protocol output.

### T47 — TerminalSession semantic hyperlink API

Deliverables:

- semantic public hyperlink operation(s);
- known-redirected-output rejection;
- session-owned output ordering;
- emission-oriented support semantics;
- no raw selector/parameter-string API.

**Gate T47:** ordinary callers can emit hyperlink text/state without knowing OSC selector 8 or assembling frames.

### T48 — scoped hyperlink lease and nesting

Deliverables:

- deterministic begin/end ownership;
- nested lease policy;
- outer-state restoration if supported by the frozen contract;
- disposal cleanup;
- failure-path cleanup policy;
- interaction with ordinary `WriteTextAsync(...)`;
- tests for overlapping/nested acquisitions and out-of-order disposal attempts where applicable.

**Gate T48:** session-owned hyperlink state cannot be silently leaked by ordinary successful use or session disposal.

### T49 — integration, compatibility, and security acceptance

Deliverables:

- URI/parameter injection matrix;
- Unicode and resource-boundary tests;
- ordering tests against titles, OSC 7, queries, presentation transitions, and rich-input transitions;
- redirected/failing output tests;
- privacy/security documentation;
- representative terminal compatibility notes;
- optional fuzz/property tests if useful.

**Gate T49:** behavior is deterministic across Windows, Linux, and macOS and the OSC 8 surface does not weaken the established OSC injection boundary.

### T50 — public API, documentation, and sample audit

Deliverables:

- `Public-API-Baseline-0.6.md`;
- README update;
- focused hyperlink sample;
- clear scoped-state and support-semantics documentation;
- regret audit before stable release;
- explicit separation from `Icod.DCurses` hyperlink presentation policy.

**Gate T50:** the public surface does not prematurely expose generic OSC parameter extensibility or force future OSC 52 design into the hyperlink API.

### T51 — package, consumer, and stable-release closure

Deliverables:

- package-only OSC 8 consumer validation;
- packaged XML-documentation verification for the 0.6 public delta;
- `net8.0`, `net9.0`, and `net10.0` package execution;
- Windows/Linux/macOS Staging validation;
- Release package/symbol/Source Link verification;
- stable `0.6.0` metadata and release notes.

**Gate T51:** stable `0.6.0` is ready for `v0.6.0` only after source, package, documentation, consumer, and cleanup-state gates are green.

---

## 7. Testing matrix

The milestone SHOULD cover at least:

| Axis | Cases |
| --- | --- |
| URI | ASCII; Unicode; spaces; `%`; `#`; `?`; reserved delimiters; already-escaped-looking text; malformed Unicode |
| Scheme | accepted schemes; malformed scheme; relative reference if supported/rejected; empty URI |
| Parameters | omitted; identifier; boundary lengths; forbidden delimiters; control characters; malformed Unicode |
| Frame | begin; end; exact bytes; canonical ST; zero partial output on validation failure |
| State | single lease; nested same target; nested different target; inner release; session disposal |
| Endpoint | terminal fake; redirected fake; failing output |
| Ordering | adjacent text; OSC 0/1/2; OSC 7; active query/control-output transaction |
| Cancellation | before acquire; before begin transmission; release/disposal semantics |
| Resource | exact limits; one over limits; percent/UTF-8 expansion boundaries |
| Framework | net8.0; net9.0; net10.0 |
| Host | Windows; Linux; macOS |
| Configuration | Debug; Staging; Release |

Ordinary automated tests SHALL use injected terminal output. They SHALL NOT depend on the CI runner's terminal emulator or external network access.

---

## 8. Explicit non-goals for 0.6.0

`0.6.0` SHALL NOT add:

- OSC 52 clipboard/selection operations;
- arbitrary public OSC selectors;
- arbitrary public OSC parameter dictionaries merely for extensibility;
- automatic URL detection/linkification of application text;
- terminal-side hyperlink querying;
- shell-integration bundles;
- browser/network fetching or URI reachability checks;
- `Icod.DCurses` cell hyperlink storage/presentation policy unless separately required as downstream acceptance work;
- cursor-style control;
- synchronized output.

---

## 9. Completion definition

`Icod.Terminal 0.6.0` is complete when:

1. OSC 8 has a safe semantic public API;
2. hyperlink URI and parameter data are deterministic and injection-safe;
3. begin/end state ownership is explicit;
4. nested/scoped behavior is deterministic if exposed;
5. session disposal cannot silently leave library-owned hyperlink state open;
6. output ordering matches the session-owned protocol boundary established by prior releases;
7. package-only consumers exercise the stable API on all supported TFMs;
8. Windows/Linux/macOS validation is green;
9. the implementation creates reusable evidence for later scoped terminal-state work without prematurely exposing a generic protocol API.

The next planned milestone remains `0.7.0` — OSC 52 clipboard/selection operations and the security-sensitive/bidirectional OSC boundary.
