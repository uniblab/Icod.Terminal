# Icod.Terminal Architecture

This document is a permanent 1.x architecture authority for `Icod.Terminal`. Historical roadmaps and tranche records explain how the design evolved; this document describes the supported architecture consumers should rely on.

## 1. Role in the Icod terminal stack

`Icod.Terminal` is the live-terminal/session layer between immutable terminal capability data and higher-level terminal user interfaces.

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

`Icod.TermInfo` owns immutable terminal capability data and terminfo interpretation. It answers which capabilities exist, how parameterized strings expand, and how compiled/system/built-in terminal descriptions are resolved.

`Icod.Terminal` consumes that information; it does not maintain a competing terminal-capability database.

### `Icod.Terminal`

`Icod.Terminal` owns one live terminal conversation and the mechanics needed to use it safely:

- endpoint observation and native platform identity;
- terminal-mode capture and semantic input policy;
- exact restoration of captured host state where promised;
- live dimensions and lifecycle observation;
- one authoritative input-reader/decoder path;
- active query/response correlation;
- bounded incremental control-language parsing;
- semantic terminal-output operations;
- reversible presentation, rich-input, and color ownership;
- semantic raster graphics output;
- evidence-driven raster backend selection;
- serialization of session-managed terminal output;
- lifecycle-aware re-entry and deterministic cleanup.

### `Icod.DCurses`

`Icod.DCurses` owns two-dimensional presentation policy: cells, styles, windows, pads, virtual-screen state, Unicode display width, clipping, wrapping, scrolling, damage tracking, desired-vs-physical screen comparison, and refresh strategy.

DCurses may request semantic raster output from `Icod.Terminal`; it should not reimplement terminal modes, query routing, Sixel/Kitty framing, graphics capability evidence, or lifecycle restoration.

### Future `Icod.Pty`

Pseudo-terminal creation, child-process hosting, ConPTY/PTY plumbing, and process ownership remain outside the `Icod.Terminal` 1.x contract.

## 2. Three abstraction levels

### 2.1 Ordinary semantic session API

This is the preferred level for applications.

`TerminalSession` exposes semantic operations for:

- reading normalized terminal events;
- querying live terminal state through typed query methods;
- writing application text;
- emitting reviewed semantic terminal metadata/control operations;
- acquiring reversible presentation/input/color state;
- displaying backend-neutral raw raster images.

Ordinary callers should prefer these APIs because they participate in session ordering, validation, resource bounds, capability evidence, lifecycle, and restoration semantics.

### 2.2 Advanced transport/provider API

`ITerminalInput`, `ITerminalOutput`, `ITerminalControlProvider`, `TerminalEndpoint`, native mode snapshots, and controlled result types remain public for custom hosts, injected transports, diagnostics, and higher-level libraries operating below the semantic layer.

These contracts do not imply that a live session can be bypassed safely. In particular, a `TerminalSession` owns the authoritative input-reader path while active.

`TerminalSession.Output` is a borrowed advanced escape hatch. Direct use is outside session serialization; callers accept responsibility for avoiding interleaving with session-managed traffic.

### 2.3 Internal wire/platform machinery

Protocol encoders/parsers, control-family writers, Sixel quantization/encoding, Kitty Graphics adaptation/chunking, query transactions, capability evidence storage, lifecycle signal sources, presentation/input managers, and OS plumbing remain implementation details unless represented separately by a public semantic contract.

Internal wire selectors are not compatibility promises merely because a public semantic API ultimately uses them.

## 3. Capability-driven, not terminal-brand-driven

`Icod.Terminal` does not equate an interactive terminal with a particular emulator or `TERM` value.

The normalized model separates:

```text
semantic operation
protocol backend
control family
support state
evidence source
```

Static TermInfo/profile advertisement and generation-scoped live evidence are distinct. A terminal name, `TERM`, environment variable, host OS, emulator brand, registry order, or caller preference is not automatically capability proof.

For Sixel, Primary Device Attributes parameter `4` is positive protocol-response evidence. A valid response without `4`, or silence/timeout, remains uncertainty rather than automatic proof of unsupported Sixel.

For Kitty Graphics in 1.8, the library uses the protocol-defined correlated Kitty query immediately followed by Primary DA as a barrier. A correlated Kitty APC response verifies `ApcKittyGraphics`; Primary DA arriving first is reviewed negative protocol-response evidence for that concrete probe. Silence before either authoritative result remains unknown.

Capability evidence is generation-scoped where it describes live protocol behavior and expires on session invalidation/resume.

## 4. Control-language layering

Version 1.5 established the permanent control-language separation:

```text
semantic intent
    -> capability/evidence resolution
        -> protocol backend selection
            -> control-family framing
                -> dialect codec / wire transport
```

The normalized framing vocabulary includes CSI, DCS, OSC, APC, PM, and SOS.

The two raster backends demonstrate the separation directly:

```text
RasterGraphics
    -> DcsSixel
        -> DCS
            -> Sixel

RasterGraphics
    -> ApcKittyGraphics
        -> APC
            -> Kitty Graphics
```

A caller therefore keeps the same `TerminalRasterImage` and `DisplayRasterAsync(...)` semantic contract regardless of which verified backend is selected.

## 5. Backend-neutral raster architecture

Version 1.7 introduced the public raster model:

```text
TerminalRasterImage
TerminalRasterPixelFormat
TerminalRasterColor
```

Supported raw formats are:

```text
Rgb24
Rgba32
Indexed8 + RGBA8 palette
```

The raster object is an immutable-owned snapshot. Caller-provided pixel/palette storage is copied at creation. The public API intentionally does not expose mutable backing buffers or a Sixel/Kitty payload container.

The raw model preserves straight alpha and is intentionally more expressive than any single backend. Backend limitations are handled during routing/conversion rather than by weakening the common image model.

Version 1.8 leaves the public surface unchanged and adds Kitty Graphics beneath it. `DisplayRasterAsync(...)` still does not expose Sixel palette registers, Kitty image/placement identifiers, raw DCS/APC framing, or an explicit backend selector.

## 6. Bounded graphics pipelines

The Sixel path is:

```text
TerminalRasterImage
    -> deterministic bounded quantization
        -> SixelPaletteImage
            -> bounded lazy Sixel payload segments
                -> committed serialized DCS transaction
```

The Kitty Graphics path is:

```text
TerminalRasterImage
    -> checked raw Kitty adaptation
        -> RGB24 or RGBA32 byte stream
            -> bounded lazy Base64 payload chunks
                -> committed serialized APC frame sequence
```

The common raster object enforces explicit limits for dimensions, total pixels, owned bytes, and indexed palette entries. Backend-specific stages add their own bounded work state and frame/chunk limits.

Large graphics do not require one giant encoded DCS string, one giant Base64 string, or one complete multi-frame Kitty transfer allocation.

Image-file decoding, gamma/color-profile processing, and hidden background compositing are outside the core raster contract.

## 7. Raster backend selection

`RasterGraphics` is one semantic operation with two reviewed backends in 1.8:

```text
verified ApcKittyGraphics
verified DcsSixel
```

When both are verified, Kitty Graphics is preferred. Verified Sixel remains fallback. When evidence is unresolved, the session may perform bounded live probing according to the reviewed routing policy.

Routing decisions occur before commitment. Once either backend commits output, a transport/protocol failure is returned to the caller; the library does not replay through the other backend because the terminal may have applied an unknown prefix of the first attempt.

Backend selection is therefore both capability-sensitive and commit-sensitive.

## 8. Alpha and image semantics

The public raster model preserves straight RGBA8 alpha.

Kitty Graphics direct RGBA32 transfer can preserve fractional alpha. Sixel cannot represent the same semantics without an external compositing policy; therefore fractional alpha remains controlled unsupported when Sixel is the selected backend rather than being silently flattened.

Indexed input is expanded for Kitty only as required by its direct raw formats:

- referenced opaque palette colors permit RGB24 expansion;
- any referenced non-opaque palette color requires RGBA32 expansion preserving alpha.

Source width and height remain intrinsic raster dimensions. Version 1.8 does not add a common placement/scaling contract, persistent image identity, source rectangles, z-order, Unicode placeholders, deletion, animation, or cursor-normalization control.

## 9. Session-managed output ordering

High-level application text, semantic output operations, query requests, state-manager traffic, and raster output use session-owned serialization domains appropriate to their contracts.

Committed Sixel output holds the session output gate across:

```text
DCS prefix
payload segment(s)
final ST
flush
```

Committed Kitty output holds the same logical gate across:

```text
APC frame 1
APC frame 2
...
APC final frame
flush
```

Each Kitty frame is structurally complete, but the entire direct transfer is one logical transaction for interleaving/cancellation purposes.

Caller cancellation is honored before commitment. Once the first backend frame commits, ordinary caller cancellation does not intentionally truncate the logical graphics transfer. Transport failure is surfaced without retry or speculative recovery.

Session teardown drains committed output before output-state restoration continues.

The library does not claim to serialize writes made directly to the borrowed `TerminalSession.Output` transport.

## 10. One authoritative input conversation

Input is not a collection of independent readers.

The live session owns one incremental byte stream that may contain:

- UTF-8 text;
- traditional keys;
- mouse/focus/bracketed-paste reports;
- modern keyboard frames;
- responses to active terminal queries;
- side-observed correlated Kitty Graphics probe replies;
- malformed or unknown terminal traffic.

The decoder/query router form one authoritative path. Public event reads and typed queries coordinate through it.

Both Sixel capability observation and Kitty Graphics probing reuse this existing path. Neither backend creates a graphics-specific reader.

For the Kitty support test, the active Primary DA query remains the barrier transaction while the input coordinator side-observes only the matching Kitty APC identified by its probe image id.

## 11. Correlated input ownership

Terminal responses are untrusted even after correlation.

For Kitty Graphics, once a complete matching `i=<probe-id>` field is observed in a recognizable APC prefix, that string becomes boundedly owned by the active probe. Later CAN/SUB, malformed termination, overflow, oversize, or missing ST cannot turn that identified response back into ordinary application input.

Correlation does not bypass grammar or size checks. Oversized correlated APC uses bounded string resynchronization, and an identified but unterminated response fails as malformed rather than being mislabeled as simple silence.

Unrelated APC/OSC/DCS/CSI traffic remains outside the Kitty side observation unless owned by another active query/decoder rule.

## 12. Ownership and reversible state

A `TerminalSession` owns terminal **state transitions**, not necessarily the underlying descriptor, handle, stream, or transport object.

Consequences include:

- supplied endpoints/transports remain borrowed;
- session disposal restores state the session changed but does not close caller-owned transports;
- reversible terminal features use leases when logical consumers may overlap;
- exact restoration is based on observed/captured state rather than a guessed normal state;
- state that cannot be observed/restored truthfully is not falsely described as exactly restorable;
- invalidation marks stale beliefs untrustworthy rather than inventing certainty.

Raster display is ephemeral output, not a claim of reversible terminal image state. Version 1.8 does not capture/restore external Sixel palettes or Kitty image/placement state and does not replay raster images automatically after resume.

## 13. Lifecycle is a state transition

When supported, suspend/resume is integrated with terminal ownership.

Before suspension, the session restores owned terminal state needed to return control safely to the host. After resume, stale beliefs and generation-scoped live evidence are invalidated, configured/owned state is re-established, required observations are refreshed, participants resume, and normal operation continues.

Lifecycle failure may invalidate session state rather than falsely report recovery.

Graphics capability evidence obtained from live protocol behavior expires with the same generation mechanism.

## 14. Failure and uncertainty

The architecture prefers truthful uncertainty over optimistic advancement.

For reversible state, partial completion is tracked and rollback failures are surfaced. For capability evidence, silence does not become unsupported truth. A known protocol barrier may provide negative evidence only where that protocol's ordering semantics make the result authoritative.

For committed raster output, a transport failure does not trigger automatic replay because the terminal may have received an unknown prefix of the image or transfer.

Controlled `Unavailable` / `Unsupported` results represent capability/semantic limitations where appropriate; transport and malformed-response failures remain failures rather than being disguised as capability states.

## 15. Managed-first platform model

The library is managed C# with narrowly scoped native interop for terminal/console mechanics. There is no runtime dependency on native `ncurses`, `curses`, `libtinfo`, or `termcap`.

Platform differences remain explicit. Windows does not fabricate POSIX semantics, and POSIX does not fabricate Windows console-mode semantics.

Sixel and Kitty Graphics are terminal traffic and do not require host-native graphics APIs. Kitty 1.8 uses direct protocol data and does not introduce filesystem/shared-memory/native-image-transfer dependencies.

## 16. No process-global current terminal

The primary API is instance-based. `Icod.Terminal` does not require a process-global current session or `cur_term`-style mutable singleton.

Multiple session objects may exist, but the library does not promise that two independently created sessions may concurrently mutate or write protocol traffic to the same physical terminal without external coordination.

## 17. Stable exclusions

The stable 1.x architecture continues to exclude:

- arbitrary raw escape-sequence dispatch as the normal application API;
- arbitrary OSC/CSI/DCS/APC/vendor command selection;
- blind activation based on terminal branding;
- virtual-screen/window/cell management owned by `Icod.Terminal`;
- PTY/ConPTY child-process ownership;
- application command policy;
- global keyboard hooks, remapping, IME control, or OS-wide hotkeys;
- image-file decoding inside the core terminal package;
- generalized graphics scene/animation ownership.

Version 1.8 owns semantic raster output through Sixel and Kitty Graphics. It still excludes public protocol-specific graphics dispatch, file/temp-file/shared-memory Kitty transfer, persistent image/placement ownership, generalized placement/scaling policy, z-order, Unicode placeholder placement, deletion, and animation.

## 18. Compatibility authority

The public raster additions remain frozen by `docs/Public-API-Baseline-1.7.md` and `.sha256`. Version 1.8 intentionally adds no public API, so that 1.7 baseline remains the current authoritative fingerprint rather than being duplicated under a new filename.

The permanent versioning rules are in `Compatibility-and-Versioning.md`.

Architectural improvements may replace internal classes/algorithms without changing these documented semantic, ownership, evidence, serialization, security, and compatibility commitments.
