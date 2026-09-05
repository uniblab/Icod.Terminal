# T57 — Explicit Clipboard Read API

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.6`  
**Status:** Implemented  
**Predecessor:** T56 — semantic clipboard write API

---

## 1. Purpose

T57 exposes the explicit public OSC 52 clipboard-read operation on top of the bounded T53 payload codec, T55 OSC response routing, and the existing T23 query transaction substrate.

Clipboard reads are deliberately more restrictive than clipboard writes. A read occurs only because a caller invokes the public read method for one typed selection and supplies a bounded caller-visible timeout.

No clipboard query occurs during session open, terminal discovery, lifecycle handling, capability probing, or disposal.

---

## 2. Public API

T57 adds:

```csharp
ValueTask<byte[]> ReadClipboardAsync(
    TerminalClipboardSelection selection,
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

The result is raw decoded bytes. T57 does not add a text-returning convenience overload because the OSC 52 protocol payload is fundamentally binary and a terminal does not communicate a character encoding alongside the response.

Callers which know their application-level encoding may decode the returned bytes explicitly.

---

## 3. Explicit privacy boundary

Calling `ReadClipboardAsync(...)` is the explicit opt-in boundary for one clipboard query.

The library does not infer permission from:

- `TERM`;
- terminal identity;
- terminfo `Ms`;
- operating system;
- terminal emulator family;
- prior successful writes;
- prior successful reads.

A successful previous query does not create a session-global clipboard-read permission state.

---

## 4. Query request and transaction ownership

The request uses the canonical T54 query frame:

```text
ESC ] 52 ; Pc ; ? ESC \\
```

The request is submitted through `TerminalQueryTransactionManager`, not written directly by the public API.

Therefore OSC 52 reads inherit the existing active-query guarantees:

- query requests are serialized through the ambiguity gate;
- the response expectation is registered before emission;
- pre-existing buffered input is protected from satisfying the new query;
- the request is written through the session control-output gate;
- the request is flushed as part of query emission;
- the caller-visible timeout begins when the transaction is queued, so queueing and waiting for the control-output gate count against the requested deadline;
- caller cancellation or timeout before emission may prevent output entirely;
- after emission, caller cancellation does not destroy wire ownership immediately;
- timeout/cancellation retains bounded late-response ownership;
- session suspend/disposal invalidates or terminates outstanding transactions through the existing transaction manager.

The default late-response ownership interval remains the transaction substrate's existing one-second value.

---

## 5. Response correlation

The T55 selection-aware OSC 52 matcher remains responsible for correlation.

A response is correlated when it has:

1. an accepted OSC framing form;
2. OSC selector `52`;
3. exactly the requested single selection target;
4. a payload field following that selection.

Wrong selections and unrelated OSC families do not satisfy the active query.

T57 refines malformed-response ownership: once a complete OSC 52 frame is structurally correlated to the active selection, malformed or oversized base64 is owned by that transaction and reported deterministically as `FormatException` rather than being ignored until timeout.

T55 additionally owns the resource-failure case in which a structurally correlated OSC 52 candidate reaches the 87,400-byte complete-frame ceiling without completing. That query also fails deterministically with `FormatException`, and the invalid OSC string is drained through its terminator so response bytes cannot be reinterpreted as ordinary application input.

This mirrors the established CSI query pattern in which a correlated response may still fail semantic parsing while adding the resource-boundary ownership required by OSC 52's larger response family.

---

## 6. Resource limits

T57 preserves the frozen resource ceilings:

```text
Maximum decoded OSC 52 payload:       65,536 bytes
Maximum encoded OSC 52 payload:       87,384 bytes
Maximum complete OSC 52 frame:        87,400 bytes
Maximum undecoded terminal buffer:    87,400 bytes
```

Decoded payload allocation occurs only after the encoded payload has passed the T53 canonical length and resource checks.

---

## 7. Timeout and cancellation

The caller must supply a timeout.

The supported timeout range is the shared query range:

```text
0 <= timeout <= 1 minute
```

The timeout is an operation deadline measured from transaction queueing, not merely a post-flush response timer. If it expires before emission commits, no request bytes are emitted. If it expires after emission, the caller receives `TimeoutException` while bounded late-response ownership remains active.

A timeout does not prove that OSC 52 is unsupported because a terminal may silently ignore clipboard queries or disable them by policy.

Caller cancellation produces `OperationCanceledException` for the caller-visible operation. If emission already committed, bounded late-response ownership remains active independently of the caller's completed task.

---

## 8. Endpoint semantics

Clipboard reads require the same conversational endpoint conditions as other active terminal queries:

- interactive input;
- interactive output;
- compatible terminal platform;
- where known, the same terminal device.

Redirected output is rejected before query emission.

The implementation does not treat terminal identity as proof of OSC 52 read support.

---

## 9. Result semantics

A successful call means:

- the canonical query was emitted;
- a correlated OSC 52 response was received;
- the payload was canonical bounded base64;
- the payload decoded successfully within the 65,536-byte ceiling.

An empty decoded byte array represents a syntactically valid empty selection response. T57 does not invent a separate `Unsupported` result for silence because a timeout cannot reliably distinguish unsupported, disabled, filtered, or ignored terminal behavior.

---

## 10. Tests

T57 adds deterministic transport-level tests covering:

- exact canonical query emission for all four public selections;
- mandatory query flush;
- decoded arbitrary binary bytes;
- seven-bit ST responses;
- seven-bit BEL responses;
- C1 OSC/ST responses;
- correlated malformed base64 producing `FormatException`;
- oversized correlated OSC 52 response ownership without ordinary-input leakage;
- wrong-selection responses not satisfying the query;
- caller timeout;
- cancellation before emission producing zero output and zero flush;
- redirected output rejection before emission;
- invalid selection and invalid timeout rejection before output.

Existing T23 transaction tests continue to prove the generic late-response ownership, queueing, suspend/resume, and disposal behavior reused by OSC 52.

---

## 11. T57 gate

T57 is complete when:

1. every clipboard read requires an explicit public invocation;
2. the public API uses the typed T56 selection contract;
3. the caller supplies a bounded timeout;
4. the request uses the existing query transaction manager;
5. request emission is serialized and flushed;
6. the caller-visible deadline begins at queueing and pre-emission expiry produces no request bytes;
7. caller timeout/cancellation preserves bounded post-emission wire ownership;
8. wrong-selection and unrelated OSC responses cannot satisfy the query;
9. correlated malformed or oversized responses fail deterministically rather than masquerading as timeout or ordinary input;
10. returned data remains raw bounded bytes;
11. no automatic or background clipboard reads exist;
12. Windows, Linux, and macOS CI are green.

The next tranche is **T58 — integration, security, and compatibility acceptance**.
