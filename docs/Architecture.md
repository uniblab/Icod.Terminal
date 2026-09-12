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

`Icod.TermInfo` owns immutable terminal capability data and expansion. `Icod.Terminal` consumes that information; it does not maintain a competing description database.

`Icod.Terminal` owns one live terminal conversation and the mechanics required to use it safely:

- endpoint observation and native platform identity;
- terminal-mode capture and semantic input policy;
- lifecycle and exact-restoration ownership where promised;
- one authoritative input-reader/decoder path;
- active query/response correlation;
- unsolicited protocol-neutral semantic event routing;
- semantic capability evidence, side-effect-free inspection, and bounded explicit verification;
- bounded incremental control-language parsing;
- semantic terminal-output operations;
- reversible presentation/input/color ownership;
- backend-neutral ephemeral raster output;
- persistent terminal-resident raster resource and placement ownership;
- bounded source-pixel placement cropping and signed z-order;
- evidence-driven backend selection behind semantic operations;
- lifecycle-aware invalidation and deterministic cleanup.

`Icod.DCurses` owns two-dimensional presentation policy: cells, styles, windows, pads, virtual-screen state, Unicode display width, clipping, wrapping, scrolling, damage tracking, desired-vs-physical screen comparison, refresh strategy, and higher-level scene/layout policy.

Pseudo-terminal creation, child-process hosting, ConPTY/PTY plumbing, and process ownership remain outside the `Icod.Terminal` 1.x runtime dependency chain.

## 2. Abstraction levels

### 2.1 Semantic session API

Applications normally use `TerminalSession` for normalized input/events, typed queries, semantic output/state operations, capability planning, raster display, and persistent resource/placement ownership.

These APIs participate in session ordering, validation, resource bounds, capability evidence, lifecycle, and cleanup semantics.

### 2.2 Advanced transport/provider API

`ITerminalInput`, `ITerminalOutput`, `ITerminalControlProvider`, `TerminalEndpoint`, native mode snapshots, and controlled result types remain public for injected transports, diagnostics, and higher-level libraries.

A live session still owns the authoritative reader. `TerminalSession.Output` is a borrowed advanced escape hatch outside ordinary session serialization.

### 2.3 Internal wire/platform machinery

Protocol encoders/parsers, control-family writers, Sixel quantization/encoding, Kitty Graphics adaptation/chunking/persistent ids, semantic-event recognition, query transactions, capability evidence storage, lifecycle sources, and OS plumbing remain implementation details unless represented separately by a public semantic contract.

## 3. Capability-driven routing

The normalized model separates:

```text
semantic operation
protocol backend
control family
support state
evidence source
endpoint availability
```

Static terminal-description evidence and generation-scoped live evidence are distinct. Terminal names, `TERM`, environment variables, host OS, emulator brands, registry order, and caller preferences are not automatically capability proof.

`TerminalSession.InspectCapability(...)` is side-effect free. `VerifyCapabilityAsync(...)` is explicit because it may emit a bounded reviewed live probe.

`PersistentRasterGraphics = 9` is separate from ordinary `RasterGraphics`: verified Sixel may satisfy ephemeral raster display while persistent terminal-resident ownership requires the reviewed persistent-capable Kitty Graphics path.

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

Raster routing illustrates the boundary:

```text
RasterGraphics
    -> DcsSixel -> DCS -> Sixel

RasterGraphics
    -> ApcKittyGraphics -> APC -> Kitty Graphics

PersistentRasterGraphics
    -> ApcKittyGraphics -> APC
        -> private resource / placement protocol identity
```

The public raster/resource contracts remain semantic even though the current persistent implementation is Kitty-specific internally.

## 5. Backend-neutral raster data

`TerminalRasterImage`, `TerminalRasterPixelFormat`, and `TerminalRasterColor` represent bounded raw image data rather than protocol payload containers.

Supported storage forms are `Rgb24`, `Rgba32`, and `Indexed8` plus RGBA8 palette.

Raster ceilings remain:

```text
maximum dimension        16,384
maximum pixel count      16 Mi
maximum owned pixel data 64 MiB
maximum indexed palette  256 entries
```

Image-file decoding, gamma/color-profile processing, and hidden background compositing remain outside the core contract.

## 6. Ephemeral raster display

`TerminalSession.DisplayRasterAsync(...)` represents ephemeral display intent. Reviewed backends are verified Kitty Graphics and verified Sixel.

Routing happens before commitment. Once a backend commits output, transport/protocol failure is surfaced and the library does not replay through another backend because the terminal may have applied an unknown prefix.

Large Sixel and Kitty transfers are emitted as bounded lazy segments/chunks rather than requiring one complete encoded transfer allocation.

## 7. Persistent raster ownership — 1.11+

Version 1.11 established a separate terminal-resident ownership domain:

```text
TerminalSession
    -> TerminalRasterResource
        -> TerminalRasterPlacement
        -> TerminalRasterPlacement
        -> ...
```

A public resource is published only after acknowledged upload establishes a private terminal identity. Placement create/update likewise uses correlated acknowledgement through the authoritative query/input path.

Placement position remains the terminal's current cursor location. `Columns` and `Rows` are independently optional and bounded to `1..16384`.

### 7.1 Source-pixel cropping — 1.12

Version 1.12 adds `TerminalRasterSourceRectangle` and `TerminalRasterPlacementOptions.SourceRectangle`.

Coordinates are zero-based source-image pixels. `Width` and `Height` are positive. Scalar values remain within the raster dimension ceiling, and every present rectangle is revalidated by placement options—including `default(TerminalRasterSourceRectangle)` values that bypass the public constructor—before the complete rectangle is checked against the owning resource and before placement output commits.

The resource state stores immutable source width/height metadata needed for this validation. It does not retain source pixel bytes for replay.

Source cropping does **not** change screen placement ownership: the placement still occurs at the current cursor.

### 7.2 Signed z-order — 1.12

`TerminalRasterPlacementOptions.ZIndex` is nullable signed `int` and accepts the full CLR `int` domain.

It expresses signed stacking order to the reviewed persistent backend. It is not a scene graph, parent/child placement chain, or global composition policy.

### 7.3 Shared placement transaction

Create and update use the same acknowledged placement transaction. The reviewed deterministic backend order is:

```text
Ga=p,i=<id>,p=<id>,C=1[,x=...[,y=...[,w=...[,h=...]]]][,c=...][,r=...][,z=...]
```

When a source rectangle is present, all four crop fields are emitted together. Signed z-order uses invariant decimal formatting. When 1.12 options are absent, existing 1.11 bytes/behavior are preserved.

### 7.4 Private identity and bounded registries

Image numbers, terminal image ids, and placement ids remain private and nonzero. Public callers cannot manufacture them.

Session bookkeeping remains bounded:

```text
256  live persistent resources
4096 live persistent placements
```

These are local ownership bounds, not terminal storage-quota promises.

## 8. One authoritative input conversation

A live session owns one incremental byte stream containing ordinary input, lifecycle traffic, active query responses, unsolicited semantic reports, graphics probe replies, and persistent graphics acknowledgements.

Stable precedence remains:

```text
active query/response ownership
    -> recognized unsolicited semantic-event ownership
        -> ordinary application-input decoding
```

No raster or persistent-resource feature creates a competing reader.

Wrong persistent identities do not satisfy another transaction. Timeout/late-response correlation remains bounded and does not allow a stale acknowledgement to complete a later placement.

## 9. Correlation grants ownership, not trust

Matching identifiers establish bounded routing ownership, not terminal authenticity. Malformed framing, numeric overflow, duplicate fields, size bounds, and response grammar remain validated after correlation.

A well-formed correlated `ENOENT` means the terminal no longer recognizes an object the session believed current. That invalidates terminal-resident certainty; it does not authorize hidden replay or imply anything about unrelated state.

## 10. Output commitment

Caller cancellation is honored before commitment where possible. Once a logical graphics transaction commits, ordinary cancellation does not intentionally truncate it.

Persistent resource upload is one committed acknowledged transaction. Placement create/update uses the same serialized session/query authority. Post-commit transport failure is surfaced without blind replay, automatic backend switching, or invented terminal certainty.

## 11. Generation-scoped ownership

Persistent raster resources and placements are not exactly restorable state. They are explicit generation-scoped terminal-resident ownership.

`InvalidateState()` and lifecycle generation changes make existing identities stale. Thereafter:

- placement update returns controlled `Unavailable` before output;
- new placement creation from a stale resource returns controlled `Unavailable` before output;
- stale disposal releases local ownership only;
- no stale numeric protocol identity is emitted;
- no hidden source raster is replayed or re-uploaded.

Applications explicitly create new resources when current persistent ownership is needed again.

## 12. Deterministic cleanup

Placement disposal is locally idempotent and, while current, attempts one quiet targeted delete.

Resource disposal prevents new children, closes child ownership first, attempts child deletion before resource-data deletion, releases local ownership even if cleanup transport fails, and aggregates multiple cleanup failures where necessary.

Session teardown drains committed transactions and deletes current placements before current resource data. Already-stale state receives local-only cleanup.

## 13. Security-sensitive transport choices

Kitty direct transfer remains the reviewed persistent transport. File, temporary-file, and shared-memory transfer are not selected silently because they introduce path, permissions, lifetime, visibility, race, and cross-process concerns.

Base64 is protocol framing, not encryption.

Persistent source cropping operates on already-owned source pixels and does not introduce an external storage transport.

## 14. Stable exclusions after 1.12

Stable 1.x still does not treat the following as ordinary `Icod.Terminal` responsibilities:

- process-global current-terminal state;
- competing live input readers;
- generic raw vendor control/event buses;
- terminal-brand-driven capability proof;
- image-file decoding/transcoding;
- hidden graphics replay or retained persistent source-image cache;
- unbounded graphics/query/event state;
- public raster backend selection or public Kitty ids;
- relative placement graphs or parent placement identities;
- absolute screen-coordinate placement / Terminal-owned layout;
- Unicode placeholder/virtual placements;
- animation/frame lifecycle;
- PTY/ConPTY process hosting;
- cells, windows, layout, damage, or scene-graph ownership.

Source rectangles and z-order are the complete 1.12 advanced placement feature set; they do not imply the excluded scene-layout features.

## 15. Dependency boundary

`Icod.Terminal.csproj` is the direct NuGet dependency authority. The 1.12 production graph remains:

```text
Icod.TermInfo 1.11.0
Icod.Timing   1.0.0
```

Tests, samples, and package consumers may use additional dependencies for qualification without widening the production graph.

## 16. Permanent authorities

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
