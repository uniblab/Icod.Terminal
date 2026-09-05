# T55 — OSC 52 Inbound Framing and Routing

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.4`  
**Status:** Implemented  
**Predecessor:** T54 — outbound OSC 52 writer

---

## 1. Purpose

T55 extends the existing session-owned single-reader response-routing substrate so a bounded OSC 52 response can be recognized, correlated, and routed without introducing another terminal reader.

This tranche does not expose the public clipboard-read API. It provides the internal framing and correlation machinery required by T57.

---

## 2. Shared response family

`TerminalResponseFrameKind` now includes `Osc` alongside the existing `Csi` and `Dcs` families.

`TerminalResponseFramer` remains the single generic response-framing service. OSC support is expectation-driven: OSC bytes are treated as an internal response candidate only while an OSC response matcher is active.

No background OSC listener and no independent OSC input loop are introduced.

---

## 3. Accepted inbound OSC framing

The T52 compatibility policy is implemented exactly for OSC response framing:

```text
ESC ] ... BEL
ESC ] ... ESC \\
0x9D ... 0x9C
```

Outbound OSC 52 remains canonical seven-bit `ESC ] ... ESC \\`; the broader forms above are inbound compatibility only.

The framer rejects mixed termination forms such as:

- C1 OSC terminated by BEL;
- C1 OSC terminated by seven-bit `ESC \\`;
- seven-bit OSC terminated by bare C1 ST;
- unexpected embedded Escape sequences;
- CAN/SUB-aborted strings.

---

## 4. Resource bounds

T52 froze the **complete OSC 52 frame ceiling** at 87,400 bytes. That protocol limit remains unchanged, and the shared response framer uses 87,400 bytes as its hard framing ceiling.

The session's **undecoded input-buffer ceiling** is also **87,400 bytes**. The transport buffer and the maximum complete OSC 52 frame therefore share one explicit bound:

```text
Maximum decoded OSC 52 payload:       65,536 bytes
Maximum encoded OSC 52 payload:       87,384 bytes
Maximum complete OSC 52 frame:        87,400 bytes
Maximum undecoded terminal buffer:    87,400 bytes
```

A maximum legal OSC 52 response fits within that bounded decoder capacity. If a structurally correlated OSC 52 candidate reaches 87,400 bytes without completing, it is treated as an oversized response owned by the active transaction. The query fails deterministically with `FormatException`; the decoder drains the invalid OSC control string through a recognized control-string terminator so the oversized response cannot fall through into ordinary keyboard/application input.

The discard/resynchronization path is itself bounded. If the terminal fails to provide a terminator within one additional frame-sized discard interval, the input decoder terminates rather than resuming ordinary input from an untrusted point inside an unterminated OSC string.

---

## 5. OSC 52 correlation

`TerminalOsc52Protocol` introduces an internal selection-aware matcher.

A complete candidate satisfies an active OSC 52 expectation only when all of the following are true:

1. the frame is an OSC frame;
2. the OSC selector is exactly `52`;
3. the selection is exactly the requested frozen single-selection target;
4. the selection field contains one target only;
5. the payload is canonical bounded RFC 4648 base64;
6. the payload decodes to no more than 65,536 bytes;
7. the complete frame uses one accepted T55 terminator form.

Wrong selections, unrelated OSC selectors, malformed base64, non-canonical base64, multi-selection fields, and oversized payloads do not produce successful query results.

For resource-failure ownership, the OSC 52 matcher can also recognize the fixed correlated prefix before a complete frame exists. That extra internal correlation is used only when the candidate reaches the framing ceiling, allowing an oversized response for the active selection to fail the transaction rather than leak into ordinary input.

---

## 6. Decoder integration

`TerminalInputDecoder.TryRouteExpectedResponseAsync(...)` continues to run before ordinary key/mouse/fallback decoding.

For CSI and DCS expectations the existing 4,096-byte response-framing default remains in force.

For OSC expectations the routing path uses the 87,400-byte OSC 52 maximum complete-frame bound, and the decoder itself is capped at the same 87,400-byte value. This keeps the OSC resource invariant singular without widening CSI/DCS transaction behavior.

An unrelated or wrong-selection OSC response is consumed only if the active matcher accepts it under the established routing rules. A structurally correlated OSC 52 response which reaches the hard frame ceiling is instead owned as an explicit failure and drained to a terminator before ordinary input decoding resumes.

---

## 7. Exact payload parsing

`TerminalOsc52Protocol.ParsePayload(...)` extracts only a correlated OSC 52 payload and delegates canonical base64 decoding to the T53 codec.

The parser therefore does not introduce a second interpretation of padding, alphabet, whitespace, unused bits, or payload size.

---

## 8. Tests

T55 adds deterministic terminal-I/O simulation covering:

- seven-bit ST termination;
- seven-bit BEL termination;
- C1 OSC/ST termination;
- rejection of mixed framing forms;
- exact selection correlation;
- wrong-selection rejection;
- unrelated OSC rejection;
- malformed/non-canonical base64 rejection;
- payload extraction;
- arbitrary one-byte fragmentation across the shared decoder path;
- C1 routing through the shared decoder path;
- exact maximum 65,536-byte decoded payload routing;
- rejection when an OSC candidate reaches the 87,400-byte complete-frame ceiling without a terminator;
- deterministic `FormatException` ownership for an oversized correlated OSC 52 response;
- draining that oversized response through its terminator while preserving trailing ordinary input;
- rejection when decoder configuration exceeds the 87,400-byte input-buffer ceiling.

---

## 9. T55 gate

T55 is complete when:

1. OSC is a first-class internal response-framing family;
2. inbound BEL/ST/C1 forms follow the T52 policy;
3. OSC 52 responses are selection-correlated before successful consumption;
4. malformed or unrelated OSC cannot become a successful query result;
5. maximum legal clipboard payloads fit within the bounded 87,400-byte shared decoder buffer;
6. OSC framing and undecoded buffering are both capped at 87,400 bytes;
7. oversized correlated OSC 52 responses fail deterministically and cannot leak into ordinary input;
8. fragmented OSC responses route through the existing single-reader path;
9. no second terminal reader or background monitor exists;
10. Windows, Linux, and macOS CI are green.

The next tranche is **T56 — semantic clipboard write API**.
