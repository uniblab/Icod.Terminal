# T101 — OSC 9;4 Byte-Exact Writer

**Release:** `0.10.0`  
**Tranche:** `T101`  
**Development version:** `0.10.0-alpha.2`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T101 implements the internal byte-exact OSC 9;4 progress framing primitive frozen by T100.

The implementation remains internal. No public OSC 9 API or generic OSC construction surface is introduced.

---

## 2. Internal wire state

T101 introduces the internal wire enum:

```csharp
internal enum Osc9ProgressState {
	Clear = 0,
	Normal = 1,
	Error = 2,
	Indeterminate = 3,
	Attention = 4
}
```

This is deliberately broader than the future public determinate-state enum because the wire layer must represent cleanup and indeterminate frames directly.

---

## 3. Canonical encoder

`OscWriter.EncodeOsc9ProgressFrame(...)` produces exactly:

```text
ESC ] 9 ; 4 ; <state> ; <progress> BEL
```

with:

- canonical seven-bit `ESC ]` OSC introducer;
- ASCII decimal state digit `0` through `4`;
- ASCII decimal progress `0` through `100`;
- no leading zeroes;
- canonical BEL termination;
- no caller-supplied framing bytes.

Representative outputs:

```text
Clear          -> ESC ] 9 ; 4 ; 0 ; 0 BEL
Normal 42%     -> ESC ] 9 ; 4 ; 1 ; 42 BEL
Error 42%      -> ESC ] 9 ; 4 ; 2 ; 42 BEL
Indeterminate  -> ESC ] 9 ; 4 ; 3 ; 0 BEL
Attention 75%  -> ESC ] 9 ; 4 ; 4 ; 75 BEL
```

---

## 4. Validation

The encoder rejects:

- wire states outside `0` through `4`;
- progress less than `0`;
- progress greater than `100`;
- nonzero progress paired with `Clear`;
- nonzero progress paired with `Indeterminate`.

The last two rules preserve the T100 canonical representation for those non-determinate wire states.

---

## 5. Commit and cancellation semantics

`OscWriter.WriteOsc9ProgressAsync(...)` follows the established terminal-control commit pattern:

1. validate the output service;
2. observe caller cancellation before encoding;
3. construct and validate the complete frame;
4. observe caller cancellation a second time before transmission commit;
5. issue exactly one `ITerminalOutput.WriteAsync(...)` call;
6. pass `CancellationToken.None` to that committed transport write;
7. perform no implicit flush.

Therefore caller cancellation cannot deliberately truncate a committed OSC frame.

---

## 6. Tests

`Osc9ProgressWriterTests` covers:

- exact clear frame;
- normal progress at 0, 9, 10, 99, and 100;
- exact error frame;
- exact indeterminate frame;
- exact attention frame;
- invalid wire state rejection;
- progress below 0 and above 100;
- rejection of noncanonical clear/indeterminate percentages;
- exactly one committed transport write;
- zero implicit flushes;
- non-cancellable committed write token;
- pre-cancelled operation emits nothing;
- invalid arguments emit nothing.

---

## 7. Scope boundary

T101 does not implement:

- completed/total stage conversion;
- public `TerminalProgressState`;
- progress ownership/nesting;
- session lifecycle integration;
- support probing;
- OSC 9;9;
- generic OSC 9 output.

Those responsibilities remain assigned to T102–T106.

---

## 8. T101 decision

T101 is implemented with a specialized internal OSC 9;4 writer and byte-exact acceptance tests. Exact-head validation is required before T102 begins.
