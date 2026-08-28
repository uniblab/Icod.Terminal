# T25 — DECRQSS and DCS Transaction Support

**Project:** `Icod.Terminal`
**Release line:** `0.3.0`
**Development version:** `0.3.0-alpha.5`
**Tranche:** T25 — DECRQSS and DCS Transaction Support
**Status:** Complete

---

## 1. Purpose

T25 validates the common 0.3 query router against a DCS-based protocol rather
than another CSI response family.

T22 already provides bounded DCS framing and T23 already owns serialized query
lifetime, cancellation, deadlines, late responses, lifecycle interruption, and
the single-reader input coordinator. T25 layers DEC Request Status String
(DECRQSS) and DEC Report Status String (DECRPSS) semantics on those mechanisms.

No second input reader, protocol-specific transaction queue, or public raw-DCS
API is introduced.

---

## 2. Public Query Surface

T25 adds:

```csharp
ValueTask<TerminalStatusStringResponse> QueryStatusStringAsync(
    TerminalStatusStringKind kind,
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

`TerminalStatusStringKind` is a closed set of fixed, parameter-free DECRQSS
identifiers. Callers do not supply arbitrary request bytes or arbitrary status
identifiers.

`TerminalStatusStringResponse` exposes:

- `Kind` — the requested control-function setting;
- `IsSupported` — whether the terminal returned a positive DECRPSS response;
- `StatusString` — the returned setting without the DCS/DECRPSS wrapper, or
  `null` for a negative response.

This is a protocol-specific typed result, not a generic public query-router
result.

---

## 3. Supported Request Identifiers

T25 exposes the following fixed requests:

| `TerminalStatusStringKind` | Control function | Identifier |
| --- | --- | --- |
| `SelectGraphicRendition` | SGR | `m` |
| `ConformanceLevel` | DECSCL | `"p` |
| `CursorStyle` | DECSCUSR | `SP q` |
| `CharacterProtection` | DECSCA | `"q` |
| `ScrollingRegion` | DECSTBM | `r` |
| `LeftRightMargins` | DECSLRM | `s` |
| `LinesPerPage` | DECSLPP | `t` |
| `ColumnsPerPage` | DECSCPP | `$|` |
| `ActiveStatusDisplay` | DECSASD | `$}` |
| `StatusLineType` | DECSSDT | `$~` |
| `AttributeChangeExtent` | DECSACE | `*x` |
| `LinesPerScreen` | DECSNLS | `*|` |

Parameterized xterm-specific DECRQSS extensions are deliberately not exposed in
T25. They can be added later with a typed parameter contract if a consumer needs
them.

The implementation retains an internal hard ceiling of 16 request-identifier
bytes even though every T25 identifier is one or two bytes.

---

## 4. Wire Forms

T25 emits conservative 7-bit request framing:

```text
ESC P $ q <identifier> ESC \
```

Responses may use either:

- 7-bit DCS: `ESC P`;
- 8-bit DCS: `0x90`;

and either:

- 7-bit ST: `ESC \`;
- 8-bit ST: `0x9C`.

A positive response is:

```text
DCS 1 $ r <status-string> ST
```

A negative response is:

```text
DCS 0 $ r ST
```

The positive `<status-string>` is returned without the DCS wrapper. It contains
the control-function bytes which follow a CSI introducer. For example, an SGR
reply may return `0;4;7m`, and a top/bottom-margin reply may return `1;24r`.

---

## 5. DECRPSS Validity Convention

Some DEC terminal manuals print the DECRPSS validity parameter in the opposite
sense. Operational VT420 testing, DEC STD 070, `vttest`, and xterm use the
interoperable convention implemented here:

- `1` — positive/valid response;
- `0` — negative/invalid or unsupported request.

T25 follows the operational convention so that real DEC-compatible and xterm
implementations interoperate correctly.

---

## 6. Correlation and Malformed Replies

The DCS matcher claims only the DECRPSS family:

```text
DCS Ps $ r ... ST
```

It deliberately does not validate `Ps` or the returned status data. Those checks
belong to the protocol parser after the frame has been correlated.

Consequently:

- `DCS 2 $ r ... ST` is claimed as a malformed DECRPSS response and fails with
  `FormatException`;
- a DCS family using another intermediate/final combination, such as an
  XTGETTCAP-style `+r` response, does not satisfy a pending DECRQSS transaction;
- a positive response whose returned control function does not match the
  requested `TerminalStatusStringKind` fails with `FormatException`.

This preserves the T24 rule that a clearly correlated but malformed response is
not leaked back into ordinary application input.

---

## 7. Bounded Parsing

T25 adds explicit internal bounds:

- request identifier: at most 16 bytes;
- returned positive status string: at most 1024 bytes;
- complete DCS framing remains bounded by the T22 response-framer limits.

Positive returned status bytes must be printable 7-bit control-sequence bytes in
the range `0x20` through `0x7E`.

A positive response must contain status data. A negative response must not
contain status data.

Malformed introducers, terminators, response headers, status values, payload
bytes, or mismatched control-function suffixes fail deterministically.

---

## 8. Cancellation, Timeout, Late Responses, and Disposal

T25 reuses the T23 transaction manager unchanged.

The existing contract therefore remains authoritative:

- pre-emission cancellation sends nothing;
- caller cancellation uses normal `OperationCanceledException` semantics;
- caller-visible timeout uses `TimeoutException`;
- after emission, caller completion does not revoke wire ownership;
- a matching late DECRPSS response is consumed during bounded cleanup;
- later ambiguity-sensitive queries wait until prior ownership resolves or
  expires;
- session disposal terminates outstanding query ownership.

The T25 tests exercise these rules through DCS responses rather than only the
synthetic transaction matcher used by T23.

---

## 9. Ordinary Input

A pending DECRQSS transaction remains demand on the same session-owned input
coordinator used by application input.

Unrelated text remains deliverable through `ReadEventAsync` while the DCS query
is pending. T25 introduces no private reader or DCS-specific pump.

---

## 10. Protocol References

T25 uses the protocol-reference order frozen by T21.

The implementation was checked against:

1. DEC VT420/VT5xx programming documentation for DECRQSS/DECRPSS framing and
   fixed request identifiers;
2. the current xterm control-sequence reference, Patch #411, for practical
   interoperable DECRPSS validity semantics and supported request forms;
3. the existing T22/T23 `Icod.Terminal` response-framing and transaction
   contracts.

---

## 11. Tests

T25 synthetic tests cover:

- every fixed public request identifier;
- exact 7-bit SGR request bytes;
- typed positive and negative DECRPSS responses;
- 8-bit DCS/ST response framing;
- arbitrary byte-by-byte fragmentation;
- ordinary input preserved ahead of a pending DCS response;
- malformed validity parameters;
- positive replies for the wrong requested status function;
- unrelated DCS-family rejection;
- returned status-string size bounds;
- invalid public enum values emitting no control bytes;
- caller cancellation with late-response ownership;
- caller timeout with late-response ownership;
- session disposal with an outstanding DCS query;
- preservation of the single-reader invariant.

The tests use injected endpoints and never write to test-process standard output
or standard error.

---

## 12. Gate Result

**Gate T25: complete.**

DECRQSS now demonstrates that the common 0.3 query architecture is not
CSI-specific. DCS requests and replies use the same bounded framing,
expectation-driven correlation, serialized transaction ownership, cancellation,
deadline, lifecycle, and ordinary-input mechanisms established by T22-T24.

The next tranche is **T26 — XTGETTCAP Live Capability Queries**.
