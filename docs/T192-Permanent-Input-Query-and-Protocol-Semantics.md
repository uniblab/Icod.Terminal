# T192 — Permanent Input, Query, and Protocol Semantics

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T191 — permanent architecture and ownership documentation, workflow #918  
**Status:** Complete and green  
**Exact-head validation:** workflow #924 at `528ef6a59aff5ec654bc59571018f1ef70cd92e5`

## 1. Purpose

T192 replaces release-by-release input/query semantics as the primary consumer authority with durable 1.x documentation.

No new input protocol, query family, public result type, or wire form is introduced by this tranche.

## 2. Permanent authorities

T192 adds:

- `docs/Input-and-Events.md`;
- `docs/Queries-and-Responses.md`.

It also rewrites:

- `docs/Modern-Keyboard-Security-and-Compatibility.md`

from a 0.17 release note into a permanent 1.x compatibility/security companion.

## 3. Input contract consolidated

`Input-and-Events.md` freezes the supported 1.x explanation of:

- one authoritative live-session reader;
- `TerminalEvent` input/lifecycle/timeout/cancelled semantics;
- wait cancellation vs transport cancellation;
- ordinary UTF-8/Rune input;
- traditional terminfo-driven keyboard normalization;
- bounded Escape ambiguity;
- zero-based mouse events;
- explicit focus reports;
- framed, chunked bracketed paste;
- modern key phase/modifier/associated-text semantics;
- negotiated Kitty reporting and xterm decode-only compatibility;
- overlapping rich-input protocol lease composition;
- screen-local modern-keyboard handoff;
- decoder buffer/resource bounds;
- end-of-input semantics;
- input privacy/security responsibilities.

The T190 removal of public `TerminalSession.Input` is therefore integrated into the permanent input contract rather than remaining only a migration/audit note.

## 4. Query contract consolidated

`Queries-and-Responses.md` freezes the supported 1.x explanation of:

- one bidirectional terminal conversation shared with ordinary input;
- typed query families rather than raw query builders;
- ambiguity-sensitive serialization;
- bounded pending requests and request size;
- the pre-emission/post-emission commit boundary;
- caller lifetime vs physical late-response ownership;
- response routing before ordinary input delivery;
- malformed correlated response behavior;
- bounded oversized-response resynchronization;
- async cancellation/timeout exception behavior;
- suspend/resume query generations;
- prohibition on old queued-generation emission;
- lifecycle-only observation using the same ambiguity domain;
- lifecycle participant query restrictions;
- query output ordering/flush behavior;
- conservative handling of uncertain output failure;
- privacy implications of clipboard/environment observations.

## 5. Cancellation boundary distinction

T192 makes an important permanent distinction explicit.

For `ReadEventAsync(...)`, caller cancellation or timeout ends only that wait. The underlying input path preserves partially accumulated UTF-8/control-sequence state so subsequent reads do not lose bytes.

For active terminal queries, cancellation and timeout depend on the emission commit boundary:

- before request emission, cancellation/timeout prevents transmission;
- after request emission, the caller may complete while the transaction retains bounded ownership of a possible late response.

These are different contracts and must not be described interchangeably in README/XML/sample guidance.

## 6. Modern keyboard compatibility

The permanent keyboard companion now states that:

- Kitty progressive keyboard reporting is actively negotiated and reversibly owned;
- semantic modes remain `Disambiguated`, `EventTypes`, and `AllKeys`;
- traditional keyboard decoding remains available independently;
- xterm `modifyOtherKeys` remains decode-only;
- raw Kitty push/pop, raw private-use values, and generic keyboard CSI writers remain outside the public API;
- screen-local keyboard ownership is coordinated with presentation transitions;
- resume re-observes support instead of trusting stale capability state;
- additional associated-text/layout metadata has privacy implications for consumers.

## 7. Resource and ambiguity invariants

T192 documents the current 1.x defensive limits without broadening the public API:

- undecoded input is bounded by `TerminalSession.MaximumBufferedInputBytes` and per-session decoder options;
- paste accumulation is chunked rather than unbounded;
- the query manager bounds the pending queue;
- raw query request size is bounded internally;
- caller-visible query timeouts are bounded;
- late-response ownership is finite;
- response resynchronization is bounded.

These limits support deterministic failure rather than unbounded parser/queue growth.

## 8. Historical documents

Historical records including T16, T18–T20, T141, T170–T176, T181, and the 0.2/0.3/0.17/0.18 public API baselines remain useful design evidence.

They are no longer required reading to understand current 1.x input/query behavior. Where a historical document reflects a superseded pre-1.0 surface, the permanent documents are authoritative for the 1.x contract.

## 9. Validation

Workflow #924 passed at exact head `528ef6a59aff5ec654bc59571018f1ef70cd92e5`.

The exact-head gate retained and passed:

- Windows/Linux/macOS build and tests;
- real `Icod.DCurses` focused acceptance;
- repeated DCurses hardening soak;
- exact Staging package verification;
- every package-only compatibility contract from 0.8 through 0.18.

## 10. Exit criteria

T192 is complete because:

1. the two permanent authorities exist and agree with current source behavior;
2. modern-keyboard compatibility guidance is no longer release-specific;
3. cancellation and timeout commit boundaries are explained consistently;
4. parser/query resource limits and late-response ownership are documented;
5. lifecycle generation and post-resume observation semantics are documented;
6. no raw/vendor protocol surface has been introduced accidentally;
7. the exact T192 head passed Windows/Linux/macOS, DCurses acceptance/soak, package verification, and every retained 0.8–0.18 package contract.
