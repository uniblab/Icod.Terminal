# Icod.Terminal 0.7.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Predecessor:** `0.6.1` — OSC 8 hyperlinks plus dependency refresh to `Icod.TermInfo 1.10.0`  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 52 clipboard/selection operations, security policy, and bidirectional OSC response routing  
**Successor:** `0.8.0` — cursor-style control and CSI state integration

---

## 1. Release objective

`Icod.Terminal 0.7.0` SHALL add semantic OSC 52 clipboard/selection operations on top of the safe OSC framing, scoped-state ownership, and active transaction routing established by `0.4.0` through `0.6.x`.

OSC 52 is security-sensitive because it can move application data into a terminal-managed selection and, on terminals that support queries, may expose selection contents back to the application. The release must therefore treat clipboard writes and clipboard reads as distinct capabilities with distinct risk, policy, routing, and failure semantics.

The public API SHALL remain semantic. Callers should not assemble selector letters, base64 payloads, raw OSC frames, or response strings directly.

---

## 2. Continuity from the existing protocol substrate

The following established contracts remain authoritative:

- `TerminalSession` owns semantic live-terminal operations;
- OSC output uses canonical 7-bit `ESC ]` introduction and `ESC \\` ST termination;
- caller-controlled data cannot inject control-sequence terminators;
- output participates in session-owned serialization;
- query responses use the single session-owned input path rather than a private reader loop;
- cancellation before transmission must not produce partial protocol output;
- after transmission commits, cleanup and transaction ownership are not caller-truncated arbitrarily;
- support uncertainty remains explicit;
- successful writes prove emission, not terminal-side execution;
- direct writes through borrowed `session.Output` remain outside the semantic ordering guarantee;
- no generic public `SendOsc(...)`, `WriteEscape(...)`, or arbitrary OSC extension surface is introduced.

OSC 52 must extend these foundations without weakening them.

---

## 3. Security model

Clipboard writes and reads SHALL be treated separately.

### 3.1 Write operations

Writing caller-supplied data to a terminal selection is explicit application behavior. The library may expose a semantic write API once:

- selection targets are typed;
- payload bytes are bounded before base64 expansion;
- the final OSC frame is bounded before transmission;
- raw control characters cannot alter OSC framing;
- empty-payload semantics are frozen explicitly;
- no environment data is collected implicitly.

### 3.2 Read/query operations

Clipboard reads are more sensitive. They SHALL require explicit caller opt-in at the API invocation. No session-open option, terminal identity, environment variable, or terminal profile shall silently enable clipboard reads.

A read API SHALL:

- require an explicit selection target;
- use a bounded transaction timeout;
- use the session-owned input/router path;
- accept only a response correlated to the active OSC 52 expectation;
- apply a strict maximum encoded and decoded response size;
- reject malformed base64 deterministically;
- reject unrelated OSC responses without consuming them as the query result;
- expose timeout, cancellation, unsupported/unknown, malformed response, and transport failure distinctly where practical.

`Icod.Terminal` SHALL NOT automatically read the system clipboard during session open, capability discovery, disposal, or feature detection.

---

## 4. Selection representation

0.7 SHOULD expose typed selection targets rather than raw OSC 52 selector strings.

The milestone must freeze which conventional targets are supported, including at minimum the ordinary clipboard target and whether primary/secondary/select targets are in scope.

Unknown selector letters SHALL NOT be accepted through a public string pass-through API in 0.7.

A future generic protocol-extension model may revisit this after the protocol-closure sequence.

---

## 5. Base64 and resource limits

OSC 52 payload data SHALL be represented as bytes internally and encoded using standard base64 without line wrapping.

The milestone SHALL define separately:

- maximum decoded write payload bytes;
- maximum encoded outbound payload bytes;
- maximum accepted encoded response bytes;
- maximum decoded read result bytes;
- maximum complete OSC 52 frame bytes.

Limits must be checked before large allocations where practical. Base64 expansion must be calculated using checked arithmetic.

The protocol layer must never accept unbounded clipboard data merely because a terminal implementation permits it.

---

## 6. Query/response routing

OSC 52 is the first planned OSC family requiring bidirectional routing.

0.7 SHALL extend the existing response-routing substrate rather than adding another reader. The design must settle:

- OSC framing recognition on the shared decoder path;
- seven-bit OSC introducer and ST terminator handling;
- whether BEL-terminated inbound OSC is accepted for compatibility;
- maximum response frame size;
- correlation rules for selection target and query form;
- whether a response received after caller cancellation remains owned for a bounded late-response period;
- interaction with ordinary input and unrelated OSC traffic;
- how malformed or oversized candidate responses are released back to ordinary input handling, rejected, or consumed.

No unrelated OSC response may accidentally satisfy an OSC 52 query.

---

## 7. Capability and compatibility semantics

Terminal identity alone SHALL NOT be treated as proof of OSC 52 support.

The public surface should distinguish as far as evidence permits:

- operation rejected because the endpoint is not an interactive terminal;
- operation emitted with support unknown;
- query timed out;
- syntactically valid response indicating data;
- syntactically valid response indicating unsupported/empty state if the protocol form permits that distinction;
- malformed response;
- transport failure.

0.7 SHALL NOT promise universal clipboard behavior across terminal emulators merely because the OSC sequence is widely implemented.

---

## 8. Proposed tranche sequence

### T52 — OSC 52 contract and reference freeze

Deliverables:

- exact outbound write/query wire forms;
- typed selection-target policy;
- base64 policy;
- empty payload semantics;
- resource limits;
- read/write security model;
- endpoint/support semantics;
- inbound termination compatibility policy;
- cancellation, timeout, and late-response ownership rules;
- explicit non-goals.

**Gate T52:** no production OSC 52 implementation until write/query, selection, security, framing, and resource semantics are written down.

### T53 — selection and payload primitives

Deliverables:

- internal typed selection mapping;
- bounded base64 encoder;
- bounded base64 decoder;
- exact size calculation helpers;
- deterministic malformed-input rejection;
- exhaustive resource-boundary tests.

**Gate T53:** OSC 52 data can be converted to/from safe bounded payload components without terminal I/O.

### T54 — outbound OSC 52 writer

Deliverables:

- canonical write frame;
- canonical query frame;
- validation before output;
- complete-frame single-write semantics where practical;
- no implicit flush unless explicitly justified;
- redirected-output and cancellation-before-commit behavior.

**Gate T54:** byte-exact outbound OSC 52 frames are proven and invalid input produces zero protocol output.

### T55 — inbound OSC framing and routing

Deliverables:

- bounded OSC candidate framing on the shared input path;
- ST and any explicitly accepted compatibility terminator handling;
- active expectation registration for OSC 52;
- unrelated OSC preservation/rejection policy;
- no second reader loop;
- fragmented-response tests across reads.

**Gate T55:** a bounded OSC 52 response can be routed through the existing session input coordinator without stealing ordinary input or unrelated response traffic.

### T56 — semantic clipboard write API

Deliverables:

- public typed write operation;
- explicit target selection;
- byte/string representation decision;
- support/endpoint semantics;
- ordering with text, titles, OSC 7, OSC 8, queries, and lifecycle transitions;
- no automatic clipboard mutation.

**Gate T56:** ordinary callers can write bounded clipboard data without knowing OSC 52 syntax or base64 framing.

### T57 — explicit clipboard read/query API

Deliverables:

- public read operation requiring explicit invocation;
- timeout/cancellation contract;
- late-response ownership;
- bounded response decode;
- malformed-response handling;
- support/unknown semantics;
- privacy/security documentation.

**Gate T57:** clipboard reads are explicit, bounded, correlated, and cannot occur implicitly.

### T58 — integration, security, and compatibility acceptance

Deliverables:

- injection matrix;
- encoded/decoded size-boundary tests;
- fragmented-response tests;
- cancellation/timeout/late-response tests;
- unrelated OSC traffic tests;
- ordering tests against all prior semantic output families;
- representative compatibility notes;
- fuzz/property testing where useful.

**Gate T58:** OSC 52 behavior is deterministic across Windows, Linux, and macOS and does not weaken existing input/output ownership guarantees.

### T59 — public API, docs, sample, package, and stable closure

Deliverables:

- public API regret audit;
- README and XML documentation;
- focused clipboard sample;
- package-only consumer smoke;
- package verifier updates;
- stable `0.7.0` metadata;
- final PR/main/tag validation gates.

**Gate T59:** the reviewed public surface and actual NuGet artifact are proven from a fresh package consumer before publication.

---

## 9. Explicit non-goals for 0.7

Unless implementation evidence requires otherwise, 0.7 SHALL NOT add:

- OS-native clipboard APIs;
- shelling out to `pbcopy`, `xclip`, `wl-copy`, PowerShell, or similar tools;
- generic public OSC framing APIs;
- automatic clipboard synchronization;
- background clipboard monitoring;
- clipboard history;
- arbitrary private OSC extensions;
- cursor-style control;
- synchronized output;
- `Icod.DCurses` clipboard UI policy.

OSC 52 is a terminal protocol feature, not a replacement for a general desktop clipboard library.

---

## 10. Stable release gate

`0.7.0` is ready for stable publication only when:

1. the T52 contract is frozen and implementation matches it;
2. all payload/frame limits are deterministic and tested;
3. read/query behavior is explicit and privacy-safe;
4. inbound OSC routing shares the existing single-reader architecture;
5. no unrelated OSC traffic can satisfy a clipboard query;
6. Windows, Linux, and macOS validation is green;
7. package-only consumers pass on `net8.0`, `net9.0`, and `net10.0`;
8. packaged XML documentation contains the reviewed 0.7 public delta;
9. `main` Release validation is green after merge;
10. only then is tag `v0.7.0` created for publication.
