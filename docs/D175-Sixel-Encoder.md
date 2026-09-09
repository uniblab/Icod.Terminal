# D175 — Sixel Encoder

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D175  
**Status:** implementation starting

## Purpose

D175 converts the deterministic D174 `SixelPaletteImage` into canonical Sixel payload segments without allocating one giant encoded control string.

The encoder owns:

- palette-register definition emission;
- RGB8 to Sixel `0..100` conversion;
- six-row band traversal;
- per-color sixel masks;
- canonical repeat selection;
- graphics carriage return/new-line sequencing;
- bounded payload segmentation for D176 streaming.

D175 does not acquire the terminal output gate or emit DCS framing. D176 owns committed session output and the outer `ESC P 0;1;0 q ... ESC \\` transaction.

## Input contract

The encoder consumes one immutable-owned `SixelPaletteImage` from D174:

```text
width
height
opaque RGB8 palette
one palette index per pixel
optional transparent mask
```

Because D174 owns and validates the source arrays, D175 can enumerate output segments asynchronously later without observing caller mutation.

## Palette-register policy

Palette indices map directly to Sixel color registers:

```text
palette index 0   -> register 0
palette index 1   -> register 1
...
palette index 255 -> register 255
```

Only registers referenced by at least one opaque pixel are defined.

Definitions are emitted once, before pixel data, in ascending register order. Every register that is used by pixel data is therefore defined by the image, preserving D172's self-contained palette rule without mutating unused terminal registers.

Each color pass explicitly re-selects its register, including on later six-row bands. The encoder never relies on a register remaining selected across a graphics newline.

## RGB8 to protocol percentage conversion

D175 converts each RGB8 channel to Sixel's integer `0..100` coordinate scale using deterministic nearest-integer rounding:

```text
percent = (channel * 100 + 127) / 255
```

Therefore:

```text
0   -> 0
128 -> 50
255 -> 100
```

This conversion is exact, integer-only, platform-independent, and does not modify D174 palette assignment.

## Six-row band traversal

Sixel encodes six vertical pixels in one data value.

D175 traverses image bands in ascending source Y order:

```text
band 0: rows 0..5
band 1: rows 6..11
...
```

For each band and each opaque pixel:

```text
mask[register, x] |= 1 << rowWithinBand
```

The least-significant bit therefore remains the top pixel in the band, matching D172.

Rows beyond the image height in the final partial band contribute zero bits.

## Bounded workspace

The encoder reuses one band mask workspace sized:

```text
paletteCount * width bytes
```

The D173/D174 bounds make the maximum:

```text
256 * 16,384 = 4,194,304 bytes
```

or 4 MiB.

No full encoded image buffer is required.

A small `bool[]` tracks registers used in the current band and another tracks registers used globally by opaque pixels.

## Color-pass encoding

Within a band, used registers are emitted in ascending register order.

For each register:

1. emit `#<register>`;
2. find the last X position whose mask is nonzero;
3. encode mask values from X=0 through that last nonzero position;
4. omit trailing zero-valued columns;
5. retain leading/interior zero-valued columns because they advance the Sixel X position.

Transparent pixels and pixels assigned to other registers contribute zero bits to that pass.

A band with no opaque pixels emits no color pass.

## Graphics movement

Between color passes in the same band D175 emits:

```text
$
```

which performs Sixel graphics carriage return.

Between consecutive six-row bands D175 emits exactly one:

```text
-
```

which performs graphics newline and returns the Sixel graphics position to the left edge.

The newline is emitted even for an empty/transparent band when a later band exists, so vertical progression remains deterministic.

No trailing graphics newline is emitted after the final image band.

## Canonical repeat policy

A run of identical Sixel data values may be represented either as raw repeated characters or:

```text
!<count><data-character>
```

D175 uses repeat syntax only when the encoded repeat command is **strictly shorter** than the raw run.

For D173's maximum width, this means:

```text
count 1..3   raw characters
count >= 4   repeat form when representable by one D172 repeat command
```

A count of 3 is not repeated because `!3X` and `XXX` have equal length and the canonical rule prefers the simpler raw representation on ties.

Runs larger than the D172 repeat-command ceiling would be split deterministically, although D173's width ceiling is currently much smaller than that limit.

## Payload segmentation

D175 returns payload segments rather than one complete byte array.

Segment classes are:

```text
raster attributes
one palette definition per used register
one color-pass segment per used register/band
one-byte movement segments
```

A color-pass segment contains its `#<register>` selection plus the encoded run data for one band/register pass.

With width limited to 16,384 and repeat encoding never increasing size, one color-pass segment is bounded by approximately:

```text
4 + 16,384 bytes
```

for the longest `#255` prefix plus raw data.

This provides a natural bounded write unit for D176.

## All-transparent images

An all-transparent image is valid.

D175 emits:

- raster attributes;
- no palette definitions;
- no color passes;
- one graphics newline between each pair of six-row bands;
- no trailing newline after the final band.

This preserves the declared raster extent and deterministic vertical band progression without painting any destination pixel.

## Small-frame composition

Tests may concatenate D175 payload segments and wrap them with the D170 `DcsWriter` when the complete vector remains below the 4,096-byte small-frame ceiling.

Production D175 does not require the payload to fit that ceiling. D176 will emit the DCS introducer/header, enumerate D175 segments, and terminate the committed transaction with one ST.

## Security and ownership

D175 accepts no raw caller-provided Sixel commands.

Every emitted byte is derived from:

- validated dimensions;
- bounded palette registers;
- generated integer RGB percentages;
- generated sixel masks;
- fixed movement commands.

No payload segment can inject ESC, CAN, SUB, C1 ST, or arbitrary terminal control sequences.

D175 adds no public API and no input path.

## Deliberately deferred

D175 does not implement:

- session output locking;
- DCS introducer/ST streaming;
- cancellation commit semantics;
- transport-failure recovery;
- Sixel capability probing;
- automatic backend routing;
- public raster display APIs.

Those belong to D176–D178.

## Acceptance

D175 is complete when:

- palette definitions are emitted for used registers only, in ascending order;
- RGB8-to-0..100 conversion is byte-exact at representative boundaries;
- six-row and partial-final-band traversal is byte-exact;
- leading/interior zero columns are retained and trailing zeros are omitted;
- multi-color passes use `$` correctly;
- inter-band progression uses exactly one `-` and no trailing newline;
- all-transparent bands/images remain deterministic;
- repeat counts 1, 3, and 4 freeze the strict-size canonical rule;
- deterministic payload segmentation is covered;
- tiny hand-verifiable payloads compose through `DcsWriter` into expected DCS frames;
- no public API is added;
- the exact D175 head passes the complete Staging/package matrix.
