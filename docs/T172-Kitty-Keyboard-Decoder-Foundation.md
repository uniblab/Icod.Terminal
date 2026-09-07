# T172 — Kitty Keyboard Decoder Foundation

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T172  
**Version:** `0.17.0-alpha.3`  
**Status:** Implemented; exact-head validation pending

## Purpose

T172 adds canonical Kitty CSI-u keyboard decoding to the existing incremental terminal input path. It does not negotiate keyboard mode; T173 owns support detection and reversible acquisition.

The decoder remains single-path. Active terminal response routing gets first opportunity to claim correlated CSI traffic. Only unclaimed input is offered to the modern keyboard parser before existing mouse/terminfo/traditional fallback.

## Supported canonical form

T172 decodes the Kitty CSI-u structure:

```text
CSI key[:shifted[:base]] ; modifiers[:event-type] ; associated-text u
```

Optional fields may be omitted according to the Kitty grammar.

## Semantic mapping

### Character keys

Unicode scalar key identities produce `TerminalKey.Character` events unless the key code maps to a frozen named identity.

### Named control keys

Canonical Unicode identities map:

```text
9    -> Tab
13   -> Enter
27   -> Escape
32   -> Space
127  -> Backspace
```

### Modifiers

Kitty encodes a modifier bitset plus one. T172 subtracts one and explicitly maps Kitty wire bits to the existing semantic enum:

```text
Kitty bit 1   -> Shift
Kitty bit 2   -> Alt
Kitty bit 4   -> Control
Kitty bit 8   -> Super
Kitty bit 16  -> Hyper
Kitty bit 32  -> Meta
Kitty bit 64  -> CapsLock
Kitty bit 128 -> NumLock
```

The integer is never cast directly to `TerminalKeyModifiers`, preserving the pre-existing public Alt/Control bit assignments.

### Event type

```text
1 -> Press
2 -> Repeat
3 -> Release
```

Omitted event type defaults to `Press`.

### Functional keys

The frozen current Kitty private-use functional-key table is normalized into `TerminalKey` identities. F13 through F35 use `TerminalKey.Function` plus `FunctionKeyNumber`.

A syntactically valid Kitty private-use key code not known by the current library maps to `TerminalKey.Unrecognized`; the private-use integer is not exposed publicly.

### Alternate key identities

Shifted and base-layout Unicode key identities populate `ShiftedCharacter` and `BaseLayoutCharacter` for `TerminalKey.Character` events only.

### Associated text

Associated text accepts up to 32 Unicode scalar values in one frame and preserves their order in `AssociatedText`.

C0, DEL, and C1 values in associated text make the modern keyboard frame malformed.

A Kitty key code of zero with associated text represents pure text. T172 returns those scalars through the existing `TerminalInputEventKind.Text` path. Additional scalars are reinserted into the bounded decoder byte buffer ahead of later transport bytes so ordinary text semantics and ordering are preserved without adding a second reader or public aggregate-text event.

## Bounded parsing

Internal bounds:

```text
maximum modern keyboard CSI frame: 4,096 bytes
maximum semicolon parameters:      64
maximum associated-text scalars:   32
```

Numeric parsing uses bounded `int` conversion. Invalid Unicode scalar values, surrogate code points, invalid event types, invalid modifier encodings, and unsupported parameter shapes make the complete CSI-u frame malformed.

A complete malformed CSI-u frame is consumed as one protocol unit and discarded. The next input starts from a clean decoder position.

Non-`u` CSI traffic is not claimed by this T172 parser and continues through existing mouse/terminfo/traditional paths.

## Response-routing priority

`TryRouteExpectedResponseAsync(...)` remains authoritative.

If an active correlated response matcher claims a frame, the keyboard decoder never sees it. When a frame is not a candidate or does not match the active expectation, modern keyboard parsing may claim a canonical CSI-u key frame.

No second response reader or keyboard input stream is introduced.

## Tests

`TerminalKittyKeyboardDecoderTests` covers:

- canonical character press;
- explicit Kitty-to-semantic modifier translation including all eight flags;
- repeat and release phases;
- shifted/base-layout keys;
- multi-scalar associated text;
- pure associated-text frames producing ordinary text events;
- representative current functional-key families;
- F13 and F35 number mapping;
- unknown private-use functional key -> `Unrecognized`;
- canonical named control keys;
- fragmented CSI-u frames;
- coalesced modern-key plus traditional text;
- malformed event/modifier/scalar/associated-text recovery;
- retained traditional arrow decoding outside CSI-u.

## Deferred to later tranches

T172 deliberately does not:

- query or activate Kitty keyboard mode;
- add `KeyboardReportingMode` to input-protocol acquisition;
- manage Kitty push/pop stacks;
- decode xterm `modifyOtherKeys` conventional forms;
- claim raw/global fixterms ownership;
- add generic CSI keyboard APIs.

Next: T173 — negotiated Kitty keyboard ownership.
