# T31 — OSC 0 Semantic Title Operation

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Development version:** `0.4.0-alpha.3`  
**Tranche:** T31  
**Theme:** semantic OSC 0 icon-and-window-title operation  
**Predecessor:** T30 — internal OSC writer foundation

---

## 1. Purpose

T31 layers the first public semantic title operation on the internal OSC writer frozen and implemented by T29/T30.

The operation sets both icon name and window title using OSC selector 0 without exposing the selector itself to public callers.

T31 deliberately does not implement OSC 1 or OSC 2. Those remain T32 and T33 so each semantic operation can be reviewed independently while sharing the same internal writer.

---

## 2. Public operation

`TerminalSession.SetTitleAsync(...)` is introduced as the OSC 0 semantic operation.

Its contract is emission-oriented:

- a valid call emits exactly one complete OSC 0 frame;
- successful completion means that the complete frame was accepted by the session output service;
- successful completion does not prove that the terminal applied the requested title;
- the method does not maintain a terminal-emulator title state;
- it does not promise restoration of a previous title.

The wire form remains the T29/T30 frozen form:

```text
ESC ] 0 ; Pt ESC \
```

---

## 3. Payload behavior

T31 reuses the T30 writer without duplicating validation logic.

Therefore the public semantic operation inherits the frozen rules:

- strict UTF-8 encoding;
- empty payload permitted;
- C0 controls rejected;
- DEL rejected;
- C1 controls rejected;
- ill-formed managed Unicode rejected;
- payload limited to 4096 encoded UTF-8 bytes;
- all validation completes before output begins;
- rejected input writes zero bytes.

T31 does not introduce a public size override or sanitization mode.

---

## 4. Endpoint policy

OSC title output is a terminal-control operation, not ordinary redirected application text.

When `TerminalSession.OutputObservation.IsTerminal` is false, `SetTitleAsync(...)` rejects the operation with `InvalidOperationException` and writes no OSC bytes, even when the session itself was explicitly opened with `RequireInteractiveOutput = false`.

This preserves the T29 rule that terminal-control bytes are not silently emitted to an endpoint already known to be unsuitable.

For an interactive output endpoint whose actual OSC 0 support is unknown, T31 permits optimistic emission. Successful transmission is not represented as proof of terminal support.

---

## 5. Cancellation and output failure

T31 preserves the T30 transmission-integrity rule:

- cancellation observed before transmission emits nothing;
- once a complete validated frame is committed to `ITerminalOutput.WriteAsync`, ordinary caller cancellation is not used to deliberately abandon the frame mid-write;
- transport exceptions propagate to the caller and remain distinguishable from input-validation failures.

T34 will later define cross-operation serialization and broader session-output ordering. T31 does not preempt that tranche.

---

## 6. Test coverage

T31 adds deterministic in-memory tests for:

- empty OSC 0 payload;
- ASCII OSC 0 payload;
- multilingual UTF-8 payload;
- 4096-byte boundary payload;
- rejected control/injection payload with zero output;
- redirected/non-terminal output rejection with zero output;
- propagated output failure;
- cancellation before transmission with zero output.

No ordinary test changes the CI runner's real terminal title.

---

## 7. Scope exclusions

T31 does not implement:

- OSC 1 icon-name-only operation;
- OSC 2 window-title-only operation;
- title queries;
- title stack push/pop;
- raw public OSC emission;
- BEL termination;
- OSC 7, OSC 8, or OSC 52;
- output-operation serialization beyond the single-frame T30 write;
- title restoration state.

---

## 8. Completion gate

T31 is complete when:

1. `TerminalSession.SetTitleAsync(...)` emits OSC 0 through the internal T30 writer;
2. no public raw selector or raw OSC API is introduced;
3. byte-exact output is deterministic for empty, ASCII, and multilingual text;
4. the 4096-byte boundary is accepted;
5. invalid payloads emit zero bytes;
6. known non-terminal output endpoints reject title operations without writing;
7. output failures propagate without being confused with validation failures;
8. cancellation before transmission writes nothing;
9. the implementation is ready for T32 to add OSC 1 by reusing the same writer.
