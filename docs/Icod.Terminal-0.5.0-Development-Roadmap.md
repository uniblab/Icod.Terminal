# Icod.Terminal 0.5.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Predecessor:** `0.4.0` — OSC 0 / 1 / 2 title operations and safe outbound OSC framing  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 7 current-working-directory / current-location publication  
**Successor:** `0.6.0` — OSC 8 hyperlinks

---

## 1. Release objective

`Icod.Terminal 0.5.0` SHALL add a semantic OSC 7 current-location publication operation on top of the OSC framing, payload-safety, and session-output-ordering substrate proven by `0.4.0`.

The release is intentionally narrow. It is not a shell-integration framework and it does not expose a general-purpose public OSC API. Its architectural purpose is to establish reusable, deterministic URI payload policy before OSC 8 hyperlinks reuse URI handling in `0.6.0`.

Conceptually, OSC 7 publishes a URI identifying the application's current working directory/location:

```text
OSC 7 ; file://host/path ST
```

The public API SHALL express the semantic operation rather than requiring callers to assemble this wire representation.

---

## 2. Continuity from 0.4.0

The following `0.4.0` rules remain authoritative:

- `TerminalSession` owns semantic live-terminal operations;
- operational frames use session-owned output serialization;
- canonical outbound OSC framing uses 7-bit `ESC ]` and `ESC \\` ST;
- user-controlled payloads cannot inject or terminate terminal control sequences;
- UTF-8 output is deterministic;
- a known redirected output endpoint rejects semantic OSC operations;
- successful emission does not prove that the terminal applied or understood the operation;
- static terminal identity is not fabricated into proof of protocol support;
- direct writes through the borrowed `session.Output` remain caller-synchronized;
- no generic public `SendOsc(...)` or `WriteEscape(...)` surface is introduced.

`0.5.0` SHALL reuse those rules rather than create an OSC-7-specific parallel transport path.

---

## 3. OSC 7 semantic contract to freeze

Before production implementation, the milestone SHALL pin the exact compatibility references used for OSC 7 and record any differences among targeted terminal families.

The contract SHALL explicitly decide:

- canonical outbound wire form;
- whether only `file:` URIs are accepted/emitted in 0.5;
- hostname authority policy;
- empty-host versus explicit-host behavior;
- absolute-path requirement;
- POSIX path conversion;
- Windows drive-root conversion;
- UNC/network-path policy;
- percent-encoding policy;
- UTF-8 treatment before URI escaping;
- treatment of spaces, `#`, `?`, `%`, non-ASCII text, and reserved characters;
- treatment of dot segments and trailing separators;
- whether symlink/canonical-path resolution is ever performed implicitly;
- maximum encoded payload size;
- caller-supplied path versus process-current-directory convenience API;
- environment/privacy disclosure rules;
- endpoint and support-uncertainty semantics;
- cancellation and output-failure behavior.

No implementation tranche may silently choose these behaviors merely because `System.Uri` happens to produce a particular representation.

---

## 4. Privacy and authority rules

OSC 7 can disclose filesystem and host information to the terminal emulator. `0.5.0` SHALL therefore make publication explicit.

The library SHALL NOT automatically emit OSC 7:

- when a session opens;
- when the process changes directory;
- when a presentation lease is acquired;
- when an input protocol is enabled;
- during disposal/restoration.

A caller must deliberately invoke the semantic location-publication API.

A convenience operation that publishes `Environment.CurrentDirectory` MAY be provided, but it must remain an explicit caller action. The core encoder SHOULD also support caller-supplied absolute locations so applications can publish a logical working location without mutating process-global current-directory state.

Hostname disclosure SHALL be treated as an explicit policy decision. The implementation SHALL NOT opportunistically scrape shell/environment variables to invent an authority component.

---

## 5. Path and URI policy

The OSC 7 payload is a URI, not an arbitrary native path string.

The implementation SHOULD centralize conversion into an internal reusable location/URI encoder suitable for later OSC 8 reuse, while keeping the public 0.5 API semantic.

The encoder SHALL be tested independently from terminal I/O.

At minimum, deterministic fixtures SHALL cover:

- POSIX root and nested absolute paths;
- Windows drive roots and nested absolute paths;
- spaces;
- literal `%`;
- `#` and `?`;
- non-ASCII Unicode names;
- path segments requiring percent encoding;
- already escaped-looking input;
- trailing separators;
- invalid/relative paths;
- malformed Unicode;
- any supported UNC/network forms;
- the encoded-size boundary.

URI escaping SHALL be applied exactly once. Input which merely resembles an already escaped URI must not become an injection or double-decoding channel.

---

## 6. Proposed tranche sequence

### T37 — OSC 7 contract and reference freeze

Deliverables:

- pinned protocol references;
- compatibility notes for targeted terminal families;
- wire-form decision;
- file-URI/path policy;
- hostname/privacy policy;
- resource-limit decision;
- explicit non-goals.

**Gate T37:** no production OSC 7 code is added until the wire, URI, authority, privacy, and path contracts are written down.

### T38 — Reusable URI/location encoder

Deliverables:

- internal structured URI/location representation as needed;
- deterministic POSIX and Windows path conversion;
- UTF-8 percent encoding;
- single-escape rules;
- encoded-size enforcement;
- platform-independent fixture tests.

**Gate T38:** URI output is byte-deterministic and does not depend accidentally on the CI host OS or current culture.

### T39 — OSC 7 writer integration

Deliverables:

- OSC selector 7 framing through the established internal OSC substrate;
- validation-before-write behavior;
- complete-frame write semantics;
- cancellation-before-transmission behavior;
- no implicit flush.

**Gate T39:** exact OSC 7 bytes are proven with injected output and invalid payloads emit no partial frame.

### T40 — TerminalSession semantic current-location API

Deliverables:

- semantic public operation for a caller-supplied current location;
- optional explicit process-current-directory convenience operation if justified by the contract review;
- known-redirected-output rejection;
- session-owned output ordering;
- emission-oriented success semantics.

**Gate T40:** callers never need to know selector `7` or manually assemble a file URI for ordinary use.

### T41 — Cross-platform path and privacy acceptance

Deliverables:

- Windows/POSIX fixture matrix;
- hostname/authority fixtures;
- disclosure tests proving no implicit publication;
- concurrency/order tests with ordinary output and existing control operations;
- output-failure propagation;
- lifecycle/disposal tests.

**Gate T41:** behavior is deterministic across Windows, Linux, and macOS and no session lifecycle action emits OSC 7 without an explicit caller request.

### T42 — Public API, documentation, and sample audit

Deliverables:

- `Public-API-Baseline-0.5.md`;
- README update;
- dedicated or appropriately extended sample demonstrating OSC 7;
- documentation of privacy/disclosure semantics;
- documentation of URI/path normalization rules;
- regret audit before stable release.

**Gate T42:** no public API prematurely commits OSC 8 hyperlink parameters or general shell-integration/extensibility policy.

### T43 — Package, consumer, and stable-release closure

Deliverables:

- package-only OSC 7 consumer validation;
- packaged XML-documentation verification for the new public API;
- `net8.0`, `net9.0`, and `net10.0` package execution;
- Windows/Linux/macOS Staging validation;
- Release package/symbol/Source Link verification;
- stable `0.5.0` metadata and release notes.

**Gate T43:** stable `0.5.0` is ready for a matching `v0.5.0` tag only after source, package, documentation, and consumer gates are green.

---

## 7. Explicit non-goals for 0.5.0

`0.5.0` SHALL NOT add:

- OSC 8 hyperlinks;
- OSC 52 clipboard operations;
- arbitrary public OSC selectors;
- automatic shell prompt hooks;
- automatic current-directory monitoring;
- terminal-emulator-specific shell integration bundles;
- filesystem canonicalization or symlink resolution merely to publish OSC 7;
- title-stack/query features;
- cursor-style control;
- synchronized output.

Those concerns remain in later milestones or consumer policy.

---

## 8. Testing matrix

The milestone SHOULD cover:

| Axis | Cases |
| --- | --- |
| Native path | POSIX root/nested; Windows drive root/nested; supported UNC forms |
| URI text | spaces; `%`; `#`; `?`; Unicode; reserved characters |
| Validity | absolute; relative; empty; malformed Unicode; unsupported form |
| Authority | omitted/empty; explicit supported hostname; invalid hostname forms |
| Size | empty/minimal payload; exact maximum; one byte over maximum |
| Endpoint | terminal fake; redirected fake; failing output |
| Ordering | adjacent text; title operation; active control-output transaction |
| Privacy | no open-time emission; no dispose-time emission; explicit invocation only |
| Framework | net8.0; net9.0; net10.0 |
| Host | Windows; Linux; macOS |
| Configuration | Debug; Staging; Release |

Ordinary automated tests SHALL use injected output and SHALL NOT publish the CI runner's actual working directory to its host terminal.

---

## 9. Completion definition

`Icod.Terminal 0.5.0` is complete when:

1. OSC 7 is represented by a safe semantic current-location operation;
2. URI/path encoding is deterministic and reusable for later OSC 8 work;
3. POSIX and Windows path semantics are explicit and tested;
4. authority/hostname disclosure is explicit rather than accidental;
5. no lifecycle action publishes location implicitly;
6. session output ordering and endpoint policy match the 0.4 operational contract;
7. package-only consumers exercise the new API on all supported TFMs;
8. Windows/Linux/macOS validation is green;
9. the resulting URI foundation can support OSC 8 without redesigning basic escaping/path semantics.

The last criterion is the architectural reason for making OSC 7 its own release: `0.5.0` should prove URI semantics before `0.6.0` adds hyperlink parameters and scoped hyperlink state.
