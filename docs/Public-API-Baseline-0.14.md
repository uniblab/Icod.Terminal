# Icod.Terminal 0.14 Public API Baseline

**Release:** `0.14.0`  
**Theme:** lifecycle-safe terminal color ownership and exact restoration

---

## 1. Relationship to 0.13

The 0.13 observable color contract remains intact. `TerminalColor`, `TerminalPaletteColor`, `TerminalDynamicColor`, palette/dynamic set/query APIs, and terminal-policy reset APIs remain public.

0.14 adds lifecycle-safe scoped ownership. It does not reinterpret OSC 104 or OSC 110–119 reset controls as restoration.

---

## 2. Indexed-palette ownership

```csharp
ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

```csharp
public sealed class TerminalPaletteColorLease : IAsyncDisposable {
	public byte Index { get; }
	public TerminalColor Color { get; }
	public ValueTask DisposeAsync();
}
```

The first owner for one index observes the exact current palette color before any mutation. Nested owners for the same index do not re-query merely because of nesting. Owners are identity-aware and may be disposed out of order.

Releasing the controlling owner reapplies the next active owner. Releasing the final owner explicitly replays the exact observed `TerminalColor` using OSC 4 set form. OSC 104 is never used as scoped restoration.

Different palette indices have independent baselines and ownership stacks.

---

## 3. Dynamic-color ownership

```csharp
ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

```csharp
public sealed class TerminalDynamicColorLease : IAsyncDisposable {
	public TerminalDynamicColor Kind { get; }
	public TerminalColor Color { get; }
	public ValueTask DisposeAsync();
}
```

The supported identities remain:

```text
DefaultForeground      OSC 10
DefaultBackground      OSC 11
TextCursor             OSC 12
MouseForeground        OSC 13
MouseBackground        OSC 14
HighlightBackground    OSC 17
HighlightForeground    OSC 19
```

The first owner observes the exact current value before mutation. Final release replays the observed value using the corresponding set form. OSC 110–114, 117, and 119 remain terminal-policy reset operations and are never substituted for exact lease restoration.

OSC 10–12 remain the common/core interoperability tier. OSC 13/14/17/19 remain the extended xterm tier. 0.14 does not infer support from terminal brand.

---

## 4. Acquisition semantics

Scoped acquisition is query-before-mutate.

- caller cancellation before query emission performs no mutation;
- timeout remains `TimeoutException`;
- caller cancellation remains cancellation;
- a correlated malformed response remains `FormatException`;
- transport/session failures remain distinct;
- no response is not cached as permanent unsupported state.

A lease is returned only after the required baseline observation succeeds and the requested owned value is emitted.

---

## 5. Release and retry semantics

Lease disposal is asynchronous because restoration is serialized terminal output.

Successful repeated disposal is idempotent.

If physical restoration fails, the lease remains logically responsible for cleanup and a later `DisposeAsync()` may retry. A failed restoration is not silently converted to success and is not replaced by a reset-to-policy operation.

---

## 6. Invalidation

`TerminalSession.InvalidateState()` marks physical color state uncertain without discarding logical owners or the external baseline established for the current lifecycle epoch.

The next safe ownership transition re-establishes the effective owned value before relying on physical state.

---

## 7. Managed suspend/resume

Before managed suspend, active scoped color ownership restores the current externally observed baselines.

A pre-suspend baseline is not authoritative after resume. During lifecycle re-entry, session-owned observed-state managers use the internal observation window to query fresh baselines before retained ownership is reapplied.

Public query APIs remain unavailable during lifecycle re-entry and ordinary public lifecycle-participant resume callbacks.

The implementation retains the single input reader and existing query transaction/router architecture.

---

## 8. Session disposal

`TerminalSession.DisposeAsync()` is the final cleanup owner.

Active palette and dynamic-color leases do not need to be disposed before the session. Session disposal attempts exact restoration from the latest truthful lifecycle-epoch baselines. After successful session cleanup, later lease disposal is a no-op.

Higher-level consumers that own a supplied `TerminalSession`, such as `Icod.DCurses`, therefore also trigger scoped color restoration when they dispose that owned Terminal session.

---

## 9. Interaction with unscoped mutation/reset

The existing unscoped set/reset APIs remain public, but they are rejected while scoped ownership for that color family is active so they cannot silently invalidate the lease restoration guarantee.

Explicit color queries remain compatible with scoped ownership because observation does not mutate the owned state.

Reset remains distinct from restoration:

```text
OSC 104                  terminal-policy indexed-palette reset
OSC 110–114 / 117 / 119  terminal-policy dynamic-color reset
OSC set form              exact scoped restoration replay
```

---

## 10. Deliberate exclusions

0.14 adds no generic public OSC/CSI/DCS construction, terminal-brand support oracle, Tektronix OSC 15/16/18 colors, raw color string API, automatic global color probing, or authoritative long-lived terminal-color cache.

OSC 133 extended metadata remains assigned to 0.15, OSC 9 safe extensions to 0.16, modern keyboard protocols to 0.17, and broad compatibility/fuzz hardening to 0.18.

---

## 11. Compatibility

The stable package targets:

```text
net8.0
net9.0
net10.0
```

All three remain first-class supported targets. Vendor end-of-support alone does not remove net8.0 or net9.0 from the Icod.Terminal contract; removal requires a concrete security or security-maintenance reason.

This document freezes the stable 0.14 public delta.
