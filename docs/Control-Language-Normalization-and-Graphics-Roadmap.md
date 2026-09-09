# Control-Language Normalization and Graphics Roadmap

**Project:** `Icod.Terminal`  
**Scope:** `1.5.0` through `1.8.0`  
**Compatibility floor:** stable `1.x`  
**Primary consumers:** direct `TerminalSession` users, `Icod.DCurses`, terminal-aware tools

## Purpose

The stable 1.x line already supports a growing set of terminal protocols including OSC 0/1/2, 4/104, 7, 8, 9, 22, 52, 99, 133, 633, 777, and 1337; CSI-based queries/input modes; DCS-based query paths; and terminfo-resolved output.

Before adding broad CSI, APC, Sixel, and Kitty Graphics support, the implementation needs one normalized control-language architecture so protocol families, dialects, capability evidence, routing policy, framing, and higher-level semantic intent do not become conflated.

This roadmap freezes the development order needed to reach that goal without changing the meaning of existing stable 1.x APIs.

## Core terminology

The architecture separates five layers:

```text
semantic intent
    -> capability/evidence resolution
    -> protocol backend selection
    -> control family framing
    -> dialect codec / wire transport
```

These terms are distinct:

- **semantic operation** — what the caller wants to accomplish, such as desktop notification, current-location publication, cursor style, clipboard write, or raster graphics;
- **protocol backend** — one concrete implementation of a semantic operation, such as OSC 99, OSC 777, terminfo `Ms`, DECSCUSR, Sixel, or Kitty Graphics;
- **control family** — the ECMA-48/terminal framing family: CSI, DCS, OSC, APC, PM, or SOS;
- **dialect** — the grammar carried by a family, such as Kitty Graphics inside APC or Sixel inside DCS;
- **capability evidence** — why a backend is believed usable, such as TermInfo advertisement, built-in profile advertisement, or a successful live protocol response.

Protocol number or terminal brand is never itself the semantic operation.

## Control-family correction

The expected framing families are:

```text
CSI  ESC [  ... final byte
DCS  ESC P  ... ST
OSC  ESC ]  ... ST / selected BEL compatibility
APC  ESC _  ... ST
PM   ESC ^  ... ST
SOS  ESC X  ... ST
ST   ESC \
```

Input may also receive corresponding 8-bit C1 introducers where the existing decoder contract permits them.

CSI is not merely a decimal-number list. A complete CSI grammar preserves parameter bytes, intermediate bytes, and the final byte so private prefixes, omitted parameters, colon subparameters, and future syntax are not lost.

APC is an application-defined string container, not inherently a key/value protocol. Kitty Graphics is one APC dialect whose payload begins with `G` and then carries comma-separated control key/value pairs plus a Base64 payload.

Sixel is a DCS dialect, not APC.

## Layer ownership

### Icod.TermInfo

`Icod.TermInfo` remains the immutable capability authority. It supplies:

- standard terminfo capability values;
- arbitrary extended capabilities;
- parameterized capability expansion;
- built-in and caller-selected terminal descriptions;
- static descriptive evidence.

It does not own the live terminal conversation, active probing, query correlation, protocol routing, or curses policy.

### Icod.Terminal

`Icod.Terminal` owns:

- the authoritative live input reader;
- control-family framing and dialect parsing/encoding;
- active protocol queries and response correlation;
- effective capability evidence;
- semantic backend routing;
- output serialization and committed multi-frame transactions;
- reversible/session-owned protocol state;
- exact wire-specific APIs already released in stable 1.x.

### Icod.DCurses and other high-level consumers

Higher layers should request semantic behavior. They must not choose OSC/CSI/DCS/APC codes, construct escape sequences, or create a second terminal reader.

For example, a future raster-capable curses layer asks to display a raster image. `Icod.Terminal` decides whether the selected backend is Sixel or Kitty Graphics.

## Compatibility rules

The normalization program SHALL preserve the meaning of all existing public 1.x methods.

In particular:

```text
SendNotificationAsync            remains OSC 9
SendTitledNotificationAsync      remains OSC 777
SendKittyNotificationAsync       remains OSC 99
PublishCurrentLocationAsync      remains OSC 7
VS Code OSC 633 methods          remain OSC 633
ITerm2 OSC 1337 methods          remain OSC 1337
```

Automatic semantic routing, when introduced, must use new APIs or internal high-level callers. Existing wire-explicit APIs are not silently reinterpreted.

## 1.5.0 — Normalization and control-language foundation

Version 1.5 establishes the broker needed by every later family:

1. terminology and layer-ownership freeze;
2. generalized control-family framing;
3. one bounded incremental control-language state machine;
4. structural CSI/DCS/string frame models;
5. multi-family query transactions and barrier responses;
6. effective capability support/evidence model;
7. semantic backend registry;
8. deterministic routing policy;
9. reconciliation of existing OSC/CSI/DCS/TermInfo implementations;
10. cross-family acceptance and public/package closure.

Version 1.5 should add little or no new wire protocol. Its purpose is to make later protocols fit cleanly.

## 1.6.0 — Complete CSI grammar and consolidation

Version 1.6 builds on the normalized framing core.

### Goals

- implement complete CSI parameter/intermediate/final grammar;
- retain private prefixes and colon subparameters structurally;
- migrate existing CSI query/mode emitters and response parsing onto the shared grammar;
- preserve current byte behavior for already released APIs;
- add semantic terminal/cell pixel geometry observation needed by graphics;
- fuzz fragmented, malformed, oversized, 7-bit, and 8-bit CSI inputs.

Candidate existing users include DA, DSR, CPR, DEC private modes, bracketed paste, focus, mouse reporting, synchronized output, Kitty keyboard negotiation, DECSCUSR, and other CSI-based state/query paths.

## 1.7.0 — DCS foundation and Sixel

Version 1.7 adds the first raster backend.

### DCS foundation

The shared DCS representation preserves:

```text
parameter bytes
intermediate bytes
final selector
application payload
ST termination
```

Existing DECRQSS and XTGETTCAP paths should be reconciled with the same DCS framing rather than maintaining separate ad-hoc framing logic.

### Sixel

Sixel is implemented as a DCS dialect and separate codec.

The tranche should provide:

- static and live capability evidence, including Primary DA Sixel evidence where available;
- a common raw raster model independent of image-file libraries;
- deterministic palette/quantization policy;
- six-row band encoding;
- Sixel run-length encoding;
- bounded dimensions, pixels, palette entries, and encoded transaction size;
- a committed streaming output transaction so large graphics do not require one giant intermediate allocation;
- a first common semantic raster-display operation.

The first raster model should favor raw formats such as RGB24, RGBA32, and indexed data rather than making PNG/JPEG decoding a dependency of `Icod.Terminal`.

## 1.8.0 — APC foundation and Kitty Graphics

Version 1.8 adds the second raster backend.

### APC foundation

APC framing remains generic application-defined string framing. Dialect parsing happens after a complete bounded APC frame is identified.

### Kitty Graphics

The first supported Kitty Graphics tranche should prefer direct data transmission:

```text
ESC _ G key=value,...;BASE64 ST
```

Initial support should include:

- direct transfer only;
- protocol-defined chunking;
- bounded raster payloads;
- explicit response correlation;
- a compound APC+CSI capability probe using a CSI barrier response;
- common-raster routing between Kitty Graphics and Sixel;
- no competing input reader.

Filesystem, temporary-file, and shared-memory transfer methods should remain excluded until separately security-reviewed.

Advanced Kitty-only features such as persistent image IDs, placements, deletion, z-order, and animation should remain separate typed extensions above the common raster operation.

## Effective capability model

A backend must not be represented by one simplistic Boolean.

The normalized model distinguishes support state:

```text
Unavailable
Unsupported
Unknown
Advertised
Verified
```

and evidence source:

```text
TermInfo
BuiltInProfile
LiveProbe
ProtocolResponse
```

An explicit caller preference is routing policy, not evidence.

A timeout is not automatically proof of unsupported behavior.

A known negative protocol response overrides weaker advertisement evidence. A successful correlated live response can produce verified evidence.

## Semantic backend examples

```text
DesktopNotification
    -> OSC 99
    -> OSC 777
    -> OSC 9

CurrentLocation
    -> OSC 7
    -> OSC 9;9 compatibility where explicitly applicable

ShellCurrentDirectoryMetadata
    -> OSC 633 Cwd
    -> OSC 1337 CurrentDir

ClipboardWrite
    -> exact TermInfo extended capability where semantically equivalent
    -> OSC 52

CursorStyle
    -> exact TermInfo capability where semantically equivalent
    -> DECSCUSR

RasterGraphics
    -> Kitty Graphics / APC
    -> Sixel / DCS
```

Not all similarly named operations are equivalent. Portable current location and vendor shell-integration metadata, for example, remain different semantic operations even though they may carry the same path.

## Resource-budget normalization

The family layer should centralize explicit ceilings for at least:

- CSI parameter bytes and parameter count;
- CSI numeric magnitude;
- OSC response/frame bytes;
- DCS header and response bytes;
- APC control bytes and response bytes;
- query transaction frame count and late-response retention;
- raster width, height, total pixels, raw bytes, and encoded transaction bytes;
- committed output transaction chunk size.

Graphics resource limits should be based on source pixels and transaction resources rather than reusing small OSC metadata bounds.

## Query transaction requirements

The current response router already owns CSI, DCS, and OSC response families. The normalized transaction model must be able to wait for more than one possible family in one logical query.

This is required by protocols such as Kitty Graphics where one transaction may complete from an APC response or from a CSI barrier indicating the APC response did not arrive before a known synchronization point.

One live `TerminalSession` continues to own one authoritative input reader.

## Framing-first rule

Every new protocol implementation follows this order:

1. frame the control family;
2. parse the family structure;
3. identify the dialect;
4. parse/encode the dialect;
5. map the result to semantic operations/evidence;
6. expose only reviewed typed APIs.

No Sixel parser reads directly from the terminal. No Kitty Graphics parser reads directly from the terminal. No future OSC/CSI/APC/DCS protocol creates a second reader.

## Testing strategy

Every control family must be tested with input split at every meaningful byte boundary, including introducer and ST boundaries.

Required classes of tests include:

- 7-bit and recognized 8-bit introducers;
- fragmented frames;
- maximum valid frame;
- one-byte-too-large frame;
- malformed or missing terminator;
- unknown dialect selector;
- unrelated control family while a query is active;
- late correlated response;
- cancellation before and after commitment;
- mixed text/CSI/OSC/DCS/APC streams;
- bounded recovery after malformed or oversized input.

Graphics additionally requires deterministic codec tests and transaction interleaving tests.

## Explicit exclusions

The normalization work must not introduce:

- public generic `WriteCsiAsync`, `WriteDcsAsync`, `WriteApcAsync`, or raw vendor dispatch;
- an `OscCode`-style public capability model;
- a single `SupportsKitty` Boolean spanning unrelated Kitty protocols;
- automatic terminal-brand claims treated as verified support;
- alternate terminal readers owned by individual protocols;
- hidden filesystem/network/browser/shared-memory side effects;
- silent reinterpretation of existing stable public methods.

## Completion condition

This roadmap is complete when a high-level consumer can request a semantic operation, `Icod.Terminal` can resolve effective support and choose an appropriate reviewed backend, and the caller does not need to know whether the emitted protocol is TermInfo, CSI, DCS, OSC, or APC.
