# Icod.Terminal 1.16.0 Development Roadmap

**Release:** `1.16.0`  
**Theme:** Persistent Raster Animation and Frame Lifecycle  
**Status:** T160–T169 complete; stable 1.16.0 candidate qualified  
**Stable compatibility floor:** `1.0.0`  
**Prior completed release line:** `1.15.0` — Unicode Placeholder and Virtual Raster Placement

## Latest accepted checkpoint

The stable 1.16.0 candidate is accepted at exact head `4865691ea65b759a7fe5b279dea08ec8427a6278`.

- Workflow #1959 / run `35250115968` completed successfully across the full nine-job PR matrix.
- The candidate produced `Icod.Terminal.1.16.0.nupkg` and `.snupkg`, and the stable release-line package verifier passed.
- Generated public API snapshots were identical across `net8.0`, `net9.0`, and `net10.0`; the final fingerprint is `d2acfa85aad87c739b3f682096d4d7139627f12bc9d8981b65529eeb79a2da8d`.
- The Linux runtime leg passed 2,169 `Icod.Terminal.Tests` and 15/15 TermInfo integration tests on each target framework.
- The animation sample and all other runtime samples passed on every target framework; stable `Icod.DCurses` acceptance and the eight-cycle hardening soak passed.
- Release version authority, package notes/tags, changelog, curated release notes, packed README references, permanent authorities, and final API-baseline wording are synchronized.
- Merge, `v1.16.0` tagging, GitHub Release creation, NuGet publication, and GitHub Packages publication remain explicit maintainer/release-workflow actions.

## Release objective

Version 1.16 extends the persistent-raster ownership model with terminal-resident animation frames and playback control while preserving the architectural boundaries established by 1.11–1.15.

The release should allow a caller to take an existing persistent raster resource, add additional full-size frames, obtain opaque semantic frame handles/tokens, control per-frame timing, select a current frame, and start/stop terminal-driven playback without learning Kitty image ids, raw frame numbers, `a=f` / `a=a` control dictionaries, or other protocol-private state.

The governing rule is:

> Terminal owns animation protocol identity, frame-sequence certainty, timing commands, and serialized control; callers own source frames, presentation placement, and higher-level animation policy.

Design authority:

[`docs/superpowers/specs/2026-09-15-1.16.0-persistent-raster-animation-frame-lifecycle-design.md`](docs/superpowers/specs/2026-09-15-1.16.0-persistent-raster-animation-frame-lifecycle-design.md)

Permanent persistent-raster ownership authority:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Why animation follows 1.15

The persistent-raster progression is now:

```text
1.11  opaque persistent resources and physical placements
1.12  bounded source-pixel cropping and signed z-order
1.13  immutable-parent relative placement ownership
1.14  side-effect-free lifecycle observability
1.15  virtual placements and Unicode-placeholder text-grid rendering
1.16  animation frames and playback lifecycle
```

Version 1.16 should build on the existing terminal-resident resource rather than create a second image-ownership system. The base resource remains the owner of image data and placements; animation adds one sequence/lifecycle axis attached to that resource.

## Protocol basis

The Kitty Graphics Protocol defines animation in two primary actions:

```text
a=f  transmit animation frame data
a=a  control animation playback/current frame/timing
```

The protocol first creates a normal image and then adds frames to that image. Frame numbering is 1-based and the original image data is frame 1/root. Full-frame additions are appended when no existing frame is selected for editing. Per-frame gaps are controlled by `z`; animation state uses `s`; explicit current-frame selection uses `c`; looping uses `v`.

The protocol separately defines:

```text
a=c  compose one animation frame into another
```

Version 1.16 deliberately excludes `a=c` composition and the more complex partial-frame/delta-editing model. The base animation lifecycle must be stable before those semantics are exposed.

Protocol reference:

<https://sw.kovidgoyal.net/kitty/graphics-protocol/#animation>

## Preferred semantic ownership model

The approved semantic direction is:

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    |
    +-- TerminalRasterPlaceholder
    |
    +-- TerminalRasterAnimation
            |
            +-- TerminalRasterAnimationFrame
```

### TerminalRasterResource

The existing resource remains the owner of terminal-resident raster data, intrinsic width/height, private image identity, generation-scoped certainty, placement/placeholder relationships, and final resource cleanup.

The original resource pixels are the root animation frame.

### TerminalRasterAnimation

`TerminalRasterAnimation` is the preferred public abstraction for one frame sequence attached to one existing persistent resource.

It should own:

- local sequence certainty;
- the opaque set/order of acknowledged frames;
- playback-control operations;
- frame timing control;
- association with one resource/session/generation;
- final animation-wrapper disposal semantics.

It should not own:

- raster placements;
- screen coordinates;
- cursor position;
- clipping;
- windows/cells;
- DCurses damage/layout;
- source-frame retention after successful transfer;
- replay/re-upload policy.

### TerminalRasterAnimationFrame

`TerminalRasterAnimationFrame` is an opaque semantic token/handle for one known frame in the current sequence.

The public object may expose semantic metadata selected at T160, such as whether it is the root frame or its configured duration, but it must not expose the protocol 1-based frame number or private image identity.

## API-regret questions for T160

T160 must freeze the exact public model before production implementation. Required decisions include:

1. exact animation-controller creation/access spelling;
2. whether the root frame is surfaced as a `TerminalRasterAnimationFrame` token;
3. exact frame-add method spelling and result type;
4. timing representation (`TimeSpan` is preferred unless testing finds a better bounded contract);
5. playback state/options shape;
6. loop-count semantics and infinite-loop representation;
7. whether animation state reuses `TerminalRasterOwnershipState` with an animation-specific loss reason or uses a dedicated immutable state type;
8. exact public capability spelling and numeric value, with `PersistentRasterAnimation = 11` the expected candidate;
9. maximum locally tracked frame count and any per-session animation-count ceiling;
10. whether animation wrapper disposal is local-only or emits an explicit stop command before local release.

The release roadmap does not pre-empt these decisions beyond the approved semantic boundaries.

## Frame data contract

Version 1.16 supports **full-frame additions only**.

Every additional frame must match the owning resource's intrinsic pixel dimensions.

The implementation may internally adapt the existing `TerminalRasterImage` storage forms already supported by the persistent Kitty path, but must not introduce file/temp-file/shared-memory transfer or a retained source-frame cache.

The following are excluded from 1.16 frame creation:

- partial rectangles;
- background-canvas selection from a previous frame;
- editing an existing frame's pixel data;
- alpha composition onto an earlier frame;
- gapless hidden composition frames.

Those operations belong to the deferred composition/delta track.

## Root-frame semantics

The base persistent resource image is animation frame 1/root.

No second upload is required merely to enter animation mode.

T160 should prefer exposing the root as a semantic frame token so timing/current-frame APIs can treat frame 1 uniformly with later frames without exposing numeric identity.

The root frame's protocol default gap is zero; if callers want the root displayed during terminal-driven playback, Terminal must provide a semantic way to assign a positive gap before starting normal playback.

## Frame timing contract

1.16 exposes positive displayed-frame timing only.

Protocol `z=0` means "unspecified/ignored" and negative gaps create gapless frames. Version 1.16 should not expose either raw meaning directly.

Preferred semantic rule:

```text
visible frame duration > TimeSpan.Zero
```

Terminal converts the duration to a bounded positive millisecond value with deterministic validation/rounding rules frozen in T160/T164.

Gapless negative timing remains deferred with frame-composition support.

## Playback model

The public model should cover both explicit frame selection and terminal-driven playback.

Semantic operations required by 1.16 are:

```text
select one known frame as current
stop playback
run in loading mode
run in normal looping mode
configure finite or infinite looping
change a known frame's positive duration
```

Protocol-private values remain internal:

```text
s=1  stop
s=2  loading-mode run
s=3  normal run
c=N  current protocol frame number
r=N  target protocol frame number for timing
z=N  frame gap in milliseconds
v=N  protocol loop encoding
```

The public API must not ask callers to provide those raw numeric encodings.

## Loading mode

Loading mode is in scope because it is valuable for streamed animation production without requiring client-side frame switching.

When loading mode reaches the last known frame, the terminal waits for additional frames instead of looping to frame 1.

A caller may therefore:

```text
create resource
    -> create/open animation controller
    -> set root-frame duration
    -> append some frames
    -> start loading-mode playback
    -> append additional frames over time
```

The frame registry and transaction model must remain bounded even when the caller streams frames incrementally.

## Looping semantics

Kitty's loop field uses protocol-specific values where `1` represents infinite looping and values greater than 1 encode a number of repeats as `value - 1`.

The public contract must instead expose semantic loop policy.

T160 should prefer a model equivalent to:

```text
infinite
or
non-negative finite repeat count
```

and keep Kitty's offset encoding private.

Stopping playback resets the terminal's loop counter under the protocol; this behavior must be reflected truthfully in the public semantics/documentation.

## Semantic capability

Version 1.16 should add a distinct semantic capability, expected candidate:

```text
TerminalCapability.PersistentRasterAnimation = 11
```

All previously released `TerminalCapability` values retain their numeric identities.

The semantic distinction remains:

```text
RasterGraphics
    ephemeral raster display

PersistentRasterGraphics
    terminal-resident resource/physical-placement ownership

UnicodeRasterPlaceholders
    virtual-placement ownership and placeholder-cell presentation

PersistentRasterAnimation
    frame-sequence ownership and animation playback control
```

Persistent raster support alone must not silently become verified animation support.

## Capability evidence and verification

`InspectCapability(...)` remains side-effect free.

T161 must audit what evidence can truthfully establish animation support. Version/brand heuristics alone are not sufficient public truth.

If no narrow bounded animation-specific probe can establish support safely without creating durable state, `VerifyCapabilityAsync(...)` must retain controlled no-safe-probe semantics rather than invent traffic.

A successful acknowledged frame addition is authoritative for that specific operation and may strengthen current-generation live animation evidence according to T161's frozen evidence rules.

## Sequence-certainty model

Animation introduces a new local certainty problem that is distinct from resource existence.

When a frame addition succeeds, Terminal knows the next protocol frame has been appended and may publish an opaque frame handle.

If a frame-add transaction commits to the transport but the final outcome becomes ambiguous—for example, transport failure or timeout after the terminal might have accepted the frame—Terminal must not guess whether the frame exists or what the next protocol frame number should be.

Therefore animation needs a distinct sequence-certainty axis:

```text
resource ownership may remain Current
while
animation frame-sequence certainty is lost
```

After sequence certainty is lost:

- no new frame append may emit guessed frame identity;
- no current-frame or per-frame timing command may target a locally guessed frame number;
- no blind retry may occur;
- no automatic image/resource invalidation may be manufactured without independent evidence;
- resource placements/placeholders may remain otherwise usable under their existing contracts;
- disposal remains deterministic/local according to established ownership rules.

T160/T166 must freeze the public observation vocabulary for this condition.

## Response/error semantics

Locally knowable invalid operations are rejected before output.

At minimum this includes:

- null animation/frame options;
- frame dimensions not equal to the resource dimensions;
- invalid or non-positive displayed duration;
- stale/released/disposed resource or animation controller;
- frame token from another animation/session/generation;
- unrepresentable duration/loop values;
- local frame-capacity exhaustion;
- cancellation observed before commitment.

Protocol responses are untrusted input and remain owned by the existing authoritative query/graphics-response path.

A correlated `ENOENT` identifying the owning image/resource follows established missing-resource invalidation rules.

`EINVAL`, storage-pressure failures, malformed responses, wrong identities, late responses, timeout, and generic transport failures do not by themselves manufacture resource-missing truth.

For full-frame 1.16 operations, storage/capacity error spelling must be qualified against actual supported terminals and parser behavior during T167 rather than guessed from composition-only protocol text.

## Committed-output semantics

Animation frame transfer may use chunked committed Kitty data transmission.

The established rule remains:

- cancellation is effective before commitment;
- a committed logical transfer is not intentionally truncated merely because caller cancellation later arrives;
- partial transport failure is surfaced;
- no blind replay or backend switch occurs.

If the terminal may have accepted an appended frame but Terminal cannot prove the transaction result, animation sequence certainty is lost as described above.

## Resource/placement/placeholder interaction

Animation changes which frame of one existing image resource is current. It does not create a second placement graph.

Existing physical placements and virtual placeholders continue to refer to the same owning raster resource.

1.16 must qualify that:

- starting/stopping animation does not mutate placement parentage;
- frame selection does not create/delete placements;
- placeholder identity remains tied to the image/resource and virtual placement rather than to a frame token;
- disposing a placement or placeholder does not delete animation frames;
- disposing/invalidation of the owning resource invalidates the animation controller and frame tokens;
- resource cleanup remains final authority for terminal-resident image/frame data.

## Animation wrapper lifecycle

The animation controller is subordinate to its owning resource and session generation.

Expected semantic outcomes include:

```text
resource/session generation loss
    animation no longer usable

resource missing
    animation no longer usable

resource intentional release/disposal
    animation released/disposed consistently with resource lifecycle

explicit animation-wrapper disposal
    wrapper is final locally; resource remains independently owned

sequence ambiguity
    animation append/control operations requiring frame identity become unavailable
    resource may remain current
```

Exact public state/reason types are frozen in T160.

## Capacity and boundedness

No animation API may create an unbounded local registry or aggregate pixel buffer beyond existing `TerminalRasterImage` bounds.

T160/T162 must select an explicit maximum locally tracked frame count. The bound should be high enough for useful animation but low enough to make registry memory, frame-token validation, wrap/overflow reasoning, and adversarial tests deterministic.

The chosen frame bound is local policy; the terminal may impose a lower storage quota and return controlled failure.

All concurrency/stress tests must use fixed bounded work.

## Concurrency

1.16 must qualify concurrent:

- animation state reads;
- frame-token reads/validation;
- frame addition attempts;
- playback-control attempts;
- resource/animation disposal;
- session invalidation;
- output-gate contention with unrelated terminal output.

The implementation must preserve monotonic state transitions, exact frame publication, no stale/guessed identity emission, and the existing serialized output/query ownership model.

## DCurses boundary

`Icod.DCurses` remains responsible for:

```text
cells
windows
screen coordinates
clipping
damage
scrolling
layout
refresh ordering
higher-level animation policy/timelines
```

`Icod.Terminal` is responsible for:

```text
terminal-resident frame sequence ownership
opaque frame tokens
protocol-private frame numbers
frame timing commands
playback control
acknowledgement/error correlation
sequence certainty
resource/session lifecycle integration
serialized terminal output
```

No DCurses source adoption is required for Icod.Terminal 1.16 success. Stable downstream package acceptance remains a release gate.

## Explicit non-goals

Version 1.16 does not add:

- `a=c` frame composition;
- partial-frame/delta frame transfer APIs;
- editing pixels of an already-known frame;
- gapless negative-duration frames;
- background-canvas selection from prior frames;
- absolute screen-coordinate placement;
- pixel-within-cell placement;
- automatic GIF/APNG decoding;
- image-file decoding/transcoding generally;
- retained source-frame caches for replay;
- automatic reconstruction after generation invalidation;
- audio/timeline synchronization;
- scene/window/cell/damage/layout ownership;
- public Kitty image ids or frame numbers;
- caller-selected production raster backend;
- PTY/ConPTY child-process hosting.

## Tranche roadmap

### T160 — architecture/API-regret gate and public animation contract freeze

Freeze:

- public type/method names;
- capability name/value;
- controller/root-frame model;
- frame-token semantics;
- duration/loop abstractions;
- animation state/sequence-uncertainty vocabulary;
- local frame-count bounds;
- protocol identity exclusions;
- initial cross-TFM public API fingerprint.

No production protocol implementation should precede this freeze.

### T161 — semantic capability and evidence rules

Implement/qualify the animation capability in static/live evidence, inspect-first planning, generation lifetime, endpoint availability, and no-invented-probe semantics.

### T162 — private frame-sequence model and root-frame semantics

Implement bounded frame registry/state, root-frame token semantics, exact append ordering, ownership validation, frame-count capacity, session/resource association, and monotonic sequence-certainty transitions without emitting animation protocol traffic yet.

### T163 — acknowledged full-frame transfer

Implement full-frame `a=f` transmission using the existing reviewed Kitty direct-transfer and authoritative graphics-response path. Publish an opaque frame token only after a successful correlated result.

Qualify chunking, cancellation, dimensions, supported pixel forms, exact frame ordering, and no hidden source retention.

### T164 — per-frame timing and current-frame selection

Implement positive frame-duration control and semantic current-frame selection through opaque frame tokens. Include root-frame duration configuration and exact invalid-token rejection before output.

### T165 — terminal-driven playback control

Implement stop, loading-mode run, normal looping run, and finite/infinite loop policy while keeping raw `s`/`v` protocol values private.

Qualify state transitions, stop/reset semantics, start with insufficient timing, and concurrent append while loading-mode playback runs.

### T166 — lifecycle propagation and sequence-certainty loss

Integrate animation/frame state with resource release, resource-missing evidence, session generation invalidation, explicit animation-wrapper disposal, and ambiguous committed frame-add/control outcomes.

Prove animation sequence uncertainty does not falsely stale an otherwise current raster resource.

### T167 — adversarial hardening

Qualify at least:

```text
root-only animation
maximum local frame count
frame-count overflow/capacity rejection
repeated add/select/timing/start/stop cycles
loading-mode append churn
wrong acknowledgement identity
malformed acknowledgement
ENOENT
EINVAL
timeout
late response
transport failure before commitment
transport failure after commitment
sequence uncertainty
stale/released/disposed frame token
cross-animation token misuse
cross-session token misuse
generation invalidation
concurrent state reads
concurrent frame additions
concurrent playback control
output-gate contention
resource disposal during playback
animation wrapper disposal during playback
```

All work/memory remains explicitly bounded.

### T168 — sample/downstream/package/API/XML/security/documentation qualification

Add an executable backend-neutral animation sample demonstrating:

```text
create persistent resource
    -> obtain animation controller/root frame
    -> set root duration
    -> add full-size frames
    -> place/display resource through existing placement API
    -> run loading mode while appending frames
    -> stop/select frame
    -> run finite/infinite normal playback
    -> deterministic cleanup
```

The sample must not branch on Kitty identities or emit raw protocol commands.

Extend fresh NuGet-only package consumers, generated XML validation, API identity-exclusion tests, permanent ownership/architecture/security/compatibility docs, and stable `Icod.DCurses` downstream qualification.

### T169 — stable 1.16.0 release closure

Synchronize stable version/package metadata, changelog, `docs/releases/1.16.0.md`, root README, final API baseline/fingerprint, current roadmap, and release-facing authorities.

The exact stable candidate and final post-candidate bookkeeping head must each pass the repository's complete release matrix before the PR is considered merge-ready.

Merge, `v1.16.0` tagging, GitHub Release creation, NuGet publication, and GitHub Packages publication remain explicit maintainer/release-workflow actions.

## Required release matrix

Every acceptance/release-closure head must preserve the established matrix:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / public API freeze
Package Foundation
Package Presentation
Package Semantic and hardening
Package Stable 1.x release line
Validated package artifact
```

## Release success definition

Version 1.16 succeeds when a caller can:

```text
create a persistent raster resource
    -> treat its base pixels as the root frame
    -> append acknowledged full-size animation frames
    -> receive opaque semantic frame tokens
    -> configure positive frame timing
    -> explicitly select a known current frame
    -> run/stop terminal-driven playback
    -> stream more frames in loading mode
    -> choose finite or infinite looping
    -> observe deterministic lifecycle/sequence certainty
```

without learning Kitty image ids or frame numbers, without retaining hidden source-frame copies for replay, without moving screen-layout/damage ownership into Terminal, and without corrupting otherwise valid raster-resource ownership when only animation sequence certainty is lost.
