# D178 — First Semantic Raster-Display Operation

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D178  
**Status:** implemented / validating

## Purpose

D178 is the intentional public API review point for the 1.7 graphics program.

The goal is to expose the smallest stable operation that describes **what raster to display** rather than **how to speak Sixel**. The same public raster contract must remain usable when Kitty Graphics is added as another backend in 1.8.

The public surface is deliberately limited to:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

There is no public raw DCS writer, Sixel command writer, palette-register API, backend selector, or graphics escape-string hook.

## Public raw-raster formats

`TerminalRasterPixelFormat` exposes three tightly packed representations:

```text
Rgb24      R G B
Rgba32     R G B A
Indexed8   one palette-index byte per pixel
```

`TerminalRasterColor` represents one straight/unpremultiplied RGBA8 color.

The alpha default is `255` so ordinary RGB palette entries remain concise.

## Owned image snapshot

`TerminalRasterImage` is an immutable-owned image snapshot.

Public factories are:

```csharp
TerminalRasterImage.CreateRgb24(
    int width,
    int height,
    ReadOnlySpan<byte> pixels
)

TerminalRasterImage.CreateRgba32(
    int width,
    int height,
    ReadOnlySpan<byte> pixels
)

TerminalRasterImage.CreateIndexed8(
    int width,
    int height,
    ReadOnlySpan<byte> pixels,
    ReadOnlySpan<TerminalRasterColor> palette
)
```

Every factory copies caller storage before returning. Later mutation of the source pixel or palette arrays cannot affect asynchronous display.

The public metadata is intentionally small:

```text
Width
Height
PixelFormat
PixelCount
GetPixelColor(x, y)
```

Internal byte layout/accessors needed by the Sixel and future Kitty implementations remain internal. In particular, D178 does not expose the owned backing arrays or a public row-memory view merely because the current implementation stores tight rows internally.

## Existing bounds

D178 retains the already-validated D173 bounds:

```text
width or height       <= 16,384
pixel count           <= 16 Mi
owned direct bytes    <= 64 MiB
indexed palette       1..256 entries
```

Factories reject inconsistent lengths and out-of-range palette indices before an image exists.

These limits are resource/safety policy, not Sixel wire syntax.

## Semantic display method

The first public operation is:

```csharp
ValueTask<TerminalControlMutationResult> DisplayRasterAsync(
    TerminalRasterImage image,
    CancellationToken cancellationToken = default
)
```

The method describes display intent without accepting a protocol backend.

Version 1.7 executes the operation through Sixel only after the normalized evidence/routing layer has verified that backend.

## Capability routing

D178 first resolves:

```text
TerminalSemanticOperation.RasterGraphics
```

If a verified raster backend is already selected, no discovery query is required.

Otherwise D178 performs the bounded D177 Sixel probe and resolves again.

Only `Verified` evidence is sufficient for automatic 1.7 graphics emission. Merely unknown or static/unreviewed identity information does not authorize sending Sixel.

If no verified implemented backend is available, the method returns:

```text
TerminalControlMutationResult.Unavailable(...)
```

This is deliberate. Lack of verified evidence is not equivalent to an authoritative claim that the terminal can never display raster graphics.

There is no blind Sixel fallback.

## Sixel execution

When `DcsSixel` is the verified selected backend:

```text
TerminalRasterImage
    -> SixelPaletteQuantizer
    -> SixelPaletteImage
    -> SixelOutputTransaction
```

D174–D176 therefore remain implementation layers behind the semantic API.

The public method does not expose quantizer settings, register numbers, DCS parameters, band commands, RLE choices, or terminator details.

## Alpha semantics

RGBA32 and Indexed8 can represent fractional alpha because the public raster model is backend-neutral and Kitty Graphics can consume that information in a later release.

Sixel cannot preserve ordinary per-pixel fractional alpha in the 1.7 canonical path.

Therefore, when Sixel is the selected backend and a source contains alpha `1..254`, D178 returns:

```text
TerminalControlMutationResult.Unsupported(...)
```

before graphics output commits.

It does not silently:

- discard alpha;
- threshold alpha;
- composite against an invented terminal background;
- premultiply colors;
- send a semantically different image.

Binary alpha continues to use the D174 rule:

```text
0     transparent / leave destination pixel untouched
255   opaque
```

## Cancellation and commitment

Caller cancellation is observed:

- before capability probing;
- by the capability query path;
- after discovery and before image output;
- by D176 while waiting for the shared output gate and immediately before commitment.

Once D176 writes the first Sixel DCS byte, the existing committed-output rule applies: ordinary caller cancellation cannot truncate the control string mid-frame.

## Failure model

Capability state and transport failure are intentionally separate.

Controlled capability results use `TerminalControlMutationResult`:

```text
Available    raster emitted successfully
Unavailable  no verified implemented raster backend is currently available
Unsupported  selected backend cannot preserve supplied raster semantics
```

Transport exceptions after output has begun are not converted into `Failed` results. They propagate because after a partial terminal write the remote graphics state is uncertain and D176 explicitly forbids speculative retry/recovery.

## Placement and scaling

D178 deliberately freezes **no** public placement, scaling, cropping, animation, persistent-image, or z-order options.

The first operation displays at the backend's ordinary current-position semantics using the image's intrinsic pixel dimensions.

This avoids prematurely forcing Sixel-specific cursor/raster behavior onto the future Kitty Graphics contract. Portable placement/scaling options may be added later only after both backends demonstrate a common semantic meaning.

## Backend evolution

In 1.7:

```text
RasterGraphics -> verified DcsSixel
```

In 1.8 the implementation may add:

```text
RasterGraphics -> verified ApcKittyGraphics
               -> verified DcsSixel
```

without changing the D178 caller contract.

A caller that constructs `TerminalRasterImage` and calls `DisplayRasterAsync(...)` therefore does not need to know which protocol is selected.

## Public API baseline

D178 intentionally changes the stable public API for the first time in this release program.

The repository's generated public API snapshot must therefore be reviewed across `net8.0`, `net9.0`, and `net10.0`, verified identical, and frozen as the 1.7 baseline. The prior 1.4 fingerprint must not simply be overwritten without reviewing the generated D178 surface.

## Deliberately deferred

D178 does not add:

- `TerminalRasterDisplayOptions`;
- explicit protocol/backend selection;
- public quantizer configuration;
- dithering options;
- fractional-alpha compositing policy;
- public raw pixel-memory access;
- PNG/JPEG/GIF decoding;
- Kitty Graphics implementation;
- graphics placement identifiers;
- animation;
- public raw DCS/Sixel commands.

## Acceptance

D178 is complete when:

- RGB24, RGBA32, and Indexed8 are constructible through the public owned raster model;
- caller pixel/palette mutation after construction cannot affect the image;
- public raster metadata and per-pixel color inspection are deterministic;
- verified Sixel evidence produces a successful semantic display and canonical D176 frame;
- an unknown/non-advertising Primary DA result produces `Unavailable` and no Sixel frame;
- fractional alpha with verified Sixel produces `Unsupported` before graphics commitment;
- pre-cancelled display performs no probe or graphics write;
- already-verified Sixel evidence avoids a duplicate capability probe;
- no public backend selector or raw Sixel/DCS API exists;
- the generated public API is identical across all target frameworks and is intentionally frozen as the 1.7 baseline;
- the exact D178 head passes the complete Staging/package matrix.
