# Icod.Terminal

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.Terminal/v1.0.0/icod_tui_toolchain.jpg)

[![PR Staging build](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml/badge.svg)](https://github.com/uniblab/Icod.Terminal/actions/workflows/pull-request.yaml)
[![Main Release validation](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/uniblab/Icod.Terminal/actions/workflows/main.yaml)

`Icod.Terminal` is the managed, cross-platform live-terminal session layer for the Icod library family. It sits between `Icod.TermInfo` and higher-level consumers such as `Icod.DCurses`, terminal-aware command-line tools, monitors, editors, pagers, and REPLs.

## Status

`1.6.0` is the current development release line. It builds on the completed/published 1.5 control-language normalization and focuses on complete CSI grammar, consolidation of existing CSI paths, and terminal/cell pixel geometry required by later graphics work.

C160 introduces the internal `TerminalCsiSyntax` layer above the normalized structural frame model. It preserves 7-bit and 8-bit CSI framing, private-use parameter bytes, semicolon-delimited parameters, colon-delimited subparameters, omitted/empty components, intermediate bytes, final selectors, and bounded raw parameter data without prematurely assigning dialect-specific numeric defaults.

The stable 1.0 architecture, ownership, lifecycle, input/query, restoration, security, and compatibility guarantees remain the compatibility floor for the 1.x line. Existing OSC 633, OSC 777, OSC 1337, OSC 99, and all released CSI-based APIs retain their exact wire semantics.

Full release notes and concise release history:

- [Icod.Terminal 1.6.0 release notes](docs/releases/1.6.0.md)
- [1.6.0 development roadmap](Icod.Terminal-1.6.0-Development-Roadmap.md)
- [C160 complete CSI grammar foundation](docs/C160-Complete-CSI-Grammar-Foundation.md)
- [Control-language normalization and graphics roadmap](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)
- [Changelog](CHANGELOG.md)

## Installation

```text
dotnet add package Icod.Terminal --version 1.6.0
```

The package targets:

```text
net8.0
net9.0
net10.0
```

and depends on `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`.

## Architecture

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

`Icod.TermInfo` is the immutable terminal-capability authority. `Icod.Terminal` owns the live terminal conversation: endpoint observation, terminal modes, input decoding, lifecycle, active query routing, semantic output, protocol framing/routing, and scoped/reversible terminal state. `Icod.DCurses` owns the higher-level virtual-screen/curses presentation model and requests semantic behavior rather than selecting raw terminal control codes.

PTY/process hosting remains orthogonal to this package.

## Quick start

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync( "Terminal session ready.\r\n" );

TerminalEvent terminalEvent = await session.ReadEventAsync(
	TimeSpan.FromSeconds( 1 )
);
```

For a curses-style virtual screen, prefer `Icod.DCurses` rather than rebuilding windows/cells/diff policy directly over `TerminalSession`.

## Core 1.x guarantees

### One authoritative input path

A live `TerminalSession` owns the only authoritative input reader for its transport. Ordinary consumers use:

- `ReadEventAsync(...)` for application input and lifecycle events;
- typed query operations for terminal responses.

Do not run a competing `Console.Read*`, stream read, or retained custom-transport read on the same live terminal conversation.

### Bounded query routing

Queries share the same incremental input/router domain as application input. The contract includes bounded parser state, ambiguity-aware query serialization, finite caller timeouts, pre-emission versus post-emission cancellation semantics, bounded late-response ownership, and lifecycle query generations.

A timeout is not automatically proof that the terminal does not support a feature.

### Reversible ownership

Scoped state uses leases where overlapping ownership matters. The 1.x documentation distinguishes:

1. **exact restoration** — a captured/observed external baseline is replayed exactly;
2. **terminal-policy reset** — control returns to terminal policy without claiming the exact previous value;
3. **Icod-owned nested state** — an outer library-owned value can be restored even when the pre-Icod state is not observable;
4. **ephemeral metadata** — explicit output with no lifecycle replay/restoration state.

`TerminalSession.DisposeAsync()` remains final cleanup/restoration authority for session-owned state.

### Output serialization boundary

Use session semantic operations for ordinary terminal output. `TerminalSession.Output` remains available only as an advanced borrowed transport and is outside normal session serialization when used directly by callers.

`WriteTerminalStringAsync(...)` is intended for already-resolved terminfo capability strings; it is not a recommendation to construct arbitrary OSC/CSI/DCS/APC/vendor traffic manually.

## Control-language normalization (`1.5`)

Version 1.5 separates five layers which had previously been easy to conflate:

```text
semantic intent
    -> capability/evidence resolution
    -> protocol backend selection
    -> control family framing
    -> dialect codec / wire transport
```

The normalized control-family vocabulary is:

```text
CSI  ESC [
DCS  ESC P
OSC  ESC ]
APC  ESC _
PM   ESC ^
SOS  ESC X
ST   ESC \
```

N150–N158 keep this normalized machinery internal. Existing public operations do not automatically reroute merely because the library now has a semantic backend registry and evidence broker.

Examples of the distinction include:

```text
DesktopNotification
    -> OSC 9
    -> OSC 777
    -> OSC 99

RasterGraphics
    -> Sixel / DCS
    -> Kitty Graphics / APC
```

A terminal/vendor name is not itself a capability. “Kitty” already spans CSI keyboard reporting, OSC 99 notifications, and APC graphics, so the architecture does not introduce a generic `SupportsKitty` flag.

Static TermInfo advertisement and live evidence are separate. Exact TermInfo recipes are recognized only when their complete semantic contract is present; successful reviewed live probes can upgrade concrete backend evidence; `InvalidateState()` expires generation-scoped live evidence while retaining immutable TermInfo/profile evidence.

The complete 1.5 task sequence is N150–N159. See [`Icod.Terminal-1.5.0-Development-Roadmap.md`](Icod.Terminal-1.5.0-Development-Roadmap.md), [`docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md`](docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md), and [`docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md`](docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md).

## Semantic terminal features

The supported semantic surface includes:

- application text and resolved terminfo capability output;
- terminal titles (OSC 0/1/2);
- current-location publication (OSC 7);
- hyperlinks (OSC 8);
- clipboard/selection operations and explicit reads (OSC 52);
- cursor style observation/ownership (DECSCUSR/DECRQSS);
- synchronized output (DEC private mode 2026);
- terminal progress (OSC 9;4);
- terminal pointer shape (OSC 22);
- typed Kitty desktop notifications and notification queries (OSC 99);
- portable semantic prompt/command metadata (OSC 133);
- typed VS Code shell integration (OSC 633);
- typed titled desktop notifications (OSC 777);
- typed iTerm2 shell-integration/semantic-history metadata (OSC 1337);
- indexed palette and selected dynamic terminal colors (OSC 4/104, 10–14, 17, 19 and resets);
- negotiated modern keyboard reporting;
- bracketed paste, focus, and mouse input protocols;
- bounded safe OSC 9 notification and Windows-CWD compatibility operations.

The safe OSC 9 subset intentionally excludes host-affecting vendor commands for sleep/blocking UI, GUI macros, process launch, environment disclosure, and emulator mutation.

### Kitty desktop notifications — OSC 99

The 1.4 OSC 99 API is explicitly Kitty-specific and typed:

```csharp
KittyNotificationOptions options = new() {
	Identifier = "build-42",
	Urgency = KittyNotificationUrgency.Normal,
	Expiration = TimeSpan.FromSeconds( 30 )
};

await session.SendKittyNotificationAsync(
	"Build",
	"Compilation complete",
	options
);
```

The same identifier can be supplied later to update or explicitly close a notification. The options surface also supports application/type filtering, activation focus policy, occasion, sound, icon names, transmitted PNG/JPEG/GIF icon data, and optional icon-cache identifiers.

Title, body, and transmitted icon data are Base64 encoded. Encoded payload chunks are automatically limited to 4,096 bytes and multi-frame notifications are serialized as one logical session-output transaction. When chunking requires an identifier and the caller did not supply one, `Icod.Terminal` creates an internal bounded identifier rather than exposing raw OSC 99 framing.

The active-query surface reuses the existing authoritative response router:

```csharp
KittyNotificationSupport support =
	await session.QueryKittyNotificationSupportAsync(
		TimeSpan.FromSeconds( 1 )
	);

IReadOnlyList<string> alive =
	await session.QueryKittyAliveNotificationsAsync(
		TimeSpan.FromSeconds( 1 )
	);
```

A query timeout is not converted into proof that OSC 99 is unsupported. Query IDs are generated internally and responses are correlated to the exact active request.

Version 1.4 deliberately does not expose notification buttons or unsolicited activation/close reports. Those terminal-originated events belong on the session's authoritative `ReadEventAsync(...)` path and require a separately reviewed event-routing contract; OSC 99 does not get a competing input reader.

The library also does not automatically choose among OSC 9, OSC 777, or OSC 99 from terminal branding and exposes no generic raw `WriteOsc99Async(...)` dispatcher.

See [`docs/Kitty-Osc99-Desktop-Notifications.md`](docs/Kitty-Osc99-Desktop-Notifications.md).

### iTerm2 OSC 1337

The 1.3 iTerm2 surface is explicitly vendor-specific and typed:

```csharp
await session.SetITerm2MarkAsync();
await session.PublishITerm2RemoteHostAsync(
	"alice",
	"host.example.test"
);
await session.PublishITerm2CurrentDirectoryAsync( "/srv/repo" );
await session.SetITerm2UserVariableAsync(
	"branch",
	"main"
);
await session.PublishITerm2ShellIntegrationVersionAsync(
	20,
	"bash"
);
```

The six supported operations are `SetMark`, `CurrentDir`, `RemoteHost`, `SetUserVar`, the current `ShellIntegrationVersion=<version>;shell=<shell>` form, and `ClearCapturedOutput`.

User-variable values are encoded as strict UTF-8 followed by Base64. Other text is strictly validated and UTF-8 encoded. Frames use canonical ST termination, a 65,536-byte payload ceiling, the shared session output gate, and pre-commit cancellation.

OSC 7 remains the preferred portable current-location API and OSC 133 remains the portable prompt/command-region API. The library never silently aliases those protocols to OSC 1337 and does not infer iTerm2 support from terminal identity.

The public API deliberately excludes generic raw OSC 1337 dispatch and invasive/overlapping operations for profile mutation, focus stealing, URL opening, pasteboard/file transfer, custom script control, arbitrary color/cursor mutation, Unicode-version changes, or Touch Bar key labels.

See [`docs/ITerm2-Osc1337-Shell-Integration.md`](docs/ITerm2-Osc1337-Shell-Integration.md).

### Titled desktop notifications — OSC 777

The 1.2 OSC 777 API is explicit and semantic:

```csharp
await session.SendTitledNotificationAsync(
	"Build",
	"Compilation complete"
);
```

It emits canonical `OSC 777;notify;<title>;<message> ST` framing using strict UTF-8 and a 4,096-byte complete OSC payload bound. Title and message may be empty, but semicolons, C0/C1/DEL controls, and malformed Unicode are rejected before output commitment because OSC 777 defines no interoperable field-escaping grammar.

`SendNotificationAsync(message)` remains the existing OSC 9 compatibility path. The library does not automatically choose between OSC 9 and OSC 777, infer support from terminal identity, or expose a generic raw OSC 777 dispatcher.

See [`docs/Osc777-Desktop-Notifications.md`](docs/Osc777-Desktop-Notifications.md).

### VS Code OSC 633

The 1.1 OSC 633 API is explicitly vendor-specific and typed. It provides semantic operations for `A`, `B`, `C`, `D`, and `E` command detection plus the stable documented `P` properties `Cwd`, `IsWindows`, `ContinuationPrompt`, and `HasRichCommandDetection`.

Command-line, `Cwd`, and `ContinuationPrompt` values use VS Code's protocol escaping before strict UTF-8 framing. Optional nonces are explicit bounded caller input for the command-line and current-directory forms that define them. The library does not expose a generic raw OSC 633 writer, does not infer VS Code support from terminal identity, and does not expose the unfinalized `F`/`G`, `H`/`I`, `SetMark`, or `EnvJson`/`EnvSingle*` extensions.

See [`docs/VsCode-Osc633-Shell-Integration.md`](docs/VsCode-Osc633-Shell-Integration.md).

## Modern keyboard reporting

Modern keyboard reporting is opt-in through the compound input-protocol lease. Traditional keyboard decoding remains the compatibility floor.

The public modes are semantic:

```text
Disambiguated
EventTypes
AllKeys
```

Kitty support is negotiated before reversible ownership is acquired. xterm `modifyOtherKeys` remains decode-only and is not blindly activated.

## Security and privacy

Semantic APIs validate and bound terminal protocol data before commitment where the contract permits it. The library deliberately avoids a generic raw vendor-command API as the ordinary extension mechanism.

Several operations disclose caller-supplied metadata by design:

- clipboard contents;
- current filesystem locations;
- hyperlinks;
- OSC 133 command-line metadata;
- OSC 633 command-line/current-directory/continuation-prompt metadata and optional nonce;
- OSC 99 notification title/body, filtering metadata, sound/icon choices, and optional icon bytes;
- OSC 1337 current-directory, remote-host, user-variable, and shell-integration metadata;
- OSC 9 and OSC 777 desktop notification text/title;
- keyboard/mouse/focus/paste input.

The library does not automatically discover or redact secrets. Applications remain responsible for deciding what data is appropriate to publish. Base64 used by OSC 99 and OSC 1337 is an encoding, not encryption.

OSC 99 capability/alive queries disclose that the application is probing notification functionality and may return terminal-maintained notification identifiers. Callers should treat query responses as untrusted terminal input.

## Compatibility policy

Stable `1.0.0` remains the compatibility floor. Versions `1.1.0`, `1.2.0`, `1.3.0`, and `1.4.0` intentionally added compatible OSC 633, OSC 777, OSC 1337, and OSC 99 surfaces respectively. Version `1.5.0` is an internal normalization release and intentionally retains the frozen 1.4 public surface. Version `1.6.0` builds on that architecture with CSI consolidation while preserving all released 1.0–1.5 contracts at the C160 stage.

For the stable 1.x line:

- patch releases fix/harden the documented contract without intentionally breaking it;
- minor releases may add compatible API/semantic features with an intentional baseline update;
- ordinary removals, renames, signature breaks, enum renumbering, or incompatible ownership/security/restoration changes require a new major release.

Vendor runtime EOL alone is not sufficient reason to drop net8.0 or net9.0; a concrete security/toolchain/maintenance blocker is required.

## Platform support

The built-in `SystemTerminalControlProvider` supports:

- Windows;
- Linux;
- macOS.

Other hosts receive controlled `Unsupported` results from the built-in provider. Custom implementations may be supplied through `ITerminalControlProvider`, `ITerminalInput`, and `ITerminalOutput`.

## Permanent documentation

The permanent 1.x authorities include:

- [Architecture](docs/Architecture.md)
- [Terminal Session and Ownership](docs/Terminal-Session-and-Ownership.md)
- [Lifecycle and Restoration](docs/Lifecycle-and-Restoration.md)
- [Input and Events](docs/Input-and-Events.md)
- [Queries and Responses](docs/Queries-and-Responses.md)
- [Modern Keyboard Security and Compatibility](docs/Modern-Keyboard-Security-and-Compatibility.md)
- [Presentation and Reversible State](docs/Presentation-and-Reversible-State.md)
- [Semantic Output Protocols](docs/Semantic-Output-Protocols.md)
- [Control-Language Normalization and Graphics Roadmap](docs/Control-Language-Normalization-and-Graphics-Roadmap.md)
- [C160 Complete CSI Grammar Foundation](docs/C160-Complete-CSI-Grammar-Foundation.md)
- [N150 Terminology and Layer-Ownership Freeze](docs/N150-Control-Language-Terminology-and-Layer-Ownership-Freeze.md)
- [N151 Generalized Control-Family Framing](docs/N151-Generalized-Control-Family-Framing.md)
- [N152 Incremental Control-Language State Machine](docs/N152-Incremental-Control-Language-State-Machine.md)
- [N153 Structural Control Frame Model](docs/N153-Structural-Control-Frame-Model.md)
- [N154 Multi-Family Query Transactions](docs/N154-Multi-Family-Query-Transactions.md)
- [N155 Capability Support and Evidence Model](docs/N155-Capability-Support-and-Evidence-Model.md)
- [N156 Semantic Backend Registry](docs/N156-Semantic-Backend-Registry.md)
- [N157 Deterministic Semantic Backend Routing Policy](docs/N157-Deterministic-Semantic-Backend-Routing-Policy.md)
- [N158 Existing Protocol and TermInfo Reconciliation](docs/N158-Existing-Protocol-and-TermInfo-Reconciliation.md)
- [N159 Acceptance, Package, and Documentation Closure](docs/N159-1.5.0-Acceptance-Package-and-Documentation-Closure.md)
- [Kitty OSC 99 Desktop Notifications](docs/Kitty-Osc99-Desktop-Notifications.md)
- [VS Code OSC 633 Shell Integration](docs/VsCode-Osc633-Shell-Integration.md)
- [OSC 777 Titled Desktop Notifications](docs/Osc777-Desktop-Notifications.md)
- [iTerm2 OSC 1337 Shell Integration](docs/ITerm2-Osc1337-Shell-Integration.md)
- [Security and Privacy](docs/Security-and-Privacy.md)
- [Licensing](docs/Licensing.md)
- [Public API Baseline 1.0](docs/Public-API-Baseline-1.0.md)
- [Public API Baseline 1.1](docs/Public-API-Baseline-1.1.md)
- [Public API Baseline 1.2](docs/Public-API-Baseline-1.2.md)
- [Public API Baseline 1.3](docs/Public-API-Baseline-1.3.md)
- [Public API Baseline 1.4](docs/Public-API-Baseline-1.4.md)
- [Compatibility and Versioning](docs/Compatibility-and-Versioning.md)
- [Migration to 1.0](docs/Migration-to-1.0.md)

Historical T-series, 0.x baselines, and the rc1 baseline remain available as design/release evidence.

## Samples

Repository samples are indexed by task in [`samples/README.md`](samples/README.md). They cover session basics, rich input, queries, scoped presentation/state, colors, titles, location, hyperlinks, clipboard, semantic prompt metadata, OSC 9/777/99 desktop notifications, and iTerm2 shell metadata.

## Build and validation

On Windows:

```text
build.cmd
```

On POSIX hosts:

```text
sh build.sh
```

PR validation runs Windows/Linux/macOS runtime/source validation, the current machine public-API fingerprint, one portable package candidate, and four parallel package-contract shards retaining contracts from 0.8 through the stable 1.x release line. The package-candidate gate also verifies the exact project-appropriate GPL/LGPL header template for every tracked `.cs` and `.csproj` file.

The semantic package shard compiles and runs fresh NuGet-only OSC 633, OSC 777, OSC 1337, and OSC 99 consumers on `net8.0`, `net9.0`, and `net10.0` and verifies generated XML documentation for the corresponding public APIs.

The repository also runs current `Icod.DCurses 0.1.0` integration/ownership acceptance, including a package-boundary soak against the freshly packed Terminal artifact. Because DCurses is still an early downstream, these checks are **compatibility witnesses for the integration paths it currently exercises**, not exhaustive proof of every `Icod.Terminal` 1.x contract. Terminal's own API, invariant, unit/hardening, and package gates remain the primary release evidence for the full surface.

After merge, Release distribution validation runs six Windows/Linux/macOS x64/ARM64 runtime jobs plus the single portable package/four-shard package contract.

## Release process

`1.6.0` is publishable only after the exact final 1.6 PR head is green, the merge result passes Release distribution validation, and publication is explicitly authorized.

The tag-triggered workflow requires curated `docs/releases/1.6.0.md` release notes and re-runs the public API, hardening, historical package, stable release-line package, current semantic package consumers, and downstream compatibility gates before publication. It does not fall back to generic auto-generated GitHub notes.

Tagging triggers publication; no release tag should be created merely because a PR is green.

## Development roadmap

Current release status is tracked in [`Icod.Terminal-Development-Roadmap.md`](Icod.Terminal-Development-Roadmap.md). The detailed current tranche is [`Icod.Terminal-1.6.0-Development-Roadmap.md`](Icod.Terminal-1.6.0-Development-Roadmap.md). The completed 1.5 program remains preserved in [`Icod.Terminal-1.5.0-Development-Roadmap.md`](Icod.Terminal-1.5.0-Development-Roadmap.md), and the completed rc1 program remains preserved in `Icod.Terminal-1.0.0-rc1-Development-Roadmap.md`.

## Authors

Inspired by original work from Bill Joy, author of the original `termcap`; Mary Ann (born Mark) Horton, author of `terminfo`; Pavel Curtis, author of `pcurses`; and Zeyd Ben-Halim, Eric S. Raymond, and Thomas Dickey, whose work developed and maintained `libtinfo` and ncurses.

Managed .NET implementation by Timothy J. Bruce <uniblab@hotmail.com>.

## Copyright

Copyright (c) 2026 Timothy J. Bruce

## License

The published `Icod.Terminal` library package and the C# sources compiled into the library are licensed under **LGPL-3.0-or-later**.

Repository executable/test programs—including `Icod.Terminal.Tests`, samples, package smoke tests, validation utilities, and downstream acceptance tools—are licensed under **GPL-3.0-or-later**. Their GPL license does not change the LGPL license of the reusable `Icod.Terminal` library they consume.

The root [`LICENSE`](LICENSE) contains the LGPLv3 terms and the incorporated GPLv3 terms. See [Licensing](docs/Licensing.md) for the project-by-project policy and source-header requirements.