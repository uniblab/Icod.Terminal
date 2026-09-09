# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Language:** C# 13  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Current release line:** `1.4.0`  
**Stable compatibility floor:** `1.0.0`

## Purpose

This file is the permanent entry point for current `Icod.Terminal` development and release planning.

The repository began with a detailed pre-1.0 architecture and milestone plan. That original document remains valuable historical design evidence and is preserved verbatim at:

[`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`](docs/history/Icod.Terminal-Initial-Development-Roadmap.md)

The completed rc1 development program remains preserved at:

[`Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`](Icod.Terminal-1.0.0-rc1-Development-Roadmap.md)

Stable `1.0.0` established the permanent 1.x contract. Minor releases may add compatible typed protocol APIs while preserving the documented 1.0 ownership, lifecycle, security, and compatibility guarantees.

## Current architecture

```text
Icod.TermInfo
      ^
      |
Icod.Terminal
      ^
      |
Icod.DCurses
      ^
      |
terminal applications
```

- `Icod.TermInfo` owns immutable terminal capability data and expansion.
- `Icod.Terminal` owns the live terminal conversation, native terminal modes, input decoding, lifecycle, query routing, semantic terminal output, and reversible/scoped terminal state.
- `Icod.DCurses` owns cells, windows, virtual-screen state, refresh/diff policy, and curses presentation abstractions.
- PTY/process hosting remains orthogonal to the `Icod.Terminal` runtime contract.

## 1.4.0 program — Kitty OSC 99 desktop notifications

`1.4.0` adds a typed Kitty OSC 99 desktop-notification surface while preserving the existing OSC 9 and OSC 777 APIs unchanged and independent.

The 1.4 tranche covers:

```text
K401  OSC 99 contract/reference review and event-routing boundary
K402  bounded Base64/chunked encoder and close/update semantics
K403  typed notification options, urgency/occasion, filtering, sounds, and icons
K404  explicit support/alive queries through the authoritative response router
K405  output/query/cancellation/privacy/composition hardening
K406  fresh package/XML validation and notification sample update
K407  public-API baseline and release-documentation closure
```

The public output API is:

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
```

The public observation API is:

```csharp
ValueTask<KittyNotificationSupport> QueryKittyNotificationSupportAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

ValueTask<IReadOnlyList<string>> QueryKittyAliveNotificationsAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

Title/body text and Kitty-defined textual metadata use strict UTF-8 with Base64 where required by the protocol. Payload-bearing requests are automatically split into encoded chunks no larger than 4,096 bytes. Multi-frame requests share an identifier and use `d=0` until final `d=1` completion.

`KittyNotificationOptions` exposes stable identifier/update semantics, application/type filtering metadata, focus suppression, `Always`/`Unfocused`/`Invisible` occasion, low/normal/critical urgency, expiration, sounds, icon names, and bounded PNG/JPEG/GIF icon transfer/cache identifiers.

Support and alive queries reuse the existing authoritative input/query router, unique response correlation, bounded timeouts, cancellation, late-response ownership, and query flush behavior. A timeout is not converted into unsupported truth.

The 1.4 release deliberately defers buttons and unsolicited activation/close reports. Those messages are application input rather than query responses and require a separate reviewed extension to the `TerminalEvent` path so OSC 99 cannot steal bytes from ordinary input or active queries.

The public surface also excludes generic raw OSC 99 metadata dispatch, automatic terminal-brand negotiation/fallback, and host-native desktop notification calls.

## Completed 1.3.0 program — iTerm2 OSC 1337 shell integration

`1.3.0` added a typed iTerm2 OSC 1337 shell-integration and semantic-history metadata surface as a distinct vendor protocol family. It did not alias OSC 1337 to OSC 7 or OSC 133.

The stable 1.3 surface covers:

```text
SetMark
CurrentDir
RemoteHost
SetUserVar
ShellIntegrationVersion=...;shell=...
ClearCapturedOutput
```

Portable OSC 7 remains the preferred current-location API and OSC 133 remains the prompt/command-region API.

## Completed 1.2.0 program — OSC 777 titled desktop notifications

`1.2.0` added one typed urxvt-style OSC 777 desktop-notification operation while preserving the existing OSC 9 notification API unchanged.

The stable public addition is:

```csharp
ValueTask SendTitledNotificationAsync(
	string title,
	string message,
	CancellationToken cancellationToken = default
);
```

The complete OSC 777 payload remains bounded to 4,096 UTF-8 bytes and the library does not silently route between OSC 9 and OSC 777.

## Completed 1.1.0 program — VS Code OSC 633

`1.1.0` added a typed VS Code OSC 633 shell-integration surface as a distinct vendor protocol family rather than aliasing it to OSC 133.

Its stable surface includes:

```text
A                  prompt start
B                  prompt end / command-input start
C                  pre-execution / command-output start
D                  status-less completion / abort
D;<exit-code>      signed decimal completion
E;<command>[;nonce]
P;Cwd=<cwd>[;nonce]
P;IsWindows=True|False
P;ContinuationPrompt=<prompt>
P;HasRichCommandDetection=True|False
```

The complete OSC 633 payload remains bounded to 65,536 UTF-8 bytes with VS Code message escaping and explicit caller-supplied nonces where defined.

## Permanent 1.x authorities

Consumers and maintainers should treat these documents as the current contract authorities:

- `docs/Architecture.md`
- `docs/Terminal-Session-and-Ownership.md`
- `docs/Lifecycle-and-Restoration.md`
- `docs/Input-and-Events.md`
- `docs/Queries-and-Responses.md`
- `docs/Modern-Keyboard-Security-and-Compatibility.md`
- `docs/Presentation-and-Reversible-State.md`
- `docs/Semantic-Output-Protocols.md`
- `docs/VsCode-Osc633-Shell-Integration.md`
- `docs/Osc777-Desktop-Notifications.md`
- `docs/ITerm2-Osc1337-Shell-Integration.md`
- `docs/Kitty-Osc99-Desktop-Notifications.md`
- `docs/Security-and-Privacy.md`
- `docs/Public-API-Baseline-1.0.md`
- `docs/Public-API-Baseline-1.1.md`
- `docs/Public-API-Baseline-1.2.md`
- `docs/Public-API-Baseline-1.3.md`
- `docs/Public-API-Baseline-1.4.md`
- `docs/Compatibility-and-Versioning.md`
- `docs/Migration-to-1.0.md`
- `docs/releases/1.0.0.md`
- `docs/releases/1.1.0.md`
- `docs/releases/1.2.0.md`
- `docs/releases/1.3.0.md`
- `docs/releases/1.4.0.md`
- `CHANGELOG.md`

Historical T-series, 0.x public API baselines, and `Public-API-Baseline-1.0-rc1.*` remain design/release evidence.

## API baseline policy

The stable 1.0 baseline remains retained as compatibility evidence:

```text
SHA-256 8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

The retained additive fingerprints are:

```text
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
1.3  0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
1.4  3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
```

`1.4.0` intentionally adds the typed Kitty notification types and four `TerminalSession` operations documented in `docs/Public-API-Baseline-1.4.md`. The machine-generated baseline is identical across `net8.0`, `net9.0`, and `net10.0`. Existing 1.0–1.3 public members and enum values remain unchanged.

## Release discipline

Pull requests validate Staging on Windows, Linux, and macOS runtime/source paths while one portable package candidate feeds four parallel package-contract shards.

`main` Release validation runs runtime/source checks on:

- Windows x64;
- Windows ARM64;
- Linux x64;
- Linux ARM64;
- macOS x64;
- macOS ARM64;

and separately validates one RID-independent package candidate through the four package shards.

The release line retains:

- full build/test coverage on `net8.0`, `net9.0`, and `net10.0`;
- stable 1.0–1.3 compatibility evidence plus the current 1.4 public-API fingerprint;
- exact NuGet artifact/XML/symbol/Source Link verification;
- historical package-only contracts from 0.8 through 0.18;
- the stable 1.x release-line package contract;
- fresh OSC 633, OSC 777, OSC 1337, and OSC 99 package/XML consumers on all three TFMs;
- current `Icod.DCurses 0.1.0` project-reference and package-boundary compatibility/ownership acceptance.

The DCurses checks are compatibility witnesses for the integration surface its current early release exercises. They are not treated as exhaustive proof of every `Icod.Terminal` contract; Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full 1.x surface.

Tags trigger publication. A `v1.4.0` tag is created only after the exact 1.4 PR head and resulting exact `main` commit pass their required gates and publication is explicitly authorized. The tag workflow requires curated `docs/releases/1.4.0.md` notes and uses the direct metadata dependency corrected in the 1.2 release engineering work.
