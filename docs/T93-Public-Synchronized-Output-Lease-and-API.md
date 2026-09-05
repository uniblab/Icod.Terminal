# T93 — Public Synchronized-Output Lease and API

**Release:** `0.9.0`  
**Tranche:** `T93`  
**Development version:** `0.9.0-alpha.4`  
**Status:** Implemented; exact-head validation pending  
**Theme:** minimal semantic public API over the T92 ownership manager

## Public delta

T93 introduces exactly one public lease type and one `TerminalSession` operation:

```csharp
public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable {
	public ValueTask DisposeAsync();
}

public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
	CancellationToken cancellationToken = default
);
```

No public raw private-mode API, support flag, mode number, nesting depth, or generic CSI surface is introduced.

## Acquisition semantics

`AcquireSynchronizedOutputAsync(...)` acquires one logical synchronized-output owner.

- first logical owner emits `CSI ? 2026 h`;
- additional logical owners emit no begin frame;
- acquisition is caller-cancellable before logical ownership commits;
- committed control-frame transmission remains non-truncatable;
- successful acquisition means protocol emission or successful logical joining only;
- successful acquisition does not prove that the terminal recognizes or continues honoring private mode 2026.

## Lease disposal semantics

`TerminalSynchronizedOutputLease.DisposeAsync()` releases exactly one logical owner.

- non-final release emits nothing;
- final release emits `CSI ? 2026 l` and flushes once;
- disposal order is identity-aware and need not be LIFO;
- successful disposal is idempotent;
- failed final disposal retains lease ownership so the same lease can retry cleanup;
- after session disposal has already cleared the manager, later lease disposal emits nothing.

The lease intentionally has no public state property because every owner requests the same boolean synchronized-output state.

## Failure posture

The public lease does not weaken T92 cleanup semantics.

If final release fails, `DisposeAsync()` propagates the failure and does not mark the lease released. A later call can retry the same owner release. Session disposal remains the authoritative final cleanup path.

## Capability posture

The public API remains optimistic semantic emission.

It does not:

- infer support from `TERM` or terminal identity;
- run DECRQM automatically;
- cache a support bit;
- promise that terminal-side synchronized presentation remains active for the lease lifetime.

## Tests

T93 public-contract tests cover:

- nested public leases using one begin and one final end;
- out-of-order public lease disposal;
- idempotent successful disposal;
- retry through the same lease after failed final release;
- lease disposal after session disposal producing no additional output;
- pre-cancelled acquisition producing no output.

## Decision

T93 freezes the public API shape above for the remainder of 0.9 unless T94–T96 integration evidence exposes a concrete semantic flaw.
