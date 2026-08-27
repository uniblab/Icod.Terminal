# T19 — Icod.DCurses Integration and Rich-Input Acceptance

**Project:** `Icod.Terminal`
**Development line:** `0.2.0`
**Development version:** `0.2.0-alpha.7`
**Tranche:** T19 — Icod.DCurses integration and rich-input acceptance
**Reference Terminal branch:** `0.2.0`
**Acceptance reference:** `Icod.DCurses` branch `0.1.0`
**DCurses reference commit:** `17a7bad9b7e8dc84b9bf5c5b2a2f1f45448447a0`
**Accepted Terminal behavior:** `Icod.Terminal 0.2.0-alpha.6`
**Status:** Acceptance record prepared; validation gate pending

---

## 1. Purpose

T19 is a consumer-acceptance tranche. It does not add another terminal protocol
or another input path.

Its purpose is to prove that the rich-input mechanisms implemented by T14-T18
compose cleanly at the next architectural layer:

```text
Icod.DCurses
      |
Icod.Terminal
      |
Icod.TermInfo
```

The acceptance evidence is taken from the active `Icod.DCurses 0.1.0`
development line after the DCurses rich-input and application-shaped acceptance
work.

## 2. Event-stream acceptance

DCurses consumes the ordinary Terminal session event stream.

`src/Integration/CursesSession.Input.Terminal.cs` delegates all three
`CursesSession.ReadEventAsync` forms to `TerminalSession.ReadEventAsync` and
maps the returned semantic input payloads.

The mapping covers:

- ordinary text;
- named and modified keys;
- normalized mouse events;
- focus events;
- bracketed-paste Begin/Data/End events;
- end-of-input.

Mouse coordinates remain normalized to zero-based terminal cells. Shift,
Control, and Alt modifiers are preserved through the translation layer.

There is no second DCurses read loop.

## 3. Protocol-ownership acceptance

`src/Integration/CursesSession.InputProtocols.Terminal.cs` delegates rich-input
protocol acquisition directly to:

```text
TerminalSession.AcquireInputProtocolsAsync
```

DCurses translates only the caller-facing options and controlled result. The
actual protocol enable/disable bytes, nesting semantics, rollback, disposal, and
lifecycle re-entry remain Terminal responsibilities.

DCurses therefore does not maintain:

- a mouse protocol emitter;
- a focus protocol emitter;
- a bracketed-paste protocol emitter;
- a parallel protocol reference counter;
- independent suspend/resume protocol state.

## 4. Automated acceptance evidence

`tests/Icod.DCurses.Tests/src/CursesRichInputAcceptanceTests.cs` provides the
non-interactive T19 consumer gate.

The test stream carries, in one ordinary `CursesSession.ReadEventAsync` path:

1. focus input;
2. bracketed-paste Begin/Data/End;
3. an SGR mouse event;
4. a traditional Control+Up key event.

The tests additionally prove that:

- zero-based mouse coordinates survive translation;
- protocol acquisition through DCurses reaches Terminal;
- disposing the enclosing curses session restores active mouse, focus, and paste
  protocol state even when the individual curses lease has not yet been
  disposed;
- capability absence remains a controlled unavailable result rather than
  triggering a private DCurses fallback implementation.

These tests operate on injected Terminal transports and do not require an
interactive CI terminal.

## 5. Live and application-shaped consumers

`Icod.DCurses.Input.Showcase` is the live rich-input inspection consumer. It
requests bracketed paste, focus reporting, and mouse reporting through the
Terminal-owned lease path and displays the semantic events returned by the
ordinary curses event stream.

The later DCurses T12 acceptance harnesses for `watch`, `slabtop`, and `top`
exercise the same Terminal-backed session while using retained screens, timed
event loops, lifecycle repaint, application input, multiple logical windows, and
prompt/cursor policy. Those harnesses provide additional evidence that the rich
input integration does not require a competing terminal substrate.

## 6. Deliberate absence of Terminal production changes

T19 requires no new Terminal public API and no production decoder change.

The behavior accepted by DCurses is the published `0.2.0-alpha.6` behavior from
T18. `0.2.0-alpha.7` records the successful consumer boundary and advances the
development line to the final T20 release gate.

This tranche also reconciles package-verifier and release-facing dependency
metadata with the `0.2.0` branch's current `Icod.TermInfo 1.2.0` package
reference. No historical `0.1.x` dependency statement is rewritten.

## 7. T19 gate

T19 is complete when:

1. the Terminal solution builds and tests for `net8.0`, `net9.0`, and
   `net10.0`;
2. package verification and fresh package consumers remain green on Windows,
   Linux, and macOS;
3. the DCurses `0.1.0` acceptance source continues to show one Terminal-backed
   event stream for text, key, mouse, focus, and paste input;
4. DCurses continues to delegate protocol acquisition to Terminal;
5. no private DCurses mouse parser, paste reader, focus parser, protocol emitter,
   or second input loop is introduced;
6. Terminal session disposal and lifecycle handling remain the ownership point
   for reversible rich-input protocol state.

After this gate passes, development proceeds to T20 — the `0.2.0` public API,
package, and release gate.
