# T102 — Progress Value Model and Stage Conversion

**Release:** `0.10.0`  
**Tranche:** `T102`  
**Development version:** `0.10.0-alpha.3`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T102 adds the typed semantic progress state and completed/total conversion layer above the byte-exact OSC 9;4 writer introduced by T101.

No session manager or public lease is added in this tranche.

---

## 2. Public semantic determinate state

T102 introduces:

```csharp
public enum TerminalProgressState {
	Normal,
	Error,
	Attention
}
```

`Attention` remains the neutral semantic name frozen by T100 for OSC 9;4 wire state 4, which Windows Terminal describes as warning and ConEmu describes as paused.

`Clear` and `Indeterminate` are not public determinate-state enum members:

- clear is a cleanup/physical-state operation;
- indeterminate is a distinct progress mode.

---

## 3. Internal logical value

`TerminalProgressValue` represents one validated logical progress value in either form:

```text
Determinate(state, completed, total, percentage)
Indeterminate
```

Determinate values retain the original `long completed` and `long total` inputs together with the computed wire percentage so later ownership layers can preserve semantic state without recomputing or losing caller intent.

---

## 4. Validation

Determinate progress requires:

```text
total > 0
completed >= 0
completed <= total
state in { Normal, Error, Attention }
```

Invalid values throw before any wire conversion or output is attempted.

---

## 5. Percentage conversion

T102 converts completed/total to the integer OSC 9;4 percentage range using integer arithmetic only.

The implementation uses `UInt128` internally after validating the signed `long` inputs:

```text
numerator   = completed * 100
quotient    = numerator / total
remainder   = numerator % total
round up if remainder * 2 >= total
```

This satisfies the frozen T100 requirements:

- no floating-point arithmetic;
- nearest-integer percentage;
- exact half cases round upward;
- no overflow for any valid nonnegative `long` completed/total inputs;
- deterministic 0–100 result.

Examples:

```text
0 / 10 -> 0%
1 / 10 -> 10%
2 / 10 -> 20%
1 / 3  -> 33%
2 / 3  -> 67%
3 / 3  -> 100%
1 / 8  -> 13%
3 / 8  -> 38%
5 / 8  -> 63%
7 / 8  -> 88%
1 / 200 -> 1%
```

The implementation is also tested with `long.MaxValue` workloads.

---

## 6. Wire-state mapping

Determinate semantic states map internally as:

```text
Normal    -> OSC 9;4 state 1
Error     -> OSC 9;4 state 2
Attention -> OSC 9;4 state 4
```

Indeterminate maps to wire state 3 with canonical percentage 0.

---

## 7. Tests

`TerminalProgressValueTests` covers:

- exact 0%, 10%, 20%, 33%, 67%, and 100% examples;
- uneven eighths and nearest-integer rounding;
- exact-half rounding upward;
- `long.MaxValue` completed/total without overflow;
- negative completed rejection;
- nonpositive total rejection;
- completed greater than total rejection;
- invalid semantic enum rejection;
- semantic-to-wire-state mapping;
- canonical indeterminate value.

---

## 8. Scope boundary

T102 does not add:

- a session progress manager;
- nested ownership;
- lifecycle participation;
- a public progress lease;
- support probing;
- direct public setters;
- OSC 9;9.

Those remain assigned to T103–T106.

---

## 9. T102 decision

T102 is implemented with the semantic determinate-state enum and an overflow-safe integer progress-value conversion layer. Exact-head validation is required before T103 begins.
