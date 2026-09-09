# Icod.Terminal 1.8.0 Development Roadmap

**Release:** `1.8.0`  
**Theme:** APC foundation, Kitty Graphics, and verified multi-backend raster routing  
**Status:** A180 starting  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.7.0`

## Why this release exists

`1.5.0` normalized control-family framing, query transactions, capability evidence, and semantic backend routing. `1.6.0` completed CSI and added internal pixel geometry. `1.7.0` then completed DCS/Sixel and introduced the first public backend-neutral raster contract:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

Version `1.8.0` adds the second raster backend without changing that caller-facing image model. Kitty Graphics is an APC dialect and must therefore fit the same layering discipline established for Sixel:

```text
semantic raster intent
    -> capability/evidence resolution
        -> backend selection
            -> Kitty Graphics dialect / APC
            -> Sixel dialect / DCS
```

The primary design requirement is that callers continue to ask to display a raster, not to speak Kitty protocol.

## Protocol reference

The reference protocol is the current Kitty terminal graphics protocol:

`https://sw.kovidgoyal.net/kitty/graphics-protocol/`

The reviewed direct-transfer framing is:

```text
ESC _ G <control-data> ; <base64-payload> ESC \
```

where control data is a comma-separated Kitty-defined `key=value` grammar. Raw RGB24 (`f=24`) and RGBA32 (`f=32`) data are first-class transmission formats. Direct transport (`t=d`) is the initial 1.8 medium. Large direct transfers are split into Base64 payload chunks no larger than 4,096 bytes and use `m=1` for continuation and `m=0` for the final chunk.

The protocol's recommended support test combines a Kitty Graphics query action (`a=q`) with a following Primary DA request as a synchronization barrier. An APC query reply proves protocol handling; a Primary DA reply arriving first without the Kitty response is reviewed negative evidence for this specific backend.

## Release invariants

1. One live `TerminalSession` remains the authoritative input reader.
2. APC framing remains a generic control-family concern; Kitty `G...` syntax belongs to the Kitty Graphics dialect layer.
3. Canonical library-generated APC uses seven-bit `ESC _ ... ESC \\` framing.
4. No generic raw public APC or Kitty Graphics writer is introduced.
5. The public 1.7 raster model remains the semantic caller contract unless a demonstrated backend-neutral need requires an additive 1.8 extension.
6. Existing Sixel output remains byte-stable and available as the fallback raster backend when verified.
7. Kitty Graphics direct transfer is implemented before file, temporary-file, or shared-memory media.
8. Filesystem, temporary-file, and shared-memory transmission remain excluded from 1.8 unless separately security-reviewed; no hidden filesystem or IPC side effects are introduced merely for performance.
9. Initial Kitty raster transmission uses raw RGB24/RGBA32 data; PNG/JPEG/GIF decoding remains outside `Icod.Terminal`.
10. Indexed8 input is adapted internally to a Kitty-supported raw format without changing the public raster contract.
11. RGBA fractional alpha is preserved by Kitty Graphics rather than rejected or flattened.
12. Direct-transfer payload chunking is deterministic and bounded; Base64 chunks do not exceed 4,096 bytes.
13. One logical Kitty image transfer cannot interleave with unrelated session output or another graphics transfer.
14. Caller cancellation is honored before the first committed APC frame. After transfer commitment, cancellation policy must preserve protocol integrity and must not strand an incomplete multi-chunk image silently.
15. Kitty query/acknowledgement responses are terminal-controlled untrusted input and remain bounded.
16. Capability probing uses the existing multi-family query/router architecture and never creates a second reader.
17. Terminal brand, `TERM`, operating system, environment variables, and caller preference are not capability proof.
18. Timeout alone is not unsupported truth. A reviewed CSI barrier can provide negative Kitty evidence only because the protocol explicitly defines that ordering contract.
19. Backend routing is deterministic and evidence-driven. Verified Kitty Graphics is preferred for the common raster operation; verified Sixel remains a valid fallback.
20. Existing stable 1.0–1.7 public API, wire, ownership, cancellation, restoration, security, and package contracts remain compatible.

## Tranche plan

```text
A180  APC construction contract and reference freeze
A181  Kitty Graphics control-data and response grammar
A182  backend-neutral raster-to-Kitty raw adaptation
A183  direct Base64 chunk encoder
A184  committed multi-frame APC graphics transaction
A185  Kitty Graphics live capability probe and response correlation
A186  multi-backend raster routing and fallback
A187  raster semantic parity, alpha, geometry, and cursor behavior
A188  APC/Kitty hardening, fragmentation, and resource closure
A189  package, documentation, compatibility, and release closure
```

## A180 — APC construction contract and reference freeze

**Status:** Starting.

**Goal:** establish one canonical internal APC construction primitive while keeping Kitty-specific syntax out of the family layer.

### Required contract

A canonical small APC frame is:

```text
ESC _ <application-defined payload> ESC \
```

A180 should:

- add one internal `ApcWriter` (name subject to implementation review);
- emit seven-bit `ESC _` and seven-bit ST exactly;
- treat the application payload as opaque at the family layer;
- reject bytes which would abort or prematurely terminate APC framing (`CAN`, `SUB`, `ESC`, and C1 ST);
- impose an explicit small-frame bound large enough for later reviewed Kitty direct-transfer control/payload framing while remaining independent of Kitty semantics;
- round-trip canonical frames through the existing normalized APC `TerminalControlFrameStructure` parser;
- preserve existing inbound APC 7/8-bit recognition and resource bounds;
- add no public API and no Kitty `key=value` parser yet.

### Acceptance

A180 is accepted only on one exact PR head passing Windows/Linux/macOS Staging runtime validation, package candidate/API baseline, all package shards, and validated artifact.

## A181 — Kitty Graphics control-data and response grammar

**Goal:** define the Kitty dialect above generic APC.

The codec/parser should recognize only the reviewed subset needed by 1.8 common raster display and capability probing.

Initial command keys include the subset required for:

```text
a   action
f   pixel format
s   source width
v   source height
t   transmission medium
m   continuation flag
q   response quietness
I/i response/query identity where needed
```

The exact identity key strategy must be frozen after reviewing protocol correlation requirements and collision behavior.

Requirements:

- ASCII-only control grammar;
- deterministic key ordering for generated commands;
- duplicate/unknown-key policy explicitly defined;
- bounded key/value/control-data lengths and numeric magnitude;
- strict response grammar for `OK` and protocol error payloads;
- no generic arbitrary public Kitty metadata dictionary;
- no placement/deletion/animation controls in the common raster path.

## A182 — backend-neutral raster-to-Kitty raw adaptation

**Goal:** map the existing public `TerminalRasterImage` into Kitty's supported raw formats without lossy policy surprises.

Expected mapping:

```text
Rgb24    -> f=24, byte-exact RGB payload
Rgba32   -> f=32, byte-exact RGBA payload
Indexed8 -> expand deterministically to RGB24 when fully opaque,
            otherwise RGBA32 preserving palette alpha
```

Requirements:

- preserve sRGB byte values; no hidden gamma conversion;
- preserve straight RGBA alpha, including values `1..254`;
- no hidden matte/background compositing;
- no PNG conversion dependency;
- checked arithmetic and retained D173 public raster limits;
- bounded conversion storage;
- deterministic output for identical raster input.

## A183 — direct Base64 chunk encoder

**Goal:** encode direct (`t=d`) Kitty payloads into protocol-sized APC chunks.

Requirements:

- Base64 encode raw raster bytes;
- encoded payload of every APC chunk <= 4,096 bytes;
- every non-final Base64 chunk length is a multiple of four;
- first chunk carries complete required control metadata;
- continuation chunks carry only protocol-required continuation/quietness metadata;
- `m=1` for all non-final chunks and `m=0` for final chunk;
- no giant complete Base64 allocation for large rasters;
- bounded reusable source/encoded work buffers;
- hand-verifiable one-pixel and small multi-chunk golden vectors.

## A184 — committed multi-frame APC graphics transaction

**Goal:** write a complete direct Kitty image transfer safely through the session's existing output serialization model.

Unlike Sixel, one logical transfer can consist of several separately terminated APC frames. The transaction must therefore freeze a transfer-level commit contract.

Requirements:

- acquire the existing session output gate before the first APC frame;
- honor caller cancellation before commitment;
- once the first chunk commits, prevent unrelated output from interleaving until the final chunk and flush;
- define post-commit cancellation so an image is not silently left in an incomplete protocol transfer;
- surface transport failure without automatic replay that could duplicate or replace terminal state unpredictably;
- no complete encoded-image allocation;
- teardown waits for a committed transfer before restoration proceeds.

## A185 — Kitty Graphics live capability probe and response correlation

**Goal:** integrate the protocol-defined APC+CSI support probe with the normalized query/evidence architecture.

The intended probe is equivalent to the protocol's one-pixel query followed by Primary DA:

```text
APC Kitty query action
CSI Primary DA request
```

Evidence rules to freeze:

- correlated Kitty `OK` response -> `ApcKittyGraphics / Verified / ProtocolResponse`;
- correlated Kitty protocol response, including a recognized protocol error that proves dialect handling -> support semantics determined explicitly by error class;
- Primary DA barrier before any Kitty response -> reviewed `Unsupported / ProtocolResponse` for `ApcKittyGraphics`;
- timeout before an authoritative barrier/response -> `Unknown`, not unsupported;
- caller cancellation -> no fabricated negative evidence;
- responses arriving after query completion remain subject to the existing late-response ownership rules;
- no competing input reader.

## A186 — multi-backend raster routing and fallback

**Goal:** make the existing `DisplayRasterAsync(...)` semantic operation actually select between the two raster backends.

Preferred routing:

```text
RasterGraphics
    -> verified ApcKittyGraphics
    -> verified DcsSixel
```

Requirements:

- verified Kitty is preferred when both backends are verified;
- verified Sixel remains usable when Kitty is unsupported or unverified;
- if neither is verified, bounded probing occurs in a deterministic order;
- backend selection remains internal;
- no silent conversion of transport/protocol failures into a retry on a different backend after bytes commit;
- pre-commit backend fallback is allowed only where semantics remain truthful;
- existing Sixel behavior remains stable.

## A187 — semantic parity, alpha, geometry, and cursor behavior

**Goal:** ensure the shared public raster operation has coherent semantics across Sixel and Kitty rather than merely selecting whichever encoder exists.

Review and qualify:

- fractional alpha: supported through Kitty, controlled unsupported through Sixel;
- all-transparent raster behavior;
- cursor movement after image display;
- interaction with the current cell/pixel geometry substrate;
- clipping/truncation expectations;
- whether any placement/scaling option is genuinely backend-neutral enough for an additive public API.

Default preference is **no new public option** unless cross-backend behavior can be defined precisely and tested on both implementations.

Persistent image IDs, explicit placement IDs, source rectangles, z-order, Unicode placeholders, deletion, and animation remain separate Kitty-specific concerns.

## A188 — APC/Kitty hardening, fragmentation, and resource closure

**Goal:** qualify the new family/dialect under adversarial and boundary conditions.

Coverage includes:

- APC 7-bit and accepted 8-bit input framing;
- every meaningful response split point;
- malformed/missing ST;
- CAN/SUB interruption;
- oversized APC responses;
- invalid Base64/control-data responses;
- duplicate/overflow numeric keys;
- unrelated APC/CSI/OSC/DCS traffic during active probes;
- late Kitty responses after barrier/timeout;
- maximum public raster dimensions/pixels/storage;
- direct chunk boundaries at 4096 and surrounding values;
- deterministic segmentation;
- output cancellation/failure/teardown races;
- repeated capability invalidation/resume generations.

## A189 — package, documentation, compatibility, and release closure

**Goal:** qualify `1.8.0` as the first multi-backend raster release.

Required evidence:

- Windows/Linux/macOS Staging runtime/source validation;
- post-merge Release distribution validation;
- `net8.0`, `net9.0`, and `net10.0` consistency;
- retained 1.0–1.7 package/API compatibility gates;
- public API fingerprint unchanged from 1.7 unless A187 proves an intentional additive need;
- fresh NuGet-only raster consumer exercising the same public API against the new backend-routing implementation;
- retained current `Icod.DCurses` compatibility witness;
- synchronized README, changelog, architecture, security, compatibility, release notes, roadmaps, package metadata, and PR ledger;
- exact final PR head green before readiness/merge consideration.

## Explicit 1.8 exclusions

The following are outside the initial 1.8 release contract unless separately approved during the roadmap:

- generic public APC writing;
- generic public Kitty Graphics key/value dispatch;
- PNG/JPEG/GIF decoding or transcoding;
- file transmission (`t=f`);
- temporary-file transmission (`t=t`);
- shared-memory transmission (`t=s`);
- persistent image identifiers as a public ownership model;
- placement IDs and placement lifecycle management;
- arbitrary source rectangles;
- z-index/layering;
- Unicode placeholder placements;
- animation frames;
- generalized graphics scene management;
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

A green development PR is necessary but not sufficient to publish `1.8.0`.

The complete A180–A189 program must finish on an unchanged exact PR head that passes Staging. Merge remains explicit. After merge, the resulting `main` head must pass Release distribution validation. Tagging and publishing `v1.8.0` remain separate explicit actions after post-merge validation succeeds.
