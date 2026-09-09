# A180 — APC Construction Contract and Reference Freeze

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A180  
**Status:** implementation starting

## Purpose

A180 establishes one internal canonical Application Program Command (APC) construction primitive before Kitty Graphics dialect code is added.

The family layer must remain dialect-neutral. Kitty Graphics uses APC, but APC itself is an application-defined string control. Therefore A180 owns only the outer control-string framing and framing-safety validation. Kitty's `G`, comma-separated `key=value` control data, Base64 payload, actions, image identifiers, chunking, and response semantics belong to later tranches.

## Canonical outbound framing

Library-generated APC uses the seven-bit form:

```text
ESC _ <application-defined payload> ESC \
```

The implementation does not emit the eight-bit APC introducer or C1 ST. Existing inbound recognition of reviewed seven-bit/eight-bit control-family forms remains unchanged.

## Payload contract

At the APC family layer the payload is opaque. A180 does not parse or normalize application-defined syntax.

The writer rejects bytes that would change APC framing before the intended final ST:

```text
CAN     0x18
SUB     0x1A
ESC     0x1B
C1 ST   0x9C
```

Other payload validation belongs to the dialect which owns the bytes. For Kitty Graphics, later code will generate a restricted ASCII control grammar and Base64 payload, so arbitrary caller bytes will never reach this writer through a public API.

## Resource ceiling

A180 bounds one complete internally constructed APC frame to **8192 bytes**.

This is an Icod construction bound, not a claim that APC itself defines an 8192-byte universal limit. It leaves room for the later Kitty direct-transfer rule of at most 4096 Base64 payload bytes plus reviewed control data and four framing bytes while preventing an unbounded complete-frame allocation.

A183 will impose the stricter Kitty payload-chunk ceiling and deterministic chunking rules. Large logical images remain multi-frame streaming transactions rather than one giant APC frame.

## Parser relationship

A canonical A180 frame must round-trip through the existing normalized `TerminalControlFrameStructure` APC path with:

- `Family == TerminalControlFamily.Apc`;
- seven-bit introducer;
- introducer length `2`;
- application payload preserved byte-exactly;
- seven-bit ST;
- terminator length `2`.

No new APC input reader or scanner is introduced. Existing normalized control-language framing remains authoritative.

## Public API boundary

A180 introduces no public API.

In particular it does not add:

- `WriteApcAsync(...)`;
- `WriteRawApcAsync(...)`;
- a public Kitty Graphics encoder;
- a public arbitrary Kitty control-data dictionary;
- a second terminal input reader.

The intended public graphics surface remains the 1.7 backend-neutral `DisplayRasterAsync(...)` operation.

## Tests

A180 regression coverage must prove:

- canonical empty APC framing;
- byte-exact non-empty payload framing;
- round-trip through `TerminalControlFrameStructure`;
- rejection of CAN, SUB, ESC, and C1 ST inside payload;
- exact maximum complete-frame size accepted;
- maximum-plus-one rejected;
- public API baseline unchanged from 1.7.

## Acceptance rule

A180 is accepted only on one exact PR head that passes the complete Staging matrix:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / public API freeze
Package Foundation
Package Presentation
Package Semantic and hardening
Package Stable 1.x release line
Validated package artifact
```

A green A180 checkpoint does not authorize merge or publication. The 1.8 PR remains draft while later tranches are under development.
