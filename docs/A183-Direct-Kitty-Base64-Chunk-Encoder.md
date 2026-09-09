# A183 — Direct Kitty Base64 Chunk Encoder

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A183  
**Status:** implemented / validating

## Purpose

A183 converts one A182 raw Kitty raster into a lazy, deterministic sequence of bounded Kitty Graphics application payloads for direct transmission.

A183 does not write to the terminal, acquire the session output gate, probe capability, or change backend routing. A184 owns the committed multi-frame output transaction.

## Protocol reference

The current Kitty Graphics protocol requires direct pixel data to be Base64 encoded and split into encoded chunks no larger than 4096 bytes. Every non-final chunk must have a length that is a multiple of four. The `m` key is `1` while more chunks follow and `0` for the final chunk.

Only the first graphics escape code carries the complete image metadata. Subsequent chunks carry only `m` and optionally `q` for the ordinary image-transfer case.

The common semantic raster operation uses the transmit-and-display action `a=T`.

Reference:

`https://sw.kovidgoyal.net/kitty/graphics-protocol/`

## Raw-to-Base64 chunk geometry

A183 fixes the maximum raw source block at:

```text
3072 bytes = 4096 * 3 / 4
```

A complete 3072-byte raw block encodes to exactly 4096 Base64 bytes with no padding. Therefore every non-final A183 chunk is naturally four-byte aligned and can be encoded independently while remaining byte-for-byte equivalent to slicing the Base64 representation of the complete raster.

Only the final raw block may be shorter than 3072 bytes and require Base64 padding.

A183 never allocates one complete Base64 representation of the image.

## Canonical first-chunk control data

The first application payload is:

```text
Ga=T,f=<24|32>,s=<width>,v=<height>,t=d,m=<0|1>,q=2;<base64>
```

The key order above is the canonical Icod 1.8 order.

Semantics:

- `a=T` — transmit and display;
- `f=24` or `f=32` — A182 raw format;
- `s` / `v` — source width and height;
- `t=d` — direct transmission;
- `m=1` — more chunks follow;
- `m=0` — this is the final chunk;
- `q=2` — suppress routine terminal responses for the semantic display transfer.

The explicit `q=2` policy prevents ordinary uncorrelated Kitty acknowledgements/errors from entering the application input path. Capability verification is performed separately by A185 using an explicit correlated query action.

## Canonical continuation control data

Every subsequent application payload is one of:

```text
Gm=1,q=2;<base64>
Gm=0,q=2;<base64>
```

No width, height, pixel format, action, transport medium, image id, placement id, or other metadata is repeated on continuation chunks.

A183 does not implement animation-frame continuation, where the Kitty protocol has additional rules.

## Payload and APC bounds

The Base64 portion of every application payload is at most 4096 bytes.

The small complete APC wrapper from A180 has an 8192-byte ceiling, so a maximum-size Base64 chunk plus the bounded A183 control data remains comfortably within the A180 complete-frame bound.

The application payload is ASCII-only by construction, and its Base64 section cannot contain APC framing-control bytes rejected by A180.

## Determinism

For identical `KittyRasterData`, A183 emits the same:

- chunk count;
- chunk boundaries;
- control-data ordering;
- `m` values;
- Base64 bytes.

No hash iteration, scheduler behavior, or platform-specific line/text encoding affects output.

## Public API boundary

A183 introduces no public API and no arbitrary Kitty control-data surface.

The public operation remains `TerminalSession.DisplayRasterAsync(...)`.

## Tests

A183 coverage must prove:

- one RGB24 pixel exact golden vector;
- one RGBA32 pixel exact golden vector;
- exactly 3072 raw bytes produce one final 4096-byte Base64 chunk;
- 3075 RGB24 raw bytes split into 4096-byte plus four-byte Base64 chunks;
- RGBA boundary behavior with a padded final chunk;
- every encoded chunk is at most 4096 bytes;
- every non-final encoded chunk is four-byte aligned;
- concatenated encoded chunks decode to the original raw raster;
- continuation chunks carry only `m` and `q`;
- every application payload fits inside the A180 small-frame ceiling when wrapped;
- repeated encoding is deterministic;
- null input is rejected before lazy enumeration begins;
- the 1.7 public API fingerprint remains unchanged.

## Acceptance rule

A183 is accepted only on one exact PR head passing the complete Staging matrix: Runtime Windows, Runtime Linux, Runtime macOS, package candidate/public-API freeze, all four package shards, and the validated package artifact.

A green A183 checkpoint does not authorize merge or publication. PR #46 remains draft while A184–A189 are developed.
