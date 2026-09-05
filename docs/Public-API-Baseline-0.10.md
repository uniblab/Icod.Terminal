# Icod.Terminal 0.10 Public API Baseline

**Release:** `0.10.0`  
**Theme:** OSC 9;4 terminal progress ownership  
**Status:** Frozen stable public surface

---

## Public API delta from 0.9

`0.10.0` adds one semantic enum, one scoped owner, and one `TerminalSession` acquisition operation.

```csharp
namespace Icod.Terminal;

public enum TerminalProgressState {
	Normal,
	Error,
	Attention
}

public sealed class TerminalProgressLease : IAsyncDisposable {
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
}

public sealed partial class TerminalSession {
	public ValueTask<TerminalProgressLease> AcquireProgressAsync(
		CancellationToken cancellationToken = default
	);
}
```

---

## Semantic contract

- `ReportAsync(completed, total)` reports normal determinate progress.
- `ReportAsync(state, completed, total)` reports determinate normal/error/attention progress.
- `SetIndeterminateAsync()` selects indeterminate progress.
- callers report integral completed/total work; the library computes OSC 9;4 percentage internally.
- `total` must be positive and `completed` must be between zero and `total` inclusive.
- percentage conversion uses integer arithmetic and nearest-integer rounding with exact halves upward.
- successful emission proves protocol output, not emulator support.

---

## Ownership contract

- acquiring a lease emits nothing until that owner reports a value;
- nested ownership is ordered and identity-aware but does not require LIFO disposal;
- a newer owner which has never reported does not mask a lower reported owner;
- non-controlling owner updates are logical-only;
- releasing the controlling owner restores the newest remaining reported owner;
- final release clears library-owned terminal progress;
- successful repeated disposal is idempotent;
- failed cleanup retains ownership for retry.

---

## Lifecycle contract

- suspend clears physical terminal progress while retaining logical ownership;
- resume restores the current controlling logical progress when one remains;
- releasing all owners while suspended prevents progress re-entry;
- `TerminalSession.InvalidateState()` marks progress physical state untrusted;
- session disposal performs authoritative progress cleanup before synchronized-output final leave;
- late lease disposal after successful session cleanup emits nothing.

---

## Deliberate omissions

`0.10.0` does not add:

- a raw or generic OSC 9 public writer;
- public wire-state numbers;
- support inference/probing from operating system, `TERM`, environment variables, or emulator identity;
- a duplicate OSC 9;9 current-working-directory API over the existing OSC 7 semantic operation;
- host-executing/blocking ConEmu OSC 9 operations;
- OSC 4 palette mutation.

Those omissions are part of the reviewed 0.10 contract rather than missing implementation.
