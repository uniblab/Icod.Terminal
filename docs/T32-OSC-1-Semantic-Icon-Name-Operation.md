# T32 — OSC 1 Semantic Icon-Name Operation

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Development version:** `0.4.0-alpha.4`  
**Tranche:** T32  
**Theme:** OSC 1 icon-name-only semantic operation  
**Predecessor:** T31 — OSC 0 semantic title operation

---

## 1. Objective

T32 adds the public semantic OSC 1 operation while preserving every T29/T30 safety and framing invariant and every T31 endpoint/cancellation rule.

The public operation is:

```csharp
await session.SetIconNameAsync(
	"icon"
);
```

Its wire form is:

```text
ESC ] 1 ; Pt ESC \
```

where `Pt` is validated and strict UTF-8 encoded by the shared internal `OscWriter`.

---

## 2. Public Contract

`TerminalSession.SetIconNameAsync(...)`:

- expresses OSC 1 semantics only;
- does not alter or imply OSC 0 behavior;
- does not expose a raw OSC selector;
- does not expose a general-purpose OSC API;
- is emission-oriented rather than terminal-state-oriented;
- does not claim that a terminal actually displays or stores an icon name;
- does not claim exact restoration of any prior icon-name state.

T32 deliberately keeps OSC 0 and OSC 1 semantically distinct at the public API level.

---

## 3. Shared Implementation Path

T31 and T32 now share one private `TerminalSession` helper which:

1. validates the managed argument boundary;
2. observes caller cancellation before transmission;
3. rejects a session whose observed output endpoint is known not to be a terminal;
4. delegates complete frame validation and encoding to `OscWriter`;
5. emits the complete frame through the established output path.

The only intentional semantic difference between the two operations is the frozen OSC selector:

```text
SetTitleAsync     -> selector 0
SetIconNameAsync  -> selector 1
```

No duplicate OSC framing or validation implementation is introduced.

---

## 4. Safety and Resource Rules

T32 inherits the frozen T29/T30 rules unchanged:

- canonical 7-bit `ESC ]` OSC introducer;
- canonical `ESC \\` ST terminator;
- strict UTF-8 payload encoding;
- rejection of C0, DEL, and C1 controls;
- rejection of ill-formed Unicode input;
- 4096 encoded-byte payload ceiling;
- complete validation before any output;
- no public terminator override;
- no 8-bit C1 OSC/ST emission;
- no BEL-terminated output mode.

---

## 5. Endpoint and Cancellation Semantics

A known non-terminal output endpoint is rejected before any OSC bytes are emitted.

Cancellation observed before transmission writes nothing.

Once the complete validated frame is submitted to the output transport, ordinary caller cancellation is not used to deliberately abandon the frame midway through transmission.

These semantics intentionally match T31.

---

## 6. Test Coverage

T32 adds deterministic in-memory coverage for:

- empty OSC 1 payload;
- ASCII icon name;
- multilingual UTF-8 icon name;
- invalid/control-character payload rejection with zero writes;
- redirected/non-terminal output rejection with zero writes;
- cancellation before transmission with zero writes.

The existing T30 writer tests remain the byte-boundary and validation authority for the shared 4096-byte ceiling and complete framing behavior.

Normal automated tests do not change the host terminal's icon name.

---

## 7. Non-Goals

T32 does not add:

- OSC 2 window-title-only operation;
- title stack push/pop;
- title or icon-name queries;
- arbitrary public OSC emission;
- OSC 7, OSC 8, or OSC 52;
- restoration of an unknown prior icon name.

---

## 8. Completion Gate

T32 is complete when:

1. `TerminalSession.SetIconNameAsync(...)` emits OSC 1 through the shared T30 writer;
2. OSC 0 and OSC 1 remain distinct semantic public operations;
3. no OSC framing or validation logic is duplicated;
4. T29 payload safety and resource limits remain unchanged;
5. known non-terminal output is rejected with zero output;
6. cancellation before transmission writes nothing;
7. deterministic tests cover the new semantic operation across all supported TFMs;
8. the package version advances to `0.4.0-alpha.4` while assembly version remains `0.4.0.0`.

The next tranche is T33 — OSC 2 window-title-only semantic operation.
