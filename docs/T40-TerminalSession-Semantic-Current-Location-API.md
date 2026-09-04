# T40 — TerminalSession Semantic Current-Location API

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Tranche:** T40 — semantic current-location publication  
**Development version:** `0.5.0-alpha.5`  
**Predecessor:** T39 — OSC 7 writer integration  
**Status:** Implemented; cross-platform/privacy acceptance remains T41

---

## 1. Purpose

T40 exposes the first public OSC 7 semantic operation through `TerminalSession`
without exposing selector numbers, raw OSC framing, or pre-escaped URI payloads.

The public contract is deliberately explicit about native path grammar and
privacy. A caller supplies the location to publish; the session does not read or
monitor `Environment.CurrentDirectory` as a side effect of opening, writing,
querying, or disposing the session.

---

## 2. Public API

T40 adds:

```csharp
public enum TerminalLocationPathStyle {
    Posix,
    WindowsDrive,
    WindowsUnc
}
```

and:

```csharp
public ValueTask PublishCurrentLocationAsync(
    string path,
    TerminalLocationPathStyle pathStyle,
    string? authority = null,
    CancellationToken cancellationToken = default
);
```

The caller therefore describes a native filesystem location rather than a URI.
The internal T38 encoder remains authoritative for `file:` URI construction and
the internal T39 writer remains authoritative for OSC 7 framing.

---

## 3. Why path style is explicit

The public path-style value prevents host-dependent interpretation.

For example, a Windows-drive path can be published while tests execute on Linux,
and a POSIX path can be published while tests execute on Windows. The semantic
result is defined by the argument rather than by `OperatingSystem`,
`Path.DirectorySeparatorChar`, or the process environment.

This also keeps the API usable for applications which present or proxy a logical
location different from the host process's own current directory.

---

## 4. Authority behavior

The optional authority is an explicit disclosure choice.

It is valid for POSIX and Windows-drive locations and is validated by the T38
encoder. A UNC path derives its authority from its server component and rejects
a second authority.

The method does not discover or infer:

- the machine host name;
- SSH host information;
- shell variables;
- remote-session metadata;
- user identity.

---

## 5. Endpoint semantics

`PublishCurrentLocationAsync(...)` requires an output endpoint observed as a
terminal.

If the session already knows that output is redirected, the method throws
`InvalidOperationException` and writes no bytes.

If the endpoint is a terminal but OSC 7 support is merely unknown, explicit
caller publication is permitted. A successful call means only that the complete
OSC 7 frame was written; it does not prove that the terminal recognized or used
the location.

---

## 6. Session-owned output ordering

The semantic method acquires the same `AcquireSessionOutputAsync(...)` lease used
by high-level session output introduced in 0.4.

Consequently OSC 7 publication serializes with:

- `WriteTextAsync(...)`;
- OSC 0/1/2 title operations;
- active terminal queries;
- presentation transitions;
- rich-input protocol transitions.

Direct writes through borrowed `session.Output` remain caller-synchronized.

---

## 7. Validation and transmission behavior

T40 preserves T38/T39 behavior:

- `path` is non-null;
- `pathStyle` must be a defined public value;
- native path and authority validation complete before transmission;
- the complete `file:` URI and OSC 7 frame are built before transmission;
- invalid input emits no bytes;
- cancellation before transmission emits no bytes;
- a valid frame is submitted in one output write;
- no implicit flush occurs;
- output failures propagate as output failures.

---

## 8. Process-current-directory convenience API

T40 deliberately does **not** add an overload which automatically reads
`Environment.CurrentDirectory`.

The T37 roadmap permitted such a convenience API but did not require it. The
T40 implementation evidence favors keeping the first public contract maximally
explicit: the caller chooses both the location and its grammar, and therefore
makes the disclosure decision in the same call.

T42 may reconsider a convenience overload during the public API/regret audit if
consumer evidence shows that it materially improves usability without weakening
the privacy model.

---

## 9. Test coverage

`TerminalSessionLocationTests` covers:

- exact POSIX OSC 7 bytes;
- exact Windows-drive OSC 7 bytes;
- exact UNC OSC 7 bytes;
- explicit authority emission;
- zero-write invalid path rejection;
- known redirected-output rejection;
- cancellation-before-transmission behavior;
- unknown public path-style rejection;
- output-failure propagation;
- no implicit flush.

All tests use injected terminal services and do not publish the CI runner's real
working directory.

---

## 10. T40 gate

T40 is complete when the repository matrix proves the new public API and tests on
Windows, Linux, and macOS.

The next tranche is **T41 — cross-platform path and privacy acceptance**, which
will deepen lifecycle, ordering, privacy/non-emission, and platform acceptance
coverage around the public API before the T42 documentation/sample/public-API
audit.
