# Icod.Terminal Public API Baseline — 1.10.0 Development

**Release:** `1.10.0`  
**Status:** provisional C102 public API checkpoint; final freeze occurs at C108  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the current intentional additive public surface under development for `Icod.Terminal 1.10.0`. It is a development checkpoint, not the final release freeze. The final 1.10 public API baseline is frozen at C108 after capability inspection, verification, lifecycle, downstream acceptance, and hardening tranches are complete.

Historical baselines, including the final 1.9 baseline, remain unchanged.

## Current machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the C102 checkpoint fingerprint is:

```text
9551fbcd75f8f6854871b3b064150005e0c9c5d3da5ea436290100e6426e7d8c
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.10.sha256`

`packaging/VerifyPublicApiBaseline.ps1` uses that file as the current development baseline. The fingerprint is intentionally advanced only when a reviewed 1.10 tranche changes the public surface.

## C102 additive capability model

The current checkpoint adds the curated public semantic capability vocabulary:

```text
TerminalCapability
    ClipboardRead
    ClipboardWrite
    CursorStyle
    SynchronizedOutput
    KeyboardReporting
    MouseReporting
    FocusReporting
    BracketedPaste
    RasterGraphics
```

It also adds these public planning types:

```text
TerminalCapabilitySupport
    Unknown
    Unsupported
    Advertised
    Verified

TerminalCapabilityEndpointAvailability
    Unavailable
    Available

TerminalCapabilityEvidenceKind
    None
    StaticDescription
    LiveObservation

TerminalCapabilityStatus
```

`TerminalCapabilityStatus` exposes:

```csharp
public TerminalCapability Capability { get; }
public TerminalCapabilitySupport Support { get; }
public TerminalCapabilityEndpointAvailability EndpointAvailability { get; }
public TerminalCapabilityEvidenceKind EvidenceKind { get; }
public bool IsUsable { get; }
```

Construction is library-owned; the constructor is not public. This makes the type an inspection result rather than a caller-manufactured authority.

## Dependency and protocol neutrality

The public evidence vocabulary deliberately does not expose:

- `Icod.TermInfo` as an evidence identity;
- OSC, CSI, DCS, or APC framing;
- concrete protocol backends;
- terminal-brand heuristics;
- routing preference values;
- raw terminal capability names.

Static implementation evidence projects to `StaticDescription`; live probe/protocol evidence projects to `LiveObservation` in later integration tranches.

## Compatibility rule

Version 1.10 remains an additive minor release over the stable 1.0 compatibility floor. Existing public members and existing enum numeric values remain unchanged. New 1.10 enum values/types are subject to the normal forward-compatible minor-release rules until C108 freezes the final release baseline.
