# Icod.Terminal 0.4.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Repository:** `https://github.com/uniblab/Icod.Terminal`  
**Release line:** `0.4.0`  
**Predecessor:** `0.3.0` — released and published  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 0 / OSC 1 / OSC 2 title operations and safe outbound OSC framing  
**Successor roadmap:** `0.5.0` — OSC 7 current-working-directory publication

---

## 1. Release Objective

`Icod.Terminal 0.4.0` SHALL establish the first operational OSC output contract on top of the live-session and query/response foundation completed by `0.3.0`.

The release SHALL support semantic title operations corresponding to:

- **OSC 0** — set icon name and window title;
- **OSC 1** — set icon name;
- **OSC 2** — set window title.

The release SHALL use these deliberately narrow operations to establish reusable internal OSC framing, payload validation, encoding, output serialization, capability-policy, testing, and package-validation rules needed by later OSC 7, OSC 8, and OSC 52 work.

`0.4.0` is therefore not merely a convenience release for changing a terminal title. It is the first protocol-closure tranche after `0.3.0`.

---

## 2. Architectural Continuity from 0.3.0

The following `0.3.0` invariants remain authoritative:

- `TerminalSession` owns the live terminal conversation;
- there is one session-owned input path;
- active response routing remains expectation-driven and bounded;
- ordinary input remains separate from terminal responses;
- live observations do not mutate immutable `Icod.TermInfo` descriptions;
- cancellation and deadlines remain ordinary managed async concepts;
- protocol features do not create independent reader loops.

`0.4.0` primarily exercises the **output side** of the same live session.

---

## 3. Protocol Contract

### 3.1 OSC framing

The implementation SHALL centralize OSC frame construction internally.

The wire family is conceptually:

```text
OSC Ps ; Pt ST
```

where `Ps` is the OSC selector and `Pt` is protocol-specific text.

The implementation SHALL define one canonical emitted terminator policy for `Icod.Terminal` while remaining able to parse required reply terminators in later milestones where response routing is introduced.

The public API SHALL NOT expose raw OSC selector numbers merely as an escape hatch.

### 3.2 Payload encoding

Title text SHALL be encoded deterministically as terminal output text according to the session's established output policy.

Payload validation SHALL prevent title text from injecting or prematurely terminating terminal-control sequences.

At minimum, the policy SHALL explicitly address:

- ESC;
- BEL;
- C0 control characters;
- OSC string termination;
- embedded NUL;
- invalid or unpaired text input where applicable;
- maximum payload size.

The chosen behavior for invalid payload content SHALL be explicit: reject, sanitize, or encode. Silent control-sequence injection is forbidden.

### 3.3 Semantic distinction among OSC 0, 1, and 2

The API SHALL not collapse all three operations into a single undocumented "set title" helper if doing so loses protocol intent.

The public contract should make it possible to express:

- set both icon name and window title;
- set icon name only;
- set window title only.

Exact public names remain provisional until the API tranche.

### 3.4 Capability and support policy

OSC title support is not represented uniformly across all terminfo databases and terminal environments.

The release SHALL distinguish, where useful:

- operation not attempted because the endpoint is not suitable;
- support known unavailable;
- support unknown but emission permitted by caller policy;
- operation emitted successfully;
- output failure.

The library SHALL NOT infer proof of OSC title support from a `TERM` string alone.

---

## 4. Non-Goals

`0.4.0` SHALL NOT implement:

- OSC 7 current-working-directory publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard/selection;
- arbitrary public OSC emission;
- generic terminal protocol plug-ins;
- title-stack push/pop operations;
- title querying unless a dedicated design review determines it is required for a truthful public contract;
- terminal-emulator window management;
- cursor-style operations;
- synchronized output;
- CSI-u or Kitty keyboard protocols.

Those remain later roadmap concerns.

---

## 5. Proposed Tranches

### T29 — 0.4 contract and reference freeze

Freeze:

- exact OSC 0/1/2 wire references;
- canonical output terminator policy;
- payload encoding and forbidden-control policy;
- support/capability semantics;
- public API design constraints;
- resource limits;
- explicit non-goals.

**Gate T29:** every supported wire form and every rejection rule has a normative or deliberately chosen reference.

### T30 — Internal OSC writer foundation

Implement reusable internal primitives for:

- OSC introducer;
- numeric selector formatting;
- semicolon separation;
- text payload emission;
- canonical string termination;
- bounded payload-length accounting;
- single-write or serialized multi-write emission as required by the output abstraction.

The writer SHALL remain internal in `0.4.0`.

**Gate T30:** byte-exact fixtures prove valid framing and prove that forbidden input cannot inject a second terminal sequence.

### T31 — OSC 0 semantic operation

Implement the operation which sets both icon name and window title.

Tests SHALL cover:

- empty text where permitted;
- ASCII;
- multilingual UTF-8 text;
- boundary-size payloads;
- invalid/injection payloads;
- output failure;
- redirected/non-terminal endpoint policy.

**Gate T31:** byte-exact OSC 0 behavior is deterministic on all supported TFMs and hosts.

### T32 — OSC 1 semantic operation

Implement icon-name-only operation.

The implementation SHALL reuse the T30 writer and SHALL not fork protocol framing logic.

**Gate T32:** OSC 1 differs from OSC 0 only where the protocol selector/semantic contract requires it.

### T33 — OSC 2 semantic operation

Implement window-title-only operation.

The implementation SHALL reuse the common framing and payload rules.

**Gate T33:** OSC 2 output is byte-exact and independently testable.

### T34 — Session integration and output ordering

Integrate title operations with the session-owned output path.

Define:

- synchronization with ordinary session writes;
- behavior during disposal/closed-session state;
- cancellation behavior if an async API exists;
- flush policy;
- interaction with existing presentation leases;
- thread/concurrency policy.

**Gate T34:** concurrent test scenarios cannot interleave partial OSC frames with application output or other terminal-control frames.

### T35 — Public API and documentation audit

Freeze the intended `0.4.0` public surface.

Documentation SHALL explain:

- OSC 0/1/2 semantic differences;
- support uncertainty;
- payload safety policy;
- title state is terminal-owned, not an `Icod.Terminal` emulator model;
- absence of exact prior-title restoration unless the library has actually observed it;
- no arbitrary OSC API in this release.

**Gate T35:** no public type or method prematurely commits the later OSC 7/8/52 design.

### T36 — Package, consumer, and release closure

Extend package-only consumer validation with at least one title-operation smoke using injected/in-memory output.

Validate:

- `net8.0`, `net9.0`, `net10.0`;
- Windows, Linux, macOS;
- Debug local path;
- Staging pull-request path;
- Release default-branch path;
- exact package/symbol/Source Link structure;
- public API baseline;
- release notes;
- matching `<Version>` / `<PackageVersion>` for the stable release commit.

**Gate T36:** stable `0.4.0` is ready for a matching `v0.4.0` release tag only after all source, package, and consumer gates are green.

---

## 6. Testing Matrix

Every supported title operation SHOULD be tested across:

| Axis | Cases |
| --- | --- |
| Selector | OSC 0, OSC 1, OSC 2 |
| Text | empty, ASCII, UTF-8 multilingual, maximum allowed length |
| Safety | ESC, BEL, NUL, C0 controls, terminator injection attempts |
| Endpoint | interactive-capable fake, redirected fake, failing output |
| Concurrency | isolated operation, adjacent ordinary writes, competing control operations |
| Framework | net8.0, net9.0, net10.0 |
| Host | Windows, Linux, macOS |
| Configuration | Debug, Staging, Release |

Normal tests SHALL use injected/in-memory output and SHALL NOT change the CI runner's actual terminal title.

Optional manual/live integration tests MAY exercise real terminal title changes but SHALL remain outside the ordinary deterministic test suite.

---

## 7. API Direction

Exact names remain provisional, but the public API should favor semantic operations conceptually similar to:

```text
SetTitle(...)
SetWindowTitle(...)
SetIconName(...)
```

or another naming set which makes OSC 0/1/2 intent equally clear.

Avoid:

```text
SendOsc(0, ...)
WriteEscape("...")
```

as the primary public contract.

A future generalized protocol-extension API, if ever justified, belongs to a later milestone after OSC 7/8/52, cursor-style, and synchronized-output implementation evidence exists.

---

## 8. Completion Definition

`Icod.Terminal 0.4.0` is complete when:

1. OSC 0, 1, and 2 are represented by safe semantic operations;
2. one reusable internal OSC framing implementation serves all three;
3. payload injection and size behavior are explicit and tested;
4. session output ordering prevents frame interleaving;
5. support uncertainty is represented honestly;
6. no generic public OSC abstraction has been frozen prematurely;
7. package-only consumer validation covers the new feature;
8. all Windows/Linux/macOS and net8/net9/net10 gates are green;
9. the resulting internal OSC foundation is suitable for OSC 7 without redesign.

That final criterion is important: the purpose of `0.4.0` is not only title control. It is to make the simplest OSC family prove the transport and safety decisions which later milestones can reuse.
