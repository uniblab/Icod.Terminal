# T26 — XTGETTCAP Live Capability Queries

**Project:** `Icod.Terminal`
**Release line:** `0.3.0`
**Development version:** `0.3.0-alpha.6`
**Tranche:** T26 — XTGETTCAP Live Capability Queries
**Status:** Complete

---

## 1. Purpose

T26 adds explicit live terminal capability interrogation through xterm's
XTGETTCAP protocol.

T22 supplied bounded DCS framing. T23 supplied serialized transaction ownership,
deadlines, cancellation, and late-response handling. T25 proved that the common
router can carry a typed DCS query family. T26 reuses those layers rather than
adding another reader or protocol-specific transaction loop.

The result is deliberately an **observation**, not a replacement terminal
description.

---

## 2. Public API

T26 adds one public operation:

```csharp
ValueTask<TerminalCapabilityObservation> QueryLiveCapabilityAsync(
    string name,
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

and one public result type:

```csharp
sealed class TerminalCapabilityObservation
{
    string Name { get; }
    bool IsSupported { get; }
    IReadOnlyList<byte>? ValueBytes { get; }
}
```

`ValueBytes` is `null` for a protocol-level negative response.

For a positive response it contains the exact bytes obtained after hexadecimal
decoding, including an empty byte string when the terminal positively reports an
empty value.

The API intentionally does not decode capability values as Unicode text.
Special-key capabilities commonly contain ESC and other terminal control bytes.

---

## 3. Single-Name Transaction Policy

The xterm protocol permits a semicolon-separated list of hex-encoded names in
one XTGETTCAP request.

T26 intentionally issues exactly **one capability name per transaction**.

This keeps:

- response correlation exact;
- negative results unambiguous;
- resource bounds simple;
- late-response ownership identical to the T23/T25 model;
- partial-result behavior out of the public contract.

Current xterm documentation states that an invalid name terminates processing of
a multi-name list. That creates partial-result semantics which are unnecessary
for the 0.3 acceptance goal.

Callers that need several observations can issue several asynchronous operations;
the common transaction manager serializes their ambiguity-sensitive wire
lifetimes.

Batching may be reconsidered during the T28 API-regret review if a concrete
consumer demonstrates enough value to justify a stable partial-result contract.

---

## 4. Name Validation and Wire Encoding

Capability names are caller supplied, but they are never emitted verbatim.

T26 requires:

- a non-empty name;
- no more than 64 decoded name bytes;
- printable non-space ASCII characters only (`0x21` through `0x7e`).

The name is then encoded as two hexadecimal digits per byte before transmission.

For example:

```text
TN -> 544E
Co -> 436F
ku -> 6B75
#2 -> 2332
```

A punctuation character therefore remains data after encoding and cannot become
a DCS delimiter or string terminator.

The encoded name limit is 128 bytes.

---

## 5. Wire Contract

T26 emits the conservative 7-bit form:

```text
ESC P + q <hex-name> ESC \
```

For a supported capability, xterm responds with:

```text
DCS 1 + r <hex-name> = <hex-value> ST
```

For an unsupported/invalid capability, xterm responds with:

```text
DCS 0 + r ST
```

T26 accepts either:

- 7-bit `ESC P` DCS plus `ESC \` ST; or
- 8-bit C1 `0x90` DCS plus `0x9c` ST.

Hexadecimal digits in responses may be uppercase or lowercase.

The request encoder emits uppercase hexadecimal only so synthetic fixtures and
diagnostics remain deterministic.

---

## 6. Response Correlation and Parsing

The internal response matcher claims only the XTGETTCAP DCS family:

```text
DCS Ps + r ... ST
```

It does not claim:

- DECRPSS (`DCS Ps $ r ... ST`);
- XTGETXRES (`DCS Ps + R ... ST`);
- unrelated DCS families.

Once a frame is clearly in the outstanding XTGETTCAP family, strict parsing is
authoritative. A correlated malformed frame therefore fails with
`FormatException` rather than leaking into ordinary application input.

A positive response must contain exactly one literal `=` separator. The
hex-decoded returned name must exactly match the requested capability name.

A negative response must contain no capability payload.

---

## 7. Resource Bounds

T26 introduces the following internal implementation bounds:

- one capability name per transaction;
- 64 decoded bytes per capability name;
- 128 encoded bytes per capability name;
- 1024 decoded bytes per returned capability value;
- the existing T22 complete DCS-frame ceiling remains authoritative.

The parser rejects:

- odd-length hexadecimal fields;
- non-hexadecimal characters;
- oversized decoded values;
- mismatched returned names;
- duplicate/multiple name-value pairs in a single-name transaction;
- malformed validity parameters;
- payload data on a negative response.

These are implementation bounds, not new public compatibility constants.

---

## 8. Static Terminfo vs Live Observation

`Icod.TermInfo` remains the immutable capability/profile authority for
`TerminalDescription`.

XTGETTCAP is not a general remote replacement for a terminfo database. Current
xterm documentation limits this protocol primarily to special keyboard
capabilities, plus selected observations such as:

- `Co` / `colors`;
- `TN` / `name`;
- `RGB`.

Some returned values may represent terminal state that can differ from a static
terminfo entry.

`QueryLiveCapabilityAsync` therefore returns an independent
`TerminalCapabilityObservation`.

It does **not**:

- mutate `TerminalDescription`;
- rewrite `TerminalIdentity`;
- add or replace terminfo capabilities;
- cache live observations as static profile facts.

Consumers must choose deliberately whether a live observation or static
terminfo capability is appropriate for their operation.

---

## 9. Cancellation, Timeout, and Late Responses

T26 delegates transaction lifetime to the existing T23 manager.

The established rules therefore remain unchanged:

- cancellation before emission sends nothing;
- caller cancellation uses normal `OperationCanceledException` semantics;
- caller-visible timeout uses `TimeoutException`;
- cancellation or timeout after emission does not revoke the wire transaction;
- the ambiguity-sensitive slot remains owned during the bounded late-response
  interval;
- a matching late XTGETTCAP response is consumed rather than exposed as
  application input;
- a subsequent query cannot transmit until prior ownership resolves or expires;
- session disposal terminates outstanding query ownership.

---

## 10. Protocol Reference

T26 uses the xterm control-sequence reference as the protocol authority for
XTGETTCAP:

`https://invisible-island.net/xterm/ctlseqs/ctlseqs.html`

The implementation was checked against xterm Patch #411, updated 2026-08-23.

That reference documents:

```text
DCS + q Pt ST
DCS 1 + r Pt ST
DCS 0 + r ST
```

with request names and positive response name/value strings encoded as two
hexadecimal digits per byte.

---

## 11. Tests

T26 synthetic tests cover:

- exact 7-bit request encoding;
- punctuation-safe name encoding;
- positive byte-valued responses;
- negative typed responses;
- supported empty values;
- 8-bit DCS/ST handling;
- uppercase and lowercase hexadecimal;
- arbitrary response fragmentation;
- ordinary input delivery while a query is pending;
- malformed and odd-length hexadecimal;
- mismatched returned names;
- duplicate/multiple result rejection;
- decoded value bounds;
- public name validation before emission;
- unrelated DCS-family rejection;
- caller cancellation with late-response ownership;
- timeout with late-response ownership;
- session disposal;
- preservation of the single-reader invariant.

The tests use injected terminal endpoints and do not write to test-process
standard output or standard error.

---

## 12. Gate Result

**Gate T26: complete.**

Callers can explicitly obtain bounded live XTGETTCAP observations through the
same serialized DCS transaction path proven by T25. Returned bytes remain
separate from immutable `Icod.TermInfo` data, negative responses are typed,
malformed correlated responses fail deterministically, and ordinary application
input remains live under the single-reader invariant.

The next tranche is **T27 — Query Integration and Probe Acceptance**.
