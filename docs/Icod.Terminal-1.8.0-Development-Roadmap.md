# Icod.Terminal 1.8.0 Development Roadmap

**Release:** `1.8.0`  
**Theme:** APC foundation, Kitty Graphics, and verified multi-backend raster routing  
**Status:** A180–A189 complete — A189 accepted on qualification head `0de1a1c6b95b2b36407a94935b3126c8bfd6ca5a`, Staging workflow #1355 / `34476585310`  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.7.0`

## Why this release exists

`1.5.0` normalized control-family framing, query transactions, capability evidence, and semantic backend routing. `1.6.0` completed CSI and added internal pixel geometry. `1.7.0` completed DCS/Sixel and introduced the first public backend-neutral raster contract:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

Version `1.8.0` adds the second raster backend without changing that caller-facing image model. Kitty Graphics is an APC dialect and therefore follows the same layering discipline established for Sixel:

```text
semantic raster intent
    -> capability/evidence resolution
        -> backend selection
            -> Kitty Graphics dialect / APC
            -> Sixel dialect / DCS
```

The primary design requirement remains that callers ask to display a raster, not to speak Kitty protocol.

## Protocol reference

The reference protocol is the Kitty terminal graphics protocol:

`https://sw.kovidgoyal.net/kitty/graphics-protocol/`

The reviewed direct-transfer framing is:

```text
ESC _ G <control-data> ; <base64-payload> ESC \
```

Direct transport (`t=d`) is the 1.8 medium. Raw RGB24 (`f=24`) and RGBA32 (`f=32`) are first-class transmission formats. Large direct transfers use Base64 payload chunks no larger than 4096 bytes and protocol continuation metadata.

The reviewed support test combines a correlated Kitty Graphics query (`a=q`) with a following Primary DA request as a synchronization barrier. A correlated APC response proves protocol handling; Primary DA arriving first is reviewed negative evidence for this concrete Kitty backend. Silence alone remains uncertainty.

## Release invariants

1. One live `TerminalSession` remains the authoritative input reader.
2. APC framing remains a generic control-family concern; Kitty `G...` syntax belongs to the Kitty Graphics dialect layer.
3. Canonical library-generated APC uses seven-bit `ESC _ ... ESC \\` framing.
4. No generic raw public APC or Kitty Graphics writer is introduced.
5. The public 1.7 raster model remains the semantic caller contract; 1.8 adds no public raster API.
6. Existing Sixel output remains byte-stable and available as the fallback raster backend when verified.
7. Kitty Graphics uses direct raw transfer in 1.8; file, temporary-file, and shared-memory media remain excluded.
8. No hidden filesystem or IPC side effects are introduced merely for graphics performance.
9. RGB24/RGBA32 are transmitted directly; Indexed8 is adapted internally without changing the public raster contract.
10. RGBA fractional alpha is preserved by Kitty Graphics rather than rejected or flattened.
11. Direct-transfer Base64 payload chunks are deterministic and bounded to 4096 bytes.
12. One logical Kitty image transfer cannot interleave with unrelated session output or another graphics transfer.
13. Caller cancellation is honored before the first committed APC frame; post-commit cancellation does not intentionally strand an incomplete transfer.
14. Transport failure after commitment is surfaced without automatic replay or backend switching.
15. Kitty query responses are terminal-controlled untrusted input and remain bounded.
16. Capability probing uses the existing one-reader multi-family query/router architecture.
17. Terminal brand, `TERM`, operating system, environment variables, and caller preference are not capability proof.
18. Timeout alone is not unsupported truth; the reviewed Primary DA barrier supplies negative Kitty evidence only because the protocol defines the ordering contract.
19. Backend routing is deterministic and evidence-driven: verified Kitty Graphics is preferred; verified Sixel remains fallback.
20. Correlation establishes bounded ownership of a Kitty reply but never bypasses grammar or resource validation.
21. Existing stable 1.0–1.7 public API, wire, ownership, cancellation, restoration, security, and package contracts remain compatible.

## Tranche plan

```text
A180  APC construction contract and reference freeze           complete
A181  Kitty Graphics control-data and response grammar         complete
A182  backend-neutral raster-to-Kitty raw adaptation           complete
A183  direct Base64 chunk encoder                              complete
A184  committed multi-frame APC graphics transaction           complete
A185  Kitty Graphics live capability probe and correlation     complete
A186  multi-backend raster routing and fallback                complete
A187  raster semantic parity, alpha, geometry, and cursor      complete
A188  APC/Kitty hardening, fragmentation, and resource closure complete
A189  package, documentation, compatibility, and release closure complete
```

## Accepted checkpoints

| Tranche | Exact head | Staging workflow |
| --- | --- | --- |
| A180 | `88ac1db0a2904393622082423b8773bd8b19f121` | `34417006958` |
| A181 | `6b5abbdda8ac3ef57bf4c99054a65946e0f13b3a` | `34417741619` |
| A182 | `d9a02336aa2fd61b294064f526ade6021d9d35d2` | `34418298756` |
| A183 | `6f160610bf2df3c666e7d3350e7051475aceca36` | `34418863405` |
| A184 | `d3a7a9262263f28f7ae2d4511520f1b556e5539e` | `34420648720` |
| A185 | `c6a79421b8e8eb3c0c333493f5925f44a2931152` | `34422948067` |
| A186 | `21fb617965f41270e9f3cd43f405fa6ec6e87b2e` | `34426364013` |
| A187 | `80d78b74fe780421bf2e667ef434d38775c0dd48` | `34427249773` |
| A188 | `997feb9628d34389199ca3fffdf90e829f33819a` | `34469956370` |
| A189 qualification | `0de1a1c6b95b2b36407a94935b3126c8bfd6ca5a` | `34476585310` |

Every accepted checkpoint passed Windows, Linux, and macOS runtime/source validation, package candidate/public-API freeze, all four package-contract shards, and the validated package artifact. The public API fingerprint remained the 1.7 value throughout 1.8 implementation.

## A180 — APC construction contract and reference freeze

**Status:** Complete.

A180 establishes the canonical internal APC family writer while keeping Kitty-specific syntax out of the family layer.

Completed contract:

- internal `ApcWriter`;
- canonical seven-bit `ESC _` introducer and `ESC \\` terminator;
- opaque application payload at the family layer;
- rejection of framing-altering CAN, SUB, ESC, and C1 ST payload bytes;
- bounded small complete frames;
- normalized APC round-trip through `TerminalControlFrameStructure`;
- no public API and no Kitty grammar in the family writer.

Permanent contract: `docs/A180-APC-Construction-Contract-and-Reference-Freeze.md`.

## A181 — Kitty Graphics control-data and response grammar

**Status:** Complete.

A181 defines the reviewed Kitty dialect above generic APC. It adds deterministic typed control-data construction and strict response parsing for the common display/probe subset, with bounded numeric and field handling, duplicate/unknown-field policy, and non-zero image-id correlation.

Permanent contract: `docs/A181-Kitty-Graphics-Control-Data-and-Response-Grammar.md`.

## A182 — backend-neutral raster-to-Kitty raw adaptation

**Status:** Complete.

The frozen mapping is:

```text
Rgb24    -> f=24, byte-exact RGB payload
Rgba32   -> f=32, byte-exact RGBA payload
Indexed8 -> RGB24 when referenced palette colors are opaque,
            otherwise RGBA32 preserving palette alpha
```

The adapter preserves byte values and straight alpha, performs checked bounded conversion, introduces no image-decoder dependency, and performs no hidden compositing/gamma conversion.

Permanent contract: `docs/A182-Backend-Neutral-Raster-to-Kitty-Raw-Adaptation.md`.

## A183 — direct Base64 chunk encoder

**Status:** Complete.

A183 lazily Base64-encodes direct Kitty raster bytes into deterministic application payloads whose encoded image-data portion never exceeds 4096 bytes. Large rasters do not require a complete Base64 allocation. First and continuation metadata follow the reviewed Kitty direct-transfer contract.

Permanent contract: `docs/A183-Direct-Kitty-Base64-Chunk-Encoder.md`.

## A184 — committed multi-frame APC graphics transaction

**Status:** Complete.

A184 serializes one logical Kitty image transfer across all of its separately terminated APC chunks. It honors caller cancellation before commitment, ignores ordinary caller cancellation after the first committed frame so the transfer is not intentionally truncated, surfaces transport failure without replay, and holds the session output gate through final flush and teardown drainage.

Permanent contract: `docs/A184-Committed-Multi-Frame-APC-Graphics-Transaction.md`.

## A185 — Kitty Graphics live capability probe and response correlation

**Status:** Complete.

A185 integrates the protocol-defined Kitty query + Primary DA barrier with the existing authoritative input/query and capability-evidence architecture.

Evidence rules:

- correlated Kitty response -> `Verified / ProtocolResponse`;
- Primary DA barrier first -> reviewed `Unsupported / ProtocolResponse` for `ApcKittyGraphics`;
- timeout without authoritative barrier/response -> `Unknown`, not unsupported;
- caller cancellation -> no fabricated negative evidence;
- no competing input reader.

Permanent contract: `docs/A185-Kitty-Graphics-Live-Capability-Probe-and-Correlation.md`.

## A186 — multi-backend raster routing and fallback

**Status:** Complete.

`DisplayRasterAsync(...)` now selects internally between:

```text
RasterGraphics
    -> verified ApcKittyGraphics
    -> verified DcsSixel
```

Verified Kitty is preferred; verified Sixel remains fallback. Unresolved evidence is probed in deterministic bounded order. Backend fallback is permitted only before bytes commit; committed transport/protocol failure is never retried through another backend.

Permanent contract: `docs/A186-Multi-Backend-Raster-Routing-and-Fallback.md`.

## A187 — semantic parity, alpha, geometry, and cursor behavior

**Status:** Complete.

A187 freezes the common semantic boundary without pretending that the two protocols have identical feature models:

- source raster dimensions remain intrinsic pixel dimensions;
- fractional alpha is preserved through Kitty and controlled unsupported through Sixel;
- transparent-raster behavior is explicit;
- no public placement/scaling or cursor-normalization option is introduced;
- the existing geometry substrate remains internal;
- persistent Kitty image/placement IDs, source rectangles, z-order, Unicode placeholders, deletion, and animation remain separate concerns.

Permanent contract: `docs/A187-Raster-Semantic-Parity-Alpha-Geometry-and-Cursor.md`.

## A188 — APC/Kitty hardening, fragmentation, and resource closure

**Status:** Complete.

A188 qualifies the live APC/Kitty path under adversarial conditions, including seven/eight-bit fragmentation, CAN/SUB aborts, malformed or missing ST, oversized correlated replies, unrelated control traffic, late responses, subsequent-query integrity, and repeated evidence generations.

Once a complete matching Kitty `i=<probe-id>` field is observed, the reply is transaction-owned even if later framing fails. Ownership remains bounded and does not imply trust. Oversized correlated replies retain the 4096-byte normal response limit and use bounded string resynchronization rather than leaking hostile bytes into ordinary input.

Permanent contract: `docs/A188-APC-Kitty-Hardening-Fragmentation-and-Resource-Closure.md`.

## A189 — package, documentation, compatibility, and release closure

**Status:** Complete — accepted on qualification head `0de1a1c6b95b2b36407a94935b3126c8bfd6ca5a`, workflow #1355 / `34476585310`.

A189 qualifies `1.8.0` as the first multi-backend raster release without adding new graphics behavior.

Closure evidence includes:

- Windows/Linux/macOS Staging runtime/source validation;
- `net8.0`, `net9.0`, and `net10.0` package consistency;
- retained 1.0–1.7 package/API compatibility gates;
- public API fingerprint unchanged from 1.7;
- fresh NuGet-only raster consumer using the same public API while excluding both Sixel/DCS and Kitty/APC public escape hatches;
- retained current `Icod.DCurses` compatibility witness;
- synchronized README, changelog, architecture, security, compatibility, semantic-output documentation, graphics roadmap, release notes, package metadata, both development roadmaps, and PR ledger;
- curated release notes satisfying the Stable 1.x release-line gate.

Permanent closure contract: `docs/A189-1.8.0-Package-Documentation-Compatibility-and-Release-Closure.md`.

Only status/evidence closure edits follow the accepted qualification head. The actual final PR head must pass the same complete Staging matrix before PR #46 leaves draft status.

## Explicit 1.8 exclusions

The following remain outside the 1.8 common contract:

- generic public APC writing;
- generic public Kitty Graphics key/value dispatch;
- PNG/JPEG/GIF decoding or transcoding;
- file transmission (`t=f`);
- temporary-file transmission (`t=t`);
- shared-memory transmission (`t=s`);
- public persistent image identifiers or placement ownership;
- arbitrary source rectangles;
- z-index/layering;
- Unicode placeholder placements;
- animation/deletion/scene management;
- public backend selection;
- terminal-brand automatic activation.

## Relationship to later work

```text
1.5.0  normalized control families / evidence / routing
    |
1.6.0  complete CSI grammar / geometry
    |
1.7.0  DCS / Sixel / public common raster operation
    |
1.8.0  APC / Kitty Graphics / multi-backend raster routing
    |
future   advanced graphics placement/lifecycle only after separate review
```

## Release rule

A189 qualification is complete, but qualification is not publication authorization.

The status/evidence-only final PR head must pass the complete Staging workflow before PR #46 is marked ready for review. Merge remains explicit. After merge, the resulting `main` head must pass Release distribution validation. Tagging and publishing `v1.8.0` remain separate explicit actions after post-merge validation succeeds.