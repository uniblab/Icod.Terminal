# T122 — OSC 133 Semantic Marker Model

**Release:** `0.12.0`  
**Tranche:** `T122`  
**Development version:** `0.12.0-alpha.3`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T122 adds the typed semantic value layer between the frozen T120 OSC 133 contract and the byte-exact T121 writer.

This tranche does not add public `TerminalSession` marker methods. Those remain assigned to T124 after T123 establishes the session serialization integration.

---

## 2. Internal semantic vocabulary

T122 introduces exactly five internal semantic marker kinds:

```text
PromptStart
CommandInputStart
CommandOutputStart
CommandFinished
CommandAborted
```

These names preserve the T120 semantic distinctions without exposing the protocol letters `A`, `B`, `C`, or `D` outside the wire layer.

The marker enum begins at numeric value 1 deliberately. A default-initialized `TerminalSemanticPromptMarker` therefore has an invalid kind and is rejected instead of accidentally becoming a prompt-start marker.

---

## 3. Typed completion representation

`TerminalSemanticPromptMarker` is an immutable internal value which carries:

- one semantic marker kind;
- a `byte` exit status only when the kind is `CommandFinished`.

The exact T120 status domain is therefore represented directly without widening or narrowing conversion:

```text
0..255
```

No additional public command-completion wrapper is introduced in T122. `byte` already expresses the complete portable status domain, and the frozen T120 public operation model uses separate finish and abort operations.

---

## 4. Abort is not nullable completion

T122 preserves the strongest T120 distinction:

```text
CommandFinished(0) -> OSC 133 ; D ; 0 ST
CommandAborted     -> OSC 133 ; D ST
```

An aborted marker reports `HasExitStatus == false` and attempting to read `ExitStatus` from a non-completion marker throws `InvalidOperationException`.

The implementation does not use a nullable exit status to encode both completion and abort.

---

## 5. Exhaustive mapping to T121

`TerminalSemanticPromptMarkerCodec` maps every semantic kind through the specialized T121 byte-exact writer:

```text
PromptStart        -> A
CommandInputStart  -> B
CommandOutputStart -> C
CommandFinished    -> D;status
CommandAborted     -> D
```

The codec accepts no raw marker character, raw OSC 133 payload, arbitrary property string, or metadata dictionary.

Undefined/default marker values are rejected before output.

---

## 6. Commit behavior

Semantic writes delegate to the established T121 writer and therefore retain its transaction guarantees:

1. semantic marker validation occurs before output;
2. caller cancellation is observed before commit;
3. one complete OSC 133 frame is written;
4. the committed transport write uses `CancellationToken.None`;
5. no implicit flush occurs.

T122 adds no compensating markers and no marker-history state machine.

---

## 7. Ordering posture

T122 intentionally does not encode legal/illegal A→B→C→D history.

The T120 freeze makes semantic marker operations independently callable because prompts may redraw, integration may begin mid-region, and recovery/multiplexer/subshell scenarios can make the complete terminal history unknowable to one `TerminalSession` instance.

T123 therefore should integrate deterministic session output serialization around these typed values, not introduce a synthetic shell-history state machine.

---

## 8. Tests

`TerminalSemanticPromptMarkerTests` proves:

- exactly five semantic marker kinds exist;
- every kind maps to its exact canonical T121 frame;
- command completion retains byte statuses including `0` and `255`;
- abort is distinct from status `0` completion;
- non-completion markers expose no synthetic exit status;
- default/uninitialized marker values are rejected;
- semantic writes preserve the T121 non-cancellable committed write and no-flush behavior;
- pre-cancelled semantic writes emit nothing.

---

## 9. Public-surface posture

T122 adds no public raw OSC 133 strings and no public generic marker enum.

This is deliberate. T124 will expose the frozen semantic operations directly, equivalent to:

```text
BeginPromptAsync
BeginCommandInputAsync
BeginCommandOutputAsync
FinishCommandAsync(byte)
AbortCommandAsync
```

Keeping the T122 marker representation internal prevents the implementation transport/value model from becoming an unnecessary public compatibility commitment.

---

## 10. T122 decision

The OSC 133 semantic marker model is now strongly typed, exhaustive, default-safe, and preserves the exact distinction between abort and successful completion.

T123 may now integrate these values into `TerminalSession` output serialization while preserving the T120 independently-callable ordering contract.
