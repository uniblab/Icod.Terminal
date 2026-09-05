# T126 — OSC 133 Composition and Downstream Acceptance

**Release:** `0.12.0`  
**Tranche:** `T126`  
**Development version:** `0.12.0-alpha.7`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T126 proves that the public OSC 133 semantic-prompt operations introduced by T124 compose with the existing `TerminalSession` output domain and with downstream `Icod.DCurses` refresh output.

This tranche adds no new OSC 133 protocol surface and no command-region state.

---

## 2. Stateless semantic-output composition

The integration suite now interleaves OSC 133 markers with existing semantic output operations including:

- ordinary application text;
- OSC 0 title publication;
- OSC 7 current-location publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard writes;
- DECSCUSR cursor style;
- OSC 22 pointer shape.

The test asserts byte-level ordering through the single session-owned output serialization domain.

OSC 133 does not wrap, suppress, reinterpret, or implicitly flush any of these operations.

---

## 3. Managed-state composition

T126 proves marker emission while session-managed state is active:

- presentation leases;
- rich-input protocol leases;
- terminal-progress ownership;
- synchronized-output ownership.

OSC 133 remains orthogonal to those state owners:

- marker calls do not acquire or release their leases;
- marker calls do not alter their restoration policy;
- marker calls do not become lifecycle participants merely because another feature owns lifecycle state;
- all physical writes continue to serialize through the established output gate.

---

## 4. Progress and synchronized output

A focused composition test interleaves:

```text
prompt start
synchronized-output begin
command-input start
progress report
command-output start
command finish
progress clear
synchronized-output end
```

This proves OSC 133 markers may be emitted while DEC private mode 2026 is active and while OSC 9;4 progress ownership is active without introducing another output transport or hidden flush.

---

## 5. Active-query composition

T126 also proves an active terminal query may remain outstanding while semantic prompt markers are emitted.

The test:

1. emits prompt start;
2. starts a device-status query;
3. observes the query request;
4. emits command-input start while the query remains pending;
5. supplies the correlated terminal response;
6. verifies the query completes normally;
7. emits command-output start and command completion.

OSC 133 therefore composes with the existing query transaction manager and shared output ordering domain without claiming or consuming terminal responses.

---

## 6. Downstream `Icod.DCurses` acceptance

T126 adds:

```text
tools/dcurses-semantic-prompt-acceptance/
```

The acceptance executable references the current `Icod.Terminal` project and the published `Icod.DCurses` package.

It opens a `CursesSession` over a `TerminalSession` and emits semantic prompt markers around real `CursesSession.RefreshAsync()` activity using only the public methods:

```csharp
BeginPromptAsync
BeginCommandInputAsync
BeginCommandOutputAsync
FinishCommandAsync
AbortCommandAsync
```

The executable verifies that DCurses refresh payloads remain ordered between the expected OSC 133 frames and that successful completion with status `0` remains distinct from a later bare-D abort.

The downstream consumer never constructs A/B/C/D strings or raw OSC bytes.

---

## 7. Multi-target and CI gate

`packaging/VerifyDCursesSemanticPrompt.ps1` restores and runs the downstream acceptance executable for:

```text
net8.0
net9.0
net10.0
```

The gate is integrated into:

- pull-request validation;
- full distribution verification used by the main/release validation path.

This follows the existing synchronized-output, terminal-progress, and pointer-shape acceptance pattern.

---

## 8. Scope boundary

T126 intentionally does not add:

- a new public OSC 133 type;
- command-region ownership;
- lifecycle state;
- support probing;
- vendor metadata;
- a package-only OSC 133 smoke project.

Package-baseline, documentation, sample, release metadata, and fresh NuGet-only consumption remain T127 closure work.

---

## 9. T126 decision

OSC 133 semantic prompt markers now have explicit composition coverage across the principal `TerminalSession` output/state families and a downstream `Icod.DCurses` acceptance gate on all supported target frameworks.

The existing single output serialization domain is sufficient; no special OSC 133 composition mechanism is required.
