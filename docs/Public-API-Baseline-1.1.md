# Icod.Terminal Public API Baseline — 1.1.0

**Release:** `1.1.0`  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional additive public-surface change for `Icod.Terminal 1.1.0` while retaining `docs/Public-API-Baseline-1.0.md` and `docs/Public-API-Baseline-1.0.sha256` as the stable compatibility-floor evidence.

Version 1.1 does not replace or reinterpret the 1.0 contract. It adds a typed VS Code OSC 633 shell-integration surface under the stable 1.x compatibility policy.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the 1.1 SHA-256 is:

```text
455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
```

The authoritative fingerprint is stored in:

`docs/Public-API-Baseline-1.1.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates all three snapshots, proves that they agree, and then validates this current 1.1 fingerprint.

## Intentional 1.1 additions

The 1.1 public API delta consists of these additive `TerminalSession` methods:

```csharp
ValueTask BeginVsCodePromptAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginVsCodeCommandInputAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginVsCodeCommandOutputAsync(
	CancellationToken cancellationToken = default
);

ValueTask FinishVsCodeCommandAsync(
	int exitCode,
	CancellationToken cancellationToken = default
);

ValueTask AbortVsCodeCommandAsync(
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeCommandLineAsync(
	string commandLine,
	string? nonce = null,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeCurrentDirectoryAsync(
	string currentDirectory,
	string? nonce = null,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeIsWindowsAsync(
	bool isWindows,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeContinuationPromptAsync(
	string continuationPrompt,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeRichCommandDetectionAsync(
	bool hasRichCommandDetection,
	CancellationToken cancellationToken = default
);
```

These methods expose documented VS Code OSC 633 semantic operations while keeping raw marker/property dispatch internal.

## Compatibility anchors

The stable 1.0 fingerprint remains checked in unchanged:

```text
8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

The 1.1 release therefore retains separate evidence for:

- the frozen 1.0 compatibility floor;
- the intentional additive 1.1 surface;
- identical public API shape across `net8.0`, `net9.0`, and `net10.0`;
- unchanged existing public enum numeric values;
- continued absence of the removed pre-1.0 `TerminalSession.Input` property.

## Protocol boundary

The public additions are explicitly VS Code-specific. Version 1.1 does not add:

- a generic `WriteOsc633Async(...)` or arbitrary selector/property API;
- public raw A/B/C/D/E marker values;
- unfinalized continuation markers `F` or `G`;
- unfinalized right-prompt markers `H` or `I`;
- unfinalized `SetMark`, `EnvJson`, or `EnvSingle*` environment-transfer operations.

OSC 133 remains the portable semantic prompt/command-region API, and OSC 7 remains the preferred portable current-location publication API.

## Baseline rule

Any later public-surface change must be deliberate under `docs/Compatibility-and-Versioning.md`. Accidental removals, renames, signature drift, enum renumbering, or target-framework divergence remain release blockers.
