# Icod.Terminal 0.8.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.5`  
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

The 0.8 public delta is converging on:

```csharp
public enum TerminalCursorStyle {
    BlinkingBlock,
    SteadyBlock,
    BlinkingUnderline,
    SteadyUnderline,
    BlinkingBar,
    SteadyBar
}

public ValueTask SetCursorStyleAsync(
    TerminalCursorStyle style,
    CancellationToken cancellationToken = default
);

public ValueTask<TerminalCursorStyleObservation> QueryCursorStyleAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

A scoped lease is not yet frozen. T85 must prove truthful restoration before any lease becomes public.

0.8 SHALL NOT expose a generic public CSI writer, raw cursor-style parameter, raw DECRQSS parser, or a fake `Default`/`Reset` style.

---

## 5. State and restoration policy

The release distinguishes:

- **set** — emit one semantic style;
- **observe/query** — explicitly request the current style where DECRQSS is supported;
- **restore** — return to an actually observed previous semantic style.

Exact restoration is not:

- emitting `0`;
- emitting `1` without knowing prior state;
- emitting xterm `7`;
- guessing from `TERM` or emulator identity.

If T85 accepts a lease, acquisition must obtain an authoritative prior semantic style before mutation.

---

## 6. Output and query invariants

Cursor-style writes and queries must preserve the existing session contracts:

- validate arguments before protocol output;
- reject known redirected/incompatible endpoints where required;
- serialize session-owned output through the shared gate;
- construct complete control frames before commit;
- allow caller cancellation before commit;
- do not caller-truncate committed frames;
- do not implicitly flush ordinary setter output;
- query requests reuse the established DECRQSS flush/deadline path;
- malformed correlated positive state remains distinct from timeout and unsupported state.

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

- public `TerminalCursorStyle` candidate enum;
- deterministic semantic-to-DECSCUSR parameter mapping;
- strict DECRQSS status-string parser;
- omitted/`0`/`1` normalization;
- leading-zero support;
- deterministic rejection of malformed, multi-parameter, private/non-decimal, overflow, `7`, and unknown positive values;
- terminal-I/O-free codec tests.

**Implementation record:** [`docs/T82-Typed-Cursor-Style-Codec-and-DECRQSS-Interpretation.md`](docs/T82-Typed-Cursor-Style-Codec-and-DECRQSS-Interpretation.md)

**Validation:** PR workflow #250 green across Windows, Linux, and macOS.

### T83 — semantic cursor-style set API — implemented; validation pending

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

### T84 — typed cursor-style query/observation API — implemented; validation pending

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

### T85 — restoration and scoped-state decision — next

Deliverables:

- determine whether a cursor-style lease can restore truthfully;
- if yes, define acquisition/restore ownership and nesting semantics;
- if no, explicitly freeze 0.8 as setter/query-only;
- prove failure-before-mutation when prior style cannot be observed;
- define disposal and lifecycle implications.

**Gate T85:** no public restoration API claims more knowledge than the terminal actually supplied.

### T86 — integration, compatibility, and regression acceptance

Deliverables:

- DEC/xterm compatibility matrix;
- broader output ordering against text, OSC 0/1/2, OSC 7, OSC 8, and OSC 52;
- query timeout/cancellation/late-response regression coverage;
- cursor visibility independence tests;
- lifecycle regression coverage;
- Windows/Linux/macOS acceptance.

### T87 — public API, docs, sample, package, and stable closure

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
- automatic cursor-style query during session open, suspend/resume, or disposal.

Synchronized output remains planned for `0.9.0`.

---

## 9. Stable release gate

`0.8.0` is ready for stable publication only when:

1. the T80 contract remains authoritative;
2. CSI intermediate-byte output is structurally implemented and tested;
3. every public cursor-style value has deterministic wire semantics;
4. typed query interpretation reuses the existing DECRQSS architecture;
5. malformed and unknown positive cursor-style responses fail deterministically;
6. any restoration API is truthful about previous-state knowledge;
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
VersionSuffix:   alpha.5
Version:         0.8.0-alpha.5
PackageVersion:  0.8.0-alpha.5
AssemblyVersion: 0.8.0.0
```

**T80–T82 are complete and green. T83 and T84 are implemented on the current head. The next design tranche is T85.**
