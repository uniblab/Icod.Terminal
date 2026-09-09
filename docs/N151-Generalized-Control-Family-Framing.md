# N151 — Generalized Control-Family Framing

**Release:** `Icod.Terminal 1.5.0`  
**Tranche:** N151  
**Status:** implemented; exact-head validation pending

## Purpose

N151 generalizes the existing internal terminal-response framer from the released CSI/DCS/OSC response families to the normalized control-family vocabulary frozen by N150:

```text
CSI
DCS
OSC
APC
PM
SOS
```

The work is intentionally framing-only. It does not add a Kitty Graphics parser, a Sixel parser, a generic APC API, a generic DCS API, or any new public surface.

## Layer boundary

The framing layer answers only:

> Does the current byte prefix form one complete bounded member of this control family?

It does not answer:

- which semantic operation the frame represents;
- which vendor/dialect owns the payload;
- whether the terminal supports that dialect;
- whether the frame satisfies one active query;
- how a future CSI/DCS/APC payload should be interpreted structurally.

Those responsibilities remain with later N153–N158 work.

## Compatibility adapter

The released query infrastructure still uses the internal `TerminalResponseFrameKind` vocabulary:

```text
Csi
Dcs
Osc
```

N151 does not replace that single-family query contract prematurely. Instead, `TerminalResponseFramer` now exposes a normalized overload accepting `TerminalControlFamily`, while the existing response-kind overload delegates CSI/DCS/OSC to it.

This adapter remains intentionally temporary until N154 introduces multi-family query transactions.

## Framing rules

### CSI

CSI preserves its existing ECMA-style grammar:

```text
introducer
parameter bytes     0x30–0x3F
intermediate bytes  0x20–0x2F
final byte          0x40–0x7E
```

Both the existing 7-bit `ESC [` and 8-bit `0x9B` introducers remain recognized.

### DCS

DCS retains its released behavior unchanged:

- CSI-like header before the final selector;
- application payload after the final selector;
- bounded ST framing;
- CAN/SUB cancellation rejected as invalid;
- no dialect interpretation in the framer.

Future Sixel therefore remains a DCS dialect layered above this family parser.

### OSC

OSC retains its released compatibility rules unchanged:

- 7-bit `ESC ]` framing;
- existing BEL termination compatibility for 7-bit OSC;
- existing `ESC \\` ST handling;
- existing 8-bit OSC/ST handling;
- CAN/SUB cancellation rejection.

N151 does not broaden BEL termination to any other string family.

### APC / PM / SOS

APC, PM, and SOS use strict bounded string framing.

Seven-bit forms are:

```text
ESC _ ... ESC \\   APC
ESC ^ ... ESC \\   PM
ESC X ... ESC \\   SOS
```

Eight-bit C1 forms are:

```text
0x9F ... 0x9C       APC
0x9E ... 0x9C       PM
0x98 ... 0x9C       SOS
```

The chosen representation is paired deliberately:

- a seven-bit string introducer requires seven-bit `ESC \\` termination;
- an eight-bit C1 introducer requires eight-bit `0x9C` ST termination.

BEL is ordinary payload for these families and does not terminate them.

CAN (`0x18`) and SUB (`0x1A`) abort the candidate as invalid. An ESC inside a seven-bit string is accepted only when it begins the terminating `ESC \\`; an ESC inside an eight-bit form is rejected as invalid rather than ambiguously changing representation mid-frame.

## Bounds

N151 retains the existing bounded framing contract:

- default response frame limit: 4,096 bytes;
- hard maximum inherited from the established OSC 52 response bound;
- incomplete prefixes do not allocate an unbounded dialect buffer;
- a candidate reaching its framing limit without valid completion is invalid.

Later graphics work will introduce transaction/source-resource limits appropriate for large image payloads instead of abusing this ordinary response-frame bound.

## Tests

N151 adds focused regression coverage for:

- legacy CSI/DCS/OSC adapter equivalence with the normalized family overload;
- seven-bit APC completion;
- eight-bit APC completion;
- strict terminator pairing;
- PM seven-bit/eight-bit completion;
- SOS seven-bit/eight-bit completion;
- BEL not terminating APC;
- CAN/SUB cancellation;
- incomplete split `ESC` terminator state;
- maximum frame enforcement;
- unknown family rejection.

The existing query/router tests remain the primary regression evidence that CSI/DCS/OSC behavior did not change.

## Deliberately deferred

N151 does not implement:

- one shared incremental Ground/Escape state machine — N152;
- structural CSI/DCS/string frame records — N153;
- multi-family query transactions — N154;
- semantic capability evidence or backend selection — N155–N158;
- Sixel — planned for 1.7;
- Kitty Graphics — planned for 1.8.

## Acceptance

N151 is complete when the exact PR head passes Windows/Linux/macOS Staging validation and all package contracts while preserving the frozen 1.4 public API fingerprint.
