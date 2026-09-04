# T50 — Public API, Documentation, and Sample Audit

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Tranche:** T50 — public API, documentation, and sample audit  
**Development version:** `0.6.0-alpha.9`  
**Predecessor:** T49 — integration, compatibility, and security acceptance  
**Status:** Complete pending fresh repository validation

---

## 1. Purpose

T50 performs the public-contract regret audit for the 0.6 OSC 8 milestone before package-only consumer and release closure.

The implementation evidence from T44 through T49 is now sufficient to decide which OSC 8 concepts should become stable public API and which implementation details must remain internal.

---

## 2. Accepted public delta

T50 accepts exactly these public additions:

```text
TerminalHyperlinkLease
TerminalSession.AcquireHyperlinkAsync(...)
TerminalSession.WriteHyperlinkAsync(...)
```

The complete reviewed signatures are recorded in `docs/Public-API-Baseline-0.6.md`.

No other public OSC 8 type or operation is justified before stable release closure.

---

## 3. Why both bounded and scoped APIs remain

The two `TerminalSession` operations serve different semantic use cases.

`WriteHyperlinkAsync(...)` is the ordinary bounded operation for one string of application text. It owns begin/text/release as one serialized action and should remain the preferred API for simple links.

`AcquireHyperlinkAsync(...)` exists for streaming and structured-output scenarios where application code must perform multiple session writes while one hyperlink is active. The returned `TerminalHyperlinkLease` makes the terminal-state lifetime explicit and participates in strict-LIFO restoration.

Both APIs use the same internal `TerminalHyperlinkManager`; T48 eliminated the earlier risk of parallel ownership models.

---

## 4. Public surface deliberately rejected

The audit rejects adding:

```text
SendOsc(...)
WriteEscape(...)
BeginHyperlinkRaw(...)
EndHyperlinkRaw(...)
SetOsc8Parameters(...)
AcquireHyperlinkAsync(... arbitrary parameter dictionary ...)
WriteHyperlinkAsync(... System.Uri ...)
OpenHyperlink(...)
DetectHyperlinks(...)
```

Reasons:

- raw OSC APIs would freeze protocol syntax rather than semantic operations;
- arbitrary parameter dictionaries would prematurely expose an extensibility model before OSC 52 and later protocol work have demonstrated a reusable abstraction;
- a `System.Uri` overload would imply framework normalization semantics that the 0.6 wire contract intentionally avoids;
- hyperlink activation, URL detection, reachability checking, DNS resolution, browser launch, and trust policy belong to application/UI layers rather than terminal transport.

---

## 5. Lease surface audit

`TerminalHyperlinkLease` intentionally exposes only:

```csharp
public string Uri { get; }
public string? Identifier { get; }
public ValueTask DisposeAsync();
```

This is sufficient for callers to inspect the semantic state they acquired without exposing lease IDs, OSC bytes, raw parameter fields, manager state, or output synchronization primitives.

The lease remains `IAsyncDisposable` because release may need to write either an outer hyperlink begin frame or the canonical OSC 8 close frame.

---

## 6. Ownership semantics retained

The public contract retains strict LIFO nesting:

```text
Acquire A   -> begin A
Acquire B   -> begin B
Dispose B   -> restore A
Dispose A   -> close
```

Out-of-order disposal remains an error with zero protocol/state mutation. Failed release remains retryable. Session disposal remains final cleanup authority for outstanding library-owned hyperlink state.

The library does not attempt to recover unknown hyperlink state which may have existed before its own first OSC 8 begin operation.

---

## 7. URI/security semantics retained

T50 accepts the T44/T47A/T49 URI contract without widening it:

- one absolute caller-supplied URI string;
- already URI-encoded ASCII input;
- RFC 3986 generic validation;
- no browser/WHATWG rewriting;
- percent-escape hexadecimal normalization only;
- no raw Unicode IRI input;
- no fixed scheme allow-list;
- consumer-owned scheme/trust policy;
- 2083-byte URI limit;
- optional `id` only;
- 128-byte RFC 3986-unreserved identifier limit;
- malformed authority, controls, malformed Unicode, malformed `%HH`, relative references, and scoped IPv6 zone identifiers rejected before output.

The library does not fetch or activate URI targets.

---

## 8. Support semantics retained

Successful completion means the requested bytes were written. It does not prove terminal-side recognition, rendering, activation, or reachability.

The public API does not expose a fabricated `SupportsOsc8` boolean based on `TERM`, terminfo identity, emulator name, operating system, or static profile naming.

Known redirected output continues to reject semantic OSC 8 operations.

---

## 9. Icod.DCurses boundary

The public `Icod.Terminal` contract stops at semantic terminal hyperlink state and output ordering.

It does not define:

- hyperlink-bearing cells;
- virtual-screen hyperlink storage;
- diff/refresh grouping;
- visual affordance;
- pointer hit testing;
- activation policy;
- URI trust policy.

Those remain higher-level concerns for `Icod.DCurses` or other presentation consumers.

---

## 10. Focused sample

T50 adds `samples/Icod.Terminal.Hyperlink.Sample` targeting:

```text
net8.0
net9.0
net10.0
```

The sample demonstrates:

1. `WriteHyperlinkAsync(...)` for one bounded link;
2. `AcquireHyperlinkAsync(...)` for explicit scoped state;
3. one nested hyperlink scope;
4. restoration of the outer scope after disposing the inner scope;
5. final canonical close when the outer scope is disposed.

The sample requires URI/text input from the caller rather than silently discovering or generating hyperlink targets.

It explicitly teaches emission-oriented semantics and avoids opening a browser or otherwise activating the supplied target.

---

## 11. Documentation updates

T50 adds or updates:

- `docs/Public-API-Baseline-0.6.md`;
- this T50 completion record;
- root `README.md` with the 0.6 OSC 8 public contract and examples;
- `samples/README.md` with hyperlink sample guidance;
- the solution file so the hyperlink sample participates in ordinary repository builds.

---

## 12. Regret audit conclusion

The public surface is intentionally small and coherent.

The 0.6 API adds one state-bearing lease type and two semantic session operations. It does not expose implementation helpers, URI parser internals, raw OSC framing, arbitrary parameter extensibility, terminal support guesses, or browser/application policy.

This is considered an acceptable stable public delta.

---

## 13. Gate T50

T50 is complete when a fresh repository build/test matrix proves the public baseline, documentation, and new sample compile cleanly on all supported frameworks and hosts.

The next tranche is **T51 — package, consumer, and stable-release closure**.
