# Icod.Terminal Public API Baseline — 1.0.0

**Release:** `1.0.0`  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document is the stable public source/binary contract for `Icod.Terminal` 1.x.

Stable `1.0.0` intentionally adopts the exact public surface qualified by `1.0.0-rc1`. The rc1 baseline remains checked in as historical qualification evidence; this document is the permanent stable authority.

The baseline is machine generated from the built assembly rather than reconstructed from historical release notes. It covers exported public types and members, method/constructor signatures, property accessors including `init`, nullability, optional/default parameter values, implemented public interfaces, constants where represented by the snapshot format, generic constraints represented by the snapshot format, and every public enum numeric value.

The permanent behavioral contract is documented separately in the 1.x architecture, ownership, lifecycle, input/query, presentation/output, and security documents. This baseline freezes API shape; those documents freeze supported semantics.

## Exact machine fingerprint

The deterministic reflection snapshot contains:

```text
633 lines
77 exported public types
32 public enums
242 public enum values
124 public methods
144 public properties
13 public constructors
```

After normalizing line endings to LF, the stable SHA-256 is:

```text
8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

The authoritative fingerprint is stored in:

`docs/Public-API-Baseline-1.0.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the snapshot independently for net8.0, net9.0, and net10.0, first proves the TFMs agree with one another, and then proves the normalized fingerprint matches this stable value.

A future intentional public-surface change therefore cannot pass validation merely because every TFM changed in the same way; the checked-in 1.x baseline must also be reviewed and deliberately updated.

## Deliberate pre-1.0 correction

T190 found one D-class regret correction before the 1.0 freeze:

```text
TerminalSession.Input
```

is no longer public.

`ITerminalInput` remains a public custom-transport injection seam, and the custom `TerminalSession.OpenAsync(...)` overload still accepts caller-supplied `ITerminalInput` and `ITerminalOutput` implementations. Once a session is live, however, input is owned by one authoritative decoder/query-router path through `ReadEventAsync(...)` and typed query methods. Returning the raw input transport would allow a second reader to steal fragmented input or correlated responses.

`TerminalSession.Output` remains public as an advanced borrowed transport. Direct writes through it are outside session serialization and are caller-coordinated; ordinary consumers should prefer session-managed semantic output APIs.

This is the only intentional public removal between the published 0.18 surface and stable 1.0.

## Enum numeric-value freeze

Every public enum value is included in the machine fingerprint. This is the authoritative 1.x numeric baseline, including values that evolved during early pre-1.0 development.

Particularly important compatibility anchors include:

```text
TerminalInputEventKind
  Text       = 0
  Key        = 1
  Mouse      = 2
  Focus      = 3
  Paste      = 4
  EndOfInput = 5

TerminalEventKind
  Input     = 0
  Lifecycle = 1
  Timeout   = 2
  Cancelled = 3

TerminalKeyEventPhase
  Press   = 0
  Repeat  = 1
  Release = 2

TerminalKeyboardReportingMode
  Disambiguated = 0
  EventTypes    = 1
  AllKeys       = 2

TerminalKeyModifiers
  None     = 0
  Shift    = 1
  Control  = 2
  Alt      = 4
  Super    = 8
  Hyper    = 16
  Meta     = 32
  CapsLock = 64
  NumLock  = 128
```

The fingerprint also covers the complete `TerminalKey` vocabulary, all pointer/cursor/color/lifecycle/status enums, and every other exported enum. Adding a member in the middle of an implicitly numbered enum, renumbering an existing member, or changing a flags value changes the snapshot and fails the gate.

## What the baseline does not mean

Freezing a signature does not imply that every terminal supports the represented protocol operation.

The 1.x API continues to distinguish:

- API availability from terminal-side protocol support;
- successful emission from terminal-side application;
- timeout from unsupported behavior;
- terminal-policy reset from exact restoration;
- logical ownership from believed physical state.

Those semantics are defined by the permanent 1.x contract documents.

## Compatibility rule

After stable 1.0, accidental removal, renaming, signature drift, enum renumbering, or TFM-specific public-surface divergence is a release defect.

Any intentional public-surface change must:

1. be justified under the compatibility/versioning policy;
2. update the machine fingerprint in review;
3. update this baseline and affected XML/permanent documentation;
4. add migration guidance when source or binary compatibility changes;
5. pass fresh package/downstream validation on all supported TFMs.

The stable fingerprint is the starting compatibility authority for the 1.x line.
