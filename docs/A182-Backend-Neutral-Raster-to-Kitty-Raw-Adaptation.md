# A182 — Backend-Neutral Raster-to-Kitty Raw Adaptation

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A182  
**Status:** implemented / validating

## Purpose

A182 maps the public backend-neutral raster model introduced in 1.7 onto the raw pixel formats used by the reviewed Kitty Graphics direct-transfer path.

This tranche does not Base64-encode data, construct multi-frame transfers, probe capability, or change public routing. It establishes only the deterministic raw-byte adaptation boundary consumed by A183.

## Mapping

The frozen mapping is:

```text
TerminalRasterPixelFormat.Rgb24
    -> Kitty f=24
    -> byte-exact existing owned RGB bytes

TerminalRasterPixelFormat.Rgba32
    -> Kitty f=32
    -> byte-exact existing owned RGBA bytes

TerminalRasterPixelFormat.Indexed8
    -> expand deterministically to RGB24 when every referenced palette color is opaque
    -> otherwise expand deterministically to RGBA32 and preserve alpha exactly
```

An unused non-opaque palette entry does not force RGBA32. Only colors actually referenced by image pixels affect the chosen raw transfer format.

## Ownership and allocation

`TerminalRasterImage` already owns immutable snapshots of caller pixel/palette storage.

For RGB24 and RGBA32, A182 reuses that owned immutable memory internally rather than creating another complete pixel copy.

Indexed8 requires expansion because the initial Kitty transfer contract uses raw 24-bit or 32-bit pixels rather than indexed palette data. That expansion allocates exactly one destination raster buffer of three or four bytes per source pixel.

The adapted `ReadOnlyMemory<byte>` therefore always refers either to:

- storage already owned immutably by the source `TerminalRasterImage`; or
- one private expansion array created by A182.

No caller-owned mutable storage is exposed to later asynchronous transmission.

## Alpha contract

Kitty Graphics can carry straight RGBA32 data, so A182 preserves all alpha values exactly, including `0`, `255`, and fractional values `1..254`.

A182 does not:

- premultiply alpha;
- composite against a terminal/background color;
- threshold alpha;
- convert color space or gamma;
- quantize true color;
- reuse the Sixel palette reduction path.

This is intentionally different from the Sixel backend boundary, where fractional alpha is not representable by the reviewed 1.7 path.

## Color contract

RGB channel bytes are preserved exactly as supplied by the public raster model. Indexed expansion copies the resolved palette channel bytes exactly.

No ICC profile, gamma, dithering, palette optimization, or perceptual transformation is performed.

## Bounds

A182 inherits the public raster limits:

```text
maximum dimension          16,384
maximum pixels             16 Mi
maximum raw adapted bytes  64 MiB
```

The worst-case Indexed8 expansion is RGBA32 and therefore remains within the existing 64 MiB raw-raster ceiling at the maximum allowed pixel count.

All dimension and byte-count arithmetic remains checked.

## Public API boundary

A182 introduces no public API.

The caller continues to use:

```text
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

The Kitty raw format and adapted byte representation remain internal backend details.

## Tests

A182 coverage must prove:

- RGB24 dimensions/format/bytes remain exact;
- RGB24 adaptation does not create another complete pixel copy;
- RGBA32 bytes including fractional alpha remain exact;
- RGBA32 adaptation does not create another complete pixel copy;
- opaque Indexed8 expands deterministically to RGB24;
- unused transparent/non-opaque palette entries do not force RGBA32;
- referenced transparent entries force RGBA32 and retain exact alpha;
- referenced fractional-alpha entries retain exact alpha;
- repeated adaptation is deterministic;
- null input is rejected at method entry;
- the 1.7 public API fingerprint remains unchanged.

## Acceptance rule

A182 is accepted only on one exact PR head passing the complete Staging matrix: Windows, Linux, macOS, package candidate/public-API freeze, all four package shards, and the validated package artifact.

A green A182 checkpoint does not authorize merge or publication. PR #46 remains draft while A183–A189 are developed.
