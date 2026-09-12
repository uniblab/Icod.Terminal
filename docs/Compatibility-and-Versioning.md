# Compatibility and Versioning

This document defines the permanent compatibility and versioning policy for the `Icod.Terminal` 1.x line.

The public API fingerprint, permanent semantic documentation, package contracts, and downstream acceptance tests together define the supported 1.x contract. Compatibility is not limited to source compilation: documented ownership, restoration, cancellation, query routing, resource bounds, capability evidence, semantic event routing, committed-output behavior, and security guarantees are compatibility commitments too.

## 1. Versioning model

`Icod.Terminal` uses semantic versioning.

For stable 1.x releases:

- a **patch** release fixes defects, strengthens tests/documentation, improves performance, or hardens implementation without intentionally breaking the documented 1.x contract;
- a **minor** release may add compatible public APIs, semantic protocol support, or optional behavior while preserving existing public signatures and documented guarantees;
- a **major** release is required for ordinary intentional source/binary breaks, removal or incompatible reinterpretation of public members, enum renumbering, or incompatible changes to documented ownership/security/restoration semantics.

A bug fix may change behavior when the previous behavior violated an already-documented contract, but compatibility-sensitive corrections must still be documented.

## 2. Public API baselines

The stable `1.0.0` exported surface remains frozen by:

- `docs/Public-API-Baseline-1.0.md`;
- `docs/Public-API-Baseline-1.0.sha256`.

Compatible minor-release additions receive separate reviewed baselines rather than overwriting earlier evidence:

- `1.1` — additive OSC 633 surface;
- `1.2` — additive OSC 777 titled-notification surface;
- `1.3` — additive typed iTerm2 OSC 1337 surface;
- `1.4` — additive typed Kitty OSC 99 notification/query surface;
- `1.7` — additive backend-neutral raster-display surface;
- `1.9` — additive protocol-neutral semantic-event envelope and interactive Kitty notification options;
- `1.10` — additive protocol-neutral semantic capability inspection/planning surface;
- `1.11` — additive persistent-raster capability and opaque resource/placement ownership surface.

Versions `1.5.0` and `1.6.0` intentionally added no public API and retained the 1.4 fingerprint:

```text
3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
```

Version `1.7.0` advanced the fingerprint to:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Versions `1.8.0` and `1.8.1` intentionally added no public API and retained that fingerprint.

Version `1.9.0` advanced the fingerprint to:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

Version `1.10.0` advanced the fingerprint to:

```text
ee705250d19d51df92645e5020f188646dd2dbf38483278e6e57ce6fbbc1e9fb
```

Version `1.11.0` intentionally advances the authoritative current public API fingerprint to:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

The authoritative current baseline is:

- `docs/Public-API-Baseline-1.11.md`;
- `docs/Public-API-Baseline-1.11.sha256`.

Historical baselines remain checked in unchanged as compatibility evidence.

`packaging/VerifyPublicApiBaseline.ps1` regenerates the reflection snapshot independently for `net8.0`, `net9.0`, and `net10.0`, proves that all three exported surfaces agree, and verifies the authoritative current fingerprint. The fingerprint is a review gate, not a promise that 1.x can never grow; intentional compatible additions require an explicit new baseline in the same reviewed minor release.

## 3. Source and binary compatibility

Within stable 1.x, ordinary releases preserve existing public type/member names and signatures.

Compatibility-sensitive changes include:

- removing or renaming a public type/member;
- changing parameter order or types;
- changing return types incompatibly;
- making optional parameters required;
- tightening nullability in a way that rejects previously valid calls;
- changing implemented public interfaces incompatibly;
- changing public enum numeric values;
- changing public constant values callers may have compiled into assemblies;
- changing an established semantic operation from supported behavior to unconditional failure without an exceptional compatibility reason.

Compatible overloads, new types, and new semantic operations may be introduced in a minor release when they do not make existing behavior ambiguous or unsafe.

## 4. Stable enum numerics

Existing public enum numeric values are stable throughout 1.x. Existing values must not be renumbered or reused for another meaning.

Adding an enum value is compatibility-sensitive even when binary-compatible. It requires a minor release, explicit review, a baseline update, and documentation for callers with exhaustive switches.

Known frozen values include:

```text
TerminalEventKind
    Input      = 0
    Lifecycle  = 1
    Timeout    = 2
    Cancelled  = 3
    Semantic   = 4

TerminalRasterPixelFormat
    Rgb24      = 0
    Rgba32     = 1
    Indexed8   = 2

TerminalCapability
    ClipboardRead              = 0
    ClipboardWrite             = 1
    CursorStyle                = 2
    SynchronizedOutput         = 3
    KeyboardReporting          = 4
    MouseReporting             = 5
    FocusReporting             = 6
    BracketedPaste             = 7
    RasterGraphics             = 8
    PersistentRasterGraphics   = 9

TerminalCapabilitySupport
    Unknown      = 0
    Unsupported  = 1
    Advertised   = 2
    Verified     = 3

TerminalCapabilityEndpointAvailability
    Unavailable  = 0
    Available    = 1

TerminalCapabilityEvidenceKind
    None               = 0
    StaticDescription  = 1
    LiveObservation    = 2
```

The 1.10 capability-planning numerics remain stable, and 1.11 appends `PersistentRasterGraphics = 9` without renumbering values `0..8`.

## 5. Behavioral compatibility

Permanent documents under `docs/` define behavioral guarantees versioned alongside the API.

Stable guarantees include:

- one authoritative live-session input reader;
- active query response ownership preceding unsolicited semantic-event recognition, which precedes ordinary input decoding;
- no double delivery of one frame as both query response and semantic event;
- bounded semantic-event buffering in the same application-event ordering domain as ordinary input;
- bounded malformed/oversized owned-frame recovery;
- query correlation and bounded late-response ownership;
- pre-commit versus post-commit cancellation semantics;
- truthful `Unavailable`, `Unsupported`, `Unknown`, and failure distinctions;
- exact restoration only where explicitly promised;
- session/lease ownership and disposal authority;
- bounded parser/query/raster work;
- output serialization boundaries;
- static description evidence distinct from generation-scoped live observation;
- lifecycle invalidation expiring generation-scoped live evidence;
- terminal/vendor identity and caller preference not being capability proof;
- query timeout not automatically becoming unsupported truth;
- committed graphics output not intentionally truncated by ordinary caller cancellation;
- partial committed graphics failure surfaced without automatic replay/backend switching;
- persistent raster identities scoped to the lifecycle generation that established them;
- no automatic persistent-raster replay/re-upload after lifecycle uncertainty;
- child placement cleanup preceding resource-data cleanup while identities are current;
- stale persistent handles never emitting stale terminal identifiers during disposal;
- teardown draining committed output before output-state restoration;
- correlated terminal responses remaining untrusted and bounded after ownership is established.

Minor/patch releases may strengthen correctness while preserving these guarantees, but must not silently weaken or reverse them.

## 6. Raster compatibility contract

The public raster model is backend-neutral. It represents bounded raw image data plus semantic graphics intent, not “a Sixel image” or “a Kitty image.”

Version 1.7 added:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

The raster object owns a snapshot of caller-provided pixel/palette storage. RGB24 is opaque; RGBA32 and indexed palette colors preserve straight alpha.

Version 1.8 added Kitty Graphics below the unchanged ephemeral raster API while retaining Sixel fallback. The public contract does not expose a raw DCS/APC writer, backend selector, Kitty numeric image id, Kitty image number, Kitty placement id, arbitrary control-data dictionary, or scene graph.

Version 1.11 adds a separate persistent ownership domain:

```text
TerminalCapability.PersistentRasterGraphics
TerminalRasterResource
TerminalRasterPlacement
TerminalRasterPlacementOptions
TerminalSession.CreateRasterResourceAsync(...)
TerminalRasterResource.CreatePlacementAsync(...)
TerminalRasterPlacement.UpdateAsync(...)
```

Persistent resources and placements are opaque session-owned handles. Their backend protocol identities remain private. Placement creation/update uses current-cursor positioning and optional `Columns`/`Rows`; callers continue to use ordinary terminal operations for cursor movement rather than receiving a scene-coordinate API.

The existing ephemeral `DisplayRasterAsync(...)` contract remains compatible and may still resolve through verified Kitty Graphics or Sixel. Persistent ownership is a distinct capability and is not emulated through Sixel.

Fractional alpha remains valid common raster data. Kitty RGBA32 preserves it; Sixel returns controlled unsupported when equivalent semantics cannot be represented truthfully rather than silently compositing.

## 7. Sixel and Kitty Graphics protocol compatibility

Sixel and Kitty Graphics remain internal backends beneath public semantic graphics contracts.

Stable Sixel behavior includes canonical seven-bit DCS framing, deterministic bounded quantization, bounded lazy payload generation, caller cancellation before commitment but not intentional frame truncation after commitment, serialization through final ST/flush, and no automatic retry after partial transport failure.

Stable Kitty Graphics behavior includes canonical seven-bit APC framing, direct transfer (`t=d`), RGB24/RGBA32 raw transmission, deterministic Indexed8 expansion, Base64 image data bounded to 4096 bytes per protocol chunk, one logical multi-frame serialized transaction, no ordinary post-commit cancellation truncation, no automatic replay/Sixel switch after partial committed failure, and bounded correlated support-query ownership.

Version 1.11 additionally reviews the narrow persistent Kitty subset required to upload acknowledged terminal-resident image data, create/update placements, and delete placements/resources. Those commands remain internal implementation detail behind opaque public ownership objects.

File/temp-file/shared-memory Kitty transports, source rectangles, z-order, Unicode placeholders, relative placements, pixel-coordinate placement, animation, and scene-graph policy remain outside the 1.11 compatibility promise unless separately reviewed in a later release.

## 8. Capability evidence and uncertainty

Protocol support and NuGet package compatibility are separate concerns.

Successful semantic output proves emission, not terminal recognition or visual application unless a protocol supplies explicit acknowledged evidence.

Static terminal description/profile advertisement and generation-scoped live observation are distinct. Terminal name, `TERM`, host OS, emulator brand, registry order, and caller preference are not capability proof.

For Sixel, Primary DA attribute `4` may provide positive live support evidence; a valid DA response without `4`, timeout, or caller cancellation does not automatically prove terminal-wide unsupported behavior.

For Kitty Graphics, a valid correlated probe response provides positive evidence; the reviewed Primary DA barrier arriving first provides negative evidence for that concrete probe; timeout before either authoritative result remains unknown; caller cancellation is not negative evidence.

Live evidence expires under the existing session-generation invalidation contract.

## 9. Version 1.10 capability-planning compatibility

Version 1.10 is an additive minor release. It does not reinterpret existing raster/query/input contracts.

The public additions are:

```text
TerminalCapability
TerminalCapabilitySupport
TerminalCapabilityEndpointAvailability
TerminalCapabilityEvidenceKind
TerminalCapabilityStatus
TerminalSession.InspectCapability(...)
TerminalSession.VerifyCapabilityAsync(...)
```

`InspectCapability(...)` is synchronous and side-effect free. It reads current in-memory semantic knowledge only and emits no terminal traffic.

`VerifyCapabilityAsync(...)` is explicit and bounded. It may strengthen knowledge only through existing reviewed probe paths. Version 1.10 introduced live verification for `KeyboardReporting` and `RasterGraphics`; capabilities without a reviewed probe remain inspection-only rather than receiving invented traffic.

Support knowledge and endpoint availability are separate compatibility dimensions. A statically advertised capability may remain `Advertised` while the required endpoint is `Unavailable`; this makes `IsUsable` false without rewriting truthful support knowledge to `Unsupported`.

Public evidence intentionally projects to only:

```text
None
StaticDescription
LiveObservation
```

The public contract does not expose `Icod.TermInfo`, raw OSC/CSI/DCS/APC identities, Kitty/Sixel backend ids, routing scores, or terminal-brand heuristics.

Generation-scoped live observations expire on lifecycle invalidation/resume while valid static description evidence remains. Already-returned `TerminalCapabilityStatus` values are immutable snapshots; callers inspect again for current knowledge.

The permanent contract authority is `docs/Capability-Inspection-and-Planning.md`.

### Version 1.11 persistent-raster capability

Version 1.11 appends `PersistentRasterGraphics = 9` to that semantic capability vocabulary.

`RasterGraphics` and `PersistentRasterGraphics` are intentionally distinct. Verified Sixel can satisfy ordinary raster display but does not imply terminal-resident persistent resource ownership. The persistent capability is verified only through the reviewed Kitty Graphics path and uses the same side-effect-free inspection / explicit bounded verification model introduced in 1.10.

`CreateRasterResourceAsync(...)` does not hide a new background probe. It requires current verified persistent-raster capability and a usable endpoint before committing upload traffic.

## 10. Correlation and response ownership

The one-reader/query ownership model is a stable 1.x behavioral contract.

A correlated response is transaction-owned but remains untrusted. Matching identifiers do not bypass grammar, size, termination, or overflow checks. Identified malformed/oversized traffic is recovered boundedly rather than leaked back into ordinary application input.

Version 1.9 extends the same ownership principle to unsolicited semantic reports. Active query ownership remains first; semantic ownership is second; ordinary input decoding follows. After bounded semantic recovery, routing restarts at query precedence.

Version 1.10 verification reuses this established query/ownership machinery and does not introduce a second reader or generic raw probe API.

Version 1.11 resource creation and acknowledged placement mutation reuse the same authoritative query ownership. Correlation includes the private image number/image id and, where relevant, the private placement id. A well-formed terminal `ENOENT` for a resource/placement believed current invalidates that local terminal-resident certainty and is surfaced as controlled `Unavailable`; other well-formed negative replies remain controlled failures rather than trusted statements about unrelated state.

## 11. Resource-bound compatibility

Documented resource ceilings are part of the safety contract. Implementations may become more efficient, but minor/patch releases must not silently remove bounds and introduce unbounded work or retention.

Raster ceilings include:

```text
maximum dimension        16,384
maximum pixel count      16 Mi
maximum owned pixel data 64 MiB
maximum indexed palette  256 entries
```

Persistent-raster ownership adds these session-local ceilings:

```text
maximum live persistent resources   256
maximum live persistent placements  4096
placement Columns / Rows             1..16384 when supplied
```

The persistent registries are local bookkeeping limits, not claims about terminal storage quota. Exhaustion returns controlled `Unavailable` before protocol output rather than creating unbounded local state.

Other stable bounds include the 4096-byte normal response frame, 4096-byte Kitty Base64 image-data chunk, bounded control-family frames, fixed Sixel histogram, bounded resynchronization state, and bounded semantic-event/application-event buffering.

Interactive Kitty notification buttons remain bounded to 16 labels, 512 UTF-8 bytes per label, and 2,048 UTF-8 bytes for the combined button payload including separators.

Increasing a ceiling may be compatible when semantics remain unchanged; decreasing a ceiling so previously supported values are rejected requires explicit compatibility review.

## 12. Target frameworks and operating systems

The stable 1.x package targets:

```text
net8.0
net9.0
net10.0
```

All three are first-class package targets. Dropping one is compatibility-sensitive and requires an explicit maintenance/security/toolchain justification and release documentation.

The built-in `SystemTerminalControlProvider` provides native behavior for Windows, Linux, and macOS. Other operating systems receive controlled unsupported results from the built-in provider rather than fabricated POSIX/Windows behavior.

Custom hosts remain possible through `ITerminalControlProvider`, `ITerminalInput`, and `ITerminalOutput`.

## 13. Architecture compatibility

Permanent layer boundaries remain part of the support model:

- `Icod.TermInfo` owns immutable capability information;
- `Icod.Terminal` owns the live terminal conversation, query/evidence model, capability planning, unsolicited semantic-event routing, semantic output, ephemeral raster routing, persistent raster resource/placement ownership, and reversible session mechanics;
- `Icod.DCurses` owns higher-level virtual-screen/curses presentation policy;
- PTY/process hosting remains orthogonal.

Persistent Kitty Graphics does not move virtual-screen/scene ownership into `Icod.Terminal`. Opaque resources/placements are terminal-resident ownership handles, not cells, windows, layers, or a scene graph. Unsolicited semantic events do not make the library a generic vendor-event bus. Capability planning does not make the internal backend registry, evidence ledger, or `Icod.TermInfo` provenance part of the public contract.

## 14. Security compatibility

Security boundaries are compatibility commitments.

Stable 1.x does not quietly introduce through a minor/patch release:

- generic raw OSC/CSI/DCS/APC/vendor dispatch as the ordinary API;
- hazardous host-affecting OSC 9 commands;
- generic raw OSC 633/777/1337/99 dispatch replacing reviewed semantic surfaces;
- arbitrary public Sixel/Kitty writers merely because internal grammars exist;
- public Kitty numeric image ids/image numbers/placement ids as semantic graphics identity;
- a generic raw unsolicited-event stream or arbitrary vendor-event dictionary;
- terminal-brand-triggered activation presented as capability truth;
- a competing protocol-specific input reader;
- automatic clipboard reads;
- hidden shell/environment metadata capture;
- authentication claims for terminal-supplied notification interaction reports;
- automatic image-file decoding or network/process side effects in raster display;
- hidden file/temp-file/shared-memory graphics transport;
- hidden persistent-raster source-image caching or automatic replay after lifecycle uncertainty;
- silent compositing of unsupported fractional-alpha raster data;
- cancellation-driven truncation of already-committed graphics transfers;
- automatic retry/backend switch after partial committed graphics output;
- hidden/background capability verification behind ordinary inspection;
- public capability evidence that exposes backend/dependency provenance as trusted identity.

New security-sensitive semantic features require explicit typed API, bounded validation, documentation, and tests.

## 15. Dependency compatibility policy

`Icod.Terminal.csproj` is the package authority for direct NuGet dependency requirements.

Active tests, samples, package smoke consumers, and auxiliary verification tools do not independently pin exact `Icod.TermInfo`, `Icod.Timing`, or other transitive runtime dependency versions merely to duplicate package metadata. Successful restore/build against the declared package graph is the dependency-compatibility witness unless a concrete incompatibility is under investigation.

Package verification may assert dependency identity and package shape without turning one resolved transitive version into a second behavioral contract.

Historical release/tranche documents may retain exact dependency versions as evidence of what was shipped at that time.

## 16. Direct consumers and Icod.DCurses

Direct consumers should use `TerminalSession` when they need live terminal/session mechanics without a curses virtual-screen model.

Applications needing windows/cells/diff/refresh should normally use `Icod.DCurses` and allow that layer to own the supplied session according to its integration contract.

Persistent raster resources/placements are appropriate building blocks for higher-level consumers, but layout, damage tracking, clipping policy, and virtual-screen/scene decisions remain higher-level responsibilities.

Do not create independent state-owning sessions over the same physical terminal merely to divide responsibilities.

## 17. Deprecation policy

When an existing 1.x API can be replaced compatibly, deprecation is preferred before removal.

Ordinary removal should identify a replacement, document migration, mark the old surface obsolete where practical, preserve it through a reasonable migration interval, and remove it only in a major release.

Exceptional removal without a normal deprecation period is reserved for cases such as active security vulnerability or an impossible-to-support contract and still requires explicit release documentation.

## 18. Compatibility evidence

A release is not considered compatible merely because unit tests pass.

The repository maintains layered evidence including:

- retained historical public API fingerprints plus the authoritative current 1.11 fingerprint;
- Windows/Linux/macOS runtime/source validation;
- exact multi-TFM API snapshot agreement;
- fresh NuGet-only consumers for newly added or compatibility-critical semantic APIs;
- generated XML documentation verification;
- retained historical package consumers/contracts;
- current `Icod.DCurses` package-boundary integration/ownership tests;
- repeated ownership/disposal/lifecycle hardening;
- exact protocol regression vectors;
- resource-bound tests;
- release/distribution validation on configured architectures.

Version 1.9 qualified semantic-event ownership and interactive Kitty notification reporting. Version 1.10 qualified side-effect-free semantic capability inspection, explicit bounded verification, lifecycle invalidation, concurrency/cancellation, package-only consumption, loose dependency coupling, and downstream compatibility. Version 1.11 qualifies persistent-raster acknowledgement/correlation, bounded resource/placement ownership, placement replacement, deterministic disposal, lifecycle invalidation/no-replay behavior, adversarial terminal replies, package-only consumption/XML documentation, a protocol-neutral sample, and current DCurses acceptance.

Exact release qualification evidence belongs to the relevant pull-request workflow, merged `main` workflow, release notes, and GitHub Release rather than being hard-coded permanently into this policy document.

## 19. Release rule

A green feature checkpoint is not publication authorization.

For every stable release:

1. one unchanged final pull-request head must pass the complete Staging qualification matrix;
2. only that qualified exact head may be considered ready for merge;
3. merge remains an explicit maintainer action;
4. the resulting `main` head must pass Release distribution validation;
5. `v<semver>` tagging/publication remains a separate explicit maintainer action and must use the curated `docs/releases/<version>.md` notes.

These gates may evolve operationally, but equivalent compatibility evidence must exist before historical checks are removed.
