# Icod.Terminal 0.11.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.11.0`  
**Development version:** `0.11.0-alpha.1`  
**Predecessor:** `0.10.0` — OSC 9;4 terminal progress ownership  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** semantic OSC 22 mouse-pointer shape control and ownership  
**Status:** T110 contract/reference freeze in progress

---

## 1. Release objective

`Icod.Terminal 0.11.0` SHALL add a reviewed semantic OSC 22 pointer-shape subsystem without exposing arbitrary OSC construction or terminal-specific raw pointer-name injection.

The release SHALL distinguish mouse-pointer shape from text-cursor shape. `TerminalCursorStyle` remains DECSCUSR text-cursor shape/blink policy; OSC 22 is a separate terminal mouse-pointer presentation feature.

The release SHALL provide:

- semantic mouse-pointer shape values;
- byte-exact OSC 22 set/reset framing;
- scoped session-owned pointer-shape leases;
- deterministic nested restoration among library-owned shapes;
- out-of-order-safe disposal where possible without violating the frozen precedence model;
- managed suspend/resume participation;
- invalidation and failure recovery;
- authoritative session-disposal reset;
- composition with existing terminal operations, synchronized output, and progress;
- optional explicit OSC 22 query support only when the protocol contract is sufficiently portable and bounded;
- downstream `Icod.DCurses` acceptance.

---

## 2. Protocol scope

### Core OSC 22 behavior

The portable core is pointer-shape set/reset. Canonical outbound framing SHALL be frozen in T110.

The public API SHALL use semantic values rather than arbitrary strings.

### Modern pointer-name vocabulary

Modern terminals increasingly accept CSS-compatible pointer names for OSC 22. T110 SHALL classify the portable semantic subset and map each public value to one canonical wire name.

### Query/stack extensions

Kitty-family OSC 22 extensions add stack and query behavior beyond the basic xterm-compatible setter/reset. These extensions SHALL be reviewed explicitly rather than assumed portable.

No automatic support query or terminal-emulator detection shall occur during ordinary acquisition.

---

## 3. Architectural rules

0.11 reuses the established terminal-control architecture:

- semantic public APIs;
- specialized internal OSC writers;
- complete-frame construction before commit;
- caller cancellation before commit;
- non-caller-cancellable committed control frames;
- session-owned output serialization;
- interactive-output requirement;
- truthful optimistic support semantics;
- lifecycle participants for scoped terminal state;
- retryable cleanup debt;
- authoritative session-disposal cleanup;
- no second output transport or generic OSC escape hatch.

OSC 22 does not require `Icod.TermInfo` database changes for its base semantic API.

---

## 4. T110 — OSC 22 contract and reference freeze

Freeze:

- authoritative xterm/Kitty/compatible-terminal references;
- exact base set/reset wire grammar and terminator policy;
- semantic public shape vocabulary;
- canonical wire-name mapping;
- default/reset semantics;
- ownership and precedence rules;
- nested and out-of-order disposal behavior;
- suspend/resume/disposal behavior;
- invalidation and failure semantics;
- interaction with synchronized output and progress;
- query/stack extension disposition;
- explicit non-goals.

**Gate T110:** no public pointer-shape API is implemented until the vocabulary and ownership model are frozen.

---

## 5. T111 — byte-exact OSC 22 writer

Implement:

- specialized internal OSC 22 encoder/writer;
- canonical semantic pointer-name emission;
- canonical terminal-default/reset emission;
- validation before emission;
- one complete non-cancellable committed write;
- no implicit flush;
- byte-exact and cancellation tests.

Expected development version: `0.11.0-alpha.2`.

---

## 6. T112 — semantic pointer-shape model

Implement the reviewed public/internal value layer:

- `TerminalPointerShape` or equivalent public semantic enum;
- exact canonical mapping to wire names;
- validation and exhaustive mapping tests;
- no public raw string constructor.

Expected development version: `0.11.0-alpha.3`.

---

## 7. T113 — session pointer-shape manager and nesting

Implement session-owned logical pointer state:

- logical pointer-shape owners;
- deterministic active-controller precedence;
- nested restoration of previous library-owned shape;
- final release resets pointer shape to terminal default;
- out-of-order-safe disposal according to T110;
- output serialization through the existing control-output domain;
- retryable cleanup debt.

Expected development version: `0.11.0-alpha.4`.

---

## 8. T114 — public pointer-shape lease/API

Add the reviewed semantic API, expected to include:

- explicit pointer-shape setter where appropriate;
- scoped `TerminalPointerShapeLease` acquisition;
- asynchronous disposal/reset;
- optional explicit query API only if frozen by T110.

Public API SHALL NOT expose raw OSC 22 strings or terminal-specific X11 cursor-name injection.

Expected development version: `0.11.0-alpha.5`.

---

## 9. T115 — lifecycle, invalidation, failure, disposal

Prove:

- managed suspend resets library-owned pointer shape before suspension;
- resume restores current logical pointer shape when owners remain;
- releasing all owners while suspended prevents re-entry;
- `TerminalSession.InvalidateState()` invalidates pointer physical-state assumptions;
- session disposal performs authoritative reset;
- failed set/reset retains truthful cleanup responsibility;
- caller cancellation cannot truncate a committed OSC frame;
- late lease disposal after session cleanup emits nothing.

Expected development version: `0.11.0-alpha.6`.

---

## 10. T116 — composition and downstream acceptance

Prove composition with:

- ordinary text writes;
- OSC 0/7/8/9;4/52 operations;
- cursor style;
- presentation state;
- synchronized output;
- active queries where applicable;
- downstream `Icod.DCurses` mouse/pointer workflows.

Add real downstream acceptance showing a higher-level consumer can change pointer shape without constructing OSC bytes.

Expected development version: `0.11.0-alpha.7`.

---

## 11. T117 — public API, docs, sample, package, stable closure

Deliver:

- `docs/Public-API-Baseline-0.11.md`;
- root README update;
- focused pointer-shape sample;
- package XML-documentation assertions;
- fresh package-only consumer on net8.0/net9.0/net10.0;
- retained 0.8/0.9/0.10 package-contract gates;
- stable `0.11.0` metadata;
- PR/main/tag release gates.

Expected stable version: `0.11.0`.

---

## 12. Explicit non-goals

0.11 SHALL NOT add:

- public arbitrary OSC construction;
- public arbitrary OSC 22 strings;
- executable/window/Dock icon mutation;
- DECSCUSR text-cursor changes as part of OSC 22;
- platform GUI pointer APIs outside the terminal protocol;
- automatic terminal-emulator detection;
- automatic capability probing during ordinary pointer-shape acquisition;
- application-side retained pointer rendering.

---

## 13. Current development state

```text
VersionPrefix:   0.11.0
VersionSuffix:   alpha.1
Version:         0.11.0-alpha.1
PackageVersion:  0.11.0-alpha.1
AssemblyVersion: 0.11.0.0
```

**T110 contract/reference freeze is the current tranche.**
