# Semantic Output Protocols

This document is the permanent 1.x guide to `Icod.Terminal` semantic output APIs and their protocol mappings.

The public contract is **semantic intent**, not arbitrary escape-sequence construction. Protocol selectors, framing, control bytes, and vendor-specific syntax remain internal unless a public semantic type explicitly represents part of the protocol contract.

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
- implicit flush occurs only where the protocol/transaction contract requires it.

The advanced `TerminalSession.Output` property and `WriteTerminalStringAsync(...)` boundary are discussed separately below because they do not provide the same semantic validation guarantees.

## 2. Application text and terminfo output

`WriteTextAsync(...)` writes application text using the session's configured application encoding and participates in session output serialization.

`WriteCapabilityAsync(...)` and `WriteTerminalStringAsync(...)` exist for capability-driven terminal renderers. They preserve terminfo's one-byte capability representation and padding semantics rather than treating terminal capability strings as ordinary application text.

`WriteTerminalStringAsync(...)` is an advanced already-resolved terminal-string boundary. It is not the recommended path for synthesizing arbitrary OSC/CSI/DCS protocols when a semantic API exists.

## 3. Titles — OSC 0, 1, and 2

The semantic title methods are:

```text
SetTitleAsync       -> OSC 0
SetIconNameAsync    -> OSC 1
SetWindowTitleAsync -> OSC 2
```

Title text uses strict UTF-8, rejects malformed UTF-16 and C0/DEL/C1 controls, and is bounded to 4096 encoded UTF-8 bytes.

The library does not maintain a title stack, query title state, or claim exact restoration of a prior title. Successful completion proves only complete frame emission.

## 4. Current location — OSC 7

`PublishCurrentLocationAsync(...)` publishes one explicit caller-supplied filesystem location as a canonical `file:` URI.

Supported path grammars are selected explicitly through `TerminalLocationPathStyle`:

- POSIX absolute paths;
- fully qualified Windows drive paths;
- Windows UNC paths.

The library validates and percent-encodes native path data deterministically. It does not:

- read `Environment.CurrentDirectory` automatically;
- monitor process directory changes;
- perform filesystem existence checks;
- resolve links/reparse points;
- infer host authority from shell/environment state.

The encoded location payload is bounded to 16,384 bytes.

OSC 7 remains the preferred portable current-location publication API.

## 5. Hyperlinks — OSC 8

`AcquireHyperlinkAsync(...)` and `WriteHyperlinkAsync(...)` expose bounded semantic hyperlink output.

The caller provides an absolute, already percent-encoded ASCII URI string. The library validates generic URI syntax, normalizes percent-escape hex digits to uppercase, and rejects malformed escapes, raw spaces/non-ASCII, malformed Unicode, and control characters.

The hyperlink URI is bounded to 2083 bytes. The optional `id` parameter uses unreserved ASCII and is bounded to 128 bytes.

`Icod.Terminal` does not:

- fetch the URI;
- resolve DNS;
- check filesystem/network reachability;
- launch a browser or shell;
- impose an application trust policy on URI schemes;
- perform automatic URL detection.

A hyperlink lease owns only OSC 8 state created by `Icod.Terminal`. See `Presentation-and-Reversible-State.md` for LIFO/lifecycle behavior.

## 6. Clipboard and selections — OSC 52

The public clipboard selections are semantic values:

- Clipboard;
- Primary;
- Secondary;
- Select.

`WriteClipboardAsync(...)` has byte and string forms. The byte form preserves exact bytes; the string form uses strict UTF-8 independently of `ApplicationEncoding`.

Payload bytes are Base64 encoded inside OSC 52, preventing arbitrary caller bytes from injecting OSC framing.

The decoded payload limit is 65,536 bytes. The encoded/frame/parser bounds are chosen so a maximum legal response remains bounded and correlated safely.

`ReadClipboardAsync(...)` is an explicit active query returning decoded bytes. Clipboard reads are never initiated automatically during session open, lifecycle transitions, or disposal.

Terminal policy may disable or ignore clipboard operations. A write success means emission; a query timeout is not proof of unsupported behavior.

## 7. Cursor style — DECSCUSR

`SetCursorStyleAsync(...)`, `QueryCursorStyleAsync(...)`, and `AcquireCursorStyleAsync(...)` expose semantic block/underline/bar cursor styles.

The public enum does not expose raw DECSCUSR parameters. Bar cursor forms are the retained xterm-compatible extension; support is not inferred from terminal identity.

Cursor-style leases provide exact observed restoration. See `Presentation-and-Reversible-State.md`.

Cursor style is distinct from cursor visibility and from graphical pointer shape.

## 8. Synchronized output — DEC private mode 2026

`AcquireSynchronizedOutputAsync(...)` owns DEC private mode 2026 through a shared first-owner/last-owner lease.

Synchronized output is a terminal-side presentation timing mode, not an application-side buffer. Ordinary writes, semantic protocols, and queries retain their own semantics while the mode is active.

Final leave flushes output as part of the ownership contract.

## 9. Terminal progress — OSC 9;4

`AcquireProgressAsync(...)` returns `TerminalProgressLease`.

The lease reports:

- determinate normal progress;
- determinate error/attention progress;
- indeterminate progress;
- final clear on last-owner release.

Callers provide completed/total work; `Icod.Terminal` computes the protocol percentage internally. Raw wire state numbers are not public API.

OSC 9;4 retains its established BEL-terminated compatibility wire form; the newer safe OSC 9 text operations use canonical ST independently.

## 10. Pointer shape — OSC 22

`TerminalPointerShape` contains semantic CSS-compatible pointer identities rather than arbitrary pointer-name strings.

Public operations include:

- explicit set;
- explicit terminal-policy reset;
- scoped ownership;
- bounded current/default/grabbed/support queries.

`TerminalPointerShape.Default` means the CSS-compatible shape named `default`; it does not mean terminal-policy reset.

The scoped owner restores only outer Icod-owned state and finally resets to terminal policy. It does not claim an exact unknown external pointer baseline.

## 11. Semantic prompt and command regions — OSC 133

The portable semantic marker surface is:

```text
BeginPromptAsync        -> A
BeginCommandInputAsync  -> B
BeginCommandOutputAsync -> C
FinishCommandAsync(n)   -> D;n
AbortCommandAsync       -> bare D
```

The methods are independently callable. `Icod.Terminal` does not maintain an authoritative shell-history state machine or synthesize missing markers.

Typed extended prompt/command-output metadata is available through the public options types. Extended command-output currently supports `cmdline_url` metadata with strict UTF-8 byte-level percent encoding.

Only RFC 3986 unreserved bytes remain literal; all other UTF-8 bytes are uppercase `%HH`. This is framing protection, not confidentiality.

Extended OSC 133 payload is bounded to 65,536 encoded bytes.

The library does not inspect shell history/process arguments, parse shell syntax, or redact secrets automatically.

OSC 133 metadata is ephemeral: no automatic open marker, lifecycle replay, or synthetic finish/abort on disposal.

## 12. VS Code shell integration — OSC 633

`Icod.Terminal 1.1` adds a distinct typed surface for VS Code's OSC 633 shell-integration namespace. It is not an alias for OSC 133 and is not emitted by the portable OSC 133 methods.

The public marker/command surface is:

```text
BeginVsCodePromptAsync()        -> OSC 633;A ST
BeginVsCodeCommandInputAsync()  -> OSC 633;B ST
BeginVsCodeCommandOutputAsync() -> OSC 633;C ST
FinishVsCodeCommandAsync(n)     -> OSC 633;D;n ST
AbortVsCodeCommandAsync()       -> OSC 633;D ST
PublishVsCodeCommandLineAsync() -> OSC 633;E;... ST
```

The stable typed property surface is:

```text
PublishVsCodeCurrentDirectoryAsync(...)      -> P;Cwd=...
PublishVsCodeIsWindowsAsync(...)              -> P;IsWindows=True|False
PublishVsCodeContinuationPromptAsync(...)     -> P;ContinuationPrompt=...
PublishVsCodeRichCommandDetectionAsync(...)   -> P;HasRichCommandDetection=True|False
```

Command-line, `Cwd`, and `ContinuationPrompt` values use the VS Code message serializer before strict UTF-8 framing:

```text
\       -> \\
;       -> \x3b
U+0000 through U+0020 -> \x00 through \x20
```

Optional nonces are accepted only on the command-line and current-directory forms that define them. The complete OSC payload is bounded to 65,536 UTF-8 bytes and a supplied nonce is bounded to 512 printable ASCII characters excluding semicolon.

OSC 633 operations are explicit, ephemeral metadata. They add no lifecycle participant, restoration lease, automatic resume replay, automatic shell detection, process/shell inspection, or startup-file modification. Successful completion proves only that the frame was written to an interactive terminal output endpoint.

The public API deliberately excludes generic raw OSC 633 dispatch and currently unfinalized/private forms including continuation-region markers `F`/`G`, right-prompt markers `H`/`I`, `SetMark`, and `EnvJson`/`EnvSingle*` environment transfer.

For the full 1.1 contract, see `VsCode-Osc633-Shell-Integration.md`.

## 13. Safe OSC 9 subset

The public safe OSC 9 surface is intentionally narrow:

### 13.1 Notification

`SendNotificationAsync(message)` emits the legacy semantic notification form:

```text
OSC 9;<message> ST
```

The message is strict UTF-8, C0/DEL/C1 controls are rejected, and the complete OSC payload is bounded to 4096 bytes including the `9;` prefix.

Display of a desktop notification is terminal/OS policy. Emission does not prove presentation.

### 13.2 Windows current-directory compatibility

`PublishWindowsCurrentDirectoryCompatibilityAsync(windowsPath)` emits:

```text
OSC 9;9;<windowsPath> ST
```

This is an explicitly named compatibility operation. The library does not normalize/resolve the path or infer it from the current process/shell.

Its payload is bounded to 32,768 bytes including the `9;9;` prefix.

OSC 7 remains the preferred portable location API; the library never silently substitutes OSC 9;9 or automatically emits both.

### 13.3 Deliberately excluded OSC 9 commands

The public API intentionally does not expose ConEmu-family commands for:

```text
9;1   sleep/delay
9;2   GUI message box
9;5   wait for key
9;6   GUI macro execution
9;7   process launch
9;8   environment disclosure
9;10  xterm/emulation mutation
```

It also omits redundant/unjustified forms:

```text
9;3   title mutation      -> existing OSC 0/1/2 APIs own title semantics
9;11  comments            -> no required semantic API
9;12  prompt signaling    -> OSC 133 owns prompt semantics
```

There is no generic public OSC 9 selector/payload API, Kitty OSC 99 command surface, or OSC 777 notification-action surface.

These exclusions are part of the 1.x safety contract.

## 14. Palette colors — OSC 4 / 104

The indexed palette uses `byte` indices `0..255` and normalized 16-bit `TerminalColor` values.

Set/query/reset operations are semantic and bounded. Multiple-set operations validate the complete collection before output and reject duplicates.

OSC 104 is terminal-policy reset, not exact restoration.

Scoped palette ownership is separately available and performs query-before-mutate exact restoration; see `Presentation-and-Reversible-State.md`.

## 15. Dynamic colors — OSC 10–14, 17, 19 / resets 110–114, 117, 119

The semantic dynamic colors are:

```text
DefaultForeground      OSC 10 / reset 110
DefaultBackground      OSC 11 / reset 111
TextCursor             OSC 12 / reset 112
MouseForeground        OSC 13 / reset 113
MouseBackground        OSC 14 / reset 114
HighlightBackground    OSC 17 / reset 117
HighlightForeground    OSC 19 / reset 119
```

OSC 10–12 form the broad/core interoperability tier. OSC 13/14/17/19 are more implementation-specific and must not be assumed supported from terminal brand.

Reset operations are terminal-policy resets. Scoped color leases provide exact observed restoration and use set forms to replay the baseline.

Tektronix OSC 15/16/18 and resets 115/116/118 are not part of the public semantic color contract.

## 16. Color grammar

Canonical outbound color representation is:

```text
rgb:rrrr/gggg/bbbb
```

with 16-bit RGB channels.

Inbound observation accepts the frozen strict `rgb:` component forms and supported hash forms. Named colors, `rgbi:`, CSS color syntax, alpha channels, mixed-width components, and arbitrary raw color strings are outside the 1.x parser contract.

## 17. Ephemeral vs owned state

A semantic output method does not automatically imply lifecycle ownership.

Examples of ephemeral output include:

- title publication;
- current location;
- OSC 133 markers and metadata;
- OSC 633 VS Code shell-integration markers and metadata;
- notification and OSC 9;9 metadata.

These are not replayed on resume or synthesized on disposal.

Scoped features such as hyperlinks, cursor style, synchronized output, progress, pointer shape, and colors each have their own documented ownership/restoration semantics. Consumers should not infer one lease's rules from another merely because both implement `IAsyncDisposable`.

## 18. Advanced raw-output boundary

`TerminalSession.Output` exposes the borrowed `ITerminalOutput` service as an advanced escape hatch. Direct calls are outside the session output-ordering contract and can interleave with session-managed traffic unless the caller provides external coordination.

Direct raw output also bypasses the semantic API's payload validation, injection protection, protocol bounds, and support posture.

Likewise, `WriteTerminalStringAsync(...)` is intended for already-resolved terminfo protocol strings and padding semantics, not as a generic mechanism for user-controlled escape construction.

Ordinary consumers should prefer semantic APIs and `WriteTextAsync(...)`.

## 19. No generic protocol dispatcher

The 1.x public surface intentionally does not provide:

- arbitrary OSC selector/payload transmission;
- arbitrary CSI final/intermediate/parameter construction;
- arbitrary DCS construction;
- generic DECSET/DECRST mode numbers;
- arbitrary vendor-command registration;
- raw response-frame delivery.

A new protocol belongs in `Icod.Terminal` only when it has a defensible semantic contract, bounded framing, truthful support/ownership behavior, and a clear security posture.
