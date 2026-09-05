# T53 — OSC 52 Selection and Payload Primitives

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.2`  
**Status:** Implemented pending cross-platform validation

---

## 1. Purpose

T53 implements the terminal-I/O-free primitives required by the OSC 52 contract frozen in T52.

This tranche intentionally does not emit terminal control sequences and does not expose any new public API. It establishes only the typed internal selection subset and the bounded base64 conversion layer needed by later writer and query work.

---

## 2. Typed selection mapping

The internal protocol layer now represents the frozen 0.7 selection subset as:

```text
Clipboard  -> c
Primary    -> p
Secondary  -> q
Select     -> s
```

Unknown enum values are rejected with `ArgumentOutOfRangeException`.

The implementation does not accept raw selector strings, multiple selectors, empty selector lists, or cut-buffer indices.

---

## 3. Payload limits

The T52 resource limits are represented directly by the codec:

```text
Maximum decoded payload:       65,536 bytes
Maximum encoded payload:       87,384 bytes
Maximum complete OSC 52 frame: 87,400 bytes
```

The exact canonical write frame for the maximum decoded payload is 87,393 bytes. The frozen 87,400-byte complete-frame ceiling therefore retains seven bytes of headroom while still bounding all later framing work.

All size calculations use checked arithmetic.

---

## 4. Canonical encoder

`TerminalOsc52PayloadCodec.Encode(...)` accepts decoded bytes and produces standard RFC 4648 base64 with:

- the ordinary `A-Z a-z 0-9 + /` alphabet;
- required `=` padding;
- no whitespace;
- no line wrapping;
- no URL-safe alphabet substitution.

An empty input produces an empty encoded payload, preserving the T52 semantic representation for setting a selection to empty.

Payloads larger than 65,536 decoded bytes are rejected before base64 conversion.

---

## 5. Strict decoder

`TerminalOsc52PayloadCodec.Decode(...)` accepts only canonical base64 ASCII bytes.

The decoder rejects:

- encoded payloads whose length is not a multiple of four;
- payloads larger than 87,384 encoded bytes;
- whitespace;
- URL-safe `-` / `_` alphabet characters;
- padding outside the final quantum;
- padding in the first or second position of a quantum;
- malformed padding counts;
- non-base64 bytes;
- non-zero unused bits in the final padded quantum.

The unused-bit checks intentionally reject alternate non-canonical encodings that would decode to the same byte sequence. This keeps response normalization deterministic and ensures a later OSC 52 query cannot accept multiple textual encodings for the same payload.

The decoded length is calculated and bounded before the destination array is allocated.

---

## 6. Test coverage

`TerminalOsc52PayloadCodecTests` covers:

- all four supported selection mappings;
- invalid typed selection rejection;
- exact base64 expansion at 0, 1, 2, 3, 4, 65,535, and 65,536 decoded bytes;
- exact maximum write-frame length calculation;
- RFC 4648 reference vectors;
- full 65,536-byte encode/decode round-trip;
- one-byte-over decoded rejection;
- exact decoded-length calculation;
- malformed-length rejection;
- whitespace rejection;
- URL-safe alphabet rejection;
- misplaced/malformed padding rejection;
- non-zero unused-bit rejection;
- one-quantum-over encoded rejection before decode allocation;
- empty payload round-trip behavior.

The maximum-payload round-trip fills the payload with all possible byte values repeatedly, so the acceptance case is not limited to printable text.

---

## 7. T53 gate

T53 is complete when cross-platform validation proves that:

1. selection mapping is exact and closed to the frozen four-value subset;
2. payload encoding is canonical and deterministic;
3. payload decoding accepts only canonical RFC 4648 base64;
4. all frozen resource limits are enforced before unsafe allocation or terminal I/O;
5. exact boundary and one-over-boundary behavior is tested on `net8.0`, `net9.0`, and `net10.0`.

The next tranche is **T54 — outbound OSC 52 writer**.
