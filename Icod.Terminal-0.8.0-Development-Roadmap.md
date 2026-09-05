# Icod.Terminal 0.8.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.7`  
**Predecessor:** `0.7.0` — OSC 52 clipboard/selection operations  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** typed cursor-style control, CSI intermediate-byte output, DECRQSS cursor-state interpretation, and truthful restoration  
**Successor:** `0.9.0` — synchronized output and nested transactional output state

---

## 1. Release objective

`Icod.Terminal 0.8.0` SHALL add semantic cursor-style control on top of the live-session, output-serialization, query-routing, and lifecycle foundations established by `0.1.0` through `0.7.0`.

The primary wire operation is:

```text
CSI Ps SP q
```

Public callers SHALL work with typed cursor-style semantics rather than raw numeric parameters or arbitrary CSI construction.

Cursor style and cursor visibility are distinct terminal states and remain separate throughout 0.8.

---

## 2. Existing foundations reused by 0.8

0.8 builds on:

- session-owned control-output serialization;
- canonical seven-bit CSI/DCS output conventions;
- one shared input reader;
- bounded query transactions;
- caller-visible monotonic timeouts;
- bounded late-response ownership;
- DECRQSS request/DECRPSS response routing;
- `TerminalStatusStringKind.CursorStyle` for DECRQSS identifier `SP q`;
- lifecycle suspend/resume and deterministic disposal machinery;
- existing presentation-state ownership patterns.

No second cursor-specific input loop or output channel is introduced.

---

## 3. Frozen protocol posture

T80 freezes the semantic cursor-style set as:

```text
BlinkingBlock
SteadyBlock
BlinkingUnderline
SteadyUnderline
BlinkingBar
SteadyBar
```

Outbound mapping is explicit:

```text
BlinkingBlock     -> 1
SteadyBlock       -> 2
BlinkingUnderline -> 3
SteadyUnderline   -> 4
BlinkingBar       -> 5
SteadyBar         -> 6
```

Omitted/`0`/`1` inbound state normalize to blinking block. xterm `7` is not treated as a generic cursor style or restoration primitive.

Bar styles are retained as xterm-compatible extensions without fabricating endpoint support.

---

## 4. Public API direction

The reviewed 0.8 public delta now consists of concepts equivalent to:

```csharp
public enum TerminalCursorStyle {
    BlinkingBlock,
    SteadyBlock,
    BlinkingUnderline,
    SteadyUnderline,
    BlinkingBar,
    SteadyBar
}

public sealed class TerminalCursorStyleObservation {
    public bool IsSupported { get; }
    public TerminalCursorStyle? Style { get; }
}

public sealed class TerminalCursorStyleLease : IAsyncDisposable {
    public TerminalCursorStyle Style { get; }
}

public ValueTask SetCursorStyleAsync(
    TerminalCursorStyle style,
    CancellationToken cancellationToken = default
);

public ValueTask<TerminalCursorStyleObservation> QueryCursorStyleAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);

public ValueTask<TerminalCursorStyleLease> AcquireCursorStyleAsync(
    TerminalCursorStyle style,
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

The lease was accepted in T85 only because outer acquisition observes the actual prior semantic cursor style before mutation.

0.8 SHALL NOT expose a generic public CSI writer, raw cursor-style parameter, raw DECRQSS parser, or a fake `Default`/`Reset` style.

---

## 5. State and restoration policy

The release distinguishes:

- **set** — emit one semantic style;
- **observe/query** — explicitly request the current style where DECRQSS is supported;
- **lease** — own a style temporarily while retaining an authoritative prior state;
- **restore** — return to an actually observed or session-owned previous semantic style.

Exact restoration is not:

- emitting `0`;
- emitting `1` without knowing prior state;
- emitting xterm `7`;
- guessing from `TERM` or emulator identity.

The outermost lease queries the real terminal style before mutation. Nested leases use the known session-owned top style and therefore do not issue redundant queries. Releases are strict LIFO. Unscoped mutation is rejected while a cursor-style lease stack is active.

Suspend restores the originally observed baseline; resume reapplies the active logical top style. Releasing while physically suspended is logical-only and emits no cursor-style bytes.

---

## 6. Output and query invariants

Cursor-style writes and queries preserve the existing session contracts:

- validate arguments before protocol output;
- reject known redirected/incompatible endpoints where required;
- serialize session-owned output through the shared gate;
- construct complete control frames before commit;
- allow caller cancellation before commit;
- do not caller-truncate committed frames;
- do not implicitly flush ordinary setter output;
- query requests reuse the established DECRQSS flush/deadline path;
- malformed correlated positive state remains distinct from timeout and unsupported state;
- cleanup/restoration is not caller-cancellable;
- support is never inferred merely from successful output emission.

Successful setter completion proves emission only.

---

## 7. Tranche sequence and status

The 0.8 release uses tranche numbers `T80` through `T87`, aligned with the release line.

### T80 — cursor-style contract and reference freeze — complete

Frozen:

- six semantic styles;
- DEC/xterm compatibility boundary;
- explicit outbound parameters `1`–`6`;
- omitted/`0`/`1` inbound normalization;
- rejection of xterm `7` as a semantic style;
- typed query semantics through DECRQSS;
- truthful restoration rules;
- cursor-style/visibility independence.

**Implementation record:** [`docs/T80-Cursor-Style-Contract-and-Reference-Freeze.md`](docs/T80-Cursor-Style-Contract-and-Reference-Freeze.md)

### T81 — reusable CSI intermediate-byte output primitive — complete

Implemented:

- internal structural CSI writer;
- parameter/intermediate/final-byte validation;
- canonical seven-bit `ESC [` framing;
- DECSCUSR frame generation for parameters `1`–`6`;
- complete-frame emission with pre-commit cancellation;
- no implicit flush;
- no public raw CSI surface;
- byte-exact tests for all six frozen parameters.

**Implementation record:** [`docs/T81-CSI-Intermediate-Byte-Output-Primitive.md`](docs/T81-CSI-Intermediate-Byte-Output-Primitive.md)

**Validation:** PR workflow #246 green across Windows, Linux, and macOS.

### T82 — typed cursor-style codec and DECRQSS interpretation — complete

Implemented:

- public `TerminalCursorStyle` enum;
- deterministic semantic-to-DECSCUSR parameter mapping;
- strict DECRQSS status-string parser;
- omitted/`0`/`1` normalization;
- leading-zero support;
- deterministic rejection of malformed, multi-parameter, private/non-decimal, overflow, `7`, and unknown positive values;
- terminal-I/O-free codec tests.

**Implementation record:** [`docs/T82-Typed-Cursor-Style-Codec-and-DECRQSS-Interpretation.md`](docs/T82-Typed-Cursor-Style-Codec-and-DECRQSS-Interpretation.md)

**Validation:** PR workflow #250 green across Windows, Linux, and macOS.

### T83 — semantic cursor-style set API — complete

Implemented:

- public `SetCursorStyleAsync(...)`;
- semantic validation before output;
- redirected-output rejection before emission;
- session-owned output serialization;
- pre-commit cancellation;
- non-truncating committed frame writes;
- no implicit flush;
- focused tests for all six styles, invalid enum, redirection, cancellation, output failure, and application-text ordering.

**Implementation record:** [`docs/T83-Semantic-Cursor-Style-Set-API.md`](docs/T83-Semantic-Cursor-Style-Set-API.md)

**Validation:** PR workflow #260 green together with T84.

### T84 — typed cursor-style query/observation API — complete

Implemented:

- public `TerminalCursorStyleObservation`;
- public `QueryCursorStyleAsync(...)`;
- reuse of existing `QueryStatusStringAsync(CursorStyle, ...)` transport;
- explicit unsupported observation from negative DECRPSS;
- strict positive-state parsing through the T82 codec;
- timeout/cancellation/malformed-response distinction;
- no automatic probing or cached support flag;
- end-to-end public tests against a scripted DCS transport.

**Implementation record:** [`docs/T84-Typed-Cursor-Style-Query-and-Observation.md`](docs/T84-Typed-Cursor-Style-Query-and-Observation.md)

**Validation:** PR workflow #260 green together with T83.

### T85 — restoration and scoped-state decision — complete

Accepted and implemented:

- public `TerminalCursorStyleLease`;
- public `AcquireCursorStyleAsync(...)`;
- authoritative outer baseline observation before mutation;
- no mutation when the baseline query is unsupported, malformed, timed out, or cancelled;
- nested strict-LIFO session-owned style restoration without redundant queries;
- unscoped setter rejection while a lease is active;
- retryable restoration ownership after cleanup failure;
- baseline restoration before suspend and active-top reapplication after resume;
- logical-only release while suspended;
- exact baseline restoration during outer release/session disposal;
- no guessed `0`, `1`, or xterm `7` restoration.

**Implementation record:** [`docs/T85-Cursor-Style-Restoration-and-Scoped-State.md`](docs/T85-Cursor-Style-Restoration-and-Scoped-State.md)

**Validation:** PR workflow #270 green across Windows, Linux, and macOS after correction of a test-source syntax error found by the preceding validation run.

### T86 — integration, compatibility, and regression acceptance — implemented; validation pending

Implemented acceptance coverage:

- DEC/xterm compatibility matrix retained and documented;
- cursor-style/visibility independence through real presentation leases;
- deterministic ordering with application text, OSC 0/1/2, OSC 7, OSC 8, and OSC 52;
- managed suspend restores the observed cursor-style baseline;
- resume reapplies the active logical style;
- final release restores the exact observed baseline;
- release while suspended emits no cursor-style bytes and does not resurrect released state after resume;
- existing T82/T84/query integration coverage remains authoritative for malformed/unknown responses, timeout, cancellation, late-response ownership, and single-reader behavior.

**Implementation record:** [`docs/T86-Cursor-Style-Integration-Compatibility-and-Regression-Acceptance.md`](docs/T86-Cursor-Style-Integration-Compatibility-and-Regression-Acceptance.md)

**Gate:** exact `0.8.0-alpha.7` PR head must be green across Windows, Linux, and macOS, including all target frameworks and package verification.

### T87 — public API, docs, sample, package, and stable closure — next

Deliverables:

- public API regret audit;
- `docs/Public-API-Baseline-0.8.md`;
- README update;
- focused cursor-style sample;
- package-verifier XML documentation assertions;
- package-only consumer smoke on `net8.0`, `net9.0`, and `net10.0`;
- stable `0.8.0` metadata;
- final PR/main/tag release gates.

---

## 8. Explicit non-goals for 0.8

0.8 SHALL NOT add:

- synchronized output mode 2026;
- a generic public CSI writer;
- arbitrary CSI private/intermediate-byte injection;
- cursor visibility redesign;
- cursor color control;
- shell integration protocols;
- Kitty/CSI-u keyboard negotiation;
- OS-native pointer/mouse cursor APIs;
- terminal-emulator-specific configuration files;
- background cursor-style probing;
- automatic cursor-style query during session open, suspend/resume, or disposal outside an explicitly acquired cursor-style lease's already-observed state.

Synchronized output remains planned for `0.9.0`.

---

## 9. Stable release gate

`0.8.0` is ready for stable publication only when:

1. the T80 contract remains authoritative;
2. CSI intermediate-byte output is structurally implemented and tested;
3. every public cursor-style value has deterministic wire semantics;
4. typed query interpretation reuses the existing DECRQSS architecture;
5. malformed and unknown positive cursor-style responses fail deterministically;
6. restoration is based on authoritative previous-state knowledge;
7. cursor visibility behavior remains independent;
8. Windows, Linux, and macOS validation is green;
9. package-only consumers pass on `net8.0`, `net9.0`, and `net10.0`;
10. packaged XML documentation contains the reviewed 0.8 public delta;
11. `main` Release validation is green after merge;
12. only then is tag `v0.8.0` created for publication.

---

## 10. Current development state

```text
VersionPrefix:   0.8.0
VersionSuffix:   alpha.7
Version:         0.8.0-alpha.7
PackageVersion:  0.8.0-alpha.7
AssemblyVersion: 0.8.0.0
```

**T80–T85 are complete and green. T86 is implemented on the current head and awaits its exact-head PR validation. T87 is the final development tranche after that gate is green.**
