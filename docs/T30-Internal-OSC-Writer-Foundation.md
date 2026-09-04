# T30 — Internal OSC Writer Foundation

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Development version:** `0.4.0-alpha.2`  
**Tranche:** T30 — Internal OSC writer foundation  
**Predecessor:** T29 — OSC 0 / 1 / 2 contract and reference freeze  
**Status:** Implemented

---

## 1. Purpose

T30 implements the internal OSC writer required by the T29 contract without yet exposing public title APIs.

The tranche provides one reusable internal path for constructing and emitting OSC 0, OSC 1, and OSC 2 title frames. T31-T33 will layer the semantic title operations on this foundation.

---

## 2. Internal implementation

The new internal `OscWriter` owns:

- canonical 7-bit OSC introducer bytes `ESC ]`;
- selectors 0, 1, and 2 only;
- the required semicolon separator;
- strict UTF-8 title payload encoding;
- the T29 4096-byte encoded payload ceiling;
- rejection of C0, DEL, and C1 controls;
- rejection of ill-formed managed Unicode input;
- canonical `ESC \\` ST termination;
- validation-before-write behavior;
- complete-frame, single-call output emission.

`OscTitleSelector` is internal and intentionally contains only the three selectors approved for the 0.4.0 title family.

No public raw selector or generic OSC API is added.

---

## 3. Complete-frame construction

`OscWriter.EncodeTitleFrame(...)` validates the selector and complete managed payload before allocating and returning one complete OSC frame.

The resulting byte layout is:

```text
ESC ] selector ; payload ESC \
```

The fixed frame overhead is six bytes:

```text
1B 5D Ps 3B ... 1B 5C
```

where `Ps` is the ASCII byte for `0`, `1`, or `2`.

The 4096-byte limit applies only to the encoded UTF-8 payload and therefore permits a maximum complete frame size of 4102 bytes.

---

## 4. Validation-before-write

The writer enforces the T29 zero-partial-output rule.

Before any write begins it validates:

1. the output object where applicable;
2. the title value;
3. selector membership in OSC 0/1/2;
4. absence of C0, DEL, and C1 controls;
5. strict UTF-8 validity;
6. encoded payload length.

Any validation failure occurs before the output service is called.

Tests explicitly verify that rejected input produces zero writes and zero bytes.

---

## 5. Cancellation and frame integrity

`WriteTitleAsync(...)` observes caller cancellation before transmission is committed.

Once the complete frame has been validated and the final pre-write cancellation check passes, the writer submits the complete frame to `ITerminalOutput.WriteAsync(...)` in one call using a non-cancellable token.

This implements the T29 rule that ordinary caller cancellation before transmission emits nothing, while cancellation is not deliberately used to abandon an OSC frame after transmission begins.

This does not claim that every physical output implementation can provide transactional hardware writes. It guarantees that `Icod.Terminal` does not intentionally split or cancel the frame internally.

Broader serialization among terminal operations remains T34 work.

---

## 6. Byte-exact test coverage

T30 adds deterministic in-memory tests covering:

- OSC 0 empty payload;
- OSC 0 ASCII;
- OSC 1 ASCII;
- OSC 2 ASCII;
- multilingual strict UTF-8;
- exactly 4096 encoded payload bytes;
- rejection above 4096 encoded payload bytes;
- NUL rejection;
- BEL rejection;
- TAB/LF/CR rejection;
- ESC rejection;
- DEL rejection;
- C1 rejection, including ST-range input;
- unpaired surrogate rejection;
- unsupported selector rejection;
- zero writes for rejected payloads;
- one output call for one complete valid frame;
- cancellation before transmission producing zero output.

The tests use injected output and do not alter the host terminal title.

---

## 7. Deliberate boundaries

T30 does not add:

- public title methods;
- support/capability policy on `TerminalSession`;
- redirected-output policy;
- OSC 7, OSC 8, or OSC 52;
- title query or stack support;
- a public generic `SendOsc(...)` API;
- global serialization of application writes and control operations.

Those responsibilities remain assigned to T31-T35 as defined by the 0.4.0 roadmap.

---

## 8. Completion gate

T30 is complete when:

- one internal writer constructs all three approved OSC title selectors;
- framing is byte-exact and uses only the frozen `ESC ] ... ESC \\` form;
- UTF-8 and resource-limit rules match T29;
- forbidden input cannot inject a second terminal sequence;
- every validation failure occurs before output;
- a valid frame is submitted in one output call;
- the writer remains internal and does not freeze future OSC 7/8/52 public API shape.

The resulting writer is the implementation foundation for T31 — OSC 0 semantic operation.
