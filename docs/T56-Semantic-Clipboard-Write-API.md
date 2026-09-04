# T56 — Semantic Clipboard Write API

**Project:** `Icod.Terminal`  
**Release line:** `0.7.0`  
**Development version:** `0.7.0-alpha.5`  
**Status:** Implemented  
**Predecessor:** T55 — inbound OSC framing and routing

---

## 1. Purpose

T56 exposes the first public OSC 52 surface while preserving the protocol and security boundaries frozen in T52 through T55.

Ordinary callers can now write bounded terminal-managed clipboard or selection data without knowing selector letters, OSC framing, base64 syntax, or output ordering rules.

T56 is write-only. Clipboard reads remain exclusively T57 work.

---

## 2. Public selection type

T56 introduces:

```csharp
public enum TerminalClipboardSelection {
    Clipboard,
    Primary,
    Secondary,
    Select
}
```

The public type intentionally mirrors only the single-selection subset frozen in T52.

It does not expose:

- raw selector strings;
- multi-selection lists;
- xterm cut buffers `0` through `7`;
- empty/default selector behavior;
- arbitrary OSC targets.

The public enum is mapped internally to `TerminalOsc52Selection`, preserving the separation between semantic API and protocol representation.

---

## 3. Byte-first write contract

The primary public operation is:

```csharp
ValueTask WriteClipboardAsync(
    TerminalClipboardSelection selection,
    ReadOnlyMemory<byte> payload,
    CancellationToken cancellationToken = default
)
```

The byte payload is authoritative. OSC 52 is capable of transporting arbitrary bytes after base64 encoding, so the public API does not force all selection content through a text encoding.

The existing T53 limit remains authoritative:

```text
Maximum decoded payload: 65,536 bytes
```

Oversized byte payloads are rejected before output.

An empty byte payload is valid and expresses the T52 semantic operation of replacing the selected terminal-managed selection with empty content.

---

## 4. Text convenience contract

T56 also exposes:

```csharp
ValueTask WriteClipboardAsync(
    TerminalClipboardSelection selection,
    string value,
    CancellationToken cancellationToken = default
)
```

Text is encoded using strict UTF-8 without a byte-order mark.

This encoding is intentionally independent of `TerminalSession.ApplicationEncoding`.

Reasons:

1. clipboard text should have one deterministic representation across sessions;
2. a caller changing ordinary application-output encoding must not silently change clipboard semantics;
3. UTF-8 preserves the byte-first protocol model while providing a conventional cross-platform text representation;
4. malformed UTF-16 input is rejected rather than silently replaced.

The UTF-8 byte count is computed and bounded before the destination byte array is allocated.

An empty string maps to an empty byte payload and therefore clears the target selection.

---

## 5. Endpoint and support semantics

Clipboard writes require an interactive terminal output endpoint.

If `OutputObservation.IsTerminal` is false, the operation fails before any OSC bytes are emitted, even when the session itself was intentionally opened with `RequireInteractiveOutput = false`.

Successful completion means only that the complete OSC 52 frame was emitted.

It does not prove that the endpoint:

- supports OSC 52;
- permits clipboard writes under its current security policy;
- retained the data;
- exposed the data through a desktop clipboard;
- mapped primary/secondary/select targets in a particular host-specific way.

No terminal name, terminfo capability, or emulator identity is promoted to proof of support in T56.

---

## 6. Output ordering

T56 uses the existing session-owned output serialization gate.

Clipboard writes therefore participate in the same ordering domain as:

- application text;
- OSC 0/1/2 title operations;
- OSC 7 location publication;
- OSC 8 hyperlink operations;
- query request emission;
- lifecycle/disposal output transitions.

The complete OSC 52 frame is validated before transmission and emitted through one protocol write.

The semantic operation does not implicitly flush.

Caller cancellation may prevent acquisition or transmission before commit. Once the complete OSC frame is committed to the lower writer, caller cancellation does not deliberately truncate the control string.

---

## 7. Security and lifecycle behavior

T56 introduces no automatic clipboard mutation.

The library does not write selection content during:

- session open;
- capability discovery;
- suspend or resume;
- state invalidation;
- disposal.

Every clipboard mutation originates from an explicit caller invocation of `WriteClipboardAsync(...)`.

The write operation does not read clipboard data and does not access native OS clipboard APIs.

---

## 8. Tests

T56 adds deterministic tests covering:

- byte-exact frames for clipboard, primary, secondary, and select targets;
- arbitrary binary data including control bytes and `0xFF`;
- empty-payload clear semantics;
- strict UTF-8 text independent of `ApplicationEncoding`;
- exact 65,536-byte payload acceptance;
- one-byte-over rejection with zero output;
- invalid selection rejection with zero output;
- redirected-output rejection with zero output;
- cancellation before transmission;
- output failure propagation;
- ordering behind application text;
- ordering behind the shared control-output lease;
- no implicit flush;
- rejection after session disposal.

---

## 9. T56 gate

T56 is complete when:

1. callers use a typed public selection target rather than raw selector letters;
2. binary clipboard writes are bounded to 65,536 bytes;
3. text convenience uses deterministic strict UTF-8;
4. empty payloads retain the frozen set-empty semantic;
5. redirected output produces zero OSC 52 output;
6. writes participate in session-owned output serialization;
7. no write implicitly flushes;
8. no automatic clipboard mutation exists;
9. Windows, Linux, and macOS CI are green.

The next tranche is **T57 — explicit clipboard read/query API**.
