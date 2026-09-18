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
- Terminal-owned dimensions, semantic profile facts, and side-effect-free opaque screen-operation planning;
- bounded session-bound screen-output transactions and serialized-output epoch validation;
- reversible presentation/input/color ownership;
- backend-neutral ephemeral raster output;
- persistent terminal-resident raster resource and physical-placement ownership;
- bounded source-pixel placement cropping and signed z-order;
- bounded immutable-parent relative placement ownership;
- side-effect-free persistent ownership-state observation;
- opaque virtual raster-placeholder ownership and typed current-cursor placeholder-cell encoding/output;
- evidence-driven backend selection behind Terminal semantic operations;
- lifecycle-aware invalidation and deterministic cleanup.

`Icod.DCurses` owns two-dimensional presentation policy: cells, styles, windows, pads, virtual-screen state, Unicode display width, clipping, wrapping, scrolling, damage tracking, desired-vs-physical screen comparison, refresh strategy, and higher-level scene/layout policy.

Pseudo-terminal creation, child-process hosting, ConPTY/PTY plumbing, and process ownership remain outside the `Icod.Terminal` 1.x runtime dependency chain.

## 2. Abstraction levels

### 2.1 Semantic session API

Applications normally use `TerminalSession` for normalized input/events, typed queries, semantic output/state operations, capability planning, raster display, persistent resource/placement ownership, and typed raster-placeholder cell emission.

These APIs participate in session ordering, validation, resource bounds, capability evidence, lifecycle, and cleanup semantics.

Placeholder cells remain current-cursor text output. Terminal owns their protocol-private identity and encoding; applications and higher-level renderers own cursor movement, screen coordinates, clipping, scrolling, damage, and redraw order.

### 2.1.1 Semantic screen planning and commitment

`TerminalSession.Profile` and `TerminalSession.Screen` project the selected terminal description into Terminal-owned semantic facts and opaque operation plans. TermInfo remains the private capability-data, expansion, color, and padding authority. New screen signatures do not expose TermInfo types, raw capability identifiers, or terminal strings.

Planning is side-effect free. A plan identifies its semantic operation, resolved terminal-byte cost, and padding-sensitive affected-line count. A higher layer may compare independently safe plans against its own retained-screen state; Terminal does not decide which cells changed or whether rewriting is preferable.

`PlanRenditionBaseline()` represents unknown physical rendition state. It derives obligations from profile entry/selection evidence and returns a plan only when every exposed attribute and color axis can be restored unconditionally. Attribute restoration precedes original-color restoration. A reset-only profile has no reachable rendition state and therefore receives a valid zero-byte plan; a partially restorable profile receives no plan.

`CreateScreenOutputTransaction(...)` captures the session serialized-output epoch and composes plans, application text, strict hyperlinks, and current raster-placeholder cells. Commit rejects stale work before output, holds the existing output gate, optionally emits synchronized-output framing, attempts required cleanup after commitment, and flushes before release.

This contract does not transfer cells, windows, pads, layout, clipping, Unicode display width, damage, desired-versus-physical comparison, or repaint policy into Terminal.

The downstream boundary is qualified by two independent package consumers. Published stable `Icod.DCurses 1.6.0` exercises unchanged 1.x compatibility against the candidate Terminal package. A separate future-renderer consumer has only a direct `Icod.Terminal` package reference and uses Terminal-owned dimensions, profile, planner, operation plans, and screen-output transactions without direct TermInfo source use.

### 2.2 Advanced transport/provider API

`ITerminalInput`, `ITerminalOutput`, `ITerminalControlProvider`, `TerminalEndpoint`, native mode snapshots, and controlled result types remain public for injected transports, diagnostics, and higher-level libraries.

A live session still owns the authoritative reader. `TerminalSession.Output` is a borrowed advanced escape hatch outside ordinary session serialization.

### 2.3 Internal wire/platform machinery

Protocol encoders/parsers, control-family writers, Sixel quantization/encoding, Kitty Graphics adaptation/chunking/persistent ids, Unicode-placeholder codepoint/diacritic/SGR identity encoding, semantic-event recognition, query transactions, capability evidence storage, lifecycle sources, and OS plumbing remain implementation details unless represented separately by a public semantic contract.

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

The raster semantic capabilities are distinct:

```text
RasterGraphics = 8
    ephemeral backend-neutral raster display

PersistentRasterGraphics = 9
    terminal-resident resource and physical-placement ownership

UnicodeRasterPlaceholders = 10
    virtual-placement ownership plus typed Unicode-placeholder cell rendering
```

Verified Sixel may satisfy ordinary raster display. Persistent ownership requires the reviewed persistent-capable Kitty Graphics path. Placeholder support is not inferred solely from persistent-raster support; where no truthful independent probe exists, successful acknowledged placeholder creation supplies transaction-specific live evidence rather than a fabricated query.

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

UnicodeRasterPlaceholders
    -> ApcKittyGraphics -> acknowledged private virtual placement
        -> typed current-cursor Unicode placeholder-cell output
```

The public raster/resource/placeholder contracts remain semantic even though the current persistent and placeholder implementations are Kitty-specific internally.

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

Ordinary placement position remains the terminal's current cursor location. `Columns` and `Rows` are independently optional and bounded to `1..16384`.

### 7.1 Source-pixel cropping — 1.12

Version 1.12 added `TerminalRasterSourceRectangle` and `TerminalRasterPlacementOptions.SourceRectangle`.

Coordinates are zero-based source-image pixels. `Width` and `Height` are positive. Scalar values remain within the raster dimension ceiling, and every present rectangle is revalidated by placement options—including `default(TerminalRasterSourceRectangle)` values that bypass the public constructor—before the complete rectangle is checked against the owning resource and before placement output commits.

The resource state stores immutable source width/height metadata needed for this validation. It does not retain source pixel bytes for replay.

Source cropping does **not** change screen placement ownership.

### 7.2 Signed z-order — 1.12

`TerminalRasterPlacementOptions.ZIndex` is nullable signed `int` and accepts the full CLR `int` domain.

It expresses signed stacking order to the reviewed persistent backend. It is not a general scene-composition policy.

### 7.3 Relative placement — 1.13

A physical placement may use another current physical placement as immutable parent. Relative offsets are signed terminal-cell offsets and portable depth is bounded to 8.

Resource lifetime and placement-parent lifetime are separate ownership axes. Deleting a parent removes its placement descendants without automatically disposing independently owned raster resources used by those descendants.

Parentage never changes after acknowledged creation. There is no public reparent operation.

### 7.4 Lifecycle observation — 1.14

Resources and physical placements expose one synchronous immutable `TerminalRasterOwnershipState` snapshot.

Observation is side-effect free: it writes no terminal bytes, registers no query, acquires no output gate, performs no cleanup, and does not claim terminal-authenticated remote existence.

The state vocabulary distinguishes current local certainty, stale certainty loss, ownership released by another owner, and explicit wrapper disposal.

### 7.5 Virtual placeholders — 1.15

A resource may also own an acknowledged `TerminalRasterPlaceholder` virtual placement.

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    |       physical placement
    |
    +-- TerminalRasterPlaceholder
            virtual placement
            |
            +-- TerminalRasterPlaceholderCell
                    semantic row/column token
```

Placeholder dimensions are required and bounded to `1..256` rows and columns. Virtual placements share the existing combined 4096 live-placement budget with physical placements.

A placeholder has no Terminal-owned screen position. `GetCell(row, column)` is synchronous and side-effect free; the resulting token privately retains the owning placeholder/session identity needed for validation and encoding while exposing only semantic row/column coordinates.

Each emitted token is self-contained: private image identity, virtual-placement identity, row, and column are all encoded explicitly. The implementation does not rely on neighbor shorthand. Private foreground/underline identity state is terminated selectively so unrelated rendition state is not leaked into following application text.

A physical placement may use a current virtual placeholder as immutable relative parent through `CreateRelativePlacementFromPlaceholderAsync(...)`. The virtual placeholder itself is not relative. The same depth-8 and descendant-before-parent lifetime rules apply to the physical subtree below it.

### 7.6 Private identity and bounded registries

Image numbers, terminal image ids, physical placement ids, virtual-placement ids, and session generation identity remain private. Public callers cannot manufacture them.

Session bookkeeping remains bounded:

```text
256  live persistent resources
4096 live physical + virtual placements
8    maximum relative-placement depth
256  maximum placeholder rows
256  maximum placeholder columns
```

Virtual-placement ids use a private nonzero 24-bit domain because that is the complete identity representable by the reviewed placeholder encoding. These are local ownership bounds, not terminal storage-quota promises.

### 7.7 Resource-owned animation — 1.16

One persistent resource owns one `TerminalRasterAnimation` controller and its root frame. Successful full-size appends publish opaque `TerminalRasterAnimationFrame` tokens; private frame numbers never enter the public contract.

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    +-- TerminalRasterPlaceholder
    +-- TerminalRasterAnimation
            |
            +-- TerminalRasterAnimationFrame
```

The controller is not independently disposable. Resource cleanup remains final authority for image and frame data, while placement and placeholder lifetimes remain separate presentation axes.

Animation adds a second certainty dimension. A resource can remain `Current` while its animation becomes `SequenceUncertain` after an ambiguous committed append. In that state Terminal does not guess the tail or publish a token; known-token timing/selection and stop remain available, while new appends and tail-dependent run modes are blocked.

Animation frame transfer and control reuse the session output gate, authoritative input/query reader, and graphics-response correlation path. No competing reader, hidden source-frame cache, file/shared-memory transfer, or automatic replay is introduced.

Session bookkeeping admits at most 4096 known animation frames across resources, including roots, with one pending append reservation per animation. Stale, released, and owner-disposed animations release their capacity; sequence-uncertain animations retain acknowledged tokens and their capacity.

## 8. One authoritative input conversation

A live session owns one incremental byte stream containing ordinary input, lifecycle traffic, active query responses, unsolicited semantic reports, graphics probe replies, persistent graphics acknowledgements, placeholder acknowledgements, and animation frame/control acknowledgements.

Stable precedence remains:

```text
active query/response ownership
    -> recognized unsolicited semantic-event ownership
        -> ordinary application-input decoding
```

No raster, persistent-resource, placeholder, or animation feature creates a competing reader.

Wrong persistent identities do not satisfy another transaction. Timeout/late-response correlation remains bounded and does not allow a stale acknowledgement to complete a later resource, placement, or placeholder operation.

## 9. Correlation grants ownership, not trust

Matching identifiers establish bounded routing ownership, not terminal authenticity. Malformed framing, numeric overflow, duplicate fields, size bounds, and response grammar remain validated after correlation.

A well-formed correlated missing-resource response invalidates only the ownership certainty justified by that response. It does not authorize hidden replay or imply anything about unrelated state.

Virtual-parent loss similarly invalidates the dependent placement subtree without declaring independent child raster resources missing.

## 10. Output commitment

Caller cancellation is honored before commitment where possible. Once a logical screen or graphics transaction commits, ordinary cancellation does not intentionally truncate it.

Screen-output transactions, persistent resource upload, placement create/update, and placeholder creation are serialized through the session's existing output authority. Typed placeholder-cell output uses the normal session output gate. Post-commit transport failure is surfaced without blind replay, automatic backend switching, or invented terminal certainty.

## 11. Generation-scoped ownership

Persistent raster resources, physical placements, and virtual placeholders are explicit generation-scoped terminal-resident ownership, not exactly restorable state.

`InvalidateState()` and lifecycle generation changes make existing identities stale. Thereafter:

- placement update and new child creation reject stale ownership before output;
- placeholder-cell output rejects stale/released/disposed or cross-session tokens before output;
- stale disposal releases local ownership only;
- no stale numeric protocol identity is emitted;
- no hidden source raster is replayed or re-uploaded.

Applications explicitly create new resources/placeholders when current persistent ownership is needed again.

## 12. Deterministic cleanup

Physical placement disposal is locally idempotent and cleans relative descendants before their parent while current.

Placeholder disposal is locally idempotent and also releases dependent physical relative descendants before the virtual parent.

Resource disposal prevents new children, closes physical and virtual placement ownership, cascades dependent relative subtrees across resources, attempts placement cleanup before resource-data deletion, releases local ownership even if cleanup transport fails, and aggregates multiple cleanup failures where necessary.

Session teardown drains committed transactions and deletes current placement graphs before current resource data. Already-stale state receives local-only cleanup.

## 13. Security-sensitive transport choices

Kitty direct transfer remains the reviewed persistent transport. File, temporary-file, and shared-memory transfer are not selected silently because they introduce path, permissions, lifetime, visibility, race, and cross-process concerns.

Base64 is protocol framing, not encryption.

Persistent source cropping and virtual-placeholder rendering operate on already-owned raster resources and do not introduce an external storage transport.

## 14. Optional TermInfo 1.14 backend planning boundary

The active 1.18 repository uses `Icod.TermInfo 1.15.0`. Optional integration tests and the `Icod.Terminal.TermInfoPersistentRaster.Sample` use `Icod.TermInfo.Inspection 1.15.0`.

Inspection 1.14 adds advisory Sixel/Kitty backend availability evidence, candidate evaluation, and explicit backend-selection planning. That planner remains a **consumer/application policy layer**; it is not invoked by `Icod.Terminal` production routing.

The qualified integration boundary is:

```text
TermInfo static description
    -> lifecycle / placement profiles
    -> backend availability profiles

Terminal live PersistentRasterGraphics result
    -> caller-owned lifecycle evidence
    -> caller-owned Kitty Graphics availability evidence

separate Sixel and Kitty candidate contexts
    -> RasterBackendPlanner
    -> no hidden ranking
    -> explicit caller preference where required
    -> application decides whether to invoke Terminal semantic APIs
```

A conclusive live `PersistentRasterGraphics` result may be mapped by the caller to Kitty availability because Terminal's reviewed persistent route is Kitty-based. Ordinary `RasterGraphics` does not identify Kitty versus Sixel. `UnicodeRasterPlaceholders` is not supplied to the TermInfo 1.14 lifecycle/placement planner as if that planner understood 1.15 placeholder semantics.

TermInfo planning does not replace Terminal's live capability checks, routing, commitment, identity ownership, or cleanup. Production `Icod.Terminal` retains no dependency on `Icod.TermInfo.Inspection` or `Icod.TermInfo.Source`.

## 15. Stable exclusions after 1.18

Stable 1.x still does not treat the following as ordinary `Icod.Terminal` responsibilities:

- process-global current-terminal state;
- competing live input readers;
- generic raw vendor control/event buses;
- terminal-brand-driven capability proof;
- image-file decoding/transcoding;
- hidden graphics replay or retained persistent source-image cache;
- unbounded graphics/query/event state;
- public raster backend selection or public Kitty numeric identities;
- mutable/reparentable placement graphs;
- absolute screen-coordinate placement / Terminal-owned layout;
- pixel-within-cell positioning;
- automatic placeholder redraw or emitted-screen-position tracking;
- partial-frame animation updates, frame composition, and delta editing;
- PTY/ConPTY process hosting;
- cells, windows, layout, damage, or scene-graph ownership.

Relative placement, lifecycle observation, virtual placeholders, and resource-owned animation are supported as bounded semantic ownership contracts; they do not transfer higher-level scene/layout or timeline ownership into Terminal.

## 16. Dependency boundary

`Icod.Terminal.csproj` is the direct NuGet dependency authority. The active 1.18 production graph is:

```text
Icod.TermInfo 1.15.0
Icod.Timing   1.0.0
```

Optional integration tests/samples use `Icod.TermInfo.Inspection 1.15.0`. Inspection and Source remain absent from the production dependency graph.

Historical release documents retain the dependency versions shipped by those releases; advancing the active development dependency does not rewrite those records.

## 17. Permanent authorities

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
