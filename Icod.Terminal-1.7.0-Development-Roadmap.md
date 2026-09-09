# Icod.Terminal 1.7.0 Development Roadmap

**Release:** `1.7.0`  
**Theme:** complete DCS construction, Sixel graphics, and the first common raster-display contract  
**Status:** D170–D173 complete; D174 implemented and validating  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.6.0`

## Why this release exists

`1.5.0` normalized control-family framing, structural frame parsing, query transactions, capability evidence, and semantic backend routing. `1.6.0` then completed the CSI grammar, consolidated existing CSI users, added internal terminal/cell pixel observations, and hardened fragmented/oversized correlated-response recovery.

Version `1.7.0` applies the same discipline to DCS and delivers the first raster graphics backend: Sixel.

The release is deliberately split between three concerns which must not be conflated:

```text
DCS framing/construction
    -> Sixel dialect codec
        -> common semantic raster operation
```

DCS remains a control-family substrate. Sixel is one DCS dialect. The raster model is semantic image data and must not become a Sixel-specific byte container.

## External protocol references

The implementation is based on the DEC/xterm Sixel model in which:

```text
DCS Pa ; Pb ; Ph q <sixel data> ST
```

is the image transport, with Primary Device Attributes parameter `4` advertising Sixel graphics where that evidence is meaningful.

Reference behavior is treated as protocol evidence, not as permission to infer support from terminal brand or operating system.

## Release invariants

1. One live `TerminalSession` remains the authoritative input reader.
2. DCS structural parsing remains the normalized 1.5 `TerminalControlFrameStructure` path.
3. Canonical library-generated DCS output uses seven-bit `ESC P ... ESC \\` framing unless an exact selected-terminal capability explicitly requires another representation.
4. DCS parameter bytes, intermediate bytes, final selector, payload, and terminator remain distinct layers.
5. No generic raw public DCS writer is introduced.
6. Existing DECRQSS and XTGETTCAP public behavior and exact request bytes remain stable.
7. Sixel encoding is bounded by dimensions, pixel count, palette count, work memory, and output transaction size.
8. Large graphics must not require one giant complete encoded-frame allocation.
9. Image-file decoding is outside `Icod.Terminal`; the core raster contract consumes raw pixel/index data.
10. Quantization and palette construction are deterministic for identical input/options.
11. Capability evidence distinguishes `Unavailable`, `Unsupported`, `Unknown`, `Advertised`, and `Verified`; timeout is not unsupported truth.
12. Primary DA Sixel evidence is interpreted only within the reviewed DA capability semantics; terminal branding is not a Sixel support oracle.
13. The first public raster surface, if frozen in 1.7, must be semantic and backend-neutral enough for Kitty Graphics to implement in 1.8 without a source break.
14. Sixel-specific advanced controls remain separate from the common raster contract.
15. Existing stable 1.0–1.6 APIs and documented ownership/security/restoration guarantees remain compatible.

## Accepted checkpoints

| Tranche | Exact head | Staging workflow |
| --- | --- | --- |
| D170 | `7d338fe5a1de122738d4573093db90e1500003a1` | `34399549931` |
| D171 | `277db7da8a586dda44966fa77990b4a9f32e953a` | `34400644772` |
| D172 | `75d5562e5b1c0813a52d43f445365a228948a5f3` | `34401396507` |
| D173 | `b2674f6cb356ca49a0b2564721c12f2eae9b9265` | `34402697961` |

Every accepted checkpoint passed Windows, Linux, macOS runtime validation, package candidate, all four package-contract shards, and the validated artifact.

## D170 — DCS construction contract and reference freeze

**Status:** Complete.

**Goal:** establish one internal canonical DCS construction primitive and freeze the boundaries that later Sixel code depends on.

Completed work:

- added the internal `DcsWriter` construction primitive;
- canonical seven-bit `ESC P` introducer and `ESC \\` terminator;
- structural validation of parameter bytes (`0x30`–`0x3F`), intermediate bytes (`0x20`–`0x2F`), and final selector (`0x40`–`0x7E`);
- opaque payload treatment at the DCS layer while rejecting CAN, SUB, ESC, and C1 ST bytes that would abort or terminate framing;
- bounded complete-frame encoding for small/query/control frames with a 4,096-byte ceiling;
- byte-exact tests for empty/non-empty structural fields, invalid grammar, exact maximum size, and maximum-plus-one;
- round-trip proof through the normalized `TerminalControlFrameStructure` parser;
- explicit separation between small complete-frame construction and the later committed streaming transaction needed by large Sixel graphics;
- no public raw DCS API and no Sixel policy in the generic writer.

Permanent contract: `docs/D170-DCS-Construction-Contract-and-Reference-Freeze.md`.

## D171 — existing DCS reconciliation

**Status:** Complete.

**Goal:** move existing DCS emitters onto the canonical DCS construction substrate without changing released behavior.

Completed migration set:

- DECRQSS request construction now uses `DcsWriter`;
- XTGETTCAP request construction now uses `DcsWriter`;
- existing DECRPSS and XTGETTCAP structural response parsing remains unchanged;
- DECRQSS retains all twelve frozen request identifiers;
- XTGETTCAP retains printable-ASCII name validation and uppercase hexadecimal request encoding;
- canonical request bytes remain `ESC P $ q <identifier> ESC \\` and `ESC P + q <hex-name> ESC \\` respectively;
- existing seven-bit/eight-bit DCS and mixed-ST inbound compatibility remains unchanged;
- query correlation, timeout/cancellation behavior, late-response ownership, and one-reader semantics remain unchanged;
- protocol-level tests freeze all twelve DECRQSS request forms and representative XTGETTCAP names;
- consolidated requests round-trip through `TerminalControlFrameStructure` with the expected intermediate, final selector, payload, and terminator.

Permanent contract: `docs/D171-Existing-DCS-Reconciliation.md`.

## D172 — Sixel grammar and codec contract

**Status:** Complete.

**Goal:** freeze the Sixel dialect boundary above generic DCS.

Completed contract:

- canonical explicit DCS parameters `0;1;0`;
- `Pa=0` with explicit raster attributes owning pixel shape;
- `Pb=1` background-preserving zero-bit semantics;
- ignored historical `Ph=0`;
- square-pixel raster attributes `"1;1;<width>;<height>`;
- sixel values `0..63` mapped to `?`..`~`;
- bounded repeat command construction;
- graphics carriage return/new line (`$` / `-`);
- RGB-only color-register selection/definition over registers `0..255` and components `0..100`;
- self-contained image palette rule: every image defines every register it uses;
- no claim of exact restoration of external Sixel palette state;
- no silent xterm private-color-register mode 1070 ownership;
- no public raw Sixel API.

Permanent contract: `docs/D172-Sixel-Grammar-and-Codec-Contract.md`.

## D173 — common raw raster model

**Status:** Complete.

**Goal:** create the backend-neutral image representation shared by Sixel and future Kitty Graphics.

Completed model:

```text
Rgb24      tightly packed R G B
Rgba32     tightly packed R G B A
Indexed8   one-byte palette indices + RGBA8 palette
```

The model:

- owns immutable copies of caller pixel/palette data;
- uses exact tight-packed row lengths with no implicit stride/padding;
- preserves straight/unpremultiplied RGBA alpha exactly;
- treats RGB24 as implicitly opaque;
- validates every Indexed8 palette reference;
- bounds dimensions to 16,384, total pixels to 16 Mi, owned pixel storage to 64 MiB, and palettes to 256 entries;
- performs no gamma, color-profile, premultiplication, compositing, or image-file decoding;
- remains internal pending D178 public review.

Permanent contract: `docs/D173-Common-Raw-Raster-Model.md`.

## D174 — deterministic palette and quantization policy

**Status:** Implemented; exact-head validation pending.

**Goal:** convert true-color raster input into a bounded Sixel palette reproducibly.

Completed implementation:

- internal `SixelPaletteQuantizer` with configurable palette ceiling `1..256`;
- exact byte-for-byte Indexed8 passthrough when the existing palette fits and is fully opaque;
- lossless exact-color path when distinct opaque colors fit the requested ceiling, ordered by first row-major appearance;
- explicit Sixel alpha policy: alpha `0` is transparent/untouched, alpha `255` is opaque, fractional alpha `1..254` is rejected rather than silently composited;
- transparent pixels use a separate mask and consume no Sixel color register;
- all-transparent rasters are valid and produce an empty palette;
- fixed 5-bit-per-channel `32 x 32 x 32` histogram for bounded high-entropy work memory;
- deterministic weighted median-cut reduction with explicit channel, box-selection, sort, and palette-order tie-breaking;
- rounded weighted RGB representatives;
- deterministic nearest-palette remapping using squared RGB distance and lower-index ties;
- no dithering, gamma conversion, color management, or hidden matte policy;
- regression coverage for exact indexed input, exact RGB color order, transparent and all-transparent input, fractional-alpha rejection, palette ceilings 1/256, high-entropy reduction, repeated-run determinism, and coordinate bounds.

Permanent contract: `docs/D174-Deterministic-Sixel-Palette-and-Quantization.md`.

## D175 — Sixel encoder

**Goal:** encode the common raster model into correct bounded Sixel payload.

### Required work

- six-row band traversal;
- per-color mask generation;
- correct horizontal carriage and vertical band progression;
- deterministic palette-definition order;
- raster attributes where required by the frozen contract;
- run-length encoding using Sixel repeat syntax only when it reduces or preserves canonical size according to the chosen policy;
- omission of redundant commands where canonicalization permits it;
- exact edge behavior when height is not divisible by six;
- overflow-safe encoded-size accounting;
- byte-exact golden tests for small hand-verifiable rasters.

D175 should expose internal chunks/segments suitable for D176 rather than forcing creation of one giant byte array.

## D176 — committed streaming graphics output transaction

**Goal:** emit large graphics safely through the existing `TerminalSession` output-ownership model.

### Required semantics

- acquire the session control/output serialization boundary before the first frame byte commits;
- observe caller cancellation before commitment;
- once DCS transmission commits, do not allow caller cancellation to truncate the control string mid-frame;
- stream bounded chunks without allocating the full encoded graphic;
- guarantee exactly one final ST on successful completion;
- define transport-failure behavior explicitly when failure occurs after commitment;
- do not silently retry partial graphics;
- keep flush policy explicit;
- prevent ordinary semantic output from interleaving inside one Sixel DCS transaction;
- retain disposal/lifecycle safety.

This tranche is a security/reliability boundary, not merely a performance optimization.

## D177 — Sixel capability evidence and live observation

**Goal:** integrate Sixel into the normalized capability/evidence architecture without branding heuristics.

### Evidence sources to review

- selected TermInfo metadata where a complete, semantically relevant Sixel advertisement exists;
- built-in profile evidence only where explicitly justified;
- Primary Device Attributes parameter `4` as protocol-response evidence for Sixel graphics;
- optional reviewed live probes only when they do not create destructive terminal state.

### Required semantics

- positive Primary DA Sixel evidence may produce `Verified / ProtocolResponse` for `DcsSixel`;
- absence of parameter `4` in a response whose capability semantics are authoritative may produce a reviewed negative result only when that inference is protocol-correct;
- timeout/cancellation remains unknown/no conclusion;
- live evidence remains generation-scoped and is invalidated by the existing state-invalidation rules;
- a terminal name, `TERM`, OS, or caller preference never becomes capability proof.

## D178 — first semantic raster-display operation

**Goal:** expose the smallest stable semantic raster operation that future Kitty Graphics can also implement.

### Design rule

The public API must describe **what image to display**, not **how to speak Sixel**.

Candidate concepts include:

```text
TerminalRasterImage
TerminalRasterPixelFormat
TerminalRasterDisplayOptions
TerminalSession.DisplayRasterAsync(...)
```

Names are not frozen until D173–D177 validate the shape.

### Required behavior

- backend-neutral raw raster input;
- explicit placement semantics only where portable enough to preserve across Sixel and Kitty Graphics;
- no generic Sixel command/string escape hatch;
- truthful unsupported/unavailable behavior when no qualified backend can execute the operation;
- 1.7 may route only to Sixel even though the semantic contract is designed for 1.8 Kitty Graphics;
- public API baseline advances intentionally only after final review.

Sixel-specific palette/register/display-mode controls that do not map cleanly to a common semantic operation stay internal or separate typed APIs.

## D179 — hardening, package, documentation, and release closure

**Goal:** qualify `1.7.0` as the first stable raster-capable `Icod.Terminal` release.

### Required evidence

- Windows/Linux/macOS Staging runtime/source validation;
- Release validation after merge;
- `net8.0`, `net9.0`, and `net10.0` consistency;
- retained 1.0–1.6 compatibility gates;
- explicit public-API baseline review/update if D178 becomes public;
- byte-exact DCS regression coverage for DECRQSS/XTGETTCAP;
- hand-verifiable Sixel golden vectors;
- quantizer determinism/property coverage;
- dimension/pixel/palette/encoded-size boundary tests;
- fragmentation/malformed DCS response regressions retained;
- committed-output cancellation and transport-failure tests;
- large-raster bounded-memory tests;
- capability-evidence positive/negative/timeout/invalidation tests;
- current `Icod.DCurses` compatibility witness;
- README, changelog, compatibility/security docs, package metadata, release notes, roadmaps, and PR summary synchronized.

## Public API strategy

D170–D177 should remain internal unless a clearly reusable semantic type is proven necessary sooner.

D178 is the intended public review point. The preferred outcome is one small backend-neutral raster surface that 1.8 can implement through Kitty Graphics without changing existing callers.

The following remain explicitly out of scope for 1.7:

- public raw DCS writing;
- ReGIS implementation;
- Kitty Graphics implementation;
- PNG/JPEG/GIF decoding;
- terminal-brand automatic activation;
- filesystem/shared-memory image transport;
- animation;
- persistent image identifiers/placements;
- generalized graphics scene management.

## Relationship to later releases

```text
1.5.0  normalized control families / evidence / routing
    |
1.6.0  complete CSI grammar / consolidation / geometry
    |
1.7.0  DCS construction / Sixel / common raster operation
    |
1.8.0  APC / Kitty Graphics / multi-backend raster routing
```

## Release rule

A green development PR is necessary but not sufficient to publish `1.7.0`.

After merge, the resulting `main` head must pass Release distribution validation. Tagging and publishing `v1.7.0` remain separate explicit actions after that post-merge validation succeeds.
