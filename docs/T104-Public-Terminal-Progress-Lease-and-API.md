# T104 — Public Terminal Progress Lease and API

**Release:** `0.10.0`  
**Tranche:** `T104`  
**Development version:** `0.10.0-alpha.5`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T104 exposes the T100–T103 OSC 9;4 progress model through a scoped semantic public API. Callers report work in domain units such as stages, items, or bytes; callers never construct OSC frames or percentages.

---

## 2. Public acquisition

`TerminalSession` now exposes:

```csharp
public ValueTask<TerminalProgressLease> AcquireProgressAsync(
	CancellationToken cancellationToken = default
);
```

Acquisition:

- requires interactive terminal output;
- is cancellable before logical ownership commits;
- emits no OSC 9;4 frame by itself;
- rejects acquisition while progress state is suspended;
- rejects acquisition while cleanup debt from a prior failed physical transition remains unresolved;
- proves only logical ownership, not terminal support.

---

## 3. Public lease

T104 introduces:

```csharp
public sealed class TerminalProgressLease : IAsyncDisposable
```

with:

```csharp
public ValueTask ReportAsync(
	long completed,
	long total,
	CancellationToken cancellationToken = default
);

public ValueTask ReportAsync(
	TerminalProgressState state,
	long completed,
	long total,
	CancellationToken cancellationToken = default
);

public ValueTask SetIndeterminateAsync(
	CancellationToken cancellationToken = default
);

public ValueTask DisposeAsync();
```

The two-argument report overload uses `TerminalProgressState.Normal`.

---

## 4. Caller-facing stage semantics

A caller can report stages directly:

```csharp
await progress.ReportAsync( 1, 10 );
await progress.ReportAsync( 2, 10 );
```

The internal T102 value model converts those to 10% and 20% respectively.

Callers may likewise report other finite work units without changing the API:

```csharp
await progress.ReportAsync( processedFiles, totalFiles );
await progress.ReportAsync( bytesCopied, totalBytes );
```

---

## 5. Semantic rendering states

Determinate reports may select:

```text
Normal
Error
Attention
```

For example:

```csharp
await progress.ReportAsync(
	TerminalProgressState.Error,
	3,
	10
);
```

Indeterminate progress is explicit:

```csharp
await progress.SetIndeterminateAsync();
```

No fabricated completed/total values are required for the indeterminate state.

---

## 6. Nested ownership

The public lease inherits T103 ordered, identity-aware ownership.

Example:

```text
outer reports 30%       -> emit normal 30
inner indeterminate      -> emit indeterminate
outer reports 40%       -> logical-only update
inner dispose            -> restore normal 40
outer dispose            -> clear
```

Out-of-order non-controlling disposal is physically silent.

---

## 7. Lease operation serialization

Each `TerminalProgressLease` serializes its own report, indeterminate, and disposal operations through a private operation gate.

This prevents a concurrent dispose from racing through a report on the same logical owner.

Caller cancellation applies while waiting to perform a report/update. Disposal itself is non-caller-cancellable so cleanup ownership cannot be abandoned by a disposal token.

---

## 8. Disposal semantics

Successful disposal:

- releases the logical owner;
- restores the next controlling reported owner when applicable;
- clears progress when no reported owner remains;
- is idempotent on repeated calls.

If manager release fails, the lease retains its manager/owner identity so a later disposal can retry cleanup.

After session disposal, late lease disposal emits no additional terminal output.

A report attempted after successful lease disposal throws `ObjectDisposedException`.

---

## 9. Tests

`TerminalProgressLeaseTests` covers:

- acquisition emits nothing;
- `1 / 10` and `2 / 10` stage reporting;
- `Error` and `Attention` determinate states;
- explicit indeterminate state;
- nested restoration of the latest outer logical value;
- out-of-order non-controlling disposal;
- invalid report emits nothing;
- idempotent successful disposal;
- report-after-dispose rejection;
- pre-cancelled acquisition/report emits nothing;
- late disposal after session disposal emits nothing.

---

## 10. Scope boundary

T104 does not complete the deep lifecycle/failure matrix. T105 remains responsible for:

- suspend/resume acceptance;
- release while suspended;
- write/cleanup failure retry;
- re-entry failure and double-failure behavior;
- state invalidation integration;
- combined disposal/failure ordering.

---

## 11. T104 decision

T104 is implemented with a scoped semantic public progress API supporting determinate completed/total reporting, semantic status states, indeterminate progress, nested restoration, and deterministic disposal. Exact-head validation is required before T105 begins.
