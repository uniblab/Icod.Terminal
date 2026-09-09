# Icod.Terminal 1.5.0 Development Roadmap

**Release:** `1.5.0`  
**Theme:** semantic protocol normalization and control-language foundation  
**Status:** N150–N158 complete; N159 acceptance/package/documentation closure in progress  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.4.0`

## Why this release exists

The 1.0–1.4 line accumulated a deliberately typed set of terminal protocols while preserving one live-session input path and explicit ownership/security semantics. The next planned work—complete CSI, APC, Sixel, and Kitty Graphics—would make protocol-family duplication expensive if the current OSC/CSI/DCS implementations remain independent special cases.

Version 1.5 therefore normalizes the architecture before adding another major wire family.

The release is intentionally infrastructure-heavy. It should add little or no new wire protocol and should not silently reroute existing public methods.

## Naming note

The repository already contains historical `T150`–`T157` documents for the `0.15.0` OSC 133 extended-metadata program.

To preserve unambiguous historical references, the 1.5 normalization tasks use the `N` prefix:

```text
N150 .. N159
```

`N150` is the 1.5 equivalent of the newly requested T150 terminology/layer-ownership tranche.

## Release invariants

Every 1.5 task is constrained by these rules:

1. Existing public 1.x methods retain their documented wire semantics.
2. One live `TerminalSession` owns one authoritative input reader.
3. `Icod.TermInfo` remains immutable/static capability authority.
4. `Icod.Terminal` owns live probing, query correlation, protocol routing, framing, and output commitment.
5. `Icod.DCurses` and other higher layers request semantic operations rather than OSC/CSI/DCS/APC codes.
6. Protocol number, control family, terminal brand, and semantic operation are separate concepts.
7. Caller routing preference is not capability evidence.
8. Query timeout is not automatically unsupported truth.
9. No generic raw public vendor/control-family writer is introduced as the normalization mechanism.
10. All new parser/router state is bounded and cancellation-aware.

## N150 — terminology and layer-ownership freeze

**Status:** complete.

**Goal:** establish the shared internal vocabulary used by the remainder of 1.5 and by later CSI/DCS/APC/graphics releases.

### Deliverables

- freeze the definitions of semantic operation, protocol backend, control family, support state, and evidence source;
- define internal strongly typed vocabulary for those concepts;
- document the difference between family framing and dialect parsing;
- document Sixel as DCS and Kitty Graphics as an APC dialect;
- document the TermInfo/Terminal/DCurses ownership boundary;
- preserve all existing public APIs and wire behavior;
- add invariant tests for enum uniqueness/completeness and representative family/backend classification;
- add the long-range 1.5→1.8 roadmap.

### Acceptance

N150 is complete when later tasks can discuss routing and framing without using ambiguous terms such as “OSC feature,” “Kitty support,” or “terminal supports graphics” as if those identified one wire protocol.

No public API addition is required by N150.

N150 passed the full PR Staging gate on head `03897bed254532140ed00d329723d49cd6ee6ba1`. The package candidate retained the 1.4 public API fingerprint exactly.

## N151 — generalized control-family framing

**Status:** complete.

**Goal:** evolve the current response framing abstraction beyond CSI/DCS/OSC without changing existing response behavior.

### Work

- introduce common internal family vocabulary for CSI, DCS, OSC, APC, PM, and SOS;
- reconcile `TerminalResponseFrameKind` with the common family model;
- preserve recognized 7-bit and 8-bit introducers;
- centralize family introducer/terminator rules;
- preserve OSC BEL compatibility only where already required;
- add bounded APC/PM/SOS framing tests before any dialect uses those families.

### Implemented design

`TerminalResponseFramer` accepts `TerminalControlFamily` directly. The compatibility `TerminalResponseFrameKind` adapter delegates to the normalized family parser and remains available to existing query matchers.

APC, PM, and SOS use strict string framing:

```text
7-bit introducer  -> ESC \\ terminator
8-bit introducer  -> 0x9C ST terminator
```

CAN (`0x18`) and SUB (`0x1A`) abort framing as invalid. BEL is not a terminator for APC/PM/SOS. OSC retains its existing BEL/ST compatibility rules unchanged, and DCS retains its released framing behavior unchanged.

### Acceptance

Existing CSI/DCS/OSC query tests remain byte-for-byte green while the family layer can identify APC/PM/SOS safely.

N151 adds dedicated regression coverage proving the legacy CSI/DCS/OSC adapter produces the same framing result as the normalized family overload, plus 7-bit/8-bit APC/PM/SOS completion, malformed termination, cancellation-byte, incomplete-escape, and maximum-frame behavior.

N151 passed the full PR Staging gate on exact head `9ae77d5b2d5870f69d8e1b561e813cb35d14f17c`.

## N152 — one incremental control-language state machine

**Status:** complete.

**Goal:** prevent separate protocol implementations from creating duplicate ad-hoc scanners.

### Work

- define a bounded state machine for Ground/Escape plus CSI, DCS, OSC, APC, PM, and SOS family states;
- ensure split introducers and split ST terminators are handled incrementally;
- preserve ordinary application input when a sequence is not claimed by an active query/event dialect;
- add mixed-stream and malformed-frame recovery tests;
- keep dialect parsing outside the framing state machine.

### Implemented design

`TerminalResponseFramer` now delegates byte-at-a-time control-family recognition to one reusable `TerminalControlSequenceScanner`. The scanner retains explicit state for Ground/Escape, CSI header progress, DCS header/payload progress, and string-family payload/ST progress.

The scanner is resettable, bounded, and unaware of OSC numbers, Sixel, Kitty Graphics, or any other dialect grammar.

### Acceptance

No individual Sixel, Kitty Graphics, OSC, or CSI implementation requires its own competing transport scanner.

N152 passed as part of the full Staging validation on head `97982751a4024793372a6becb9807b59692f579e`.

## N153 — structural CSI/DCS/string frame model

**Status:** complete.

**Goal:** preserve enough syntax for complete future dialects without lossy early parsing.

### Work

- represent CSI parameter bytes, intermediate bytes, and final byte explicitly;
- preserve omitted/empty parameters, private prefixes, and colon subparameters;
- represent DCS header structure separately from application payload;
- represent OSC/APC/PM/SOS payload bytes without assuming a dialect grammar;
- add helpers for typed dialect parsers without exposing raw control construction publicly.

### Implemented design

`TerminalControlFrameStructure` retains the normalized family, introducer form, raw parameter and intermediate regions, optional final selector, opaque payload, and exact terminator kind/length.

Existing CSI DA/DSR/CPR, DECRQSS, and XTGETTCAP parsing now consume the shared structural model rather than duplicating frame-layout parsing.

N154 subsequently extended routed frame-kind mapping so APC, PM, and SOS `TerminalResponseFrame` instances also flow through the same N153 structural parser.

### Acceptance

The structural model can represent current queries plus future Sixel and Kitty Graphics without changing the framing layer.

N153 passed as part of the full Staging validation on head `97982751a4024793372a6becb9807b59692f579e`.

## N154 — multi-family query transactions

**Status:** complete.

**Goal:** allow one logical active query to correlate more than one possible control family or synchronization barrier.

### Work

- evolve one-frame-kind expectations into a bounded transaction abstraction;
- support accepted families, correlation predicates, completion predicates, and barrier responses;
- retain pre-emission/post-emission cancellation and late-response ownership;
- preserve the single authoritative reader;
- prove no unrelated frame is stolen from application input.

### Implemented design

`TerminalQueryResponsePlan` supplies a bounded internal set of response rules. Each rule has one family-specific matcher, one reviewed frame bound, and a `Completion` or `Barrier` disposition. Version 1.5 permits at most six rules, at most one rule per family, and requires at least one completion rule.

Existing matcher-based `ExecuteQueryAsync(...)` calls are adapters over a one-rule completion plan. The new internal `ExecuteQueryTransactionAsync(...)` path uses the same `TerminalQueryTransactionManager`, ambiguity gate, output serialization, cancellation, timeout, suspension, disposal, and late-response ownership implementation.

The decoder tests the bounded set of accepted families through `TerminalResponseFramer`, then invokes only the matcher for the family actually framed. Rule frame bounds are always clamped to the decoder's configured maximum buffered bytes, preserving the historical small-buffer fallback contract.

The synthetic acceptance transaction uses an APC completion response and a CSI DA-style barrier response without any Kitty-specific branch in the input decoder. Ordinary application input remains live while the compound query is pending.

Permanent N154 contract: `docs/N154-Multi-Family-Query-Transactions.md`.

### Acceptance

A synthetic APC-query/CSI-barrier transaction can be represented without special-casing Kitty Graphics in the input reader.

N154 passed the full PR Staging gate on exact head `39c8345a5aeb937ca437c58fa3fecfde1660ce48`: Windows, Linux, macOS, package candidate, Foundation, Presentation, Semantic/Hardening, Stable 1.x release-line, and validated package artifact all succeeded.

## N155 — capability support and evidence model

**Status:** complete.

**Goal:** separate support state from the source of the claim.

### Support states

```text
Unavailable
Unsupported
Unknown
Advertised
Verified
```

### Evidence sources

```text
TermInfo
BuiltInProfile
LiveProbe
ProtocolResponse
```

### Implemented design

`TerminalCapabilitySubject` distinguishes evidence about a semantic operation from evidence about one concrete protocol backend. This permits exact TermInfo recipe evidence without falsely asserting a hard-coded wire backend.

`TerminalCapabilityEvidenceLedger` retains bounded latest evidence. Static evidence may be `Unknown` or `Advertised`; live evidence may be `Unknown`, `Unsupported`, or `Verified`. `Unavailable` is derived from endpoint state and cannot be stored as evidence.

Effective evidence resolution is:

```text
endpoint unavailable
    ↓
latest decisive live evidence
    ↓
TermInfo advertisement
    ↓
built-in profile advertisement
    ↓
latest inconclusive live observation
    ↓
Unknown
```

An inconclusive live observation does not erase stronger static or decisive live evidence. Live evidence is generation-scoped while selected TermInfo/profile evidence persists.

Permanent N155 contract: `docs/N155-Capability-Support-and-Evidence-Model.md`.

### Acceptance

N155 passed the full PR Staging gate on exact head `c1e84579421563e9bce7b1094b4f76734bb7c35b`.

## N156 — semantic backend registry

**Status:** complete.

**Goal:** map every reviewed semantic operation to explicit implementation candidates without encoding preference or capability truth in registry declaration order.

### Implemented design

`TerminalSemanticBackendRegistry` contains at least one unique candidate for every `TerminalSemanticOperation`. Representative mappings include:

```text
DesktopNotification
    OSC 9
    OSC 777
    OSC 99

CurrentLocation
    OSC 7
    OSC 9;9

ClipboardWrite
    TermInfo capability
    OSC 52

CursorStyle
    TermInfo capability
    DECSCUSR

RasterGraphics
    Sixel / DCS
    Kitty Graphics / APC
```

Mode-based operations have specific semantic backend identities (`CsiSynchronizedOutput`, `CsiMouseReporting`, `CsiFocusReporting`, and `CsiBracketedPaste`) rather than a generic DEC-private-mode bucket. `TermInfoCapability` remains outside the raw control-family taxonomy.

Permanent N156 contract: `docs/N156-Semantic-Backend-Registry.md`.

### Acceptance

The registry is internal, complete for the current semantic-operation vocabulary, and carries no implicit support or preference evidence. N156 is included in the fully green N157 checkpoint below.

## N157 — deterministic routing policy

**Status:** complete.

**Goal:** select among reviewed N156 candidates without conflating capability evidence, declaration order, and caller preference.

### Implemented design

Automatic routing evaluates:

1. endpoint availability;
2. verified live backend evidence;
3. exact TermInfo implementation evidence;
4. advertised backend evidence;
5. an explicitly reviewed safe fallback for otherwise unknown support;
6. aggregate `Unknown` or `Unsupported`.

Explicit internal backend selection is routing policy rather than evidence. It may select a reviewed `Unknown` candidate but cannot override `Unsupported` or `Unavailable`.

Preference tables are explicit. Examples include OSC 99 > OSC 777 > OSC 9 for notifications, Kitty Graphics > Sixel for raster graphics, and exact TermInfo recipes ahead of weaker advertised wire alternatives for clipboard write and cursor style.

Permanent N157 contract: `docs/N157-Deterministic-Semantic-Backend-Routing-Policy.md`.

### Acceptance

N157 passed the full PR Staging gate on exact head `1091a508181295ecbdf3bff591948e251c307dac`.

## N158 — existing protocol and TermInfo reconciliation

**Status:** complete.

**Goal:** normalize what already exists before adding new CSI/DCS/APC feature families.

### Mandatory audit

- titles;
- current location and Windows compatibility CWD;
- hyperlinks;
- clipboard;
- cursor style;
- synchronized output;
- progress;
- pointer shape;
- OSC 9/99/777 notifications;
- OSC 133/633/1337 shell metadata;
- palette/dynamic colors;
- keyboard reporting;
- bracketed paste/focus/mouse modes;
- DA/DSR/CPR;
- DECRQSS;
- XTGETTCAP.

### Implemented TermInfo reconciliation

Exact reviewed TermInfo implementation candidates are seeded for:

```text
ClipboardWrite   Ms
CursorStyle      Ss
PaletteColor     can_change_color + initc
```

Existing CSI semantic backends receive TermInfo `Advertised` evidence when complete metadata contracts exist:

```text
FocusReporting   fe + fd + kxIN + kxOUT
BracketedPaste   BE + BD + PS + PE
MouseReporting   XM + xm + recognized KeyMouse/kmous prefix
```

Partial overlaps are deliberately not promoted. In particular `Cs`/`Cr` represent text-cursor color only and therefore do not advertise the whole OSC 10–19 `DynamicColor` semantic family.

### Session integration

`TerminalSession` owns a lazily created N155 evidence ledger seeded from the selected immutable `TerminalDescription`. Internal semantic resolution combines that static evidence with later live evidence and N157 policy.

Successful correlated OSC 99 capability responses record `Osc99KittyNotification` as `Verified` with `ProtocolResponse` evidence. Timeout/cancellation records no negative conclusion.

The existing Kitty keyboard support probe now records concrete `CsiKittyKeyboard` evidence: Kitty flags are `Verified / ProtocolResponse`, a Primary-DA barrier without Kitty flags is `Unsupported / ProtocolResponse`, and timeout remains `Unknown / LiveProbe`.

`TerminalSession.InvalidateState()` advances the live-evidence generation. Explicit out-of-band invalidation and managed resume therefore discard stale live conclusions while preserving immutable TermInfo/profile evidence.

Permanent N158 contract: `docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md`.

### Acceptance

N158 passed the full PR Staging gate on exact head `50b30098ac81c9ad36ab3b9d4e0907efe3c883ad` in workflow run `34374645658`. Windows, Linux, macOS, package candidate, Foundation, Presentation, Semantic/Hardening, Stable 1.x release-line, and validated package artifact all succeeded. The generated public API remained identical to the frozen 1.4 baseline.

## N159 — acceptance, package, and documentation closure

**Status:** in progress.

### Required evidence

- Windows/Linux/macOS Staging runtime/source validation;
- net8.0/net9.0/net10.0 package/public-surface consistency;
- all retained historical package contracts;
- existing 1.0–1.4 explicit protocol behavior unchanged;
- framing fragmentation tests for every normalized family;
- malformed/oversized recovery tests;
- multi-family transaction tests;
- capability evidence precedence tests;
- deterministic backend routing tests;
- no second reader;
- no generic raw public control-family writer;
- DCurses acceptance showing semantic use without protocol-number knowledge;
- release notes, changelog, README, compatibility/security docs, and API baseline closure.

### Accepted evidence

N158 exact-head run `34374645658` supplies the runtime/package acceptance foundation for N159. It confirms:

- all three runtime operating-system lanes pass;
- all four package-contract shards pass;
- the validated package artifact is produced;
- `net8.0`, `net9.0`, and `net10.0` public snapshots are identical;
- the frozen 1.4 public API fingerprint remains `3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27`.

Because 1.5 introduces no public-surface delta, N159 intentionally retains `docs/Public-API-Baseline-1.4.md` / `.sha256` as the current machine baseline rather than creating a duplicate 1.5 baseline for identical API bytes.

The release-facing README, changelog, curated 1.5 release notes, package release notes, N158 permanent record, and N159 acceptance record are being synchronized on the closure head. The final documentation-complete head must pass the same PR matrix before N159 is marked complete.

Permanent N159 contract: `docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md`.

## Public API strategy

N150–N159 remain internal. Version 1.5 has not added a public automatic-routing API and therefore does not change the frozen 1.4 public surface.

Existing wire-specific APIs remain valuable for deterministic advanced use and are not deprecated merely because internal semantic routing now exists.

## Relationship to later releases

```text
1.5.0  normalization / family framing / broker
    |
1.6.0  complete CSI grammar / CSI consolidation / pixel geometry
    |
1.7.0  complete DCS foundation / Sixel / common raster API
    |
1.8.0  complete APC foundation / Kitty Graphics / graphics routing
```

The long-range contract is recorded in `docs/Control-Language-Normalization-and-Graphics-Roadmap.md`.

## Current implementation status

- branch/version authority established for `1.5.0`;
- long-range roadmap recorded;
- N150 terminology/layer-ownership foundation complete and fully green;
- N151 generalized family framing complete and fully green;
- N152 shared incremental control-language scanner complete and fully green;
- N153 structural CSI/DCS/string frame model complete and fully green;
- XTGETTCAP correlation strengthened so late responses are matched to the requested capability rather than merely the DCS `+r` family;
- N154 bounded multi-family transaction engine, synthetic APC/CSI acceptance, and structural string-family integration complete and fully green;
- N155 bounded semantic/backend capability evidence model complete and fully green;
- N156 semantic backend registry complete;
- N157 deterministic backend resolver complete and fully green;
- N158 TermInfo reconciliation, session evidence integration, OSC 99/Kitty-keyboard live evidence, and generation invalidation complete and fully green on `50b30098ac81c9ad36ab3b9d4e0907efe3c883ad`;
- N159 release-facing synchronization and final exact-head validation are in progress;
- no existing public wire behavior intentionally changed;
- frozen 1.4 public API fingerprint retained exactly.