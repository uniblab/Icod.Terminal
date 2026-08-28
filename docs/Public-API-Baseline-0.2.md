# Icod.Terminal 0.2 Public API Baseline

**Baseline line:** `0.2.x`
**Reviewed at:** `0.2.0-alpha.8`
**Predecessor:** [`Public-API-Baseline-0.1.md`](Public-API-Baseline-0.1.md)
**Purpose:** intentional source-level inventory of the public 0.2 rich-input delta

This document extends the 0.1 baseline. Unchanged 0.1 signatures remain part of
the 0.2 consumer contract and are not duplicated here.

The T20 regret review found no new public type or member that requires removal,
renaming, or signature change before stable `0.2.0`.

---

## 1. Rich input event families

`TerminalInputEventKind` adds:

```csharp
Mouse,
Focus,
Paste
```

`TerminalInputEvent` adds:

```csharp
public TerminalMouseEvent? Mouse { get; }
public TerminalFocusEvent? Focus { get; }
public TerminalPasteEvent? Paste { get; }
```

The existing `Text`, `Key`, and `EndOfInput` forms remain unchanged.

### Mouse

```csharp
public enum TerminalMouseAction {
    Press,
    Release,
    Move,
    WheelUp,
    WheelDown,
    WheelLeft,
    WheelRight
}

public enum TerminalMouseButton {
    None,
    Primary,
    Middle,
    Secondary,
    Button4,
    Button5,
    Button6,
    Button7
}

public sealed class TerminalMouseEvent {
    public TerminalMouseEvent(
        TerminalMouseAction action,
        TerminalMouseButton button,
        int column,
        int row,
        TerminalKeyModifiers modifiers = TerminalKeyModifiers.None
    );

    public TerminalMouseAction Action { get; }
    public TerminalMouseButton Button { get; }
    public int Column { get; }
    public int Row { get; }
    public TerminalKeyModifiers Modifiers { get; }
}
```

Mouse coordinates are always zero-based terminal-cell coordinates.

### Focus

```csharp
public enum TerminalFocusState {
    Focused,
    Unfocused
}

public sealed class TerminalFocusEvent {
    public TerminalFocusEvent( TerminalFocusState state );

    public TerminalFocusState State { get; }
}
```

### Bracketed paste

```csharp
public enum TerminalPastePhase {
    Begin,
    Data,
    End
}

public sealed class TerminalPasteEvent {
    public TerminalPasteEvent(
        TerminalPastePhase phase,
        string? text = null
    );

    public TerminalPastePhase Phase { get; }
    public string? Text { get; }
}
```

A paste operation is a frame: Begin, one or more bounded Data chunks when data
exists, then End. Data chunk boundaries are decoder/resource boundaries, not
semantic text boundaries. Applications that need one logical paste string must
assemble the Data chunks themselves.

---

## 2. Decoder policy

The public session policy adds:

```csharp
public sealed class TerminalInputDecoderOptions {
    public TimeSpan EscapeSequenceTimeout { get; init; }
    public int MaximumBufferedBytes { get; init; }
    public int PasteChunkBytes { get; init; }
}
```

and:

```csharp
public sealed class TerminalSessionOptions {
    public TerminalInputDecoderOptions InputDecoderOptions { get; init; }
}
```

The defaults preserve the bounded 0.1 Escape ambiguity and maximum undecoded
buffer behavior. `PasteChunkBytes` controls the target maximum raw bytes
represented by one Data event; the decoder may retain the small extra amount
needed to finish a UTF-8 scalar or exact paste terminator.

All policy remains per session. There is no process-global decoder setting.

---

## 3. Reversible rich-input protocol ownership

The public protocol request contract is:

```csharp
public enum TerminalMouseTrackingMode {
    ButtonEvents,
    ButtonMotion,
    AnyMotion
}

public sealed class TerminalInputProtocolOptions {
    public bool BracketedPaste { get; init; }
    public bool FocusReporting { get; init; }
    public TerminalMouseTrackingMode? MouseTrackingMode { get; init; }
}

public sealed class TerminalInputProtocolLease : IAsyncDisposable {
    public bool BracketedPaste { get; }
    public bool FocusReporting { get; }
    public TerminalMouseTrackingMode? MouseTrackingMode { get; }

    public ValueTask DisposeAsync();
}
```

`TerminalSession` adds:

```csharp
public ValueTask<TerminalControlResult<TerminalInputProtocolLease>>
    AcquireInputProtocolsAsync(
        TerminalInputProtocolOptions options,
        CancellationToken cancellationToken = default
    );
```

Acquisition is capability-driven and controlled. Capability absence is reported
through `TerminalControlResult<T>` rather than by installing a hard-coded
fallback protocol.

Leases may overlap. Focus and paste reporting remain active until the last
requesting lease is released. Mouse tracking follows the strongest active
request. Session lifecycle suspension temporarily removes active reporting and
resume re-enters desired reporting. Session disposal remains authoritative
cleanup.

---

## 4. Traditional keyboard completeness

No new public key enum is required for T18.

Traditional xterm/terminfo modified navigation, editing, and function-key
sequences normalize into the existing:

```csharp
TerminalKey
TerminalKeyModifiers
TerminalInputEvent.FunctionKeyNumber
```

contract.

Shift, Alt, and Control remain independent flags. Function keys continue to use
the base semantic function number plus modifiers rather than inventing separate
Shift-Fn/Control-Fn key identities.

---

## 5. Single-stream invariant

Mouse, focus, paste, ordinary text, named keys, modified keys, end-of-input,
timeouts, cancellation, and lifecycle events continue through the existing
`TerminalSession` event path.

The 0.2 baseline explicitly does not add:

- a second rich-input read loop;
- a public raw escape-frame event hierarchy;
- unbounded paste accumulation;
- terminal-emulator behavior;
- query/response routing;
- CSI-u/Kitty keyboard negotiation;
- PTY/ConPTY child-process hosting.

Those remain later or adjacent concerns.

---

## 6. Runtime dependencies

The stable 0.2 line is intended to retain:

```text
Icod.TermInfo 1.2.0
Icod.Timing   1.0.0
```

`Icod.DCurses` remains a downstream consumer and is not a runtime dependency of
`Icod.Terminal`.
