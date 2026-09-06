# T131 — Terminal Color Codec and Parser Foundation

**Release:** `0.13.0`  
**Tranche:** `T131`  
**Development version:** `0.13.0-alpha.2`  
**Status:** Implemented; validation pending

---

## 1. Purpose

T131 implements the standalone semantic color value and wire-specification codec frozen by T130. It intentionally adds no OSC 4 session mutation/query API; those operations begin in T132.

---

## 2. Public semantic value

T131 adds public immutable `TerminalColor` with normalized 16-bit RGB channels:

```csharp
TerminalColor color = new(
	0x1234,
	0x5678,
	0x9abc
);
```

The type implements value equality and exposes `FromRgb8(...)` for conventional byte RGB construction. Byte channels expand by multiplication by 257 so both endpoints and intermediate byte values map exactly into the canonical 16-bit domain.

No alpha channel, raw color string, source-precision metadata, terminal identity, or `System.Drawing` dependency is introduced.

---

## 3. Internal canonical encoder

`TerminalColorCodec` emits exactly:

```text
rgb:rrrr/gggg/bbbb
```

using four lowercase hexadecimal digits per 16-bit channel.

The canonical specification is 18 ASCII bytes. Encoding is culture-independent and does not use named colors or caller-provided protocol strings.

---

## 4. Strict parser

The parser accepts the T130 grammar only:

```text
rgb:R/G/B
#RGB
#RRGGBB
#RRRGGGBBB
#RRRRGGGGBBBB
```

`rgb:` is ASCII case-insensitive, requires exactly three equal-width components, and permits 1–4 hexadecimal digits per component.

Hash forms permit exactly 1–4 hexadecimal digits per component through the four frozen total lengths.

Malformed, mixed-width, non-hex, whitespace-bearing, trailing-junk, unsupported, non-ASCII, and overlong specifications are rejected.

---

## 5. Precision normalization

T131 preserves the T130 distinction between the two grammars.

`rgb:` shorthand scales to the complete 16-bit range:

```text
rgb:a/...    -> 0xaaaa
rgb:ab/...   -> 0xabab
rgb:abc/...  -> 0xabca
rgb:abcd/... -> 0xabcd
```

Hash shorthand supplies most-significant bits and zero-fills low bits:

```text
#a..          -> 0xa000
#ab....       -> 0xab00
#abc......    -> 0xabc0
#abcd........ -> 0xabcd
```

The implementation uses integer/bit operations only; no floating-point normalization is required.

---

## 6. Tests

T131 tests cover:

- 16-bit constructor boundaries and representative values;
- byte-to-16-bit expansion boundaries;
- value equality, inequality, hashing, and default black;
- byte-exact canonical lowercase encoding;
- all four accepted `rgb:` precisions;
- all four accepted hash precisions;
- uppercase hexadecimal and uppercase `RGB:` acceptance;
- explicit proof that `rgb:` and hash shorthand normalize differently;
- malformed/mixed-width/missing/extra components;
- unsupported named, `rgbi:`, CSS, and integer forms;
- whitespace and trailing-junk rejection;
- non-ASCII rejection;
- bounded overlong-input rejection;
- representative canonical encode/parse round trips.

---

## 7. T131 decision

The color value/codec foundation is now sufficient for T132 to build typed OSC 4 indexed-palette mutation and observation without exposing raw terminal color strings.

No session-level color operation, query expectation, response routing, reset API, dynamic-color enum, or scoped ownership is introduced by T131.
