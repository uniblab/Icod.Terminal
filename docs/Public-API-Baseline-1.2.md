# Icod.Terminal Public API Baseline — 1.2.0

**Release:** `1.2.0`  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional additive public-surface change for `Icod.Terminal 1.2.0` while retaining the 1.0 and 1.1 baselines as compatibility evidence.

Version 1.2 does not replace or reinterpret the stable 1.x contract. It adds one typed OSC 777 titled desktop-notification operation under the existing compatibility/versioning policy.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the 1.2 SHA-256 is:

```text
dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
```

The authoritative fingerprint is stored in:

`docs/Public-API-Baseline-1.2.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates all three snapshots, proves that they agree, and validates this current 1.2 fingerprint.

## Intentional 1.2 addition

The complete 1.2 public API delta is exactly one additive `TerminalSession` method:

```csharp
ValueTask SendTitledNotificationAsync(
	string title,
	string message,
	CancellationToken cancellationToken = default
);
```

The method exposes bounded urxvt-style OSC 777 titled desktop notifications while keeping protocol selectors/framing internal.

No public enum or public type is added or renumbered by this release.

## Compatibility anchors

The stable 1.0 fingerprint remains checked in unchanged:

```text
8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

The intentional 1.1 additive fingerprint also remains checked in unchanged:

```text
455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
```

The 1.2 release therefore retains separate evidence for:

- the frozen 1.0 compatibility floor;
- the intentional OSC 633 additions introduced by 1.1;
- the single intentional OSC 777 addition introduced by 1.2;
- identical public API shape across `net8.0`, `net9.0`, and `net10.0`;
- unchanged existing public enum numeric values;
- continued absence of the removed pre-1.0 `TerminalSession.Input` property.

## Protocol boundary

Version 1.2 does not add:

- a generic `WriteOsc777Async(...)` or arbitrary command/payload API;
- raw OSC 777 selectors or field arrays;
- automatic routing/fallback between OSC 9, OSC 777, or another notification protocol;
- host-native notification process execution;
- notification IDs, actions, activation callbacks, icons, urgency, timeout, or replacement semantics.

Those richer semantics require separate protocol review rather than widening OSC 777 into a raw vendor-command escape hatch.

## Baseline rule

Any later public-surface change must be deliberate under `docs/Compatibility-and-Versioning.md`. Accidental removals, renames, signature drift, enum renumbering, or target-framework divergence remain release blockers.
