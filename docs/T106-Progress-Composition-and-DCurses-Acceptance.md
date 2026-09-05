# T106 — Progress Composition and DCurses Acceptance

**Release:** `0.10.0`  
**Tranche:** `T106`  
**Development version:** `0.10.0-alpha.7`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T106 proves that the OSC 9;4 progress subsystem composes with the existing `Icod.Terminal` protocol surface and can be consumed by a real higher-level `Icod.DCurses` workflow without constructing raw OSC bytes.

---

## 2. Existing semantic-output composition

`TerminalProgressCompositionTests` proves progress ownership alongside:

- ordinary application text;
- OSC 0 title publication;
- OSC 7 location publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard writes;
- cursor style;
- presentation cursor visibility;
- synchronized output;
- active device-status queries.

The tests assert byte-exact ordering rather than only successful completion.

---

## 3. Synchronized-output composition

Progress and synchronized-output leases are orthogonal logical owners using the same session output serialization domain.

A representative sequence is:

```text
progress 10%
synchronized-output begin
progress 20%
application text
progress indeterminate
synchronized-output end
progress clear
```

`Icod.Terminal` guarantees deterministic byte ordering among its own operations. It does not guarantee how a particular terminal visually schedules tab/taskbar progress rendering while synchronized output is active.

---

## 4. Query composition

An active progress lease does not reserve the session output gate for its lifetime.

A device-status query may therefore execute while progress ownership remains active:

```text
progress 25%
DSR request
DSR response
progress 50%
progress clear
```

The existing query request flush semantics remain unchanged.

---

## 5. Real DCurses acceptance

T106 adds:

- `tools/dcurses-progress-acceptance/Icod.Terminal.DCursesProgressAcceptance.csproj`;
- `tools/dcurses-progress-acceptance/Program.cs`;
- `packaging/VerifyDCursesProgress.ps1`.

The executable references:

- the current `Icod.Terminal` source through a project reference;
- published `Icod.DCurses 0.1.0` through its NuGet package.

The acceptance flow retains the canonical `TerminalSession`, opens a real `CursesSession`, and uses only the public progress API:

```text
ReportAsync(1, 10)
DCurses RefreshAsync()
ReportAsync(2, 10)
SetIndeterminateAsync()
DCurses RefreshAsync()
ReportAsync(Attention, 7, 10)
DisposeAsync()
```

The executable verifies exact OSC 9;4 progress frames and proves real DCurses refresh payload exists between progress transitions.

No internal `OscWriter`, raw OSC construction, or DCurses source modification is used by the downstream consumer.

---

## 6. Multi-target acceptance

`VerifyDCursesProgress.ps1` restores the downstream project without cache and runs it under:

- `net8.0`;
- `net9.0`;
- `net10.0`.

---

## 7. CI and distribution integration

The progress acceptance gate now runs in:

- pull-request validation on Windows, Linux, and macOS;
- distribution verification;
- tagged release package validation.

It is additive to the existing DCurses synchronized-output acceptance rather than replacing it.

---

## 8. T106 decision

T106 adds no new public API beyond T104.

`0.10.0-alpha.7` is the cumulative integration candidate for the OSC 9;4 progress release.

After exact-head validation is green, T107 may perform public API/package/documentation/stable closure.
