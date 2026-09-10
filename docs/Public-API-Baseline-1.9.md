# Icod.Terminal Public API Baseline — 1.9.0 Development

**Release:** `1.9.0`  
**Checkpoint:** E191 semantic-event public envelope  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional additive public-surface change introduced by E191 while retaining all prior stable public API baselines as compatibility evidence.

Version 1.9 adds the minimum protocol-neutral public envelope required for unsolicited semantic terminal events before parser/routing implementation begins.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the E191 fingerprint is:

```text
96c7605b52e27a9f736d021e4924c7ac733786f6377acd6c28a76c58800ea702
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.9.sha256`

This is the current 1.9 development baseline, not a claim that the final E199 public surface is already frozen. If a later reviewed 1.9 tranche intentionally adds public members, this same 1.9 baseline must be regenerated and reviewed while historical 1.0–1.7 baselines remain untouched.

## Intentional E191 additions

E191 appends one value to the existing outer event enum:

```text
TerminalEventKind.Semantic = 4
```

The prior numeric values remain:

```text
Input      = 0
Lifecycle  = 1
Timeout    = 2
Cancelled  = 3
```

E191 adds a nullable semantic payload to `TerminalEvent`:

```csharp
public TerminalSemanticEvent? Semantic { get; }
```

It also adds these public types:

```text
TerminalSemanticEventKind
TerminalSemanticEvent
TerminalNotificationEventKind
TerminalNotificationEvent
```

The semantic envelope currently exposes one reviewed family:

```text
TerminalSemanticEventKind.Notification
```

The first notification event kinds are:

```text
Activated
ButtonActivated
Closed
CloseTrackingUnavailable
```

`TerminalNotificationEvent` exposes:

```csharp
public TerminalNotificationEventKind Kind { get; }
public string Identifier { get; }
public int? ButtonNumber { get; }
```

`ButtonNumber` is present only for `ButtonActivated` and is one-based. `CloseTrackingUnavailable` remains distinct from `Closed` so the public model preserves the terminal's uncertainty rather than fabricating a close event.

## Deliberate exclusions

E191 does not expose:

- raw OSC frames;
- raw protocol selectors;
- arbitrary metadata dictionaries;
- protocol-backend identifiers;
- a second `ReadSemanticEventAsync(...)` method;
- public event factories intended for applications to synthesize terminal input;
- parser/routing behavior;
- automatic notification state tracking.

The internal factories exist only so the authoritative decoder/coordinator can construct validated event objects in later tranches.

## Compatibility rule

E191 is additive. Existing public members are retained and existing enum numeric values are unchanged.

Consumers using exhaustive switches over `TerminalEventKind` must nevertheless be prepared for the new `Semantic` case when they adopt the 1.9 package, as with any additive public enum expansion.

Final 1.9 release closure remains E199. The exact E199 public surface must be regenerated, reviewed, and recorded before stable promotion.
