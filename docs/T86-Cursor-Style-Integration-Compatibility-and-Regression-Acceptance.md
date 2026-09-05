# T86 — Cursor-Style Integration, Compatibility, and Regression Acceptance

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.7`  
**Status:** Implemented; cross-platform PR validation pending

---

## 1. Purpose

T86 closes the integration and regression-acceptance tranche for the 0.8 cursor-style feature set.

T80 through T85 established the protocol contract, structural CSI writer, typed semantic codec, public setter/query APIs, and truthful restoration lease. T86 does not broaden that public API. It proves that the resulting surface composes with the previously shipped terminal features and lifecycle machinery.

---

## 2. Compatibility matrix

The frozen semantic mapping remains:

| Semantic style | DECSCUSR `Ps` | Compatibility posture |
| --- | ---: | --- |
| `BlinkingBlock` | `1` | DEC core |
| `SteadyBlock` | `2` | DEC core |
| `BlinkingUnderline` | `3` | DEC core |
| `SteadyUnderline` | `4` | DEC core |
| `BlinkingBar` | `5` | xterm extension |
| `SteadyBar` | `6` | xterm extension |

Inbound DECRQSS interpretation remains:

```text
SP q       -> BlinkingBlock
0 SP q     -> BlinkingBlock
1 SP q     -> BlinkingBlock
2 SP q     -> SteadyBlock
3 SP q     -> BlinkingUnderline
4 SP q     -> SteadyUnderline
5 SP q     -> BlinkingBar
6 SP q     -> SteadyBar
```

Parameter `7` remains explicitly excluded from the semantic style set and from restoration logic.

Existing T82/T84 tests already cover this numeric matrix, malformed positive responses, unknown values, leading-zero forms, explicit unsupported responses, timeout, cancellation, and late-response ownership. T86 therefore adds acceptance coverage only where cross-feature behavior was not already proven.

---

## 3. Cursor visibility independence

T86 proves that cursor style and cursor visibility remain orthogonal.

A presentation lease may hide the physical cursor using the terminal's reversible visibility capability while `SetCursorStyleAsync(...)` independently emits DECSCUSR. Releasing the visibility lease restores visibility without modifying cursor style.

The accepted ordering is therefore structurally equivalent to:

```text
hide cursor
set cursor style
restore cursor visibility
```

No DECSCUSR call substitutes for DECTCEM/terminfo cursor visibility, and no visibility transition rewrites or infers cursor style.

---

## 4. Prior semantic-output composition

T86 proves deterministic session-owned output ordering across:

- application text;
- OSC 0 title;
- OSC 1 icon name;
- OSC 2 window title;
- OSC 7 current location;
- OSC 8 hyperlink begin/text/end;
- DECSCUSR cursor style;
- OSC 52 clipboard write.

All of these operations continue to serialize through the existing session output gate. Cursor-style mutation does not introduce a second output channel or bypass prior semantic ownership.

Ordinary cursor-style mutation does not flush. Existing presentation transitions retain their established flush behavior, and active queries continue to own their request flushes.

---

## 5. Lifecycle acceptance

T86 adds explicit managed suspend/resume coverage for a live cursor-style lease.

For an outer lease whose observed baseline is `A` and requested style is `B`:

```text
observe A
apply B
suspend -> restore A
resume  -> reapply B
release -> restore A
```

The observed baseline remains authoritative throughout the lifecycle cycle. No query is reissued during suspend or resume, because the lease already owns an authoritative semantic baseline and current logical top style.

A lease released while the session is physically suspended performs a logical-only pop. Because the baseline was already restored during suspend preparation, release emits no additional control bytes. A later resume with no remaining cursor-style lease emits no cursor-style state.

---

## 6. Existing query regression evidence

T84 and the pre-existing query integration suite remain authoritative for:

- one shared terminal reader;
- request/response correlation;
- caller-visible timeout;
- cancellation;
- late-response ownership;
- suspend interruption of active queries;
- post-resume query recovery;
- deterministic disposal of outstanding transactions.

T86 does not duplicate those tests merely under cursor-style names. The typed cursor-style query is layered directly on the same DECRQSS transaction substrate and was validated end-to-end in T84.

---

## 7. Resource and security acceptance

The cursor-style feature introduces no caller-controlled arbitrary protocol text and no new large-buffer path.

The accepted posture remains:

- closed six-value semantic enum;
- internal-only CSI structural writer;
- no raw numeric public setter;
- no raw CSI public writer;
- no automatic support probing;
- no fallback style substitution;
- no guessed restoration;
- bounded existing DECRQSS framing and transaction ownership.

The 0.7 OSC 52 payload/frame ceilings remain unchanged.

---

## 8. T86 test additions

T86 adds:

```text
tests/Icod.Terminal.Tests/src/Integration/TerminalCursorStyleAcceptanceTests.cs
tests/Icod.Terminal.Tests/src/Lifecycle/TerminalSessionCursorStyleLifecycleTests.cs
```

The added tests prove:

1. cursor visibility remains independent from cursor style;
2. cursor-style output composes in deterministic order with every prior semantic output family;
3. active cursor-style lease state restores to the observed baseline before suspend;
4. the active logical top style is reapplied after resume;
5. outer release restores the exact observed baseline;
6. release while physically suspended emits no cursor-style bytes;
7. resume after that suspended release does not resurrect a released style.

---

## 9. T86 gate

T86 is complete when the exact implementation head passes the pull-request workflow on Windows, Linux, and macOS, including all supported target frameworks and package verification.

After T86 is green, the remaining 0.8 work is **T87 — public API, documentation, sample, package, and stable closure**.
