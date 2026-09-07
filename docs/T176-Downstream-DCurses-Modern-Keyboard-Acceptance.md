# T176 — Downstream Icod.DCurses Modern-Keyboard Acceptance

**Release line:** `0.17.0`  
**Tranche version:** `0.17.0-alpha.7`  
**Status:** Complete  
**Validation:** workflow #841

---

## 1. Purpose

T176 proves the 0.17 modern-keyboard contract through the real downstream `Icod.DCurses 0.1.0` integration surface rather than only through unit tests inside `Icod.Terminal`.

The acceptance project targets:

```text
net8.0
net9.0
net10.0
```

and references the working `Icod.Terminal` project together with the published `Icod.DCurses 0.1.0` package.

---

## 2. Acceptance scenario

The downstream consumer opens a real `TerminalSession`, acquires one compound rich-input lease containing:

- bracketed paste;
- focus reporting;
- SGR button-event mouse tracking;
- Kitty `AllKeys` keyboard reporting.

Before DCurses opens, the acceptance proves:

- Kitty support is negotiated through the public query path;
- exact Kitty `AllKeys` flags are pushed;
- paste/focus/mouse protocols are enabled through the same compound lease.

It then opens `CursesSession` with alternate-screen presentation enabled.

The required presentation choreography is observed as:

```text
Kitty pop
alternate-screen enter
Kitty AllKeys push
```

This proves the T175 screen-local ownership handoff through the real DCurses presentation layer.

---

## 3. Real refresh and input coexistence

While the curses session owns the alternate screen, the acceptance performs a real `CursesSession.RefreshAsync()` and requires actual terminal output.

It then feeds the same live `TerminalSession`:

- a canonical Kitty CSI-u repeat event with shifted/base-layout identities and associated text;
- a focus-in event;
- a bracketed-paste region.

The public event stream must preserve:

- `TerminalKeyEventPhase.Repeat`;
- Shift + Control modifiers;
- character `a`;
- shifted character `A`;
- base-layout character `q`;
- associated text `x`;
- focus state;
- paste begin/data/end semantics.

No internal decoder API, raw terminal writer, or side-channel event injection is used.

---

## 4. Ownership-transfer correction

The first acceptance implementation incorrectly expected the caller-held `TerminalInputProtocolLease` to perform final cleanup after `CursesSession.DisposeAsync()`.

That contradicted the public DCurses ownership contract: successful `CursesSession.OpenAsync(existingTerminalSession, ...)` transfers ownership of the supplied `TerminalSession` to the returned curses session. Disposing DCurses therefore also disposes that terminal session and releases its owned input protocols.

The corrected acceptance now requires `CursesSession.DisposeAsync()` itself to perform both presentation restoration and final rich-input baseline restoration.

Observed cleanup includes:

```text
Kitty pop
alternate-screen exit
Kitty AllKeys push
Kitty pop
mouse tracking disable
SGR mouse encoding disable
focus disable
bracketed-paste disable
```

After this owner-driven cleanup, disposing the stale protocol lease must emit no additional output. This proves idempotent owner release.

---

## 5. CI integration

The new acceptance is wired into:

- pull-request Staging validation on Windows, Linux, and macOS;
- `packaging/VerifyDistribution.ps1`, and therefore the repository Release/distribution path.

Every invocation runs the downstream acceptance independently on net8.0, net9.0, and net10.0.

---

## 6. Completion gate

T176 is complete at workflow #841.

That exact-head validation proved:

- the full solution builds/tests cleanly;
- retained downstream acceptance remains green;
- the new modern-keyboard DCurses acceptance passes;
- screen-local Kitty ownership survives real DCurses full-screen entry/exit;
- rich input and modern keyboard events coexist with real refresh traffic;
- DCurses/TerminalSession ownership transfer restores all composed state exactly once.

T177 may therefore proceed to public API, documentation, package-contract, and stable-release closure.
