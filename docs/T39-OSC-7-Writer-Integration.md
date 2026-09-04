# T39 — OSC 7 Writer Integration

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Tranche:** T39 — OSC 7 writer integration  
**Development version:** `0.5.0-alpha.4`  
**Predecessor:** T38 — reusable URI/location encoder  
**Status:** Implemented; public session API remains deferred to T40

---

## 1. Purpose

T39 connects the host-independent `file:` URI encoder completed in T38 to the
existing canonical OSC transport established by 0.4.

This tranche proves byte-exact OSC 7 framing and output behavior while keeping
OSC 7 internal. The public semantic `TerminalSession` operation is intentionally
reserved for T40.

---

## 2. Canonical frame

T39 implements the frozen T37 wire form:

```text
ESC ] 7 ; <file-uri> ESC \
```

The implementation uses:

- 7-bit `ESC ]` OSC introduction;
- ASCII selector `7`;
- ASCII `;` separator;
- the fully validated ASCII `file:` URI produced by
  `TerminalLocationUriEncoder`;
- 7-bit `ESC \\` String Terminator;
- no BEL terminator;
- no 8-bit C1 OSC/ST forms.

---

## 3. Writer API

T39 adds internal writer operations:

```csharp
OscWriter.EncodeLocationFrame(
    path,
    pathKind,
    authority
);

OscWriter.WriteLocationAsync(
    output,
    path,
    pathKind,
    authority,
    cancellationToken
);
```

The writer accepts structured native path input rather than an already-formed
URI. T38 therefore remains the single URI escaping/validation authority.

---

## 4. Validation before transmission

The complete native path is converted into the complete `file:` URI before any
OSC byte is written.

Any failure in:

- absolute-path validation;
- path grammar validation;
- UNC parsing;
- authority validation;
- Unicode validation;
- URI percent encoding;
- the 16384-byte URI resource limit;

fails before the output service receives a write.

Rejected input therefore cannot emit a partial OSC introducer or selector.

---

## 5. Complete-frame write semantics

A valid OSC 7 frame is constructed completely in memory and submitted through
one `ITerminalOutput.WriteAsync(...)` call.

This mirrors the 0.4 title writer commitment model and makes frame boundaries
observable and deterministic to injected output implementations.

T39 does not split the introducer, selector, URI payload, and ST terminator into
separate writes.

---

## 6. Cancellation

Cancellation follows the established 0.4 operational-output contract:

- cancellation is checked before URI/frame construction;
- cancellation is checked again immediately before transmission is committed;
- pre-transmission cancellation writes no bytes;
- once the complete frame is committed to `ITerminalOutput`, the underlying
  write receives `CancellationToken.None` so ordinary caller cancellation does
  not deliberately truncate a control frame.

This is a frame-integrity rule, not a guarantee that arbitrary external stream
failures cannot interrupt physical transport.

---

## 7. Flush behavior

`WriteLocationAsync(...)` does **not** call `FlushAsync(...)`.

OSC 7 publication is an output operation, not a response-synchronization
transaction. T40 will integrate the writer with the session-owned output gate,
but it will retain this no-implicit-flush rule unless later evidence requires a
specific protocol boundary.

---

## 8. Output failures

Transport/output failures propagate unchanged from the borrowed
`ITerminalOutput` implementation.

They are not converted into:

- unsupported results;
- terminal capability observations;
- URI validation errors;
- protocol acknowledgement results.

Successful completion means the complete frame was accepted by the output
service. It still does not prove that the terminal emulator recognized or used
OSC 7.

---

## 9. Tests

`OscLocationWriterTests` adds byte-exact and lifecycle fixtures for:

- POSIX root;
- nested POSIX path;
- Windows drive path;
- UNC path;
- explicit authority;
- URI escaping before OSC framing;
- one-call complete-frame submission;
- zero implicit flush;
- invalid-path rejection with zero output;
- oversized URI rejection with zero output;
- pre-transmission cancellation with zero output;
- output-failure propagation.

The tests use only injected output services and never modify the CI runner's
actual terminal state.

---

## 10. Architectural boundary

T39 deliberately does not add a public method to `TerminalSession`.

At this point the stack is:

```text
native filesystem location
        |
        v
TerminalLocationUriEncoder       (T38)
        |
        v
ASCII file: URI payload
        |
        v
OscWriter OSC 7 frame            (T39)
        |
        v
ITerminalOutput
```

T40 will add the semantic `TerminalSession` API, endpoint checks, session-owned
output serialization, and explicit publication semantics on top of this now
byte-proven internal path.

---

## 11. T39 gate

T39 is complete when Windows, Linux, and macOS CI prove:

- exact canonical OSC 7 frames;
- no partial output for validation/resource failures;
- one-call frame submission;
- cancellation-before-transmission behavior;
- no implicit flush;
- output-failure propagation;
- no regression to the 0.4 title writer tests.

The next tranche is **T40 — `TerminalSession` semantic current-location API**.
