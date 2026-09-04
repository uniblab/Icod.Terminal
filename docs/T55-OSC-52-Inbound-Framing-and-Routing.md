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

## 4. Resource bound

T52 froze a complete OSC 52 frame ceiling of 87,400 bytes. T55 therefore raises the shared response-framer hard ceiling to that same value.

The session's public undecoded-input ceiling is also raised from the earlier 4,096-byte value to 87,400 bytes so one maximum legal OSC 52 response can be retained and correlated without violating the decoder's bounded-buffer invariant.

This is a bounded-capacity change, not unbounded buffering. The underlying decoder continues to constrain every read to remaining buffer capacity and rejects further growth at the ceiling.

The 65,536-byte decoded clipboard payload ceiling remains unchanged.

---

## 5. OSC 52 correlation

`TerminalOsc52Protocol` introduces an internal selection-aware matcher.

A candidate satisfies an active OSC 52 expectation only when all of the following are true:

1. the frame is an OSC frame;
2. the OSC selector is exactly `52`;
3. the selection is exactly the requested frozen single-selection target;
4. the selection field contains one target only;
5. the payload is canonical bounded RFC 4648 base64;
6. the payload decodes to no more than 65,536 bytes;
7. the complete frame uses one accepted T55 terminator form.

Wrong selections, unrelated OSC selectors, malformed base64, non-canonical base64, multi-selection fields, and oversized payloads do not satisfy the expectation.

---

## 6. Decoder integration

`TerminalInputDecoder.TryRouteExpectedResponseAsync(...)` continues to run before ordinary key/mouse/fallback decoding.

For CSI and DCS expectations the existing 4,096-byte response-framing default remains in force.

For OSC expectations the routing path uses the OSC 52 maximum complete-frame bound. This avoids widening existing CSI/DCS transaction behavior merely because OSC 52 requires larger bounded payloads.

An OSC response is consumed only after the active matcher accepts it. A rejected candidate remains on the ordinary application-input path under the same ownership rules established by T22/T23.

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
- rejection when an OSC candidate reaches the complete-frame ceiling without a terminator.

---

## 9. T55 gate

T55 is complete when:

1. OSC is a first-class internal response-framing family;
2. inbound BEL/ST/C1 forms follow the T52 policy;
3. OSC 52 responses are selection-correlated before consumption;
4. malformed or unrelated OSC cannot satisfy the expectation;
5. maximum legal clipboard payloads fit within the bounded shared decoder;
6. fragmented OSC responses route through the existing single-reader path;
7. no second terminal reader or background monitor exists;
8. Windows, Linux, and macOS CI are green.

The next tranche is **T56 — semantic clipboard write API**.
