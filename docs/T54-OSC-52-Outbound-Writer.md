# T54 — OSC 52 Outbound Writer

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.3`  
**Status:** Implemented  
**Depends on:** T52 contract freeze; T53 selection and payload primitives

---

## 1. Purpose

T54 implements the outbound OSC 52 framing primitive required by the frozen 0.7 contract.

The tranche remains internal. It does not introduce the public clipboard API, response routing, or query transaction semantics owned by later tranches.

---

## 2. Canonical write framing

`OscWriter.EncodeOsc52WriteFrame(...)` produces exactly:

```text
ESC ] 52 ; <selection> ; <canonical-base64> ESC \
```

The selection byte is obtained only through the T53 typed mapping:

```text
Clipboard  -> c
Primary    -> p
Secondary  -> q
Select     -> s
```

The payload is encoded by `TerminalOsc52PayloadCodec`, preserving the T53 canonical RFC 4648 and resource-limit rules.

An empty byte payload produces an empty payload field and therefore expresses the T52 semantic clear/set-empty operation without any raw protocol escape hatch.

---

## 3. Canonical query framing

`OscWriter.EncodeOsc52QueryFrame(...)` produces exactly:

```text
ESC ] 52 ; <selection> ; ? ESC \
```

Only canonical seven-bit `ESC ]` introduction and `ESC \\` termination are emitted.

BEL and C1 forms remain inbound compatibility concerns for T55; T54 never emits them.

---

## 4. Validation and allocation boundary

A write frame is not allocated until:

1. the typed selection is validated;
2. the decoded payload length is accepted by the T53 limit;
3. the exact encoded length is calculated;
4. the exact complete write-frame size is checked.

The maximum decoded payload remains 65,536 bytes. Its canonical write frame is 87,393 bytes, below the frozen 87,400-byte complete-frame ceiling.

A one-byte-over decoded payload is rejected before a complete OSC frame is created or any terminal output occurs.

---

## 5. Output commit semantics

`WriteOsc52Async(...)` and `WriteOsc52QueryAsync(...)` follow the established OSC writer contract:

- validate the complete operation first;
- observe caller cancellation before transmission commits;
- emit the complete frame through one `ITerminalOutput.WriteAsync(...)` call;
- pass `CancellationToken.None` after commit so caller cancellation cannot intentionally abandon an OSC frame partway through;
- do not flush implicitly;
- propagate output failures without retry.

The query writer is deliberately only a wire primitive. T57 will invoke it from the established query transaction substrate, which owns request registration, serialization, flushing where required, timeout, cancellation, and late-response ownership.

---

## 6. Source organization

`OscWriter` is now partial so protocol-family-specific writer code can remain focused without duplicating the shared OSC abstraction.

T54 adds:

```text
src/Output/OscWriter.Osc52.cs
```

Existing OSC 0/1/2, OSC 7, and OSC 8 behavior remains in `OscWriter.cs` unchanged apart from the class becoming `partial`.

---

## 7. Test coverage

`Osc52WriterTests` proves:

- exact canonical write bytes for all four typed selections;
- exact canonical query bytes for all four typed selections;
- empty-payload framing;
- arbitrary binary payload base64 framing;
- exact-maximum payload frame size;
- one-byte-over rejection;
- invalid-selection rejection;
- one complete output write per operation;
- no implicit flush;
- cancellation before commit produces zero protocol output;
- invalid or oversized input produces zero protocol output;
- transport failures propagate without retry.

T53's xUnit accessibility defect was also corrected: public theory signatures no longer expose the internal `TerminalOsc52Selection` type.

---

## 8. T54 gate

T54 is complete when CI proves that:

1. write frames are byte-exact and canonically ST-terminated;
2. query frames are byte-exact and use literal `?`;
3. all input is validated before terminal output;
4. oversized input produces zero protocol output;
5. complete frames are emitted in one write;
6. ordinary writer operations do not flush;
7. caller cancellation before commit produces zero output;
8. existing OSC writer families remain green across `net8.0`, `net9.0`, and `net10.0`.

The next tranche is **T55 — inbound OSC framing and routing**.
