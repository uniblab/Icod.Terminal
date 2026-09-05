# T134 — Common Dynamic Color Mutation, Observation, and Reset

**Release:** `0.13.0`  
**Tranche:** `T134`  
**Development version:** `0.13.0-alpha.5`  
**Status:** Implemented; exact-head validation pending

---

## 1. Scope

T134 implements the common/core dynamic-color tier frozen by T130:

```text
DefaultForeground OSC 10 / reset 110
DefaultBackground OSC 11 / reset 111
TextCursor        OSC 12 / reset 112
```

It also freezes the public enum name `TerminalDynamicColor` for all seven non-Tektronix identities planned for 0.13. T135 will activate the four extended identities through the same public methods.

---

## 2. Public API

```csharp
public enum TerminalDynamicColor {
	DefaultForeground,
	DefaultBackground,
	TextCursor,
	MouseForeground,
	MouseBackground,
	HighlightBackground,
	HighlightForeground
}

ValueTask SetDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	CancellationToken cancellationToken = default
);

ValueTask<TerminalColor> QueryDynamicColorAsync(
	TerminalDynamicColor kind,
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

ValueTask ResetDynamicColorAsync(
	TerminalDynamicColor kind,
	CancellationToken cancellationToken = default
);
```

During T134, only the first three identities are accepted. The four extended identities fail with `ArgumentOutOfRangeException` before output and are enabled by T135.

---

## 3. Wire contract

Mutation uses canonical full-precision color encoding:

```text
OSC 10 ; rgb:rrrr/gggg/bbbb ST
OSC 11 ; rgb:rrrr/gggg/bbbb ST
OSC 12 ; rgb:rrrr/gggg/bbbb ST
```

Observation uses explicit bounded queries:

```text
OSC 10 ; ? ST
OSC 11 ; ? ST
OSC 12 ; ? ST
```

Reset uses terminal-policy reset controls:

```text
OSC 110 ST
OSC 111 ST
OSC 112 ST
```

Reset is not exact restoration of a prior observed color.

---

## 4. Observation semantics

Dynamic-color observation reuses `TerminalSession.ExecuteQueryAsync(...)` and the existing active-query response router.

The response matcher correlates the exact OSC identity. A response for OSC 11 cannot satisfy an outstanding OSC 10 query.

A matching reply is parsed through the T131 `TerminalColorCodec`, so the same strict precision/grammar contract applies to palette and dynamic-color observations.

ST, BEL, and C1 OSC framing are supported by the existing response framing architecture.

A matching malformed color fails with `FormatException`. Timeout and caller cancellation remain distinct. No result is cached as authoritative state.

---

## 5. Output semantics

Mutation and reset:

- require interactive terminal output;
- participate in the existing session output serialization domain;
- observe caller cancellation before commitment;
- commit complete frames with `CancellationToken.None`;
- do not flush implicitly;
- prove emission only, not terminal recognition.

Queries retain the existing active-query flush/deadline contract.

---

## 6. Tests

T134 tests cover:

- exact set/query/reset framing for OSC 10/110, 11/111, and 12/112;
- typed observation through the shared router;
- exact identity correlation;
- correlated malformed reply failure;
- BEL-terminated hash-form observation;
- non-cancellable committed mutation writes;
- no mutation/reset implicit flush;
- rejection of T135 extended identities before output during the T134 tranche.

---

## 7. T134 decision

The common dynamic-color tier now shares one semantic public API and one observation architecture with the indexed palette.

`TerminalDynamicColor` is the frozen public enum name. T135 extends the existing protocol mapping to mouse foreground/background and highlight background/foreground rather than adding another API family.
