# Icod.Terminal Public API Baseline — 0.6

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Status:** T50 public API regret audit complete

---

## 1. Purpose

This document freezes the public API added by the 0.6 OSC 8 hyperlink milestone before stable release closure.

The 0.6 public delta remains semantic. It does not expose selector numbers, raw OSC framing, arbitrary OSC 8 parameter dictionaries, generic URI activation, or a public escape-sequence construction surface.

---

## 2. New public lease type

```csharp
namespace Icod.Terminal;

public sealed class TerminalHyperlinkLease : IAsyncDisposable {
    public string Uri { get; }
    public string? Identifier { get; }

    public ValueTask DisposeAsync();
}
```

Semantics:

- represents one session-owned OSC 8 hyperlink scope;
- exposes the canonical URI and optional canonical identifier emitted for the scope;
- cleanup is asynchronous because restoring the previous hyperlink state requires terminal output;
- leases are strictly LIFO;
- disposing an inner lease restores the immediately previous session-owned hyperlink;
- disposing the outermost lease emits the canonical OSC 8 close frame;
- failed release remains retryable;
- session disposal is the final cleanup authority for outstanding leases.

A lease does not expose raw OSC parameter text or arbitrary protocol state.

---

## 3. New scoped TerminalSession operation

```csharp
public ValueTask<TerminalHyperlinkLease> AcquireHyperlinkAsync(
    string uri,
    string? identifier = null,
    CancellationToken cancellationToken = default
);
```

Semantics:

- accepts one non-empty absolute already URI-encoded target;
- accepts an optional OSC 8 `id` value;
- validates and bounds all URI/id data before output;
- rejects known redirected output;
- emits one canonical OSC 8 begin frame through the session-owned output boundary;
- tracks the resulting hyperlink state until lease release or session disposal;
- observes cancellation only during acquisition and before begin transmission commitment;
- does not fetch, resolve, open, or otherwise activate the URI;
- successful completion proves frame emission, not terminal-side support or activation.

---

## 4. New bounded TerminalSession operation

```csharp
public ValueTask WriteHyperlinkAsync(
    string value,
    string uri,
    string? identifier = null,
    CancellationToken cancellationToken = default
);
```

Semantics:

- provides the ordinary one-shot hyperlink operation for application text;
- uses the same persistent hyperlink ownership manager as `AcquireHyperlinkAsync(...)`;
- serializes begin, application text, and release/restore as one bounded session-owned operation;
- encodes application text with the session's configured application encoding;
- leaves failed close/restore state owned so later release or session disposal can retry cleanup;
- aggregates application-write and cleanup failures when both occur;
- performs no implicit flush.

---

## 5. URI and identifier contract visible to callers

The 0.6 public API commits to the following:

- URI input is caller-supplied absolute URI text, not a native filesystem path;
- URI input is already percent-encoded ASCII text;
- the library validates RFC 3986 generic syntax without browser/WHATWG normalization;
- percent escapes are preserved but hex digits normalize to uppercase;
- raw spaces, raw non-ASCII text, malformed Unicode, malformed percent escapes, C0, DEL, and C1 controls are rejected;
- relative references are rejected;
- no fixed scheme allow-list is imposed by `Icod.Terminal`;
- userinfo, reg-name, bracketed IPv6, IPvFuture, and decimal ports are parsed according to the 0.6 generic authority contract;
- malformed authority syntax and scoped IPv6 zone identifiers are rejected;
- maximum URI payload length is 2083 bytes;
- the only public OSC 8 parameter semantic is optional `id`;
- identifiers are limited to RFC 3986 unreserved ASCII and 128 bytes;
- null and empty identifiers canonicalize to omitted `id`.

Consumers that require a scheme trust policy must apply it before calling these APIs.

---

## 6. Scoped ownership contract

The public API intentionally commits to strict LIFO nesting rather than arbitrary overlapping disposal.

```text
Acquire A   -> emit begin A
Acquire B   -> emit begin B
Dispose B   -> re-emit begin A
Dispose A   -> emit canonical close
```

Out-of-order release throws without mutating the tracked stack or emitting protocol output.

The session restores only state that it created. It does not claim to discover or reconstruct hyperlink state that existed before library-owned OSC 8 output began.

---

## 7. Support and security semantics

Successful completion means requested bytes were written to the session output. It does not prove that the terminal:

- implements OSC 8;
- displays hyperlink decoration;
- recognizes the URI scheme;
- permits activation;
- can reach the target.

`TERM`, terminfo identity, emulator name, and host OS are not fabricated into proof of OSC 8 support.

The library performs no network access, DNS lookup, filesystem lookup, browser launch, shell invocation, automatic URL detection, or automatic environment disclosure.

---

## 8. Regret audit

T50 rejects adding the following public APIs in 0.6:

```text
SendOsc(...)
WriteEscape(...)
BeginHyperlinkRaw(...)
EndHyperlinkRaw(...)
SetOsc8Parameters(...)
AcquireHyperlinkAsync(string uri, IReadOnlyDictionary<string,string> parameters, ...)
WriteHyperlinkAsync(string value, Uri uri, ...)
OpenHyperlink(...)
DetectHyperlinks(...)
```

The first five would expose protocol syntax rather than semantics. Arbitrary dictionaries would prematurely freeze extensibility needed by later OSC families. A `System.Uri` overload would imply framework normalization semantics that the 0.6 wire contract deliberately avoids. Activation and automatic detection belong to application/UI policy rather than the terminal transport layer.

No additional public OSC 8 type or convenience API is justified before stable release closure.

---

## 9. Separation from Icod.DCurses

`Icod.Terminal` owns terminal protocol state and ordering. It does not define hyperlink-bearing cells, style inheritance, hit testing, activation policy, or virtual-screen diff behavior.

Those are higher-level presentation concerns for `Icod.DCurses` or other consumers. The 0.6 API is deliberately sufficient for such consumers without forcing a cell model into this package.

---

## 10. Freeze decision

The T50 review accepts exactly these public additions for stable 0.6:

```text
TerminalHyperlinkLease
TerminalSession.AcquireHyperlinkAsync(...)
TerminalSession.WriteHyperlinkAsync(...)
```

No generic OSC extension surface is accepted into 0.6.
