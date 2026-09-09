# Icod.Terminal 1.7.0 Development Roadmap

**Release:** `1.7.0`  
**Theme:** complete DCS construction, Sixel graphics, and the first common raster-display contract  
**Status:** D170–D178 complete; D179 release closure in progress  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.6.0`

## Why this release exists

`1.5.0` normalized control-family framing, structural frame parsing, query transactions, capability evidence, and semantic backend routing. `1.6.0` completed the CSI grammar, consolidated existing CSI users, added internal terminal/cell pixel observations, and hardened fragmented/oversized correlated-response recovery.

Version `1.7.0` applies the same discipline to DCS and delivers Sixel as the first raster graphics backend together with the first backend-neutral public raster-display operation.

The release keeps these layers distinct:

```text
DCS framing/construction
    -> Sixel dialect codec
        -> backend-neutral raster model
            -> capability/evidence resolution
                -> semantic raster display
```

DCS is a control-family substrate. Sixel is one DCS dialect. Raster data is semantic image content and is not a Sixel byte container.

## External protocol reference

The reviewed Sixel transport is:

```text
DCS Pa ; Pb ; Ph q <sixel data> ST
```

Primary Device Attributes parameter `4` is treated as positive Sixel capability evidence. Reference behavior is protocol evidence, not permission to infer support from terminal brand, `TERM`, operating system, or caller preference.

## Release invariants

1. One live `TerminalSession` remains the authoritative input reader.
2. DCS structural parsing remains on the normalized 1.5 `TerminalControlFrameStructure` path.
3. Canonical library-generated DCS output uses seven-bit `ESC P ... ESC \\` framing unless an exact selected capability explicitly requires another representation.
4. DCS parameter bytes, intermediate bytes, final selector, payload, and terminator remain distinct layers.
5. No generic raw public DCS or Sixel writer is introduced.
6. Existing DECRQSS and XTGETTCAP public behavior and exact request bytes remain stable.
7. Sixel work is bounded by dimensions, pixel count, owned raster bytes, palette count, quantizer state, and output-segment size.
8. Large graphics do not require a complete encoded-frame allocation.
9. Image-file decoding remains outside `Icod.Terminal`; the raster API consumes raw pixel/index data.
10. Quantization and encoding are deterministic for identical input.
11. Capability evidence distinguishes `Unavailable`, `Unsupported`, `Unknown`, `Advertised`, and `Verified`; silence/timeout is not unsupported truth.
12. Primary DA attribute `4` may verify Sixel; its absence does not automatically prove Sixel unsupported.
13. The public raster surface is semantic and backend-neutral enough for Kitty Graphics to implement later without changing existing callers.
14. Sixel-specific palette/register/display controls that do not map cleanly to common raster semantics remain internal.
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
| D177 | `2e241b6d0fcec1d68c6b6aaeaf665750ddf0a053` | `34411677303` |
| D178 | `f9428927168524be5cc552c82ad00e2fcda70b13` | `34412478452` |

Every accepted checkpoint passed Windows, Linux, macOS runtime/source validation, package candidate, all four package-contract shards, and the validated package artifact.

## D170 — DCS construction contract and reference freeze

**Status:** Complete.

D170 established the internal canonical DCS writer for small bounded frames:

- seven-bit `ESC P` introducer and `ESC \\` terminator;
- parameter bytes limited to `0x30`–`0x3F`;
- intermediate bytes limited to `0x20`–`0x2F`;
- final selector limited to `0x40`–`0x7E`;
- CAN, SUB, ESC, and C1 ST rejected in payload where they could abort/terminate framing;
- 4,096-byte complete-frame ceiling;
- byte-exact and round-trip tests through normalized DCS parsing;
- no public raw DCS API.

Permanent contract: `docs/D170-DCS-Construction-Contract-and-Reference-Freeze.md`.

## D171 — existing DCS reconciliation

**Status:** Complete.

D171 moved released DCS request construction onto `DcsWriter` without changing bytes:

```text
DECRQSS    ESC P $ q <identifier> ESC \
XTGETTCAP  ESC P + q <uppercase-hex-name> ESC \
```

All twelve DECRQSS identifiers, XTGETTCAP validation/encoding, inbound seven/eight-bit DCS compatibility, query correlation, timeout/cancellation, and one-reader semantics remain intact.

Permanent contract: `docs/D171-Existing-DCS-Reconciliation.md`.

## D172 — Sixel grammar and codec contract

**Status:** Complete.

D172 froze the canonical internal Sixel dialect:

```text
DCS 0;1;0 q
"1;1;<width>;<height>
<palette definitions>
<pixel commands>
ST
```

The codec owns Sixel values `?`–`~`, bounded repeat syntax, `$`/`-` graphics movement, RGB register selection/definition, and the rule that each generated image defines every color register it uses. Version 1.7 does not claim restoration of external Sixel palette state or silently enable xterm private-color-register mode 1070.

Permanent contract: `docs/D172-Sixel-Grammar-and-Codec-Contract.md`.

## D173 — common raw raster model

**Status:** Complete.

D173 established the immutable-owned backend-neutral raster substrate:

```text
Rgb24      tightly packed R G B
Rgba32     tightly packed R G B A
Indexed8   one-byte palette indices + RGBA8 palette
```

Caller storage is snapshotted. Dimensions are bounded to 16,384, total pixels to 16 Mi, owned pixel storage to 64 MiB, and indexed palettes to 256 entries. Straight alpha is preserved; there is no image-file decoding, gamma conversion, hidden compositing, or color-profile processing.

Permanent contract: `docs/D173-Common-Raw-Raster-Model.md`.

## D174 — deterministic palette and quantization

**Status:** Complete.

D174 added the internal Sixel conversion layer:

- configurable palette ceiling `1..256`;
- exact opaque Indexed8 passthrough where possible;
- lossless exact-color mapping when colors fit;
- binary Sixel alpha semantics (`A=0` transparent, `A=255` opaque, fractional alpha rejected by the Sixel conversion);
- separate transparency mask;
- fixed `32 x 32 x 32` RGB histogram;
- deterministic weighted median-cut reduction and stable tie-breaking;
- deterministic nearest-palette remapping;
- no dithering, hidden matte, gamma conversion, or hash-order-dependent behavior.

Permanent contract: `docs/D174-Deterministic-Sixel-Palette-and-Quantization.md`.

## D175 — Sixel encoder

**Status:** Complete.

D175 implemented deterministic bounded payload generation:

- six-row band traversal including partial final bands;
- per-register masks;
- globally used register definitions in stable order;
- deterministic RGB8-to-0..100 conversion;
- explicit color selection per pass;
- leading/interior zero preservation with trailing-zero omission;
- `$` between color passes and `-` between bands;
- repeat syntax only when strictly shorter than raw data;
- bounded reusable work state and lazy bounded payload segments;
- hand-verifiable golden vectors.

Permanent contract: `docs/D175-Sixel-Encoder.md`.

## D176 — committed streaming graphics output

**Status:** Complete.

D176 connected D175 segments to the existing session output-ownership boundary:

- caller cancellation is honored while waiting for the output gate and immediately before commitment;
- the first canonical Sixel DCS prefix write is the commit point;
- ordinary caller cancellation after commitment cannot truncate the frame;
- bounded payload segments stream without one giant encoded graphic allocation;
- exactly one final ST and one flush occur on successful completion;
- transport failure is surfaced without automatic retry or speculative terminator recovery;
- ordinary session output cannot interleave inside a committed Sixel frame;
- teardown drains committed output before restoration proceeds.

Permanent contract: `docs/D176-Committed-Streaming-Graphics-Output.md`.

## D177 — Sixel capability evidence and live observation

**Status:** Complete.

D177 integrated Sixel with the normalized capability/evidence architecture:

- Primary DA attribute `4` records `Verified / ProtocolResponse` for `DcsSixel`;
- successful existing `QueryPrimaryDeviceAttributesAsync(...)` calls opportunistically update Sixel evidence after typed parsing;
- valid DA without `4` records `Unknown / ProtocolResponse`, never automatic `Unsupported`;
- bounded Sixel-probe timeout records `Unknown / LiveProbe`;
- caller cancellation propagates without negative evidence;
- malformed correlated DA responses fail before evidence changes;
- live evidence expires with the existing generation invalidation rules;
- verified `DcsSixel` evidence routes `RasterGraphics` to Sixel;
- no TermInfo/brand/`TERM`/OS/caller-preference heuristic was added.

Permanent contract: `docs/D177-Sixel-Capability-Evidence-and-Live-Observation.md`.

## D178 — first semantic raster-display operation

**Status:** Complete.

D178 intentionally added the first public raster API:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

The public raster model exposes `Rgb24`, `Rgba32`, and `Indexed8`, owns immutable snapshots of supplied pixel/palette data, and retains the D173 bounds.

`DisplayRasterAsync(...)` describes the semantic intent to display an image rather than exposing Sixel protocol commands. In 1.7:

- verified Sixel evidence routes the operation to `DcsSixel`;
- unresolved/unknown graphics support returns a controlled unavailable result rather than blindly emitting graphics;
- the operation may perform the bounded D177 Primary DA probe when verification is not already available;
- fractional alpha remains representable by the backend-neutral raster model but is returned as controlled unsupported when the selected Sixel backend cannot preserve it;
- transport failures continue to surface rather than being disguised as capability results;
- no public placement/scaling options, raw DCS/Sixel writing, palette-register controls, or explicit Sixel backend selector are frozen.

The reviewed public surface advances the current machine baseline to:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

across identical `net8.0`, `net9.0`, and `net10.0` snapshots. Historical baseline files remain intact.

Permanent contract: `docs/D178-First-Semantic-Raster-Display-Operation.md`.  
Public API baseline: `docs/Public-API-Baseline-1.7.md` / `.sha256`.

## D179 — hardening, package, documentation, and release closure

**Status:** In progress.

D179 adds no new graphics dialect or public feature. Closure work includes:

- maximum-width high-compressibility and low-compressibility Sixel segmentation witnesses;
- fresh NuGet-only raster consumer on `net8.0`, `net9.0`, and `net10.0`;
- packed XML-documentation verification for the complete D178 public surface;
- package exclusion checks proving raw DCS/Sixel writers and raster backing-memory access remain nonpublic;
- synchronization of README, changelog, architecture, security, compatibility, release notes, package documentation, roadmap, and PR ledger;
- retained DECRQSS/XTGETTCAP byte-exact tests;
- retained quantizer determinism and raster bounds;
- retained committed-output cancellation/failure/teardown tests;
- retained capability evidence positive/absence/timeout/invalidation tests;
- retained current `Icod.DCurses` package-boundary compatibility witness;
- one final unchanged exact PR head passing the complete Staging matrix.

Permanent closure record: `docs/D179-1.7.0-Hardening-Package-and-Documentation-Closure.md`.

## Public API strategy

D170–D177 remain internal implementation/evidence tranches. D178 is the intentional compatible public expansion.

The 1.7 public contract is deliberately smaller than the internal Sixel implementation. It describes raw raster data and semantic display intent; it does not expose protocol construction or Sixel-specific state.

The following remain explicitly out of scope for 1.7:

- public raw DCS/Sixel writing;
- ReGIS;
- Kitty Graphics implementation;
- PNG/JPEG/GIF decoding;
- terminal-brand automatic activation;
- filesystem/shared-memory graphics transport;
- placement/scaling policy beyond the terminal's current Sixel semantics;
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

After D179 documentation/package closure, the exact final PR head must pass the complete Staging matrix before the PR leaves draft status. Merge remains an explicit user action/authorization.

After merge, the resulting `main` head must pass Release distribution validation. Tagging and publishing `v1.7.0` remain separate explicit actions after that post-merge validation succeeds.
