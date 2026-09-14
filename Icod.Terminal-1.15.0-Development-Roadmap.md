# Icod.Terminal 1.15.0 Development Roadmap

**Release:** `1.15.0`  
**Theme:** Unicode Placeholder and Virtual Raster Placement  
**Status:** approved design; implementation plan complete; T150 ready  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** stable `1.14.0`

## Release objective

Version 1.15 adds a backend-neutral semantic abstraction for Unicode-placeholder raster presentation.

The release introduces an opaque `TerminalRasterPlaceholder` handle representing an acknowledged terminal-side virtual raster placement. Applications and higher-level renderers such as `Icod.DCurses` can derive semantic placeholder-cell tokens from that handle and emit them as ordinary terminal text without learning Kitty image ids, placement ids, Unicode placeholder encoding, SGR identity packing, combining-mark tables, or raw APC graphics commands.

The governing rule is:

> Terminal owns protocol identity and encoding; higher-level renderers own cells, cursor position, clipping, scrolling, damage, and layout.

Design authority:

[`docs/superpowers/specs/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement-design.md`](docs/superpowers/specs/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement-design.md)

Implementation plan:

[`docs/superpowers/plans/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement.md`](docs/superpowers/plans/2026-09-14-1.15.0-unicode-placeholder-virtual-raster-placement.md)

Permanent ownership authority to be extended during qualification:

[`docs/Persistent-Raster-Ownership.md`](docs/Persistent-Raster-Ownership.md)

## Why this release follows 1.14

The persistent-raster progression becomes:

```text
1.11  opaque persistent resources and placements
1.12  bounded source-pixel cropping and signed z-order
1.13  immutable-parent relative placement ownership
1.14  lifecycle observability for owned resources and placements
1.15  virtual placements and Unicode-placeholder text-grid rendering
```

Version 1.14 made Terminal's local ownership certainty visible. Version 1.15 uses that mature ownership/lifecycle foundation to add a different presentation mechanism: an invisible virtual placement whose visible instances are ordinary text cells.

The feature is intentionally designed for higher-level text-grid renderers. It does not make `Icod.Terminal` responsible for screen layout or damage management.

## Approved public-model direction

The 1.15 ownership model is:

```text
TerminalRasterResource
    |
    +-- TerminalRasterPlacement
    |       ordinary or relative physical placement
    |
    +-- TerminalRasterPlaceholder
            acknowledged virtual placement
            |
            +-- TerminalRasterPlaceholderCell
                    semantic text-grid token
```

### `TerminalRasterPlaceholder`

A placeholder is an opaque virtual-placement handle.

It owns:

- virtual-placement lifetime;
- rows and columns;
- private virtual-placement identity;
- session/generation association;
- lifecycle state.

It does not own:

- an absolute screen position;
- a window;
- a DCurses cell;
- damage state;
- scrolling policy;
- layout;
- animation.

### `TerminalRasterPlaceholderCell`

A placeholder cell is an immutable semantic token for one placeholder row/column coordinate.

The public token exposes semantic coordinates only. Its private association with one specific placeholder/session remains internal.

A cell token is not independently disposable.

## Candidate public API

T150 owns the exact API spelling freeze. The approved candidate shape is:

```csharp
public sealed class TerminalRasterPlaceholderOptions {
    public int Columns { get; init; }
    public int Rows { get; init; }
}

public sealed class TerminalRasterPlaceholder : IAsyncDisposable {
    public int Columns { get; }
    public int Rows { get; }

    public TerminalRasterOwnershipState OwnershipState { get; }

    public TerminalRasterPlaceholderCell GetCell(
        int row,
        int column
    );
}

public readonly struct TerminalRasterPlaceholderCell {
    public int Row { get; }
    public int Column { get; }
}
```

`TerminalRasterResource` adds virtual-placeholder creation approximately equivalent to:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlaceholder>>
    CreatePlaceholderAsync(
        TerminalRasterPlaceholderOptions options,
        CancellationToken cancellationToken = default
    );
```

`TerminalSession` adds current-cursor semantic output approximately equivalent to:

```csharp
public ValueTask WriteRasterPlaceholderCellAsync(
    TerminalRasterPlaceholderCell cell,
    CancellationToken cancellationToken = default
);

public ValueTask WriteRasterPlaceholderCellsAsync(
    ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
    CancellationToken cancellationToken = default
);
```

`TerminalRasterResource` adds a relative-parent overload approximately equivalent to:

```csharp
public ValueTask<TerminalControlResult<TerminalRasterPlacement>>
    CreateRelativePlacementAsync(
        TerminalRasterPlaceholder parent,
        int columnOffset,
        int rowOffset,
        TerminalRasterPlacementOptions? options = null,
        CancellationToken cancellationToken = default
    );
```

T150 may refine naming, collection shape, and option construction. It may not expose protocol identity or move screen-layout ownership into Terminal.

## Public identity boundary

The 1.15 public surface must not expose:

```text
Kitty image id
Kitty image number
Kitty placement id
virtual-placement protocol id
session generation id
U+10EEEE
row/column combining-mark table
foreground-color image-id packing
underline-color placement-id packing
U=1
raw APC graphics dictionaries
backend selection
```

The new token type must not have a public constructor that accepts protocol identity.

## Placeholder dimensions and capacities

Both placeholder dimensions are required.

The portable public bound is:

```text
Columns  1..256
Rows     1..256
```

Version 1.15 does not add source crop or z-order to placeholder options.

Existing capacity ceilings remain one combined bounded ownership system:

```text
maximum live persistent resources                          256
maximum live physical + virtual placements               4096
maximum relative-placement depth                            8
maximum placeholder rows                                  256
maximum placeholder columns                               256
```

Virtual placements count against the existing 4096-placement ceiling.

## Private virtual-placement ID rule

Ordinary placement IDs retain their existing private 32-bit domain.

Virtual-placement IDs are allocated from the private nonzero range:

```text
1..0x00FFFFFF
```

This matches the 24-bit placement identity representable in Unicode-placeholder underline color while remaining far above the bounded 4096-placement registry.

Allocation must remain nonzero, collision-safe, monotonic where practical, and wrap-safe.

## Self-contained cell contract

Every generated placeholder cell must be independently renderable.

Each cell's internal encoding contains enough information for:

```text
complete image identity
complete virtual-placement identity
explicit row
explicit column
```

The implementation will not use left-neighbor shorthand.

This is a release-level requirement because shorthand would undermine:

- clipping;
- sparse redraw;
- horizontal scrolling;
- overlapping rasters;
- damage-based rendering;
- arbitrary cell ordering;
- DCurses virtual-screen diffing.

## Cell-generation contract

Cell generation is synchronous and side-effect free:

```csharp
TerminalRasterPlaceholderCell cell = placeholder.GetCell(row, column);
```

It performs no:

- terminal write;
- query registration;
- output-gate acquisition;
- capability verification;
- lifecycle mutation;
- cleanup;
- replay;
- backend selection.

Rows/columns outside the declared placeholder dimensions are rejected locally.

## Current-cursor emission contract

Placeholder cells are ordinary text-grid output.

The typed single-cell and bulk output methods emit at the caller's current text cursor position.

Terminal does not choose screen coordinates or move to a semantic window location.

The caller owns cursor movement, clipping, scrolling, layout, and redraw order.

Bulk output preserves caller ordering exactly.

The initial implementation performs no cross-cell compression/shorthand.

## SGR identity boundary

Placeholder encoding temporarily uses foreground and underline colors as protocol identity channels.

Each independently emitted cell must terminate private identity state with selective resets equivalent to:

```text
SGR 39
SGR 59
```

Background color is not altered solely for raster identity.

Unrelated rendition attributes remain unchanged.

This prevents protocol-private identity colors from leaking into following application text.

## Lifecycle model

`TerminalRasterPlaceholder` reuses the final 1.14 ownership vocabulary.

Required semantics are:

```text
new acknowledged placeholder
    Current / None

session generation invalidation
    Stale / SessionStateLost

correlated missing resource
    Stale / ResourceMissing

owning resource intentional disposal
    Released / ResourceReleased

explicit placeholder wrapper disposal
    Disposed / ExplicitDisposal
```

`AncestorReleased` is not ordinarily applicable because a virtual placement cannot itself be relative.

Explicit wrapper disposal remains wrapper-local and final for that wrapper.

A stale/released/disposed placeholder never becomes current again.

## Token validity

A placeholder cell is not independently owned, but output must validate its owning placeholder/session before emitting private identity.

Tokens are rejected before output if their owner is:

- stale;
- released;
- disposed;
- from another session;
- from an invalid generation.

No stale numeric identity may be emitted.

## Semantic capability model

Version 1.15 should add one capability, with exact name frozen in T150:

```text
TerminalCapability.UnicodeRasterPlaceholders
```

Expected numeric value:

```text
10
```

Existing released capability values remain unchanged.

The semantic distinction is:

```text
RasterGraphics
    ephemeral raster display

PersistentRasterGraphics
    terminal-resident resource and physical-placement ownership

UnicodeRasterPlaceholders
    virtual-placement ownership and Unicode placeholder-cell rendering
```

Placeholder support uses persistent resources but must not be inferred solely from `PersistentRasterGraphics`.

## Capability evidence rules

`InspectCapability(...)` remains side-effect free.

No terminal-brand heuristic is introduced.

No fictional placeholder-specific live query may be added.

T151 must explicitly audit whether a truthful bounded live verification path exists. If it does not, `VerifyCapabilityAsync(...)` retains controlled no-safe-probe semantics.

Successful placeholder creation acknowledgement is authoritative for that specific creation transaction.

## Placeholder creation and acknowledgement

`CreatePlaceholderAsync(...)` reuses the authoritative query/input transaction manager.

The internal request conceptually contains:

```text
placement creation
resource private image id
private virtual-placement id
U=1
columns
rows
```

The public handle is returned only after a successful correlated acknowledgement.

Pre-commit invalid operations generate no output.

Icod.Terminal does not deliberately discard acknowledgement using quiet mode.

## Error semantics

Local no-output failures include:

```text
null options
Columns outside 1..256
Rows outside 1..256
stale/disposed resource
wrong session
combined placement capacity exhausted
invalid placeholder cell coordinates
pre-commit cancellation
```

A correlated missing-resource `ENOENT` follows established resource/placement invalidation semantics.

Malformed responses, wrong identities, timeout, late response, and generic transport failure do not manufacture missing-resource truth.

If transport fails after placeholder creation commits:

- no public placeholder is published;
- no blind retry occurs;
- no automatic backend switch occurs;
- no raster replay/re-upload occurs;
- no unsupported certainty is invented.

Failure while writing visible placeholder text remains ordinary committed text-output failure and does not automatically stale the virtual placement.

## Virtual placeholder as relative parent

A physical `TerminalRasterPlacement` may use a `TerminalRasterPlaceholder` as its immutable parent.

The virtual placeholder itself cannot be relative.

The relationship is:

```text
TerminalRasterPlaceholder
    virtual text anchor
        |
        +-- TerminalRasterPlacement
                physical relative child
```

Existing 1.13 relative-placement guarantees continue to apply:

- immutable parentage;
- signed cell offsets;
- maximum depth 8;
- separate raster-resource lifetime;
- descendant-before-parent cleanup;
- no reparenting;
- `ENOPARENT`, `ECYCLE`, `ETOODEEP`, and `ENOENT` hardening.

Disposing/releasing the virtual parent releases dependent relative placements according to existing placement-lifetime semantics while raster resources remain independently owned unless separately released or invalidated.

## DCurses boundary

`Icod.DCurses` continues to own:

```text
cells
windows
screen coordinates
clipping
damage
scrolling
layout
refresh ordering
virtual-screen diffing
```

`Icod.Terminal` owns:

```text
virtual placement ownership
placeholder token generation
protocol-private identity
placeholder encoding
acknowledgement correlation
lifecycle certainty
serialized terminal output
```

The public 1.15 token model must be suitable for later DCurses adoption without importing Kitty protocol concepts.

No DCurses source change is required for 1.15 success. Stable downstream package acceptance remains mandatory.

## Explicit non-goals

Version 1.15 does not add:

- animation/frame lifecycle;
- absolute screen-coordinate raster placement;
- pixel-within-cell positioning;
- general scene graphs;
- window ownership;
- cell ownership inside Terminal;
- reparenting;
- automatic placeholder redraw;
- tracking emitted placeholder screen positions;
- terminal-authenticated object existence;
- hidden source-image replay;
- caller-visible protocol ids;
- public Unicode-placeholder raw encoding helpers;
- raw Kitty dispatch;
- Sixel placeholder emulation;
- image decoding/transcoding;
- PTY/ConPTY hosting.

# Tranche roadmap

## T150 — Architecture/API-regret gate and public contract freeze

**Objective:** freeze the minimum additive public semantic surface before production implementation.

Required RED public-shape tests cover:

- `TerminalRasterPlaceholderOptions`;
- `TerminalRasterPlaceholder`;
- `TerminalRasterPlaceholderCell`;
- placeholder creation on `TerminalRasterResource`;
- current-cursor single/bulk cell output;
- placeholder-relative-parent overload;
- placeholder `OwnershipState`;
- additive capability value;
- absence of public image/placement/generation ids;
- absence of public Kitty/raw-encoding/backend vocabulary.

T150 must freeze:

- exact capability spelling;
- exact creation method spelling;
- exact output method spelling;
- options constructor/init semantics;
- bulk collection type;
- XML wording around text-grid ownership and protocol identity.

Deliverable:

```text
docs/Public-API-Baseline-1.15.md
```

The final `.sha256` fingerprint is generated later from the built package/API freeze.

## T151 — Semantic capability and protocol-support evidence model

**Objective:** expose truthful Unicode-placeholder capability planning without brand guessing or invented probes.

Required qualification:

- existing capability numeric values unchanged;
- new additive capability value;
- side-effect-free `InspectCapability(...)`;
- generic Kitty graphics evidence does not automatically become placeholder-verified evidence without proof;
- endpoint availability remains separate from support knowledge;
- generation invalidation expires live evidence appropriately;
- no hidden query is emitted when no truthful bounded placeholder probe exists.

If a safe live verification primitive is identified, its exact request/response semantics must be reviewed and tested before use.

## T152 — Virtual-placement ownership and lifecycle integration

**Objective:** implement acknowledged virtual-placement ownership below the frozen API.

Internal work includes:

- placeholder state object;
- owning resource association;
- private 24-bit placement-id allocation;
- combined physical/virtual capacity accounting;
- lifecycle state;
- registry membership;
- acknowledged virtual-placement creation;
- placeholder disposal;
- resource-disposal integration;
- session-drain integration.

Required tests include:

```text
1x1 creation
256x256 creation
0 and 257 dimension rejection
combined placement-capacity boundary
virtual placement-id collision/wrap handling
correlated acknowledgement
wrong identity
ENOENT
stale generation
explicit placeholder disposal
resource disposal
session teardown
```

## T153 — Self-contained placeholder-cell token and encoder

**Objective:** generate independently renderable semantic cells.

The encoder must always carry:

```text
foreground identity = low 24 image-id bits
underline identity  = <=24-bit virtual placement id
row diacritic       = explicit
column diacritic    = explicit
image high byte     = explicit
```

No neighbor inheritance is allowed.

Required reference tests include:

```text
row 0
row 255
column 0
column 255
image low-24 boundary
image high byte 0
image high byte 255
placement id 1
placement id 0xFFFFFF
exact Unicode scalars
exact UTF-8 bytes
no shorthand
no public raw identity
```

Official protocol examples should be reproduced as exact reference vectors where applicable.

## T154 — Typed current-cursor placeholder emission

**Objective:** integrate semantic placeholder cells with session-managed output while leaving layout to the caller.

Required behavior:

- single-cell output;
- bulk output;
- exact caller ordering;
- current-cursor semantics;
- normal output-gate serialization;
- cancellation before commitment;
- committed-output integrity after commitment;
- selective foreground/underline reset;
- unrelated rendition preserved;
- stale/released/disposed token rejected before output;
- cross-session token rejected before output;
- no query registration;
- no lifecycle mutation merely because a cell is emitted.

Bulk work must remain bounded and must not build an unbounded aggregate output buffer.

## T155 — Virtual-parent relative-placement integration

**Objective:** permit an ordinary physical placement to use a virtual placeholder as immutable relative parent.

Required matrix:

```text
physical child relative to virtual placeholder
positive offsets
negative offsets
crop/extents/z-order retained on child
immutable parentage
virtual placeholder itself cannot be relative
cross-resource child
placeholder disposal releases child
placeholder resource remains independent
child resource remains independent
ENOPARENT
ECYCLE defensive handling
ETOODEEP existing graph semantics
session invalidation
```

No parent protocol id becomes public.

## T156 — Error, lifecycle, capacity, and concurrency hardening

**Objective:** adversarially qualify the complete mixed physical/virtual ownership model.

Required matrix:

```text
256 resources
4096 combined physical/virtual placements
24-bit virtual placement-id wrap/collision
placeholder create/dispose churn
resource with many placeholders
mixed ordinary/relative/virtual registry
session invalidation during placeholder work
concurrent OwnershipState reads
concurrent GetCell(...) calls
concurrent placeholder output
output-gate contention
wrong acknowledgement identities
malformed acknowledgement
late response
timeout
transport failure before commitment
transport failure after commitment
stale token output
disposed token output
cross-session token misuse
maximum 256x256 coordinate generation
```

Concurrency tests must use fixed bounded work and bounded memory.

## T157 — Samples and downstream integration qualification

**Objective:** make the abstraction executable and prove its suitability for higher-level renderers.

Add a focused sample, preferably:

```text
samples/Icod.Terminal.RasterPlaceholder.Sample
```

The sample demonstrates:

1. semantic capability inspection/verification as appropriate;
2. raster resource creation;
3. virtual placeholder creation;
4. semantic cell-token generation;
5. caller-controlled cursor positioning;
6. complete small placeholder-grid output;
7. sparse redraw of one cell;
8. output of cells in non-raster order;
9. optional physical child placement relative to the virtual placeholder;
10. deterministic cleanup.

The sample must contain no Kitty ids, placeholder codepoint, raw combining-table values, raw APC commands, or backend branching.

Stable `Icod.DCurses` package acceptance/hardening remains a release gate.

## T158 — Package/API/XML/security/documentation qualification

**Objective:** qualify the feature through the packed NuGet and permanent documentation.

Required package work:

- fresh package-only placeholder consumer;
- compile/run on `net8.0`, `net9.0`, `net10.0`;
- generated XML documentation checks for every new public type/member;
- public API fingerprint generation;
- public identity-exclusion checks;
- stable production dependency verification;
- package README synchronization;
- sample verifier integration.

Permanent documentation to update:

```text
README.md
docs/Persistent-Raster-Ownership.md
docs/Architecture.md
docs/Security-and-Privacy.md
docs/Compatibility-and-Versioning.md
samples/README.md
Icod.Terminal-Development-Roadmap.md
Icod.Terminal-1.15.0-Development-Roadmap.md
docs/Public-API-Baseline-1.15.md
```

Historical release documents remain unchanged.

## T159 — Stable 1.15.0 release closure

**Objective:** produce one exact stable release candidate and qualify the complete release matrix.

Required release authorities:

```text
VersionPrefix = 1.15.0
CHANGELOG.md
docs/releases/1.15.0.md
PackageReleaseNotes
root README
final 1.15 API baseline
final 1.15 SHA fingerprint
permanent ownership authority
main roadmap
versioned roadmap
sample documentation
```

No new production package dependency is expected solely for placeholder support.

The final exact head must pass:

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

The roadmap must not self-certify the commit that contains its own release-closure status.

Merge, tag, GitHub Release creation, and NuGet publication remain explicit maintainer actions.

## Testing strategy

Testing proceeds from the semantic contract outward:

1. public API RED/GREEN freeze;
2. capability evidence contract;
3. virtual-placement ownership and acknowledgement;
4. placeholder-cell token generation;
5. exact protocol encoding;
6. current-cursor typed output;
7. virtual-parent relative-placement bridge;
8. lifecycle/error/capacity/concurrency hardening;
9. sample and downstream acceptance;
10. fresh package-only consumer/XML/API qualification;
11. complete Windows/Linux/macOS release matrix.

Every public lifecycle state and capability state used by the feature requires a direct executable witness.

Exact byte tests are required for protocol encoding.

State-machine tests are required for ownership.

Package-only tests are required for the shipped surface.

## Compatibility requirements

Version 1.15 is additive over the stable `1.0.0` compatibility floor and the complete 1.14 public surface.

Existing consumers that never call placeholder APIs retain existing:

- resource/placement behavior;
- current-cursor and relative placement wire behavior;
- source crop and signed z-order;
- lifecycle observation;
- generation-scoped invalidation;
- 256-resource / 4096-placement ceilings;
- depth-8 relative graph bound;
- deterministic cleanup;
- no hidden replay;
- opaque protocol identity.

No existing public enum numeric value changes.

## Release success definition

Version 1.15 is complete when a consumer can:

```text
create an opaque acknowledged virtual placeholder
    -> derive semantic row/column cell tokens
    -> store/reorder/clip those tokens independently
    -> emit them at caller-owned cursor positions
    -> render every cell without left-neighbor inheritance
    -> observe placeholder lifecycle using the 1.14 state model
    -> use the virtual placeholder as parent for a physical relative placement
```

without learning Kitty image/placement ids, placeholder Unicode/diacritic encoding, SGR identity packing, backend selection, or session generation identity, and without moving window/cell/layout/damage/scene ownership into `Icod.Terminal`.