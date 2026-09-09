# Icod.Terminal Public API Baseline — 1.4.0

**Release:** `1.4.0`  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional additive public-surface change for `Icod.Terminal 1.4.0` while retaining the 1.0 through 1.3 baselines as compatibility evidence.

Version 1.4 adds a typed Kitty OSC 99 desktop-notification surface with bounded send/update/close semantics and explicit capability/alive queries. Existing OSC 9 and OSC 777 notification APIs remain unchanged and independent.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the 1.4 SHA-256 is:

```text
3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
```

The authoritative fingerprint is stored in:

`docs/Public-API-Baseline-1.4.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates all three snapshots, proves that they agree, and validates this current 1.4 fingerprint.

## Intentional 1.4 additions

Version 1.4 adds these public types:

```text
KittyNotificationOptions
KittyNotificationSupport
KittyNotificationOccasion
KittyNotificationUrgency
```

and these additive `TerminalSession` methods:

```csharp
ValueTask SendKittyNotificationAsync(
	string title,
	string body = "",
	KittyNotificationOptions? options = null,
	CancellationToken cancellationToken = default
);

ValueTask CloseKittyNotificationAsync(
	string identifier,
	CancellationToken cancellationToken = default
);

ValueTask<KittyNotificationSupport> QueryKittyNotificationSupportAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

ValueTask<IReadOnlyList<string>> QueryKittyAliveNotificationsAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

`KittyNotificationOptions` exposes typed notification identity/update, filtering, focus policy, occasion, urgency, expiration, sound, icon-name, and transmitted/cached-icon metadata. `KittyNotificationSupport` exposes the terminal's correlated capability report without converting timeout into unsupported truth.

Two new closed enums represent Kitty-defined occasion and urgency values. Existing public enum values are unchanged.

## Compatibility anchors

The retained prior fingerprints remain unchanged:

```text
1.0  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
1.3  0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
```

Version 1.4 preserves all prior public members and protocol behavior while adding the reviewed OSC 99 surface.

## Protocol boundary

Kitty OSC 99 supports more than request/response notification emission. Version 1.4 includes:

- title and body payloads;
- automatic bounded chunking;
- stable identifiers for update/replacement;
- explicit close by identifier;
- application/type filtering metadata;
- focus-action suppression;
- occasion, urgency, expiration, and sound metadata;
- icon-name lookup;
- bounded PNG/JPEG/GIF icon transfer/cache identifiers;
- explicit capability query;
- explicit alive-notification query.

The 1.4 public API deliberately does **not** add:

- a generic raw OSC 99 dispatcher;
- arbitrary metadata-key injection;
- buttons;
- activation-event reports;
- close-event reports;
- a second raw input reader or protocol-specific unsolicited-event loop;
- automatic terminal-brand support inference;
- automatic fallback/routing between OSC 9, OSC 777, and OSC 99;
- host-native desktop notification calls.

Buttons and activation/close reports are unsolicited terminal input. They require a separate reviewed `TerminalEvent` routing contract so they cannot steal or bypass the session's authoritative input stream.

## Baseline rule

Any later public-surface change must be deliberate under `docs/Compatibility-and-Versioning.md`. Accidental removals, renames, signature drift, enum renumbering, or target-framework divergence remain release blockers.
