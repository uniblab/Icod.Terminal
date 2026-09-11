# Security and Privacy

`Icod.Terminal` mediates a bidirectional terminal conversation. Terminal control sequences are not merely visual formatting: some operations publish metadata, request external state, alter terminal-owned presentation state, influence desktop integration, display raster graphics, or report unsolicited terminal-side observations.

This document defines the permanent 1.x security and privacy boundary.

## 1. Trust model

`Icod.Terminal` assumes that:

- application-supplied arguments may be untrusted;
- terminal input, unsolicited semantic events, capability responses, and query responses are external input and may be malformed or adversarial;
- the attached terminal, multiplexer, remote session, or transport may fabricate otherwise well-formed observations;
- the terminal may not implement a protocol exactly as expected;
- successful byte transmission does not prove terminal-side support or application;
- terminal metadata may be logged, persisted, forwarded, surfaced to the desktop, or visible to other software depending on the environment;
- large raster inputs and large terminal replies may create accidental or adversarial resource pressure.

The library therefore favors typed semantic APIs, bounded parsing/encoding, pre-output validation, explicit capability evidence, and one authoritative input/query/event path over raw generic protocol construction.

## 2. Control-sequence injection boundary

Text-bearing semantic protocols validate payloads according to their relevant protocol before commitment where possible.

Where raw control characters are not meaningful semantic data, the library rejects C0, DEL, and C1 controls so caller text cannot inject BEL, ESC, OSC/ST, or another terminal sequence.

Different protocols use different safe encodings. Examples include:

- OSC 7 path data uses strict UTF-8 percent encoding;
- OSC 52 binary payloads use Base64;
- OSC 99 text/icon/button data uses protocol-defined Base64 and closed metadata grammar;
- OSC 133 and OSC 633 metadata use reviewed serializers/escaping;
- OSC 777 rejects delimiters/control bytes for which the protocol defines no interoperable escape;
- OSC 1337 user-variable values use strict UTF-8 plus Base64;
- Kitty Graphics raw image bytes use protocol-defined Base64 inside a typed bounded APC dialect;
- closed color, pointer, keyboard, notification-event, capability-planning, and raster APIs avoid arbitrary caller-supplied protocol strings.

Validation protects framing integrity. It does not make semantic content confidential, authentic, or trustworthy.

## 3. Bounded resources

Input decoding, paste handling, query transactions, unsolicited semantic reports, application-event buffering, request/response frames, late-response ownership, resynchronization, capability verification, and graphics processing are bounded.

The normalized control-language layer uses one bounded scanner for CSI, DCS, OSC, APC, PM, and SOS rather than separate unbounded per-dialect accumulators.

Public raster ceilings remain:

```text
maximum raster dimension       16,384
maximum raster pixels          16 Mi
maximum owned pixel bytes      64 MiB
maximum indexed palette        256 entries
```

Relevant protocol/resource ceilings include:

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

Sixel output is generated as bounded lazy segments. Kitty Graphics direct output is generated as bounded lazy Base64/application chunks and one bounded APC frame at a time. Large graphics do not require one complete encoded transfer in memory.

Malformed or oversized owned query/semantic traffic is recovered through bounded drain/resynchronization rather than unbounded accumulation or leakage back into ordinary application text.

## 4. One authoritative input reader

A live `TerminalSession` owns the authoritative input decoder, query router, and unsolicited semantic-event classifier.

The stable 1.x surface does not expose a session raw-input property. A competing raw read could steal bytes from UTF-8 scalars, key sequences, paste frames, lifecycle traffic, unsolicited semantic reports, or active query responses.

`ITerminalInput` remains public for custom transport injection, but a caller supplying the transport must not create a competing reader while the session owns it.

The routing order is:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

A query-owned response is never also published as a semantic event. A recognizable unsolicited report does not satisfy an unrelated query merely because both use a shared control family.

Sixel capability observation, Kitty Graphics support probing, and 1.10 capability verification all reuse the same authoritative input/query path. None introduces a protocol-specific reader, notification reader, capability reader, or callback stream.

## 5. Capability evidence is not terminal identity

`TERM`, terminal names, environment variables, host operating system, known emulator brands, registry membership, and caller backend preference are context, not sufficient proof that a live protocol is supported.

The internal model separates semantic operation, backend, support state, evidence source, and endpoint availability.

Static terminal-description evidence and generation-scoped live evidence are distinct. Timeout or silence is not automatically `Unsupported`, and caller cancellation is not negative capability evidence.

For Sixel, Primary DA attribute `4` may provide positive support evidence. A valid Primary DA response without `4` does not automatically prove terminal-wide Sixel unsupported.

For Kitty Graphics, a correlated valid Kitty response verifies the backend; the reviewed Primary DA barrier arriving first provides negative evidence for that concrete probe; timeout before either authoritative result remains unknown.

The distinction prevents both false-positive brand guessing and false-negative interpretation of ordinary silence.

## 6. Public capability planning security boundary — 1.10

Version 1.10 exposes semantic capability planning through:

```text
TerminalSession.InspectCapability(...)
TerminalSession.VerifyCapabilityAsync(...)
TerminalCapabilityStatus
```

`InspectCapability(...)` is side-effect free. It reads only the session's existing in-memory knowledge and emits no terminal bytes. Applications may therefore use inspection for ordinary planning without triggering hidden terminal fingerprinting.

`VerifyCapabilityAsync(...)` is deliberately explicit because verification may emit bounded terminal query/probe traffic. In 1.10 only reviewed existing probe paths are used, initially for `KeyboardReporting` and `RasterGraphics`. Capabilities without a reviewed live probe remain inspection-only.

A successful `Verified` result means that the reviewed terminal observation established current-generation support under the library contract. It is **not** authentication of the terminal, emulator, multiplexer, desktop session, host, or user.

Public capability evidence intentionally exposes only:

```text
None
StaticDescription
LiveObservation
```

It does not expose raw protocol frames, backend identities, routing scores, arbitrary terminfo capability names, `Icod.TermInfo` objects, or terminal-brand heuristics. This is both an abstraction boundary and a security/privacy boundary: consumers should not accidentally treat implementation provenance or terminal branding as trusted identity.

Support and endpoint availability remain separate. An unavailable endpoint does not rewrite known terminal support to `Unsupported`, and verification does not bypass endpoint/query lifecycle restrictions merely to produce an answer.

Pre-cancelled verification emits no probe traffic. Repeated verification of decisive current live evidence does not intentionally re-probe merely to return the same result.

## 7. Correlation grants ownership, not trust

A response that matches an active query identity is still attacker-controlled terminal input.

Correlation or semantic recognition grants bounded routing ownership, not trust. Matching identifiers do not bypass validation.

For the Kitty Graphics support probe, once a complete matching image id is observed in a recognizable APC prefix, that string remains transaction-owned through later malformed/aborted/oversized recovery. Numeric parsing is overflow-safe; CAN/SUB, malformed termination, oversize, and missing termination remain failures according to the bounded query contract.

The same principle applies to unsolicited semantic notification reports. Recognition prevents hostile owned traffic from being reinterpreted as ordinary text, but does not authenticate identifiers, button numbers, close events, or event ordering.

## 8. Emission is not application

For unacknowledged output protocols, successful completion normally means only that the requested bytes were successfully written.

It does not prove that the terminal:

- supports the protocol;
- recognized the frame;
- applied the requested state;
- displayed a notification or image;
- retained an identifier or cache entry.

For explicit query APIs, successful completion means a correlated response was received and parsed according to the reviewed grammar. The response remains untrusted terminal input.

Raster display is capability-gated before emission, but successful `DisplayRasterAsync(...)` output still does not claim visual verification after bytes are written.

## 9. Raster input and alpha semantics

`TerminalRasterImage` owns an immutable snapshot of caller-provided raw raster data. Copying input at construction prevents asynchronous display from observing later caller mutation of supplied buffers.

The backend-neutral model preserves straight RGBA alpha.

Sixel supports only alpha semantics it can preserve truthfully. Fractional alpha remains valid raster data, but the Sixel backend returns controlled unsupported rather than silently compositing against an invented background.

Kitty Graphics direct RGBA32 transfer preserves fractional alpha. Indexed8 input expands to RGB24 only when referenced palette colors are opaque; otherwise it expands to RGBA32 preserving indexed alpha.

The library does not perform hidden premultiplication, gamma/profile conversion, or arbitrary background flattening.

## 10. Deterministic bounded graphics work

Sixel quantization uses bounded deterministic work state and stable tie-breaking. High-entropy input does not create an unbounded color dictionary or optimization search.

Kitty Graphics direct transfer (`t=d`) remains the reviewed transport. The library does not silently choose file, temporary-file, or shared-memory transfer media because those introduce path naming, lifetime, permissions, race, visibility, cleanup, and cross-process concerns absent from direct terminal traffic.

Base64 is framing-safe encoding, not encryption.

## 11. Committed graphics output

Both raster backends use committed output semantics through the session serialization boundary.

Before commitment, validation and caller cancellation remain effective. After the first backend frame commits, ordinary caller cancellation is not allowed to intentionally truncate the logical graphics transaction.

For Sixel, the committed unit is the complete DCS transaction through ST and flush. For Kitty Graphics, several complete APC frames may form one logical direct-transfer transaction and remain serialized through the final frame and flush.

If the transport fails after commitment, the error is surfaced. The library does not automatically retry the image, replay uncertain output, switch raster backends, or speculate that additional recovery commands are safe.

Session teardown drains committed output before output-state restoration continues.

## 12. No generic raw graphics or capability escape hatch

The stable semantic surface intentionally does not expose:

- a generic public DCS/Sixel writer;
- a generic public APC/Kitty Graphics writer;
- caller-selected Sixel/Kitty raster routing;
- arbitrary Kitty control-data dictionaries;
- raw capability-probe frame/matcher APIs;
- public backend-routing scores;
- arbitrary internal evidence-ledger entries;
- arbitrary terminfo capability-name probing;
- persistent Kitty image/placement ids through the common raster API.

`TerminalSession.Output` remains a public advanced borrowed transport and can be misused by a caller. Direct writes through it are outside session serialization and semantic validation; that escape hatch is not an endorsement of constructing arbitrary untrusted terminal traffic.

## 13. Image decoding is out of scope

`Icod.Terminal` consumes bounded raw pixel/index data. It does not decode PNG, JPEG, GIF, or other image files as part of raster display.

This avoids importing file-parser attack surface, metadata handling, decompression-bomb policy, color-profile interpretation, and format-specific security decisions into the core live-terminal package.

Applications may decode image formats with libraries appropriate to their own trust model, then provide bounded raw raster data to `Icod.Terminal`.

## 14. Clipboard privacy — OSC 52

Clipboard writes can place application data into terminal or desktop selection state. Clipboard reads request external selection data and are explicitly privacy-sensitive.

`ReadClipboardAsync(...)` is never called automatically by session open, capability inspection, capability verification unrelated to clipboard, graphics probing, lifecycle handling, or disposal.

Applications should treat returned clipboard bytes as untrusted external input and should not publish secrets to terminal clipboard state unintentionally.

## 15. Current-location and shell metadata disclosure

OSC 7, OSC 9;9, OSC 633 `Cwd`, OSC 1337 `CurrentDir`, and related semantic metadata can reveal user names, source-tree names, customer/project names, mount points, shares, and host identity.

`Icod.Terminal` does not automatically discover and publish environment/current-directory/shell-history data. The caller decides whether disclosure is appropriate.

## 16. Hyperlink security — OSC 8

The library validates hyperlink framing and URI syntax but does not decide whether a URI is safe for a particular application to expose to users.

It does not fetch targets, resolve DNS, launch browsers/shells, or apply a universal URI-scheme trust policy.

## 17. Desktop notification privacy and trust

OSC 9, OSC 777, and OSC 99 notifications can leave the terminal window and appear in desktop notification surfaces, logs, recordings, screen sharing, or accessibility software.

Applications should not place secrets in notification content unless that disclosure is intended. Base64 used by OSC 99 is encoding, not encryption.

Version 1.9 notification activation/button/close reports are validated but unauthenticated terminal-controlled input. A malicious or compromised terminal path can fabricate notification identifiers, activation events, button numbers, close events, and close-tracking results.

An identifier is correlation data, not a capability token, trusted desktop handle, cryptographic proof, or evidence that a trusted human performed an action. Applications must not use these events as an authorization boundary without their own independent security mechanism.

## 18. Modern keyboard, focus, mouse, and paste privacy

Modern keyboard protocols can expose press/repeat/release phase, associated text, shifted/base-layout identities, and modifier state. Focus/mouse reports expose interaction context. Bracketed-paste data may contain arbitrary user text.

Applications should collect, log, and transmit only what they need. Bracketed paste marks provenance and boundaries; it does not make pasted content safe to execute.

## 19. Terminal observations can fingerprint the environment

Explicit queries and capability verification can reveal terminal/environment characteristics such as device attributes, supported protocol families, graphics support, cursor/color state, clipboard state, or notification support.

Applications should issue only observations they need.

`InspectCapability(...)` performs no terminal I/O. `VerifyCapabilityAsync(...)` is explicit so the application controls whether the benefit of stronger capability evidence justifies the terminal traffic and possible fingerprinting signal.

`DisplayRasterAsync(...)` may use the reviewed raster capability/probe machinery when necessary; it does not conduct broad emulator-brand fingerprinting.

## 20. Redirected endpoints

Semantic operations that require a live terminal reject known redirected/non-terminal output rather than blindly writing control bytes into a file or pipe.

Active queries additionally require compatible interactive input/output endpoints through the shared query contract.

Capability support knowledge is not rewritten merely because the current endpoint is unavailable. Endpoint availability is represented separately from support truth.

## 21. Restoration, lifecycle, and evidence invalidation

When `Icod.Terminal` claims exact restoration, it establishes a truthful baseline first. Unknown state is not replaced by a guessed default while being described as restoration.

Suspend/resume and explicit invalidation are trust boundaries for live observations. Generation-scoped live probe/protocol-response evidence expires through the semantic evidence generation mechanism. Immutable selected description/profile evidence may persist because it describes static session configuration rather than a prior live observation.

Already-returned `TerminalCapabilityStatus` values are immutable snapshots. They do not update themselves across lifecycle changes; callers inspect again when current knowledge matters.

Raster images and notification observations are ephemeral output/observations. They are not automatically replayed after resume and are not represented as exactly restorable terminal state.

## 22. Dependencies and native boundaries

Native platform APIs are used only for terminal-control/lifecycle operations that require them. The package does not hide PTY process hosting, shell execution, browser/network access, OS clipboard integration, image decoding, or native desktop notification APIs behind terminal semantic methods.

`Icod.Terminal.csproj` remains the package authority for its direct NuGet dependencies. Tests, samples, and tools do not need to duplicate exact transitive dependency versions as a security or compatibility mechanism; successful restore/build of the declared package graph is the dependency witness.

Sixel, Kitty Graphics, notification protocols, and capability probes are terminal traffic only.

## 23. Reporting security issues

Security defects should be reported through the repository owner's supported private security-reporting channel when available rather than publishing exploitable details before a fix can be prepared.

Compatibility or missing-feature requests should remain distinct from security reports.

## 24. Permanent security principles

For stable 1.x, new features should preserve these principles:

1. expose semantic intent rather than generic dangerous protocol dispatch;
2. validate and bound untrusted payloads before commitment where possible;
3. keep parsing, conversion, event buffering, capability verification, and resynchronization bounded;
4. preserve one authoritative input/query/event reader;
5. do not infer support solely from brand/environment identity;
6. separate capability support, endpoint availability, and evidence lifetime;
7. keep ordinary inspection side-effect free and make terminal probing explicit;
8. treat correlation or semantic recognition as bounded ownership rather than trust;
9. distinguish emission from terminal application or acknowledgement;
10. treat unsolicited semantic events and query responses as unauthenticated external input;
11. make metadata disclosure explicit;
12. do not claim exact restoration without a truthful baseline;
13. surface uncertainty and compound failures rather than hiding them;
14. avoid hidden host execution, network access, file decoding, or process-global side effects;
15. once a terminal graphics transaction is committed, preserve logical-transfer integrity rather than using ordinary caller cancellation to truncate it;
16. never automatically replay or switch backends after partial committed graphics failure;
17. do not turn typed semantic event or capability-planning support into a generic raw vendor/protocol bus;
18. do not expose dependency/backend provenance as authentication or terminal identity.
