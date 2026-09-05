# T83 — Semantic Cursor-Style Set API

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.4`  
**Status:** Implemented; PR validation pending  
**Predecessor:** T82 — typed cursor-style codec and DECRQSS interpretation

## 1. Purpose

T83 exposes the first semantic public cursor-style operation:

```csharp
ValueTask SetCursorStyleAsync(
    TerminalCursorStyle style,
    CancellationToken cancellationToken = default
);
```

The public method remains semantic. Callers never supply a raw DECSCUSR numeric parameter, CSI intermediate byte, final byte, or escape string.

## 2. Mapping and output

`SetCursorStyleAsync(...)` delegates semantic mapping to `TerminalCursorStyleCodec` and emission to the structural `CsiWriter` introduced by T81.

The frozen mappings are:

```text
BlinkingBlock     -> CSI 1 SP q
SteadyBlock       -> CSI 2 SP q
BlinkingUnderline -> CSI 3 SP q
SteadyUnderline   -> CSI 4 SP q
BlinkingBar       -> CSI 5 SP q
SteadyBar         -> CSI 6 SP q
```

Only canonical seven-bit `ESC [` CSI framing is emitted.

## 3. Session ownership

The setter follows the same session-owned output discipline as existing semantic OSC operations:

1. validate the semantic style;
2. observe caller cancellation;
3. reject known redirected output;
4. acquire the shared session-output gate;
5. construct and emit one complete DECSCUSR frame;
6. release the gate.

This preserves deterministic ordering with ordinary application text and all other session-owned control output.

## 4. Failure and cancellation semantics

Invalid enum values fail before output with `ArgumentOutOfRangeException`.

Known redirected output fails before output with `InvalidOperationException`.

Caller cancellation before commit prevents all output. Once frame emission begins, the writer uses non-cancellable transmission so ordinary cancellation cannot deliberately truncate the CSI frame.

Transport failures propagate unchanged.

The setter does not add an implicit flush.

## 5. Support semantics

Successful completion proves only that the complete DECSCUSR frame was emitted.

It does not prove that:

- the terminal implements DECSCUSR;
- a DEC-core style was applied;
- an xterm bar style was applied;
- terminal policy permits cursor-style mutation.

No query is performed automatically before setting a style, no fallback style is substituted, and no retry occurs.

## 6. Cursor visibility independence

`SetCursorStyleAsync(...)` does not alter cursor visibility, acquire a presentation lease, or interact with DECTCEM state.

The stronger cross-feature regression matrix remains T86 work.

## 7. Tests

T83 adds focused session tests which prove:

- exact wire output for all six semantic styles;
- no implicit flush;
- invalid styles emit nothing;
- redirected output emits nothing;
- pre-cancelled operations emit nothing;
- transport failures propagate without a flush;
- cursor-style output composes in deterministic order with application text.

Broader ordering against OSC 0/1/2, OSC 7, OSC 8, OSC 52, active queries, and presentation-state transitions remains part of T86 integration acceptance.

## 8. Gate

T83 is complete when the public semantic setter is proven to emit only the frozen DECSCUSR forms through the shared session-output serialization path without introducing raw CSI surface area, implicit flushing, support fabrication, or cursor-visibility coupling.

The next tranche is **T84 — typed cursor-style query/observation API**.
