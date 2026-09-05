# Icod.Terminal 0.10.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.10.0`  
**Development version:** `0.10.0-alpha.1`  
**Predecessor:** `0.9.0` — synchronized output and nested transactional output state  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** safe semantic OSC 9 operations, centered on terminal progress/activity state  
**Status:** T100 contract/reference freeze in progress

---

## 1. Release objective

`Icod.Terminal 0.10.0` SHALL add a reviewed semantic subset of the OSC 9 extension family without exposing arbitrary host-control escape injection.

The principal feature is OSC 9;4 terminal progress state, including:

- determinate progress;
- indeterminate progress;
- normal/error/attention rendering state;
- caller-friendly completed/total reporting rather than forcing callers to compute wire percentages;
- scoped owned progress state;
- deterministic nesting and restoration among library-owned progress scopes;
- managed suspend/resume participation;
- cleanup on lease/session disposal;
- composition with existing output serialization and synchronized output.

The release MAY include additional OSC 9 operations only when they satisfy the same semantic, non-host-executing design standard.

---

## 2. OSC 9 family classification

OSC 9 is a vendor-extension namespace with subcommands of very different risk and portability characteristics. `0.10.0` SHALL classify each candidate before implementation.

### Primary in-scope operation

- **OSC 9;4 — terminal progress/activity state.**

This is the mandatory feature family for 0.10.0.

### Candidate compatibility operation

- **OSC 9;9 — current-working-directory publication.**

This overlaps semantically with existing OSC 7 location publication and therefore requires a compatibility decision rather than automatic addition.

### Explicitly excluded from 0.10.0 public API

Host-control or host-executing OSC 9 extensions such as commands which:

- sleep or block the host terminal;
- show native message boxes;
- wait for native key input outside the ordinary terminal input path;
- execute GUI macros;
- launch host processes;
- read or mutate host environment state;
- change emulator-global compatibility modes.

These are outside `Icod.Terminal`'s terminal-protocol abstraction and SHALL NOT be exposed merely because an emulator accepts them.

---

## 3. Architectural rules

0.10 reuses the established protocol architecture:

- semantic public APIs rather than arbitrary OSC construction;
- complete-frame construction before commit;
- caller cancellation observed before commit;
- committed control frames written non-cancellably;
- session-owned output serialization;
- interactive-output requirement for terminal-control operations;
- truthful optimistic support semantics;
- lifecycle participants for scoped terminal state;
- authoritative session-disposal cleanup;
- no second reader or output transport.

OSC 9 progress does not require `Icod.TermInfo` changes, a query parser, response routing, or capability-database changes.

---

## 4. T100 — OSC 9 contract and reference freeze

Freeze:

- authoritative OSC 9 references and supported-emulator evidence;
- exact OSC 9;4 wire grammar and terminator policy;
- semantic state names for wire states 0 through 4;
- completed/total to percentage conversion and rounding;
- validation rules;
- support/compatibility posture;
- scoped ownership and nesting semantics;
- suspend/resume/disposal behavior;
- failure and cancellation semantics;
- interaction with synchronized output;
- OSC 9;9 disposition;
- explicit OSC 9 non-goals.

**Gate T100:** no encoder or public API implementation begins until the progress ownership model and wire-state terminology are frozen.

---

## 5. T101 — byte-exact OSC 9;4 writer

Implement:

- specialized internal OSC 9;4 encoder/writer;
- canonical complete-frame output;
- determinate, indeterminate, attention/error, and clear frames;
- byte-exact tests;
- pre-commit cancellation tests;
- no flush unless frozen by T100;
- no generic OSC 9 public writer.

Expected development version: `0.10.0-alpha.2`.

---

## 6. T102 — progress value model and stage conversion

Implement the typed semantic value layer:

- terminal progress state enum/value type as frozen by T100;
- completed/total validation;
- integer-safe percentage conversion;
- deterministic rounding;
- boundary tests for 0%, 100%, uneven stage counts, and large integral counts;
- no floating-point dependency in public progress conversion.

Expected development version: `0.10.0-alpha.3`.

---

## 7. T103 — session progress manager and nesting

Implement session-owned logical progress state:

- nested progress owners;
- current logical value per owner;
- deterministic restoration of the previous library-owned state when an inner owner releases;
- outermost final release clears progress;
- identity/order rules frozen by T100;
- output serialization through the existing session control-output domain;
- failure-retained cleanup ownership.

Expected development version: `0.10.0-alpha.4`.

---

## 8. T104 — public progress lease/API

Add the reviewed semantic API, expected to include a scoped `TerminalProgressLease` with operations for:

- reporting completed/total progress;
- entering indeterminate state;
- changing semantic progress state;
- asynchronous disposal/clear.

Public API SHALL NOT expose raw OSC 9 subcommand numbers or require callers to compute escape strings.

Expected development version: `0.10.0-alpha.5`.

---

## 9. T105 — lifecycle, cancellation, failure, and disposal

Prove:

- managed suspend clears library-owned progress before giving control back;
- resume restores the current logical progress state if owners remain;
- releasing all owners while suspended prevents re-entry;
- session disposal clears progress best-effort;
- failed update/clear operations retain truthful cleanup responsibility;
- caller cancellation cannot truncate a committed OSC frame;
- late lease disposal after session disposal emits nothing.

Expected development version: `0.10.0-alpha.6`.

---

## 10. T106 — composition and downstream acceptance

Prove composition with:

- ordinary text writes;
- OSC 0/1/2 title operations;
- OSC 7 location publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard operations;
- cursor style;
- presentation state;
- synchronized output;
- active terminal queries;
- downstream `Icod.DCurses` refresh flow.

Add downstream acceptance showing a higher-level consumer can report stages and indeterminate progress without constructing OSC bytes.

Expected development version: `0.10.0-alpha.7`.

---

## 11. T107 — public API, docs, sample, package, and stable closure

Deliver:

- `docs/Public-API-Baseline-0.10.md`;
- root README update;
- focused terminal-progress sample;
- package XML-documentation assertions;
- fresh package-only consumer on net8.0/net9.0/net10.0;
- retained 0.8 and 0.9 package-contract gates;
- stable `0.10.0` metadata;
- PR/main/tag release gates.

Expected stable version: `0.10.0`.

---

## 12. Explicit non-goals

0.10 SHALL NOT add:

- public arbitrary OSC 9 construction;
- public generic OSC construction;
- host process launching through OSC 9;
- GUI macro execution;
- native message-box APIs;
- host environment-variable access through OSC;
- blocking/wait-key host commands;
- OSC 4 palette mutation as part of the OSC 9 feature family;
- background capability probing;
- a retained-mode progress renderer inside `Icod.Terminal`.

OSC 4 palette mutation remains orthogonal. `Icod.TermInfo` already describes standard color capabilities, including `initc`/`orig_colors`, but that work is not a prerequisite for OSC 9 progress.

---

## 13. Current development state

```text
VersionPrefix:   0.10.0
VersionSuffix:   alpha.1
Version:         0.10.0-alpha.1
PackageVersion:  0.10.0-alpha.1
AssemblyVersion: 0.10.0.0
```

**T100 contract/reference freeze is the current tranche.**
