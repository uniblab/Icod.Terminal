# Icod.Terminal 0.13.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.13.0`  
**Development version:** `0.13.0-alpha.7`  
**Predecessor:** `0.12.0` — OSC 133 semantic prompt integration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** observable terminal palette and dynamic-color control  
**Status:** T130–T135 complete/green; T136 lifecycle/ownership decision implemented; T137 next after validation

---

## 1. Release objective

`Icod.Terminal 0.13.0` adds typed mutation, reset, and observation for:

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

The release uses:

- immutable 16-bit RGB `TerminalColor`;
- byte palette indices;
- closed public `TerminalDynamicColor` with seven selected non-Tektronix identities;
- canonical `rgb:rrrr/gggg/bbbb` output;
- strict bounded `rgb:` and selected hash reply parsing;
- existing active-query transaction/router reuse;
- distinct timeout/cancellation/format failures;
- no automatic probing or authoritative color cache;
- terminal-policy reset kept distinct from exact restoration;
- no `System.Drawing` dependency;
- future `Icod.DCurses` typed observation consumption.

T136 additionally freezes all 0.13 color mutation as **unscoped**. The session does not automatically capture, replay, or restore color state across invalidation, suspend/resume, or disposal.

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

Portability tiers are documentation guidance only; they do not trigger terminal detection or hidden suppression.

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

Record: `docs/T131-Terminal-Color-Codec-and-Parser-Foundation.md`.

### T132 — OSC 4 indexed palette mutation/observation

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.3`.  
**Validation:** workflow #546.

Record: `docs/T132-OSC-4-Indexed-Palette-Mutation-and-Observation.md`.

### T133 — OSC 104 indexed palette reset

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.4`.  
**Validation:** workflow #553.

Record: `docs/T133-OSC-104-Indexed-Palette-Reset.md`.

### T134 — common dynamic colors

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.5`.  
**Validation:** workflow #560.

Delivered the unified public dynamic-color API and activated OSC 10/11/12 with resets 110/111/112.

Record: `docs/T134-Common-Dynamic-Color-Mutation-Observation-and-Reset.md`.

### T135 — extended dynamic colors

**Status:** Complete and green.  
**Version:** `0.13.0-alpha.6`.  
**Validation:** workflow #566.

Activated the same public API for:

- `MouseForeground` — OSC 13 / 113;
- `MouseBackground` — OSC 14 / 114;
- `HighlightBackground` — OSC 17 / 117;
- `HighlightForeground` — OSC 19 / 119.

Record: `docs/T135-Extended-Dynamic-Color-Mutation-Observation-and-Reset.md`.

### T136 — scoped color ownership feasibility and lifecycle decision

**Status:** Implemented; exact-head validation pending.  
**Version:** `0.13.0-alpha.7`.

Decision:

- no public palette-color lease in 0.13;
- no public dynamic-color lease in 0.13;
- existing color mutation remains explicitly unscoped;
- OSC 104/110–119 reset is never presented as exact restoration;
- a stale pre-suspend baseline is not considered authoritative after resume;
- lifecycle-safe ownership would require post-resume re-observation before reapplying owned state;
- current lifecycle ordering deliberately re-enables active queries only after lifecycle participants resume;
- changing that core ordering merely to enable color leases is deferred to a future lifecycle-architecture tranche/release.

T136 adds lifecycle tests proving unscoped color mutation is not reset/replayed by `InvalidateState()`, suspend/resume, or disposal.

Record: `docs/T136-Scoped-Color-Ownership-Feasibility-and-Lifecycle-Decision.md`.

### T137 — lifecycle, composition, and downstream acceptance

**Status:** Next after T136 validation.  
**Expected version:** `0.13.0-alpha.8`.

Prove composition with ordinary application output, presentation/input-protocol ownership, cursor style, synchronized output, progress, pointer shape, OSC 7/8/52/133, and active terminal queries.

Add real downstream `Icod.DCurses` acceptance consuming typed palette/dynamic-color observations without raw OSC parsing or a second input path.

### T138 — public API/docs/samples/package/stable closure

**Expected stable version:** `0.13.0`.

Deliver the 0.13 public API baseline, README/sample updates, release metadata, XML API assertions, fresh NuGet-only consumers, retained historical gates, new color gates, and exact stable PR/main/tag validation.

---

## 5. Testing expectations

0.13 testing includes byte-exact framing, palette boundaries, all selected dynamic identities, channel/precision grammar coverage, malformed responses, timeout/cancellation, output serialization, committed-write non-cancellability, query isolation, redirected-output rejection, reset/restoration distinction, explicit unscoped lifecycle semantics, composition, package consumers, and real `Icod.DCurses` typed observation consumption.

---

## 6. Explicit non-goals

0.13 does not add arbitrary OSC construction, raw public color strings, X11 named-color injection, Tektronix dynamic colors, emulator detection as a support oracle, automatic palette probing, authoritative long-lived color caching, `System.Drawing` dependency, downstream color-selection/contrast/theme policy, or lifecycle-safe color leases.

---

## 7. Current development state

```text
VersionPrefix:   0.13.0
VersionSuffix:   alpha.7
Version:         0.13.0-alpha.7
PackageVersion:  0.13.0-alpha.7
AssemblyVersion: 0.13.0.0
```

**Next after green validation:** T137 — lifecycle/composition and downstream `Icod.DCurses` observation acceptance.
