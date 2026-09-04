# T58 — OSC 52 Integration, Security, and Compatibility Acceptance

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.7`  
**Status:** Implemented  
**Predecessor:** T57 — explicit clipboard read/query API

---

## 1. Purpose

T58 is the acceptance tranche for the 0.7 OSC 52 work. It does not intentionally widen the public clipboard API. Instead it verifies that T52 through T57 compose safely with the rest of the terminal session model and that OSC 52 does not weaken established input, output, lifecycle, or query ownership guarantees.

---

## 2. Injection matrix

Clipboard write data is always treated as bytes and base64-encoded before entering the OSC 52 frame.

Acceptance coverage explicitly includes payload bytes containing:

- NUL;
- BEL;
- ESC;
- `]`;
- `\\`;
- bytes that resemble `OSC 52` syntax;
- arbitrary high bytes including `0xFF`.

Only the library-owned OSC introducer and ST terminator appear as raw control bytes. Caller payload bytes cannot terminate, restart, or otherwise alter OSC framing.

---

## 3. Resource-boundary acceptance

The frozen limits remain distinct:

```text
Maximum decoded OSC 52 payload:       65,536 bytes
Maximum encoded OSC 52 payload:       87,384 bytes
Maximum complete OSC 52 frame:        87,400 bytes
Maximum undecoded terminal buffer:    98,304 bytes
```

T58 verifies the exact maximum decoded payload through the public read API under fragmented transport delivery.

A deterministic property matrix additionally exercises canonical RFC 4648 encode/decode round trips over representative lengths spanning zero, padding boundaries, 4 KiB boundaries, large payloads, and the exact 65,536-byte ceiling.

---

## 4. Fragmentation and compatibility

T55 already established support for:

```text
ESC ] ... BEL
ESC ] ... ESC \\
0x9D ... 0x9C
```

T58 verifies that the public T57 read API remains correct when a maximum legal response is split across many transport reads.

The compatibility policy remains asymmetric by design: outbound OSC 52 is always canonical seven-bit `ESC ] ... ESC \\`, while inbound compatibility accepts the frozen BEL/ST/C1 forms only during an active OSC 52 expectation.

---

## 5. Unrelated OSC traffic

An active clipboard query cannot be satisfied by:

- another OSC selector such as OSC 51;
- an OSC 52 response for another selection target;
- malformed framing;
- unrelated application traffic.

T58 exercises unrelated OSC followed by a wrong-selection OSC 52 response and then the correct correlated response. Only the correctly correlated response completes the query.

---

## 6. Timeout and late-response ownership

OSC 52 reads reuse `TerminalQueryTransactionManager` and therefore inherit the established ambiguity-sensitive transaction model.

After an emitted query reaches caller-visible timeout or cancellation, the transaction retains bounded late-response ownership. A delayed reply belonging to that expired caller is consumed by the old transaction and cannot satisfy a newer clipboard query.

T58 exercises this behavior through the public clipboard-read API, not merely through the generic internal query tests.

---

## 7. Semantic output composition

Clipboard writes and reads are verified alongside the earlier session-owned semantic output families:

1. application text;
2. OSC 2 window title;
3. OSC 7 current location;
4. OSC 8 bounded hyperlink begin/text/end;
5. OSC 52 clipboard write;
6. OSC 52 clipboard query.

The complete sequence preserves session-owned output ordering. Clipboard writes do not flush implicitly; clipboard queries do flush as required by the conversational transaction substrate.

---

## 8. Bracketed-paste regression audit

The T55 increase of `TerminalSession.MaximumBufferedInputBytes` from 4,096 bytes to 98,304 bytes exposed an unintended coupling: `TerminalInputDecoderOptions.PasteChunkBytes` also inherited the larger value because its default referenced the same session ceiling.

T58 removes that coupling.

The defaults are now deliberately separate:

```text
MaximumBufferedBytes: 98,304
PasteChunkBytes:       4,096
```

OSC 52 therefore receives the larger bounded undecoded-input capacity it requires without silently changing the historical bracketed-paste chunking policy.

---

## 9. Compatibility posture

0.7 does not claim that terminal identity proves OSC 52 support. Representative terminal implementations may:

- support writes but disable reads;
- require explicit user configuration for clipboard queries;
- ignore OSC 52 entirely;
- impose smaller implementation-specific payload limits;
- accept only some selection targets;
- terminate responses with BEL or ST;
- decline to expose clipboard data for security reasons.

`Icod.Terminal` therefore preserves the following semantics:

- a successful write proves emission only;
- a successful read proves receipt of a valid correlated response;
- timeout does not prove unsupported behavior;
- endpoint rejection is distinct from terminal-side non-response;
- no automatic clipboard probing is performed.

---

## 10. T58 gate

T58 is complete when:

1. caller-controlled bytes cannot inject OSC framing;
2. exact payload and frame bounds remain deterministic;
3. public maximum-payload reads survive fragmented delivery;
4. unrelated OSC and wrong-selection responses cannot satisfy a query;
5. timeout/cancellation preserve bounded late-response ownership;
6. OSC 52 composes in-order with prior semantic output families;
7. the 98,304-byte decoder ceiling does not alter the 4,096-byte default paste chunk;
8. deterministic payload property tests pass;
9. Windows, Linux, and macOS CI are green.

The next tranche is **T59 — public API, docs, sample, package, and stable closure**.
