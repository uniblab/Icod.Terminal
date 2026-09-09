# D171 — Existing DCS Reconciliation

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D171  
**Status:** complete

## Accepted checkpoint

Exact head:

```text
277db7da8a586dda44966fa77990b4a9f32e953a
```

Pull-request Staging workflow:

```text
34400644772
```

That exact head passed Windows, Linux, macOS runtime validation, package candidate, Package Foundation, Package Presentation, Package Semantic and hardening, Package Stable 1.x release line, and the validated package artifact.

## Purpose

D171 reconciles the two released DCS query request families with the canonical D170 `DcsWriter` without changing their wire bytes, public API, query ownership, response parsing, or compatibility behavior.

The affected protocols are:

```text
DECRQSS
XTGETTCAP
```

Both already consumed the normalized N153 DCS structural model on input. D171 removes the remaining hand-built seven-bit DCS request framing so input and output now share one reviewed control-family structure.

## Canonical request shapes

DECRQSS remains exactly:

```text
ESC P $ q <request-identifier> ESC \
```

Its DCS structural fields are:

```text
parameter bytes      empty
intermediate bytes   $
final selector       q
payload              request identifier
terminator           seven-bit ST
```

XTGETTCAP remains exactly:

```text
ESC P + q <hex-encoded-capability-name> ESC \
```

Its DCS structural fields are:

```text
parameter bytes      empty
intermediate bytes   +
final selector       q
payload              uppercase hexadecimal capability name
terminator           seven-bit ST
```

## Implementation result

`TerminalDecrqssProtocol.CreateRequest(...)` now delegates outer DCS construction to `DcsWriter.EncodeFrame(...)` after retaining its existing status-string-kind and identifier-length validation.

`TerminalXtGetTcapProtocol.CreateRequest(...)` now delegates outer DCS construction to `DcsWriter.EncodeFrame(...)` after retaining its existing capability-name validation, ASCII conversion, uppercase hexadecimal encoding, and encoded-name bound.

The obsolete per-protocol `EscapeByte` constants and manual request-array framing are removed.

## Compatibility freeze

D171 does not change:

- `TerminalStatusStringKind`;
- DECRQSS request identifiers;
- XTGETTCAP capability-name validation;
- uppercase hexadecimal request encoding;
- DECRPSS parsing;
- XTGETTCAP response parsing;
- seven-bit/eight-bit DCS input acceptance;
- mixed seven-bit/eight-bit ST compatibility;
- query correlation;
- timeout/cancellation semantics;
- bounded late-response ownership;
- one-reader session ownership;
- any public signature.

The existing public end-to-end query tests remain authoritative behavioral evidence. D171 adds protocol-construction tests specifically so later DCS/Sixel work cannot silently alter the released request bytes.

## Byte-exact regression matrix

D171 freezes all twelve DECRQSS request identifiers:

```text
m
"p
 SP q
"q
r
s
t
$|
$}
$~
*x
*|
```

and representative XTGETTCAP names including ordinary two-character names and punctuation:

```text
ku  -> 6B75
TN  -> 544E
Co  -> 436F
#2  -> 2332
```

The tests also parse consolidated requests back through `TerminalControlFrameStructure` and verify the expected DCS family, empty parameter region, intermediate byte, `q` selector, payload, and seven-bit ST terminator.

## Layering result

After D171 the DCS architecture is:

```text
semantic/query operation
    -> DECRQSS or XTGETTCAP dialect validation/encoding
        -> DcsWriter structural framing
            -> terminal output transaction
```

and inbound responses remain:

```text
terminal input
    -> bounded control-language scanner
        -> TerminalControlFrameStructure
            -> DECRPSS or XTGETTCAP dialect parser
```

This leaves Sixel free to reuse the same DCS family framing without inheriting query-specific semantics.

## Deliberately deferred

D171 does not add Sixel syntax, raster types, streaming output, or graphics capability probing. Those begin in D172 and later tranches.

It also does not broaden the generic DCS writer into a public raw-control API.
