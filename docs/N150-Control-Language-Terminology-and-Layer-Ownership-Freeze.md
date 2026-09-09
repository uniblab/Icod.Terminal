# N150 — Control-Language Terminology and Layer-Ownership Freeze

**Release:** `Icod.Terminal 1.5.0`  
**Tranche:** N150  
**Status:** implementation foundation  
**Public API change:** none required

## Naming

The repository already contains historical T150–T157 documents for the `0.15.0` OSC 133 extended-metadata release. The 1.5 normalization program therefore uses `N150`–`N159` so historical references remain unambiguous.

N150 is the requested 1.5 terminology/layer-ownership tranche.

## Purpose

N150 freezes the language used by the control-family normalization program before CSI, DCS/Sixel, APC/Kitty Graphics, or automatic semantic routing are expanded.

The central rule is:

> A semantic operation, protocol backend, control family, terminal dialect, support state, and evidence source are different concepts and must not be represented by one overloaded enum/string/Boolean.

## Semantic operation

A **semantic operation** describes caller intent without selecting an escape sequence.

Examples:

```text
DesktopNotification
CurrentLocation
ClipboardWrite
CursorStyle
RasterGraphics
```

A high-level consumer such as `Icod.DCurses` should reason about semantic operations.

It should not need to know whether the chosen backend is OSC 99, OSC 777, Sixel, Kitty Graphics, a TermInfo capability, or another reviewed implementation.

## Protocol backend

A **protocol backend** is one concrete implementation of a semantic operation.

Examples:

```text
Osc9Notification
Osc777Notification
Osc99KittyNotification
Osc7CurrentLocation
Osc52Clipboard
DecscusrCursorStyle
Sixel
KittyGraphics
TermInfoCapability
```

A backend may be portable, vendor-specific, capability-string-driven, stateful, query-capable, or output-only.

Backend identity does not by itself prove support.

## Control family

A **control family** is the outer framing grammar.

The normalized family vocabulary is:

```text
CSI
DCS
OSC
APC
PM
SOS
```

Ordinary one-character ESC functions and C0 controls remain outside this string-family enum rather than being forced into it.

### 7-bit introducers

```text
CSI  ESC [
DCS  ESC P
OSC  ESC ]
APC  ESC _
PM   ESC ^
SOS  ESC X
ST   ESC \
```

Recognized 8-bit C1 introducers may be accepted on input according to the decoder's framing contract. Canonical emission remains 7-bit unless an exact selected capability requires something else.

## Dialect

A **dialect** is an application grammar carried inside a control family.

Examples:

```text
OSC family -> OSC 99 Kitty notifications
OSC family -> OSC 633 VS Code shell integration
DCS family -> DECRQSS
DCS family -> XTGETTCAP
DCS family -> Sixel
APC family -> Kitty Graphics
CSI family -> Primary DA
CSI family -> DEC private modes
CSI family -> Kitty keyboard protocol
```

The family scanner must not contain dialect-specific assumptions.

### Explicit corrections

Sixel is DCS, not APC.

APC itself is not a key/value language. Kitty Graphics is an APC dialect that defines key/value control data and a Base64 payload.

CSI is not merely a list of decimal integers. Complete CSI structure preserves parameter bytes, intermediate bytes, and final byte, including private prefixes and colon subparameters.

## Support state

A **support state** describes the current confidence/result for one semantic backend or operation.

The 1.5 vocabulary is:

```text
Unavailable
Unsupported
Unknown
Advertised
Verified
```

### Meaning

- `Unavailable` — the operation cannot be attempted because required session/endpoint conditions do not exist;
- `Unsupported` — authoritative evidence establishes that the backend/operation is not supported for the relevant live context;
- `Unknown` — there is insufficient positive or negative evidence;
- `Advertised` — static description/profile evidence claims support but no live proof has established it;
- `Verified` — a successful live protocol observation or equivalent strong evidence confirms the capability for the current live context.

Timeout alone does not become `Unsupported` unless a protocol defines an explicit barrier/negative inference that makes that conclusion valid.

## Evidence source

An **evidence source** records why a support claim exists.

The initial vocabulary is:

```text
TermInfo
BuiltInProfile
LiveProbe
ProtocolResponse
```

A caller preference or forced backend selection is not evidence. It is routing policy.

## Ownership boundaries

### Icod.TermInfo owns

- immutable terminal descriptions;
- standard and extended capability values;
- capability expansion;
- static/built-in profile advertisement;
- terminfo/database acquisition.

It does not own live probing or route active terminal transactions.

### Icod.Terminal owns

- one authoritative live input reader;
- output serialization;
- query routing and response correlation;
- control-family framing;
- dialect codecs;
- live capability evidence;
- backend selection;
- session lifecycle and reversible state;
- typed wire-specific APIs already exposed by stable 1.x.

### Icod.DCurses and other high-level consumers own

- UI/application semantics;
- virtual screen/window/cell/pad policy;
- logical rendition and drawing;
- requests for semantic terminal behavior.

They do not create raw escape frames or second input readers.

## Existing explicit API compatibility

N150 does not reinterpret any existing public operation.

The following examples remain wire-specific by contract:

```text
SendNotificationAsync                  -> OSC 9
SendTitledNotificationAsync            -> OSC 777
SendKittyNotificationAsync             -> OSC 99
PublishCurrentLocationAsync            -> OSC 7
PublishWindowsCurrentDirectoryCompatibilityAsync -> OSC 9;9
VS Code methods                         -> OSC 633
ITerm2 methods                          -> OSC 1337
```

Future automatic semantic routing must use new APIs or internal high-level calls.

## Why a single SupportsKitty flag is forbidden

The name `Kitty` already spans unrelated control families:

```text
CSI     Kitty keyboard reporting
OSC 99  Kitty desktop notifications
APC     Kitty Graphics
```

Therefore `SupportsKitty` has no stable technical meaning.

Support must be represented per semantic operation/backend with evidence.

## Why an OSC-code capability model is forbidden

One OSC namespace can contain unrelated semantic operations. OSC 9 already contains notification, progress, and Windows current-directory compatibility forms.

Conversely, one semantic operation can have multiple OSC implementations, such as desktop notification through OSC 9, OSC 777, or OSC 99.

Therefore semantic routing cannot be keyed by OSC number.

## N150 internal code vocabulary

N150 introduces internal strongly typed terms for:

- `TerminalSemanticOperation`;
- `TerminalProtocolBackend`;
- `TerminalControlFamily`;
- `TerminalCapabilitySupportState`;
- `TerminalCapabilityEvidenceSource`.

These are intentionally internal. Their purpose is to prevent stringly typed architecture and to give N151–N159 a stable shared language without prematurely expanding the public API baseline.

## N150 acceptance tests

Tests SHALL verify:

- every enum member has a unique numeric value;
- every declared protocol backend classifies into one control family or explicitly into the non-control-family TermInfo path;
- representative currently supported backends map correctly;
- future Sixel maps to DCS;
- future Kitty Graphics maps to APC;
- no public API addition occurs merely from N150.

## Relationship to N151+

N150 freezes words and ownership only.

N151 generalizes family framing.

N152 introduces one incremental family scanner.

N153 preserves structural frame components.

N154 evolves query routing to multi-family transactions.

N155 gives support/evidence composition behavioral semantics.

N156/N157 build backend registry and routing policy.

N158 reconciles existing protocols and TermInfo capabilities.

N159 closes release acceptance.
