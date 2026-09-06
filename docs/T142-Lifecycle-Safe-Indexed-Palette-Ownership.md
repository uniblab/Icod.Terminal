# T142 — Lifecycle-Safe Indexed Palette Ownership

**Project:** `Icod.Terminal`  
**Release line:** `0.14.0`  
**Development version:** `0.14.0-alpha.3`  
**Tranche:** T142 — indexed-palette scoped ownership  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T142 builds the first public ownership API on the T140/T141 lifecycle/query foundation.

The goal is not merely to automate OSC 4 reset. The goal is a truthful scoped contract:

> A successful palette-color lease has observed the real external color before its first mutation and can therefore restore that exact 16-bit color when the final owner releases.

OSC 104 is never used as a restoration substitute.

---

## 2. Public API

T142 adds:

```csharp
ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

and:

```csharp
public sealed class TerminalPaletteColorLease : IAsyncDisposable {
	public byte Index { get; }
	public TerminalColor Color { get; }
	public ValueTask DisposeAsync();
}
```

Acquisition preserves the existing active-query failure model:

- timeout -> `TimeoutException`;
- caller cancellation -> cancellation;
- correlated malformed response -> `FormatException`;
- session/transport failure remains distinct.

A timeout is not converted into a permanent unsupported result.

---

## 3. First-owner baseline

The first scoped owner for one palette index performs:

```text
OSC 4 ; index ; ? ST
```

and waits for a correlated OSC 4 color response.

Only after parsing that response to `TerminalColor` does the manager emit the requested owned color.

Consequently:

- a failed or malformed baseline observation creates no owner;
- no palette mutation is retained when baseline observation fails;
- the observed baseline retains full 16-bit channel precision.

---

## 4. Per-index ordered ownership

Ownership is maintained independently for each byte palette index.

For one index:

```text
external baseline = B
owner A requests A
owner B requests C
```

physical state becomes `C`.

If A is released out of order while B remains active, no physical output occurs. B remains effective.

When B is later released, the manager restores `B`, the external baseline observed before A was acquired.

Different indices maintain separate baselines and owner stacks.

---

## 5. Exact restoration

Final release emits explicit OSC 4 mutation using the saved baseline:

```text
OSC 4 ; index ; rgb:rrrr/gggg/bbbb ST
```

It does not emit:

```text
OSC 104 ; index ST
```

This distinction is part of the public correctness contract.

If restoration fails, the lease retains logical ownership so disposal may be retried. The manager marks physical state uncertain rather than falsely reporting successful restoration.

---

## 6. Unscoped mutation boundary

The existing unscoped APIs remain available when no scoped palette ownership exists:

- `SetPaletteColorAsync(...)`;
- `SetPaletteColorsAsync(...)`;
- `ResetPaletteColorAsync(...)`;
- `ResetPaletteColorsAsync(...)`;
- `ResetPaletteAsync(...)`.

While any scoped palette-color lease is active, unscoped palette mutation/reset is rejected.

This conservative rule prevents an untracked mutation from destroying the baseline/restoration guarantee of an active scoped owner.

Explicit observation through `QueryPaletteColorAsync(...)` remains allowed because observation itself does not mutate ownership state.

---

## 7. Invalidation

`TerminalSession.InvalidateState()` now marks scoped palette physical state uncertain.

It does not discard:

- the logical owner stacks;
- the current lifecycle-epoch external baselines.

Before a later ownership transition that depends on known physical state, the manager replays the current effective owned colors, then proceeds with the requested release/acquisition transition.

Invalidation does not manufacture a new external baseline.

---

## 8. Managed suspend/resume

### Suspend

Before managed suspend, every owned palette index is explicitly restored to its current external baseline.

Logical ownership is retained while the process is suspended.

### Resume observation

During the T141 internal post-resume observation window, the manager re-queries every still-owned palette index using its bounded query timeout.

The newly reported color becomes the baseline for the new lifecycle epoch.

This means an external shell or terminal may legitimately change the palette while the process is stopped.

### Reapply

After refresh, ordinary lifecycle participant resume reapplies the newest retained owner for every managed index.

If reapplying multiple indices partially fails, T142 attempts to restore the refreshed external baselines for all managed indices before propagating failure. A rollback failure is preserved together with the original re-entry failure.

Thus a failed lifecycle re-entry does not intentionally leave a mixed half-owned palette behind.

---

## 9. Session disposal

`TerminalSession.DisposeAsync()` closes scoped palette ownership as part of the session-owned output restoration path.

Every active palette index is restored to its latest truthful baseline. Outstanding lease objects are marked released by the manager, so disposing a lease after successful session cleanup is a no-op.

Cleanup failures participate in the existing aggregate terminal-restoration error path.

---

## 10. Tests

T142 adds deterministic in-memory coverage for:

- first-owner query before mutation;
- exact 16-bit baseline replay;
- no OSC 104 during scoped restoration;
- malformed baseline response causing no mutation;
- same-index nested ownership;
- out-of-order release;
- independent ownership and baselines for different indices;
- rejection of unscoped mutation while scoped ownership exists;
- invalidation followed by owned-state re-establishment;
- suspend restoring the pre-suspend baseline;
- external palette change while suspended;
- post-resume baseline re-observation;
- owned-color reapply after resume;
- final release restoring the post-resume baseline;
- session disposal with an active lease;
- late lease disposal after session cleanup.

T144 remains responsible for broader failure injection, cancellation races, partial-write ambiguity, and concurrency stress.

---

## 11. Decision

T142 establishes a truthful lifecycle-safe indexed-palette ownership contract without widening public query availability, creating a second reader, using reset as fake restoration, or discarding 16-bit observed precision.

**Next after green exact-head validation:** T143 — apply the same ownership/restoration model to the seven selected dynamic-color identities.
