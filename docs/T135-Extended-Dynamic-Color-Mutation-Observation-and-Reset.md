# T135 — Extended Dynamic Color Mutation, Observation, and Reset

**Release:** `0.13.0`  
**Tranche:** `T135`  
**Development version:** `0.13.0-alpha.6`  
**Status:** Implemented; exact-head validation pending

---

## 1. Scope

T135 activates the extended xterm dynamic-color tier through the public API introduced in T134:

```text
MouseForeground     OSC 13 / reset 113
MouseBackground     OSC 14 / reset 114
HighlightBackground OSC 17 / reset 117
HighlightForeground OSC 19 / reset 119
```

No new public API family is added.

---

## 2. Public API

The existing surface now accepts all seven `TerminalDynamicColor` identities:

```csharp
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

Invalid enum values fail with `ArgumentOutOfRangeException` before output.

---

## 3. Wire mappings

Mutation/query identities:

```text
DefaultForeground   10
DefaultBackground   11
TextCursor          12
MouseForeground     13
MouseBackground     14
HighlightBackground 17
HighlightForeground 19
```

Reset identifiers are the corresponding value plus 100.

All mutations use canonical `rgb:rrrr/gggg/bbbb` color encoding and ST termination.

---

## 4. Observation and correlation

Extended observations use the same active-query transaction and exact OSC-identity matcher as the common tier.

A reply for one dynamic-color identity cannot satisfy a query for another. Correlated malformed color payloads fail with `FormatException`.

The T131 color parser remains the sole semantic color parser, preserving identical precision rules for indexed palette and dynamic colors.

---

## 5. Portability posture

The extended tier is intentionally documented separately from the common/core tier.

Successful mutation proves complete protocol emission only. Successful observation proves a matching conforming response for that transaction only.

The library does not infer support from terminal brand, `TERM`, operating system, environment variables, or success of another color identity.

---

## 6. Tests

T135 extends the existing dynamic-color test matrix to all seven selected identities and verifies:

- exact set/query/reset frames for OSC 13/113, 14/114, 17/117, and 19/119;
- typed observation;
- exact identity correlation across extended identities;
- correlated malformed extended replies;
- BEL-terminated extended observations;
- invalid enum rejection before output;
- non-cancellable committed mutation writes;
- no implicit mutation/reset flush.

---

## 7. T135 decision

The complete selected non-Tektronix xterm dynamic-color family is now exposed through one semantic public API.

T136 can therefore evaluate scoped restoration uniformly across indexed palette and dynamic colors without further expanding the color identity surface.
