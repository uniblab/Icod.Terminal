# Icod.Terminal 1.12.0 Development Roadmap

**Release:** `1.12.0`  
**Theme:** bounded advanced persistent-raster placement geometry  
**Status:** T120 accepted; T121 implementation starting  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** published `1.11.1`

## Release objective

Version 1.12.0 extends the persistent-raster ownership model introduced in 1.11 with two narrowly bounded placement controls:

- pixel-space source rectangles; and
- signed z-order.

The release deliberately does not expand into relative placement graphs, Unicode placeholders, animation, cells/windows/layout, or other scene-graph responsibilities.

Before the new public surface lands, the release completes the already-approved table-driven cleanup of `TerminalTermInfoSemanticEvidence` so reviewed TermInfo evidence contracts are represented once and behavior remains easier to audit.

The design authority is:

[`docs/superpowers/specs/2026-09-12-1.12.0-advanced-raster-placement-design.md`](docs/superpowers/specs/2026-09-12-1.12.0-advanced-raster-placement-design.md)

The implementation plan is:

[`docs/superpowers/plans/2026-09-12-1.12.0-advanced-raster-placement.md`](docs/superpowers/plans/2026-09-12-1.12.0-advanced-raster-placement.md)

The existing persistent ownership authority remains:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Frozen public direction

The additive 1.12 public surface is planned as:

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

Source rectangles are expressed in source-image pixels and must fit completely inside the resource. The public contract does not expose backend clipping behavior.

`ZIndex` accepts the full signed 32-bit `int` range. `null` retains the backend/default placement order.

`UpdateAsync(...)` remains a complete replacement of the placement at the current cursor position, not a partial patch against prior options.

## Tranche roadmap

```text
T120  1.12 architecture/API regret gate + roadmap normalization            accepted
T121  table-drive TerminalTermInfoSemanticEvidence                         in progress
T122  source-rectangle public contract + resource-aware validation          planned
T123  z-order public contract + validation                                  planned
T124  create/update encoder and acknowledged placement integration          planned
T125  lifecycle/cancellation/malformed-response/boundary hardening          planned
T126  sample/package-only consumer/XML docs/downstream qualification        planned
T127  API freeze/release docs/three-OS/package release closure              planned
```

## T120 — architecture/API regret gate

Accepted. The design, implementation plan, current-roadmap normalization, and T120 authority record freeze source rectangle + z-order as the complete 1.12 feature scope.

Acceptance:

- published `1.11.1` is the explicit base;
- source rectangle and z-order are the only new placement features approved for this release;
- relative placement, Unicode placeholders, animation, and scene-graph ownership remain excluded;
- no new `TerminalCapability` value is planned;
- no production dependency change is planned;
- stable compatibility floor remains `1.0.0`;
- the current long-range roadmap is normalized from stale 1.11.0 wording to published 1.11.1 and this 1.12 line.

See [`docs/T120-1.12.0-Architecture-and-API-Regret-Gate.md`](docs/T120-1.12.0-Architecture-and-API-Regret-Gate.md).

## T121 — table-driven TermInfo semantic evidence

Refactor the internal `TerminalTermInfoSemanticEvidence` implementation into reviewed immutable tables while preserving exact behavior.

The exact semantic contracts remain:

```text
ClipboardWrite  <- extended string Ms
CursorStyle     <- extended string Ss
PaletteColor    <- can_change_color + initialize_color
```

The metadata-backed input backend advertisements remain:

```text
CsiFocusReporting     <- fe + fd + kxIN + kxOUT
CsiBracketedPaste     <- BE + BD + PS + PE
CsiMouseReporting     <- XM + xm + key_mouse prefix validation
```

Acceptance:

- `Seed(...)` and `HasExactImplementation(...)` share one exact-semantic rule table;
- backend advertisements are represented by one reviewed backend rule table;
- all existing evidence states, subjects, sources, routing outcomes, and validation behavior remain unchanged;
- no public API or package dependency change;
- focused routing tests and full Staging matrix remain green.

## T122 — source rectangle contract

Add `TerminalRasterSourceRectangle` and `TerminalRasterPlacementOptions.SourceRectangle`.

Acceptance:

- zero-based non-negative `X`/`Y`;
- positive `Width`/`Height`;
- scalar bounds respect `TerminalRasterImage.MaximumDimension`;
- resource state retains immutable source width/height metadata only;
- create/update reject a rectangle extending beyond the actual source resource before output;
- `null` means full source image;
- no raster-pixel cache or replay behavior is introduced;
- focused constructor/options/resource-validation tests cover boundaries and no-output failures.

## T123 — z-order contract

Add `TerminalRasterPlacementOptions.ZIndex`.

Acceptance:

- nullable signed 32-bit `int` surface;
- full `int.MinValue..int.MaxValue` accepted;
- negative values preserved;
- `null` means backend/default order;
- no new capability enum or backend selector;
- source rectangle and z-order remain orthogonal options.

## T124 — persistent placement integration

Extend the existing acknowledged placement create/update path.

The reviewed backend emits:

```text
x=<source-x>
y=<source-y>
w=<source-width>
h=<source-height>
z=<signed-z-index>
```

alongside existing private image/placement identities, `C=1`, and optional cell extents.

Acceptance:

- all four source rectangle fields are emitted together;
- `z` uses invariant signed decimal formatting;
- create and update use the same encoder contract;
- existing placement acknowledgement correlation is unchanged;
- update retains private placement identity and current-cursor replacement semantics;
- omitted advanced options preserve 1.11 bytes/behavior.

## T125 — hardening

Qualify the new geometry through existing lifecycle and query ownership.

Acceptance includes:

- source rectangle exact-edge boundaries;
- invalid rectangle before output;
- min/max z-order;
- create/update with combinations of rectangle, cell extents, and z-order;
- caller cancellation before commitment;
- transport failure after commitment;
- malformed/wrong-identity/negative acknowledgement behavior;
- `ENOENT` invalidation;
- generation invalidation/stale local-only cleanup;
- resource/placement capacity ceilings unchanged;
- repeated replacement/disposal cycles.

## T126 — consumer and downstream qualification

Update or add executable sample coverage that demonstrates cropping and layering without exposing backend ids or commands.

Extend fresh package-only consumer validation for:

- `TerminalRasterSourceRectangle`;
- `SourceRectangle`;
- `ZIndex`;
- placement create/update on `net8.0`, `net9.0`, `net10.0`.

Require generated XML documentation and current `Icod.DCurses` package acceptance/soak.

## T127 — stable release closure

Before stable release:

- freeze the final 1.12 public API baseline and fingerprint;
- update README, changelog, package release notes, compatibility/security/architecture/persistent-raster authorities, sample catalog, and curated `docs/releases/1.12.0.md`;
- qualify exact head on Runtime Windows/Linux/macOS;
- pass package candidate/API freeze;
- pass Package Foundation, Presentation, Semantic and hardening, Stable 1.x release line;
- produce validated package artifact;
- leave merge, mainline Release validation, tag, and NuGet publication to the maintainer/release workflow.

## Compatibility guardrails

Version 1.12 must preserve:

- existing public signatures and enum numeric values;
- existing 1.11 behavior when the new placement options are unused;
- one authoritative input/query path;
- opaque terminal resource/placement identities;
- generation-scoped persistent ownership;
- no hidden source-raster retention or replay;
- resource ceiling `256` and placement ceiling `4096`;
- direct-transfer persistent transport only;
- production dependencies `Icod.TermInfo 1.11.0` and `Icod.Timing 1.0.0` unless a separately reviewed requirement changes them.

## Explicit exclusions

Version 1.12.0 does not include:

- relative placements;
- parent image/placement identities;
- relative horizontal/vertical offsets;
- placement chains, cycle detection, or graph lifetime ownership;
- Unicode placeholder/virtual placements;
- animation/frame lifecycle;
- pixel offsets inside terminal cells beyond source cropping;
- caller-selected raster backend;
- public Kitty protocol identities;
- generic raw Kitty command dispatch;
- image-file decoding/transcoding;
- PTY/ConPTY process hosting;
- cells/windows/damage/layout policy belonging to `Icod.DCurses`.

## Release gate

```text
approved design
    -> T121 behavior-preserving internal cleanup
        -> T122/T123 additive public value/options contracts
            -> T124 acknowledged backend integration
                -> T125 adversarial/lifecycle hardening
                    -> T126 package/downstream/sample qualification
                        -> T127 exact-head stable release closure
```
