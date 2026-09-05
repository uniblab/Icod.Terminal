# Icod.Terminal 0.9 Public API Baseline

**Release:** `0.9.0`  
**Status:** Frozen stable public delta

## 1. Scope

The 0.9 release adds synchronized-output ownership using DEC private mode 2026 without exposing raw CSI/private-mode construction or fabricated capability detection.

The stable public delta from 0.8 is intentionally limited to one lease type and one acquisition method.

## 2. New public type

```csharp
namespace Icod.Terminal;

public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable {
	public ValueTask DisposeAsync();
}
```

The lease owns one logical request for synchronized output.

Multiple leases share first-owner/last-owner physical mode transitions. Out-of-order disposal is valid because every owner requests the same boolean synchronized-output state.

A failed final disposal retains cleanup ownership so the same lease can retry. Successful disposal is idempotent. Session disposal remains authoritative cleanup and invalidates outstanding lease objects without duplicate output.

## 3. New TerminalSession method

```csharp
public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
	CancellationToken cancellationToken = default
);
```

Successful completion means the logical ownership request was established and any required first-owner begin frame was emitted successfully. It does not prove that the attached terminal implements or continues honoring private mode 2026.

The operation performs no automatic capability query and infers no support from `TERM`, operating system, emulator identity, or environment variables.

## 4. Wire semantics

The first logical owner emits:

```text
ESC [ ? 2 0 2 6 h
```

The final logical owner emits:

```text
ESC [ ? 2 0 2 6 l
```

followed by one output flush.

Nested acquisition/release contributes no repeated begin/end frames.

## 5. Lifecycle semantics

With active logical owners, managed suspension leaves synchronized output and flushes before suspension. Logical ownership survives the suspended interval. Resume re-enters mode 2026 only when logical owners remain.

Releasing all owners while suspended is logical-only and prevents re-entry.

## 6. Composition

Synchronized output is a terminal-side presentation-timing bracket. It does not buffer application bytes inside `Icod.Terminal` and does not change the semantics of operations performed while the lease is active.

Application text, semantic OSC operations, cursor-style operations, presentation transitions, rich-input protocol transitions, and active terminal queries remain valid according to their existing contracts.

The pre-existing advanced `WriteTerminalStringAsync(...)` surface retains its historical low-level contract: direct external callers remain responsible for coordinating raw terminfo output with concurrent session traffic.

## 7. Explicit non-additions

0.9 does not add:

- a public raw private-mode number API;
- a generic CSI/DECSET/DECRST writer;
- `SupportsSynchronizedOutput` or another inferred support flag;
- automatic DECRQM probing;
- a synchronized-output nesting-depth property;
- an application-side transaction buffer;
- automatic synchronized output around every write or presentation lease.

## 8. Compatibility

The package continues to target:

- `net8.0`;
- `net9.0`;
- `net10.0`.

T96 acceptance demonstrates that published `Icod.DCurses 0.1.0` can execute a real `CursesSession.RefreshAsync()` over the current 0.9 `TerminalSession` while bracketed by synchronized output.

This document is the stable public API baseline for 0.9.0.
