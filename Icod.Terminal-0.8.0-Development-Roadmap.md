# Icod.Terminal 0.8.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Current version:** `0.8.0`  
**Predecessor:** `0.7.0` — OSC 52 clipboard/selection operations  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** typed cursor-style control, CSI intermediate-byte output, DECRQSS cursor-state interpretation, and truthful restoration  
**Successor:** `0.9.0` — synchronized output and nested transactional output state

---

## 1. Release objective

`Icod.Terminal 0.8.0` adds semantic cursor-style control on top of the live-session, output-serialization, query-routing, and lifecycle foundations established by `0.1.0` through `0.7.0`.

The primary wire operation is:

```text
CSI Ps SP q
```

Public callers work with typed semantic cursor styles rather than raw numeric parameters or arbitrary CSI construction. Cursor style and cursor visibility remain independent terminal states.

---

## 2. Frozen protocol posture

The stable semantic set is:

```text
BlinkingBlock     -> 1
SteadyBlock       -> 2
BlinkingUnderline -> 3
SteadyUnderline   -> 4
BlinkingBar       -> 5
SteadyBar         -> 6
```

Outbound output uses canonical seven-bit CSI. Inbound omitted/`0`/`1` normalize to `BlinkingBlock`; recognized values `2` through `6` map directly. xterm parameter `7` is not a generic semantic style or restoration primitive.

Bar styles are retained as xterm-compatible extensions without fabricating proof of terminal support.

---

## 3. Stable public API

The reviewed 0.8 public delta is frozen in [`docs/Public-API-Baseline-0.8.md`](docs/Public-API-Baseline-0.8.md):

```text
TerminalCursorStyle
TerminalCursorStyleObservation
TerminalCursorStyleLease
TerminalSession.SetCursorStyleAsync(...)
TerminalSession.QueryCursorStyleAsync(...)
TerminalSession.AcquireCursorStyleAsync(...)
```

No generic public CSI writer, raw DECSCUSR parameter setter, fake default/reset style, automatic support probe, or cached emulator capability flag is part of 0.8.

---

## 4. Tranche status

The 0.8 release uses tranche numbers `T80` through `T87`, aligned with the release line.

### T80 — cursor-style contract and reference freeze — complete

Frozen numeric mapping, DEC/xterm boundary, query semantics, parameter-zero policy, restoration truthfulness, and cursor-style/visibility independence.

Record: [`docs/T80-Cursor-Style-Contract-and-Reference-Freeze.md`](docs/T80-Cursor-Style-Contract-and-Reference-Freeze.md)

### T81 — reusable CSI intermediate-byte output primitive — complete

Implemented structural CSI parameter/intermediate/final-byte output, canonical seven-bit framing, and byte-exact DECSCUSR emission for parameters `1`–`6` without a public raw CSI escape hatch.

Record: [`docs/T81-CSI-Intermediate-Byte-Output-Primitive.md`](docs/T81-CSI-Intermediate-Byte-Output-Primitive.md)

Validation: workflow #246 green.

### T82 — typed cursor-style codec and DECRQSS interpretation — complete

Implemented the six-value `TerminalCursorStyle` model, deterministic outbound mapping, strict inbound status parsing, omitted/`0`/`1` normalization, and rejection of malformed/unknown positive state.

Record: [`docs/T82-Typed-Cursor-Style-Codec-and-DECRQSS-Interpretation.md`](docs/T82-Typed-Cursor-Style-Codec-and-DECRQSS-Interpretation.md)

Validation: workflow #250 green.

### T83 — semantic cursor-style set API — complete

Implemented public `SetCursorStyleAsync(...)` with semantic validation, redirected-output rejection, shared output serialization, cancellation-before-commit, non-truncating committed frames, and no implicit flush.

Record: [`docs/T83-Semantic-Cursor-Style-Set-API.md`](docs/T83-Semantic-Cursor-Style-Set-API.md)

Validation: workflow #260 green with T84.

### T84 — typed cursor-style query/observation API — complete

Implemented `TerminalCursorStyleObservation` and `QueryCursorStyleAsync(...)` on the existing DECRQSS `SP q` transaction path. Unsupported, timeout, cancellation, and malformed positive state remain distinct outcomes.

Record: [`docs/T84-Typed-Cursor-Style-Query-and-Observation.md`](docs/T84-Typed-Cursor-Style-Query-and-Observation.md)

Validation: workflow #260 green with T83.

### T85 — truthful restoration and scoped state — complete

Accepted `TerminalCursorStyleLease` and `AcquireCursorStyleAsync(...)` because the outermost acquisition first observes an authoritative semantic baseline. Nested leases are strict LIFO. Suspend restores the observed baseline; resume reapplies the active innermost logical style; disposal performs final best-effort restoration. No guessed reset participates.

Record: [`docs/T85-Cursor-Style-Restoration-and-Scoped-State.md`](docs/T85-Cursor-Style-Restoration-and-Scoped-State.md)

Validation: workflow #270 green.

### T86 — integration, compatibility, and regression acceptance — complete

Added acceptance coverage for cursor-style/visibility independence, deterministic ordering with text and OSC 0/1/2/7/8/52, lifecycle baseline restoration/re-entry, and release-while-suspended behavior. Existing T82/T84 query tests remain authoritative for malformed responses, cancellation, timeout, late-response ownership, and single-reader behavior.

Record: [`docs/T86-Cursor-Style-Integration-Compatibility-and-Regression-Acceptance.md`](docs/T86-Cursor-Style-Integration-Compatibility-and-Regression-Acceptance.md)

Validation: workflow #275 green.

### T87 — public API, docs, sample, package, and stable closure — implemented

Implemented:

- frozen `docs/Public-API-Baseline-0.8.md`;
- stable `0.8.0` repository/package metadata;
- root README 0.8 documentation;
- focused `samples/Icod.Terminal.CursorStyle.Sample` project;
- explicit sample compilation in PR/distribution/release validation;
- package XML documentation assertions for the complete 0.8 public delta;
- fresh package-only cursor-style consumer under `tools/package-cursor-style-smoke`;
- fresh package consumer runs on `net8.0`, `net9.0`, and `net10.0`;
- PR, `main` distribution, and tagged-release integration of the 0.8 package gate;
- stable closure record and release-order gate.

Record: [`docs/T87-0.8.0-Public-API-Package-and-Stable-Closure.md`](docs/T87-0.8.0-Public-API-Package-and-Stable-Closure.md)

**T87 gate:** exact stable PR head must be green before merge. After merge, `main` Release distribution validation must be green before tag `v0.8.0` is created.

---

## 5. Explicit non-goals for 0.8

0.8 does not add:

- synchronized output mode 2026;
- a generic public CSI writer;
- arbitrary CSI private/intermediate-byte injection;
- cursor visibility redesign;
- cursor color control;
- shell integration protocols;
- Kitty/CSI-u keyboard negotiation;
- OS-native pointer/mouse cursor APIs;
- background cursor-style probing;
- automatic cursor-style query during session open.

Synchronized output remains planned for `0.9.0`.

---

## 6. Stable release gate

`0.8.0` is release-authorized only when:

1. T80–T87 implementation is present on the exact PR head;
2. PR Staging validation is green on Windows, Linux, and macOS;
3. Linux exact package verification is green;
4. 0.8 XML documentation verification is green for all three TFMs;
5. fresh package-only cursor-style consumers pass on `net8.0`, `net9.0`, and `net10.0`;
6. PR is merged to `main`;
7. exact `main` Release distribution validation is green across its configured architecture matrix;
8. only then is tag `v0.8.0` created;
9. the tag workflow rebuilds, retests, repacks, revalidates the exact release package, and only then publishes.

Stable versioning in the branch is necessary for package validation but does not bypass this release gate.

---

## 7. Final version state

```text
VersionPrefix:   0.8.0
VersionSuffix:   <empty>
Version:         0.8.0
PackageVersion:  0.8.0
AssemblyVersion: 0.8.0.0
```

The feature/API design for `0.8.0` is closed. The next protocol-closure line is `0.9.0`.
