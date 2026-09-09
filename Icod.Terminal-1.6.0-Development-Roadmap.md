# Icod.Terminal 1.6.0 Development Roadmap

**Release:** `1.6.0`  
**Theme:** complete CSI grammar, CSI consolidation, and terminal pixel/cell geometry  
**Status:** C160–C164 complete; C165 release closure in progress  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.5.0`

## Why this release exists

`1.5.0` normalized terminal control-language framing, structural control frames, multi-family query transactions, capability evidence, semantic backend registration, and deterministic routing. It deliberately stopped short of broad CSI implementation.

Version `1.6.0` is the first protocol-family tranche built on that normalized core. Its purpose is to make CSI one complete shared grammar rather than a collection of operation-specific numeric parsers and byte literals, migrate the existing CSI users onto that grammar, and establish the pixel/cell observations needed by later graphics work.

The release preserves every existing stable 1.x wire-specific public contract. It does not introduce a generic raw public CSI writer or a premature public graphics/geometry API.

## Release invariants

1. Existing public 1.x CSI-based APIs retain their released wire bytes and semantics.
2. CSI grammar and semantic interpretation remain separate layers.
3. Parameter bytes, intermediate bytes, and the final byte are represented structurally.
4. Private parameter bytes and colon subparameters are never discarded merely because an existing query does not use them.
5. Omitted, empty, zero, and explicit numeric parameters remain distinguishable where the dialect requires that distinction.
6. One live `TerminalSession` remains the authoritative input reader.
7. CSI parser state, parameter cardinality, numeric conversion, response framing, and query ownership remain bounded.
8. Seven-bit and supported eight-bit CSI forms share one grammar.
9. Terminal identity or branding is capability evidence only when explicitly modeled; it is not a CSI support oracle.
10. Timeout remains an unanswered query rather than automatic unsupported proof.

## Accepted checkpoints

| Tranche | Exact head | Staging workflow |
| --- | --- | --- |
| C160 | `7c54169b879a75389475570f8460fbdc8b8c7535` | `34383863267` |
| C161 | `c5f0cd8345e6c2fea10fdb15d4ec8439443d9083` | `34385272523` |
| C162 | `fa7aeacb9f8ff6d2b719c81cba847ccda6d3f19f` | `34387272361` |
| C163 | `c191d629e1d6347df6b6af182a9ffcdbbbfce7a5` | `34389924515` |
| C164 | `3f5e1eccf2f655f25faf665c25262ad7a31df999` | `34393525555` |

Each accepted workflow passed Windows, Linux, macOS runtime validation, package candidate, all four package-contract shards, and the validated package artifact.

## C160 — complete CSI grammar foundation

**Status:** Complete.

**Goal:** formalize one reusable internal CSI syntax model over the normalized structural frame representation.

Completed work:

- defines the typed internal `TerminalCsiSyntax` view;
- retains all parameter bytes in `0x30`–`0x3F`;
- retains all intermediate bytes in `0x20`–`0x2F`;
- retains exactly one final byte in `0x40`–`0x7E`;
- distinguishes leading private parameter bytes from ordinary parameter/subparameter data without losing raw bytes;
- parses semicolon-separated parameters and colon-separated subparameters structurally;
- preserves omitted/empty components rather than coercing them prematurely to zero;
- enforces bounded raw parameter bytes, parameter count, and subparameter count;
- supports seven-bit and eight-bit CSI through the common scanner/structure path;
- adds focused grammar tests without adding public API.

Permanent contract: `docs/C160-Complete-CSI-Grammar-Foundation.md`.

## C161 — typed CSI parameter semantics

**Status:** Complete.

**Goal:** provide reusable bounded conversion helpers for dialects which need numeric or enum-like CSI parameters.

Completed work:

- typed access to parameter/subparameter components;
- explicit `Omitted`, `Empty`, and `Numeric` distinctions, including numeric zero;
- overflow-safe ASCII-decimal conversion with reviewed bounds;
- helpers for dialects that forbid private prefixes, intermediates, empty parameters, or colon subparameters;
- deterministic `FormatException` behavior for malformed correlated responses;
- migration of `TerminalCsiQueryProtocol` numeric parsing to the shared C160/C161 stack;
- preservation of historical DA/DSR/CPR parameter-count and numeric-value ceilings;
- regressions proving existing DA/DSR/CPR empty/subparameter rejection behavior remains unchanged.

Permanent contract: `docs/C161-Typed-CSI-Parameter-Semantics.md`.

## C162 — existing CSI consolidation

**Status:** Complete.

**Goal:** remove operation-specific CSI grammar/construction duplication while preserving released bytes.

Completed migration set:

- Primary and Secondary Device Attributes request construction;
- Device Status Report request construction;
- Cursor Position Report request construction;
- DA/DSR/CPR response matching through `TerminalCsiSyntax`;
- DEC private-mode set/reset construction;
- synchronized output mode 2026;
- Kitty progressive-keyboard flags query;
- Kitty progressive-keyboard push/pop construction;
- compound Kitty keyboard query + Primary DA barrier construction;
- mouse tracking private modes 1000, 1002, and 1003;
- SGR mouse encoding private mode 1006;
- existing DECSCUSR cursor-style emission remains on `CsiWriter`.

### TermInfo boundary retained

Bracketed-paste and focus enable/disable output is deliberately not rebuilt from assumed DEC private-mode numbers. The selected terminal's exact extended capabilities remain authoritative:

```text
BracketedPaste   BE / BD
FocusReporting   fe / fd
```

Their input markers remain `PS`/`PE` and `kxIN`/`kxOUT`. `XM`, `xm`, and `kmous` continue to determine whether a supported mouse protocol is advertised before canonical tracking modes are emitted.

Permanent contract: `docs/C162-Existing-CSI-Consolidation.md`.

## C163 — terminal and cell pixel geometry

**Status:** Complete.

**Goal:** establish the observations later raster backends need without coupling graphics codecs to platform-specific console APIs.

C163 distinguishes:

```text
character grid        columns x rows
terminal pixels       width x height
character-cell pixels width x height
```

The pixel geometry substrate remains internal in 1.6.

Active-query evidence:

```text
CSI 14 t  -> CSI 4 ; height ; width t
CSI 16 t  -> CSI 6 ; height ; width t
```

The implementation:

- correlates selector 4 separately from selector 6;
- supports seven-bit and eight-bit CSI responses through the C160/C161 stack;
- requires positive dimensions bounded to `1,000,000`;
- routes both queries through the existing transaction manager and authoritative session reader;
- provides exact-only cell-pixel derivation from character-grid and terminal-pixel observations;
- never rounds or truncates fractional geometry.

The POSIX audit confirmed `TIOCGWINSZ` already provides pixel width/height, but the existing `ITerminalControlProvider.GetSize(...)` contract exposes only `TerminalSize(columns, rows)`. Version 1.6 records that native evidence without expanding the stable provider/public contract merely to surface optional pixel fields.

Permanent contract: `docs/C163-Terminal-and-Cell-Pixel-Geometry.md`.

## C164 — CSI hardening, fragmentation, and recovery

**Status:** Complete.

**Goal:** qualify the complete grammar and query substrate against hostile and highly fragmented terminal input.

Completed coverage includes:

- exact maximum raw CSI parameter bytes;
- exact maximum parameter count;
- exact maximum subparameter count;
- numeric maximum versus maximum-plus-one;
- mixed private prefixes, semicolons, colons, and empty components;
- CAN/SUB invalidation;
- every two-chunk split point of a real `CSI 4;800;1200t` response through `TerminalSession`;
- one-reader ownership throughout fragmented queries;
- malformed correlated geometry response followed by successful subsequent query;
- oversized correlated geometry response followed by bounded drain/resynchronization and a successful later geometry query.

### Timeout ownership correction

C164 validation exposed a scheduler race in the existing late-response transaction test on macOS/net9. Production timeout ownership had been anchored to the instant the timeout continuation happened to execute rather than to the logical caller deadline.

The corrected transaction model records the monotonic timeout start plus duration and computes elapsed ownership from the logical deadline using `IMonotonicClock.GetElapsedTime(...)`. No provider-specific timestamp arithmetic is introduced. Explicit cancellation/suspension/disposal interruptions retain their actual interruption timestamp semantics.

This removes scheduler-dependent ownership extension without lengthening any timeout.

### Oversized geometry correlation

Geometry matchers now implement `ICorrelatedTerminalResponseMatcher` for their selector-specific seven-bit/eight-bit CSI prefixes. A correlated geometry response that reaches the 4,096-byte CSI framing ceiling is transaction-owned, fails deterministically, drains through the final byte, and leaves the same session able to execute the next query.

Permanent contract: `docs/C164-CSI-Hardening-Fragmentation-and-Recovery.md`.

## C165 — acceptance, package, and documentation closure

**Status:** In progress.

**Goal:** qualify `1.6.0` as the CSI foundation for the later DCS/Sixel and APC/Kitty Graphics releases.

Required closure evidence:

- Windows/Linux/macOS Staging runtime/source validation;
- `net8.0`, `net9.0`, and `net10.0` package/public consistency;
- retained 1.0–1.5 public compatibility;
- unchanged authoritative public-API baseline;
- byte-exact regression coverage for migrated CSI public APIs;
- complete C160/C161 grammar and semantic tests;
- C163 geometry-query tests;
- C164 boundary/fragmentation/recovery coverage;
- retained `Icod.DCurses` compatibility witness;
- README, changelog, compatibility/security review, package metadata, release notes, roadmaps, and PR summary synchronized;
- exact documentation/package-complete PR head passes the complete Staging matrix.

Permanent closure record: `docs/C165-1.6.0-Acceptance-Package-and-Documentation-Closure.md`.

## Public API strategy

C160–C164 remain internal and add no public API. A public semantic geometry surface is intentionally deferred until later graphics integration demonstrates the correct stable shape.

A generic `WriteCsiAsync(...)`, arbitrary final-byte dispatcher, or raw parameter/intermediate writer is outside the 1.6 public API plan.

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

## Release rule

C165 completion and a green PR are necessary but not sufficient to publish `1.6.0`.

After merge, the resulting `main` head must pass Release distribution validation. Tagging and publishing `v1.6.0` remain separate explicit actions after that post-merge validation succeeds.
