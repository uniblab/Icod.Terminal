# T46 — OSC 8 Writer Integration

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Tranche:** T46 — OSC 8 writer integration  
**Development version:** `0.6.0-alpha.4`  
**Predecessor:** T45 — reusable hyperlink URI and parameter encoding  
**Status:** Implemented; public/session hyperlink ownership remains deferred to T47/T48

---

## 1. Purpose

T46 connects the deterministic T45 hyperlink URI/identifier encoder to the
existing internal OSC transport.

This tranche proves canonical OSC 8 begin/end bytes and transport behavior
without yet exposing a public hyperlink API or tracking hyperlink state in
`TerminalSession`.

---

## 2. Canonical frames

The writer emits hyperlink begin as:

```text
ESC ] 8 ; <params> ; <uri> ESC \
```

and close as:

```text
ESC ] 8 ; ; ESC \
```

Only the canonical 7-bit OSC introducer and `ESC \\` String Terminator are
emitted. BEL and 8-bit C1 forms remain unsupported output forms.

---

## 3. Internal writer surface

T46 adds:

```csharp
OscWriter.EncodeHyperlinkBeginFrame(
    uri,
    identifier
);

OscWriter.EncodeHyperlinkEndFrame();

OscWriter.WriteHyperlinkBeginAsync(
    output,
    uri,
    identifier,
    cancellationToken
);

OscWriter.WriteHyperlinkEndAsync(
    output,
    cancellationToken
);
```

These members remain internal. Public semantic ownership belongs to T47/T48.

---

## 4. Validation boundary

A begin frame is constructed only after:

- T45 validates the absolute URI;
- percent escapes are normalized without decoding;
- URI length is within 2083 bytes;
- the optional identifier is validated;
- identifier length is within 128 bytes;
- the complete parameter and URI strings are available.

Invalid begin input therefore produces no OSC bytes.

The end frame contains no caller-controlled payload and is a fixed canonical
frame.

---

## 5. Complete-frame writes

Each begin or end operation constructs the complete frame in memory and submits
it through exactly one `ITerminalOutput.WriteAsync(...)` call.

This maintains the frame-level atomicity convention established by OSC 0/1/2 and
OSC 7. It does not claim that an arbitrary transport can never fail after
partially consuming a supplied buffer.

---

## 6. Cancellation

Begin and end writes observe caller cancellation before transmission is
committed.

Once a complete frame is committed to the output service, the writer uses
`CancellationToken.None` for that write so ordinary caller cancellation does not
intentionally truncate the protocol frame.

T48 scoped cleanup will call the same writer under the stronger non-cancellable
lease-release/disposal contract frozen in T44.

---

## 7. Flush policy

Neither hyperlink begin nor hyperlink end flushes output implicitly.

This matches the established OSC title/location policy. Session lifecycle and
future scoped-state ownership decide higher-level cleanup/flush behavior rather
than the low-level OSC writer.

---

## 8. Failure behavior

Output exceptions propagate unchanged.

The T46 writer itself does not maintain hyperlink ownership state and therefore
does not attempt recovery after an output failure. T47/T48 will decide when a
successful write becomes session-owned state and how failed restore/close writes
remain retryable.

---

## 9. Shared framing refactor

T46 factors the established OSC frame construction into internal helpers for:

- selector + one semicolon-separated payload;
- selector + two semicolon-separated payload fields.

OSC 0/1/2 and OSC 7 continue to use their existing wire forms through the shared
single-separator helper. OSC 8 uses the double-separator helper.

This is internal reuse only; no generic public OSC API is introduced.

---

## 10. Tests

`OscHyperlinkWriterTests` proves:

- byte-exact begin frames with and without `id`;
- HTTP(S), mailto, file, and custom-scheme targets;
- query/fragment preservation;
- percent-escape normalization before framing;
- byte-exact canonical close;
- exactly one output write per begin/end frame;
- no implicit flush;
- invalid URI/identifier zero-output behavior;
- oversized URI zero-output behavior;
- cancellation-before-begin/end transmission;
- begin/end transport-failure propagation.

All tests use injected output and do not depend on the CI runner's terminal.

---

## 11. Gate T46

T46 is complete when the repository matrix proves:

- canonical OSC 8 begin bytes;
- canonical OSC 8 close bytes;
- T45 validation occurs before output;
- one complete frame is submitted per write;
- cancellation is observed before transmission commitment;
- no implicit flush occurs;
- transport failures remain observable;
- existing OSC 0/1/2 and OSC 7 tests remain green.

The next tranche is **T47 — `TerminalSession` semantic hyperlink API**. T47 will
introduce the ordinary public semantic entry point and session-owned output
ordering, while T48 will add the strict-LIFO scoped lease and cleanup-state
machinery frozen in T44.
