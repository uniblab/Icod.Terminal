# D174 — Deterministic Sixel Palette and Quantization

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D174  
**Status:** implementation starting

## Purpose

D174 converts the backend-neutral D173 raster snapshot into the bounded indexed representation required by the D175 Sixel band encoder.

The conversion must be deterministic, bounded, and explicit about alpha. It must not depend on dictionary iteration order, thread scheduling, platform-native color types, or a hidden terminal background color.

The layering remains:

```text
TerminalRasterImage
    -> SixelPaletteQuantizer
        -> SixelPaletteImage
            -> D175 band encoder
```

## Palette ceiling

The Sixel conversion accepts an internal `maximumColors` value in the inclusive range:

```text
1 .. 256
```

The default is 256, matching the D172 color-register ceiling `0..255`.

The resulting palette never contains more entries than the requested ceiling.

## Exact indexed passthrough

An `Indexed8` source is passed through byte-for-byte when:

- its palette length does not exceed `maximumColors`; and
- every palette entry is fully opaque (`Alpha == 255`).

In that case D174 preserves:

- palette entry order;
- pixel indices;
- exact RGB8 channel values.

This avoids unnecessary quantization of already-valid indexed images.

If the source indexed palette contains transparency, D174 uses the general conversion path so transparent pixels can be represented explicitly rather than emitted as ordinary Sixel colors.

## Alpha policy

Sixel has no ordinary per-pixel fractional alpha channel. D174 therefore freezes a conservative policy:

```text
Alpha == 0     transparent / leave destination pixel untouched
Alpha == 255   opaque / eligible for palette quantization
Alpha 1..254   rejected for the Sixel backend
```

No implicit matte color, terminal-background guess, premultiplication, or thresholding is performed.

This preserves D173's rule that alpha information is not silently destroyed merely because Sixel is the first backend. A future public semantic API may expose an explicit compositing policy if needed, and Kitty Graphics may consume RGBA directly in 1.8.

Transparent pixels are represented by a separate bounded mask in `SixelPaletteImage`; they do not consume a Sixel color register.

An all-transparent raster is valid and produces an empty Sixel palette.

## Exact-color path

For opaque source pixels, D174 first tracks exact RGB colors in deterministic first-pixel order up to the requested palette ceiling.

If the number of distinct opaque colors does not exceed `maximumColors`, no lossy reduction occurs:

- palette entries are ordered by first appearance in row-major source order;
- each source pixel maps to the exact palette entry;
- identical inputs produce identical palettes and indices.

This path applies to RGB24, RGBA32 with only binary alpha, and indexed images that are not eligible for direct passthrough.

## Bounded histogram

When exact colors exceed the requested ceiling, D174 does not retain an unbounded unique-color dictionary.

Instead it uses a fixed 5-bit-per-channel RGB histogram:

```text
32 red buckets
32 green buckets
32 blue buckets
32 * 32 * 32 = 32,768 bins
```

Each occupied bin records:

- pixel count;
- summed 8-bit red values;
- summed 8-bit green values;
- summed 8-bit blue values.

The histogram therefore has a fixed upper memory bound independent of image pixel count or color entropy.

## Deterministic median-cut reduction

Occupied histogram bins are reduced using deterministic weighted median cut.

For each color box:

1. determine red, green, and blue ranges from the bin-average colors;
2. select the channel with the largest range, with ties resolved red, then green, then blue;
3. sort bins by the selected channel and then by the remaining RGB channels;
4. split at the weighted median pixel count while keeping both sides non-empty.

When choosing which box to split next, D174 prefers:

1. largest `(maximum channel range * pixel count)` score;
2. largest maximum channel range;
3. largest pixel count;
4. lowest minimum packed RGB value;
5. earliest box position.

These tie-breaks are explicit so dictionary/hash iteration order can never affect the result.

Each final box produces one opaque palette color using the rounded weighted mean of its source RGB sums.

Final palette order is deterministic by representative packed RGB value, then minimum source packed RGB value.

## Pixel remapping

D174 precomputes a mapping from each occupied 5-bit histogram bin to the nearest final palette color using squared Euclidean RGB distance:

```text
(red difference)^2
+ (green difference)^2
+ (blue difference)^2
```

Equal-distance ties choose the lower palette index.

A second bounded row-major pass writes the final one-byte index array. Transparent pixels retain an arbitrary zero index which is ignored whenever their transparent-mask bit is set.

## Resource bounds

D174 adds no image-size expansion beyond D173's accepted raster bounds.

The main work structures are bounded by:

```text
Histogram bins            32,768
Palette entries           <= 256
Output indices            one byte per source pixel
Transparent mask          zero bytes when absent;
                          one byte per source pixel when present
Exact-color tracking      <= maximumColors entries before overflow
```

The quantizer performs no recursive per-pixel allocations and no parallel nondeterministic work.

## Color-space policy

Version 1.7 quantizes directly in source RGB8 coordinates.

It does not perform:

- gamma correction;
- linear-light conversion;
- ICC color management;
- Lab/OKLab conversion;
- dithering.

These may be considered as compatible quality improvements only if their introduction is explicitly versioned/configurable so identical existing inputs do not silently change canonical output.

For the first stable Sixel release, bounded deterministic behavior is preferred over perceptual sophistication.

## Dithering

D174 deliberately does not dither.

Dithering can materially improve gradients but changes pixel values based on traversal/error state and complicates deterministic streaming. A future optional, explicitly selected policy may add dithering without changing the canonical 1.7 path.

## Relationship to D175

D174 does not assign wire-format `#Pc` definitions or convert RGB8 channels to Sixel's `0..100` component scale.

D175 receives:

```text
width
height
opaque palette RGB8 entries
one palette index per source pixel
optional transparent mask
```

and owns:

- palette-register definition emission;
- RGB8 -> 0..100 Sixel component conversion;
- six-row band traversal;
- per-color bit masks;
- repeat-command selection;
- graphics carriage/new-line sequencing.

## Deliberately deferred

D174 does not add:

- public quantizer options;
- public palette objects;
- dithering;
- fractional-alpha compositing;
- image-file decoding;
- Sixel command emission;
- terminal capability routing;
- streaming output.

## Acceptance

D174 is complete when:

- opaque Indexed8 input has an exact passthrough path;
- exact-color input below the ceiling is lossless and deterministic;
- RGB/RGBA/indexed input shares one bounded conversion result;
- fully transparent pixels do not consume palette entries;
- fractional alpha is rejected rather than silently flattened;
- palette ceilings `1` and `256` are covered;
- gradients/high-entropy input remain within the requested palette ceiling;
- repeated quantization of identical input yields identical palette, indices, and transparency mask;
- stable tie-breaking is covered;
- no public API is added;
- the exact D174 head passes the complete Staging/package matrix.
