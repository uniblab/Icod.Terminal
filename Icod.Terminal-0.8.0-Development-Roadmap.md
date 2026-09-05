# Icod.Terminal 0.8.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.1`  
**Predecessor:** `0.7.0` — OSC 52 clipboard/selection operations  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** typed cursor-style control, CSI intermediate-byte output, DECRQSS cursor-state interpretation, and truthful restoration  
**Successor:** `0.9.0` — synchronized output and nested transactional output state

---

## 1. Release objective

`Icod.Terminal 0.8.0` SHALL add semantic cursor-style control on top of the live-session, output-serialization, query-routing, and lifecycle foundations established by `0.1.0` through `0.7.0`.

The primary wire operation is DECSCUSR-style cursor-shape selection:

```text
CSI Ps SP q
```

The release must expose typed cursor-style semantics rather than raw numeric parameters or arbitrary CSI construction.

0.8 must also prove that the existing query substrate can interpret DECRQSS cursor-style state reliably enough to support typed observation and, where previous state is genuinely known, truthful scoped restoration.

Cursor style and cursor visibility are distinct terminal states. This release SHALL NOT merge or conflate those concepts.

---

## 2. Existing foundations to reuse

0.8 SHALL build on existing contracts rather than duplicate them.

Already available:

- session-owned control-output serialization;
- canonical seven-bit CSI/DCS output conventions;
- one shared input reader;
- bounded query transactions;
- caller-visible monotonic timeouts;
- cancellation-before-emission behavior;
- bounded late-response ownership;
- DECRQSS request/DECRPSS response routing;
- public `TerminalStatusStringKind.CursorStyle` for DECRQSS identifier `SP q`;
- lifecycle suspend/resume and deterministic disposal machinery;
- presentation-state lease patterns whose restoration rules are explicit.

0.8 SHALL extend these mechanisms rather than creating a second cursor-specific input loop or output channel.

---

## 3. Protocol reference and compatibility posture

The implementation SHALL distinguish portable DEC-style semantics from emulator extensions.

### 3.1 DECSCUSR core

The standard operation is:

```text
CSI Ps SP q
```

The typed model must cover the established cursor-style choices:

- blinking block;
- steady block;
- blinking underline;
- steady underline;
- blinking bar where the target compatibility profile supports the extension;
- steady bar where the target compatibility profile supports the extension.

The exact treatment of parameter `0`, explicit default/reset behavior, and emulator-specific extensions such as xterm's restore-initial-resource form SHALL be frozen before public API exposure.

### 3.2 Query integration

The existing DECRQSS request identifier for cursor style is:

```text
SP q
```

0.8 SHALL reuse `QueryStatusStringAsync(TerminalStatusStringKind.CursorStyle, ...)` internally and/or as the transport substrate for a typed cursor-style query.

A successful DECRQSS response must be parsed into a typed cursor-style state only when the returned status string is syntactically and semantically recognized.

Unknown numeric values must not be silently coerced to a known style.

---

## 4. Public API principles

The final public surface remains subject to T86/T87 review, but the release should converge on semantic operations such as:

- a closed `TerminalCursorStyle` value type or enum;
- an explicit asynchronous cursor-style setter;
- an explicit typed query/observation operation where reliable;
- an optional scoped cursor-style lease only when previous state is actually known.

0.8 SHALL NOT expose:

```text
SendCsi(...)
WriteEscape(...)
SetCursorStyle(int rawParameter)
string rawCursorStyle
arbitrary CSI intermediate bytes
```

The library should not promise universal support merely because the wire sequence is well known.

---

## 5. State and restoration policy

Cursor-style mutation is easy to emit but exact restoration is more subtle.

The release SHALL distinguish:

- **set** — emit one requested cursor style;
- **observe/query** — request and parse current cursor style where DECRQSS is supported;
- **reset/default** — use only a protocol-defined behavior whose semantics are explicitly frozen;
- **restore** — return to a previously observed style, not merely emit a guessed inverse operation.

A scoped cursor-style lease SHALL NOT claim exact restoration unless the previous style was successfully observed or otherwise authoritatively known.

If previous state cannot be known, the public API may still expose an unscoped setter while declining to expose a misleading lease.

---

## 6. Output-layer requirement

0.8 is the milestone which must make CSI intermediate-byte output a deliberate reusable internal primitive.

DECSCUSR contains an intermediate space byte before final `q`:

```text
CSI Ps SP q
```

The output layer SHALL encode this structurally rather than embedding ad hoc literal escape strings inside the public session method.

The primitive must remain internal unless a later protocol-extension milestone provides evidence that a public CSI construction surface is warranted.

---

## 7. Resource and failure semantics

Cursor-style frames are small and fixed-size, but normal output/query invariants still apply.

The implementation SHALL guarantee:

- argument validation before protocol output;
- no partial frame caused by caller cancellation after commit;
- no implicit flush for ordinary set operations unless explicitly justified;
- query request flush through the established transaction substrate;
- bounded response parsing;
- malformed DECRQSS cursor-style state reported distinctly from timeout;
- redirected/incompatible endpoint rejection consistent with existing session policy;
- support uncertainty remains explicit.

---

## 8. Lifecycle semantics

If 0.8 introduces a cursor-style lease, active logical cursor-style ownership must participate correctly in managed terminal lifecycle transitions.

Before a catchable suspend, the library SHALL avoid leaving misleading library-owned cursor-style state behind when it can restore the prior state truthfully.

After successful resume/re-entry, active logical state may be re-applied only under the same ownership rules used before suspension.

If reliable previous-state restoration cannot be guaranteed, 0.8 SHALL prefer a simpler setter/query API over an unsafe scoped abstraction.

---

## 9. Development tranche sequence

The 0.8 release uses tranche numbers `T80` through `T87`, so the tranche family follows the release line directly. The planned 0.9 release can therefore use `T90` onward.

### T80 — cursor-style contract and reference freeze

Deliverables:

- exact DECSCUSR wire grammar;
- DEC versus xterm extension boundary;
- typed style set;
- parameter `0` policy;
- bar-cursor compatibility policy;
- reset/default semantics;
- query semantics through DECRQSS;
- restoration truthfulness rules;
- explicit non-goals.

**Gate T80:** no production public cursor-style implementation until numeric mapping, query interpretation, extension policy, and restoration semantics are frozen.

### T81 — reusable CSI intermediate-byte output primitive

Deliverables:

- internal CSI writer support for parameter bytes plus intermediate bytes plus final byte;
- DECSCUSR frame generation using the primitive;
- canonical seven-bit emission;
- validation before output;
- byte-exact tests for every frozen parameter value;
- no generic public CSI escape hatch.

**Gate T81:** DECSCUSR can be emitted structurally without ad hoc public or session-level escape concatenation.

### T82 — typed cursor-style codec and DECRQSS interpretation

Deliverables:

- typed internal/public candidate representation;
- mapping between semantic style and DECSCUSR parameter;
- strict parser for returned DECRQSS cursor-style status strings;
- deterministic rejection of malformed, unknown, or out-of-contract values;
- DEC/xterm extension policy encoded explicitly;
- terminal-I/O-free codec tests.

**Gate T82:** cursor style round-trips between semantic state and recognized protocol representation without terminal I/O.

### T83 — semantic cursor-style set API

Deliverables:

- public typed setter;
- endpoint validation;
- session-owned control-output ordering;
- cancellation-before-commit behavior;
- no implicit flush unless required by evidence;
- interaction tests with text, OSC 0/1/2, OSC 7, OSC 8, OSC 52, and existing presentation transitions.

**Gate T83:** callers can set cursor style without knowing CSI syntax or numeric parameters.

### T84 — typed cursor-style query/observation API

Deliverables:

- typed cursor-style query built on existing DECRQSS transport;
- caller-supplied bounded timeout;
- supported/unsupported/malformed/timeout behavior;
- raw `TerminalStatusStringResponse` remains compatible;
- no second reader loop;
- deterministic correlation and late-response tests.

**Gate T84:** callers can explicitly observe cursor style when the terminal reports it, without parsing DECRQSS strings themselves.

### T85 — restoration and scoped-state decision

Deliverables:

- audit whether a cursor-style lease can restore truthfully;
- if yes, acquire/restore API with strict ownership and lifecycle semantics;
- if no, explicit decision to keep 0.8 setter/query-only;
- nested acquisition policy if a lease is introduced;
- disposal, failure, suspend/resume, and retry tests appropriate to the chosen model.

**Gate T85:** no public restoration API claims more knowledge than the terminal actually supplied.

### T86 — integration, compatibility, and regression acceptance

Deliverables:

- representative DEC-compatible and xterm-compatible parameter matrix;
- malformed/unknown DECRQSS response tests;
- output ordering against prior semantic protocol families;
- query timeout/cancellation/late-response regression coverage;
- cursor visibility independence tests;
- lifecycle regression coverage;
- Windows/Linux/macOS CI acceptance.

**Gate T86:** cursor-style support composes with all existing session behavior without weakening prior ownership or query guarantees.

### T87 — public API, docs, sample, package, and stable closure

Deliverables:

- public API regret audit;
- `docs/Public-API-Baseline-0.8.md`;
- README update;
- focused cursor-style sample or extension of an existing sample, whichever teaches the contract more clearly;
- package-verifier XML documentation assertions;
- package-only consumer smoke for the new public delta;
- stable `0.8.0` metadata;
- final PR/main/tag release gates.

**Gate T87:** the reviewed public cursor-style surface and actual NuGet package are proven from fresh consumers on all supported TFMs.

---

## 10. Explicit non-goals for 0.8

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
- background cursor-style probing during session open;
- automatic terminal feature probing without an explicit caller operation.

Synchronized output remains the planned `0.9.0` closure milestone.

---

## 11. Stable release gate

`0.8.0` is ready for stable publication only when:

1. the T80 cursor-style contract is frozen and implementation matches it;
2. CSI intermediate-byte output is structurally implemented and tested;
3. every public cursor-style value has deterministic wire semantics;
4. typed query interpretation reuses the existing single-reader DECRQSS architecture;
5. malformed and unknown cursor-style responses fail deterministically;
6. any restoration API is truthful about previous-state knowledge;
7. cursor visibility behavior remains unchanged and independent;
8. Windows, Linux, and macOS validation is green;
9. package-only consumers pass on `net8.0`, `net9.0`, and `net10.0`;
10. packaged XML documentation contains the reviewed 0.8 public delta;
11. `main` Release validation is green after merge;
12. only then is tag `v0.8.0` created for publication.

---

## 12. Initial development state

The `0.8.0` branch begins at:

```text
VersionPrefix: 0.8.0
VersionSuffix: alpha.1
Version:       0.8.0-alpha.1
PackageVersion:0.8.0-alpha.1
AssemblyVersion:0.8.0.0
```

The first implementation tranche is **T80 — cursor-style contract and reference freeze**.
