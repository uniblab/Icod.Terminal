# T143 — Lifecycle-Safe Dynamic-Color Ownership

**Project:** `Icod.Terminal`  
**Release line:** `0.14.0`  
**Development version:** `0.14.0-alpha.4`  
**Status:** Implemented; exact-head validation pending

## Purpose

T143 extends the lifecycle-safe observed-state ownership model established by T140-T142 from OSC 4 indexed palette entries to the selected non-Tektronix dynamic-color family:

- `DefaultForeground` — OSC 10
- `DefaultBackground` — OSC 11
- `TextCursor` — OSC 12
- `MouseForeground` — OSC 13
- `MouseBackground` — OSC 14
- `HighlightBackground` — OSC 17
- `HighlightForeground` — OSC 19

Reset controls OSC 110-114/117/119 remain terminal-policy resets and are never used as exact restoration.

## Public contract

T143 adds:

```csharp
public sealed class TerminalDynamicColorLease : IAsyncDisposable

public ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
    TerminalDynamicColor kind,
    TerminalColor color,
    TimeSpan queryTimeout,
    CancellationToken cancellationToken = default
);
```

The lease exposes its semantic `Kind` and requested `Color`.

## Acquisition

The first owner for one dynamic-color identity must successfully observe that exact identity before mutation. The baseline query therefore precedes the first set frame.

Failure to obtain a truthful observation preserves the existing active-query failure model:

- timeout -> `TimeoutException`;
- caller cancellation -> cancellation;
- malformed correlated reply -> `FormatException`;
- session/transport failures remain distinct.

No mutation is retained when baseline observation fails.

Nested owners for the same identity do not re-query the terminal. The newest active owner controls the physical color.

## Identity isolation and ordering

Ownership is keyed by `TerminalDynamicColor`.

Different dynamic-color identities retain independent external baselines and owner stacks. Owners of the same identity may be disposed out of order. Releasing a non-controlling owner changes logical ownership only; releasing the controlling owner applies the next owner or the external baseline.

Unscoped dynamic-color mutation/reset is rejected while any scoped dynamic-color owner exists. Explicit query operations remain permitted because observation alone does not invalidate restoration ownership.

## Exact restoration

Final release explicitly emits the set form for the observed baseline color:

```text
OSC Ps ; rgb:rrrr/gggg/bbbb ST
```

where `Ps` is 10, 11, 12, 13, 14, 17, or 19 for the owned identity.

The manager never substitutes the corresponding reset control.

## Invalidation

`TerminalSession.InvalidateState()` marks physical dynamic-color state uncertain while retaining logical ownership and the current lifecycle-epoch baselines.

Before a later ownership transition, retained effective colors are re-established from logical state.

## Suspend and resume

Before managed suspend, the manager restores every active dynamic-color identity to its external baseline.

During resume, T141's internal observation window queries every still-owned identity again. Those replies become the new external baselines for the new lifecycle epoch. Only after all required observations succeed does normal participant resume reapply retained effective owner colors.

The tests cover this behavior for both:

- the common/core tier through OSC 10;
- the extended xterm tier through OSC 19.

## Re-entry rollback

Dynamic-color re-entry is transactional across managed identities.

If reapplying a later identity fails after earlier identities have already been reapplied, T143 restores the successfully reapplied identities to their freshly observed post-resume baselines before propagating the failure. Rollback failures are preserved with the original re-entry error.

## Session cleanup

`TerminalSession.DisposeAsync()` restores every managed dynamic-color identity to its latest truthful baseline and then releases logical owners. A late lease disposal after successful session cleanup is a no-op.

## Existing unscoped API

The existing APIs remain available when no scoped dynamic-color owner is active:

```text
SetDynamicColorAsync(...)
QueryDynamicColorAsync(...)
ResetDynamicColorAsync(...)
```

Their 0.13 semantics remain unchanged: set/reset are unscoped; query is an explicit live observation.

## Validation coverage

T143 adds deterministic coverage for:

- query-before-mutate and exact baseline replay for all seven supported identities;
- absence of OSC 110-114/117/119 restoration;
- same-identity nested ownership with out-of-order release;
- independent ownership of different identities;
- unscoped mutation/reset exclusion while scoped ownership exists;
- post-resume fresh baseline observation and retained-owner replay on common and extended tiers.

T144 remains responsible for broader failure, cancellation, concurrency, and stress hardening shared by palette and dynamic-color ownership.
