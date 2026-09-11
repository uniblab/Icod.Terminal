# Icod.Terminal Public API Baseline — 1.10.0

**Release:** `1.10.0`  
**Status:** final C108 public API freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the final intentional additive public surface for `Icod.Terminal 1.10.0`. Historical baselines, including the final 1.9 baseline, remain unchanged.

## Final machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the final 1.10 fingerprint is:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.10.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates the public API snapshot independently for every supported target framework and verifies this exact fingerprint.

## Additive semantic capability model

Version 1.10 adds the curated public semantic capability vocabulary:

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

It also adds:

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

Construction is library-owned; callers receive immutable planning snapshots rather than manufacturing authoritative capability state.

## Side-effect-free session inspection

Version 1.10 adds:

```csharp
public TerminalCapabilityStatus InspectCapability(
    TerminalCapability capability
);
```

Inspection is synchronous and side-effect free. It projects the session's existing semantic evidence and routing state only; it emits no terminal bytes and performs no live probe.

Support knowledge and endpoint availability remain separate. A statically advertised output capability may remain `Advertised` while its endpoint is `Unavailable`, in which case `IsUsable` is false without rewriting truthful support knowledge to `Unsupported`.

## Explicit bounded verification

Version 1.10 also adds:

```csharp
public ValueTask<TerminalCapabilityStatus> VerifyCapabilityAsync(
    TerminalCapability capability,
    CancellationToken cancellationToken = default
);
```

Verification attempts to strengthen current support knowledge only where an existing reviewed bounded live probe exists. The initial probe paths are:

```text
KeyboardReporting
    Kitty keyboard support probe

RasterGraphics
    existing Kitty Graphics + Sixel probe orchestration
```

The operation returns without probing when the required endpoint is unavailable or current live support evidence is already decisive. Capabilities without a reviewed support probe return their current inspection status unchanged; the public API does not invent protocol traffic merely to make every capability probeable.

The method reuses the existing authoritative query coordinator and existing internally bounded probe deadlines while honoring caller cancellation.

## Lifecycle and hardening result

C105–C107 qualified the final surface for:

- live-evidence generation invalidation;
- static-evidence persistence;
- concurrent side-effect-free inspection;
- pre-cancelled verification with zero traffic;
- suspended/closed query ownership;
- unavailable endpoints with zero probe traffic;
- inspection-only capabilities without reviewed probes;
- repeated invalidation and repeated verification;
- multi-backend evidence where negative evidence for one backend does not erase an independent viable alternate;
- fresh NuGet-only consumption on `net8.0`, `net9.0`, and `net10.0`.

No additional public API was required by those tranches.

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

Version 1.10 is an additive minor release over the stable 1.0 compatibility floor. Existing public members and existing enum numeric values remain unchanged. The 1.10 additions are frozen by this baseline and the permanent capability-planning contract in `docs/Capability-Inspection-and-Planning.md`.
