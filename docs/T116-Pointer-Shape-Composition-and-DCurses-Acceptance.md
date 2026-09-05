# T116 — Pointer-Shape Composition and Icod.DCurses Acceptance

**Release:** `0.11.0`  
**Tranche:** `T116`  
**Development version:** `0.11.0-alpha.7`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T116 proves that OSC 22 pointer-shape ownership composes with the existing semantic terminal-control surface and with a real downstream `Icod.DCurses` consumer.

No new public API or ownership rule is introduced in this tranche.

---

## 2. Composition coverage

`TerminalPointerShapeCompositionTests` proves byte ordering with:

- ordinary text output;
- OSC 0 title updates;
- OSC 7 current-location publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard output;
- DECSCUSR text-cursor style;
- presentation leases including alternate-screen and cursor visibility;
- OSC 9;4 progress ownership;
- reversible input-protocol leases;
- synchronized output;
- active device-status queries.

The tests assert that pointer-shape leases do not hold the session output gate for their lifetime and that nested pointer ownership restores correctly around other semantic writes.

---

## 3. Synchronized-output composition

Pointer-shape operations remain ordinary serialized control writes inside a synchronized-output lease.

The library does not claim that a terminal visually applies or defers mouse-pointer changes according to synchronized-output mode. T116 proves only deterministic byte ordering and absence of output-gate deadlock.

---

## 4. Active-query composition

An active terminal query can complete while a pointer-shape lease remains owned.

Pointer ownership does not create a second reader, does not consume query replies, and does not interfere with the existing response router.

---

## 5. Real Icod.DCurses acceptance

T116 adds:

- `tools/dcurses-pointer-shape-acceptance/Icod.Terminal.DCursesPointerShapeAcceptance.csproj`;
- `tools/dcurses-pointer-shape-acceptance/Program.cs`;
- `packaging/VerifyDCursesPointerShape.ps1`.

The acceptance project references current `Icod.Terminal` source and published `Icod.DCurses 0.1.0`.

It performs real `CursesSession.RefreshAsync()` calls while:

1. an outer `Pointer` lease is active;
2. a nested `Wait` lease temporarily controls pointer shape;
3. disposal of the nested lease restores `Pointer`;
4. disposal of the outer lease emits terminal-policy reset.

The acceptance asserts that real DCurses refresh payloads occur between those OSC 22 transitions.

It intentionally does not assert DCurses flush counts or emulator-side visual behavior.

---

## 6. Framework and workflow coverage

The downstream acceptance runs on:

- `net8.0`;
- `net9.0`;
- `net10.0`.

The gate is wired into:

- pull-request validation on Windows, Linux, and macOS;
- distribution validation;
- tagged Release validation.

Existing synchronized-output and terminal-progress DCurses gates remain retained.

---

## 7. Decision

T116 establishes that semantic OSC 22 pointer ownership composes cleanly with the existing terminal protocol families and with real DCurses refresh behavior.

T117 may now focus on public API baseline, README/sample/package verification, and stable-release closure.
