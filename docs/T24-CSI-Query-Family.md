# T24 — CSI Device, Status, and Cursor Queries

**Project:** `Icod.Terminal`
**Release line:** `0.3.0`
**Development version:** `0.3.0-alpha.4`
**Tranche:** T24 — CSI Device, Status, and Cursor Queries
**Status:** Complete

---

## 1. Purpose

T24 is the first public active-query tranche in the 0.3 line.

T22 established bounded CSI/DCS response framing and expectation-driven routing.
T23 established serialized query transactions, monotonic caller deadlines,
normal cancellation, bounded late-response ownership, and the session-owned
single-reader coordinator.

T24 uses those mechanisms to expose common CSI terminal conversations without
adding another reader, raw-response API, or protocol-specific transaction loop.

---

## 2. Public Query Surface

T24 adds four explicit asynchronous operations on `TerminalSession`:

```csharp
ValueTask<TerminalPrimaryDeviceAttributes> QueryPrimaryDeviceAttributesAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);

ValueTask<TerminalSecondaryDeviceAttributes> QuerySecondaryDeviceAttributesAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);

ValueTask<TerminalDeviceStatus> QueryDeviceStatusAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);

ValueTask<TerminalCursorPosition> QueryCursorPositionAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

The API intentionally returns semantic typed values rather than raw CSI byte or
string representations.

No public arbitrary response matcher, generic query registration mechanism, or
raw CSI transaction API is introduced.

---

## 3. Wire Requests

T24 emits conservative 7-bit CSI request forms:

| Query | Request |
| --- | --- |
| Primary Device Attributes | `ESC [ c` |
| Secondary Device Attributes | `ESC [ > c` |
| Device Status Report | `ESC [ 5 n` |
| Cursor Position Report | `ESC [ 6 n` |

Responses may use either the 7-bit `ESC [` CSI introducer or the 8-bit C1 CSI
byte `0x9B`. This is response parsing policy only; T24 does not change the
session's broader C1 transmission policy.

---

## 4. Typed Results

### 4.1 Primary Device Attributes

`TerminalPrimaryDeviceAttributes` exposes:

- `DeviceCode` — the first Primary DA parameter;
- `Attributes` — the remaining parameters in wire order;
- `HasAttribute(int)` — a convenience membership test.

The first parameter is deliberately described as a device code rather than an
emulator identity. Active DA results are observed protocol facts and do not
rewrite `TerminalIdentity.Name` or the immutable `TerminalDescription`.

### 4.2 Secondary Device Attributes

`TerminalSecondaryDeviceAttributes` exposes the three conventional Secondary DA
parameters:

- `TerminalTypeCode`;
- `FirmwareVersion`;
- `OptionCode`.

The third value is documented generically because DEC terminals and compatible
emulators may assign somewhat different operational meaning to it.

### 4.3 Device Status

`TerminalDeviceStatus` preserves the ECMA-48 Device Status Report values:

- `Ready` (`0`);
- `BusyRequestAgain` (`1`);
- `BusyReportFollows` (`2`);
- `MalfunctionRequestAgain` (`3`);
- `MalfunctionReportFollows` (`4`).

Values 1 through 4 are protocol responses, not timeouts and not caller
cancellation. This preserves the T21 rule that protocol-defined negative or
non-ready replies remain typed response data.

### 4.4 Cursor Position

`TerminalCursorPosition.Row` and `TerminalCursorPosition.Column` are explicitly
**one-based**, matching the CPR wire protocol.

This differs intentionally from normalized mouse coordinates, which remain
zero-based terminal-cell coordinates under the 0.2 rich-input contract.
Keeping CPR one-based avoids silently changing the terminal's reported values
and makes conversion an explicit consumer choice.

---

## 5. Correlation and Input Ambiguity

T24 response matchers remain internal and expectation-driven.

A CPR-shaped sequence such as:

```text
CSI 1 ; 2 R
```

may also be a modified function-key sequence. It is interpreted as CPR only
while the serialized CPR transaction owns the active expectation.

Without such an expectation, the existing 0.2 keyboard decoder remains
authoritative.

The CSI matcher checks only enough structure to identify the expected response
family:

- CSI framing family;
- required private marker, when applicable;
- expected final byte.

Parameter syntax and semantics are validated only after the transaction claims
the correlated frame.
This means a response which clearly belongs to the outstanding family but has
invalid parameter count or values fails deterministically with `FormatException`
instead of becoming ordinary application input.

---

## 6. Bounded Parsing

T24 introduces internal hard limits for its CSI parameter parser:

- at most 32 numeric parameters per response;
- each numeric parameter at most `1,000,000`;
- no empty parameters;
- no signs;
- no non-decimal numeric characters;
- no unexpected private marker;
- no unexpected final byte.

These values are implementation bounds, not new public compatibility constants.
They exist to prevent hostile or malformed responses from creating unbounded
numeric parsing or collection growth.

Primary DA requires at least one parameter.
Secondary DA requires exactly three parameters.
Standard DSR requires exactly one value in the range 0 through 4.
Standard CPR requires exactly two positive parameters.

---

## 7. Cancellation, Timeout, and Late Responses

T24 does not add a second cancellation model.

All four public methods delegate transport ownership to the T23 transaction
manager, so the frozen contract remains unchanged:

- cancellation before emission sends nothing;
- caller cancellation surfaces through normal `OperationCanceledException`
  semantics;
- caller-visible deadline expiration surfaces as `TimeoutException`;
- after emission, caller completion does not immediately revoke the wire slot;
- a matching late response remains owned and discarded during the bounded T23
  cleanup interval;
- an ambiguity-sensitive subsequent query cannot transmit until prior ownership
  is resolved or expires.

---

## 8. Endpoint and Lifecycle Rules

The T23 endpoint availability and lifecycle rules remain authoritative.

Queries require interactive input and output endpoints representing a usable
terminal conversation. Session suspension interrupts caller-visible query
success, and disposal terminates outstanding query ownership.

T24 adds no automatic probes during `TerminalSession.OpenAsync`.

---

## 9. DEC-Private DSR Policy

T24 intentionally does not expose the broader DEC-private DSR catalog merely
because xterm implements it.

The initial public CSI surface is limited to the portable/common operations
needed to validate the query architecture:

- Primary DA;
- Secondary DA;
- standard status DSR;
- standard CPR.

DECXCPR and other private printer, keyboard, locator, checksum, and status
requests may be added in a later tranche when a concrete consumer justifies a
stable typed contract. The current parser preserves private markers strictly so
such additions do not require weakening correlation.

---

## 10. Protocol References

T24 uses the following authority order already frozen by T21:

1. ECMA-48 Fifth Edition for standardized Device Status Report and Cursor
   Position Report semantics;
2. DEC-compatible/xterm control-sequence documentation for Primary and Secondary
   Device Attributes forms and the practical CPR ambiguity with modified
   function keys;
3. existing `Icod.TermInfo` data for immutable capability/profile information,
   without mutating it from live replies.

The implementation was checked against the current xterm control-sequence
reference for Patch #411 (updated 2026-08-23).

---

## 11. Tests

T24 synthetic tests cover:

- exact Primary DA request bytes and typed parsing;
- exact Secondary DA request bytes and typed parsing;
- all five standard DSR status values;
- exact CPR request bytes and one-based result coordinates;
- 8-bit CSI response acceptance;
- malformed correlated CPR and Secondary DA responses;
- numeric magnitude and parameter-count bounds;
- CPR-shaped modified F3 input when no query is active;
- text, key, focus, mouse, and bracketed-paste delivery while CPR remains
  outstanding;
- preservation of the T23 single-reader invariant during the integrated rich
  input/query case.

The tests use injected terminal endpoints and never write to test-process
standard output or standard error.

---

## 12. Gate Result

**Gate T24: complete.**

The common CSI query family now operates through normal async/await over the T23
transaction substrate. Typed results are returned without exposing raw routing
machinery, CPR ambiguity remains expectation-driven, ordinary rich input remains
live, and cancellation/timeout/late-response behavior continues to be owned by
the common session query manager.

The next tranche is **T25 — DECRQSS and DCS Transaction Support**.
