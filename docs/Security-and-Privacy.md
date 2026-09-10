# Security and Privacy

`Icod.Terminal` mediates a bidirectional terminal conversation. Terminal control sequences are not merely visual formatting: some operations publish metadata, request external state, alter terminal-owned presentation state, influence desktop integration, or report unsolicited terminal-side observations. Raster graphics add large bidirectional protocol paths that must preserve the same bounded, typed, evidence-driven design.

This document defines the permanent 1.x security and privacy boundary.

## 1. Trust model

`Icod.Terminal` assumes that:

- application-supplied arguments may be untrusted;
- terminal input, unsolicited semantic events, and query responses are external input and may be malformed or adversarial;
- the attached terminal, multiplexer, remote session, or transport may fabricate otherwise well-formed observations;
- the attached terminal, multiplexer, or remote session may not implement a protocol exactly as expected;
- successful byte transmission does not prove terminal-side support or application;
- terminal metadata may be logged, persisted, forwarded, surfaced to the desktop, or visible to other software depending on the environment;
- large raster inputs and large terminal replies may be accidental or adversarial resource pressure.

The library therefore favors typed semantic APIs, bounded parsing/encoding, pre-output validation, explicit capability evidence, and one authoritative input/query/event path over raw generic protocol construction.

## 2. Control-sequence injection boundary

Text-bearing semantic protocols validate payloads according to their relevant protocol before commitment where possible.

Where raw control characters are not meaningful semantic data, the library rejects C0, DEL, and C1 controls so caller text cannot inject BEL, ESC, OSC/ST, or another terminal sequence.

Different protocols use different safe encodings. Examples include:

- OSC 7 path data uses strict UTF-8 percent encoding;
- OSC 52 binary payloads use Base64;
- OSC 99 text/icon/button data uses protocol-defined Base64 and closed metadata grammar;
- OSC 133 and OSC 633 metadata use their reviewed serializers/escaping;
- OSC 777 rejects delimiters/control bytes for which the protocol defines no interoperable escape;
- OSC 1337 user-variable values use strict UTF-8 plus Base64;
- Kitty Graphics raw image bytes use protocol-defined Base64 inside a typed bounded APC dialect;
- closed color, pointer, keyboard, notification-event, and raster APIs avoid arbitrary caller-supplied protocol strings.

Validation protects framing integrity. It does not make semantic content confidential, authentic, or trustworthy.

## 3. Bounded resources

Input decoding, paste handling, query transactions, unsolicited semantic reports, application-event buffering, request/response frames, late-response ownership, resynchronization, and graphics processing are bounded.

The normalized control-language layer uses one bounded scanner for CSI, DCS, OSC, APC, PM, and SOS rather than separate unbounded per-dialect accumulators.

Public raster ceilings remain:

```text
maximum raster dimension       16,384
maximum raster pixels          16 Mi
maximum owned pixel bytes      64 MiB
maximum indexed palette        256 entries
```

Relevant graphics/protocol ceilings include:

```text
normal terminal response frame       4,096 bytes
small complete DCS frame              4,096 bytes
small complete APC frame              8,192 bytes
Kitty Base64 image data per APC chunk 4,096 bytes
Sixel quantizer histogram             32 x 32 x 32 bins
Kitty notification buttons            16 labels
Kitty button label                     512 UTF-8 bytes
Kitty combined button payload        2,048 UTF-8 bytes including separators
```

Sixel payload output is generated as bounded lazy segments. Kitty Graphics direct output is generated as bounded lazy Base64/application payload chunks and one bounded APC frame at a time. Large graphics do not require one complete encoded transfer in memory.

Oversized correlated terminal responses do not cause an unbounded accumulator: recovery uses the existing bounded string resynchronization ceiling.

Version 1.9 applies the same principle to unsolicited semantic candidates. Malformed or oversized owned OSC 99 reports are consumed/recovered boundedly and are not leaked into ordinary application text. Semantic events share the existing bounded application-event backpressure domain rather than accumulating in an unbounded side queue.

## 4. One authoritative input reader

A live `TerminalSession` owns the authoritative input decoder, query router, and unsolicited semantic-event classifier.

The stable 1.x surface does not expose a session raw-input property. A competing raw read could steal bytes from UTF-8 scalars, key sequences, paste frames, lifecycle traffic, unsolicited semantic reports, or active query responses.

`ITerminalInput` remains public for custom transport injection, but a caller supplying the transport must not create a competing reader while the session owns it.

Version 1.9 freezes the routing order as:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

A query-owned response is never also published as a semantic event. A recognizable unsolicited report does not satisfy an unrelated query merely because both use OSC 99 or another shared control family.

Both Sixel capability observation and the 1.8 Kitty Graphics support probe reuse the same authoritative input/query path. Neither adds a graphics-specific input loop. Unsolicited semantic events likewise do not add a notification-specific background reader or callback stream.

The Kitty probe's Primary DA response is owned by the normal query transaction while the input coordinator side-observes only an APC response correlated to the active probe image id.

## 5. Capability evidence is not terminal identity

`TERM`, terminal names, environment variables, host operating system, known emulator brands, registry membership, and caller backend preference are useful context but are not sufficient proof that a live protocol is supported.

The internal evidence model separates support state from evidence source:

```text
Unavailable
Unsupported
Unknown
Advertised
Verified

TermInfo
BuiltInProfile
LiveProbe
ProtocolResponse
```

For Sixel, a valid Primary Device Attributes response containing attribute `4` is positive `Verified / ProtocolResponse` evidence for `DcsSixel`.

A valid response without `4` is **not** automatically authoritative proof that Sixel is unsupported. It records only unknown protocol-response evidence. Probe timeout likewise remains unknown. Caller cancellation propagates and is not converted into negative evidence.

For Kitty Graphics in 1.8, the support test uses a protocol-defined correlated Kitty query followed immediately by Primary DA as a synchronization barrier:

- a correlated valid Kitty reply verifies `ApcKittyGraphics`;
- Primary DA arriving before such a reply is reviewed negative `Unsupported / ProtocolResponse` evidence for that concrete support test;
- timeout before either authoritative result remains `Unknown`;
- cancellation does not manufacture negative evidence.

The distinction prevents both false-positive brand guessing and false-negative interpretation of ordinary silence.

## 6. Correlation grants ownership, not trust

A response that matches an active query identity is still attacker-controlled terminal input.

For the Kitty Graphics support probe, once a complete matching `i=<probe-id>` control-data field is observed in a recognizable APC prefix, the string becomes boundedly transaction-owned. This prevents a response that has already identified itself as the probe reply from leaking into ordinary application input merely because later framing is hostile.

Correlation does not bypass validation:

- numeric image-id parsing is overflow-safe;
- an incomplete identifier prefix does not claim the response;
- CAN or SUB abort after correlation fails the probe;
- malformed escape termination after correlation fails the probe;
- exceeding the normal 4096-byte response-frame limit fails the probe and invokes bounded drain/resynchronization through string termination;
- observing correlation without a structural terminator before the protocol deadline is a malformed-response failure, not silent uncertainty.

Unrelated APC/OSC/DCS/CSI traffic does not satisfy the active Kitty probe merely because it is structurally valid terminal control traffic.

The same principle applies to 1.9 unsolicited semantic recognition: recognition grants bounded routing ownership only. It does not authenticate the report or exempt it from metadata, numeric, framing, and resource validation.

## 7. Emission is not application

For unacknowledged output protocols, successful completion normally means only that the requested bytes were successfully written to the output service.

It does not prove that the terminal:

- supports the protocol;
- recognized the frame;
- applied the requested state;
- displayed a notification or image;
- retained an identifier or cache entry.

For explicit query APIs, successful completion means a correlated response was received and parsed according to the reviewed grammar. The response remains untrusted terminal input.

Raster display is therefore capability-gated before emission, but successful `DisplayRasterAsync(...)` output still does not claim visual verification after bytes are written.

Similarly, successful `SendKittyNotificationAsync(...)` completion proves request emission according to its output contract, not that the host notification service displayed or retained the notification.

## 8. Raster input and alpha semantics

`TerminalRasterImage` owns an immutable snapshot of caller-provided raw raster data. Copying input at construction prevents asynchronous display from observing later caller mutation of the supplied buffers.

The backend-neutral model preserves straight RGBA alpha.

Sixel supports only the alpha semantics it can preserve truthfully:

- alpha `0` means transparent/leave destination untouched;
- alpha `255` means opaque/paint the pixel;
- fractional alpha remains valid raster data, but the Sixel backend returns controlled unsupported rather than silently compositing against an invented matte/background.

Kitty Graphics direct RGBA32 transfer preserves fractional alpha. Indexed8 input is expanded to RGB24 only when referenced palette colors are opaque; otherwise it expands to RGBA32 preserving the indexed alpha values.

The library does not perform hidden premultiplication, gamma conversion, profile conversion, or arbitrary background flattening.

## 9. Deterministic Sixel quantization

True-color input may require palette reduction for Sixel. The quantizer uses bounded deterministic work state and stable tie-breaking.

Security/reliability consequences include:

- identical input/options produce identical palette/remapping output;
- high-entropy input does not create an unbounded color dictionary;
- palette cardinality remains bounded;
- no hash-order-dependent output is relied upon;
- no hidden dithering state or unbounded optimization search is introduced.

Determinism is not a claim of perceptual optimality; it is a reproducibility and bounded-work guarantee.

## 10. Kitty direct-transfer security boundary

Version 1.8 deliberately uses direct Kitty Graphics transmission (`t=d`).

It does not silently choose protocol media that require host-side path or IPC state:

```text
t=f  file
t=t  temporary file
t=s  shared memory
```

Those media are outside the 1.8 semantic raster contract and require separate security review before use because they introduce path naming, lifetime, permissions, race, visibility, cleanup, or cross-process concerns absent from direct terminal traffic.

Raw image bytes are Base64 encoded into bounded protocol chunks. Base64 is framing-safe encoding, not encryption.

## 11. Committed graphics output

Both 1.8 raster backends use committed output semantics through the existing session serialization boundary.

Before the first backend frame commits:

- raster/conversion invariants are validated as far as the design permits;
- caller cancellation is honored;
- output-gate acquisition remains cancellable.

After commitment:

- ordinary caller cancellation is no longer allowed to intentionally truncate the logical graphics transfer;
- the session output gate remains held until that backend's logical transaction completes or the transport fails;
- unrelated session-managed output cannot interleave inside the transfer.

For Sixel, the committed object is one DCS frame ending in one ST and flush.

For Kitty Graphics, the committed object may be several individually complete APC frames, but all chunks belong to one logical direct-transfer transaction and the gate remains held through the final frame and flush.

If the underlying transport fails after commitment, the error is surfaced. The library does not automatically retry the image, replay an uncertain frame, switch to the other raster backend, or speculate that additional terminators/recovery commands are safe.

## 12. Teardown and committed output

`TerminalSession.DisposeAsync()` remains final cleanup/restoration authority for session-owned state.

Teardown drains the same session output gate used by committed raster graphics before output-state restoration proceeds. This prevents restoration traffic from interleaving inside either a Sixel control string or a multi-frame Kitty direct transfer.

This ordering is a contract requirement; the internal synchronization primitive may change in future implementations.

Notifications are not retained as reversible session state merely because interactive reporting was requested. Version 1.9 does not automatically close identified notifications during session disposal.

## 13. No generic raw graphics escape hatch

The stable semantic raster surface intentionally does not expose:

- a generic public DCS or Sixel writer;
- a generic public APC or Kitty Graphics writer;
- direct Sixel palette-register mutation;
- caller-selected Sixel/Kitty backend routing;
- arbitrary Kitty control-data dictionaries;
- Kitty placement/image identifiers, z-order, source rectangles, deletion, or animation through the common raster API;
- arbitrary graphics control-string injection through `DisplayRasterAsync(...)`.

`TerminalSession.Output` remains a public advanced borrowed transport and can always be misused by a caller. Direct writes through it are outside session serialization and semantic validation. That advanced escape hatch is not an endorsement of constructing arbitrary untrusted terminal traffic.

## 14. Image-file decoding is out of scope

`Icod.Terminal` consumes bounded raw pixel/index data. It does not decode PNG, JPEG, GIF, or other image files as part of the raster-display path.

This avoids importing file-parser attack surface, metadata handling, decompression-bomb policy, color-profile interpretation, and format-specific security decisions into the core live-terminal package.

Applications may decode image formats with libraries appropriate to their own trust model, then provide bounded raw raster data to `Icod.Terminal`.

## 15. Clipboard privacy — OSC 52

Clipboard writes can place application data into terminal or desktop selection state. Clipboard reads request external selection data and are explicitly privacy-sensitive.

`ReadClipboardAsync(...)` is never called automatically by session open, graphics probing, lifecycle handling, or disposal.

Applications should treat returned clipboard bytes as untrusted external input and should not publish secrets to terminal clipboard state unintentionally.

## 16. Current-location and shell metadata disclosure

OSC 7, OSC 9;9, OSC 633 `Cwd`, OSC 1337 `CurrentDir`, and related semantic metadata can reveal user names, source-tree names, customer/project names, mount points, shares, and host identity.

`Icod.Terminal` does not automatically discover and publish environment/current-directory/shell-history data. The caller decides whether disclosure is appropriate.

## 17. Hyperlink security — OSC 8

The library validates hyperlink framing and URI syntax but does not decide whether a URI is safe for a particular application to expose to users.

It does not fetch targets, resolve DNS, launch browsers/shells, or apply a universal URI-scheme trust policy.

## 18. Desktop notification privacy

OSC 9, OSC 777, and OSC 99 notifications can leave the terminal window and appear in desktop notification surfaces, logs, recordings, screen sharing, or accessibility software.

Applications should not place secrets in notification content unless that disclosure is intended. Base64 used by OSC 99 is encoding, not encryption.

OSC 99 notification identifiers, button labels, and alive/query results may reveal application state. Capability/alive responses remain untrusted terminal input.

Interactive reporting does not create confidentiality: requesting activation/button/close reports can expose additional interaction metadata to the application.

## 19. Unsolicited semantic notification event boundary

Version 1.9 exposes Kitty OSC 99 activation, button, close, and close-tracking-unavailable reports as typed `TerminalNotificationEvent` values through the existing `ReadEventAsync(...)` path.

These reports are **validated but unauthenticated terminal-controlled input**. A malicious or compromised terminal path can fabricate:

- notification identifiers;
- activation events;
- button numbers;
- close events;
- `untracked` close-tracking results;
- syntactically valid event ordering.

An identifier provides application-level correlation only. It is not a capability token, cryptographic nonce, trusted desktop handle, or proof that the operating system displayed the associated notification. A typed activation/button report is not proof that a trusted human performed the action.

Applications must not use these events as an authorization boundary without their own independent security mechanism.

The semantic envelope deliberately does not expose raw OSC bytes, arbitrary selector dictionaries, backend identifiers, or generic vendor payloads. There is no protocol-specific background reader, callback stream, or `ReadSemanticEventAsync(...)` workaround.

Malformed or oversized owned semantic candidates are consumed/recovered within bounded parser/resynchronization limits and are not leaked into ordinary application text. After recovery, active-query precedence is re-entered before later traffic is decoded.

## 20. Safe OSC 9 exclusion boundary

The public OSC 9 surface intentionally excludes vendor commands that can execute, block, control host-side behavior, or disclose environment data. There is no generic `WriteOsc9Async(command, payload)` escape hatch.

## 21. iTerm2 OSC 1337 exclusion boundary

The reviewed OSC 1337 surface intentionally excludes generic dispatch and invasive operations for profile mutation, focus stealing, URL opening, pasteboard/file transfer, custom scripting, arbitrary colors/cursors, Unicode-version mutation, and Touch Bar state.

Those omissions are security boundaries rather than missing convenience aliases.

## 22. Modern keyboard, focus, mouse, and paste privacy

Modern keyboard protocols can expose press/repeat/release phase, associated text, shifted/base-layout identities, and modifier state. Focus/mouse reports expose interaction context. Bracketed-paste data may contain arbitrary user text.

Applications should collect, log, and transmit only what they need. Bracketed paste marks provenance and boundaries; it does not make pasted content safe to execute.

## 23. Terminal observations can fingerprint the environment

Explicit queries can reveal terminal/environment characteristics such as device attributes, capability strings, cursor/color state, clipboard contents, notification support, and graphics support.

Applications should issue only observations they need.

`DisplayRasterAsync(...)` may issue narrowly scoped Sixel/Kitty capability probes when verified graphics evidence is not already available; it does not conduct broad terminal fingerprinting.

## 24. Redirected output

Semantic operations that require a live terminal reject known redirected/non-terminal output rather than blindly writing control bytes into a file or pipe.

Active queries additionally require compatible interactive input/output endpoints through the shared query contract.

## 25. Restoration, lifecycle, and evidence invalidation

When `Icod.Terminal` claims exact restoration, it establishes a truthful baseline first. Unknown state is not replaced by a guessed default while being described as restoration.

Suspend/resume and explicit invalidation are trust boundaries for live observations. Generation-scoped `LiveProbe` and `ProtocolResponse` evidence, including Sixel and Kitty Graphics verification, expires through the existing semantic evidence generation mechanism. Immutable selected TermInfo/profile evidence may persist because it describes static session configuration rather than a prior live observation.

Raster images are ephemeral output. The library does not replay them automatically after resume and does not claim to restore external Sixel palette/image contents or Kitty image/placement state on disposal.

Notification requests and unsolicited interaction events are also not reversible state. The library does not replay sent notifications on resume, synthesize missed notification events, or treat already-decoded semantic observations as capability evidence merely because they survived an application-level lifecycle transition.

## 26. Dependencies and native boundaries

Native platform APIs are used only for terminal-control/lifecycle operations that require them. The package does not hide PTY process hosting, shell execution, browser/network access, OS clipboard integration, image decoding, or native desktop notification APIs behind terminal semantic methods.

Sixel and Kitty Graphics are terminal traffic only. The 1.8 Kitty implementation uses direct transfer specifically so no filesystem/shared-memory graphics dependency is introduced.

Kitty desktop notifications in 1.9 remain terminal protocol traffic. `Icod.Terminal` does not invoke a platform notification API itself.

## 27. Reporting security issues

Security defects should be reported through the repository owner's supported private security-reporting channel when available rather than publishing exploitable details before a fix can be prepared.

Compatibility or missing-feature requests should remain distinct from security reports.

## 28. Permanent security principles

For the stable 1.x line, new features should preserve these principles:

1. expose semantic intent rather than generic dangerous protocol dispatch;
2. validate and bound untrusted payloads before commitment where possible;
3. keep parsing, conversion, event buffering, and resynchronization bounded;
4. preserve one authoritative input/query/event reader;
5. do not infer support solely from brand/environment identity;
6. separate capability state from evidence source/lifetime;
7. treat correlation or semantic recognition as bounded ownership rather than trust;
8. distinguish emission from terminal application or acknowledgement;
9. treat unsolicited semantic events as unauthenticated external input;
10. make metadata disclosure explicit;
11. do not claim exact restoration without a truthful baseline;
12. surface uncertainty and compound failures rather than hiding them;
13. avoid hidden host execution, network access, file decoding, or process-global side effects;
14. once a terminal graphics transaction is committed, preserve logical-transfer integrity rather than using ordinary caller cancellation to truncate it;
15. never automatically replay or switch backends after partial committed graphics failure;
16. do not turn typed semantic event support into a generic raw vendor-event bus.
