# Icod.Terminal 1.6.0 Development Roadmap

**Release:** `1.6.0`  
**Theme:** complete CSI grammar, CSI consolidation, and terminal pixel/cell geometry  
**Status:** C160–C162 complete; C163 implemented and validating  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.5.0`

## Why this release exists

`1.5.0` normalized terminal control-language framing, structural control frames, multi-family query transactions, capability evidence, semantic backend registration, and deterministic routing. It deliberately stopped short of broad CSI implementation.

Version `1.6.0` is the first protocol-family tranche built on that normalized core. Its purpose is to make CSI one complete shared grammar rather than a collection of operation-specific numeric parsers and byte literals, then use that grammar for existing CSI queries/modes and the pixel/cell geometry observations needed by later graphics work.

The release must preserve every existing stable 1.x wire-specific public contract. It does not introduce a generic raw public CSI writer.

## Release invariants

1. Existing public 1.x CSI-based APIs retain their released wire bytes and semantics.
2. CSI grammar and semantic interpretation remain separate layers.
3. Parameter bytes, intermediate bytes, and the final byte are always represented structurally.
4. Private parameter bytes and colon subparameters are never discarded merely because an existing query does not use them.
5. Omitted, empty, zero, and explicit numeric parameters remain distinguishable where the dialect requires that distinction.
6. One live `TerminalSession` remains the authoritative input reader.
7. All CSI parser state, parameter cardinality, numeric conversion, and query response sizes remain bounded.
8. 7-bit and supported 8-bit CSI forms share one grammar.
9. Terminal identity or branding remains capability evidence only when explicitly modeled; it is not a CSI support oracle.
10. Pixel/cell geometry observations must distinguish unavailable, unknown, malformed, and successfully observed state truthfully.

## C160 — complete CSI grammar foundation

**Status:** Complete.

Accepted checkpoint:

```text
7c54169b879a75389475570f8460fbdc8b8c7535
```

Staging workflow:

```text
34383863267
```

**Goal:** formalize one reusable internal CSI syntax model over the N153 structural frame representation.

### Completed work

- defines the typed internal `TerminalCsiSyntax` view over `TerminalControlFrameStructure`;
- retains all parameter bytes in the ECMA-48 `0x30`–`0x3F` range;
- retains all intermediate bytes in `0x20`–`0x2F`;
- retains exactly one final byte in `0x40`–`0x7E`;
- distinguishes leading private parameter bytes from ordinary parameter/subparameter data without losing the original raw bytes;
- parses semicolon-separated parameters and colon-separated subparameters structurally;
- preserves omitted/empty components rather than coercing them prematurely to zero;
- enforces bounded raw parameter bytes, parameter count, and subparameter count;
- supports both 7-bit `ESC [` and 8-bit CSI framing through the common N151/N152 scanner;
- adds focused grammar tests before existing query/mode consumers migrate.

### Non-goals retained

- no generic public CSI encoder/writer;
- no broad semantic interpretation of every possible CSI final byte;
- no public API change required by C160.

Permanent contract: `docs/C160-Complete-CSI-Grammar-Foundation.md`.

## C161 — typed CSI parameter semantics

**Status:** Complete.

Accepted checkpoint:

```text
c5f0cd8345e6c2fea10fdb15d4ec8439443d9083
```

Staging workflow:

```text
34385272523
```

**Goal:** provide reusable bounded conversion helpers for dialects which need numeric or enum-like CSI parameters.

### Completed work

- adds typed access to parameter/subparameter components;
- explicitly distinguishes `Omitted`, `Empty`, and `Numeric`, including explicit numeric zero;
- performs overflow-safe ASCII-decimal conversion with reviewed bounds;
- adds helpers for dialects that forbid private prefixes, intermediates, empty parameters, or colon subparameters;
- retains deterministic `FormatException` behavior for malformed correlated responses;
- migrates `TerminalCsiQueryProtocol` numeric parsing to the shared C160/C161 stack;
- preserves the historical DA/DSR/CPR parameter-count and numeric-value ceilings;
- adds regressions proving the existing DA/DSR/CPR empty/subparameter rejection behavior remains unchanged.

Permanent contract: `docs/C161-Typed-CSI-Parameter-Semantics.md`.

## C162 — existing CSI consolidation

**Status:** Complete.

Accepted checkpoint:

```text
fa7aeacb9f8ff6d2b719c81cba847ccda6d3f19f
```

Staging workflow:

```text
34387272361
```

**Goal:** remove operation-specific CSI grammar/construction duplication while preserving released bytes.

### Completed migration set

- Primary and Secondary Device Attributes request construction;
- Device Status Report request construction;
- Cursor Position Report request construction;
- DA/DSR/CPR response matching through `TerminalCsiSyntax`;
- DEC private-mode set/reset construction;
- synchronized output mode 2026 through the canonical DEC-private-mode helper;
- Kitty progressive-keyboard flags query;
- Kitty progressive-keyboard push/pop construction;
- compound Kitty keyboard query + Primary DA barrier construction;
- mouse tracking private modes 1000, 1002, and 1003;
- SGR mouse encoding private mode 1006;
- existing DECSCUSR cursor-style emission remains on `CsiWriter`.

### TermInfo boundary retained

Bracketed-paste and focus enable/disable output is deliberately **not** rebuilt from assumed DEC private-mode numbers. The selected terminal's exact extended capabilities remain authoritative:

```text
BracketedPaste   BE / BD
FocusReporting   fe / fd
```

Their input markers remain `PS`/`PE` and `kxIN`/`kxOUT` respectively.

Likewise `XM`, `xm`, and `kmous` continue to decide whether a supported mouse protocol is advertised before the manager emits canonical hard-coded tracking modes.

### Acceptance

- existing public methods remain byte-exact;
- query matchers use shared structural/semantic CSI parsing rather than ad-hoc slicing;
- malformed-but-correlated query responses remain owned by the query parser rather than leaking into application input;
- existing input-protocol lease tests retain exact mouse/Kitty transition ordering, rollback, and lifecycle restoration behavior;
- no duplicate competing CSI scanner is introduced;
- Windows/Linux/macOS runtime, all four package shards, and the validated package artifact passed on the accepted exact head.

Permanent contract: `docs/C162-Existing-CSI-Consolidation.md`.

## C163 — terminal and cell pixel geometry

**Status:** Implemented; exact-head Staging validation pending.

**Goal:** establish the observations later raster backends need without coupling graphics codecs to platform-specific console APIs.

### Implemented geometry model

C163 distinguishes:

```text
character grid        columns x rows
terminal pixels       width x height
character-cell pixels width x height
```

The new pixel geometry substrate remains internal in 1.6 while later graphics tranches exercise it.

### Implemented active-query evidence

- `CSI 14 t` requests terminal-window pixel dimensions;
- correlated `CSI 4 ; height ; width t` responses become `TerminalPixelSize(width, height)`;
- `CSI 16 t` requests character-cell pixel dimensions;
- correlated `CSI 6 ; height ; width t` responses become `TerminalPixelSize(width, height)`;
- response matchers distinguish selector `4` from selector `6` so a valid stale response for one query does not satisfy the other;
- malformed but plausibly correlated geometry responses remain owned by the query parser and fail deterministically;
- both seven-bit and eight-bit CSI response forms use the C160/C161 grammar stack;
- reported dimensions are positive and bounded to `1,000,000`.

### Authoritative session path

Internal `TerminalSession` geometry queries execute through the existing query transaction manager. Tests cover exact `14t`/`16t` emission and preserve one-reader ownership.

Timeout remains an unanswered query rather than unsupported proof.

### Native evidence audit

POSIX `TIOCGWINSZ` already provides `Rows`, `Columns`, `PixelWidth`, and `PixelHeight`. The existing `ITerminalControlProvider.GetSize(...)` contract exposes only `TerminalSize(columns, rows)`, so the pixel fields are currently discarded by the public/native path.

C163 records that fact but does not add a mandatory provider member or public geometry aggregate merely to surface optional native pixel fields. The portable active-query path remains the qualified pixel observation mechanism for 1.6.

### Exact derivation

`TerminalPixelGeometry.TryDeriveCellPixelSize(...)` derives a cell size from character-grid and terminal-pixel observations only when width and height divide exactly. It never rounds or truncates fractional geometry.

### Public API decision

C163 adds no public API. This avoids freezing a provider or aggregate geometry contract before the DCS/Sixel and APC/Kitty Graphics tranches exercise the internal substrate.

Permanent contract: `docs/C163-Terminal-and-Cell-Pixel-Geometry.md`.

## C164 — CSI hardening, fragmentation, and fuzz/property coverage

**Status:** Not started.

**Goal:** qualify the complete grammar against hostile and highly fragmented terminal input.

### Required cases

- every split point of representative CSI frames;
- 7-bit and 8-bit introducers;
- maximum raw parameter bytes;
- maximum parameter and subparameter cardinality;
- huge numeric values and overflow attempts;
- private prefixes;
- intermediate bytes;
- semicolon and colon mixtures;
- omitted and empty components;
- malformed final bytes;
- unexpected controls;
- unrelated CSI frames while a query is active;
- bounded fallback/resynchronization after malformed or oversized input.

## C165 — acceptance, package, and documentation closure

**Status:** Not started.

**Goal:** qualify `1.6.0` as the CSI foundation for the later DCS/Sixel and APC/Kitty Graphics releases.

### Required evidence

- Windows/Linux/macOS Staging runtime/source validation;
- Release validation after merge;
- `net8.0`, `net9.0`, and `net10.0` public/package consistency;
- retained 1.0–1.5 public compatibility;
- current public-API baseline review;
- byte-exact regression coverage for migrated CSI public APIs;
- complete C160/C161 grammar tests;
- C163 geometry-query tests;
- C164 hardening/property coverage;
- retained `Icod.DCurses` compatibility witness;
- README, changelog, compatibility/security docs, package metadata, release notes, and roadmaps synchronized.

## Public API strategy

C160–C163 remain internal. A public semantic geometry surface is intentionally deferred until later graphics integration demonstrates the correct stable shape.

A generic `WriteCsiAsync(...)`, arbitrary final-byte dispatcher, or raw parameter/intermediate writer is explicitly outside the 1.6 public API plan.

## Relationship to later releases

```text
1.5.0  normalized family framing / evidence / routing
    |
1.6.0  complete CSI grammar / consolidation / geometry
    |
1.7.0  DCS foundation / Sixel / common raster model
    |
1.8.0  APC foundation / Kitty Graphics / graphics routing
```
