# Icod.Terminal Public API Baseline — 0.5

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Status:** T42 reviewed public API delta; final T43 safety audit incorporated

---

## 1. Purpose

This document freezes the public API added by the 0.5 OSC 7 current-location milestone before stable release closure.

The 0.5 public delta is intentionally small. It does not expose raw OSC selectors, arbitrary URI strings, or a general shell-integration framework.

---

## 2. New public enum

```csharp
namespace Icod.Terminal;

public enum TerminalLocationPathStyle {
    Posix = 0,
    WindowsDrive = 1,
    WindowsUnc = 2
}
```

The enum exists because native path grammar must be explicit and deterministic independent of the operating system executing the process.

---

## 3. New TerminalSession operation

```csharp
public ValueTask PublishCurrentLocationAsync(
    string path,
    TerminalLocationPathStyle pathStyle,
    string? authority = null,
    CancellationToken cancellationToken = default
);
```

Semantics:

- publishes one caller-supplied filesystem location using OSC 7;
- accepts a native absolute path rather than a pre-escaped URI;
- supports POSIX absolute paths, fully-qualified Windows drive paths, and Windows UNC paths;
- optionally accepts an explicit host authority for POSIX and Windows-drive forms;
- UNC authority is derived from the UNC server component;
- converts the native path to the 0.5 canonical `file:` URI representation;
- validates and constructs the complete frame before output;
- emits through the session-owned output-ordering boundary;
- rejects known redirected output;
- performs no implicit flush;
- observes cancellation before transmission commitment;
- propagates output failures;
- returns no fabricated terminal-side support/application result.

Successful completion means the complete OSC 7 frame was written to session output. It does not prove that the terminal recognized, retained, or acted on the published location.

---

## 4. Explicit disclosure contract

Current-location publication can expose directory names, user names, source-tree structure, mount information, network-share names, and host identity.

The 0.5 public API therefore remains explicit:

- session opening does not publish a location;
- `Environment.CurrentDirectory` is not read automatically;
- process current-directory changes are not monitored;
- presentation/input/query/title operations do not publish a location;
- disposal does not publish or republish a location;
- host authority is not derived from environment or shell state.

T42 reviewed a possible convenience method that would publish `Environment.CurrentDirectory`. It is deliberately **not** added in 0.5. The existing method already provides a complete ordinary-use path, while omitting the convenience method keeps disclosure visible at the call site and avoids coupling the API to process-global mutable state.

---

## 5. URI/path contract visible to callers

The public API commits to these behaviors:

- only `file:` URIs are emitted for OSC 7;
- local POSIX paths use canonical forms such as `file:///usr/src`;
- Windows drive paths use forms such as `file:///C:/src`;
- UNC paths use authority mapping such as `file://server/share/dir`;
- Windows drive letters normalize to uppercase;
- relative and drive-relative paths are rejected;
- Windows device/extended namespace paths are rejected;
- malformed Unicode is rejected;
- C0, DEL, and C1 control characters in native paths are rejected before URI construction rather than percent-encoded;
- path text is encoded once from native data using strict UTF-8 percent encoding;
- literal `%20` native path text therefore becomes `%2520`;
- dot segments, repeated separators, trailing separators, case, and Unicode normalization form are preserved except for drive-letter normalization;
- no filesystem existence check, symlink resolution, or canonical-path lookup is performed;
- the fully encoded `file:` URI payload is limited to 16384 bytes.

Explicit authorities remain deliberately narrow in 0.5:

- ASCII DNS names, IPv4 literals, and bracketed IPv6 literals are supported;
- userinfo, ports, path/query/fragment data, and internationalized host names are rejected;
- literal `%` is rejected in authority text;
- scoped IPv6 zone identifiers such as `[fe80::1%eth0]` and URI-escaped zone forms such as `[fe80::1%25eth0]` are not part of the 0.5 contract and are rejected.

---

## 6. Non-goals and rejected additions

The T42 regret audit rejects adding the following to 0.5:

```text
SendOsc(...)
WriteEscape(...)
PublishUri(...)
SetCurrentDirectory(...)
PublishProcessCurrentDirectoryAsync(...)
```

The first two would expose a premature raw protocol extension surface. `PublishUri(...)` would widen OSC 7 beyond the `file:`-location contract and pre-commit URI semantics needed by OSC 8. `SetCurrentDirectory(...)` would confuse terminal metadata publication with process/filesystem mutation. `PublishProcessCurrentDirectoryAsync(...)` would add convenience at the cost of making sensitive process-global state less explicit.

OSC 8 hyperlink parameters, scoped hyperlink state, OSC 52, proprietary shell integration, and automatic shell hooks remain later work.

---

## 7. Compatibility with future OSC 8

The 0.5 public API does not expose the internal URI encoder or OSC writer.

This is deliberate. The internal location encoder may be reused or generalized by 0.6 OSC 8 without forcing the 0.5 public surface to become a general URI-construction API.

The reusable implementation evidence is internal; the public contract remains semantic.

---

## 8. Freeze decision

The T42 review accepts these two public additions for stable 0.5:

```text
TerminalLocationPathStyle
TerminalSession.PublishCurrentLocationAsync(...)
```

The final T43 safety audit does not widen that public surface. It only tightens validation to match the already-frozen OSC safety and authority contracts.

No additional public current-location convenience method is justified before stable release closure.
