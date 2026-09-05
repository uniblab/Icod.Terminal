# T84 — Typed Cursor-Style Query and Observation

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.5`  
**Status:** Implemented; PR validation pending  
**Predecessor:** T83 — semantic cursor-style set API

## 1. Purpose

T84 adds an explicit typed cursor-style query on top of the DECRQSS transaction substrate introduced in 0.3 and the semantic parser introduced by T82.

The public operation is:

```csharp
ValueTask<TerminalCursorStyleObservation> QueryCursorStyleAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

No new response matcher, input reader, or query transaction mechanism is introduced.

## 2. Observation contract

`TerminalCursorStyleObservation` has two public properties:

```text
IsSupported
Style
```

with these invariants:

```text
IsSupported == true  => Style contains one defined TerminalCursorStyle
IsSupported == false => Style is null
```

The type therefore distinguishes an explicit negative DECRQSS response from a successful semantic observation without overloading `null` as the entire operation result.

## 3. Transport reuse

`QueryCursorStyleAsync(...)` delegates the wire transaction to:

```csharp
QueryStatusStringAsync(
    TerminalStatusStringKind.CursorStyle,
    timeout,
    cancellationToken
);
```

The request remains the established DECRQSS cursor-style request:

```text
ESC P $ q SP q ESC \
```

The existing single-reader router, bounded response framing, query output serialization, request flush, timeout, cancellation, and late-response ownership semantics remain authoritative.

## 4. Response semantics

An explicit negative DECRPSS response becomes:

```text
IsSupported = false
Style       = null
```

A positive DECRPSS response is parsed by `TerminalCursorStyleCodec`.

Recognized values include omitted, `0`, and `1` as blinking block, `2` through `6` as their frozen semantic styles, and leading-zero decimal aliases.

A positive response whose state is malformed or outside the frozen semantic set fails with `FormatException` rather than being converted to unsupported or timeout.

## 5. Failure semantics

The typed wrapper preserves the existing distinctions:

- invalid timeout => `ArgumentOutOfRangeException`;
- endpoint/query incompatibility => `InvalidOperationException`;
- caller cancellation => `OperationCanceledException`;
- caller-visible deadline expiration => `TimeoutException`;
- malformed correlated DECRPSS or unrecognized positive cursor style => `FormatException`;
- explicit negative DECRPSS => successful unsupported observation.

No retry or fallback query occurs.

## 6. Privacy and automatic behavior

Cursor-style querying is explicit per-call behavior.

The library does not query cursor style automatically during:

- session open;
- capability discovery;
- ordinary cursor-style setting;
- suspend/resume;
- disposal.

The result is not cached as a session-global support flag.

## 7. Compatibility

The existing public raw DECRQSS operation remains source- and behavior-compatible:

```csharp
QueryStatusStringAsync(
    TerminalStatusStringKind.CursorStyle,
    ...
);
```

The new typed method is a semantic convenience layer, not a replacement for the general fixed-status-string query surface.

## 8. Tests

T84 adds public end-to-end tests proving:

- exact reuse of the DECRQSS `SP q` request;
- supported observations for omitted/`0`/`1` and `2` through `6`;
- leading-zero positive state;
- explicit unsupported observation from negative DECRPSS;
- deterministic `FormatException` for `7`, multi-parameter, private/non-decimal, and wrong-identifier states;
- pre-cancelled query produces no request;
- timeout propagates through the typed wrapper.

The lower-level DECRQSS suite continues to prove fragmented responses, 8-bit DCS/ST compatibility, ordinary-input preservation, bounded malformed response handling, cancellation/timeout late-response ownership, and disposal behavior.

## 9. Gate

T84 is complete when callers can explicitly observe cursor style through a typed API while all transport, timeout, cancellation, correlation, and late-response semantics remain those of the established DECRQSS transaction architecture.

The next tranche is **T85 — restoration and scoped-state decision**.
