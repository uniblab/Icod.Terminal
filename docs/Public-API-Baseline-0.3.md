# Icod.Terminal 0.3 Public API Baseline

**Baseline line:** `0.3.x`
**Reviewed at:** `0.3.0-alpha.8`
**Predecessor:** [`Public-API-Baseline-0.2.md`](Public-API-Baseline-0.2.md)
**Purpose:** intentional source-level inventory and regret review of the public
0.3 active-query delta

This document extends the 0.2 baseline. Unchanged 0.1/0.2 signatures remain part
of the consumer contract and are not duplicated here.

The T28A review finds no public 0.3 type or member that requires removal,
renaming, or signature change before stable `0.3.0`.

---

## 1. Review outcome

The 0.3 API remains deliberately protocol-specific and typed.

Accepted design choices:

- active queries are methods on `TerminalSession`, which already owns the live
  terminal conversation;
- every public query requires an explicit caller-visible `TimeSpan` timeout;
- cancellation uses the ordinary optional `CancellationToken`;
- protocol framing, matchers, response expectations, transaction queues, and
  late-response ownership remain internal;
- no public generic `TerminalQueryResult<T>` wrapper is introduced;
- no public raw CSI/DCS response event hierarchy is introduced;
- no public arbitrary protocol-registration or matcher-registration API is
  introduced;
- no aggregate "probe everything" object is introduced;
- session opening remains passive and never performs automatic interrogation;
- live query observations do not mutate the immutable `TerminalDescription`.

No API correction is required before stable 0.3.

---

## 2. CSI result contracts

```csharp
public enum TerminalDeviceStatus {
    Ready = 0,
    BusyRequestAgain = 1,
    BusyReportFollows = 2,
    MalfunctionRequestAgain = 3,
    MalfunctionReportFollows = 4
}

public sealed class TerminalPrimaryDeviceAttributes {
    public int DeviceCode { get; }
    public IReadOnlyList<int> Attributes { get; }

    public bool HasAttribute(
        int attribute
    );
}

public sealed class TerminalSecondaryDeviceAttributes {
    public int TerminalTypeCode { get; }
    public int FirmwareVersion { get; }
    public int OptionCode { get; }
}

public sealed class TerminalCursorPosition {
    public int Row { get; }
    public int Column { get; }
}
```

CPR `Row` and `Column` are intentionally one-based, matching the wire protocol.

`TerminalSession` adds:

```csharp
public ValueTask<TerminalPrimaryDeviceAttributes>
    QueryPrimaryDeviceAttributesAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );

public ValueTask<TerminalSecondaryDeviceAttributes>
    QuerySecondaryDeviceAttributesAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );

public ValueTask<TerminalDeviceStatus>
    QueryDeviceStatusAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );

public ValueTask<TerminalCursorPosition>
    QueryCursorPositionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );
```

---

## 3. DECRQSS contracts

```csharp
public enum TerminalStatusStringKind {
    SelectGraphicRendition,
    ConformanceLevel,
    CursorStyle,
    CharacterProtection,
    ScrollingRegion,
    LeftRightMargins,
    LinesPerPage,
    ColumnsPerPage,
    ActiveStatusDisplay,
    StatusLineType,
    AttributeChangeExtent,
    LinesPerScreen
}

public sealed class TerminalStatusStringResponse {
    public TerminalStatusStringKind Kind { get; }
    public bool IsSupported { get; }
    public string? StatusString { get; }
}
```

`TerminalSession` adds:

```csharp
public ValueTask<TerminalStatusStringResponse>
    QueryStatusStringAsync(
        TerminalStatusStringKind kind,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );
```

A closed enum is retained so callers cannot inject arbitrary control bytes through
the DECRQSS request identifier.

A negative protocol response is represented by `IsSupported == false` and
`StatusString == null`; it is not converted into a timeout.

---

## 4. XTGETTCAP contracts

```csharp
public sealed class TerminalCapabilityObservation {
    public string Name { get; }
    public bool IsSupported { get; }
    public IReadOnlyList<byte>? ValueBytes { get; }
}
```

`ValueBytes` intentionally preserves exact decoded terminal bytes. XTGETTCAP
values may contain ESC and other control bytes and are not inherently Unicode
text.

`TerminalSession` adds:

```csharp
public ValueTask<TerminalCapabilityObservation>
    QueryLiveCapabilityAsync(
        string name,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );
```

The public API intentionally uses one capability name per transaction. Names are
bounded printable non-space ASCII and are hex-encoded before emission.

---

## 5. Cancellation, timeout, and wire lifetime

All public query methods share these caller-visible rules:

- cancellation before emission sends nothing and completes with
  `OperationCanceledException`;
- caller-visible deadline expiry completes with `TimeoutException`;
- a protocol-level negative response is typed, not treated as a timeout;
- a correlated malformed response completes with `FormatException`;
- an endpoint/session state which cannot support active querying fails with
  `InvalidOperationException`.

Once request bytes may have reached the terminal, caller cancellation or timeout
does not revoke the wire transaction. The session retains bounded internal
ownership until the response is consumed, the late-response window expires, or
the session terminates.

Ambiguity-sensitive requests remain serialized during that drain period.

No transaction handle or late-response object is exposed publicly.

---

## 6. One-reader invariant and explicit probing

Exactly one session-owned coordinator reads `ITerminalInput`.

Expected response frames are demultiplexed from the same stream used for
ordinary application text, keys, mouse, focus, paste, and lifecycle-aware event
consumption.

`TerminalSession.OpenAsync` sends no DA, DSR, CPR, DECRQSS, XTGETTCAP, or other
automatic probe. Active interrogation occurs only when the caller invokes an
explicit query method.

The public 0.3 API therefore adds no second reader, no public response parser,
and no caller-extensible raw protocol registration surface.

---

## 7. Resource policy

The reviewed 0.3 implementation bounds are:

```text
maximum pending query transactions       32
maximum encoded request bytes          4096
maximum caller-visible timeout       1 minute
default late-response ownership     1 second
maximum late-response ownership    10 seconds

default CSI/DCS response frame        4096 bytes
hard response-frame ceiling         65536 bytes

maximum CSI parameter count            32
maximum CSI parameter value       1,000,000

maximum DECRQSS request id              16 bytes
maximum DECRPSS status string         1024 bytes

maximum XTGETTCAP capability name       64 bytes decoded
maximum encoded capability name        128 bytes
maximum XTGETTCAP decoded value       1024 bytes
```

These remain internal resource policy rather than public constants.

---

## 8. Immutable terminal description boundary

`Icod.TermInfo` remains the static capability authority.

Live query results are independent observations and never mutate the session's
immutable `TerminalDescription` in place.

---

## 9. Runtime dependency closure

The intended stable 0.3 runtime dependency closure is:

```text
Icod.TermInfo 1.3.0
Icod.Timing   1.0.0
```

No query implementation adds a runtime dependency on `Icod.DCurses`,
`Icod.ProcPs`, PTY/ConPTY support, or a terminal-emulator package.

---

## 10. Stable 0.3 conclusion

The reviewed public 0.3 delta is suitable for stable release without a breaking
API correction.

T28B should change release/version metadata only unless alpha.8 validation
discovers a release-blocking defect.
