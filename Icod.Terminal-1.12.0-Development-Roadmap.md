# Icod.Terminal 1.12.0 Development Roadmap

**Release:** `1.12.0`  
**Theme:** bounded advanced persistent-raster placement geometry  
**Status:** T120–T126 accepted; T127 stable release-candidate qualification pending  
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

No public API was added after T123.

Source rectangles use zero-based source-image pixels and must fit completely within the owning resource. `ZIndex` accepts the full signed `int` domain. `UpdateAsync(...)` remains a complete replacement of the placement at the current cursor position, not a partial patch against prior options.

The final reviewed public API fingerprint is:

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
T127  API freeze/release docs/three-OS/package release closure             in progress
```

## Accepted checkpoints

### T120–T123

The design/API-regret gate, behavior-preserving TermInfo evidence cleanup, source-rectangle contract/resource-aware validation, z-order contract, and final public API freeze are accepted. The only intended public additions are the eight members recorded in `docs/Public-API-Baseline-1.12.md`.

### T124 — placement integration

Accepted on exact head:

```text
aba1f7c0989d2c451294edf75590637c227a7e05
```

Workflow:

```text
#1653 / 34713178166
```

All nine PR jobs passed.

The accepted backend-neutral semantic contract is implemented by one shared acknowledged placement path. The reviewed wire order is:

```text
Ga=p,i=<id>,p=<id>,C=1[,x=...[,y=...[,w=...[,h=...]]]][,c=...][,r=...][,z=...]
```

A present source rectangle emits all four crop fields together. Signed z-order uses invariant decimal formatting. Existing 1.11 placement bytes remain unchanged when the advanced options are omitted.

### T125 — hardening

Accepted on exact head:

```text
799d096fa439c43b4f31399a551c4524fe40fa10
```

Workflow:

```text
#1657 / 34716833678
```

All nine PR jobs passed.

Acceptance covers:

- source rectangles ending exactly at the source right/bottom edge;
- combined rectangle + Columns + Rows + signed z-order;
- invalid create/update rectangles rejected before new output;
- `int.MinValue` and `int.MaxValue` through real acknowledged placement operations;
- wrong identity, duplicate-field malformed response, correlated `ENOENT`, timeout, and late-response ownership;
- generation invalidation with controlled `Unavailable` and stale local-only disposal;
- 24 repeated advanced create/place/update/delete ownership cycles;
- unchanged 256-resource / 4096-placement ceilings and registry behavior.

The Windows scheduler-sensitive scripted placement witness was made deterministic with the existing frozen monotonic clock; production timeout semantics were not changed.

### T126 — sample/package/downstream qualification

Accepted on exact head:

```text
7b38994c1ba936df5887d1aa015394c4ed626ddf
```

Workflow:

```text
#1658 / 34717103814
```

All nine PR jobs passed.

Acceptance includes:

- backend-neutral `Icod.Terminal.PersistentRaster.Sample` source cropping and z-order create/update usage;
- sample documentation explaining source-pixel crops and relative stacking intent without scene-layout claims;
- fresh NuGet-only consumption of `TerminalRasterSourceRectangle`, `SourceRectangle`, and `ZIndex` on `net8.0`, `net9.0`, and `net10.0`;
- generated XML documentation checks for the new type, constructor, four properties, and both new placement-option members on all package TFMs;
- current Stable 1.x downstream `Icod.DCurses` acceptance/hardening soak with no downstream code change.

## T127 — stable release closure

The stable release candidate must:

1. keep the public API fingerprint exactly `eed5fc18e5cdd1cdadf340ba37c3664a01fb9338c2080b709168606d51d934a8`;
2. set package version metadata to stable `1.12.0`;
3. synchronize README, changelog, release notes, current roadmap, architecture, persistent ownership, security/privacy, and compatibility authorities;
4. preserve production dependencies exactly:

```text
Icod.TermInfo 1.11.0
Icod.Timing   1.0.0
```

5. pass the exact-head full Staging matrix:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / public API freeze
Package Foundation
Package Presentation
Package Semantic and hardening
Package Stable 1.x release line
Validated package artifact
```

6. record the accepted candidate SHA/workflow/fingerprint/dependency/downstream evidence in a final closure document;
7. run the same full matrix once more after closure-only status documentation;
8. leave merge, mainline Release validation, `v1.12.0` tagging, GitHub Release creation, and NuGet publication to the maintainer/release workflow.

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
T120 accepted
    -> T121 accepted
        -> T122 accepted
            -> T123 accepted / API frozen
                -> T124 accepted
                    -> T125 accepted
                        -> T126 accepted
                            -> T127 stable candidate
                                -> exact-head matrix
                                    -> closure-only record
                                        -> final exact-head matrix
                                            -> maintainer handoff
```
