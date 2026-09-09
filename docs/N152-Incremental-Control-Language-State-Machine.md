# N152 — Incremental Control-Language State Machine

**Release:** `Icod.Terminal 1.5.0`  
**Tranche:** N152  
**Status:** implemented; exact-head validation superseded by N153 work

## Purpose

N152 replaces family-specific scanning loops with one bounded byte-at-a-time state machine used by the normalized framing layer.

The scanner is transport/framing infrastructure only. It does not know OSC command numbers, CSI semantic functions, DCS dialects, Sixel, Kitty Graphics, notification metadata, terminal brands, or routing policy.

## Scanner states

The internal state vocabulary is:

```text
Ground
Escape
CsiParameter
CsiIntermediate
DcsParameter
DcsIntermediate
DcsPayload
OscPayload
ApcPayload
PmPayload
SosPayload
StringEscape
Complete
Invalid
NotCandidate
```

The state machine recognizes both seven-bit and eight-bit family introducers and can be reset for a subsequent frame.

## Incremental contract

`TerminalControlSequenceScanner.Feed(byte)` consumes exactly one byte and reports one of:

```text
Incomplete
Complete
Invalid
NotCandidate
```

The scanner retains:

- current structural state;
- discovered `TerminalControlFamily` once known;
- whether the introducer used an eight-bit C1 byte;
- consumed length;
- whether a lone `ESC` is still an incomplete introducer.

The scanner refuses additional input after Complete/Invalid/NotCandidate until `Reset()` is called. This makes transaction boundaries explicit and prevents accidental reuse of terminal state.

## CSI

CSI uses distinct parameter and intermediate states so parameter bytes cannot appear after intermediate bytes. A byte in `0x40–0x7E` completes the sequence as the final byte.

The scanner intentionally does not parse numeric values or colon subparameters. N153 and later CSI dialect code retain the raw syntax.

## DCS

DCS uses the same header grammar as CSI until its final selector, then enters `DcsPayload` until ST.

This is the shared framing path future Sixel will use. N152 does not interpret Sixel data.

The released DCS compatibility behavior remains unchanged: either recognized ST representation may complete an already valid DCS response frame.

## OSC

OSC preserves the released framing policy:

- seven-bit OSC may use BEL or seven-bit ST;
- eight-bit OSC uses eight-bit ST;
- CAN/SUB invalidate the candidate;
- representation-changing ESC usage is rejected according to the existing contract.

## APC / PM / SOS

These families use strict string framing frozen by N151:

- seven-bit introducer -> seven-bit `ESC \\` termination;
- eight-bit introducer -> C1 `0x9C` ST termination;
- CAN/SUB invalidate the candidate;
- BEL is payload, not a terminator.

Kitty Graphics therefore remains a future APC dialect layered above this scanner rather than a scanner state of its own.

## Bounds

Each scanner instance receives an explicit maximum frame length. The constructor rejects limits below the minimum framing size or above the current ordinary-response hard ceiling. A frame that reaches the configured limit without completion becomes invalid.

Large future graphics transactions will receive separate source/transaction budgets rather than silently increasing ordinary response limits.

## Integration

`TerminalResponseFramer` now delegates framing to `TerminalControlSequenceScanner` instead of maintaining separate CSI, DCS, OSC, and string-family loops.

The released `TerminalResponseFrameKind` query adapter remains intact until N154. Existing query code therefore sees no public or semantic change.

## Tests

N152 adds direct byte-at-a-time tests for:

- all six seven-bit families;
- all six eight-bit families;
- lone-ESC incomplete introducers;
- unknown escape sequences returning NotCandidate;
- reset/reuse;
- refusal to feed after a terminal state;
- CSI parameter/intermediate ordering;
- split DCS ST termination;
- configured frame bounds;
- constructor-bound validation.

Existing historical query tests additionally exercise CSI/DCS/OSC through the same scanner because `TerminalResponseFramer` now delegates to it.

## Deliberately deferred

N152 does not define structural frame records, multi-family queries, capability evidence, backend routing, Sixel, or Kitty Graphics. Those remain N153 onward and the later 1.7/1.8 graphics releases.
