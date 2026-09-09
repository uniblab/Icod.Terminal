# D173 — Common Raw Raster Model

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D173  
**Status:** implementation starting

## Purpose

D173 introduces the backend-neutral raw raster representation that later Sixel and Kitty Graphics implementations can consume without exposing image-file decoding or protocol-specific bytes.

The model remains internal in D173. Public API review remains deferred until D178 after Sixel encoding, streaming, and capability evidence have exercised the shape.

## Supported pixel forms

The initial model supports exactly:

```text
Rgb24      R G B
Rgba32     R G B A
Indexed8   palette index + RGBA8 palette
```

Channel order is byte-exact and not platform-native:

```text
Rgb24   byte 0 red, byte 1 green, byte 2 blue
Rgba32  byte 0 red, byte 1 green, byte 2 blue, byte 3 alpha
```

Indexed8 uses one byte per pixel as a zero-based palette index.

No BGR/BGRA/native-endian aliases are introduced in 1.7.

## Tightly packed rows

D173 deliberately chooses tightly packed storage rather than a stride-bearing model.

```text
Rgb24 row bytes    width * 3
Rgba32 row bytes   width * 4
Indexed8 row bytes width
```

The complete supplied byte length must equal:

```text
row bytes * height
```

exactly. Extra row padding, truncated rows, or trailing bytes are rejected.

This keeps the first stable semantic raster contract deterministic and prevents a backend from accidentally interpreting caller padding as pixels. A future compatible API can add explicit-stride ingestion if a demonstrated consumer requires it.

## Ownership and asynchronous lifetime

D173 uses immutable owned storage.

Factories copy the supplied pixel bytes and indexed palette before returning. Later asynchronous output therefore observes the image snapshot that was validated at construction time rather than mutable caller storage.

The internal model exposes only read-only memory and does not return the owned arrays.

D178 may review whether an advanced borrowed-memory overload is worthwhile, but the default stable semantic path should remain ownership-safe without requiring callers to pin or retain mutable buffers through an asynchronous operation.

## Raster color

`TerminalRasterColor` is an internal straight/unpremultiplied RGBA8 value:

```text
Red    0 .. 255
Green  0 .. 255
Blue   0 .. 255
Alpha  0 .. 255
```

`Rgb24` pixels are observed with alpha `255`.

`Rgba32` preserves the supplied alpha byte exactly.

Indexed palette entries also preserve RGBA8 exactly.

D173 performs no premultiplication, gamma transformation, color-profile conversion, matte compositing, or alpha thresholding.

Sixel cannot directly represent fractional per-pixel alpha. The later Sixel path must therefore make that conversion/rejection policy explicit; D173 does not silently destroy alpha information merely because the first backend is Sixel.

## Indexed palette rules

Indexed8 images require:

- at least one palette entry;
- at most 256 palette entries;
- every pixel index to be strictly less than the palette entry count.

The model copies the palette and validates every index at construction.

No image can therefore carry a dangling/uninitialized palette reference into the quantizer or encoder.

## Resource bounds

D173 intentionally imposes materially tighter semantic bounds than D172's syntax ceilings:

```text
MaximumDimension        16,384
MaximumPixelCount       16,777,216   (16 Mi pixels)
MaximumOwnedPixelBytes  67,108,864   (64 MiB)
MaximumPaletteEntries   256
```

Width and height must both be positive and no greater than `16,384`.

The width/height product must be no greater than 16 Mi pixels. The exact packed byte length must be no greater than 64 MiB.

All multiplications are overflow-checked before allocation or copying.

These are library safety ceilings, not claims about terminal display geometry. D177/D178 may reject an otherwise valid raster when the attached terminal's qualified graphics geometry is smaller.

## Pixel observation

The internal model provides deterministic pixel observation as `TerminalRasterColor` so D174 quantization does not need three independent storage decoders.

Coordinates are zero-based:

```text
0 <= x < Width
0 <= y < Height
```

Out-of-range coordinates are rejected.

Row memory can also be observed read-only for later high-throughput encoder paths.

## Relationship to Sixel

D173 does not assign Sixel color registers or convert RGB values to the protocol's 0–100 coordinate scale.

That separation is deliberate:

```text
TerminalRasterImage
    -> D174 deterministic palette/quantization
        -> D175 Sixel band encoder
            -> D176 committed DCS output
```

Indexed8 input may be eligible for a direct palette-preserving path in D174, but only after D174 freezes deterministic palette assignment and transparency handling.

## Relationship to Kitty Graphics

The raw pixel forms are selected so 1.8 Kitty Graphics can consume the same semantic image without a Sixel-specific public rewrite.

Kitty-specific transfer methods, placement IDs, filesystem/shared-memory transport, animation, and protocol framing do not belong in this raster type.

## Deliberately excluded

D173 does not decode or depend on:

- PNG;
- JPEG;
- GIF;
- BMP;
- WebP;
- platform bitmap handles;
- System.Drawing;
- ImageSharp or another image-processing package.

Applications or higher layers may decode image files into the raw raster representation before calling the eventual semantic terminal API.

## Acceptance

D173 is complete when:

- RGB24, RGBA32, and Indexed8 images are represented internally;
- rows are tightly packed with exact-length validation;
- pixel/palette input is copied and caller mutation cannot change the image;
- width, height, pixel-count, byte-count, and palette bounds are enforced;
- indexed pixels cannot reference absent palette entries;
- RGB24 observes alpha 255;
- RGBA32/indexed alpha is preserved exactly;
- deterministic coordinate and row observation are covered;
- no image-file dependency or public API is added;
- the exact D173 head passes the complete Staging/package matrix.
