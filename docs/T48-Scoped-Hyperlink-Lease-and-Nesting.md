# T48 — Scoped Hyperlink Lease and Nesting

**Project:** `Icod.Terminal`  
**Release line:** `0.6.0`  
**Tranche:** T48 — scoped hyperlink lease and nesting  
**Development version:** `0.6.0-alpha.7`  
**Predecessor:** T47A — foundation audit and refinement  
**Status:** Implemented; broader compatibility/security acceptance remains T49

---

## 1. Purpose

T48 introduces the persistent session-owned OSC 8 state model frozen by T44 and
anticipated by the T47A audit.

The public scoped operation is now conceptually:

```csharp
await using TerminalHyperlinkLease hyperlink =
    await session.AcquireHyperlinkAsync(
        "https://example.com/docs",
        "docs-1"
    );

await session.WriteTextAsync( "linked text" );
```

`TerminalHyperlinkLease` owns exactly one library-created hyperlink state. It does
not claim ownership of hyperlink state that existed before `Icod.Terminal`
emitted its first OSC 8 begin frame.

---

## 2. Public API

T48 adds:

```csharp
public ValueTask<TerminalHyperlinkLease> AcquireHyperlinkAsync(
    string uri,
    string? identifier = null,
    CancellationToken cancellationToken = default
);
```

and:

```csharp
public sealed class TerminalHyperlinkLease : IAsyncDisposable {
    public string Uri { get; }
    public string? Identifier { get; }
    public ValueTask DisposeAsync();
}
```

The lease exposes the canonical URI and optional identifier which were actually
accepted for emission. It does not expose OSC selectors, parameter strings,
framing bytes, or a generic terminal state token.

---

## 3. Single authoritative manager

All persistent OSC 8 state now flows through one internal
`TerminalHyperlinkManager`.

That manager owns:

- URI/id validation through T45;
- begin-frame creation through T46;
- session-owned output ordering;
- the active hyperlink stack;
- lease identifiers;
- strict-LIFO release;
- outer-state restoration;
- retryable failed release;
- final session cleanup.

The T47 bounded convenience API `WriteHyperlinkAsync(...)` is also routed through
this same manager. There is no second parallel hyperlink ownership model.

---

## 4. Strict-LIFO nesting

Nested scopes are supported only as a stack.

For:

```text
begin A
begin B
release B
release A
```

T48 emits:

```text
OSC 8 begin A
OSC 8 begin B
OSC 8 begin A
OSC 8 close
```

The third frame restores the immediately preceding library-owned state.

Even when nested states are identical, acquisition and restoration remain
explicit. T48 does not silently coalesce identical leases or replace the stack
with reference counting.

---

## 5. Out-of-order release

Releasing a non-top lease is invalid.

An out-of-order `DisposeAsync()`:

- throws `InvalidOperationException`;
- emits no protocol bytes;
- does not mutate the active stack;
- leaves the lease available for a later valid release.

Once the inner scopes are released, the previously rejected outer lease can be
released normally.

---

## 6. Release failure and retry

A lease remains logically active until its required restore/close frame reports a
successful write.

Therefore, if lease release fails:

- `DisposeAsync()` surfaces the transport failure;
- the stack entry is retained;
- the lease remains owned;
- a later `DisposeAsync()` call retries the same required transition;
- session disposal remains the final cleanup authority.

This directly closes the ownership gap identified during T47A.

---

## 7. Bounded `WriteHyperlinkAsync(...)` unification

`WriteHyperlinkAsync(...)` now uses the same persistent manager rather than a
standalone begin/text/end implementation.

The complete bounded operation holds both the hyperlink-manager gate and the
session-owned output serialization boundary while emitting:

```text
begin
application text
restore/close
```

No other session-owned semantic output can interleave inside that span.

If the bounded final restore/close fails, its synthetic lease remains in the
manager stack. Session disposal can therefore retry a canonical final close
instead of forgetting that terminal hyperlink state may still be active.

If application-text output and cleanup both fail, both failures remain observable
through the existing `AggregateException` behavior.

---

## 8. Session disposal

Session cleanup closes OSC 8 state before ordinary presentation-state restoration.

If one or more hyperlink scopes remain active, T48 makes one best-effort
canonical OSC 8 close attempt. One successful close is sufficient to leave the
terminal outside all library-owned hyperlink scopes, regardless of stack depth.

After session cleanup begins:

- no new hyperlink acquisition may commit output;
- active lease objects are marked released by the owner;
- disposing those lease objects afterward is a no-op;
- cleanup failures participate in the session's existing output/restoration
  exception aggregation path.

The existing internal presentation-cleanup stage is used as the session output
state closure point so OSC 8 state is closed before presentation capabilities are
restored and before the final output flush/host-mode restoration completes.

---

## 9. Cancellation

Acquisition observes caller cancellation before the begin frame is committed.

Once a lease has been acquired, `DisposeAsync()` is intentionally not caller-
cancellable. Cleanup must remain possible even when the application operation
which used the link was cancelled.

The bounded `WriteHyperlinkAsync(...)` continues to observe cancellation only
before its begin frame is committed; after that, text and cleanup proceed without
caller-driven truncation.

---

## 10. Output and flush policy

Acquire, restore, close, and bounded hyperlink operations participate in the
existing session-owned output gate.

T48 does not introduce implicit flushing. Session disposal retains the existing
final deterministic flush policy.

Direct writes through the borrowed `session.Output` service remain outside the
hyperlink ownership guarantee, as with other session-owned semantic output.

---

## 11. Test evidence

`TerminalSessionHyperlinkLeaseTests` adds focused coverage for:

- single-scope begin/text/close behavior;
- nested different targets;
- nested identical targets;
- outer-state restoration;
- strict out-of-order rejection with zero state/output mutation;
- failed release followed by successful retry;
- session disposal with multiple outstanding scopes;
- post-disposal lease no-op behavior;
- failed bounded close retained for session-disposal retry;
- cancelled acquisition with zero output.

The existing T47/T47A tests continue to cover bounded exact bytes, URI/id
validation, redirection, failure aggregation, and session-output serialization.

---

## 12. Scope boundary

T48 does not add:

- arbitrary OSC 8 parameter dictionaries;
- hyperlink querying;
- support auto-detection;
- automatic URL recognition;
- `Icod.DCurses` hyperlink cells;
- OSC 52;
- cursor-style state;
- synchronized output.

Those remain outside the 0.6 scoped ownership tranche.

---

## 13. T48 gate

T48 is complete when the repository matrix proves:

1. scoped acquisition emits validated canonical OSC 8 state;
2. nesting is strict LIFO;
3. inner release restores outer library-owned state;
4. failed release remains retryable;
5. bounded `WriteHyperlinkAsync(...)` and scoped leases share one ownership model;
6. outstanding state is closed during session disposal;
7. no implicit flush or raw OSC public extension surface is introduced.

The next tranche is **T49 — integration, compatibility, and security acceptance**.
