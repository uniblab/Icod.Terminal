# Security and Privacy

`Icod.Terminal` mediates a bidirectional terminal conversation. Terminal control sequences are not merely visual formatting: some operations publish metadata, request external state, alter terminal-owned presentation state, influence desktop integration, display raster graphics, create terminal-resident raster resources/placements, or report unsolicited terminal-side observations.

This document defines the permanent 1.x security and privacy boundary.

## 1. Trust model

`Icod.Terminal` assumes that:

- application-supplied arguments may be untrusted;
- terminal input, unsolicited semantic events, capability responses, graphics acknowledgements, and query responses are external input and may be malformed or adversarial;
- the attached terminal, multiplexer, remote session, or transport may fabricate otherwise well-formed observations;
- successful byte transmission does not prove terminal-side support or application;
- acknowledged persistent graphics identity remains terminal-controlled external state rather than an authentication primitive;
- metadata and raster content may be logged, persisted, forwarded, recorded, or exposed outside the process;
- large raster inputs, replies, and terminal-resident ownership bookkeeping can create resource pressure.

The library therefore favors typed semantic APIs, bounded parsing/encoding, pre-output validation, explicit capability evidence, opaque ownership handles, and one authoritative input/query/event path over generic raw protocol construction.

## 2. Control-sequence injection boundary

Text-bearing semantic protocols validate payloads according to their reviewed grammar before commitment where possible. Where raw controls are not semantic data, C0/DEL/C1 controls are rejected so caller text cannot inject unrelated terminal sequences.

Examples of reviewed encoding boundaries include strict UTF-8 percent encoding, Base64 where required by protocol, closed metadata grammars, and typed color/pointer/keyboard/notification/raster APIs.

Kitty Graphics raw image bytes use protocol-defined Base64 within a typed bounded APC dialect. Persistent raster APIs expose opaque resources/placements plus typed placement options rather than caller-supplied control dictionaries or numeric protocol identities.

Validation protects framing integrity. It does not make content confidential, authentic, or trustworthy.

## 3. Bounded resources

Input decoding, paste handling, query transactions, unsolicited semantic reports, application-event buffering, request/response frames, late-response ownership, resynchronization, capability verification, graphics processing, and persistent-raster bookkeeping are bounded.

Public raster ceilings remain:

```text
maximum raster dimension       16,384
maximum raster pixels          16 Mi
maximum owned pixel bytes      64 MiB
maximum indexed palette        256 entries
```

Persistent-raster session bookkeeping remains:

```text
maximum live persistent resources   256
maximum live persistent placements  4096
placement Columns / Rows             1..16384 when supplied
```

Version 1.12 adds source rectangles without increasing source raster ceilings. Rectangle scalar values are bounded, dimensions must be positive, coordinates non-negative, and every present rectangle—including `default(TerminalRasterSourceRectangle)` values that bypass the public constructor—is revalidated before the complete rectangle is checked against the owning resource and before output. Signed z-order consumes only a bounded `int` value and adds no unbounded layer registry.

Relevant framing/resource ceilings remain bounded, including normal response frames, APC/DCS frames, Kitty Base64 chunk data, notification metadata, and Sixel quantization state.

Registry exhaustion returns controlled `Unavailable` before protocol output rather than unbounded local growth.

## 4. One authoritative input reader

A live `TerminalSession` owns the authoritative input decoder, query router, and unsolicited semantic-event classifier.

The stable routing order is:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

A query-owned response is never also delivered as ordinary input/semantic event. Sixel capability observation, Kitty Graphics support probing, capability verification, and persistent-raster acknowledgements all reuse this same authority.

No source-rectangle or z-order feature adds a graphics-specific reader or side channel.

## 5. Capability evidence is not identity

`TERM`, emulator names, environment values, host OS, registry membership, or caller backend preference are context rather than sufficient live support proof.

Static description evidence and generation-scoped live evidence remain distinct. Timeout or silence is not automatically `Unsupported`; caller cancellation is not negative capability evidence.

`InspectCapability(...)` emits no terminal traffic. `VerifyCapabilityAsync(...)` is explicit because verification may emit bounded probes. A `Verified` observation means support was established under the reviewed contract; it is not authentication of a terminal, host, desktop session, multiplexer, or user.

Persistent resource creation requires current verified persistent-raster capability and does not perform a hidden additional support probe.

## 6. Correlation grants ownership, not trust

A response that matches an active transaction identity is still terminal-controlled untrusted input.

Matching private image/placement ids grant bounded routing ownership only. Numeric parsing, duplicate-field rejection, grammar, termination, and response-size bounds still apply.

A stale late placement acknowledgement cannot complete a later placement with a different private identity. Timeout/late-response handling remains bounded through the existing query manager.

A well-formed correlated `ENOENT` invalidates the library's certainty about the affected terminal-resident object. It does not authenticate why the object disappeared or imply anything about unrelated state.

## 7. Emission and acknowledgement

For unacknowledged protocols, successful completion generally means requested bytes were written; it does not prove visual application.

For acknowledged persistent resource/placement operations, success means a well-formed correlated response was accepted under the protocol contract. It still does not authenticate the terminal or guarantee future persistence/visual stacking.

`ZIndex` is therefore a requested signed stacking order, not a verified global scene order and not a relative-placement relationship.

## 8. Raster input and source cropping

`TerminalRasterImage` owns an immutable snapshot of caller-provided bounded raw raster data.

Version 1.12 source rectangles select a subset of those source pixels for one placement. Resource state stores only immutable source dimensions needed to validate the rectangle; it does not retain arbitrary source pixel bytes after persistent creation for replay.

Cropping does not fetch files, create shared memory, disclose path names, or widen external-storage attack surface. It only changes which already-owned source pixels participate in a placement.

## 9. Deterministic bounded graphics work

Sixel and Kitty graphics paths retain bounded deterministic work and lazy output. The library does not silently choose file, temporary-file, or shared-memory Kitty transfer because those introduce path naming, lifetime, permissions, visibility, race, cleanup, and cross-process concerns absent from direct terminal traffic.

Persistent resource upload continues to use direct transfer.

Base64 is protocol framing, not encryption.

## 10. Committed graphics output

Raster and persistent-raster operations use session serialization and committed-output semantics.

Before commitment, validation/cancellation remain effective. After commitment, ordinary caller cancellation does not intentionally truncate a logical graphics transaction.

If transport fails after commitment, the error is surfaced. The library does not automatically replay, switch raster backends, or speculate that recovery commands are safe.

Source-rectangle validation occurs before placement output commitment. Invalid create/update rectangles therefore produce no new placement output and do not acquire a new acknowledged transaction unnecessarily.

## 11. Persistent raster ownership — 1.11+

The public surface exposes opaque `TerminalRasterResource` and `TerminalRasterPlacement` objects. Numeric protocol identities remain private implementation state so callers cannot forge the semantic ownership model.

Successful resource creation retains bounded ownership/source-dimension metadata, not a hidden arbitrary source-image cache.

Persistent identities are generation scoped. `InvalidateState()` and lifecycle changes make existing identity certainty stale. Stale mutation returns controlled `Unavailable`; stale disposal releases local ownership without emitting stale numeric identifiers.

While current, cleanup is child-first: placements are deleted before resource data. Local ownership is released even if terminal cleanup transport fails, preventing ambiguous retry ownership.

## 12. 1.12 placement options and trust boundary

`TerminalRasterSourceRectangle` and `TerminalRasterPlacementOptions.ZIndex` are typed data, not raw protocol fragments.

Security-relevant guarantees:

- constructor validation enforces the public rectangle scalar contract;
- placement options revalidate every present rectangle, including default struct values that bypass the constructor;
- resource-aware validation prevents a crop from escaping the uploaded raster dimensions;
- widened arithmetic prevents boundary arithmetic from overflowing before comparison;
- a present crop is encoded as one complete reviewed four-field tuple;
- z-order is formatted from a signed `int` using invariant decimal formatting;
- neither option exposes public image/placement ids or arbitrary control keys;
- neither option adds unbounded scene/layer bookkeeping.

Applications should not treat z-order as an authorization or visibility boundary. A terminal controls final rendering and may ignore, reinterpret, or externally compose terminal output.

## 13. No generic raw graphics/capability escape hatch

The stable semantic surface intentionally does not expose:

- generic public DCS/Sixel or APC/Kitty writers;
- caller-selected Sixel/Kitty routing;
- arbitrary Kitty control dictionaries;
- raw capability-probe frame/matcher APIs;
- public routing scores or arbitrary evidence-ledger entries;
- caller-selected Kitty image/image-number/placement ids;
- a generic relative-placement graph, scene engine, or animation system;
- hidden persistent source-image caching/replay.

`TerminalSession.Output` remains an advanced borrowed transport and can be misused; direct writes through it are outside ordinary session serialization/semantic validation.

## 14. Image decoding remains out of scope

`Icod.Terminal` consumes bounded raw pixel/index data. It does not decode PNG/JPEG/GIF or other image files as part of raster display. This avoids importing file-parser, decompression-bomb, metadata, and color-profile attack surfaces into the core live-terminal package.

## 15. Metadata and privacy-sensitive output

Clipboard data, current-location metadata, hyperlinks, desktop notifications, shell/prompt metadata, command lines, and raster pixels may escape the application process through terminal/desktop/recording surfaces.

The caller decides whether disclosure is appropriate. `Icod.Terminal` does not automatically discover/publish filesystem paths, shell history, clipboard data, or other privacy-sensitive context merely because a terminal protocol can carry it.

## 16. Terminal observations can fingerprint the environment

Explicit queries and capability verification can reveal terminal/environment characteristics. This is why live verification is explicit and bounded rather than an automatic side effect of ordinary inspection.

Applications should avoid unnecessary probing when privacy or anti-fingerprinting concerns outweigh the value of stronger capability evidence.

## 17. Dependency boundary

Version 1.12 does not add a production dependency for image decoding, scene layout, TermInfo Inspection/Source, or any graphics toolkit.

The production package graph remains:

```text
Icod.TermInfo 1.11.0
Icod.Timing   1.0.0
```

Additional test/sample consumers used for qualification do not widen the package's runtime trust/dependency boundary.

## 18. Stable exclusions after 1.12

Security/privacy behavior does not include promises for:

- terminal authenticity;
- cryptographic integrity/confidentiality of terminal protocol traffic;
- scene-graph ordering across independent applications;
- relative placement graphs or caller-manufactured protocol identities;
- Unicode placeholder virtual placement;
- animation/frame lifecycle;
- automatic persistent-raster replay/re-upload;
- Sixel persistent emulation;
- hidden image caches;
- image-file decoding;
- PTY/ConPTY process hosting.

Source rectangles and signed z-order are bounded placement inputs only; they do not weaken these exclusions.
