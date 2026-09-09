# N153 — Structural CSI/DCS/String Frame Model

**Release:** `Icod.Terminal 1.5.0`  
**Tranche:** N153  
**Status:** implemented; exact-head validation pending

## Purpose

N153 preserves complete control-frame syntax above N152 framing without immediately collapsing that syntax into dialect-specific integers, strings, or vendor commands.

The structural model is intentionally internal. It exists so current CSI/DCS queries and future Sixel/Kitty Graphics dialects consume one normalized representation rather than re-slicing raw transport bytes independently.

## Structural type

`TerminalControlFrameStructure` retains a `ReadOnlyMemory<byte>` view over one complete frame and exposes:

```text
Family
UsesEightBitIntroducer
IntroducerLength
ParameterBytes
IntermediateBytes
FinalByte
PayloadBytes
TerminatorKind
TerminatorLength
```

The structural model does not copy parameter/intermediate/payload regions. The returned `ReadOnlyMemory<byte>` values are slices of the framed memory already owned by the response/control-frame object.

## Terminator vocabulary

`TerminalStringTerminatorKind` distinguishes:

```text
None
Bell
SevenBitSt
EightBitSt
```

CSI uses `None`. DCS and the string families retain the exact framing terminator instead of erasing that distinction.

## CSI preservation

For CSI, N153 preserves raw parameter bytes (`0x30–0x3F`) and intermediate bytes (`0x20–0x2F`) separately, plus the final byte.

This intentionally preserves syntax such as:

```text
?
>
;
;;
:
```

without deciding whether an omitted parameter has a default value or whether a colon subparameter belongs to SGR, Kitty keyboard reporting, or another dialect.

Numeric conversion and operation-specific limits remain in the CSI dialect parser that owns those semantics.

## DCS preservation

DCS retains:

- raw parameter bytes;
- raw intermediate bytes;
- final selector;
- opaque application payload;
- exact ST terminator kind.

This removes the duplicated introducer/header/terminator parsing previously present in DECRQSS and XTGETTCAP and establishes the shape future Sixel will consume.

The released DCS mixed-ST compatibility is retained. N153 records whichever accepted terminator actually framed the response rather than normalizing it away.

## OSC / APC / PM / SOS preservation

String families expose their application payload as opaque bytes and do not invent CSI-like parameter/final structure.

Examples:

- OSC command syntax remains an OSC dialect concern;
- APC payload `Ga=T,f=32;...` remains opaque at the structural layer;
- Kitty Graphics key/value parsing is therefore not part of N153;
- PM and SOS remain framed application strings without public semantics.

## Current-query migration

N153 migrates three existing parser families to prove the structure is active infrastructure rather than a future-only abstraction.

### CSI DA/DSR/CPR

`TerminalCsiQueryProtocol` now obtains parameter bytes, intermediate bytes, and final byte from `TerminalControlFrameStructure`.

It continues to own:

- private-marker expectations;
- numeric-only query grammar;
- maximum parameter count;
- maximum numeric value;
- DA/DSR/CPR semantic validation.

### DECRQSS

`TerminalDecrqssProtocol` now matches:

```text
DCS
parameters
$ intermediate
r selector
payload
ST
```

through the shared structure. It retains DECRPSS validity/status-string semantics and limits.

### XTGETTCAP

`TerminalXtGetTcapProtocol` now matches:

```text
DCS
parameters
+ intermediate
r selector
payload
ST
```

through the shared structure. It retains hexadecimal name/value encoding and all capability-specific limits.

The duplicated DCS content-bound, parameter-byte, intermediate-byte, and terminator parsing has therefore been removed from both dialects.

## Validation behavior

The structural parser validates:

- family introducer;
- CSI header ordering and final byte;
- DCS header ordering, final selector, and recognized terminator;
- normalized OSC/APC/PM/SOS terminator policy;
- framing bounds required to slice the returned regions.

It does not duplicate N152 payload scanning or perform dialect interpretation. Normal production response frames have already passed N152 framing before N153 structure is consumed.

## Tests

N153 adds focused tests for:

- raw CSI private/empty/colon parameter preservation;
- CSI intermediate/final separation;
- eight-bit CSI identity;
- DCS parameter/intermediate/selector/payload separation;
- released DCS mixed-ST compatibility;
- OSC opaque payload and BEL termination;
- APC opaque Kitty-like payload preservation;
- wrong-family rejection;
- malformed-header rejection;
- adaptation from the released `TerminalResponseFrame` abstraction.

The migrated existing CSI/DECRQSS/XTGETTCAP tests remain the compatibility evidence for dialect semantics.

## Deliberately deferred

N153 does not parse Sixel, Kitty Graphics, generalized CSI numeric/subparameter semantics, or multiple response families for one query. Multi-family query transactions begin in N154.
