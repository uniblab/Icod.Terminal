# T82 — Typed Cursor-Style Codec and DECRQSS Interpretation

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.2`  
**Status:** Implemented; CI validation pending  
**Predecessor:** T81 — reusable CSI intermediate-byte output primitive  

## 1. Purpose

T82 turns the frozen T80 cursor-style semantics into executable typed code without adding terminal I/O or public session operations.

The tranche introduces a closed semantic cursor-style type and an internal codec for mapping between that type and DECSCUSR / DECRQSS cursor-style representations.

## 2. Public semantic type

The public candidate type is now:

```csharp
public enum TerminalCursorStyle {
    BlinkingBlock,
    SteadyBlock,
    BlinkingUnderline,
    SteadyUnderline,
    BlinkingBar,
    SteadyBar
}
```

No `Default`, `Reset`, `Initial`, `Restore`, `Hidden`, `Visible`, `Raw`, or `Unknown` member is introduced.

## 3. Outbound semantic mapping

The internal codec freezes the semantic-to-DECSCUSR parameter mapping:

| Style | `Ps` |
| --- | ---: |
| `BlinkingBlock` | `1` |
| `SteadyBlock` | `2` |
| `BlinkingUnderline` | `3` |
| `SteadyUnderline` | `4` |
| `BlinkingBar` | `5` |
| `SteadyBar` | `6` |

Undefined enum values fail with `ArgumentOutOfRangeException` before any later output operation can commit protocol bytes.

## 4. Inbound status-string grammar

The typed parser accepts only a DECSCUSR status string ending in:

```text
SP q
```

with either no numeric parameter or exactly one decimal parameter before the intermediate/final pair.

Recognized mappings are:

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

Leading decimal zeroes are accepted because the protocol field is numeric rather than a canonical textual serialization.

## 5. Deterministic rejection

The parser throws `FormatException` for:

- wrong or missing `SP q` identifier;
- multiple parameters;
- private markers or signed/non-decimal data;
- extra intermediate bytes;
- xterm command value `7`;
- values outside the frozen semantic set;
- numeric overflow.

`ArgumentNullException` remains the parameter-validation result for a null status-string argument.

## 6. Terminal-I/O boundary

T82 does not add:

- a session setter;
- a session query method;
- a cursor-style observation result;
- a lease;
- automatic probing;
- new response routing.

T83 will consume the outbound codec together with the T81 CSI writer. T84 will layer typed observation over the existing `QueryStatusStringAsync(TerminalStatusStringKind.CursorStyle, ...)` transaction path.

## 7. Tests

`TerminalCursorStyleCodecTests` proves:

- all six semantic-to-parameter mappings;
- undefined enum rejection;
- omitted, `0`, and `1` blinking-block aliases;
- leading-zero forms;
- all recognized `2` through `6` states;
- malformed identifier rejection;
- multi-parameter rejection;
- private/signed/non-decimal rejection;
- xterm `7` rejection;
- unknown-value rejection;
- numeric-overflow rejection;
- null argument validation.

## 8. Gate T82

T82 is complete when:

1. the six-value semantic type exists;
2. style-to-parameter mapping exactly matches T80;
3. typed status parsing recognizes only the frozen semantic states;
4. omitted/`0`/`1` normalize to blinking block;
5. xterm `7`, unknown values, and malformed forms fail deterministically;
6. the codec performs no terminal I/O;
7. cross-platform CI is green.

The next tranche is **T83 — semantic cursor-style set API**.
