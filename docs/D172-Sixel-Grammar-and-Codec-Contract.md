# D172 — Sixel Grammar and Codec Contract

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D172  
**Status:** implementation starting

## Purpose

D172 freezes the Sixel dialect boundary above the generic D170/D171 DCS substrate before raster storage, quantization, and full image encoding are introduced.

Sixel remains one DCS dialect:

```text
DCS Pa ; Pb ; Ph q <sixel-data> ST
```

The generic `DcsWriter` owns DCS framing. `SixelCodec` owns only Sixel syntax and canonical command construction. Later tranches own raster data, palette selection, band traversal, streaming, capability evidence, and public semantics.

## Protocol references

The contract follows the DEC VT330/VT340 graphics programming model and current xterm Sixel control-sequence documentation.

The relevant protocol facts are:

- `Pa` selects the Sixel macro/pixel aspect behavior;
- `Pb` selects how zero bits affect the background;
- `Ph` is the historical horizontal grid-size parameter and is ignored by VT300/xterm-class implementations;
- Sixel data characters occupy `?` through `~` and represent values 0 through 63;
- the least-significant Sixel bit is the top pixel in a six-pixel column;
- `!Pn<char>` repeats one Sixel data character;
- `"Pan;Pad;Ph;Pv` sets raster attributes and must precede Sixel pixel data;
- `#Pc` selects a color register;
- `#Pc;2;Pr;Pg;Pb` defines an RGB color using components in the protocol's 0–100 scale;
- `$` performs graphics carriage return;
- `-` performs graphics new line;
- seven-bit `ESC \\` is a valid ST representation.

## Canonical DCS parameters

Icod.Terminal-generated Sixel uses the explicit DCS parameter string:

```text
0;1;0
```

which means:

```text
Pa = 0  defer the effective pixel shape to explicit raster attributes
Pb = 1  zero-bit pixel positions retain their existing color
Ph = 0  ignored historical horizontal-grid parameter
```

The explicit values are emitted rather than relying on omitted/default parameter interpretation.

### Background policy

`Pb=1` is the canonical 1.7 policy.

This is intentionally background-preserving. It avoids clearing unpainted positions merely because a particular color pass contains zero bits and creates the correct substrate for D173's later alpha/background policy.

For fully opaque images, D175 must ensure every intended image pixel is painted by some color pass. For transparent input, D173 will define which pixels remain intentionally untouched.

D172 does not offer multiple background modes to callers.

## Canonical raster attributes

Every generated image payload begins with:

```text
"1;1;<width>;<height>
```

before color definitions or pixel data.

This freezes:

```text
Pan = 1
Pad = 1
```

for square pixels and explicitly declares image width/height.

The DCS-level `Pa=0` therefore never becomes an implicit application aspect-ratio choice.

D172 permits positive width and height values up to `1,000,000` as a syntax ceiling. D173 will impose the materially tighter raster byte/pixel/work-memory bounds required by the semantic image model.

## Sixel data characters

A Sixel data value is an integer from 0 through 63 inclusive.

Encoding is:

```text
wire byte = '?' + value
```

Therefore:

```text
0   -> ?
1   -> @
63  -> ~
```

The least-significant bit represents the top pixel of the six-pixel vertical column.

Values outside 0–63 are rejected before output construction.

## Repeat command

The structural repeat form is:

```text
!<count><sixel-data-character>
```

D172 accepts positive counts through `1,000,000` as an implementation syntax ceiling.

This does not mean D175 will emit repeat syntax for every repeated value. D175 owns canonical compression policy and may split larger logical runs into bounded repeat commands. It should use repeat form only when doing so satisfies the frozen size/canonicalization rule.

## Graphics movement

The Sixel movement bytes are:

```text
$  graphics carriage return
-  graphics new line
```

D172 exposes these only as internal codec constants. Cursor/display placement semantics belong to later image output/public-operation review.

## Color registers

D172 supports register numbers:

```text
0 .. 255
```

Canonical color selection is:

```text
#<register>
```

Canonical color definition uses RGB coordinates only:

```text
#<register>;2;<red>;<green>;<blue>
```

where each component is an integer from 0 through 100 inclusive.

The DEC HLS form is a valid Sixel protocol form but is deliberately not emitted by the 1.7 canonical codec. D174 can convert source colors deterministically to the RGB 0–100 protocol scale without introducing a second equivalent color representation.

## Palette-state policy

Sixel color registers can outlive one graphics string on some terminals. Version 1.7 therefore follows two rules:

1. every image must define every register it uses before relying on that register;
2. no image may depend on a pre-existing Sixel color-register value.

This makes image rendering self-contained even though the terminal may retain the resulting register definitions afterward.

Version 1.7 does **not** claim exact restoration of external Sixel palette state because the portable protocol does not provide the observation/restoration contract needed to make that claim truthfully.

The xterm private-color-register mode 1070 is not made a prerequisite or silently enabled in D172. It is terminal-specific behavior and requires separate capability/ownership review if later used as an optimization.

## Canonical command ordering

The future D175 image payload follows this order:

```text
1. raster attributes
2. definitions for every color register used by the image
3. Sixel band data using color selections, data characters, repeats, $, and -
```

Raster attributes are never emitted after pixel data begins.

Palette-definition order will be deterministic and is finalized with D174/D175 after the common raster and quantization policies exist.

## Resource bounds

D172 freezes syntax-layer ceilings only:

```text
MaximumDimension       1,000,000
MaximumRepeatCount     1,000,000
MaximumColorRegister   255
MaximumRgbComponent    100
SixelDataValue         0 .. 63
```

Later semantic/raster layers must impose tighter bounds where appropriate. These ceilings are not promises that a million-pixel dimension or million-character run is accepted by the eventual public raster operation.

## Small-frame relationship

Tiny Sixel vectors may be composed through `DcsWriter.EncodeFrame(...)` for tests and internal small examples using:

```text
parameter bytes     0;1;0
intermediate bytes  empty
final selector      q
payload             Sixel commands
```

Production raster output is not required to fit the D170 4,096-byte small-frame allocation. D176 supplies the committed streaming DCS transaction for ordinary/large images.

## Security and ownership

D172 remains internal and adds no raw public Sixel writer.

All generated command bytes are bounded printable ASCII. The codec never accepts caller-provided raw Sixel command strings, so caller data cannot inject DCS termination, CAN/SUB aborts, ESC sequences, or arbitrary terminal commands through this layer.

D172 does not change the one-authoritative-reader rule and adds no input path.

## Deliberately deferred

D172 does not implement:

- a public raster type;
- RGB/RGBA/indexed raster storage;
- alpha conversion;
- quantization;
- palette assignment policy;
- six-row band traversal;
- automatic repeat selection;
- streaming output;
- Sixel capability probing;
- DECSDM mode 80 ownership;
- xterm private color-register mode 1070 ownership;
- public placement semantics.

Those belong to D173–D178.

## Acceptance

D172 is complete when:

- canonical DCS Sixel parameters are encoded as `0;1;0`;
- square-pixel raster attributes are encoded as `"1;1;width;height`;
- data values 0 and 63 and their invalid boundaries are covered;
- repeat syntax and bounds are covered;
- color selection and RGB definition syntax/bounds are covered;
- graphics carriage-return/new-line bytes are frozen;
- a tiny generated payload composes through `DcsWriter` into the expected seven-bit Sixel DCS structure;
- no public API is added;
- the exact D172 head passes the complete Staging/package matrix.
