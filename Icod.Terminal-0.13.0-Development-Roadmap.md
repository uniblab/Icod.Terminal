# Icod.Terminal 0.13.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.13.0`  
**Development version:** `0.13.0-alpha.5`  
**Predecessor:** `0.12.0` — OSC 133 semantic prompt integration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** observable terminal palette and dynamic-color control  
**Status:** T130–T133 complete/green; T134 common dynamic colors implemented; T135 next after validation

---

## 1. Release objective

`Icod.Terminal 0.13.0` adds typed terminal-color mutation, reset, and observation for the indexed palette and the useful non-Tektronix xterm dynamic-color family.

Scope:

- OSC 4 / 104 — indexed palette;
- OSC 10 / 110 — default foreground;
- OSC 11 / 111 — default background;
- OSC 12 / 112 — text cursor;
- OSC 13 / 113 — mouse foreground;
- OSC 14 / 114 — mouse background;
- OSC 17 / 117 — highlight background;
- OSC 19 / 119 — highlight foreground.

Observation remains a first-class requirement for future `Icod.DCurses` consumption.

---

## 2. Frozen architectural contract

T130 froze:

- immutable 16-bit RGB `TerminalColor`;
- byte palette indices;
- seven semantic dynamic-color identities;
- canonical `rgb:rrrr/gggg/bbbb` output;
- strict bounded `rgb:` and selected hash reply parsing;
- distinct shorthand normalization semantics;
- existing active-query transaction/router reuse;
- distinct timeout/cancellation/format failures;
- no automatic probing or authoritative color cache;
- reset-to-terminal-policy distinct from exact restoration;
- no `System.Drawing` dependency;
- future `Icod.DCurses` typed observation consumption.

`TerminalDynamicColor` is now the frozen public enum name.

---

## 3. Portability tiers

Common/core:

```text
OSC 4 / 104
OSC 10 / 110
OSC 11 / 111
OSC 12 / 112
```

Extended xterm:

```text
OSC 13 / 113
OSC 14 / 114
OSC 17 / 117
OSC 19 / 119
```

Excluded Tektronix colors:

```text
OSC 15 / 115
OSC 16 / 116
OSC 18 / 118
```

---

## 4. Tranche sequence

### T130 — color contract/reference freeze

**Status:** Complete.  
**Version:** `0.13.0-alpha.1`.

Record: `docs/T130-Terminal-Color-Contract-and-Reference-Freeze.md`.

### T131 — color codec/parser foundation

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.2`.  
**Validation:** workflow #539.

Delivered `TerminalColor`, canonical encoding, strict parsing, normalization, and codec tests.

Record: `docs/T131-Terminal-Color-Codec-and-Parser-Foundation.md`.

### T132 — OSC 4 indexed palette mutation/observation

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.3`.  
**Validation:** workflow #546.

Delivered typed single/bulk mutation and single-index observation on the shared query router.

Record: `docs/T132-OSC-4-Indexed-Palette-Mutation-and-Observation.md`.

### T133 — OSC 104 indexed palette reset

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.4`.  
**Validation:** workflow #553.

Delivered single, bounded multiple, and whole-palette terminal-policy reset with explicit reset/restoration distinction.

Record: `docs/T133-OSC-104-Indexed-Palette-Reset.md`.

### T134 — common dynamic colors

**Status:** Implemented; exact-head validation pending.  
**Version:** `0.13.0-alpha.5`.

Delivered:

- frozen public `TerminalDynamicColor` enum name and seven semantic identities;
- `SetDynamicColorAsync(...)`;
- `QueryDynamicColorAsync(...)`;
- `ResetDynamicColorAsync(...)`;
- T134 activation for default foreground, default background, and text cursor;
- OSC 10/11/12 canonical set/query framing;
- OSC 110/111/112 reset framing;
- exact response-identity correlation on the existing active-query router;
- typed `TerminalColor` observation through the T131 codec;
- malformed correlated response failure;
- terminal-policy reset semantics;
- extended T135 identities rejected before output until their protocol mapping is activated.

Record: `docs/T134-Common-Dynamic-Color-Mutation-Observation-and-Reset.md`.

### T135 — extended dynamic colors

**Status:** Next after T134 validation.  
**Expected version:** `0.13.0-alpha.6`.

Activate the existing typed dynamic-color API for:

- OSC 13 / 113 — mouse foreground;
- OSC 14 / 114 — mouse background;
- OSC 17 / 117 — highlight background;
- OSC 19 / 119 — highlight foreground.

### T136 — truthful scoped color ownership

**Expected version:** `0.13.0-alpha.7`.

Determine and, where contractually sound, implement query-before-mutate scoped restoration for indexed palette and/or dynamic colors. Exact restoration requires a successful observed baseline and explicit color replay; reset controls do not substitute for restoration.

### T137 — lifecycle, composition, and downstream acceptance

**Expected version:** `0.13.0-alpha.8`.

Prove composition with existing output/query families and add real downstream `Icod.DCurses` acceptance consuming typed observations without raw OSC parsing or another input path.

### T138 — public API/docs/samples/package/stable closure

**Expected stable version:** `0.13.0`.

Deliver the 0.13 public API baseline, README/sample updates, package release metadata, XML API assertions, fresh NuGet-only consumers, retained historical gates, new color gates, and exact stable PR/main/tag validation.

---

## 5. Testing expectations

0.13 testing includes byte-exact framing, all palette boundaries, all selected dynamic identities, channel/precision grammar coverage, malformed responses, timeout/cancellation, output serialization, committed-write non-cancellability, query isolation, redirected-output rejection, reset/restoration distinction, lifecycle recovery for any T136 ownership, composition, package consumers, and real `Icod.DCurses` typed observation consumption.

---

## 6. Explicit non-goals

0.13 does not add arbitrary OSC construction, raw public color strings, X11 named-color injection, Tektronix dynamic colors, emulator detection as a support oracle, automatic palette probing, authoritative long-lived color caching, `System.Drawing` dependency, or downstream color-selection/contrast/theme policy.

---

## 7. Current development state

```text
VersionPrefix:   0.13.0
VersionSuffix:   alpha.5
Version:         0.13.0-alpha.5
PackageVersion:  0.13.0-alpha.5
AssemblyVersion: 0.13.0.0
```

**Next after green validation:** T135 — activate the extended dynamic-color identities on the existing typed API.
