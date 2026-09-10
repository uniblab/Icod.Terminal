# Icod.Terminal Public API Baseline — 1.10.0 Development

**Release:** `1.10.0`  
**Status:** provisional C104 public API checkpoint; final freeze occurs at C108  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the current intentional additive public surface under development for `Icod.Terminal 1.10.0`. It is a development checkpoint, not the final release freeze. The final 1.10 public API baseline is frozen at C108 after capability inspection, verification, lifecycle, downstream acceptance, and hardening tranches are complete.

Historical baselines, including the final 1.9 baseline, remain unchanged.

## Current machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the C104 checkpoint fingerprint is:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.10.sha256`

`packaging/VerifyPublicApiBaseline.ps1` uses that file as the current development baseline. The fingerprint is intentionally advanced only when a reviewed 1.10 tranche changes the public surface.

## C102 additive capability model

The current checkpoint retains the curated public semantic capability vocabulary:

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

It also retains these public planning types:

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

## C103 side-effect-free session inspection

C103 adds:

```csharp
public TerminalCapabilityStatus InspectCapability(
    TerminalCapability capability
);
```

The operation is synchronous and side-effect free. It projects the session's existing semantic evidence and routing state only; it does not emit terminal bytes or perform a live probe.

Support knowledge and endpoint availability remain separate. A statically advertised output capability may remain `Advertised` while its endpoint is `Unavailable`, in which case `IsUsable` is false without rewriting the support answer to `Unsupported`.

## C104 explicit bounded verification

C104 adds:

```csharp
public ValueTask<TerminalCapabilityStatus> VerifyCapabilityAsync(
    TerminalCapability capability,
    CancellationToken cancellationToken = default
);
```

Verification attempts to strengthen current support knowledge only where an existing reviewed bounded live probe exists. In the initial 1.10 vocabulary those probe paths are:

```text
KeyboardReporting
    Kitty keyboard support probe

RasterGraphics
    existing Kitty Graphics + Sixel probe orchestration
```

The operation returns immediately without probing when the required endpoint is unavailable or current live support evidence is already decisive. Capabilities without a reviewed support probe return their current inspection status unchanged; the public API does not invent protocol traffic merely to turn an unknown answer into a different state.

The method reuses the existing authoritative query coordinator and existing internally bounded probe deadlines while honoring caller cancellation.

## Dependency and protocol neutrality

The public planning surface deliberately does not expose:

- `Icod.TermInfo` as an evidence identity;
- OSC, CSI, DCS, or APC framing;
- concrete protocol backends;
- terminal-brand heuristics;
- routing preference values;
- raw terminal capability names.

Static implementation evidence projects to `StaticDescription`; current lifecycle-generation live probe/protocol evidence projects to `LiveObservation`.

## Compatibility rule

Version 1.10 remains an additive minor release over the stable 1.0 compatibility floor. Existing public members and existing enum numeric values remain unchanged. New 1.10 enum values/types are subject to the normal forward-compatible minor-release rules until C108 freezes the final release baseline.
