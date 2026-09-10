# Semantic Output Protocols

This document is the permanent 1.x guide to `Icod.Terminal` semantic output APIs and their protocol mappings.

The public contract is **semantic intent**, not arbitrary escape-sequence construction. Protocol selectors, framing, control bytes, and vendor-specific syntax remain internal unless a public semantic type explicitly represents part of the reviewed protocol contract.

## 1. Common output rules

Unless a specific API documents a stronger contract:

- user-controlled arguments are validated before output commitment;
- complete protocol frames are constructed before the first frame byte is committed where practical;
- pre-commit cancellation emits no frame;
- committed single-frame writes are allowed to finish without caller cancellation splitting the frame;
- semantic operations participate in session-owned output ordering;
- known redirected/non-terminal output is rejected when the operation requires a live terminal endpoint;
- successful completion proves emission to the configured output service, not terminal-side recognition or visual application;
- terminal brand, `TERM`, or host OS is not fabricated into proof of support;
- semantic operations do not expose a generic `SendOsc`, `WriteEscape`, or arbitrary vendor-command API;
- implicit flush occurs only where the protocol or active-query transaction contract requires it.

Multi-frame semantic operations validate the logical operation before waiting for the shared session output gate and retain that gate across the frame set when interleaving would corrupt the protocol transaction. Examples include chunked Kitty OSC 99 notifications and 1.8 Kitty Graphics direct raster transfers.

The advanced `TerminalSession.Output` property and `WriteTerminalStringAsync(...)` boundary are discussed separately below because direct use does not provide the same semantic validation guarantees.

## 2. Application text and terminfo output

`WriteTextAsync(...)` writes application text using the session's configured application encoding and participates in session output serialization.

`WriteCapabilityAsync(...)` and `WriteTerminalStringAsync(...)` exist for capability-driven terminal renderers. They preserve terminfo's one-byte capability representation and padding semantics rather than treating terminal capability strings as ordinary application text.

`WriteTerminalStringAsync(...)` is an advanced already-resolved terminal-string boundary. It is not the recommended path for synthesizing arbitrary OSC/CSI/DCS/APC protocols when a semantic API exists.

## 3. Titles — OSC 0, 1, and 2

The semantic title methods map to:

```text
SetTitleAsync       -> OSC 0
SetIconNameAsync    -> OSC 1
SetWindowTitleAsync -> OSC 2
```

Title text uses strict UTF-8, rejects malformed Unicode and framing controls, and is bounded. The library does not maintain a title stack or claim exact restoration of an arbitrary prior title.

## 4. Current location — OSC 7

`PublishCurrentLocationAsync(...)` publishes one explicit caller-supplied filesystem location as a canonical `file:` URI.

Supported path grammars are selected explicitly through `TerminalLocationPathStyle`:

- POSIX absolute paths;
- fully qualified Windows drive paths;
- Windows UNC paths.

The library validates and percent-encodes native path data deterministically. It does not read `Environment.CurrentDirectory`, monitor directory changes, perform filesystem checks, resolve links/reparse points, or infer authority from shell/environment state.

OSC 7 remains the preferred portable current-location publication API. Vendor-specific OSC 633 `Cwd`, OSC 9;9, and OSC 1337 `CurrentDir` methods remain separate explicit operations.

## 5. Hyperlinks — OSC 8

`AcquireHyperlinkAsync(...)` and `WriteHyperlinkAsync(...)` expose bounded semantic hyperlink output.

The caller provides an absolute, already percent-encoded ASCII URI. The library validates syntax and framing but does not fetch the URI, resolve DNS, check reachability, launch a browser, or impose an application trust policy on URI schemes.

A hyperlink lease owns only OSC 8 state created by `Icod.Terminal`.

## 6. Clipboard and selections — OSC 52

The public clipboard selections are semantic values: Clipboard, Primary, Secondary, and Select.

`WriteClipboardAsync(...)` has byte and string forms. Binary payload is Base64 encoded inside OSC 52 so arbitrary caller bytes cannot inject OSC framing.

`ReadClipboardAsync(...)` is an explicit active query returning decoded bytes. It uses the same authoritative input/query router as every other terminal query; clipboard reads are never initiated automatically by session open or lifecycle handling.

## 7. Cursor style — DECSCUSR / DECRQSS

`SetCursorStyleAsync(...)`, `QueryCursorStyleAsync(...)`, and `AcquireCursorStyleAsync(...)` expose semantic block/underline/bar cursor styles.

The public enum does not expose raw DECSCUSR parameters. Scoped ownership uses observed state for exact restoration where promised.

## 8. Synchronized output — DEC private mode 2026

`AcquireSynchronizedOutputAsync(...)` owns DEC private mode 2026 through a shared first-owner/last-owner lease.

Synchronized output is terminal-side presentation timing, not an application-side byte buffer. Final leave flushes output as part of that ownership contract.

## 9. Terminal progress — OSC 9;4

`AcquireProgressAsync(...)` returns `TerminalProgressLease` and exposes determinate, error/attention, indeterminate, and final-clear semantics.

Callers provide semantic work values; raw wire state numbers remain internal. OSC 9;4 retains its established compatibility termination independently of newer safe OSC text operations.

## 10. Pointer shape — OSC 22

`TerminalPointerShape` contains semantic CSS-compatible pointer identities rather than arbitrary names.

Public operations include explicit set, terminal-policy reset, scoped ownership, and bounded current/default/grabbed/support queries.

Reset returns control to terminal policy; it is not described as exact restoration of an unknown external pointer state.

## 11. Kitty desktop notifications — OSC 99

`Icod.Terminal 1.4` adds a typed Kitty OSC 99 notification contract. OSC 99 is both a semantic output family and, for selected operations, an active query family.

### 11.1 Send and update

```csharp
ValueTask SendKittyNotificationAsync(
	string title,
	string body,
	KittyNotificationOptions? options = null,
	CancellationToken cancellationToken = default
);
```

`KittyNotificationOptions` represents reviewed semantic metadata including optional stable notification identity, filtering metadata, focus policy, occasion, urgency, expiration, sound, icon name, transmitted PNG/JPEG/GIF icon data, and icon-cache identity.

Title/body and transmitted icon data are encoded with RFC 4648 Base64. Metadata fields are validated against a closed bounded grammar. The library does not expose raw OSC 99 metadata dictionaries.

Kitty OSC 99 payload chunks are limited to 4,096 encoded bytes. A large title, body, or icon is divided into protocol chunks automatically. If a multi-frame notification needs an identifier and the caller did not supply one, `Icod.Terminal` creates a bounded internal identifier for the logical transaction.

The complete logical send is validated before acquiring the session output gate. The gate remains held across all emitted chunks. Once emission is committed, individual frames are written without caller cancellation splitting the transaction. No implicit flush occurs for ordinary send/update output.

### 11.2 Close

```csharp
ValueTask CloseKittyNotificationAsync(
	string identifier,
	CancellationToken cancellationToken = default
);
```

The identifier is explicit bounded caller data. Successful completion proves only close-request emission; it does not prove that the terminal found or closed a notification.

### 11.3 Capability query

```csharp
ValueTask<KittyNotificationSupport> QueryKittyNotificationSupportAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

The request uses an internally generated query identifier and the existing active-query transaction manager. The response matcher claims only an OSC 99 response with the exact active query identity and payload type.

A timeout is an unanswered query, not proof of unsupported behavior.

### 11.4 Alive-notification query

```csharp
ValueTask<IReadOnlyList<string>> QueryKittyAliveNotificationsAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

This query is also correlated by an internally generated identifier and uses bounded response parsing. Returned notification identifiers are validated terminal-supplied data.

### 11.5 Deliberately deferred event forms

Kitty OSC 99 buttons and unsolicited activation/close reports remain deferred because they are asynchronous terminal input and must integrate through the authoritative `ReadEventAsync(...)` path. No competing reader or side-channel callback loop is provided.

There is no generic public `WriteOsc99Async(...)`, arbitrary metadata map, or raw command/payload dispatcher. The library also does not automatically choose among OSC 9, OSC 777, or OSC 99 from terminal branding.

For the full contract, see `Kitty-Osc99-Desktop-Notifications.md`.

## 12. Semantic prompt and command regions — OSC 133

The portable semantic marker surface is:

```text
BeginPromptAsync        -> A
BeginCommandInputAsync  -> B
BeginCommandOutputAsync -> C
FinishCommandAsync(n)   -> D;n
AbortCommandAsync       -> bare D
```

The methods are independently callable. `Icod.Terminal` does not maintain an authoritative shell-history state machine or synthesize missing markers.

Typed extended metadata is bounded. Command-line URL metadata uses strict UTF-8 byte-level percent encoding. The library does not inspect shell history/process arguments or redact secrets automatically.

## 13. VS Code shell integration — OSC 633

The 1.1 surface is explicitly vendor-specific and independent of OSC 133.

It includes A/B/C/D markers, E command-line publication, and the stable typed properties `Cwd`, `IsWindows`, `ContinuationPrompt`, and `HasRichCommandDetection`.

Command-line, `Cwd`, and continuation-prompt values use VS Code's message serializer. Optional nonces are accepted only on the forms that define them.

Generic raw OSC 633 dispatch and unfinalized/private `F`/`G`, `H`/`I`, `SetMark`, and `EnvJson`/`EnvSingle*` forms remain excluded.

## 14. Safe OSC 9 subset

### 14.1 Legacy notification

`SendNotificationAsync(message)` emits the bounded legacy notification form:

```text
OSC 9;<message> ST
```

Successful completion proves only emission; terminal/desktop policy controls display.

### 14.2 Windows current-directory compatibility

`PublishWindowsCurrentDirectoryCompatibilityAsync(windowsPath)` emits the explicit OSC 9;9 compatibility form. OSC 7 remains the preferred portable location API; the library never silently substitutes or emits both.

### 14.3 Excluded OSC 9 commands

The public API intentionally excludes ConEmu-family commands for sleep/delay, GUI message boxes, key waits, GUI macros, process launch, environment disclosure, and emulator mutation. There is no generic raw OSC 9 selector/payload API.

## 15. Titled desktop notifications — OSC 777

`SendTitledNotificationAsync(title, message)` emits:

```text
OSC 777;notify;<title>;<message> ST
```

The fields use strict UTF-8 and reject framing controls and semicolons because OSC 777 defines no broadly interoperable field-escaping grammar. The complete payload is bounded.

OSC 777 remains independent from OSC 9 and OSC 99. No automatic fallback or terminal-brand routing is performed.

## 16. iTerm2 shell integration and semantic history — OSC 1337

The reviewed 1.3 surface is:

```text
SetMark
CurrentDir=<path>
RemoteHost=<user>@<host>
SetUserVar=<name>=<base64(utf8(value))>
ShellIntegrationVersion=<version>;shell=<shell>
ClearCapturedOutput
```

These operations are explicit, bounded, vendor-specific metadata. Portable OSC 7 and OSC 133 remain independent.

Generic raw OSC 1337 dispatch plus invasive profile/focus/browser/pasteboard/file-transfer/custom-script and overlapping arbitrary color/cursor operations remain excluded.

## 17. Palette colors — OSC 4 / 104

Indexed palette operations use semantic byte indices and normalized `TerminalColor` values. Set/query/reset operations are bounded and validate collections before output.

OSC 104 is terminal-policy reset, not exact restoration. Scoped palette ownership separately provides observed exact restoration.

## 18. Dynamic colors — OSC 10–14, 17, 19

The semantic dynamic-color surface covers default foreground/background, text cursor, mouse foreground/background, and highlight foreground/background with the corresponding reset selectors.

Common/core and more implementation-specific forms retain separate support posture. Reset operations return to terminal policy; scoped color ownership replays an observed exact baseline where promised.

## 19. Color grammar

Canonical outbound color representation is:

```text
rgb:rrrr/gggg/bbbb
```

with 16-bit RGB channels. Inbound observation accepts the frozen strict supported forms. Named colors, CSS syntax, arbitrary raw color strings, and unsupported grammar variants are outside the parser contract.

## 20. Raster graphics — semantic operation with Sixel and Kitty Graphics backends

The public raster surface introduced in 1.7 remains:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

The public operation describes **display a bounded raw raster**. It does not describe a Sixel frame or Kitty image object.

### 20.1 Backend routing

Version 1.8 has two reviewed internal implementations:

```text
RasterGraphics
    -> verified ApcKittyGraphics
    -> verified DcsSixel
```

When both are verified, Kitty Graphics is preferred. Verified Sixel remains fallback. Unknown support does not cause blind emission; the session may perform the reviewed bounded probes needed to resolve evidence.

Terminal brand, `TERM`, OS identity, and caller preference are not support proof.

Backend fallback is a **pre-commit** routing decision. Once one backend has committed graphics bytes, a later transport failure is surfaced; the library does not replay the image through the other backend.

### 20.2 Sixel / DCS

The Sixel backend uses canonical seven-bit DCS framing, deterministic bounded palette conversion and six-row encoding, lazy payload segmentation, and one committed DCS transaction through final ST and flush.

Fractional alpha is not silently flattened. If Sixel is selected and the image contains alpha that Sixel cannot preserve, the semantic operation returns controlled unsupported.

### 20.3 Kitty Graphics / APC

The 1.8 Kitty backend uses direct transmission (`t=d`) and canonical seven-bit APC frames:

```text
ESC _ G <control-data> ; <base64-data> ESC \
```

RGB24 and RGBA32 are transmitted directly. Indexed8 expands to RGB24 when all referenced palette colors are opaque; otherwise it expands to RGBA32 preserving alpha.

Base64 image data is segmented deterministically with at most 4096 encoded bytes per Kitty chunk. One logical image may span multiple independently terminated APC frames, but all chunks are serialized as one committed session operation through the final flush.

Caller cancellation is honored before commitment. After the first APC frame commits, ordinary caller cancellation does not intentionally strand an incomplete logical transfer.

Version 1.8 does not use Kitty file, temporary-file, or shared-memory transport.

### 20.4 Capability probe

Kitty Graphics support uses the protocol-defined one-pixel query followed immediately by Primary DA as a barrier. The active Primary DA query remains within the common query transaction system while the input coordinator side-observes the matching Kitty APC by image id.

A correlated valid Kitty reply verifies the backend. Primary DA arriving first is reviewed negative protocol-response evidence for that concrete probe. Timeout before either authoritative result remains unknown.

A correlated reply remains untrusted: malformed, aborted, oversized, or unterminated responses fail according to bounded parser/recovery rules rather than becoming application input or fabricated support evidence.

### 20.5 Deliberate common-raster exclusions

The stable raster API does not expose:

- raw/public DCS, Sixel, APC, or Kitty Graphics dispatch;
- explicit backend selection;
- Sixel palette registers;
- persistent Kitty image/placement ids;
- placement/scaling/source-rectangle/z-order controls;
- Unicode placeholders;
- deletion/animation scene ownership;
- image-file decoding/transcoding.

Those features require separate semantic and compatibility review rather than being smuggled through the existing raster method.

## 21. Ephemeral vs owned state

A semantic output method does not automatically imply lifecycle ownership.

Examples of ephemeral output include:

- title publication;
- current location;
- OSC 9, OSC 777, and OSC 99 notification requests;
- OSC 133 markers/metadata;
- OSC 633 metadata;
- OSC 1337 shell metadata;
- raster image display.

These are not replayed on resume or synthesized on disposal.

Scoped features such as hyperlinks, cursor style, synchronized output, progress, pointer shape, and colors each have their own documented ownership/restoration semantics.

Raster graphics have committed **output transaction** ownership while being emitted, but that is not persistent terminal image-state ownership after the transaction completes.

## 22. Active queries and output-only operations

An output-only semantic operation uses session output serialization. A query additionally uses the authoritative terminal query manager, response matcher, finite timeout, bounded late-response ownership, and input coordinator.

OSC 52 reads, OSC 22 observations, color observations, OSC 99 support/alive queries, Sixel evidence queries, and the Kitty Graphics support test all reuse that common input/query architecture. A vendor query does not create another input reader.

Query requests are flushed as part of the request/response transaction where required so the request is committed to the terminal before waiting for a reply.

## 23. Advanced raw-output boundary

`TerminalSession.Output` exposes the borrowed `ITerminalOutput` service as an advanced escape hatch. Direct calls are outside session output ordering and can bypass semantic payload validation, resource bounds, and protocol support posture.

Likewise, `WriteTerminalStringAsync(...)` is intended for already-resolved terminfo protocol strings and padding semantics, not as a generic mechanism for user-controlled escape construction.

Ordinary consumers should prefer semantic APIs and `WriteTextAsync(...)`.

## 24. No generic protocol dispatcher

The stable 1.x public surface intentionally does not provide:

- arbitrary OSC selector/payload transmission;
- arbitrary CSI final/intermediate/parameter construction;
- arbitrary DCS construction;
- arbitrary APC construction;
- generic Sixel or Kitty Graphics dispatch;
- generic DECSET/DECRST mode numbers;
- arbitrary vendor-command registration;
- raw response-frame delivery;
- protocol-specific competing input readers.

A new terminal protocol belongs in `Icod.Terminal` only when it has a defensible semantic contract, bounded framing/parsing, truthful support and ownership behavior, and a clear security posture.
