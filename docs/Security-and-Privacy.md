# Security and Privacy

`Icod.Terminal` mediates a bidirectional terminal conversation. Terminal control sequences are not merely visual formatting: some operations publish metadata, request external state, alter terminal-owned presentation state, or influence desktop integration. This document defines the permanent 1.x security and privacy boundary.

## 1. Trust model

`Icod.Terminal` assumes that:

- application-supplied arguments may be untrusted;
- terminal input and query responses are external input and may be malformed or adversarial;
- the attached terminal, multiplexer, or remote session may not implement a protocol exactly as expected;
- successful byte transmission does not prove terminal-side support or application;
- terminal metadata may be logged, persisted, forwarded, surfaced to the desktop, or visible to other software depending on the environment.

The library therefore favors typed semantic APIs, bounded parsing, pre-output validation, explicit disclosure, and one authoritative input/query path over raw generic protocol construction.

## 2. Control-sequence injection boundary

Text-bearing semantic protocols validate their payload according to the relevant protocol before the first frame byte is committed where the protocol permits complete prevalidation.

Where raw control characters are not meaningful semantic data, the library rejects C0, DEL, and C1 controls so caller text cannot inject BEL, ESC, OSC/ST, or another terminal sequence.

Different protocols use different safe encodings:

- OSC 7 path data is strict UTF-8 percent-encoded into a canonical `file:` URI;
- OSC 8 accepts validated already-percent-encoded URI text and separately validates the `id` parameter;
- OSC 52 binary payload is Base64 encoded;
- OSC 99 title, body, and transmitted icon data are encoded with RFC 4648 Base64, while metadata fields use a closed validated grammar;
- OSC 133 `cmdline_url` metadata is strict UTF-8 then percent-encoded byte-by-byte;
- OSC 633 command-line, `Cwd`, and `ContinuationPrompt` values use VS Code's message serializer before strict UTF-8 framing;
- OSC 777 title/body fields are strict UTF-8 and reject semicolon plus C0/DEL/C1 controls because that protocol defines no interoperable field-escaping grammar;
- OSC 1337 user-variable values are strict UTF-8 followed by Base64, while delimited metadata fields are validated before framing;
- title, legacy-notification, and OSC 9;9 text reject framing controls directly;
- color, pointer, keyboard, and other closed semantic APIs avoid arbitrary protocol strings.

Validation and encoding protect framing integrity. They do **not** make semantic content trustworthy or confidential. Base64 is an encoding, not encryption.

## 3. Bounded resources

Input decoding, paste handling, query transactions, request frames, response frames, late-response ownership, and resynchronization paths are bounded.

OSC 99 adds additional explicit bounds:

- each Base64 payload chunk is limited to 4,096 encoded bytes;
- notification metadata is bounded;
- notification identifiers and cache identifiers are bounded validated ASCII;
- transmitted icon data is bounded before Base64 expansion;
- support/alive responses are bounded before semantic parsing;
- alive-result cardinality is bounded.

Malformed input is not allowed to accumulate indefinitely merely because it resembles a recognized response prefix.

## 4. One authoritative input reader

A live `TerminalSession` owns the authoritative input decoder and query router.

The stable 1.x surface does not expose `TerminalSession.Input`. Allowing arbitrary concurrent raw reads could steal bytes from UTF-8 scalars, key sequences, paste frames, or active query responses and would undermine parser/correlation integrity.

`ITerminalInput` remains public for custom transport injection. A caller that supplied the transport must not create a competing reader while the session owns it.

This rule is especially important for OSC 99. Capability and alive queries use the existing query router. Notification activation, button, and close reports are unsolicited terminal input, not ordinary correlated query replies. Version 1.4 therefore does **not** expose those event forms through a private OSC 99 reader. They require a separately reviewed extension of the authoritative `ReadEventAsync(...)` event path.

## 5. Terminal identity is not a support oracle

`TERM`, terminal names, environment variables, host OS, and known emulator brands are useful context but are not sufficient proof that a live protocol is enabled or safe to use.

`Icod.Terminal` therefore does not generally manufacture `SupportsX` truth from branding.

OSC 633, OSC 777, OSC 1337, and OSC 99 operations are emitted only when explicitly called. The library never silently chooses among OSC 9, OSC 777, and OSC 99 based on terminal identity.

OSC 99 additionally provides an explicit capability query. A successful correlated response is stronger evidence than branding, but it remains terminal-supplied data and must be treated as untrusted input.

## 6. Emission is not application

For unacknowledged output protocols, successful completion normally means only that the complete requested bytes were written to the output service.

It does not prove that the terminal:

- supports the protocol;
- recognized the frame;
- applied the requested state;
- displayed a notification;
- retained a notification identifier;
- accepted or cached transmitted icon data;
- activated or decorated a hyperlink;
- accepted clipboard content;
- accepted shell-integration metadata.

For explicit query APIs, successful completion means a correlated response was received and parsed according to the documented grammar. It still does not make the terminal a trusted authority beyond that response.

## 7. Clipboard privacy — OSC 52

Clipboard writes can place application data into terminal or desktop selection state. Clipboard reads request external selection data and are therefore explicitly privacy-sensitive.

`ReadClipboardAsync(...)` is never called automatically by session open, probing, lifecycle handling, or disposal.

Applications should treat returned clipboard bytes as untrusted external input. Sensitive application data should not be copied to terminal clipboard state unless that disclosure is intentional.

## 8. Current-location disclosure

OSC 7, OSC 9;9, OSC 633 `Cwd`, and OSC 1337 `CurrentDir` can reveal user names, source-tree names, customer/project names, mount points, network shares, and host identity.

`Icod.Terminal` does not automatically read or publish `Environment.CurrentDirectory`, monitor directory changes, or infer path/host metadata from process state. Each vendor-specific current-directory operation is explicit and independent of the portable OSC 7 API.

## 9. Hyperlink security — OSC 8

`Icod.Terminal` validates hyperlink URI syntax and protects OSC framing, but it does not decide whether a URI is safe for a particular application to expose to users.

The library does not fetch targets, resolve DNS, launch a browser or shell, or apply a universal URI-scheme trust policy. Applications accepting untrusted targets should enforce their own scheme/host/path policy before emission.

## 10. Command and shell metadata

OSC 133, OSC 633, and OSC 1337 can publish command lines, paths, host identity, shell identity, prompt metadata, or caller-defined variables.

These values can contain credentials, tokens, customer/project names, private paths, host names, or other sensitive material. Framing-safe encoding does not redact or encrypt them.

`Icod.Terminal` does not inspect shell history, process arguments, environment variables, startup files, or secret patterns automatically. The caller decides whether publication is appropriate.

OSC 633 nonces are caller-supplied trust evidence only; the library does not discover, generate from VS Code process state, persist, rotate, or independently verify them.

## 11. Desktop notification privacy — OSC 9, OSC 777, and OSC 99

Desktop notification content can leave the terminal window and appear in notification history, lock-screen UI, screen sharing, recording, remote/multiplexed logs, accessibility services, or other desktop-shell surfaces.

`SendNotificationAsync(message)` publishes one OSC 9 message. `SendTitledNotificationAsync(title, message)` publishes OSC 777 title/body fields. `SendKittyNotificationAsync(...)` can publish considerably more metadata, including:

- title and body;
- stable notification identifiers used for update/replacement;
- application/type filtering metadata;
- focus policy and occasion;
- urgency and expiration requests;
- sound names;
- icon names and cache identifiers;
- caller-supplied PNG/JPEG/GIF icon bytes.

Applications should not put secrets in notification data unless that disclosure is intended. Icon bytes may themselves contain sensitive visual or embedded metadata outside `Icod.Terminal`'s interpretation; the library validates bounded image signatures but does not sanitize image contents.

OSC 99 alive queries can reveal which caller-managed notification identifiers the terminal still considers active. Capability queries disclose that the application is probing notification features. Query responses are untrusted terminal input.

## 12. Notification identity and updates

A caller-supplied OSC 99 identifier creates an application-visible linkage between notification operations. Reusing the identifier can update or replace prior terminal notification state; `CloseKittyNotificationAsync(...)` explicitly asks the terminal to close that identified notification.

When protocol chunking requires an identifier and the caller did not supply one, `Icod.Terminal` creates an internal bounded identifier only for framing/correlation. The library does not expose hidden persistent notification ownership or replay such notifications across lifecycle transitions.

Applications should avoid embedding secrets in notification IDs because identifiers may be retained or returned by the terminal.

## 13. Notification event boundary

Kitty OSC 99 defines richer interaction features including buttons and terminal-originated activation/close reports. Those features are intentionally deferred from 1.4.

The reason is architectural rather than cosmetic: reports are unsolicited input events and must be integrated into the same authoritative event decoder used by `ReadEventAsync(...)`. A protocol-specific background reader would violate the one-reader guarantee and could race ordinary application input or query responses.

No generic raw OSC 99 API is provided as a workaround for that omission.

## 14. Safe OSC 9 exclusion boundary

The public OSC 9 surface intentionally excludes vendor commands that can execute, block, control host-side behavior, or disclose environment data.

Excluded ConEmu-family operations include:

```text
9;1   sleep/delay
9;2   GUI message box
9;5   wait for key
9;6   GUI macro execution
9;7   process launch
9;8   environment-variable disclosure
9;10  xterm/emulation mutation
```

There is no generic `WriteOsc9Async(command, payload)` escape hatch.

## 15. iTerm2 OSC 1337 exclusion boundary

The reviewed 1.3 OSC 1337 surface is intentionally narrower than the full vendor namespace. It excludes generic dispatch and invasive operations for profile mutation, focus stealing, URL opening, pasteboard/file transfer, custom scripting, arbitrary colors/cursors, Unicode-version mutation, and Touch Bar state.

Those omissions are security boundaries, not missing convenience aliases.

## 16. Modern keyboard, focus, mouse, and paste privacy

Modern keyboard protocols can expose press/repeat/release phase, associated text, shifted/base-layout identities, and expanded modifier state. Focus and mouse reports expose user interaction context. Bracketed-paste data can contain arbitrary user text.

Applications should collect, log, and transmit only what they need. Bracketed paste marks provenance and boundaries; it does not make pasted content safe to execute in a shell, SQL engine, markup processor, or another application language.

## 17. Terminal observations can fingerprint the environment

Explicit queries can reveal terminal/environment characteristics such as device attributes, capability strings, cursor/color/pointer state, clipboard contents, and OSC 99 notification capabilities.

Applications should issue only observations they need. `Icod.Terminal` does not perform broad automatic fingerprinting merely because query APIs exist.

## 18. Redirected output

Semantic operations which require a live terminal reject known redirected/non-terminal output rather than blindly writing control bytes into a file or pipe.

OSC 633, OSC 777, OSC 1337, and OSC 99 semantic operations require an interactive terminal output endpoint. Active queries additionally require compatible interactive input/output endpoints through the shared query contract.

## 19. Advanced raw output

`TerminalSession.Output` is an advanced borrowed transport outside session serialization. Direct writes can interleave with session-managed traffic and bypass semantic validation, framing bounds, and security policy.

Likewise, `WriteTerminalStringAsync(...)` exists for already-resolved terminfo strings and padding semantics; it is not the recommended way to synthesize user-controlled OSC/CSI/DCS traffic.

Consumers should use semantic APIs whenever one exists.

## 20. Restoration, lifecycle, and uncertainty

When `Icod.Terminal` claims exact restoration, it establishes a truthful baseline first. Unknown state is not replaced by a guessed default while being described as restoration.

Suspend/resume is a trust boundary for live observations. Observation-dependent state may be re-queried after resume; stale pre-resume responses cannot satisfy a new query generation.

Ephemeral metadata such as OSC 133/633/1337 shell metadata and OSC 9/777/99 notifications is not automatically replayed on resume. Disposal does not synthesize notification closes, prompt completion, or other application-history events that the library does not own.

## 21. Dependencies and native boundaries

Native platform APIs are used only for terminal-control/lifecycle operations that require them. The package does not hide PTY process hosting, shell execution, browser/network access, OS clipboard integration, or native desktop notification APIs behind terminal semantic methods.

OSC 99 notification support is terminal traffic only. It does not invoke host-native notification services directly.

## 22. Reporting security issues

Security defects should be reported through the repository owner's supported private security-reporting channel when available rather than publishing exploitable details before a fix can be prepared.

Compatibility or missing-feature requests should remain distinct from security reports; not every unsupported vendor command is a security defect.

## 23. Permanent security principles

For the stable 1.x line, new features should preserve these principles:

1. expose semantic intent rather than generic dangerous protocol dispatch;
2. validate and bound untrusted payloads before commitment where possible;
3. keep parsing and resynchronization bounded;
4. preserve one authoritative input/query reader;
5. do not infer support solely from brand/environment identity;
6. distinguish emission from terminal application or acknowledgement;
7. make metadata disclosure explicit;
8. do not claim exact restoration without a truthful baseline;
9. surface uncertainty and double failures rather than hiding them;
10. avoid hidden host execution, network access, or process-global side effects.
