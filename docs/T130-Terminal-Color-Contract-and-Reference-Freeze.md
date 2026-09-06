# T130 — Terminal Color Contract and Reference Freeze

**Release:** `0.13.0`  
**Tranche:** `T130`  
**Development version:** `0.13.0-alpha.1`  
**Status:** Frozen

---

## 1. Purpose

T130 freezes the semantic and wire-level contract for observable terminal palette and dynamic-color control before `Icod.Terminal` implements color codecs or exposes public color APIs.

Observation is a first-class requirement because future `Icod.DCurses` color policy will consume typed terminal-reported palette and dynamic-color values.

The contract prioritizes:

- typed, precision-preserving observation;
- deterministic semantic mutation;
- explicit bounded queries;
- truthful reset/restoration distinctions;
- reuse of the existing session-owned active-query architecture;
- no raw OSC or XParseColor escape hatch.

---

## 2. Authoritative reference posture

T130 uses xterm control-sequence documentation as the protocol authority for OSC 4/104 and OSC 10–19/110–119 semantics.

Cross-terminal interoperability guidance is informed by xterm.js and Kitty documentation.

Frozen reference observations:

- xterm defines OSC 4 indexed palette mutation/query and allows repeated index/specification pairs;
- xterm defines OSC 104 reset for one, multiple, or all indexed palette entries;
- xterm names OSC 10–19 as dynamic colors and defines their set/query meanings;
- xterm defines resets 110–119 for those dynamic colors;
- xterm accepts either BEL or ST and replies using the query terminator;
- xterm.js documents practical support for OSC 4, 10, 11, 12, 104, 110, 111, and 112;
- Kitty documents broader color-control behavior and highlights the limitations of the legacy xterm color-query family.

0.13 uses canonical ST (`ESC \\`) for its own outbound color operations.

---

## 3. Frozen protocol scope

### 3.1 Indexed palette

```text
OSC 4   indexed palette set/query
OSC 104 indexed palette reset
```

Palette indices use the exact byte domain:

```text
0..255
```

### 3.2 Common dynamic colors

```text
OSC 10 / 110 default foreground set/query/reset
OSC 11 / 111 default background set/query/reset
OSC 12 / 112 text-cursor color set/query/reset
```

### 3.3 Extended xterm dynamic colors

```text
OSC 13 / 113 mouse foreground set/query/reset
OSC 14 / 114 mouse background set/query/reset
OSC 17 / 117 highlight background set/query/reset
OSC 19 / 119 highlight foreground set/query/reset
```

### 3.4 Explicitly excluded

```text
OSC 15 / 115 Tektronix foreground
OSC 16 / 116 Tektronix background
OSC 18 / 118 Tektronix cursor
```

These are not promoted merely to make the numeric range contiguous.

---

## 4. `TerminalColor` semantic value — frozen

The canonical public color value SHALL be an immutable RGB value with unsigned 16-bit channels:

```csharp
public readonly struct TerminalColor : IEquatable<TerminalColor> {
	public ushort Red { get; }
	public ushort Green { get; }
	public ushort Blue { get; }
}
```

Exact constructor/factory naming is left to T131 polish, but the semantics are frozen:

- canonical storage is 16 bits per RGB channel;
- equality compares normalized RGB channel values only;
- there is no alpha channel in the 0.13 terminal-color contract;
- there is no color-space tag;
- there is no retained original wire spelling;
- there is no terminal-emulator identity embedded in the value;
- there is no `System.Drawing` dependency;
- the type is suitable for direct downstream `Icod.DCurses` consumption.

### 4.1 Why 16-bit channels

Terminal color replies may expose up to 16 bits per component. Reducing observations immediately to 8-bit channels would discard information before downstream policy can make its own approximation or contrast decision.

Sixteen-bit channels provide a bounded, allocation-free representation that covers the full frozen parser domain.

### 4.2 Eight-bit convenience

0.13 SHOULD provide ergonomic construction from ordinary 8-bit RGB values.

Eight-bit values expand to canonical 16-bit storage by multiplying by 257:

```text
0x00 -> 0x0000
0x01 -> 0x0101
...
0xFF -> 0xFFFF
```

Any public conversion back to 8-bit SHALL be documented as lossy unless the exact normalized value permits a lossless round trip.

---

## 5. Canonical outbound color grammar — frozen

0.13 SHALL emit numeric RGB colors only.

Canonical outbound specification:

```text
rgb:rrrr/gggg/bbbb
```

Each component is exactly four lowercase hexadecimal digits containing the full 16-bit channel value.

Examples:

```text
rgb:0000/0000/0000
rgb:ffff/ffff/ffff
rgb:ffff/0000/0000
rgb:1234/5678/9abc
```

Reasons:

- deterministic byte-exact output;
- exact representation of the public value model;
- no dependence on X11 named-color databases;
- no shorthand-precision ambiguity;
- straightforward query/set round-trip behavior.

Callers SHALL NOT provide arbitrary color specification strings through the normal public API.

---

## 6. Inbound color grammar — frozen

The observation parser SHALL be strict but interoperable.

### 6.1 Accepted `rgb:` form

Accepted form:

```text
rgb:R/G/B
```

Rules:

- `rgb:` prefix is ASCII case-insensitive;
- exactly three slash-separated hexadecimal components are required;
- each component contains 1–4 hexadecimal digits;
- all three components use the same digit width;
- hexadecimal digits are case-insensitive.

Examples:

```text
rgb:f/0/0
rgb:ff/00/80
rgb:fff/000/800
rgb:ffff/0000/8080
RGB:FFFF/0000/8080
```

### 6.2 Accepted hash forms

Accepted forms:

```text
#RGB
#RRGGBB
#RRRGGGBBB
#RRRRGGGGBBBB
```

No other hash lengths are accepted.

### 6.3 Explicitly rejected forms

0.13 SHALL reject:

- named colors;
- `rgbi:` floating-point notation;
- alpha-bearing specifications;
- CSS `rgb(...)` syntax;
- integer/`0x...` forms;
- missing channels;
- empty components;
- components longer than four hexadecimal digits;
- mixed-width `rgb:` components;
- non-hex characters;
- leading/trailing whitespace;
- trailing junk;
- overlong replies beyond the bounded grammar.

The observation API returns typed semantic color values, not raw reply strings.

---

## 7. Precision normalization — frozen

The two accepted grammars have deliberately different normalization rules.

### 7.1 `rgb:` components scale to the full 16-bit range

An `rgb:` component with `n` hexadecimal digits represents a value over `0..(16^n - 1)` and is scaled to `0..65535`.

For 1–4 hexadecimal digits, this can be implemented exactly without floating point by conventional bit replication:

```text
1 digit:  a    -> aaaa
2 digits: ab   -> abab
3 digits: abc  -> abca
4 digits: abcd -> abcd
```

This preserves both endpoints and xterm/XParseColor-style scaling semantics.

### 7.2 `#...` components represent most-significant bits

Hash-form components do **not** use the same scaling rule.

They are treated as the most-significant bits of a 16-bit component and zero-filled on the right:

```text
#RGB          R -> R000
#RRGGBB       RR -> RR00
#RRRGGGBBB    RRR -> RRR0
#RRRRGGGGBBBB RRRR -> RRRR
```

For example:

```text
#3a7 -> red 0x3000, green 0xa000, blue 0x7000
```

This distinction is frozen and SHALL be tested explicitly in T131.

### 7.3 Original precision provenance

`TerminalColor` does not retain original textual width or grammar.

Future consumers such as `Icod.DCurses` need the normalized observed value, not the terminal's textual spelling. If original precision provenance later proves necessary, it SHALL be a separate reviewed observation type rather than part of `TerminalColor` equality.

---

## 8. Palette index model — frozen

The public palette-index domain SHALL be exactly `byte` / `0..255`.

A wrapper type is unnecessary because every byte value is valid and there are no reserved byte values requiring type-level exclusion.

T132 MAY introduce immutable typed observation records pairing a `byte` index with a `TerminalColor`, but it SHALL NOT widen the index domain.

---

## 9. Dynamic-color semantic identity — frozen

The public semantic identity SHALL be a closed enum, provisionally named `TerminalDynamicColor`:

```csharp
public enum TerminalDynamicColor {
	DefaultForeground,
	DefaultBackground,
	TextCursor,
	MouseForeground,
	MouseBackground,
	HighlightBackground,
	HighlightForeground
}
```

Exact final type naming may receive minor polish before T134, but these seven identities and mappings are frozen:

```text
DefaultForeground   10 / 110
DefaultBackground   11 / 111
TextCursor          12 / 112
MouseForeground     13 / 113
MouseBackground     14 / 114
HighlightBackground 17 / 117
HighlightForeground 19 / 119
```

The enum SHALL NOT expose raw numeric OSC identifiers as its semantic API.

---

## 10. OSC 4 indexed-palette contract

### 10.1 Mutation

Canonical single-entry mutation:

```text
OSC 4 ; index ; rgb:rrrr/gggg/bbbb ST
```

0.13 SHALL provide typed single-entry mutation.

Because xterm permits multiple index/specification pairs in one OSC 4 control, T132 MAY expose typed bulk mutation. If it does:

- the collection is bounded to at most 256 unique palette entries;
- every entry is validated before output commitment;
- duplicate-index policy is explicit and deterministic;
- the complete frame is built before commit;
- no partial emission occurs because a later entry is invalid.

### 10.2 Observation

Canonical single-entry query:

```text
OSC 4 ; index ; ? ST
```

A successful observation requires a matching reply carrying the requested palette index and a valid color specification.

0.13 SHALL provide typed single-entry observation.

xterm may produce multiple replies for one multi-pair OSC 4 query. Bulk observation is useful for future `Icod.DCurses`, but T132 SHALL prefer a result model that can preserve per-index success/failure rather than hiding partial observation behind an all-or-nothing array.

No public API SHALL return raw OSC strings or an untyped metadata dictionary.

---

## 11. OSC 104 palette reset contract

Reset and restoration are distinct operations.

Canonical reset forms:

```text
OSC 104 ST
OSC 104 ; index ST
OSC 104 ; index ; index ... ST
```

Frozen meanings:

- no index — reset the entire indexed palette to terminal policy/default resource values;
- one index — reset that palette entry;
- multiple indices — reset those entries.

An OSC 104 reset SHALL NOT be described as restoring an observed prior value.

A future scoped owner that promises exact restoration must explicitly write the observed baseline color.

---

## 12. Dynamic-color mutation/query/reset contract

For each selected dynamic-color identity:

### Mutation

```text
OSC Ps ; rgb:rrrr/gggg/bbbb ST
```

### Query

```text
OSC Ps ; ? ST
```

### Reset

```text
OSC reset-Ps ST
```

where `reset-Ps` is the corresponding selected reset identifier in 110–119.

A successful query requires a matching dynamic-color reply plus a valid color specification.

Reset returns the terminal to its policy/resource/default value; it is not exact restoration of an observed baseline.

---

## 13. Query transaction and correlation — frozen

All color observation SHALL reuse `TerminalSession`'s existing active-query transaction manager and response router.

Color queries SHALL:

1. validate semantic arguments and timeout;
2. require an interactive session suitable for active queries;
3. acquire the existing active-query transaction;
4. serialize the complete query through the normal session output domain;
5. preserve unrelated input/events while awaiting the reply;
6. accept only a response matching the outstanding color identity/index;
7. parse the color specification strictly;
8. complete with typed observation or a documented failure;
9. release the transaction without consuming unrelated input.

There SHALL be no second public response reader, second input transport, or background color listener.

### 13.1 Terminator correlation

0.13 emits ST for color queries.

The parser/router MAY accept either BEL- or ST-terminated replies where the existing OSC response infrastructure permits both, but no public API depends on echoing a particular reply terminator.

### 13.2 Query deadlines

Public color queries SHALL use an explicit finite `TimeSpan` timeout consistent with existing query APIs.

- timeout => `TimeoutException`;
- caller cancellation remains cancellation;
- timeout SHALL NOT be converted into a permanent unsupported result.

### 13.3 Malformed matching replies

A response that matches the outstanding color identity/index but carries malformed color data SHALL fail with `FormatException` rather than being silently ignored until timeout.

Unrelated OSC responses SHALL NOT satisfy the color query.

---

## 14. Observation result semantics — frozen

OSC 4 and the selected dynamic-color queries do not provide a portable explicit negative/unsupported response equivalent to a boolean capability query.

Therefore successful observation means only:

- a matching response was received;
- the response carried valid color data;
- the returned `TerminalColor` is the normalized value explicitly reported for that transaction.

Failure distinctions remain:

- timeout;
- caller cancellation;
- malformed matching reply;
- transport/session failure.

None becomes an authoritative cached support flag.

---

## 15. Support and portability posture — frozen

### Common/core interoperability tier

```text
OSC 4 / 104
OSC 10 / 110
OSC 11 / 111
OSC 12 / 112
```

### Extended xterm tier

```text
OSC 13 / 113
OSC 14 / 114
OSC 17 / 117
OSC 19 / 119
```

The tier distinction is documentation and interoperability guidance only.

`Icod.Terminal` SHALL NOT:

- detect terminal-emulator brands to suppress requested operations;
- infer support from `TERM`;
- infer support from operating system;
- infer one color family's support from another;
- cache one successful query as a permanent support declaration.

Successful mutation proves complete emission only.

Successful observation proves a matching conforming reply for that transaction only.

---

## 16. Lifecycle posture — frozen for unscoped operations

Explicit unscoped color mutation/query/reset adds no lifecycle participant.

Opening, suspending, resuming, invalidating, or disposing a `TerminalSession` SHALL NOT automatically query, set, or reset palette/dynamic colors merely because the 0.13 APIs exist.

Any lifecycle behavior for scoped color ownership is deferred to T136 and depends on a truthful observed baseline.

---

## 17. Restoration feasibility — frozen constraints

T130 does not yet promise leases, but any T136 lease claiming exact restoration must satisfy all of these:

1. observe the relevant current color before first mutation;
2. retain no mutation if baseline observation fails;
3. store the observed `TerminalColor` as the restoration value;
4. restore by explicit color mutation, not by reset;
5. retain cleanup ownership after failed restoration when retry is meaningful;
6. define nesting and ownership ordering explicitly;
7. avoid inventing a baseline during suspend/resume;
8. treat `InvalidateState()` as physical-state uncertainty without erasing known logical baseline state;
9. perform only cleanup that the library can truthfully justify during session disposal.

OSC 104/110–119 reset APIs may exist independently but SHALL NOT masquerade as exact restoration.

---

## 18. Future `Icod.DCurses` observation contract

The 0.13 observation API SHALL be directly consumable by future `Icod.DCurses` code without raw OSC knowledge.

Downstream code must be able to obtain:

- a typed `TerminalColor` for a palette index;
- a typed `TerminalColor` for each selected dynamic-color identity;
- explicit failure rather than a fabricated fallback color;
- stable 16-bit channel values suitable for approximation, distance, and contrast calculations;
- palette indices as bytes.

`Icod.Terminal` SHALL NOT implement downstream policy such as:

- nearest-palette selection;
- color-distance metrics;
- contrast thresholds;
- theme inference;
- accessibility policy.

T137 acceptance SHALL prove that `Icod.DCurses` can consume typed observations without parsing raw OSC or opening another input path.

---

## 19. Security and bounded-resource posture

Color replies are small bounded protocol values.

The parser/router SHALL enforce bounded reply sizes appropriate to the frozen grammar and reject overlong input rather than allocate without limit.

Bulk palette APIs SHALL be bounded to the finite 256-entry domain and validate all inputs before commit.

No color API emits user text, filesystem data, command text, environment variables, or arbitrary metadata.

---

## 20. T130 decision

T130 freezes `Icod.Terminal 0.13.0` around:

- OSC 4/104 indexed palette control and observation;
- selected non-Tektronix OSC 10–19 dynamic colors and resets 110–119;
- immutable 16-bit RGB `TerminalColor` semantics;
- canonical outbound `rgb:rrrr/gggg/bbbb` encoding;
- strict inbound `rgb:` plus selected `#...` XParseColor-compatible forms;
- distinct scaling rules for `rgb:` and hash grammar;
- `byte` palette indices;
- seven semantic dynamic-color identities;
- explicit bounded active queries through existing session routing;
- no automatic probing or authoritative color cache;
- truthful reset/restoration separation;
- direct future `Icod.DCurses` observation consumption.

T131 may now implement the color value type and codec/parser foundation against this frozen contract.
