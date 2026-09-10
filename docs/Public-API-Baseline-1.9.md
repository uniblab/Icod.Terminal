# Icod.Terminal Public API Baseline — 1.9.0

**Release:** `1.9.0`  
**Status:** final E199 public API freeze  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the final intentional additive public surface for `Icod.Terminal 1.9.0` while retaining every earlier stable public API baseline as compatibility evidence.

Version 1.9 adds a protocol-neutral semantic-event envelope for unsolicited terminal observations and extends the existing typed Kitty OSC 99 notification options with explicit interactive-report controls. E196–E198 add projection, hardening, package, sample, and lifecycle evidence without adding further public members after E195.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the final 1.9 fingerprint is:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

The machine-readable fingerprint is stored in:

`docs/Public-API-Baseline-1.9.sha256`

`packaging/VerifyPublicApiBaseline.ps1` uses that file as the current baseline. Historical baselines remain checked in unchanged.

## Intentional semantic-event additions

Version 1.9 appends one value to the existing outer event enum:

```text
TerminalEventKind.Semantic = 4
```

The prior numeric values remain unchanged:

```text
Input      = 0
Lifecycle  = 1
Timeout    = 2
Cancelled  = 3
```

`TerminalEvent` gains:

```csharp
public TerminalSemanticEvent? Semantic { get; }
```

and the release adds these public semantic types:

```text
TerminalSemanticEventKind
TerminalSemanticEvent
TerminalNotificationEventKind
TerminalNotificationEvent
```

The first semantic family is:

```text
TerminalSemanticEventKind.Notification
```

The first notification-event values are:

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

`ButtonNumber` is present only for `ButtonActivated` and is one-based. `CloseTrackingUnavailable` is distinct from `Closed` so the public API represents terminal uncertainty truthfully.

Semantic events arrive through the existing `TerminalSession.ReadEventAsync(...)` path. No second public semantic reader is added.

## Intentional interactive-notification additions

`KittyNotificationOptions` gains:

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

Interactive reporting requires an explicit caller-supplied notification identifier. `FocusOnActivation` remains independent of reporting. Button labels are exposed semantically; U+2028 separation, Base64 framing, and resource limits remain implementation/protocol details rather than public wire APIs.

## Behavioral compatibility attached to the API

The additive surface participates in the established 1.x contracts:

- active query ownership precedes unsolicited semantic recognition;
- recognized semantic reports precede ordinary application-input decoding;
- no frame is double-delivered as a query response and semantic event;
- input and semantic events share one bounded ordered application-event domain;
- malformed/oversized owned reports recover boundedly and do not leak into ordinary text;
- caller wait cancellation/timeout does not discard fragmented decoder state;
- notification reports are validated but unauthenticated terminal-controlled input;
- notification requests are not retained as reversible session state and are not replayed on resume or automatically closed on disposal.

## Deliberate exclusions

The 1.9 public surface does not expose:

- raw OSC frames or selector dictionaries;
- arbitrary vendor-event payloads;
- protocol-backend identifiers on semantic events;
- a second `ReadSemanticEventAsync(...)` method;
- public terminal-event factories for application synthesis;
- a persistent notification database;
- authenticated notification identities;
- persistent raster resources/placements or graphics scene state.

## Compatibility rule

Version 1.9 is an additive minor release. Existing public members remain present and existing enum numeric values are unchanged.

Consumers using exhaustive switches over `TerminalEventKind` should handle the new `Semantic` value when adopting 1.9. Existing Kitty notification calls retain their prior behavior when the new interactive options are unused.

Stable `1.0.0` remains the compatibility floor. The permanent authority is `docs/Compatibility-and-Versioning.md`.
