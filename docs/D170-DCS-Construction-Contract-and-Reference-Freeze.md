# D170 — DCS Construction Contract and Reference Freeze

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D170  
**Status:** implementation starting

## Purpose

D170 establishes the canonical internal output representation for Device Control String (DCS) frames before Sixel encoding begins.

The 1.5 structural parser already normalizes inbound DCS into:

```text
parameter bytes
intermediate bytes
final selector
application payload
ST termination
```

D170 supplies the corresponding internal construction path so existing DECRQSS/XTGETTCAP and future Sixel output no longer hand-assemble `ESC P ... ESC \\` framing independently.

## Layer boundary

The DCS construction primitive owns only control-family syntax:

```text
ESC P
<parameter bytes>
<intermediate bytes>
<final selector>
<opaque payload>
ESC \
```

It does not assign dialect meaning to parameters, intermediates, the final selector, or ordinary payload bytes.

Examples remain dialect-owned:

```text
DECRQSS   DCS $ q <identifier> ST
XTGETTCAP DCS + q <hex-name> ST
Sixel     DCS Pa ; Pb ; Ph q <sixel-data> ST
```

## Canonical emission

Library-generated DCS uses the seven-bit representation:

```text
ESC P ... ESC \
```

The input scanner may continue to accept reviewed eight-bit DCS/ST forms according to the existing normalized framing contract. Input compatibility does not require emitting C1 bytes.

## Structural validation

The generic DCS constructor validates:

- parameter bytes are each in `0x30` through `0x3F`;
- intermediate bytes are each in `0x20` through `0x2F`;
- the final selector is in `0x40` through `0x7E`;
- complete-frame length arithmetic is overflow-safe;
- complete small frames are bounded to 4,096 bytes;
- payload content remains dialect-opaque except for bytes that would alter the DCS frame itself.

The canonical writer rejects these payload bytes structurally:

```text
CAN      0x18
SUB      0x1A
ESC      0x1B
C1 ST    0x9C
```

`CAN` and `SUB` abort a control string; `ESC` can begin the seven-bit terminator; and C1 `ST` is accepted by the normalized DCS scanner as a terminator. Allowing any of those inside a complete canonical payload would make the requested frame boundary untrue.

All other payload semantics, command grammar, validation, and tighter size ceilings belong to the owning dialect.

## Small-frame and streaming split

D170 permits complete-frame allocation for small bounded query/control strings. This is appropriate for DECRQSS and XTGETTCAP.

Sixel is different: large graphics must not require one giant encoded DCS allocation. The DCS substrate therefore separates:

1. structural frame encoding for small frames; and
2. a later committed streaming transaction in D176.

The streaming transaction will emit the same canonical introducer/header/final selector/payload/ST structure while preserving session output serialization and non-truncating post-commit semantics.

D170 does not prematurely add streaming callbacks, `IBufferWriter` public APIs, or Sixel-specific state to the generic writer.

## Existing-query reconciliation rule

D171 will migrate DECRQSS and XTGETTCAP request construction onto the new writer only when byte-exact tests prove no wire change.

The required canonical bytes remain:

```text
DECRQSS   ESC P $ q <identifier> ESC \
XTGETTCAP ESC P + q <hex-name>   ESC \
```

Released public query behavior, timeout/cancellation semantics, response correlation, and inbound mixed-ST compatibility are unchanged.

## Sixel reference boundary

The first Sixel implementation follows the DEC/xterm DCS form:

```text
DCS Pa ; Pb ; Ph q <sixel data> ST
```

Primary Device Attributes parameter `4` is recognized by xterm/DEC graphics-terminal semantics as Sixel graphics capability evidence. D177 will determine exactly how that evidence updates the existing normalized capability ledger.

Protocol references:

- xterm Control Sequences, Device-Control functions and Sixel Graphics: <https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>
- VT330/VT340 graphics programming references are treated as historical protocol source material; implementation behavior is still bounded by Icod.Terminal's stable ownership/security contracts.

## Security and ownership

DCS payloads may be large and may control terminal behavior. Consequently:

- there is no public raw DCS writer;
- public semantic operations must validate their complete semantic input before output commitment where practical;
- large committed DCS output must not be caller-cancelled mid-frame;
- session-owned semantic output must not interleave inside one committed DCS transaction;
- transport failure after commitment is surfaced and never silently retried;
- one `TerminalSession` remains the authoritative input/query path.

## D170 acceptance

D170 is complete when:

- an internal `DcsWriter` exists;
- canonical seven-bit DCS structural encoding is byte-exact;
- invalid parameter/intermediate/final bytes are rejected deterministically;
- payload framing/abort controls are rejected;
- empty and non-empty payloads are covered;
- exact maximum complete-frame size and maximum-plus-one are covered;
- size arithmetic is bounded/overflow-safe;
- no public API is added;
- existing DECRQSS/XTGETTCAP code remains unchanged until D171 byte-exact migration;
- Windows/Linux/macOS Staging and package gates pass on the exact D170 head.
