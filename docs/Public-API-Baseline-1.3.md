# Icod.Terminal Public API Baseline — 1.3.0

**Release:** `1.3.0`  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional additive public-surface change for `Icod.Terminal 1.3.0` while retaining the 1.0, 1.1, and 1.2 baselines as compatibility evidence.

Version 1.3 does not replace or reinterpret the stable 1.x contract. It adds a typed iTerm2 OSC 1337 shell-integration and semantic-history metadata surface while keeping OSC 1337 distinct from portable OSC 7 and OSC 133.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the 1.3 SHA-256 is:

```text
0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
```

The authoritative fingerprint is stored in:

`docs/Public-API-Baseline-1.3.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates all three snapshots, proves that they agree, and validates this current 1.3 fingerprint.

## Intentional 1.3 additions

The complete 1.3 public API delta is six additive `TerminalSession` methods:

```csharp
ValueTask SetITerm2MarkAsync(
	CancellationToken cancellationToken = default
);

ValueTask PublishITerm2CurrentDirectoryAsync(
	string currentDirectory,
	CancellationToken cancellationToken = default
);

ValueTask PublishITerm2RemoteHostAsync(
	string userName,
	string hostName,
	CancellationToken cancellationToken = default
);

ValueTask SetITerm2UserVariableAsync(
	string name,
	string value,
	CancellationToken cancellationToken = default
);

ValueTask PublishITerm2ShellIntegrationVersionAsync(
	int version,
	string shellName,
	CancellationToken cancellationToken = default
);

ValueTask ClearITerm2CapturedOutputAsync(
	CancellationToken cancellationToken = default
);
```

No public enum or public type is added or renumbered by this release.

## Compatibility anchors

The retained prior fingerprints remain unchanged:

```text
1.0  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
```

The 1.3 release therefore retains separate evidence for:

- the frozen 1.0 compatibility floor;
- the intentional OSC 633 additions introduced by 1.1;
- the intentional OSC 777 addition introduced by 1.2;
- the six intentional OSC 1337 additions introduced by 1.3;
- identical public API shape across `net8.0`, `net9.0`, and `net10.0`;
- unchanged existing public enum numeric values;
- continued absence of the removed pre-1.0 `TerminalSession.Input` property.

## Protocol boundary

OSC 1337 is a broad iTerm2 extension namespace. Version 1.3 exposes only the reviewed shell-integration and semantic-history metadata core:

```text
SetMark
CurrentDir
RemoteHost
SetUserVar
ShellIntegrationVersion=...;shell=...
ClearCapturedOutput
```

The public API deliberately does not add:

- a generic `WriteOsc1337Async(...)`, arbitrary command name, or raw parameter/payload API;
- profile mutation or dynamic-profile control;
- focus stealing;
- browser/URL opening;
- pasteboard reads/writes through OSC 1337;
- file upload/download or inline-file transfer;
- custom script control sequences;
- arbitrary color or cursor-shape mutation already owned by typed portable APIs;
- Unicode-version mutation;
- Touch Bar key-label state.

Portable OSC 7 remains the preferred current-location API. Portable OSC 133 remains the prompt/command-region API. The iTerm2 operations are independently explicit and are never emitted as automatic aliases or fallbacks.

## Baseline rule

Any later public-surface change must be deliberate under `docs/Compatibility-and-Versioning.md`. Accidental removals, renames, signature drift, enum renumbering, or target-framework divergence remain release blockers.
