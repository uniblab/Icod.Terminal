# T52 — OSC 52 Contract and Reference Freeze

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.1`  
**Status:** Contract frozen for implementation  
**Primary protocol reference:** xterm control sequences, OSC 52 — Manipulate Selection Data  
**Secondary evidence:** xterm terminfo `Ms` capability and OSC 52 compatibility notes

---

## 1. Purpose

T52 freezes the protocol, security, framing, resource, and ownership rules for
OSC 52 before production implementation begins.

The canonical xterm form is:

```text
OSC 52 ; Pc ; Pd ST
```

where `Pc` identifies one or more selections and `Pd` is normally RFC 4648
base64 data. A query uses `?` as `Pd`.

`Icod.Terminal 0.7.0` deliberately exposes a smaller semantic surface than the
full xterm grammar. The library will support one typed selection per operation
rather than public raw selector strings or multi-target lists.

---

## 2. Reference behavior accepted from xterm

The reference grammar recognizes selection letters:

```text
c  clipboard
p  primary
q  secondary
s  select
0-7  cut buffers
```

and permits an empty `Pc` with xterm-specific default-selection behavior.

For 0.7, `Icod.Terminal` freezes only:

```text
c  Clipboard
p  Primary
q  Secondary
s  Select
```

Cut buffers `0` through `7`, empty selection lists, and multi-selection lists are
not exposed publicly in 0.7.

This intentionally avoids freezing xterm/X11-specific legacy cut-buffer policy
into the general managed terminal API.

---

## 3. Canonical outbound framing

All library-generated OSC 52 output SHALL use canonical seven-bit framing:

```text
ESC ] 52 ; <selection> ; <payload> ESC \
```

The library SHALL NOT emit BEL-terminated OSC 52 frames or C1 OSC/ST forms.

### Write

A write uses standard unwrapped RFC 4648 base64:

```text
ESC ] 52 ; c ; SGVsbG8= ESC \
```

### Query

A read/query uses literal `?`:

```text
ESC ] 52 ; c ; ? ESC \
```

The complete outbound frame must be validated and sized before its first byte is
written.

---

## 4. Empty and clear semantics

An empty caller payload is represented as an empty base64 payload field:

```text
OSC 52 ; Pc ; ST
```

For 0.7 this operation means **set the selected terminal selection to empty**.

The public API SHALL NOT expose arbitrary invalid-base64 text as a clearing
mechanism even though xterm documents non-base64, non-`?` `Pd` as clearing the
selection. Clearing is expressed semantically by writing an empty payload.

There is no separate raw `ClearOsc52(...)` escape API.

---

## 5. Payload representation

The protocol core treats selection content as bytes.

String convenience, if accepted by the later public API review, must encode text
using a separately documented deterministic encoding policy and must not redefine
the underlying byte contract.

Outbound payload encoding SHALL use standard RFC 4648 base64 with:

- the ordinary alphabet `A-Z a-z 0-9 + /`;
- required `=` padding;
- no whitespace;
- no line wrapping;
- no URL-safe alphabet substitution.

Inbound payload decoding SHALL accept only that canonical alphabet and padding
shape. Whitespace, malformed padding, alternate alphabets, and non-base64 bytes
are rejected.

---

## 6. Resource limits

0.7 freezes conservative library-owned limits independent of any particular
terminal emulator's larger or smaller implementation limits.

```text
Maximum decoded selection payload:       65,536 bytes
Maximum base64 payload:                   87,384 bytes
Maximum complete OSC 52 frame:            87,400 bytes
```

The base64 ceiling is the exact RFC 4648 expansion of 65,536 bytes.

Both outbound writes and decoded inbound query results are limited to 65,536
bytes. Oversized encoded responses are rejected before attempting an oversized
decode allocation.

All expansion arithmetic SHALL be checked.

A terminal may impose a smaller limit. Successful library emission therefore does
not prove terminal acceptance.

---

## 7. Read security contract

Clipboard reads are security-sensitive and SHALL occur only through an explicit
caller invocation.

The library SHALL NOT:

- query clipboard data during `TerminalSession.OpenAsync(...)`;
- query clipboard data during capability discovery;
- query clipboard data during suspend/resume;
- query clipboard data during disposal;
- infer permission from `TERM`, OS, emulator identity, or terminfo alone;
- retry a clipboard query silently after timeout or malformed response.

An explicit read call is the opt-in boundary for that operation.

0.7 does not introduce background monitoring, clipboard synchronization, or a
session-global "clipboard read enabled" mode.

---

## 8. Inbound framing compatibility

OSC 52 responses SHALL be routed through the existing single session input path.
No second reader loop is permitted.

For inbound responses, 0.7 SHALL recognize these bounded forms while an OSC 52
expectation is active:

```text
ESC ] ... BEL
ESC ] ... ESC \
0x9D ... 0x9C
```

Thus inbound compatibility may accept seven-bit OSC with BEL or ST termination
and C1 OSC/ST framing, while outbound emission remains canonical seven-bit
`ESC ] ... ESC \\`.

The framing layer SHALL enforce its OSC 52 maximum before retaining an unbounded
control string.

BEL is a terminator only while framing a candidate OSC response. It is not part
of decoded clipboard data because the payload field is base64 ASCII.

---

## 9. Response correlation

A clipboard query expectation is satisfied only by a syntactically valid OSC 52
response whose selection field corresponds to the active requested selection.

The response shape is conceptually:

```text
OSC 52 ; Pc ; Pd terminator
```

where `Pd` is bounded base64 selection data.

The following SHALL NOT satisfy the query:

- another OSC selector;
- OSC 52 for a different selection;
- a raw OSC 52 frame received before the expectation is armed;
- malformed base64;
- oversized payload;
- malformed or multi-selection `Pc` outside the frozen 0.7 subset.

No unrelated OSC traffic may be consumed as a successful clipboard response.

The exact decoder disposition for malformed/unmatched OSC candidates is an
implementation detail to be proven in T55, but it must preserve ordinary input
ownership and avoid infinite retention.

---

## 10. Timeout, cancellation, and late response ownership

Clipboard query transactions SHALL reuse the 0.3 transaction model.

Before request emission commits:

- caller cancellation may prevent output entirely;
- invalid arguments produce zero protocol output.

After emission commits:

- caller cancellation or timeout ends the caller-visible operation;
- the wire transaction retains bounded late-response ownership so a delayed OSC
  52 reply cannot be misinterpreted as ordinary input or satisfy a later query;
- late ownership uses the same bounded transaction principles already established
  for CSI/DCS queries;
- session disposal terminates outstanding ownership.

No query is automatically reissued.

---

## 11. Output ordering and flush policy

OSC 52 writes and queries participate in the same session-owned control-output
serialization used by existing semantic terminal operations.

A complete OSC 52 frame SHOULD be emitted in one `WriteAsync(...)` call after
validation.

Ordinary write operations do not introduce an implicit flush.

Query request emission SHALL follow the existing active-query transaction rule:
request output is serialized and flushed where required by that transaction
substrate so the terminal can observe the request before the response deadline is
owned.

---

## 12. Endpoint and support semantics

OSC 52 semantic operations require an interactive terminal output endpoint.
Queries additionally require the established conversational input/output endpoint
pair.

Known redirected output is rejected.

Terminal identity or terminfo data may inform compatibility diagnostics, but
shall not be converted into proof that OSC 52 is permitted. xterm itself can
disable these operations through terminal policy, and other emulators vary in
write/read support.

Therefore:

- successful write means the frame was emitted;
- successful query means a valid correlated response was received and decoded;
- timeout does not mean "unsupported" with certainty;
- absence of a known capability does not automatically prohibit optimistic write
  emission unless a later compatibility gate freezes stronger evidence.

---

## 13. Public API boundary

T52 freezes semantics, not final public names.

The eventual public API is expected to expose:

- a typed single-selection representation;
- one explicit bounded write operation;
- one explicit bounded read/query operation.

0.7 SHALL NOT expose:

```text
SendOsc(...)
SendOsc52Raw(...)
string selectionSelector
string rawBase64Payload
IEnumerable<char> selections
cut-buffer indices 0-7
background clipboard monitoring
OS-native clipboard APIs
```

Final naming and overload decisions remain T56/T57/T59 work.

---

## 14. Compatibility notes

The xterm OSC 52 implementation supports both setting and querying selection data,
but terminal implementations vary substantially. Some support writes only; some
support both; some disable operations by policy.

The xterm terminfo ecosystem also exposes the `Ms` extended capability for
selection modification, but 0.7 SHALL NOT treat presence of that capability as
proof that clipboard reads are enabled.

Compatibility evidence belongs to T58 and shall not weaken the explicit security
boundary frozen here.

---

## 15. T52 gate

T52 is complete when implementation work proceeds under these invariants:

1. one typed selection per operation;
2. supported selectors limited to clipboard, primary, secondary, and select;
3. canonical seven-bit/ST outbound framing;
4. canonical bounded RFC 4648 base64 payloads;
5. 65,536-byte decoded payload ceiling;
6. explicit caller invocation required for every clipboard read;
7. inbound OSC responses use the existing single-reader path;
8. inbound framing may accept BEL/ST and C1 compatibility forms under bounded
   expectation-driven routing;
9. timeout/cancellation retains bounded late-response ownership after emission;
10. no generic public OSC or raw-selector escape hatch is introduced.

The next tranche is **T53 — selection and payload primitives**.
