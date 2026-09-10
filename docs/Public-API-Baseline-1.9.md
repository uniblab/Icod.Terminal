# Icod.Terminal Public API Baseline — 1.9.0 Development

**Release:** `1.9.0`  
**Checkpoint:** E195 interactive Kitty notification requests  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the current intentional additive public surface for 1.9 development while retaining all prior stable public API baselines as compatibility evidence.

E191 introduced the protocol-neutral semantic-event envelope used by unsolicited terminal events. E195 adds the request-side options needed for applications to explicitly ask Kitty OSC 99 for activation/button and close reports and to supply button labels. The parser, routing, and request-side changes remain additive to the stable 1.x API.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the E195 fingerprint is:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.9.sha256`

This is the current 1.9 development baseline, not a claim that the final E199 public surface is already frozen. If a later reviewed 1.9 tranche intentionally adds public members, this same 1.9 baseline must be regenerated and reviewed while all historical stable baselines remain untouched.

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

## Intentional E195 additions

E195 extends `KittyNotificationOptions` with three opt-in request properties:

```csharp
public bool ReportActivation { get; init; }
public bool ReportClose { get; init; }
public IReadOnlyList<string> Buttons { get; init; }
```

The defaults preserve existing noninteractive behavior:

```text
ReportActivation = false
ReportClose      = false
Buttons          = empty
```

`ReportActivation` requests activation/button reports. `ReportClose` requests close-state reports. `Buttons` carries ordered button labels for a supporting Kitty OSC 99 terminal.

Interactive reporting requires the caller to supply an explicit `KittyNotificationOptions.Identifier`; internally generated multipart identifiers never become application correlation identities. `FocusOnActivation` remains independent of activation reporting, so callers can request reports while retaining or suppressing the default focus action explicitly.

Button transport is validated and bounded internally. The public API deliberately exposes labels rather than protocol separators, Base64 framing, raw OSC metadata, or parser limits.

## Deliberate exclusions

The current 1.9 public surface does not expose:

- raw OSC frames;
- raw protocol selectors;
- arbitrary metadata dictionaries;
- protocol-backend identifiers on semantic events;
- a second `ReadSemanticEventAsync(...)` method;
- public event factories intended for applications to synthesize terminal input;
- a notification-state database;
- authenticated notification identities;
- protocol framing or Base64 details for button payloads.

Semantic events continue to arrive through the authoritative `TerminalSession.ReadEventAsync(...)` path.

## Compatibility rule

The 1.9 changes recorded here are additive. Existing public members are retained and existing enum numeric values are unchanged.

Consumers using exhaustive switches over `TerminalEventKind` must nevertheless be prepared for the new `Semantic` case when they adopt the 1.9 package, as with any additive public enum expansion.

Existing Kitty notification calls remain byte-compatible when the E195 interactive options are unused. Interactive reporting is explicit opt-in and does not change the meaning of successful send completion: completion proves request emission, not that a desktop notification was displayed or that a later terminal report is authentic.

Final 1.9 release closure remains E199. The exact E199 public surface must be regenerated, reviewed, and recorded before stable promotion.
