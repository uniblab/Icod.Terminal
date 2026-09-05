# Icod.Terminal 0.13.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.13.0`  
**Development version:** `0.13.0-alpha.1`  
**Predecessor:** `0.12.0` — OSC 133 semantic prompt integration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** observable terminal palette and dynamic-color control  
**Status:** Roadmap established; T130 contract/reference freeze next

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

OSC 4 addresses indexed terminal palette entries. 0.13 SHALL support the protocol's useful typed operations without exposing arbitrary OSC construction.

The contract freeze SHALL decide:

- the public palette-index type/domain;
- single-entry and protocol-supported multi-entry mutation;
- single-entry and protocol-supported multi-entry observation;
- single/multiple/all reset behavior under OSC 104;
- response correlation and ordering for multi-entry queries;
- behavior when only a subset of requested entries replies;
- finite query deadlines and caller cancellation.

### 2.2 Dynamic colors

0.13 SHALL expose typed semantic identities for the selected dynamic colors rather than making callers provide OSC numbers.

The planned semantic set is:

```text
DefaultForeground
DefaultBackground
TextCursor
MouseForeground
MouseBackground
HighlightBackground
HighlightForeground
```

The exact public type/name is a T130 decision.

### 2.3 Deliberately excluded Tektronix colors

The xterm dynamic-color range also contains Tektronix-specific entries:

- OSC 15 / 115;
- OSC 16 / 116;
- OSC 18 / 118.

These SHALL remain outside 0.13 unless T130 finds a compelling cross-terminal reason to promote them. `Icod.Terminal` does not otherwise provide a Tektronix terminal model, and including these merely to make the numeric range contiguous would weaken the semantic API.

---

## 3. Observation is part of the architecture

Color observation SHALL use the existing session-owned active-query transaction and input-response routing architecture wherever the protocol permits a query.

0.13 SHALL NOT create:

- a second public response reader;
- a second input transport;
- background terminal interrogation during `TerminalSession.OpenAsync`;
- automatic support probing;
- a cache presented as though it were authoritative terminal state.

An observation result SHALL represent what the terminal explicitly reported during that query transaction. Timeout SHALL remain distinct from an explicit unsupported/negative result when the protocol provides such a distinction.

The public observation model SHALL preserve enough precision that a later `Icod.DCurses` consumer does not have to reverse-engineer raw terminal strings or lose terminal-reported color information.

---

## 4. Terminal color value model

T130 SHALL freeze a semantic numeric color representation before OSC encoders are implemented.

The leading candidate is an immutable RGB value with 16-bit channels:

```csharp
public readonly struct TerminalColor {
	public ushort Red { get; }
	public ushort Green { get; }
	public ushort Blue { get; }
}
```

This is a design direction, not yet a frozen public API.

The contract review SHALL determine:

- whether 16-bit channels are the canonical storage precision;
- equality and value semantics;
- convenient 8-bit construction/conversion without making 8-bit the observation ceiling;
- canonical outbound color encoding;
- normalization of lower-precision terminal replies into the public value;
- whether original reply precision needs to be represented separately;
- strict parsing rules for supported `rgb:` and `#...` response grammars;
- rejection of malformed, overflowing, truncated, or unsupported color specifications;
- whether named X11 colors belong anywhere in the public API (default expectation: no).

No public API SHALL require callers to construct an XParseColor string.

---

## 5. Portability tiers

The release may contain the full requested family while documenting unequal portability.

### 5.1 Common/core tier

The expected common tier is:

```text
OSC 4 / 104
OSC 10 / 110
OSC 11 / 111
OSC 12 / 112
```

### 5.2 Extended xterm dynamic-color tier

The expected extended tier is:

```text
OSC 13 / 113
OSC 14 / 114
OSC 17 / 117
OSC 19 / 119
```

T130 SHALL validate the exact interoperability posture against primary terminal documentation before these labels are frozen.

The public API SHALL not lie about support. Successful output proves protocol emission, not terminal recognition or visual application. A successful explicit query proves only that a conforming reply was received and parsed for that transaction.

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

Because palette and dynamic colors are observable on terminals that implement the corresponding queries, 0.13 SHOULD investigate truthful query-before-mutate scoped restoration.

This is not yet promised as a public API.

T136 SHALL decide separately for indexed palette and dynamic colors whether a lease can guarantee a truthful restoration baseline. The design SHALL account for:

- observation failure before mutation;
- nested ownership;
- out-of-order disposal where identity-aware ownership is appropriate;
- external terminal changes while a lease is active;
- failed mutation after successful baseline observation;
- failed restoration and retryable cleanup ownership;
- suspend/resume;
- `TerminalSession.InvalidateState()`;
- session disposal;
- the difference between restoring an observed color and issuing a terminal-policy reset.

The library SHALL NOT claim exact restoration when it did not successfully observe the state being restored.

---

## 8. Proposed tranche sequence

### T130 — color contract and reference freeze

Freeze before implementation:

- authoritative protocol references;
- OSC 4/104 grammar and query/reset semantics;
- selected OSC 10–19 dynamic-color semantics and reset mappings;
- explicit exclusion of OSC 15/16/18 and 115/116/118;
- `TerminalColor` representation and precision;
- palette-index model;
- dynamic-color semantic identity model;
- canonical outbound color grammar;
- accepted inbound color grammars and normalization;
- query response correlation;
- portability tiers;
- timeout/cancellation/error semantics;
- support posture;
- restoration feasibility and information required by future leases;
- future `Icod.DCurses` observation-consumer requirements.

Expected development version: `0.13.0-alpha.1`.

### T131 — color codec and parser foundation

Implement and test:

- semantic color value type;
- canonical outbound encoding;
- strict inbound parser;
- precision normalization;
- malformed/overflow/truncation rejection;
- exhaustive/boundary tests independent of session logic.

Expected development version: `0.13.0-alpha.2`.

### T132 — OSC 4 indexed palette mutation and observation

Implement:

- typed palette set;
- typed palette query;
- protocol-supported multi-entry operations where they remain deterministic and bounded;
- active-query routing/correlation;
- byte-exact and response-parser integration tests.

Expected development version: `0.13.0-alpha.3`.

### T133 — OSC 104 indexed palette reset

Implement:

- reset one entry;
- reset multiple entries where supported by the frozen grammar;
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

Document these as an extended interoperability tier if T130 confirms that posture.

Expected development version: `0.13.0-alpha.6`.

### T136 — truthful scoped color ownership

Determine and, where contractually sound, implement scoped restoration for palette and/or dynamic colors using explicit observation baselines.

If truthful scoped restoration cannot be guaranteed for a category, document that result rather than introducing a reset masquerading as restoration.

Expected development version: `0.13.0-alpha.7`.

### T137 — lifecycle, composition, and downstream acceptance

Prove composition with:

- ordinary text;
- presentation state;
- rich-input protocol leases;
- cursor style;
- synchronized output;
- progress;
- pointer shape;
- OSC 7 location;
- OSC 8 hyperlinks;
- OSC 52 clipboard;
- OSC 133 semantic prompt markers;
- active terminal queries.

Add real downstream `Icod.DCurses` acceptance for the observation API. The acceptance SHALL demonstrate that `Icod.DCurses` can consume typed observed color values without parsing raw OSC or bypassing `TerminalSession`.

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

**Next:** T130 — authoritative color protocol/reference freeze and public semantic contract design.
