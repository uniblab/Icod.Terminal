# Security and Privacy

`Icod.Terminal` mediates a bidirectional terminal conversation. Terminal control sequences are not merely visual formatting: some operations publish metadata, request external state, alter terminal-owned presentation state, influence desktop integration, display raster graphics, create terminal-resident raster resources/placements/placeholders, or report unsolicited terminal-side observations.

This document defines the permanent 1.x security and privacy boundary.

## 1. Trust model

`Icod.Terminal` assumes that:

- application-supplied arguments may be untrusted;
- terminal input, unsolicited semantic events, capability responses, graphics acknowledgements, placeholder acknowledgements, and query responses are external input and may be malformed or adversarial;
- the attached terminal, multiplexer, remote session, or transport may fabricate otherwise well-formed observations;
- successful byte transmission does not prove terminal-side support or application;
- acknowledged persistent graphics identity remains terminal-controlled external state rather than an authentication primitive;
- metadata and raster content may be logged, persisted, forwarded, recorded, or exposed outside the process;
- large raster inputs, replies, and terminal-resident ownership bookkeeping can create resource pressure.

The library therefore favors typed semantic APIs, bounded parsing/encoding, pre-output validation, explicit capability evidence, opaque ownership handles, and one authoritative input/query/event path over generic raw protocol construction.

## 2. Control-sequence injection boundary

Text-bearing semantic protocols validate payloads according to their reviewed grammar before commitment where possible. Where raw controls are not semantic data, C0/DEL/C1 controls are rejected so caller text cannot inject unrelated terminal sequences.

Examples of reviewed encoding boundaries include strict UTF-8 percent encoding, Base64 where required by protocol, closed metadata grammars, and typed color/pointer/keyboard/notification/raster APIs.

Kitty Graphics raw image bytes use protocol-defined Base64 within a typed bounded APC dialect. Persistent raster APIs expose opaque resources/placements/placeholders plus typed options/tokens rather than caller-supplied control dictionaries or numeric protocol identities.

Unicode-placeholder cells are generated from opaque semantic tokens. Public callers do not provide the reserved placeholder codepoint, combining-mark identity table, SGR identity packing, image id, virtual-placement id, or raw APC commands.

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

Persistent-raster session bookkeeping is bounded:

```text
maximum live persistent resources          256
maximum live physical + virtual placements 4096
maximum relative-placement depth              8
physical placement Columns / Rows          1..16384 when supplied
placeholder Columns / Rows                  1..256, required
private virtual-placement id                1..0x00FFFFFF
```

Source rectangles do not increase source raster ceilings. Rectangle scalar values are bounded, dimensions must be positive, coordinates non-negative, and every present rectangle—including `default(TerminalRasterSourceRectangle)` values that bypass the public constructor—is revalidated before the complete rectangle is checked against the owning resource and before output. Signed z-order consumes only a bounded `int` value and adds no unbounded layer registry.

Relevant framing/resource ceilings remain bounded, including normal response frames, APC/DCS frames, Kitty Base64 chunk data, notification metadata, Sixel quantization state, virtual-placement allocation, and placeholder cell coordinates.

Registry exhaustion returns controlled `Unavailable` before protocol output rather than unbounded local growth.

## 4. One authoritative input reader

A live `TerminalSession` owns the authoritative input decoder, query router, and unsolicited semantic-event classifier.

The stable routing order is:

```text
active query response
    -> recognized unsolicited semantic event
        -> ordinary application input
```

A query-owned response is never also delivered as ordinary input/semantic event. Sixel capability observation, Kitty Graphics support probing, capability verification, persistent-raster acknowledgements, and virtual-placeholder acknowledgements all reuse this same authority.

No source-rectangle, z-order, relative-placement, lifecycle-observation, or placeholder feature adds a graphics-specific reader or side channel.

## 5. Capability evidence is not identity

`TERM`, emulator names, environment values, host OS, registry membership, or caller backend preference are context rather than sufficient live support proof.

Static description evidence and generation-scoped live evidence remain distinct. Timeout or silence is not automatically `Unsupported`; caller cancellation is not negative capability evidence.

`InspectCapability(...)` emits no terminal traffic. `VerifyCapabilityAsync(...)` is explicit because verification may emit bounded probes. A `Verified` observation means support was established under the reviewed contract; it is not authentication of a terminal, host, desktop session, multiplexer, or user.

Persistent resource creation requires a usable persistent-raster route and does not perform an unreviewed hidden support probe.

`UnicodeRasterPlaceholders` is a separate semantic capability. Generic Kitty/persistent evidence does not silently prove placeholder support. Where no truthful independent probe exists, acknowledged placeholder creation can strengthen live placeholder evidence for that transaction rather than inventing unsupported certainty.

## 6. Correlation grants ownership, not trust

A response that matches an active transaction identity is still terminal-controlled untrusted input.

Matching private image/placement identities grant bounded routing ownership only. Numeric parsing, duplicate-field rejection, grammar, termination, and response-size bounds still apply.

A stale late resource, placement, or placeholder acknowledgement cannot complete a later operation with a different private identity. Timeout/late-response handling remains bounded through the existing query manager.

A well-formed correlated `ENOENT` invalidates the library's certainty about the affected terminal-resident object. It does not authenticate why the object disappeared or imply anything about unrelated state.

A correlated virtual-parent loss affects the dependent relative-placement subtree without automatically declaring independently owned child raster resources missing.

## 7. Emission and acknowledgement

For unacknowledged protocols, successful completion generally means requested bytes were written; it does not prove visual application.

For acknowledged persistent resource/placement/placeholder operations, success means a well-formed correlated response was accepted under the protocol contract. It still does not authenticate the terminal or guarantee future persistence/visual stacking.

`ZIndex` is therefore a requested signed stacking order, not a verified global scene order.

Placeholder-cell output is ordinary committed terminal text output encoded from a validated semantic token. A successful write does not prove the terminal rendered the intended raster cell.

## 8. Raster input, source cropping, and virtual presentation

`TerminalRasterImage` owns an immutable snapshot of caller-provided bounded raw raster data.

Source rectangles select a subset of those source pixels for one physical placement. Resource state stores only immutable source dimensions needed to validate the rectangle; it does not retain arbitrary source pixel bytes after persistent creation for replay.

Virtual placeholders refer to an already-owned persistent raster resource. They do not fetch files, create shared memory, or add another source-raster storage path. The placeholder token exposes row/column semantics while protocol identity remains private.

Cropping and placeholder presentation do not disclose path names or widen external-storage attack surface. They operate on already-owned raster resources.

## 9. Deterministic bounded graphics work

Sixel and Kitty graphics paths retain bounded deterministic work and lazy output. The library does not silently choose file, temporary-file, or shared-memory Kitty transfer because those introduce path naming, lifetime, permissions, visibility, race, cleanup, and cross-process concerns absent from direct terminal traffic.

Persistent resource upload continues to use direct transfer.

Placeholder encoding uses a fixed bounded Unicode/SGR transformation for one semantic cell; it does not inspect arbitrary neighboring terminal content or depend on a mutable public encoding table.

Base64 is protocol framing, not encryption.

## 10. Committed graphics output

Raster and persistent-raster operations use session serialization and committed-output semantics.

Before commitment, validation/cancellation remain effective. After commitment, ordinary caller cancellation does not intentionally truncate a logical graphics transaction.

If transport fails after commitment, the error is surfaced. The library does not automatically replay, switch raster backends, or speculate that recovery commands are safe.

Source-rectangle validation occurs before placement output commitment. Invalid create/update rectangles therefore produce no new placement output.

Placeholder token validity is checked before output and revalidated after output-gate acquisition. Stale, released, disposed, cross-session, or invalid-generation tokens do not emit private identity. Failure while writing visible placeholder text is an output failure; it does not by itself fabricate resource-missing truth.

## 11. Persistent raster ownership — 1.11+

The public surface exposes opaque `TerminalRasterResource`, `TerminalRasterPlacement`, and `TerminalRasterPlaceholder` objects. Numeric protocol identities remain private implementation state so callers cannot forge the semantic ownership model.

Successful resource creation retains bounded ownership/source-dimension metadata, not a hidden arbitrary source-image cache.

Persistent identities are generation scoped. `InvalidateState()` and lifecycle changes make existing identity certainty stale. Stale mutation/output returns controlled failure before stale identity is emitted; stale disposal releases local ownership without emitting stale numeric identifiers.

While current, cleanup is descendant-first. Relative physical descendants are released before physical or virtual parents; placements/placeholders are removed before owning resource data. Local ownership is released even if terminal cleanup transport fails, preventing ambiguous retry ownership.

## 12. Placement and placeholder typed-data boundary

`TerminalRasterSourceRectangle`, `TerminalRasterPlacementOptions`, `TerminalRasterPlaceholderOptions`, and `TerminalRasterPlaceholderCell` are typed semantic data, not raw protocol fragments.

Security-relevant guarantees include:

- rectangle constructor/options validation enforces bounded source geometry;
- resource-aware validation prevents a crop from escaping uploaded raster dimensions;
- widened arithmetic prevents boundary arithmetic from overflowing before comparison;
- a present crop is encoded as one complete reviewed four-field tuple;
- z-order is formatted from a signed `int` using invariant decimal formatting;
- placeholder rows/columns are required within `1..256`;
- `GetCell(...)` rejects coordinates outside the placeholder dimensions;
- cell tokens cannot be publicly constructed from numeric protocol identity;
- every emitted cell is self-contained rather than relying on neighbor identity inheritance;
- private SGR identity channels are selectively reset after each cell;
- none of these public types exposes raw image/placement/generation ids or arbitrary control keys;
- no option creates unbounded scene/layer/cell bookkeeping.

Applications should not treat z-order, placeholder ownership, or rendered cell position as an authorization/visibility boundary. A terminal controls final rendering and may ignore, reinterpret, evict, or externally compose terminal output.

## 13. No generic raw graphics/capability escape hatch

The stable semantic surface intentionally does not expose:

- generic public DCS/Sixel or APC/Kitty writers;
- caller-selected production Sixel/Kitty routing;
- arbitrary Kitty control dictionaries;
- raw capability-probe frame/matcher APIs;
- public routing scores or arbitrary evidence-ledger mutation;
- caller-selected Kitty image/image-number/physical/virtual placement ids;
- public reserved placeholder codepoint/diacritic/SGR packing helpers;
- mutable/reparentable scene graphs;
- hidden persistent source-image caching/replay.

`TerminalSession.Output` remains an advanced borrowed transport and can be misused; direct writes through it are outside ordinary session serialization/semantic validation.

## 14. TermInfo 1.14 advisory backend planning

The active 1.17 repository directly depends on `Icod.TermInfo 1.15.0`. Optional integration tests and samples use `Icod.TermInfo.Inspection 1.15.0`.

Inspection's `RasterBackendPlanner` classifies advisory Sixel/Kitty availability and applies explicit caller preference. This is **not** production Terminal routing and is not a security/authentication oracle.

The qualified mapping boundary is intentionally narrow:

- a conclusive live `PersistentRasterGraphics` observation may be caller-mapped to Kitty Graphics availability because Terminal's reviewed persistent route is Kitty-based;
- ordinary `RasterGraphics` does not identify which concrete backend is available and must not be mapped to one by assumption;
- `UnicodeRasterPlaceholders` does not become TermInfo 1.14 lifecycle/placement evidence merely because the current implementation is Kitty-based;
- separate backend contexts are maintained so evidence for Kitty does not silently strengthen Sixel lifecycle/placement truth;
- no terminal-brand or emulator-name heuristic is introduced;
- no explicit preference in Inspection silently changes Terminal's production routing policy.

Applications using Inspection must treat its output as planning evidence. Before Terminal commits output, Terminal's own current capability/routing/ownership checks remain authoritative.

`Icod.TermInfo.Inspection` and `Icod.TermInfo.Source` remain absent from the production package graph.

## 15. Image decoding remains out of scope

`Icod.Terminal` consumes bounded raw pixel/index data. It does not decode PNG/JPEG/GIF or other image files as part of raster display. This avoids importing file-parser, decompression-bomb, metadata, and color-profile attack surfaces into the core live-terminal package.

## 16. Metadata and privacy-sensitive output

Clipboard data, current-location metadata, hyperlinks, desktop notifications, shell/prompt metadata, command lines, and raster pixels may escape the application process through terminal/desktop/recording surfaces.

The caller decides whether disclosure is appropriate. `Icod.Terminal` does not automatically discover/publish filesystem paths, shell history, clipboard data, or other privacy-sensitive context merely because a terminal protocol can carry it.

## 17. Terminal observations can fingerprint the environment

Explicit queries and capability verification can reveal terminal/environment characteristics. This is why live verification is explicit and bounded rather than an automatic side effect of ordinary inspection.

Applications should avoid unnecessary probing when privacy or anti-fingerprinting concerns outweigh the value of stronger capability evidence.

TermInfo backend planning does not justify extra probes by itself; runtime verification remains an explicit caller decision performed outside Inspection.

## 18. Dependency boundary

The active 1.17 production package graph is:

```text
Icod.TermInfo 1.15.0
Icod.Timing   1.0.0
```

`Icod.TermInfo.Inspection 1.15.0` is test/sample-only where used. Inspection, Source, image decoders, scene/layout libraries, and graphics toolkits are not added to the production graph.

Historical release records retain the dependency versions they actually shipped.

## 19. Persistent raster animation — 1.16

The animation surface is typed and protocol neutral. Public callers receive a resource-owned controller and opaque frame tokens; they cannot supply or observe Kitty image ids, frame numbers, generation ids, raw animation dictionaries, or backend selectors.

Locally checkable animation input is bounded before output:

- added frames must match the resource's intrinsic dimensions;
- durations must be exact positive whole milliseconds within the signed 32-bit range;
- finite repeat counts must be in `1..int.MaxValue - 1`;
- frame tokens must belong to the exact animation/session/generation;
- the session-wide known-frame budget is 4096, including roots;
- only one append reservation may be active per animation.

Frame pixels use the reviewed direct-transfer path. Terminal does not introduce file names, temporary files, shared memory, image decoders, or retained source-frame caches for animation.

Animation acknowledgements remain untrusted terminal input. Correlation routes one response to one bounded operation; grammar, numeric fields, duplicate fields, identities, and status are still validated. An ambiguous committed append loses sequence certainty without inventing a token, retrying output, or declaring the otherwise current resource missing.

`InspectCapability(PersistentRasterAnimation)` is side-effect free. Explicit verification does not manufacture a durable-state probe merely to fingerprint support. A successful real frame append can establish current-generation live evidence for the completed semantic operation.

Applications must not treat animation state, selected frame, timing, looping, placement, or visual coverage as a security boundary. The terminal controls final rendering and may ignore, evict, reinterpret, record, or externally compose output.

## 20. Semantic screen planning and output commitment — 1.17 and 1.18

Terminal-profile facts are immutable projections of the selected description, not authenticated live observations. Applications must not treat declared screen capabilities, dimensions, cursor position, rendition state, terminal content, or successful output as a security boundary or as proof of what a terminal ultimately rendered.

Screen planning is side-effect free and returns opaque reviewed plans. Public callers cannot inject raw capability identifiers, expansion programs, padding directives, or arbitrary terminal strings through the planner. Dimensions, coordinates, counts, colors, regions, payload bytes, operation count, and aggregate transaction payload are validated and bounded before commitment.

Unknown physical rendition is not treated as a known default. `PlanRenditionBaseline()` derives restoration obligations from selected-profile entry/selection evidence and returns `null` if any exposed axis lacks unconditional restoration. This prevents higher layers from silently accepting a partial reset as a safe baseline. Reset and selection strings remain private TermInfo-derived data inside the opaque plan.

Plans, hyperlink content, and raster-placeholder cells retain exact session/owner identity. A transaction rejects foreign, stale, released, or disposed retained items before output. Creation captures the serialized-output epoch; intervening session-owned output invalidates the batch before commitment rather than allowing an outdated retained-screen decision to be emitted.

Commit holds the existing output gate across the logical batch, optional synchronized-output framing, required hyperlink cleanup, one final flush, and gate release. Pre-commit cancellation emits nothing. After commitment, cleanup is attempted without ordinary caller cancellation; independent primary and cleanup failures are flattened in deterministic order. Failure does not trigger blind replay or claim that the terminal applied none, some, or all of the bytes.

Application text and hyperlink labels remain disclosure surfaces. The caller remains responsible for deciding whether terminal-visible content and hyperlink targets are appropriate. Strict hyperlink validation prevents control-character framing injection but does not authenticate or make a URI safe to follow.

## 21. Stable exclusions after 1.18

Security/privacy behavior does not include promises for:

- terminal authenticity;
- cryptographic integrity/confidentiality of terminal protocol traffic;
- scene-graph ordering across independent applications;
- caller-manufactured protocol identities;
- terminal-owned absolute screen/layout policy;
- pixel-within-cell positioning;
- automatic placeholder redraw or screen-position tracking;
- partial-frame animation updates, frame composition, and delta editing;
- automatic persistent-raster replay/re-upload;
- Sixel persistent/placeholder emulation;
- hidden image caches;
- image-file decoding;
- PTY/ConPTY process hosting.

Relative placement, lifecycle observation, Unicode placeholder virtual placement, and resource-owned animation are bounded semantic ownership/presentation features; they do not weaken these exclusions.
