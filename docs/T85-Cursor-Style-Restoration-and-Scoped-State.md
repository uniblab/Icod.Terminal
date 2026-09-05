# T85 — Cursor-Style Restoration and Scoped State

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.6`  
**Status:** Implemented; validation pending  
**Predecessor:** T84 — typed cursor-style query and observation

## 1. Decision

T85 accepts a public cursor-style lease because the T84 DECRQSS observation path
can provide an authoritative semantic style before library-owned mutation.

The lease is conditional rather than optimistic. If the outer acquisition cannot
observe a recognized current cursor style, no lease is created and no DECSCUSR
style mutation is emitted.

## 2. Public contract

The scoped API is:

```csharp
public ValueTask<TerminalCursorStyleLease> AcquireCursorStyleAsync(
    TerminalCursorStyle style,
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

`TerminalCursorStyleLease` exposes the semantic `Style` it owns and implements
`IAsyncDisposable`.

## 3. Truthful outer acquisition

The outermost acquisition:

1. validates the requested semantic style and timeout;
2. explicitly queries cursor style through the existing DECRQSS `SP q` path;
3. requires a supported, recognized `TerminalCursorStyle` observation;
4. emits the requested DECSCUSR style only after that observation succeeds;
5. records the observed style as the exact restoration baseline.

An explicit unsupported observation produces `NotSupportedException`. Timeout,
cancellation, malformed response, transport failure, and session-state failure
retain their existing meanings. None of those failures may be converted into a
guessed baseline or followed by cursor-style mutation.

## 4. Nested ownership

Cursor-style leases are strict LIFO.

Nested acquisitions do not issue another terminal query. The manager already owns
the immediately preceding semantic style and therefore uses that known style as
the inner restoration target.

Conceptually:

```text
observe A
acquire B -> set B
acquire C -> set C
release C -> restore B
release B -> restore A
```

Out-of-order release fails without changing physical cursor style.

## 5. Interaction with the unscoped setter

`SetCursorStyleAsync(...)` remains available when no cursor-style lease is active.

While a lease stack is active, unscoped mutation is rejected. Allowing it would
change physical state beneath the manager's restoration stack and make exact
restoration claims untrue.

The typed query remains explicit and may still be called independently; it does
not alter manager ownership.

## 6. Restoration failure and retry

Lease release is not caller-cancellable.

If restoration output fails, the lease remains logically owned so release may be
retried and session disposal still knows that baseline restoration is required.
The manager does not pop successful ownership state before the required restore
frame has been emitted.

The manager never substitutes:

- DECSCUSR `0`;
- DECSCUSR `1` unless the observed semantic baseline actually is blinking block;
- xterm DECSCUSR `7`;
- a hard-coded block cursor;
- a style inferred from `TERM`, operating system, or emulator identity.

## 7. Suspend and resume

The cursor-style manager is a core terminal-session lifecycle participant.

Before managed suspension, an active lease stack restores the originally observed
baseline style. This happens before the process is suspended and without
caller-driven cancellation.

After successful session re-entry, if leases remain active, the manager reapplies
the innermost active semantic style.

A lease released while the manager is in suspended state updates only the logical
stack. No cursor-style output is emitted during the suspended interval. If all
leases are released and baseline restoration had already succeeded, resume emits
nothing for cursor style. If a prior baseline restore remained pending after a
failure, that obligation is retained for re-entry or disposal.

## 8. Session disposal

Session disposal closes cursor-style ownership alongside the existing persistent
output-state managers.

If an active stack remains, disposal best-effort emits the originally observed
baseline style and then releases all lease objects from the manager. A later
`DisposeAsync()` on one of those lease objects is idempotent and emits nothing.

Cursor-style cleanup uses the internal control-output path so cleanup remains
possible after ordinary session output has stopped accepting new writes.

## 9. Cursor visibility remains independent

The lease owns DECSCUSR cursor style only. It does not acquire, release, show,
hide, or otherwise reinterpret `TerminalCursorVisibility` presentation state.

T86 must explicitly exercise this independence under broader composition and
lifecycle acceptance.

## 10. T85 gate

T85 is complete when validation proves:

1. outer acquisition queries and records a real semantic baseline before mutation;
2. unsupported/malformed/timeout/cancelled observation performs no style mutation;
3. nested leases restore strict LIFO state without redundant queries;
4. out-of-order release cannot mutate physical cursor state;
5. unscoped mutation is rejected while leases are active;
6. release failure retains restoration ownership for retry/cleanup;
7. suspend restores the observed baseline and resume reapplies the active top;
8. session disposal restores the observed baseline and invalidates outstanding leases;
9. no guessed reset or xterm parameter `7` participates in restoration.

The next tranche is **T86 — integration, compatibility, and regression acceptance**.
