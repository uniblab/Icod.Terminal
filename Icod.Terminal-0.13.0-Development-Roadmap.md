# Icod.Terminal 0.13.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.13.0`  
**Development version:** `0.13.0-alpha.4`  
**Predecessor:** `0.12.0` — OSC 133 semantic prompt integration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** observable terminal palette and dynamic-color control  
**Status:** T130–T132 complete/green; T133 implemented; T134 common dynamic colors next after validation

---

## 1. Release objective

`Icod.Terminal 0.13.0` SHALL add typed terminal-color mutation, reset, and observation for the indexed palette and the useful non-Tektronix xterm dynamic-color family.

The release covers:

- OSC 4 — indexed palette set/query;
- OSC 104 — indexed palette reset;
- OSC 10 / 110 — default foreground set/query/reset;
- OSC 11 / 111 — default background set/query/reset;
- OSC 12 / 112 — text-cursor color set/query/reset;
- OSC 13 / 113 — mouse foreground set/query/reset;
- OSC 14 / 114 — mouse background set/query/reset;
- OSC 17 / 117 — highlight background set/query/reset;
- OSC 19 / 119 — highlight foreground set/query/reset.

Observation is a first-class release requirement because future `Icod.DCurses` color policy will consume typed terminal observations.

---

## 2. Frozen architectural contract

T130 froze:

- immutable 16-bit RGB `TerminalColor`;
- `byte` palette indices (`0..255`);
- seven semantic dynamic-color identities;
- canonical outbound `rgb:rrrr/gggg/bbbb`;
- strict equal-width 1–4 digit `rgb:` and selected `#...` reply grammars;
- full-range scaling for `rgb:` shorthand;
- most-significant-bit/zero-fill semantics for hash shorthand;
- reuse of the existing active-query transaction/router;
- timeout/cancellation/format failure as distinct query outcomes;
- no automatic support probing or authoritative color cache;
- reset-to-terminal-policy kept distinct from exact observed restoration;
- no `System.Drawing` dependency;
- direct future `Icod.DCurses` consumption of typed observations.

Full record: `docs/T130-Terminal-Color-Contract-and-Reference-Freeze.md`.

---

## 3. Protocol scope and portability tiers

### Common/core tier

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

### Deliberately excluded Tektronix colors

```text
OSC 15 / 115
OSC 16 / 116
OSC 18 / 118
```

Portability tiers are documentation guidance only. They do not trigger emulator detection, hidden suppression, or cached support inference.

---

## 4. Architectural rules retained from earlier releases

0.13 preserves:

- semantic public APIs;
- specialized internal encoders/parsers;
- complete-frame construction before output commitment;
- validation before output;
- caller cancellation before commitment;
- committed control frames written non-cancellably;
- session-owned output serialization;
- interactive-output requirements;
- explicit bounded active queries;
- no generic OSC escape hatch;
- no second input/output transport;
- no implicit flush except where an established query transaction requires it.

---

## 5. Tranche sequence

### T130 — color contract/reference freeze

**Status:** Complete.  
**Version:** `0.13.0-alpha.1`.

Record: `docs/T130-Terminal-Color-Contract-and-Reference-Freeze.md`.

### T131 — color codec/parser foundation

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.2`.  
**Validation:** PR workflow #539.

Delivered:

- public immutable `TerminalColor`;
- exact 8-bit expansion convenience;
- canonical outbound color codec;
- strict bounded reply parser;
- all frozen precision/grammar normalization semantics;
- malformed-input and round-trip tests independent of session logic.

Record: `docs/T131-Terminal-Color-Codec-and-Parser-Foundation.md`.

### T132 — OSC 4 indexed palette mutation/observation

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.3`.  
**Validation:** PR workflow #546.

Delivered:

- `TerminalPaletteColor` typed index/color pair;
- single indexed palette mutation;
- bounded repeated-pair bulk mutation with duplicate rejection;
- single-index typed observation;
- exact requested-index response correlation;
- ST/BEL/C1 response compatibility through the existing router;
- malformed correlated reply failure;
- no all-or-nothing bulk observation abstraction over per-entry terminal replies.

Record: `docs/T132-OSC-4-Indexed-Palette-Mutation-and-Observation.md`.

### T133 — OSC 104 indexed palette reset

**Status:** Implemented; exact-head validation pending.  
**Version:** `0.13.0-alpha.4`.

Delivered:

- `ResetPaletteColorAsync(byte, ...)`;
- bounded `ResetPaletteColorsAsync(IReadOnlyList<byte>, ...)`;
- `ResetPaletteAsync()` for whole-palette reset;
- canonical ST framing with minimal decimal indices;
- empty/duplicate/>256 validation before commitment;
- one complete non-cancellable committed write;
- no implicit flush;
- explicit tests and documentation proving OSC 104 reset is terminal-policy reset, not exact restoration.

Record: `docs/T133-OSC-104-Indexed-Palette-Reset.md`.

### T134 — common dynamic colors

**Status:** Next after T133 validation.  
**Expected version:** `0.13.0-alpha.5`.

Implement typed mutation, observation, and reset for:

- OSC 10 / 110 — default foreground;
- OSC 11 / 111 — default background;
- OSC 12 / 112 — text cursor.

Use the T131 color codec and existing active-query router. Freeze the final public semantic enum name here before exposing it.

### T135 — extended dynamic colors

**Expected version:** `0.13.0-alpha.6`.

Implement typed mutation, observation, and reset for:

- OSC 13 / 113 — mouse foreground;
- OSC 14 / 114 — mouse background;
- OSC 17 / 117 — highlight background;
- OSC 19 / 119 — highlight foreground.

### T136 — truthful scoped color ownership

**Expected version:** `0.13.0-alpha.7`.

Determine and, where contractually sound, implement query-before-mutate scoped restoration for indexed palette and/or dynamic colors.

Any lease claiming exact restoration must successfully observe its baseline and restore by explicit color mutation. OSC 104/110–119 resets SHALL NOT masquerade as exact restoration.

### T137 — lifecycle, composition, and downstream acceptance

**Expected version:** `0.13.0-alpha.8`.

Prove composition with ordinary text, presentation state, input-protocol leases, cursor style, synchronized output, progress, pointer shape, OSC 7, OSC 8, OSC 52, OSC 133, and active terminal queries.

Add real downstream `Icod.DCurses` acceptance proving typed color observation consumption without raw OSC parsing or a second input path.

### T138 — public API/docs/samples/package/stable closure

**Expected stable version:** `0.13.0`.

Deliver:

- `docs/Public-API-Baseline-0.13.md`;
- root README update;
- focused palette/dynamic-color samples;
- package release notes/tags;
- XML documentation assertions for the full 0.13 public delta;
- fresh NuGet-only consumer on net8/net9/net10;
- retained historical package-contract gates;
- retained and new downstream `Icod.DCurses` gates;
- stable metadata and exact PR/main/tag release validation.

---

## 6. Testing expectations

0.13 testing includes:

- byte-exact outbound frames;
- all palette-index boundaries;
- all selected dynamic-color identities;
- channel boundaries and representative intermediate values;
- every accepted reply precision/grammar;
- explicit `rgb:` versus hash normalization distinction;
- malformed/incomplete/overlong/unexpected replies;
- query timeout and cancellation;
- cancellation while queued for output;
- committed-write non-cancellability;
- query isolation and unrelated-input preservation;
- redirected-output rejection;
- no automatic queries during open/suspend/resume/disposal;
- reset versus exact restoration distinction;
- lifecycle recovery for any T136 ownership model;
- composition with existing protocol families;
- fresh-package consumers;
- real `Icod.DCurses` typed observation consumption.

---

## 7. Explicit non-goals

0.13 does not add:

- arbitrary OSC construction;
- raw public color-specification strings;
- X11 named-color injection;
- Tektronix OSC 15/16/18 or 115/116/118;
- emulator detection as a support oracle;
- automatic palette probing during session open;
- an authoritative long-lived color cache;
- `System.Drawing` as a dependency/public authority;
- downstream color-distance, approximation, contrast, accessibility, or theme policy.

---

## 8. Current development state

```text
VersionPrefix:   0.13.0
VersionSuffix:   alpha.4
Version:         0.13.0-alpha.4
PackageVersion:  0.13.0-alpha.4
AssemblyVersion: 0.13.0.0
```

**Next after green validation:** T134 — common dynamic colors, OSC 10–12 / 110–112.
