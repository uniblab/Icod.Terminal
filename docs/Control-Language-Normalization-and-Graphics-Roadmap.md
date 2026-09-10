# Control-Language Normalization and Graphics Roadmap

**Project:** `Icod.Terminal`  
**Scope:** `1.5.0` through `1.8.0`  
**Compatibility floor:** stable `1.x`  
**Primary consumers:** direct `TerminalSession` users, `Icod.DCurses`, terminal-aware tools  
**Status:** normalization and first two raster backends complete through 1.8

## Purpose

The stable 1.x line supports a growing set of terminal protocols including OSC 0/1/2, 4/104, 7, 8, 9, 22, 52, 99, 133, 633, 777, and 1337; CSI-based queries/input modes; DCS-based query paths; terminfo-resolved output; Sixel over DCS; and Kitty Graphics over APC.

Versions 1.5–1.8 deliberately developed one normalized control-language architecture so protocol families, dialects, capability evidence, routing policy, framing, and higher-level semantic intent do not become conflated.

This document records the completed architectural progression and the stable rules later protocol work must preserve.

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

## Control-family model

The normalized framing families are:

```text
CSI  ESC [  ... final byte
DCS  ESC P  ... ST
OSC  ESC ]  ... ST / selected BEL compatibility
APC  ESC _  ... ST
PM   ESC ^  ... ST
SOS  ESC X  ... ST
ST   ESC \
```

Input may also receive corresponding reviewed 8-bit C1 introducers/terminators where the decoder contract permits them. Canonical library-generated DCS/APC output remains seven-bit.

CSI is not merely a decimal-number list. Its grammar preserves parameter bytes, intermediate bytes, and the final byte so private prefixes, omitted parameters, colon subparameters, and future syntax are not lost.

APC is an application-defined string container, not inherently a key/value protocol. Kitty Graphics is one APC dialect whose payload begins with `G` and then carries reviewed comma-separated control data plus optional Base64 payload.

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

Higher layers should request semantic behavior. They must not need to choose OSC/CSI/DCS/APC codes, construct escape sequences, or create a second terminal reader.

Raster-capable callers ask `Icod.Terminal` to display a `TerminalRasterImage`. The live session resolves whether verified Kitty Graphics or verified Sixel should implement that semantic intent.

## Compatibility rules

The normalization program preserves the meaning of all existing public 1.x methods.

In particular:

```text
SendNotificationAsync            remains OSC 9
SendTitledNotificationAsync      remains OSC 777
SendKittyNotificationAsync       remains OSC 99
PublishCurrentLocationAsync      remains OSC 7
VS Code OSC 633 methods          remain OSC 633
ITerm2 OSC 1337 methods          remain OSC 1337
DisplayRasterAsync               remains backend-neutral raster intent
```

Automatic semantic routing uses internal high-level routing or APIs explicitly designed for semantic behavior. Existing wire-explicit APIs are not silently reinterpreted.

## 1.5.0 — normalization and control-language foundation

Version 1.5 completed the broker used by later families:

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

Its purpose was architectural normalization rather than a large new public protocol surface.

## 1.6.0 — complete CSI grammar and consolidation

Version 1.6 completed the first family-specific program on the normalized core:

- complete CSI parameter/intermediate/final grammar;
- private prefixes and colon subparameters retained structurally;
- existing CSI query/mode emitters and response parsing migrated to the common grammar while preserving released bytes;
- semantic terminal/cell pixel geometry observation added internally for later graphics work;
- fragmented, malformed, oversized, seven-bit, and eight-bit CSI behavior hardened.

Existing users include DA, DSR, CPR, DEC private modes, bracketed paste, focus, mouse reporting, synchronized output, Kitty keyboard negotiation, DECSCUSR, and other CSI-based state/query paths.

## 1.7.0 — DCS foundation and Sixel

Version 1.7 added the first raster backend.

### DCS foundation

The shared DCS representation preserves:

```text
parameter bytes
intermediate bytes
final selector
application payload
ST termination
```

Existing DECRQSS and XTGETTCAP construction were reconciled with the common DCS framing layer without changing their released bytes or response semantics.

### Sixel

Sixel is implemented as a DCS dialect with its own deterministic codec.

Completed 1.7 work includes:

- static/live capability evidence including Primary DA Sixel evidence;
- public common raw raster model independent of image-file libraries;
- deterministic bounded palette/quantization policy;
- six-row band encoding and Sixel repeat encoding;
- bounded dimensions, pixels, palette entries, and work state;
- committed streaming output so large graphics do not require one giant encoded DCS allocation;
- the public `TerminalSession.DisplayRasterAsync(...)` semantic raster operation.

The common model supports RGB24, RGBA32, and Indexed8 raw data rather than making PNG/JPEG decoding a dependency of `Icod.Terminal`.

## 1.8.0 — APC foundation and Kitty Graphics

Version 1.8 adds the second raster backend beneath the unchanged 1.7 public raster API.

### APC foundation

APC framing remains generic application-defined string framing. `ApcWriter` constructs canonical bounded seven-bit frames. Kitty dialect parsing/encoding happens above that layer.

### Kitty Graphics common-raster subset

The completed 1.8 implementation uses direct data transmission:

```text
ESC _ G key=value,...;BASE64 ST
```

It includes:

- direct transfer (`t=d`) only;
- raw RGB24 (`f=24`) and RGBA32 (`f=32`) transmission;
- deterministic Indexed8 expansion to RGB24/RGBA32 according to alpha;
- protocol-defined continuation chunking;
- Base64 image-data chunks no larger than 4096 bytes;
- one committed session-output transaction across the complete multi-frame direct transfer;
- explicit image-id response correlation;
- the protocol-defined Kitty query plus Primary DA barrier;
- deterministic common-raster routing between verified Kitty Graphics and verified Sixel;
- no competing input reader;
- adversarial fragmentation, malformed/oversized correlation, late-response, lifecycle-evidence, and subsequent-query hardening.

Filesystem, temporary-file, and shared-memory transfer methods remain excluded from 1.8.

Advanced Kitty-only features such as persistent image IDs, placements, deletion, z-order, Unicode placeholders, source rectangles, and animation remain separate potential typed extensions rather than properties silently added to the common raster method.

## Effective capability model

A backend is not represented by one simplistic Boolean.

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

For Sixel, Primary DA parameter `4` is positive verified evidence; absence is not treated as an authoritative negative in the compatibility posture used by the library.

For Kitty Graphics, the protocol-defined support query is paired with a following Primary DA barrier. A correlated Kitty response verifies support. Primary DA arriving first supplies reviewed negative protocol-response evidence for that concrete Kitty probe. Silence before either result remains unknown.

A known negative protocol result can override weaker advertisement evidence. A successful correlated live response can produce verified evidence.

## Correlation and ownership

The 1.8 Kitty probe demonstrates an important distinction between correlation and trust.

Once a recognizable APC contains a complete matching `i=<probe-id>` field, it is transaction-owned by that probe even if later framing is malformed, aborted, oversized, or unterminated. This prevents identified response bytes from escaping into ordinary input merely because the peer becomes hostile after correlation.

The response still remains untrusted and bounded. Correlation never bypasses grammar, numeric, frame-size, or resynchronization limits.

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
    -> verified Kitty Graphics / APC
    -> verified Sixel / DCS
```

Not all similarly named operations are equivalent. Portable current location and vendor shell-integration metadata, for example, remain different semantic operations even though they may carry the same path.

## Resource-budget normalization

The family/dialect layers centralize explicit ceilings for areas including:

- CSI parameter bytes and parameter count;
- CSI numeric magnitude;
- OSC response/frame bytes;
- DCS header and response bytes;
- APC frame/control/response bytes;
- query transaction frame count and late-response retention;
- raster width, height, total pixels, raw bytes, and palette entries;
- graphics encoded segment/chunk size;
- bounded malformed/oversized response resynchronization.

Graphics resource limits are based on source pixels and transaction resources rather than reusing small metadata payload assumptions.

## Query transaction requirements

The response router owns CSI, DCS, OSC, APC, and other normalized control-family traffic through one input coordinator.

The transaction model can combine a primary expected response with protocol-defined barrier semantics or side-observed correlation. Kitty Graphics uses this to distinguish a matching APC response from a following CSI Primary DA barrier without creating a second terminal reader.

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

Every control family is tested with input split at meaningful byte boundaries, including introducer and ST boundaries.

Required classes of tests include:

- seven-bit and recognized eight-bit introducers;
- fragmented frames;
- maximum valid frame;
- one-byte-too-large/oversized frame;
- malformed or missing terminator;
- unknown dialect selector;
- unrelated control family while a query is active;
- late correlated response;
- cancellation before and after commitment;
- mixed text/CSI/OSC/DCS/APC streams;
- bounded recovery after malformed or oversized input.

Graphics additionally requires deterministic codec tests, transaction interleaving tests, backend-selection tests, capability-evidence tests, and fresh packed-consumer compatibility tests.

## Explicit exclusions

The normalization work does not introduce:

- public generic `WriteCsiAsync`, `WriteDcsAsync`, `WriteApcAsync`, or raw vendor dispatch;
- an `OscCode`-style public capability model;
- a single `SupportsKitty` Boolean spanning unrelated Kitty protocols;
- automatic terminal-brand claims treated as verified support;
- alternate terminal readers owned by individual protocols;
- hidden filesystem/network/browser/shared-memory side effects;
- silent reinterpretation of existing stable public methods;
- automatic backend replay after partial committed graphics output.

## Completion condition

The 1.5–1.8 normalization/graphics roadmap reaches its intended completion in 1.8: a high-level consumer can request backend-neutral raster display, `Icod.Terminal` can resolve effective live evidence and choose between reviewed Kitty Graphics/APC and Sixel/DCS backends, and the caller does not need to construct either wire protocol.

Future protocol/back-end work must preserve the same semantic/framing/evidence/ownership separation rather than reopening the normalized architecture.
