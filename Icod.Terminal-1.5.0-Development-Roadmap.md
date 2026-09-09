# Icod.Terminal 1.5.0 Development Roadmap

**Release:** `1.5.0`  
**Theme:** semantic protocol normalization and control-language foundation  
**Status:** N150 in progress  
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

## N151 — generalized control-family framing

**Goal:** evolve the current response framing abstraction beyond CSI/DCS/OSC without changing existing response behavior.

### Work

- introduce common internal family vocabulary for CSI, DCS, OSC, APC, PM, and SOS;
- reconcile `TerminalResponseFrameKind` with the common family model;
- preserve recognized 7-bit and 8-bit introducers;
- centralize family introducer/terminator rules;
- preserve OSC BEL compatibility only where already required;
- add bounded APC/PM/SOS framing tests before any dialect uses those families.

### Acceptance

Existing CSI/DCS/OSC query tests remain byte-for-byte green while the family layer can identify APC/PM/SOS safely.

## N152 — one incremental control-language state machine

**Goal:** prevent separate protocol implementations from creating duplicate ad-hoc scanners.

### Work

- define a bounded state machine for Ground/Escape plus CSI, DCS, OSC, APC, PM, and SOS family states;
- ensure split introducers and split ST terminators are handled incrementally;
- preserve ordinary application input when a sequence is not claimed by an active query/event dialect;
- add mixed-stream and malformed-frame recovery tests;
- keep dialect parsing outside the framing state machine.

### Acceptance

No individual Sixel, Kitty Graphics, OSC, or CSI implementation requires its own competing transport scanner.

## N153 — structural CSI/DCS/string frame model

**Goal:** preserve enough syntax for complete future dialects without lossy early parsing.

### Work

- represent CSI parameter bytes, intermediate bytes, and final byte explicitly;
- preserve omitted/empty parameters, private prefixes, and colon subparameters;
- represent DCS header structure separately from application payload;
- represent OSC/APC/PM/SOS payload bytes without assuming a dialect grammar;
- add helpers for typed dialect parsers without exposing raw control construction publicly.

### Acceptance

The structural model can represent current queries plus future Sixel and Kitty Graphics without changing the framing layer.

## N154 — multi-family query transactions

**Goal:** allow one logical active query to correlate more than one possible control family or synchronization barrier.

### Work

- evolve one-frame-kind expectations into a bounded transaction abstraction;
- support accepted families, correlation predicates, completion predicates, and barrier responses;
- retain pre-emission/post-emission cancellation and late-response ownership;
- preserve the single authoritative reader;
- prove no unrelated frame is stolen from application input.

### Acceptance

A synthetic APC-query/CSI-barrier transaction can be represented without special-casing Kitty Graphics in the input reader.

## N155 — capability support and evidence model

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

### Work

- define composition/precedence rules;
- ensure known negative live evidence overrides weaker advertisement;
- ensure timeout remains Unknown unless the protocol defines a negative barrier/result;
- keep explicit caller preference outside the evidence model;
- define lifecycle/invalidation rules for live evidence.

## N156 — semantic backend registry

**Goal:** map semantic operations to reviewed candidate implementations.

Initial reconciliation targets include:

```text
DesktopNotification
CurrentLocation
ShellCurrentDirectoryMetadata
ClipboardWrite
CursorStyle
PaletteMutation
DynamicColor
SemanticPromptLifecycle
RasterGraphics
```

Candidate backends include existing explicit OSC operations, exact TermInfo capabilities where semantically equivalent, CSI/DCS implementations, and future Sixel/Kitty Graphics entries.

The registry is internal first. Public routing APIs are not frozen until registry behavior is proven.

## N157 — deterministic routing policy

**Goal:** select among candidate backends without conflating evidence and preference.

### Internal preference order

The default resolver should consider, in order:

1. endpoint availability/incompatibility;
2. explicit negative protocol evidence;
3. verified live backend;
4. exact TermInfo implementation;
5. advertised built-in/profile backend;
6. explicitly documented safe fallback;
7. Unknown/Unsupported result.

Explicit caller backend selection may bypass automatic preference but never bypass framing, validation, security, or endpoint requirements.

## N158 — existing protocol reconciliation

**Goal:** normalize what already exists before adding new families.

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

### TermInfo reconciliation

Audit exact semantic equivalents such as extended capabilities conventionally used for clipboard, cursor style, cursor color, mouse, and other modern functions. Prefer an exact selected TermInfo recipe where equivalence is proven, while retaining existing explicit public methods unchanged.

### Routing candidates

The first high-level semantic routing candidates are:

- desktop notifications;
- current-location compatibility;
- clipboard write;
- cursor style.

Any public auto-routing API requires separate compatibility/security review before baseline freeze.

## N159 — acceptance, package, and documentation closure

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

## Public API strategy

N150–N156 should remain internal wherever practical.

Public additions should occur only after the semantic registry and routing policy are proven. Existing wire-specific APIs remain valuable for advanced callers and for deterministic compatibility; they are not deprecated merely because a new routed semantic API is added later.

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
- N150 documentation and internal vocabulary implementation in progress;
- no existing public wire behavior intentionally changed.
