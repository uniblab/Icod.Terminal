# Icod.Terminal 0.13.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.13.0`  
**Development version:** `0.13.0-alpha.1`  
**Predecessor:** `0.12.0` — OSC 133 semantic prompt integration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** observable terminal palette and dynamic-color control  
**Status:** T130 contract/reference freeze complete; T131 color codec/parser foundation next

---

## 1. Release objective

`Icod.Terminal 0.13.0` SHALL add typed terminal-color mutation, reset, and observation for the indexed palette and the useful non-Tektronix xterm dynamic-color family.

The release SHALL cover:

- OSC 4 — indexed palette set/query;
- OSC 104 — indexed palette reset;
- OSC 10 / 110 — default foreground set/query/reset;
- OSC 11 / 111 — default background set/query/reset;
- OSC 12 / 112 — text-cursor color set/query/reset;
- OSC 13 / 113 — mouse foreground set/query/reset;
- OSC 14 / 114 — mouse background set/query/reset;
- OSC 17 / 117 — highlight background set/query/reset;
- OSC 19 / 119 — highlight foreground set/query/reset.

Observation is a first-class release requirement. `Icod.DCurses` is expected eventually to consume the observation API for palette-aware color policy, approximation, contrast/accessibility work, and other higher-level behavior. Observation SHALL therefore be designed as a durable public contract rather than a diagnostic side channel.

---

## 2. Scope boundary

### 2.1 Indexed palette

OSC 4 addresses indexed terminal palette entries. T130 freezes the palette-index domain as `byte` / `0..255`, with typed mutation and observation and OSC 104 reset semantics kept distinct from exact restoration.

T132 will decide the final public bulk-operation shape while preserving:

- complete validation before commit;
- finite 256-entry bounds;
- deterministic duplicate-index policy;
- per-index observation truth where terminals return repeated query replies independently.

### 2.2 Dynamic colors

T130 freezes seven semantic dynamic-color identities:

```text
DefaultForeground
DefaultBackground
TextCursor
MouseForeground
MouseBackground
HighlightBackground
HighlightForeground
```

Their mappings are:

```text
DefaultForeground   10 / 110
DefaultBackground   11 / 111
TextCursor          12 / 112
MouseForeground     13 / 113
MouseBackground     14 / 114
HighlightBackground 17 / 117
HighlightForeground 19 / 119
```

The exact final public enum name may receive minor polish before T134, but the semantic set is frozen.

### 2.3 Deliberately excluded Tektronix colors

The xterm dynamic-color range also contains Tektronix-specific entries:

- OSC 15 / 115;
- OSC 16 / 116;
- OSC 18 / 118.

These are outside 0.13. `Icod.Terminal` does not otherwise provide a Tektronix terminal model, and including them merely to make the numeric range contiguous would weaken the semantic API.

---

## 3. Observation is part of the architecture

Color observation SHALL use the existing session-owned active-query transaction and input-response routing architecture wherever the protocol permits a query.

0.13 SHALL NOT create:

- a second public response reader;
- a second input transport;
- background terminal interrogation during `TerminalSession.OpenAsync`;
- automatic support probing;
- a cache presented as though it were authoritative terminal state.

A successful observation represents a matching, explicitly reported terminal color for that transaction. Timeout remains `TimeoutException`, caller cancellation remains cancellation, and malformed matching replies fail as format errors rather than becoming permanent unsupported state.

The public observation model SHALL preserve enough precision that a later `Icod.DCurses` consumer does not have to reverse-engineer raw terminal strings or lose terminal-reported color information.

---

## 4. Terminal color value model — frozen by T130

The canonical semantic representation is an immutable RGB value with 16-bit channels:

```csharp
public readonly struct TerminalColor : IEquatable<TerminalColor> {
	public ushort Red { get; }
	public ushort Green { get; }
	public ushort Blue { get; }
}
```

Frozen semantics:

- 16-bit canonical RGB storage;
- value equality over normalized channels;
- no alpha channel;
- no color-space tag;
- no retained original wire spelling;
- no `System.Drawing` dependency;
- ergonomic 8-bit construction should expand bytes by multiplying by 257;
- canonical outbound grammar is exactly `rgb:rrrr/gggg/bbbb` with lowercase four-digit components;
- inbound parser accepts strict equal-width 1–4 digit `rgb:` forms and `#RGB`, `#RRGGBB`, `#RRRGGGBBB`, `#RRRRGGGGBBBB`;
- `rgb:` shorthand scales components to the full 16-bit range;
- hash shorthand supplies the most-significant bits and zero-fills the remaining low bits;
- named colors, `rgbi:`, CSS syntax, alpha forms, mixed-width `rgb:` components, whitespace, and trailing junk are rejected.

Full record: `docs/T130-Terminal-Color-Contract-and-Reference-Freeze.md`.

---

## 5. Portability tiers — frozen by T130

### 5.1 Common/core tier

```text
OSC 4 / 104
OSC 10 / 110
OSC 11 / 111
OSC 12 / 112
```

### 5.2 Extended xterm dynamic-color tier

```text
OSC 13 / 113
OSC 14 / 114
OSC 17 / 117
OSC 19 / 119
```

The tier distinction is documentation/interoperability guidance only. The public API SHALL not infer support from operating system, `TERM`, emulator identity, environment variables, or success of another color family.

Successful output proves emission, not terminal recognition. Successful observation proves a matching conforming reply for that transaction only.

---

## 6. Architectural rules retained from earlier releases

0.13 SHALL preserve the established `Icod.Terminal` control architecture:

- semantic public APIs;
- specialized internal OSC encoders/parsers;
- complete-frame construction before output commit;
- validation before output;
- caller cancellation before commit;
- committed control frames written non-cancellably;
- session-owned output serialization;
- interactive-output requirement;
- explicit bounded active queries;
- truthful optimistic mutation semantics;
- no terminal-emulator inference as a capability oracle;
- no generic OSC escape hatch;
- no second output transport;
- no implicit flush unless a protocol/lifecycle contract specifically requires one.

---

## 7. Restoration and scoped ownership

Because palette and dynamic colors are observable on terminals that implement the corresponding queries, 0.13 will investigate truthful query-before-mutate scoped restoration.

T130 freezes the minimum truthfulness requirements: a lease may claim exact restoration only after successful baseline observation, must restore by explicitly writing that observed color rather than issuing a reset, and must retain/handle cleanup ownership truthfully across failure and lifecycle edges.

T136 SHALL decide separately for indexed palette and dynamic colors whether those requirements can be satisfied by a useful public lease model.

The library SHALL NOT claim exact restoration when it did not successfully observe the state being restored.

---

## 8. Tranche sequence

### T130 — color contract and reference freeze

**Status:** Complete.  
**Development version:** `0.13.0-alpha.1`.

Frozen:

- authoritative xterm protocol posture plus cross-terminal interoperability guidance;
- OSC 4/104 grammar and semantics;
- selected OSC 10–19 dynamic-color identities and 110–119 resets;
- exclusion of OSC 15/16/18 and 115/116/118;
- 16-bit `TerminalColor` semantics;
- `byte` palette indices;
- seven semantic dynamic-color identities;
- canonical outbound `rgb:rrrr/gggg/bbbb`;
- strict inbound `rgb:` and selected hash grammars;
- distinct `rgb:` scaling and hash most-significant-bit normalization;
- existing active-query transaction/routing reuse;
- timeout/cancellation/format-error semantics;
- common versus extended portability tiers;
- no automatic support probing or authoritative color cache;
- truthful reset/restoration separation;
- future `Icod.DCurses` typed observation requirements.

Record: `docs/T130-Terminal-Color-Contract-and-Reference-Freeze.md`.

### T131 — color codec and parser foundation

**Status:** Next.

Implement and test:

- semantic `TerminalColor` value type;
- canonical outbound encoding;
- strict inbound parser;
- `rgb:` full-range normalization;
- hash-form most-significant-bit normalization;
- malformed/overflow/truncation rejection;
- exhaustive/boundary tests independent of session logic.

Expected development version: `0.13.0-alpha.2`.

### T132 — OSC 4 indexed palette mutation and observation

Implement:

- typed palette set;
- typed palette query;
- protocol-supported multi-entry operations where deterministic and bounded;
- active-query routing/correlation;
- byte-exact and response-parser integration tests.

Expected development version: `0.13.0-alpha.3`.

### T133 — OSC 104 indexed palette reset

Implement:

- reset one entry;
- reset multiple entries;
- reset entire palette;
- ordering/cancellation/failure tests;
- explicit distinction between reset-to-terminal-policy and restoration of an observed prior value.

Expected development version: `0.13.0-alpha.4`.

### T134 — common dynamic colors

Implement mutation, observation, and reset for:

- OSC 10 / 110 — default foreground;
- OSC 11 / 111 — default background;
- OSC 12 / 112 — text cursor.

Expected development version: `0.13.0-alpha.5`.

### T135 — extended dynamic colors

Implement mutation, observation, and reset for:

- OSC 13 / 113 — mouse foreground;
- OSC 14 / 114 — mouse background;
- OSC 17 / 117 — highlight background;
- OSC 19 / 119 — highlight foreground.

Expected development version: `0.13.0-alpha.6`.

### T136 — truthful scoped color ownership

Determine and, where contractually sound, implement scoped restoration for palette and/or dynamic colors using explicit observation baselines.

If truthful scoped restoration cannot be guaranteed for a category, document that result rather than introducing a reset masquerading as restoration.

Expected development version: `0.13.0-alpha.7`.

### T137 — lifecycle, composition, and downstream acceptance

Prove composition with ordinary text, presentation state, rich-input protocol leases, cursor style, synchronized output, progress, pointer shape, OSC 7, OSC 8, OSC 52, OSC 133, and active terminal queries.

Add real downstream `Icod.DCurses` acceptance for the observation API. The acceptance SHALL demonstrate typed observed-color consumption without raw OSC parsing or bypassing `TerminalSession`.

Expected development version: `0.13.0-alpha.8`.

### T138 — public API, docs, samples, package, stable closure

Deliver:

- `docs/Public-API-Baseline-0.13.md`;
- root README update;
- focused palette/dynamic-color sample(s);
- package release notes/tags;
- XML-documentation assertions for the complete 0.13 public delta;
- fresh NuGet-only consumer on net8.0/net9.0/net10.0;
- retained historical package-contract gates;
- retained downstream `Icod.DCurses` acceptance gates;
- new color observation/mutation package and downstream gates;
- stable `0.13.0` metadata;
- exact PR/main/tag release gates.

Expected stable version: `0.13.0`.

---

## 9. Testing expectations

0.13 testing SHALL include at minimum:

- byte-exact outbound frames;
- all palette-index boundaries;
- all selected dynamic-color identities;
- color channel boundaries and representative intermediate values;
- every accepted reply precision/grammar frozen by T130;
- explicit tests proving `rgb:` and hash shorthand normalize differently;
- malformed, incomplete, overlong, and unexpected responses;
- query timeout;
- cancellation before query/output commitment;
- cancellation while queued for output;
- committed-write non-cancellability;
- concurrent query isolation;
- unrelated input preservation while color queries are active;
- redirected-output rejection;
- no automatic queries during open/suspend/resume/disposal;
- reset versus observed restoration distinction;
- lifecycle recovery for any scoped ownership that T136 approves;
- composition with existing output/query families;
- fresh-package consumers for all supported TFMs;
- real `Icod.DCurses` observation consumption.

---

## 10. Explicit non-goals

0.13 SHALL NOT add unless promoted by a reviewed tranche:

- arbitrary OSC construction;
- raw color-specification strings as the normal public API;
- arbitrary X11 named-color injection;
- Tektronix OSC 15/16/18 or resets 115/116/118;
- terminal-emulator detection as a support oracle;
- automatic palette probing during session open;
- an authoritative long-lived terminal-color cache;
- `System.Drawing` as a required dependency or public color authority;
- color-space conversion beyond what is required for the frozen terminal RGB protocol contract;
- `Icod.DCurses` policy for choosing or approximating colors — 0.13 provides the observation foundation that downstream policy can consume.

---

## 11. Current development state

```text
VersionPrefix:   0.13.0
VersionSuffix:   alpha.1
Version:         0.13.0-alpha.1
PackageVersion:  0.13.0-alpha.1
AssemblyVersion: 0.13.0.0
```

**Next:** T131 — implement the frozen `TerminalColor` value and color codec/parser foundation.
