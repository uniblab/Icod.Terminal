# Icod.Terminal 1.7.0 Development Roadmap

**Release:** `1.7.0`  
**Theme:** complete DCS construction, Sixel graphics, and the first common raster-display contract  
**Status:** D170–D176 complete; D177 implemented and validating  
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
| D174 | `bf0542b77127a5af4d627480cb8d3797710dc93e` | `34405392319` |
| D175 | `005992b27c95fd5c0142d23922aee40e929237e8` | `34407124131` |
| D176 | `9e48c1cee4e44a0f272378ff4f60b4cabc9bed8a` | `34408371837` |

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

**Status:** Complete.

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

**Status:** Complete.

**Goal:** encode the common raster model into correct bounded Sixel payload.

Completed implementation:

- deterministic six-row band traversal and partial-final-band handling;
- per-register mask generation using low-bit-as-top-row semantics;
- used-register-only palette definitions in ascending register order;
- deterministic RGB8-to-protocol-percentage conversion;
- explicit register selection on every color pass;
- leading/interior zero columns retained and trailing zero columns omitted;
- `$` between color passes and exactly one `-` between bands;
- deterministic empty/all-transparent band progression;
- repeat syntax used only when strictly shorter than raw run data;
- bounded reusable band workspace and bounded lazy payload segments;
- tiny hand-verifiable complete DCS golden vectors;
- no public Sixel API.

Permanent contract: `docs/D175-Sixel-Encoder.md`.

## D176 — committed streaming graphics output transaction

**Status:** Complete.

**Goal:** emit large graphics safely through the existing `TerminalSession` output-ownership model.

Completed semantics:

- acquires the existing session-output serialization gate before the first frame byte commits;
- honors caller cancellation while waiting for that gate and immediately before commitment;
- treats the first canonical Sixel DCS prefix write as the commit boundary;
- ignores ordinary caller cancellation after commitment so the control string cannot be truncated mid-frame;
- streams D175 bounded payload segments without allocating a complete encoded graphic;
- emits exactly one seven-bit ST on successful completion;
- surfaces transport failures without retry or speculative terminator recovery;
- flushes once after the successful final ST;
- prevents ordinary session output from interleaving inside the committed frame;
- drains committed session output before teardown proceeds into output-state restoration;
- includes deterministic tests for pre-commit cancellation, post-commit cancellation, interleaving, transport failure, small-frame byte equivalence, and disposal ordering.

Permanent contract: `docs/D176-Committed-Streaming-Graphics-Output.md`.

## D177 — Sixel capability evidence and live observation

**Status:** Implemented; exact-head validation pending.

**Goal:** integrate Sixel into the normalized capability/evidence architecture without branding heuristics.

Completed implementation:

- Primary DA attribute `4` records `Verified / ProtocolResponse` evidence for `DcsSixel`;
- successful existing `QueryPrimaryDeviceAttributesAsync(...)` calls opportunistically update Sixel evidence after typed parsing;
- valid Primary DA responses without attribute `4` record only `Unknown / ProtocolResponse`, not `Unsupported`;
- the internal bounded Sixel probe records `Unknown / LiveProbe` on timeout and returns false only as “not verified by this probe”;
- caller cancellation propagates and is not translated into negative evidence;
- malformed correlated Primary DA responses fail before Sixel evidence changes;
- existing live-generation invalidation expires Sixel protocol-response/live-probe evidence;
- verified `DcsSixel` evidence selects Sixel for `RasterGraphics` through the existing deterministic resolver;
- no TermInfo, built-in-profile, terminal-name, `TERM`, OS, vendor, registry-order, or caller-preference heuristic is added;
- no public API or Primary DA wire-byte change is introduced.

Permanent contract: `docs/D177-Sixel-Capability-Evidence-and-Live-Observation.md`.

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
