# Security and Privacy

`Icod.Terminal` mediates a bidirectional terminal conversation. Terminal control sequences are not merely visual formatting: some operations publish metadata, request external state, or influence desktop integration. This document defines the permanent 1.x security and privacy boundary.

## 1. Trust model

`Icod.Terminal` assumes that:

- application-supplied arguments may be untrusted;
- terminal input and query responses are external input and may be malformed or adversarial;
- the attached terminal/multiplexer/remote session may not implement a protocol exactly as expected;
- successful byte transmission does not prove terminal-side support or application;
- terminal metadata may be logged, persisted, forwarded, surfaced to the desktop, or visible to other software depending on the environment.

The library therefore favors typed semantic APIs, bounded parsing, pre-output validation, and explicit disclosure over raw generic protocol construction.

## 2. Control-sequence injection boundary

Text-bearing semantic protocols validate their payload according to the relevant protocol before the first frame byte is committed.

Where raw control characters are not meaningful semantic data, the library rejects C0, DEL, and C1 controls so caller text cannot inject BEL, ESC, OSC/ST, or another terminal sequence.

Different protocols use different safe encodings:

- OSC 7 path data is strict UTF-8 percent-encoded into a canonical `file:` URI;
- OSC 8 accepts validated already-percent-encoded URI text and separately validates the `id` parameter;
- OSC 52 binary payload is Base64 encoded;
- OSC 133 `cmdline_url` metadata is strict UTF-8 then percent-encoded byte-by-byte;
- OSC 633 command-line, `Cwd`, and `ContinuationPrompt` values use VS Code's message serializer, escaping backslash, semicolon, and ASCII U+0000 through U+0020 before strict UTF-8 framing;
- OSC 777 title/body fields are strict UTF-8 and reject semicolon plus C0/DEL/C1 controls because the protocol defines no interoperable field-escaping grammar;
- OSC 1337 shell-integration text uses strict UTF-8, bounded delimited fields, and Base64 for user-variable values;
- title/legacy-notification/OSC 9;9 text rejects framing controls directly;
- color/pointer/keyboard APIs use closed semantic enums/types rather than arbitrary protocol strings.

Validation/escaping prevents framing injection. It does **not** make the semantic content trustworthy or confidential.

## 3. Bounded input and responses

The input decoder, paste handling, query queue, request frames, response frames, late-response ownership, and resynchronization paths are bounded.

Malformed input is not allowed to accumulate indefinitely merely because it starts like a recognized escape sequence.

A correlated malformed/oversized query response fails the active query deterministically. Where a framed protocol must be drained to regain a trustworthy boundary, draining is itself bounded.

This protects integrity and resource use; it does not turn an untrusted terminal into a trusted peer.

## 4. One authoritative input reader

A live `TerminalSession` owns the authoritative input decoder/query router.

The stable 1.x surface does not expose `TerminalSession.Input`. Allowing arbitrary concurrent raw reads could steal bytes from UTF-8 scalars, key sequences, paste frames, or active query responses and would undermine parser/correlation integrity.

`ITerminalInput` remains public for custom transport injection. A caller that supplied the transport must not create a competing reader while the session owns it.

## 5. Terminal identity is not a support oracle

`TERM`, terminal names, environment variables, host OS, and known emulator brands are useful context but are not sufficient proof that a live protocol is enabled or safe to mutate.

`Icod.Terminal` therefore does not generally manufacture `SupportsX` truth from terminal branding.

Where exact reversible ownership depends on observation, the library queries/observes before mutation. Where no truthful observation path exists, the API either uses a weaker terminal-policy-reset contract or avoids blind activation entirely.

A prominent example is xterm `modifyOtherKeys`: it is accepted for decode compatibility but is not blindly enabled/disabled because arbitrary prior state cannot be restored truthfully.

OSC 633 follows the same rule: the library does not infer VS Code shell-integration support from `TERM`, `TERM_PROGRAM`, host OS, process name, or version strings. Its APIs emit only when explicitly called.

OSC 777 also follows this rule. `SendTitledNotificationAsync(...)` does not infer notification support from `TERM`, emulator brand, OS, environment variables, or process identity and does not automatically fall back to OSC 9 or another protocol.

OSC 1337 follows the same rule. iTerm2-specific methods do not activate automatically from `TERM_PROGRAM`, terminal brand, host OS, process identity, or version strings.

## 6. Emission is not application

For unacknowledged output protocols, successful completion normally means only that the complete requested bytes were written to the output service.

It does not prove that the terminal:

- supports the protocol;
- recognized the frame;
- applied the requested state;
- displayed a notification;
- displayed an OSC 777 title/body exactly as supplied;
- activated or decorated a hyperlink;
- accepted clipboard content;
- accepted or trusted OSC 633 shell-integration metadata;
- accepted, retained, or acted on OSC 1337 semantic-history metadata.

Consumer logic must not treat emission as negotiated capability proof unless the specific API documents a correlated observation/query result.

## 7. Clipboard privacy — OSC 52

Clipboard writes can place application data into terminal/desktop selection state. Clipboard reads request external selection data and are therefore explicitly privacy-sensitive.

`ReadClipboardAsync(...)` is never called automatically by session open, probing, lifecycle handling, or disposal.

Applications should treat returned clipboard bytes as untrusted external input.

Terminal security policy may disable clipboard reads or writes. A timeout is not proof of unsupported behavior and must not be converted into a blanket support conclusion.

Sensitive application data should not be copied to terminal clipboard state unless that disclosure is intentional.

## 8. Current-location disclosure — OSC 7, OSC 9;9, OSC 633 Cwd, and OSC 1337 CurrentDir

Publishing a working directory can reveal:

- user names;
- source-tree/repository names;
- customer/project names;
- mount points;
- network-share names;
- host or server identity.

`Icod.Terminal` therefore does not automatically read or publish `Environment.CurrentDirectory`, monitor directory changes, or infer an OSC 7 authority.

OSC 9;9 Windows current-directory compatibility is likewise explicit caller intent. It is not emitted automatically and does not replace OSC 7 silently.

`PublishVsCodeCurrentDirectoryAsync(...)` is also explicit caller intent. It emits the VS Code OSC 633 `Cwd` property only when supplied a value, does not read or normalize the process current directory, and does not silently emit OSC 7 or OSC 9;9 alongside it.

`PublishITerm2CurrentDirectoryAsync(...)` likewise publishes only the caller-supplied OSC 1337 `CurrentDir` value. It does not inspect process state, normalize the path, or emit OSC 7/OSC 633 alongside it.

## 9. Hyperlink security — OSC 8

`Icod.Terminal` validates hyperlink URI syntax and protects OSC framing, but it does not decide whether a URI is safe for a particular application to expose to users.

The library does not:

- fetch targets;
- resolve DNS;
- validate network reachability;
- open browsers or shells;
- check local-file existence;
- restrict URI schemes to a universal allow-list.

Applications that accept untrusted hyperlink targets should apply an application-specific scheme/host/path trust policy before calling the terminal API.

A validated OSC 8 URI is syntactically safe to frame; it is not necessarily safe to activate.

## 10. OSC 133 command metadata

Extended semantic command-output metadata can publish a command line through `cmdline_url`.

Command lines can contain:

- passwords or API keys;
- bearer tokens;
- private paths;
- customer data;
- host names;
- environment-derived secrets;
- sensitive flags/arguments.

Percent encoding prevents terminal framing injection. It does not encrypt, redact, or hide the command line.

`Icod.Terminal` does not inspect process arguments, shell history, shell syntax, or secret patterns automatically. The caller decides whether publication is appropriate.

## 11. VS Code OSC 633 metadata

OSC 633 adds explicit caller-supplied shell-integration metadata. The public 1.1 surface can disclose:

- the exact command line through `PublishVsCodeCommandLineAsync(...)`;
- the current working directory through `PublishVsCodeCurrentDirectoryAsync(...)`;
- continuation-prompt text through `PublishVsCodeContinuationPromptAsync(...)`;
- whether the shell/backend is Windows through `PublishVsCodeIsWindowsAsync(...)`;
- whether the caller claims rich command detection through `PublishVsCodeRichCommandDetectionAsync(...)`;
- an optional caller-supplied nonce on the command-line and current-directory forms.

Command lines and paths can contain credentials, tokens, customer/project names, host information, filenames, or other sensitive material. Continuation prompts can also contain application-controlled terminal metadata.

The VS Code serializer protects OSC field boundaries by escaping backslash, semicolon, and ASCII U+0000 through U+0020. It does not encrypt or redact the published value.

A nonce is trust evidence for the receiving terminal only to the extent that the caller obtained and protects it correctly. `Icod.Terminal` does not generate, discover, persist, rotate, or validate a nonce against VS Code process state, and nonce transport does not provide confidentiality.

The library does not automatically inspect shell history, process arguments, environment variables, or the process current directory to populate OSC 633. It also does not modify shell startup files or install shell integration.

Unfinalized/private OSC 633 forms are deliberately excluded from the public API, including `F`/`G`, `H`/`I`, `SetMark`, and `EnvJson`/`EnvSingle*` environment transfer. There is no generic public raw OSC 633 dispatcher that bypasses these decisions.

## 12. iTerm2 OSC 1337 metadata

The public 1.3 OSC 1337 surface can disclose or alter terminal-side semantic-history metadata:

- `PublishITerm2CurrentDirectoryAsync(...)` publishes a caller-supplied path;
- `PublishITerm2RemoteHostAsync(...)` publishes a caller-supplied user and host identity;
- `SetITerm2UserVariableAsync(...)` publishes arbitrary caller-supplied user-variable data;
- `PublishITerm2ShellIntegrationVersionAsync(...)` publishes a shell identity and integration version;
- `SetITerm2MarkAsync(...)` creates a semantic-history mark;
- `ClearITerm2CapturedOutputAsync(...)` explicitly clears the current captured-output record.

Paths, user/host identities, and user-variable values may contain customer/project names, infrastructure details, repository names, tokens, or other private application state. The library does not populate these values from `Environment.CurrentDirectory`, `Environment.UserName`, DNS/host APIs, process arguments, shell history, environment variables, or shell startup files automatically.

User-variable values are converted to strict UTF-8 and Base64. Base64 protects the OSC field grammar; it is not encryption, redaction, authentication, or confidentiality.

`ClearITerm2CapturedOutputAsync(...)` is deliberately destructive metadata behavior. It is never called automatically by session open, probing, lifecycle transitions, resume, or disposal.

The public API excludes the broader OSC 1337 host-affecting namespace: profile mutation, focus stealing, URL opening, pasteboard/file transfer, custom script control, arbitrary color/cursor mutation, Unicode-version mutation, Touch Bar labels, and generic variable-reporting queries. There is no generic raw OSC 1337 dispatcher that bypasses those exclusions.

## 13. Notification privacy — OSC 9 and OSC 777

Desktop notification content may leave the terminal window and appear in:

- desktop notification services;
- notification history;
- lock-screen UI;
- screen sharing/recording;
- remote/multiplexed terminal logs;
- other OS or desktop-shell surfaces.

`SendNotificationAsync(message)` publishes one caller-supplied legacy OSC 9 notification message.

`SendTitledNotificationAsync(title, message)` publishes two caller-supplied OSC 777 fields. Both the title and message may therefore expose secrets, repository/customer names, host information, build results, command output, or other application data.

OSC 777 validation protects field/framing integrity by rejecting semicolons, controls, malformed Unicode, and oversized payloads. It does not encrypt, redact, or make the title/body confidential.

Applications should not publish secrets in either notification protocol unless that exposure is intended. The library does not automatically redact notification content or synthesize notifications from process state.

## 14. Safe OSC 9 exclusion boundary

The public OSC 9 surface intentionally excludes vendor commands that can execute/block/control host-side behavior or disclose environment data.

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

The library also does not expose a generic `WriteOsc9Async(command, payload)` escape hatch which would allow callers to bypass that decision.

These omissions are security boundaries, not missing convenience APIs.

## 15. Modern keyboard privacy

Modern keyboard protocols can expose more context than traditional terminal keys, including:

- press/repeat/release phase;
- associated text;
- shifted and base-layout character identities;
- expanded modifier state.

Applications should collect/log/transmit only the fields they actually need.

`Icod.Terminal` does not automatically redact application-requested keyboard metadata.

See `Modern-Keyboard-Security-and-Compatibility.md` for negotiation and lifecycle details.

## 16. Focus, mouse, and paste

Focus reports can reveal when the terminal gains/loses focus. Mouse reports expose user interaction coordinates. Bracketed-paste data can contain arbitrary user-provided text.

Bracketed paste marks provenance/boundaries; it does not make the pasted text safe to execute as shell commands, SQL, markup, or another application language.

Applications remain responsible for context-appropriate escaping/confirmation of pasted content.

## 17. Terminal observations can fingerprint the environment

Explicit queries can reveal terminal/environment characteristics such as:

- device attributes;
- terminal capabilities/name data;
- cursor position/style;
- color palette/defaults;
- pointer-shape state/support;
- clipboard contents.

Applications should issue only observations they need. `Icod.Terminal` does not perform broad automatic fingerprinting merely because query APIs exist.

## 18. Redirected output

Semantic operations which require a live terminal reject known redirected/non-terminal output rather than blindly writing terminal control bytes into a file/pipe.

A caller may explicitly configure a session to allow redirected output for workflows where terminal input remains interactive but application output is redirected; the session continues to report endpoint truthfully and semantic terminal-only operations enforce their own endpoint requirements.

OSC 633, OSC 777, and OSC 1337 semantic operations require an interactive terminal output endpoint and reject known redirected output before committing a frame.

## 19. Advanced raw output

`TerminalSession.Output` is intentionally an advanced borrowed transport outside session serialization.

Direct writes through it:

- can interleave with session-managed traffic unless externally coordinated;
- bypass semantic payload validation and protocol resource bounds;
- can emit arbitrary control bytes;
- can undermine state/query assumptions if used carelessly.

The same caution applies to using `WriteTerminalStringAsync(...)` with caller-crafted escape data. That method exists for already-resolved terminfo strings/padding, not as a public generic escape builder.

Consumers should use semantic APIs whenever one exists.

## 20. Restoration and uncertainty

Security includes state integrity.

When `Icod.Terminal` claims exact restoration, it establishes a truthful baseline first. It does not replace an unknown prior value with a guessed reset/default.

If a state transition fails and rollback also fails, both failures are preserved and `IsStateValid`/manager believed state is not silently promoted to success.

When a protocol only supports terminal-policy reset rather than exact restoration, the API documents that weaker contract explicitly.

## 21. Lifecycle

Suspend/resume is treated as a trust boundary for live terminal observations.

Observation-dependent ownership may re-query after resume rather than trusting pre-suspend values. Old query generations cannot emit after resume, and late pre-suspend response ownership is honored before post-resume observation traffic.

Ephemeral metadata such as OSC 133 markers, OSC 633 shell-integration metadata, OSC 1337 shell/semantic-history metadata, OSC 9/OSC 777 notifications, and current-location publication is not automatically replayed on resume because the library is not the application-history authority for that metadata.

OSC 633 has no restoration lease or synthetic disposal behavior. Disposal does not invent a missing command-finished/abort marker.

OSC 777 likewise has no persistent notification identity, restoration state, resume replay, or synthetic disposal notification.

OSC 1337 likewise adds no restoration lease. Disposal does not invent marks or metadata updates, and it does not clear captured output automatically.

## 22. Dependencies and native boundaries

`Icod.Terminal` uses native platform APIs only for the narrow terminal-control/lifecycle operations that require them. Native state is normalized into managed contracts and restored according to the session ownership model.

The package does not include PTY process hosting, shell execution, browser/network access, or OS clipboard integration as hidden side effects of terminal semantic APIs.

OSC 633 adds no process launch, shell execution, environment capture, or shell-startup mutation side effect. OSC 777 adds no invocation of host-native notification commands, desktop APIs, IPC, or network access; it only writes the validated terminal frame. The supported OSC 1337 surface similarly writes only bounded metadata frames and does not install shell integration, mutate profiles, open URLs, or perform file/pasteboard transfers.

## 23. Reporting security issues

Security defects should be reported through the repository/owner's supported private security-reporting channel when available rather than by publishing exploitable details before a fix can be prepared.

Compatibility or missing-feature requests should remain distinct from security reports; not every unsupported terminal vendor command is a security defect.

## 24. Permanent security principles

For 1.x, new terminal features should preserve these principles:

1. expose semantic intent rather than generic dangerous protocol dispatch;
2. validate and bound untrusted payloads before commitment;
3. keep parsing and resynchronization bounded;
4. do not infer support solely from brand/environment identity;
5. distinguish emission from application/acknowledgement;
6. make metadata disclosure explicit;
7. do not claim exact restoration without a truthful baseline;
8. surface uncertain state and double failures;
9. preserve one authoritative input/query reader;
10. avoid hidden host execution, network access, or process-global side effects.
