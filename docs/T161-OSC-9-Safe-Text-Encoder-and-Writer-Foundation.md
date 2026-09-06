# T161 — OSC 9 Safe Text Encoder and Writer Foundation

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T161  
**Version:** `0.16.0-alpha.2`  
**Status:** Implemented; exact-head validation pending

## Purpose

T161 implements only the internal byte construction/writer foundation for the two safe text-bearing OSC 9 forms frozen by T160. It adds no public API.

## Implementation

`TerminalOsc9SafeTextEncoder` provides specialized encoding for:

```text
OSC 9;<message> ST
OSC 9;9;<windowsPath> ST
```

`OscWriter.Osc9SafeText` exposes only specialized internal frame/writer methods for those two forms. It does not accept an OSC command number or arbitrary parameter list.

Existing `OscWriter.Osc9.cs` progress behavior remains untouched, including its canonical BEL-terminated OSC 9;4 wire contract.

## Validation

Both text forms:

- require well-formed Unicode;
- encode with strict UTF-8;
- reject C0, DEL, and C1 controls;
- use canonical ST termination;
- validate the complete encoded payload before output;
- reject oversize payloads rather than truncating;
- use one non-cancellable transport write after commitment;
- do not flush implicitly.

Notification allows an empty message and enforces a 4,096-byte payload ceiling including `9;`.

OSC 9;9 rejects an empty path and enforces a 32,768-byte payload ceiling including `9;9;`.

No path normalization or conversion is performed.

## Tests

`Osc9SafeTextWriterTests` covers:

- byte-exact ASCII frames;
- Unicode/non-BMP UTF-8;
- printable punctuation preservation;
- C0/DEL/C1 rejection;
- malformed UTF-16 rejection;
- empty-notification behavior;
- empty-path rejection;
- exact notification payload boundary and one byte over;
- exact OSC 9;9 payload boundary and one byte over;
- one complete write/no flush;
- non-cancellable committed write;
- pre-cancelled write emits nothing;
- invalid input emits nothing.

## Non-changes

T161 does not add public `TerminalSession` methods, terminal detection, notification probing, OSC 9;9 automatic pairing with OSC 7, or any hazardous/raw OSC 9 command surface.

Next: T162 — public semantic notification API.
