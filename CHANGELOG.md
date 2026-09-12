# Changelog

Notable changes to `Icod.Terminal` are recorded here for consumers who need a concise release history. Detailed design evidence remains in the versioned roadmaps, tranche records, and public-API baseline documents.

## 1.12.0

### Advanced persistent-raster placement geometry

- Adds immutable `TerminalRasterSourceRectangle` with zero-based source-pixel `X` / `Y` and positive `Width` / `Height`.
- Adds `TerminalRasterPlacementOptions.SourceRectangle` and `.ZIndex` while preserving all existing placement options and public signatures.
- Validates source rectangles against immutable owning-resource dimensions before placement output; invalid create/update rectangles produce no new placement traffic.
- Accepts the complete signed `int` z-order domain and formats it deterministically using invariant signed decimal output.
- Routes source crop, cell extents, and z-order through the same acknowledged create/update placement transaction in deterministic `x,y,w,h,c,r,z` order.
- Preserves existing 1.11 placement bytes and behavior when the advanced options are omitted.

### Ownership, hardening, and qualification

- Keeps terminal image/placement identities opaque, placement position at the current cursor, generation-scoped ownership, child-before-resource cleanup, direct transfer, and the existing 256-resource / 4096-placement ceilings.
- Adds exact-edge crop, combined-option, invalid-no-output, `int.MinValue` / `int.MaxValue`, wrong-identity, malformed/duplicate-field, correlated `ENOENT`, timeout/late-response, generation-invalidation, stale-disposal, and repeated advanced ownership-cycle coverage.
- Table-drives internal `TerminalTermInfoSemanticEvidence` rules without changing public evidence semantics, routing behavior, or package dependencies.
- Extends the backend-neutral persistent-raster sample with source cropping and nonzero z-order while continuing to exclude protocol ids/backend branching.
- Extends fresh NuGet-only package consumption and generated XML-documentation checks for the new source-rectangle/z-order surface on `net8.0`, `net9.0`, and `net10.0`.
- Retains current `Icod.DCurses` downstream acceptance/hardening with no required downstream code change.
- Finalizes the 1.12 public API fingerprint as `eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8` while retaining all historical baselines unchanged.
- Preserves production dependencies at `Icod.TermInfo 1.11.0` and `Icod.Timing 1.0.0`.
- Continues to exclude automatic replay, Sixel persistent-resource emulation, public protocol ids, relative placement graphs, absolute screen-coordinate layout, Unicode placeholders, animation/frame lifecycle, image decoding/transcoding, and PTY/ConPTY hosting.

See `docs/releases/1.12.0.md`, `docs/Persistent-Raster-Ownership.md`, `docs/Public-API-Baseline-1.12.md`, and `Icod.Terminal-1.12.0-Development-Roadmap.md` for the complete 1.12 contract.

## 1.11.1

### TermInfo persistent-raster integration contract

- Adds a dedicated three-TFM integration-contract test project which references `Icod.TermInfo.Inspection 1.11.0` without adding Inspection or Source to the production `Icod.Terminal` dependency graph.
- Defines and tests the consumer-owned translation from conclusive live Terminal capability status to protocol-neutral TermInfo `Verified` lifecycle evidence. `Unknown`, `Advertised`, unrelated capabilities, and endpoint unavailability are not promoted.
- Proves static persistent-raster lifecycle planning can transition from `Indeterminate` through Terminal-owned live verification and caller-owned evidence to a deterministic successful replan.
- Proves verified persistent non-support produces an `Impossible` lifecycle plan without attempting persistent resource creation.
- Proves exact static Icod lifecycle declarations can produce a successful TermInfo plan without planning I/O while Terminal independently retains authority over live endpoint usability and execution.
- Adds `Icod.Terminal.TermInfoPersistentRaster.Sample`, executable architecture documentation for static inspection -> semantic planning -> optional live verification -> caller-owned evidence -> replan -> opaque Terminal resource/placement execution.

### Compatibility and qualification

- Adds no production public API and intentionally retains the final 1.11 public API fingerprint `9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2`.
- Preserves production dependencies at `Icod.TermInfo 1.11.0` and `Icod.Timing 1.0.0`; `Icod.TermInfo.Inspection 1.11.0` remains test/sample-only and `Icod.TermInfo.Source` is not introduced.
- Makes no intentional change to the persistent-raster wire protocol, acknowledgement, ownership, generation invalidation, cleanup, routing, or bounded-resource semantics released in 1.11.0.
- Retains `net8.0`, `net9.0`, and `net10.0`, Windows/Linux/macOS Staging validation, current package/downstream gates, and the stable `1.0.0` compatibility floor.
- T1111-A through T1111-D completed full pull-request qualification on exact head `651af888875a0d2acababe46a4e1313532ec7ba6` in workflow `#1628 / 34704455179`.
- Deliberately defers a table-driven `TerminalTermInfoSemanticEvidence` cleanup, static lifecycle-evidence ingestion, and any adapter package to later independent design work.

See `docs/releases/1.11.1.md` and `Icod.Terminal-1.11.1-Development-Roadmap.md` for the complete patch-release integration contract.

## 1.11.0

### Persistent raster resources and placements

- Adds `TerminalCapability.PersistentRasterGraphics = 9` while preserving all existing `TerminalCapability` numeric values.
- Adds opaque `TerminalRasterResource` and `TerminalRasterPlacement` ownership plus `TerminalRasterPlacementOptions` and `TerminalSession.CreateRasterResourceAsync(...)`.
- Supports multiple placements per terminal-resident resource, current-cursor placement, independently optional `Columns` / `Rows` in `1..16384`, and semantic placement replacement through `TerminalRasterPlacement.UpdateAsync(...)`.
- Keeps Kitty image ids, image numbers, placement ids, APC control dictionaries, and backend selection private to the implementation; the common public API remains semantic and backend-neutral.
- Keeps ordinary `RasterGraphics` separate from `PersistentRasterGraphics`, so verified Sixel can satisfy ephemeral raster display without pretending to provide persistent terminal-resident ownership.

### Acknowledgement, lifecycle, and bounded ownership

- Requires correlated acknowledgement before publishing a persistent resource and correlates placement create/update replies by the private terminal image id and placement id through the existing authoritative query/input path.
- Treats correlated terminal replies as untrusted input: wrong identities do not complete another transaction, malformed/duplicate identity fields are rejected, and response processing remains bounded.
- Treats a well-formed correlated `ENOENT` as loss of terminal-resident certainty, invalidating the affected resource/placement so later operations return controlled `Unavailable` without emitting stale identifiers.
- Bounds live ownership to 256 persistent resources and 4096 placements per session with nonzero collision-safe private identities and explicit wraparound handling.
- Makes persistent identities session-generation scoped: explicit invalidation and lifecycle generation changes stale existing handles without automatic replay, re-upload, or hidden raster retention.
- Makes placement/resource disposal locally idempotent, deletes children before resource data while current, performs local-only cleanup when stale, and surfaces/aggregates cleanup transport failures without uncertain retry ownership.
- Retains Kitty direct transfer only and introduces no file/temp-file/shared-memory transport or source-image cache.

### Qualification and compatibility

- Adds repeated create/place/update/delete stress, repeated generation invalidation, and 8,192-placement registry churn coverage alongside the existing capacity, wraparound, cancellation, transport-failure, redirected-output, and malformed-response tests.
- Adds a fresh NuGet-only persistent-raster consumer and generated XML-documentation validation for `net8.0`, `net9.0`, and `net10.0`.
- Adds `Icod.Terminal.PersistentRaster.Sample`, demonstrating capability verification plus create/place/update/dispose without Kitty/Sixel/backend/id branching.
- Retains Windows/Linux/macOS Staging validation, current `Icod.DCurses` downstream acceptance/soak, and the stable `1.0.0` compatibility floor.
- Finalizes the 1.11 public API fingerprint as `9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2` while retaining all historical baselines unchanged.
- Deliberately excludes automatic replay, Sixel persistence emulation, public protocol ids, source rectangles, z-order, Unicode placeholders, relative/pixel placement, animation, scene-graph ownership, image decoding/transcoding, and PTY/ConPTY hosting.

See `docs/releases/1.11.0.md`, `docs/Persistent-Raster-Ownership.md`, `docs/Public-API-Baseline-1.11.md`, `docs/C118-1.11.0-Persistent-Raster-Adversarial-Downstream-and-Package-Qualification.md`, `docs/C119-1.11.0-Release-Closure.md`, and `Icod.Terminal-1.11.0-Development-Roadmap.md` for the complete 1.11 contract.

## 1.10.0

### Dependency decoupling and capability planning

- Removes duplicated exact `Icod.TermInfo` and `Icod.Timing` version assertions from active package verification; `Icod.Terminal.csproj` remains the package authority for direct dependency requirements while tests, samples, and tools avoid independent transitive-version pins.
- Treats successful restore/build of the package graph as the dependency-compatibility witness instead of making one resolved dependency version part of the behavioral contract.
- Adds a curated protocol-neutral `TerminalCapability` vocabulary plus immutable `TerminalCapabilityStatus`, `TerminalCapabilitySupport`, `TerminalCapabilityEndpointAvailability`, and `TerminalCapabilityEvidenceKind` planning types.
- Keeps public evidence dependency-neutral: callers see `None`, `StaticDescription`, or `LiveObservation` rather than `Icod.TermInfo`, OSC/CSI/DCS/APC, concrete backends, routing scores, or terminal-brand heuristics.
- Adds side-effect-free `TerminalSession.InspectCapability(...)`, which projects current semantic support evidence and endpoint availability without emitting terminal bytes or performing a hidden live query.
- Adds explicit bounded `TerminalSession.VerifyCapabilityAsync(...)`; verification reuses only existing reviewed support probes, currently modern keyboard reporting and raster graphics, and does not invent protocol traffic for capabilities that lack a safe bounded probe.
- Keeps endpoint availability separate from support knowledge so a capability may remain statically advertised while temporarily unavailable on the current endpoint.

### Lifecycle, hardening, samples, and package acceptance

- Keeps live observations generation-scoped: invalidation/resume expires stale live evidence while valid static terminal-description evidence survives.
- Qualifies suspended/closed query ownership, caller cancellation, unavailable endpoints, concurrent side-effect-free inspection, repeated verification, and disposal/query shutdown behavior without adding another input reader or synchronization model.
- Preserves viable multi-backend semantics: negative evidence for one backend does not erase a separate advertised/verified alternate capable of satisfying the same semantic operation.
- Adds `Icod.Terminal.CapabilityPlanning.Sample`, demonstrating inspect-first planning and optional explicit verification without branching on terminal brand, `TERM`, protocol family, backend identity, or `Icod.TermInfo` provenance.
- Adds a fresh NuGet-only capability-planning consumer which references only `Icod.Terminal`; NuGet resolves `Icod.TermInfo` and `Icod.Timing` transitively on `net8.0`, `net9.0`, and `net10.0`.
- Simplifies prerelease package verification so development packages prove artifact shape, XML docs, restore, and executable package consumption without being forced through final-release documentation ceremony; stable releases retain the stricter closure checks.

### Compatibility

- Finalizes the 1.10 public API fingerprint as `ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb` while retaining every historical baseline unchanged.
- Retains `net8.0`, `net9.0`, and `net10.0`, Windows/Linux/macOS runtime validation, existing `Icod.DCurses` acceptance/soak witnesses, and the stable `1.0.0` compatibility floor.
- Adds no public protocol-backend selector, raw evidence ledger, arbitrary terminfo-capability-name API, terminal-brand heuristic, or requirement that every semantic capability have a live probe.

See `docs/releases/1.10.0.md`, `docs/Capability-Inspection-and-Planning.md`, `docs/Public-API-Baseline-1.10.md`, `docs/C101-Semantic-Capability-Vocabulary-and-API-Regret-Gate.md`, `docs/C105-C108-Capability-Lifecycle-Samples-Hardening-and-API-Freeze.md`, and `Icod.Terminal-1.10.0-Development-Roadmap.md` for the complete 1.10 contract.

## 1.9.0

### Unsolicited semantic events

- Adds `TerminalEventKind.Semantic = 4` as an additive event kind while preserving the existing `Input = 0`, `Lifecycle = 1`, `Timeout = 2`, and `Cancelled = 3` numeric values.
- Adds protocol-neutral `TerminalSemanticEvent` and typed `TerminalNotificationEvent` payloads for activation, one-based button activation, close, and close-tracking-unavailable observations.
- Delivers semantic events through the existing `TerminalSession.ReadEventAsync(...)` path; no second raw/semantic reader, callback stream, or arbitrary vendor-event dictionary is introduced.
- Freezes authoritative framed-input ownership as active query response -> recognized unsolicited semantic event -> ordinary application input.
- Keeps query responses and semantic events mutually exclusive: one frame is never double-delivered, and an OSC 99 notification report cannot satisfy an unrelated support/alive query merely because both share OSC 99.

### Interactive Kitty OSC 99 notifications

- Adds `KittyNotificationOptions.ReportActivation`, `ReportClose`, and `Buttons` as explicit opt-in interactive controls.
- Requires a caller-supplied notification identifier when interaction reporting is requested so application correlation does not depend on an internal multipart identifier.
- Encodes button labels using strict UTF-8 and the Kitty-defined U+2028 separator, bounded to 16 buttons, 512 UTF-8 bytes per label, and 2,048 UTF-8 bytes for the combined payload including separators.
- Preserves existing noninteractive Kitty notification bytes/behavior when the new options are unused; `FocusOnActivation` remains independent of report generation.
- Treats notification identifiers, activation reports, button numbers, close events, and `untracked` results as validated but unauthenticated terminal-controlled input rather than trusted desktop/user actions.

### Hardening, package, and compatibility

- Routes ordinary input and semantic events through the same bounded application-event coordinator so same-byte-stream ordering and backpressure remain deterministic without an unbounded semantic side queue.
- Hardens every meaningful seven/eight-bit split point, malformed metadata/payloads, CAN/SUB cancellation, mixed termination, concatenation, repeated identifiers, oversize drain/recovery, repeated wait cancellation/timeouts, queue pressure, interleaving, and repeated lifecycle cycles.
- Ensures malformed/oversized owned semantic reports are consumed/recovered boundedly rather than leaked into ordinary text or allowed to poison the coordinator.
- Restarts routing at active-query precedence after semantic recovery so an immediately following correlated response cannot be skipped.
- Retains notification requests/events as observations rather than reversible state: no hidden notification database, resume replay, synthetic interaction events, or automatic close-on-dispose is introduced.
- Extends the existing notification sample with `--kitty-interactive` and extends fresh NuGet-only package/XML validation to the 1.9 semantic-event and interactive-notification surface on `net8.0`, `net9.0`, and `net10.0`.
- Intentionally advances the final public API fingerprint to `e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315` while retaining every historical baseline unchanged.
- Retains the stable `1.0.0` compatibility floor, Windows/Linux/macOS runtime validation, current `Icod.DCurses` acceptance/soak witnesses, and the `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.

See `docs/releases/1.9.0.md`, `docs/Public-API-Baseline-1.9.md`, `docs/E190-Unsolicited-Semantic-Event-Contract-and-Reference-Freeze.md` through `docs/E199-1.9.0-Public-API-Documentation-Compatibility-and-Release-Closure.md`, and `Icod.Terminal-1.9.0-Development-Roadmap.md` for the complete 1.9 contract.

## 1.8.1

### Documentation and sample polish

- Corrects stale post-release wording which still described `1.8.0` as awaiting A189 PR/release qualification after the release had already been merged, tagged, and published.
- Reworks the root README and current roadmap into concise consumer/contributor entry points while preserving detailed historical design evidence in the versioned roadmaps and tranche documents.
- Reorganizes the sample catalog around consumer goals and adds an at-a-glance guide for choosing focused examples.
- Adds `Icod.Terminal.RasterGraphics.Sample`, a dependency-free backend-neutral raster example which generates RGB24 pixels in memory and uses only `TerminalRasterImage` plus `DisplayRasterAsync(...)`.
- Adds `Icod.Terminal.VsCodeShellIntegration.Sample`, demonstrating the typed VS Code OSC 633 shell-integration surface without process/environment discovery.
- Adds focused validation scripts which restore and build both new samples on `net8.0`, `net9.0`, and `net10.0` as part of the runtime validation graph.

### Compatibility

- Adds no public API and intentionally retains the 1.7/1.8 public API fingerprint `847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700`.
- Makes no intentional change to terminal-runtime wire behavior, capability evidence, query routing, raster routing, lifecycle/restoration semantics, or committed-output semantics.
- Retains the stable `1.0.0` compatibility floor, `net8.0`/`net9.0`/`net10.0`, and the `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.

See `docs/releases/1.8.1.md` for the curated maintenance release notes.

## 1.8.0

### APC foundation and Kitty Graphics

- Adds the internal canonical seven-bit `ApcWriter` for bounded application-defined control strings while keeping APC family framing separate from Kitty Graphics dialect semantics.
- Adds strict typed Kitty Graphics control-data/response handling, direct transfer (`t=d`), raw RGB24/RGBA32 transmission, and deterministic Indexed8 expansion which preserves referenced alpha through RGBA32 when required.
- Adds deterministic lazy Base64 segmentation with at most 4,096 encoded image-data bytes per Kitty Graphics chunk; large transfers do not require one complete encoded image allocation.
- Adds a committed multi-frame APC graphics transaction through the existing session output gate. Caller cancellation remains effective before commitment but does not intentionally truncate an already-committed logical transfer.
- Surfaces post-commit transport failure without replay, speculative recovery, or automatic switch to Sixel.
- Adds the protocol-defined correlated Kitty Graphics support query plus Primary DA synchronization barrier through the existing authoritative input/query path; no competing graphics reader is introduced.
- Adds deterministic evidence-driven raster routing which prefers verified Kitty Graphics and retains verified Sixel as fallback.
- Hardens seven/eight-bit APC response fragmentation, CAN/SUB aborts, malformed/missing terminators, oversized correlated responses, unrelated control traffic, late responses, subsequent-query integrity, and generation-scoped evidence expiration.
- Treats a complete matching `i=<probe-id>` response prefix as bounded transaction ownership rather than trust: later malformed/oversized data remains owned and strictly validated instead of leaking into ordinary application input.

### Stable raster contract

- Retains the public 1.7 `TerminalRasterImage`, `TerminalRasterColor`, `TerminalRasterPixelFormat`, and `TerminalSession.DisplayRasterAsync(...)` API unchanged.
- Preserves fractional alpha through Kitty RGBA32 while keeping Sixel's controlled unsupported behavior when fractional alpha cannot be represented truthfully.
- Keeps source raster dimensions intrinsic and deliberately adds no public placement/scaling, persistent image/placement identity, source rectangle, z-order, Unicode placeholder, deletion, animation, or cursor-normalization contract.
- Retains the established raster ceilings of 16,384 per dimension, 16 Mi pixels, 64 MiB owned pixel storage, and 256 indexed palette entries.
- Retains verified Sixel behavior/bytes as the fallback backend rather than replacing or weakening the 1.7 implementation.
- Continues to exclude generic public raw DCS/Sixel and APC/Kitty Graphics dispatch, explicit backend selection, image-file decoding/transcoding, hidden file/temp-file/shared-memory graphics transport.

### Compatibility and validation

- Adds no public API and intentionally retains the 1.7 public API fingerprint `847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700`; no redundant 1.8 baseline is created.
- Retains the stable `1.0.0` compatibility floor and all released 1.0–1.7 public signatures plus documented wire/ownership/query/resource/lifecycle/restoration/security semantics.
- Retains `net8.0`, `net9.0`, and `net10.0` plus the `Icod.TermInfo 1.10.0` / `Icod.Timing 1.0.0` dependency floor.
- Extends the fresh NuGet-only raster package smoke so both DCS/Sixel-specific and APC/Kitty-specific public escape-hatch method names remain excluded from the shipped API while the same public raster contract compiles/runs on all three TFMs.
- A180–A189 completed exact-head Staging qualification before PR #46 was merged; `v1.8.0` was subsequently tagged and published as the stable multi-backend raster release.

See `docs/releases/1.8.0.md`, `docs/A180-APC-Construction-Contract-and-Reference-Freeze.md` through `docs/A189-1.8.0-Package-Documentation-Compatibility-and-Release-Closure.md`, and `Icod.Terminal-1.8.0-Development-Roadmap.md` for the complete 1.8 contract.

### Prior release history

Earlier changelog entries remain unchanged below this point in repository history.
