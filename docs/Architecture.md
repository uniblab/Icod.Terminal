# Icod.Terminal Architecture

This document is a permanent 1.x architecture authority for `Icod.Terminal`. Historical roadmaps and tranche records explain how the design evolved; this document describes the supported architecture consumers should rely on.

## 1. Role in the Icod terminal stack

```text
Applications
  commands / monitors / editors / pagers / REPLs
                         |
                +--------+--------+
                |                 |
          Icod.DCurses       direct consumers
                |                 |
                +--------+--------+
                         |
                   Icod.Terminal
                         |
                   Icod.TermInfo
                         |
          tty / console / terminal transports

future Icod.Pty is adjacent: it may create child-process PTYs/ConPTYs,
but it is not part of the Icod.Terminal runtime dependency chain.
```

### `Icod.TermInfo`

`Icod.TermInfo` owns immutable terminal capability data, terminfo interpretation/expansion, and description resolution. `Icod.Terminal` consumes that information; it does not maintain a competing capability database.

### `Icod.Terminal`

`Icod.Terminal` owns one live terminal conversation and the mechanics required to use it safely:

- endpoint observation and native platform identity;
- terminal-mode capture and semantic input policy;
- exact restoration of captured host state where promised;
- live dimensions and lifecycle observation;
- one authoritative input-reader/decoder path;
- active query/response correlation;
- unsolicited protocol-neutral semantic event routing;
- semantic capability evidence, side-effect-free inspection, and bounded explicit verification;
- bounded incremental control-language parsing;
- semantic terminal-output operations;
- reversible presentation, rich-input, and color ownership;
- backend-neutral ephemeral raster output;
- persistent terminal-resident raster resource and placement ownership;
- evidence-driven backend selection behind semantic operations;
- serialization of session-managed terminal output;
- lifecycle-aware invalidation, re-entry, and deterministic cleanup.

### `Icod.DCurses`

`Icod.DCurses` owns two-dimensional presentation policy: cells, styles, windows, pads, virtual-screen state, Unicode display width, clipping, wrapping, scrolling, damage tracking, desired-vs-physical screen comparison, refresh strategy, and higher-level scene/layout policy.

It may consume semantic capability planning, ephemeral raster display, or persistent raster resources from `Icod.Terminal`; it should not reimplement terminal modes, query routing, semantic-event routing, Sixel/Kitty framing, capability evidence, persistent protocol identity, or lifecycle restoration.

### Future `Icod.Pty`

Pseudo-terminal creation, child-process hosting, ConPTY/PTY plumbing, and process ownership remain outside the `Icod.Terminal` 1.x contract.

## 2. Three abstraction levels

### 2.1 Ordinary semantic session API

This is the preferred level for applications.

`TerminalSession` exposes semantic operations for:

- reading normalized terminal events;
- inspecting current semantic capability knowledge without terminal I/O;
- explicitly requesting bounded verification where a reviewed live probe exists;
- querying live terminal state through typed query methods;
- writing application text and reviewed semantic terminal metadata/control operations;
- acquiring reversible presentation/input/color state;
- displaying backend-neutral raw raster images ephemerally;
- creating generation-scoped persistent raster resources and placements where explicitly verified.

These APIs participate in session ordering, validation, resource bounds, capability evidence, lifecycle, and cleanup semantics.

### 2.2 Advanced transport/provider API

`ITerminalInput`, `ITerminalOutput`, `ITerminalControlProvider`, `TerminalEndpoint`, native mode snapshots, and controlled result types remain public for custom hosts, injected transports, diagnostics, and higher-level libraries operating below the semantic layer.

These contracts do not imply that a live session can be bypassed safely. In particular, a `TerminalSession` owns the authoritative input-reader path while active.

`TerminalSession.Output` is a borrowed advanced escape hatch. Direct use is outside session serialization; callers accept responsibility for avoiding interleaving with session-managed traffic.

### 2.3 Internal wire/platform machinery

Protocol encoders/parsers, control-family writers, Sixel quantization/encoding, Kitty Graphics adaptation/chunking/persistent ids, semantic-event recognition, query transactions, capability evidence storage, lifecycle signal sources, presentation/input managers, and OS plumbing remain implementation details unless represented separately by a public semantic contract.

Internal selectors and numeric protocol identities are not compatibility promises merely because public semantic APIs ultimately use them.

## 3. Capability-driven, not terminal-brand-driven

The normalized model separates:

```text
semantic operation
protocol backend
control family
support state
evidence source
endpoint availability
```

Static TermInfo/profile advertisement and generation-scoped live evidence are distinct. A terminal name, `TERM`, environment variable, host OS, emulator brand, registry order, or caller preference is not automatically capability proof.

For Sixel, Primary Device Attributes parameter `4` may provide positive support evidence. A valid response without `4`, silence, timeout, or caller cancellation does not automatically prove terminal-wide unsupported Sixel.

For Kitty Graphics, the reviewed support path uses the protocol-defined correlated query plus Primary DA barrier semantics. A valid correlated Kitty response verifies that backend for the current generation; Primary DA arriving first is reviewed negative evidence for that concrete probe; silence before either authoritative result remains unknown.

### Public capability planning

The public vocabulary is deliberately reduced:

```text
TerminalCapability
TerminalCapabilitySupport
TerminalCapabilityEndpointAvailability
TerminalCapabilityEvidenceKind
TerminalCapabilityStatus
```

`TerminalSession.InspectCapability(...)` is synchronous and side-effect free. It projects existing knowledge only.

`TerminalSession.VerifyCapabilityAsync(...)` is explicit because it may emit bounded probe traffic. It reuses reviewed existing probe paths rather than inventing traffic for every semantic capability.

Public evidence is only `None`, `StaticDescription`, or `LiveObservation`; backend ids, routing scores, raw protocol frames, and `Icod.TermInfo` provenance remain private.

Version 1.11 adds `PersistentRasterGraphics = 9`. It is separate from ordinary `RasterGraphics`: Sixel may satisfy ephemeral raster display, while persistent terminal-resident resource ownership requires the reviewed persistent-capable Kitty Graphics path.

## 4. Control-language layering

The permanent separation is:

```text
semantic intent
    -> capability/evidence resolution
        -> protocol backend selection
            -> control-family framing
                -> dialect codec / wire transport
```

The normalized framing vocabulary includes CSI, DCS, OSC, APC, PM, and SOS.

Raster examples make the separation concrete:

```text
RasterGraphics
    -> DcsSixel
        -> DCS
            -> Sixel

RasterGraphics
    -> ApcKittyGraphics
        -> APC
            -> Kitty Graphics

PersistentRasterGraphics
    -> ApcKittyGraphics
        -> APC
            -> private resource / placement protocol identity
```

The public raster/resource contracts remain semantic even though the current persistent implementation is Kitty-specific internally.

## 5. Backend-neutral raster data

`TerminalRasterImage`, `TerminalRasterPixelFormat`, and `TerminalRasterColor` represent bounded raw image data, not a Sixel/Kitty payload container.

Supported storage forms are:

```text
Rgb24
Rgba32
Indexed8 + RGBA8 palette
```

The raster object owns a snapshot of caller-provided pixel/palette storage. Straight alpha is preserved. Mutable backing buffers are not exposed publicly.

Raster ceilings are explicit:

```text
maximum dimension        16,384
maximum pixel count      16 Mi
maximum owned pixel data 64 MiB
maximum indexed palette  256 entries
```

Image-file decoding, gamma/color-profile processing, and hidden background compositing remain outside the core contract.

## 6. Ephemeral raster display

`TerminalSession.DisplayRasterAsync(...)` represents ephemeral display intent.

Its reviewed internal backends are:

```text
verified ApcKittyGraphics
verified DcsSixel
```

When both are verified, Kitty Graphics is preferred; verified Sixel remains fallback. Routing decisions happen before commitment.

Once a backend commits output, transport/protocol failure is surfaced. The library does not replay through another backend because the terminal may have applied an unknown prefix.

The Sixel pipeline is:

```text
TerminalRasterImage
    -> deterministic bounded quantization
        -> SixelPaletteImage
            -> bounded lazy payload segments
                -> serialized DCS transaction
```

The Kitty direct-transfer pipeline is:

```text
TerminalRasterImage
    -> checked raw adaptation
        -> RGB24 or RGBA32 byte stream
            -> bounded lazy Base64 chunks
                -> serialized APC frame sequence
```

Large images do not require one giant encoded string/allocation.

## 7. Persistent raster ownership — 1.11

Version 1.11 adds a separate terminal-resident ownership domain rather than widening `DisplayRasterAsync(...)` into a scene graph.

The semantic ownership graph is:

```text
TerminalSession
    -> TerminalRasterResource
        -> TerminalRasterPlacement
        -> TerminalRasterPlacement
        -> ...
```

`TerminalSession.CreateRasterResourceAsync(...)` publishes a public resource only after acknowledged upload establishes a private terminal identity.

`TerminalRasterResource.CreatePlacementAsync(...)` creates opaque child ownership. `TerminalRasterPlacement.UpdateAsync(...)` replaces the same private placement at the terminal's current cursor position.

`TerminalRasterPlacementOptions.Columns` and `.Rows` are independently optional and bounded to `1..16384`. The reviewed backend uses no-cursor-movement placement semantics.

### Private identity

Internally, resource upload uses a nonzero private image number for acknowledgement correlation and receives a nonzero terminal-assigned image id. Placement identity is likewise private and nonzero.

Public resources/placements never expose those ids. Callers cannot manufacture raw resource/placement protocol identity.

### Bounded registries

Session bookkeeping is bounded:

```text
256  live persistent resources
4096 live persistent placements
```

Allocation avoids live collisions and handles numeric wraparound. These are local ownership bounds, not terminal storage-quota promises.

### No hidden raster cache

After successful creation, the persistent registry retains ownership metadata, not an arbitrary hidden `TerminalRasterImage` copy. Version 1.11 therefore does not promise automatic re-upload/rebind.

See `Persistent-Raster-Ownership.md` for the full public ownership contract.

## 8. Alpha and image semantics

Kitty direct RGBA32 can preserve fractional alpha. Sixel cannot represent equivalent semantics without external compositing policy, so fractional-alpha display through Sixel is controlled unsupported rather than silently flattened.

Indexed input expands for Kitty only as required by its raw direct formats: opaque referenced palette colors permit RGB24; any referenced non-opaque color requires RGBA32.

Persistent resource creation reuses the same bounded raw adaptation semantics and does not add image decoding/transcoding.

## 9. Session-managed output ordering

High-level application text, semantic output, query requests, reversible state traffic, ephemeral raster output, and persistent raster transactions use session-owned serialization domains appropriate to their contracts.

Committed Sixel output holds the session output gate through final ST/flush. Committed Kitty direct output holds it across all APC frames through final flush.

Persistent resource upload is also one logical committed transfer. Placement create/update are serialized operations coordinated with acknowledgement through the existing query manager.

Caller cancellation is honored before commitment. After commitment, ordinary cancellation does not intentionally truncate the logical graphics transaction.

Session teardown drains committed output before final output-state restoration continues.

## 10. One authoritative input conversation

A live session owns one incremental byte stream containing ordinary text/keys/paste/mouse/focus data, lifecycle traffic, active query responses, unsolicited semantic reports, graphics probe responses, and persistent graphics acknowledgements.

The stable precedence is:

```text
active query/response ownership
    -> recognized unsolicited semantic-event ownership
        -> ordinary application-input decoding
```

No raster or persistent-resource feature creates a graphics-specific reader.

Resource upload and placement create/update acknowledgements are correlated through the same transaction/query authority. Wrong private identities do not satisfy another transaction.

## 11. Correlation grants ownership, not trust

Terminal responses remain untrusted after they become transaction-owned.

Matching identifiers do not bypass grammar, termination, size, duplicate-field, or numeric-overflow validation. Malformed/oversized owned responses are recovered boundedly rather than leaked into ordinary application input.

For persistent graphics, a well-formed correlated `ENOENT` means the terminal no longer recognizes an object the session believed current. That invalidates terminal-resident certainty; it is not permission for hidden replay.

## 12. Ownership and reversible state

A `TerminalSession` owns terminal state transitions, not necessarily the underlying descriptor/stream/transport.

Supplied transports remain borrowed. Disposal restores state the session changed but does not close caller-owned transports.

Reversible terminal features use leases when consumers may overlap. Exact restoration is based on observed/captured state rather than guessed defaults.

Ephemeral raster display is output, not reversible state.

Persistent raster resources are also **not exactly restorable state**. They are explicit generation-scoped terminal-resident ownership. While identity is current, the session can target cleanup. Once lifecycle uncertainty invalidates identity, stale handles perform local-only cleanup and no stale numeric identifiers are emitted.

The library does not retain/replay resources merely to simulate restoration.

## 13. Lifecycle as a trust boundary

Suspend/resume and explicit invalidation are state transitions.

Before suspension, reversible owned state is restored as required. After resume, generation-scoped live observations and persistent terminal-resident identity certainty expire; configured reversible state is re-established according to its own contract.

Already-returned capability statuses are immutable snapshots. Callers inspect/verify again when current knowledge matters.

Persistent handles do not revive automatically after generation invalidation. Applications create new resources explicitly if they still need them.

## 14. Deterministic persistent cleanup

Placement disposal releases local ownership once and, while current, attempts one targeted quiet delete. It is locally idempotent even when terminal cleanup transport fails.

Resource disposal prevents new children, closes child ownership first, attempts child cleanup before resource-data deletion, and aggregates multiple cleanup failures if necessary.

Session teardown deletes current placements before current resource data. If persistent state is already stale, teardown performs local bookkeeping only.

## 15. Failure and uncertainty

The architecture favors truthful uncertainty over optimistic advancement.

- query silence does not automatically become unsupported truth;
- unavailable endpoints are distinct from unsupported capability;
- correlated malformed responses are failures, not support evidence;
- partial committed graphics output is not replayed automatically;
- terminal `ENOENT` invalidates current resource certainty;
- cleanup failures are surfaced rather than hidden behind invented success;
- stale resource identity is not emitted after lifecycle invalidation.

## 16. Bounded work and storage

Documented bounds are part of the safety architecture:

```text
normal terminal response frame       4,096 bytes
small complete DCS frame              4,096 bytes
small complete APC frame              8,192 bytes
Kitty Base64 image data per APC chunk 4,096 bytes
persistent resources                    256/session
persistent placements                  4096/session
```

Parser, query, semantic-event, raster conversion, and ownership registries remain bounded.

## 17. Security-sensitive transport choices

Kitty direct transfer remains the reviewed raster transport. The library does not silently choose file, temporary-file, or shared-memory transfer because those introduce path naming, permissions, lifetime, visibility, race, and cross-process concerns.

Base64 is protocol framing, not encryption.

Persistent storage is owned by the terminal and may be evicted according to terminal policy; local registry bounds do not imply terminal storage reservation.

## 18. Stable architectural exclusions

Stable 1.x does not treat the following as ordinary `Icod.Terminal` responsibilities:

- process-global current-terminal state;
- competing live input readers;
- generic raw vendor control/event buses;
- terminal-brand-driven capability proof;
- image-file decoding/transcoding;
- hidden graphics replay;
- unbounded graphics/query/event state;
- PTY/ConPTY process hosting;
- cells, windows, layout, damage, or scene-graph ownership.

Advanced persistent placement features such as source rectangles, z-order, Unicode placeholders, relative/pixel placement, and animation require separate review and downstream justification.

## 19. Dependency boundary

`Icod.Terminal.csproj` is the direct NuGet dependency authority. Tests, samples, package consumers, and verification tools do not independently pin exact transitive runtime dependency versions merely to duplicate package metadata.

Successful restore/build of the declared package graph remains the normal dependency-compatibility witness.

## 20. Permanent authorities

Related 1.x authorities include:

- `Terminal-Session-and-Ownership.md`;
- `Lifecycle-and-Restoration.md`;
- `Input-and-Events.md`;
- `Queries-and-Responses.md`;
- `Presentation-and-Reversible-State.md`;
- `Capability-Inspection-and-Planning.md`;
- `Persistent-Raster-Ownership.md`;
- `Security-and-Privacy.md`;
- `Compatibility-and-Versioning.md`.

Historical tranche records remain design evidence; these permanent documents define the current supported architecture.
