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
- serialization of session-managed terminal output;
- lifecycle-aware re-entry and deterministic cleanup.

### `Icod.DCurses`

`Icod.DCurses` owns two-dimensional presentation policy: cells, styles, windows, pads, virtual-screen state, Unicode display width, clipping, wrapping, scrolling, damage tracking, desired-vs-physical screen comparison, and refresh strategy.

DCurses may request semantic raster output from `Icod.Terminal`; it should not reimplement terminal modes, query routing, Sixel framing, capability evidence, or lifecycle restoration.

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

Protocol encoders/parsers, control-family writers, Sixel quantization/encoding, query transactions, capability evidence storage, lifecycle signal sources, presentation/input managers, and OS plumbing remain implementation details unless represented separately by a public semantic contract.

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

For Sixel in 1.7, a Primary Device Attributes response containing parameter `4` is positive protocol-response evidence. A valid response without `4`, or silence/timeout, remains uncertainty rather than automatic proof of unsupported Sixel.

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

Version 1.7 demonstrates why the separation matters:

```text
RasterGraphics             semantic operation
    -> DcsSixel            protocol backend
        -> DCS             control family
            -> Sixel       dialect codec
```

A later Kitty Graphics backend can therefore implement the same semantic raster intent through APC without changing the meaning of `TerminalRasterImage` or forcing callers to construct graphics control strings.

## 5. Backend-neutral raster architecture

Version 1.7 introduces the first public raster model:

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

The raster object is an immutable-owned snapshot. Caller-provided pixel/palette storage is copied at creation. The public API intentionally does not expose mutable backing buffers or a Sixel byte container.

The raw model preserves straight alpha and is intentionally more expressive than the current Sixel backend. Backend limitations are handled during semantic routing/conversion rather than by weakening the common image model.

`TerminalSession.DisplayRasterAsync(...)` describes the intent to display the image. In 1.7, Sixel is the only implementation candidate. The public method does not expose Sixel palette registers, repeat commands, DCS framing, or an explicit backend selector.

## 6. Bounded graphics pipeline

The Sixel implementation is intentionally layered:

```text
TerminalRasterImage
    -> deterministic bounded quantization
        -> SixelPaletteImage
            -> bounded lazy Sixel payload segments
                -> committed serialized DCS transaction
```

The pipeline has explicit limits for dimensions, total pixels, owned raster bytes, palette entries, quantizer work state, small complete DCS frames, and streaming segments.

Large graphics do not require one giant encoded DCS allocation.

The encoder is deterministic for identical input. Image-file decoding, gamma/color-profile processing, and hidden background compositing are outside the core raster contract.

## 7. Session-managed output ordering

High-level application text, semantic output operations, query requests, state-manager traffic, and raster output use session-owned serialization domains appropriate to their contracts.

Committed Sixel output holds the existing session output gate across:

```text
DCS prefix
payload segment(s)
final ST
flush
```

Caller cancellation is honored before commitment. Once the first DCS prefix write commits, ordinary caller cancellation does not truncate the frame. Transport failure is surfaced without retry or speculative recovery.

Session teardown drains committed output before output-state restoration continues.

The library does not claim to serialize writes made directly to the borrowed `TerminalSession.Output` transport.

## 8. One authoritative input conversation

Input is not a collection of independent readers.

The live session owns one incremental byte stream that may contain:

- UTF-8 text;
- traditional keys;
- mouse/focus/bracketed-paste reports;
- modern keyboard frames;
- responses to active terminal queries;
- malformed or unknown terminal traffic.

The decoder/query router therefore form one authoritative path. Public event reads and typed queries coordinate through it.

Sixel capability probing reuses this existing query path; it does not create a graphics-specific reader.

## 9. Ownership and reversible state

A `TerminalSession` owns terminal **state transitions**, not necessarily the underlying descriptor, handle, stream, or transport object.

Consequences include:

- supplied endpoints/transports remain borrowed;
- session disposal restores state the session changed but does not close caller-owned transports;
- reversible terminal features use leases when logical consumers may overlap;
- exact restoration is based on observed/captured state rather than a guessed normal state;
- state that cannot be observed/restored truthfully is not falsely described as exactly restorable;
- invalidation marks stale beliefs untrustworthy rather than inventing certainty.

Raster display is output, not a claim of reversible terminal image state. Version 1.7 does not attempt to capture/restore external Sixel palette/image contents.

## 10. Lifecycle is a state transition

When supported, suspend/resume is integrated with terminal ownership.

Before suspension, the session restores owned terminal state needed to return control safely to the host. After resume, stale beliefs and generation-scoped live evidence are invalidated, configured/owned state is re-established, required observations are refreshed, participants resume, and normal operation continues.

Lifecycle failure may invalidate session state rather than falsely report recovery.

## 11. Failure and uncertainty

The architecture prefers truthful uncertainty over optimistic advancement.

For reversible state, partial completion is tracked and rollback failures are surfaced. For capability evidence, silence does not become unsupported truth. For committed raster output, a transport failure does not trigger an automatic replay because the terminal may have received an unknown prefix of the image.

Controlled `Unavailable` / `Unsupported` results represent capability/semantic limitations where appropriate; transport and malformed-response failures remain failures rather than being disguised as capability states.

## 12. Managed-first platform model

The library is managed C# with narrowly scoped native interop for terminal/console mechanics. There is no runtime dependency on native `ncurses`, `curses`, `libtinfo`, or `termcap`.

Platform differences remain explicit. Windows does not fabricate POSIX semantics, and POSIX does not fabricate Windows console-mode semantics.

Sixel itself is terminal traffic and does not require a platform-native graphics API.

## 13. No process-global current terminal

The primary API is instance-based. `Icod.Terminal` does not require a process-global current session or `cur_term`-style mutable singleton.

Multiple session objects may exist, but the library does not promise that two independently created sessions may concurrently mutate or write protocol traffic to the same physical terminal without external coordination.

## 14. Stable exclusions

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

Version 1.7 intentionally **does** own semantic raster output and its Sixel backend. Kitty Graphics, ReGIS, persistent image identifiers/placements, generalized placement/scaling policy, filesystem/shared-memory graphics transport, and animation remain future work.

## 15. Compatibility authority

The public raster additions are frozen by `docs/Public-API-Baseline-1.7.md` and `.sha256`. The permanent versioning rules are in `Compatibility-and-Versioning.md`.

Architectural improvements may replace internal classes/algorithms without changing these documented semantic, ownership, evidence, serialization, security, and compatibility commitments.
