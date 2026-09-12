# Icod.Terminal 1.12.0 Development Roadmap

**Release:** `1.12.0`  
**Theme:** bounded advanced persistent-raster placement geometry  
**Status:** T120–T127 implemented; post-closure default-value hardening accepted; final documentation-only exact-head qualification pending  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** published `1.11.1`

## Release objective

Version 1.12.0 extends the persistent-raster ownership model introduced in 1.11 with exactly two narrowly bounded placement controls:

- pixel-space source rectangles; and
- signed z-order.

The release deliberately does not expand into relative placement graphs, Unicode placeholders, animation, cells/windows/layout, or other scene-graph responsibilities.

Before the new public surface landed, T121 completed the approved table-driven cleanup of `TerminalTermInfoSemanticEvidence` so reviewed TermInfo evidence contracts are represented once while preserving exact behavior.

Design authority:

[`docs/superpowers/specs/2026-09-12-1.12.0-advanced-raster-placement-design.md`](docs/superpowers/specs/2026-09-12-1.12.0-advanced-raster-placement-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-12-1.12.0-advanced-raster-placement.md`](docs/superpowers/plans/2026-09-12-1.12.0-advanced-raster-placement.md)

Permanent persistent-ownership authority:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Frozen public surface

The final 1.12 additive public API is:

```csharp
public readonly struct TerminalRasterSourceRectangle {
	public TerminalRasterSourceRectangle(
		int x,
		int y,
		int width,
		int height
	);

	public int X { get; }
	public int Y { get; }
	public int Width { get; }
	public int Height { get; }
}

public sealed class TerminalRasterPlacementOptions {
	public int? Columns { get; set; }
	public int? Rows { get; set; }
	public TerminalRasterSourceRectangle? SourceRectangle { get; set; }
	public int? ZIndex { get; set; }
}
```

No public API was added after T123. The post-closure default-value hardening is internal-only and preserves this surface.

Source rectangles use zero-based source-image pixels and must fit completely within the owning resource. Every present rectangle is revalidated by placement options, including `default(TerminalRasterSourceRectangle)` values that bypass the public constructor. `ZIndex` accepts the full signed `int` domain. `UpdateAsync(...)` remains a complete replacement of the placement at the current cursor position, not a partial patch against prior options.

The final reviewed public API fingerprint remains:

```text
eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8
```

## Tranche status

```text
T120  architecture/API regret gate + roadmap normalization                 accepted
T121  table-drive TerminalTermInfoSemanticEvidence                         accepted
T122  source-rectangle public contract + resource-aware validation         accepted
T123  z-order public contract + validation                                 accepted
T124  create/update encoder and acknowledged placement integration         accepted
T125  lifecycle/adversarial/boundary hardening                             accepted
T126  sample/package-only consumer/XML docs/downstream qualification       accepted
T127  API freeze/release docs/three-OS/package release closure             accepted, then pre-merge hardening extended qualification
```

## Accepted checkpoints

### T124 — placement integration

Accepted exact head:

```text
aba1f7c0989d2c451294edf75590637c227a7e05
```

Workflow:

```text
#1653 / 34713178166
```

All nine PR jobs passed.

The reviewed wire order is:

```text
Ga=p,i=<id>,p=<id>,C=1[,x=...[,y=...[,w=...[,h=...]]]][,c=...][,r=...][,z=...]
```

A present source rectangle emits all four crop fields together. Signed z-order uses invariant decimal formatting. Existing 1.11 placement bytes remain unchanged when the advanced options are omitted.

### T125 — hardening

Accepted exact head:

```text
799d096fa439c43b4f31399a551c4524fe40fa10
```

Workflow:

```text
#1657 / 34716833678
```

All nine PR jobs passed.

Acceptance covers exact-edge crops, combined options, invalid-before-output behavior, `int.MinValue` / `int.MaxValue`, wrong identity, malformed/duplicate fields, correlated `ENOENT`, timeout/late-response ownership, generation invalidation, stale local-only disposal, repeated advanced ownership cycles, and unchanged capacity ceilings.

### T126 — sample/package/downstream qualification

Accepted exact head:

```text
7b38994c1ba936df5887d1aa015394c4ed626ddf
```

Workflow:

```text
#1658 / 34717103814
```

All nine PR jobs passed.

Acceptance includes:

- backend-neutral persistent-raster source cropping and z-order create/update usage;
- sample documentation explaining source-pixel crops and signed stacking order without scene-layout claims;
- fresh NuGet-only consumption of `TerminalRasterSourceRectangle`, `SourceRectangle`, and `ZIndex` on `net8.0`, `net9.0`, and `net10.0`;
- generated XML documentation checks for the complete new public surface;
- current Stable 1.x downstream `Icod.DCurses` acceptance/hardening soak with no downstream code change.

### T127 — original stable release candidate

Accepted exact head:

```text
0b7961f6d8151253be57f65a69e17a12ec4bdec5
```

Workflow:

```text
#1659 / 34717812704
```

All nine PR jobs passed with stable `1.12.0` package metadata, the frozen public API, downstream soak, and validated package artifact.

The first closure-only exact head was:

```text
ef02714bcbd9ab84572a7ed8200a0ad8071d831a
#1660 / 34718248621
```

That head also passed all nine jobs.

## Pre-merge default-value hardening

A final documentation/sample/test audit identified one value-type edge case: callers can construct `default(TerminalRasterSourceRectangle)` without executing the validating public constructor. Before the fix, that zero-sized value could pass options validation and reach placement encoding.

The deterministic RED witness was established on:

```text
f5f0a4f69d3571083f63654acd35b9893e8807dd
#1662 / 34720241497
```

The three new regression tests failed on every runtime TFM because no `ArgumentOutOfRangeException` was thrown:

```text
ResourceAwareValidationRejectsDefaultRectangle
EncoderRejectsDefaultRectangle
PublicCreateAndUpdateRejectDefaultRectangleBeforeOutput
```

The minimal root fix makes `TerminalRasterSourceRectangle` own one reusable intrinsic validator and makes `TerminalRasterPlacementOptions.Validate()` revalidate every present rectangle before encoding or resource-aware bounds checks.

The GREEN implementation head was:

```text
4d509c75decc37d280a92769fe668d3c6fbd41ce
#1663 / 34720415393
```

All nine jobs passed. The public API/package freeze remained green, proving the fingerprint stayed unchanged.

The same commit also normalized the executable sample and sample catalog from ambiguous “relative z-order” wording to **signed z-order / stacking order**, reserving “relative placement” for the explicitly excluded parent-relative placement feature.

## Final documentation consistency pass

After #1663, current consumer/permanent authorities are synchronized to:

- describe `ZIndex` as signed z-order / signed stacking order rather than relative placement;
- explicitly document that placement options revalidate default struct rectangle values;
- record #1662 as the RED regression witness and #1663 as the accepted GREEN functional head;
- preserve the final public API fingerprint and dependency graph.

This pass is documentation-only. Its exact head must pass the complete nine-job PR matrix before merge.

## Compatibility guardrails

Version 1.12 preserves:

- every pre-existing public signature and enum numeric value;
- 1.11 placement bytes/behavior when the new options are unused;
- one authoritative input/query path;
- opaque terminal resource/placement identities;
- generation-scoped persistent ownership;
- no hidden source-raster retention or replay;
- resource ceiling `256` and placement ceiling `4096`;
- direct-transfer persistent transport only;
- production dependencies `Icod.TermInfo 1.11.0` and `Icod.Timing 1.0.0`.

## Explicit exclusions

Version 1.12.0 does not include:

- relative placements or parent placement identities;
- placement chains, cycle detection, or graph lifetime ownership;
- Unicode placeholder/virtual placements;
- animation/frame lifecycle;
- absolute screen-coordinate placement or pixel offsets inside terminal cells beyond source cropping;
- caller-selected raster backend;
- public Kitty protocol identities;
- generic raw Kitty command dispatch;
- automatic replay/re-upload after invalidation;
- retained source-image caches;
- image-file decoding/transcoding;
- PTY/ConPTY process hosting;
- cells/windows/damage/layout policy belonging to `Icod.DCurses`.

## Release gate

```text
T120–T126 accepted
    -> T127 original stable candidate #1659 green
        -> closure-only #1660 green
            -> pre-merge audit finds default-value edge case
                -> RED #1662
                    -> GREEN #1663
                        -> documentation consistency pass
                            -> final exact-head nine-job matrix
                                -> maintainer handoff
```

After the final exact-head green result, merge, mainline Release validation, `v1.12.0` tagging, GitHub Release creation, and NuGet publication remain maintainer/release-workflow actions.
