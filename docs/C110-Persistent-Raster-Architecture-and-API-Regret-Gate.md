# C110 — Persistent Raster Architecture and API Regret Gate

- **Release:** `Icod.Terminal 1.11.0`
- **Tranche:** C110
- **Status:** accepted
- **Theme:** persistent raster resource/placement ownership before implementation
- **Stable compatibility floor:** `1.0.0`

## Purpose

C110 freezes the architectural and public-API decisions required before implementing terminal-resident persistent raster resources and placements.

The approved design is:

- `docs/superpowers/specs/2026-09-11-1.11.0-persistent-raster-design.md`
- `docs/superpowers/plans/2026-09-11-1.11.0-persistent-raster.md`

C110 is deliberately documentation-only. It introduces no runtime behavior or public API by itself.

## Public capability contract

Version 1.11 adds exactly one semantic capability value:

```text
TerminalCapability.PersistentRasterGraphics = 9
```

Existing 1.10 values `0..8` remain frozen.

`RasterGraphics` and `PersistentRasterGraphics` are intentionally distinct. A session may support ordinary raster display through Sixel while persistent raster ownership remains unknown or unsupported.

## Frozen public API

The approved additive public surface is:

```csharp
public sealed class TerminalRasterPlacementOptions {
    public int? Columns { get; set; }
    public int? Rows { get; set; }
}

public sealed class TerminalRasterResource : IAsyncDisposable {
    public ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreatePlacementAsync(
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );

    public ValueTask DisposeAsync();
}

public sealed class TerminalRasterPlacement : IAsyncDisposable {
    public ValueTask<TerminalControlMutationResult> UpdateAsync(
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );

    public ValueTask DisposeAsync();
}

public sealed partial class TerminalSession {
    public ValueTask<TerminalControlResult<TerminalRasterResource>> CreateRasterResourceAsync(
        TerminalRasterImage image,
        CancellationToken cancellationToken = default
    );
}
```

No public image id, image number, placement id, backend selector, raw APC/Kitty command object, registry enumeration, or mutable raster storage is approved.

## Placement semantics

`TerminalRasterPlacementOptions.Columns` and `.Rows` are nullable. A supplied value must be within:

```text
1..16384
```

Null means protocol/default intrinsic behavior.

Placement occurs at the current terminal cursor position. Persistent placement commands use `C=1`, so the graphics operation itself does not move the text cursor. Repositioning is performed by the caller through ordinary terminal cursor operations followed by `UpdateAsync(...)`, which reuses the same private placement identity.

Absolute screen coordinates, pixel offsets, source rectangles, z-order, Unicode placeholders, relative placements, animation, and scene-graph semantics are excluded from 1.11.

## Identity and registry contract

Public resource and placement handles are opaque.

Internally:

- resource upload uses a private nonzero Kitty image number (`I`);
- the terminal acknowledgement supplies the nonzero image id (`i`) used thereafter;
- placements receive private nonzero placement ids (`p`);
- zero is never allocated;
- wraparound and collision avoidance are explicit;
- no live identity is reused while still owned.

Per-session registry ceilings are:

```text
256  live persistent resources
4096 live persistent placements
```

Capacity exhaustion returns controlled `Unavailable` before terminal output.

The registry stores ownership metadata only. It does not retain `TerminalRasterImage` or arbitrary source pixel data for replay.

## Result and failure contract

The existing result types are reused:

- `TerminalControlResult<T>` for resource/placement creation;
- `TerminalControlMutationResult` for placement updates.

Meaning:

```text
Available    operation established under the current generation
Unavailable  endpoint/generation/object cannot currently satisfy the operation
Unsupported  persistent semantic capability is not supported by current verified evidence
Failed       well-formed negative terminal response or other controlled failure
```

Existing exception semantics remain:

- invalid argument/options -> argument exception;
- disposed public handle -> `ObjectDisposedException`;
- caller cancellation before commitment -> `OperationCanceledException`;
- bounded correlated timeout -> `TimeoutException`;
- correlated malformed terminal reply -> `FormatException`;
- transport failure -> propagated transport exception.

A Kitty `ENOENT` for a believed-current resource/placement invalidates that terminal-resident object and produces controlled `Unavailable`. Other well-formed Kitty negative replies produce controlled `Failed` with bounded diagnostic text.

## Protocol and acknowledgement contract

Persistent upload uses direct Kitty transmission only:

```text
a=t
t=d
I=<private image number>
```

The upload is acknowledged. Therefore it deliberately **does not use `q=2` quiet mode**. The terminal must be able to return:

```text
i=<assigned image id>,I=<private image number>;OK
```

Once that response is accepted, all future operations use the assigned image id (`i`).

Existing ephemeral raster output remains unchanged, including its established quiet direct-transfer bytes.

Placement creation/update uses `a=p` with `C=1`, assigned image id, private placement id, and optional `c`/`r`.

Placement disposal uses the lower-case image delete selector with both image and placement ids so only that placement is removed while image data remains available. Resource cleanup, after children are closed, uses the uppercase image delete selector to free image data when the terminal can do so.

## Commit and cancellation contract

Before output commitment:

- arguments/options/capability/endpoint/cancellation are validated;
- output-gate acquisition remains cancellable.

After the first frame of a logical persistent transfer commits:

- ordinary caller cancellation does not intentionally truncate the transaction;
- remaining frames and flush complete through the session serialization boundary;
- transport failure is surfaced;
- no automatic replay occurs;
- no Sixel fallback occurs;
- terminal-resident certainty is not fabricated after ambiguous partial failure.

A failed or ambiguous resource creation never publishes a usable resource handle.

## Parent/child disposal contract

A resource owns zero or more placements.

Placement disposal is locally idempotent. While current, it emits at most one targeted placement delete. It always relinquishes local ownership even if terminal cleanup fails, while surfacing the failure to the caller.

Resource disposal:

1. stops new child creation;
2. closes live child placements;
3. while current, requests terminal-side resource-data deletion;
4. releases local ownership;
5. aggregates cleanup failures.

## Lifecycle contract

Persistent terminal identities are generation-scoped.

`InvalidateState()` and managed suspend/resume invalidate terminal-resident certainty. Existing resource/placement handles become stale.

After invalidation:

- no automatic replay/re-upload occurs;
- no hidden image copy is retained for restoration;
- create-placement/update return controlled `Unavailable` before terminal output;
- stale disposal performs local bookkeeping only and emits no stale numeric terminal identity.

Session disposal remains final cleanup authority. If persistent identities are still current, placements are deleted before parent resources. Accepted committed output drains before final output-state restoration.

## Security and architectural exclusions

1. No caller-supplied raw Kitty identifiers.
2. No generic public Kitty/APC command builder.
3. No file/temp-file/shared-memory transmission.
4. No unbounded resource or placement registry.
5. Acknowledgements remain untrusted terminal-controlled input.
6. Correlation grants ownership, not trust.
7. No retained source-image cache for hidden replay.
8. No automatic retry/backend switch after partial committed output.
9. No Sixel persistent-resource emulation.
10. No cells/windows/layout/damage/scene ownership that belongs in `Icod.DCurses`.

## Result

C110 is accepted. The public/ownership design is sufficiently narrow to begin TDD implementation. C111 is the next active tranche and must first define byte-exact persistent Kitty protocol vectors without changing existing ephemeral raster output.