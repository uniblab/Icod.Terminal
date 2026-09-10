# A186 — Multi-Backend Raster Routing and Fallback

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A186  
**Status:** complete — accepted on exact head `21fb617965f41270e9f3cd43f405fa6ec6e87b2e`, Staging workflow #1336 / `34426364013`

## Purpose

A186 connects the public backend-neutral raster operation introduced in 1.7 to both reviewed graphics backends available in 1.8:

```text
TerminalSession.DisplayRasterAsync(...)
    -> verified ApcKittyGraphics
    -> verified DcsSixel
```

No new caller-facing raster API is introduced. Backend identity, capability probing, raw pixel adaptation, and wire selection remain internal.

## Routing order

The semantic resolver already gives `ApcKittyGraphics` higher preference than `DcsSixel` when both have verified evidence.

A186 therefore applies these rules:

1. resolve current `RasterGraphics` evidence;
2. if either reviewed raster backend is already verified, use that resolution immediately;
3. if neither backend is verified, probe Kitty Graphics first unless Kitty already has authoritative `Unsupported` evidence;
4. re-resolve after the Kitty probe;
5. if Kitty is now verified, select Kitty;
6. otherwise use Sixel immediately if the Kitty probe's Primary DA barrier verified Sixel;
7. if Kitty is known unsupported and Sixel still has no current protocol-response evidence, perform the bounded Sixel Primary DA probe;
8. if no backend becomes verified, return controlled `Unavailable`.

The caller never selects a backend directly.

## Reuse of A185 barrier evidence

The A185 Kitty support probe ends with Primary DA. That barrier is also passed through the D177 Sixel evidence path.

Therefore a DA-first Kitty-negative result can establish Sixel fallback without a duplicate query:

```text
Primary DA first + attribute 4
    Kitty = Unsupported / ProtocolResponse
    Sixel = Verified / ProtocolResponse
    -> display through Sixel
```

A valid DA-first response without attribute `4` leaves Sixel `Unknown / ProtocolResponse`. A186 does not immediately send an identical second Primary DA query merely to rediscover the same current evidence; the operation returns `Unavailable` unless later evidence changes.

If Kitty already has authoritative `Unsupported` evidence before the display operation and Sixel has no current protocol-response evidence, A186 may issue the ordinary bounded Sixel probe directly.

## Existing verified evidence

A186 does not gratuitously reprobe a lower-priority backend.

In particular:

- verified Kitty is used immediately;
- verified Sixel is used immediately when Kitty is not already verified;
- an unknown higher-preference Kitty backend does not force a new probe when Sixel is already verified.

This preserves the evidence model's meaning: a verified usable backend is enough to perform the semantic operation.

## Kitty display path

When `ApcKittyGraphics` is selected:

1. A182 adapts the immutable `TerminalRasterImage` to raw RGB24/RGBA32;
2. A183 lazily Base64-chunks the direct transfer using silent `q=2` frames;
3. A184 writes the logical image as one committed session-output transaction.

RGB24 and RGBA32 preserve their owned bytes. Indexed8 expands deterministically. Fractional alpha is preserved through RGBA32.

## Sixel display path

When `DcsSixel` is selected, the 1.7 path remains intact:

1. D174 quantizes/adapts to the bounded Sixel palette model;
2. D175 creates deterministic Sixel payload segments;
3. D176 writes one committed DCS transaction.

Sixel's existing fractional-alpha limitation remains truthful: alpha values `1..254` return controlled `Unsupported` rather than being silently flattened.

## Fallback boundary

Backend fallback is strictly pre-commit.

Once a selected backend begins its graphics transaction, A186 does not catch a transport/protocol failure and retry the semantic operation through the other backend.

For example, when Kitty is selected and its first committed APC-frame write fails, that exception is surfaced. A186 does not attempt Sixel even if Sixel is also verified, because the terminal-side state after the failed write is not knowable safely.

The same principle applies to committed Sixel output.

## Quiet Kitty display traffic

A183's accepted `q=2` direct-display policy remains unchanged.

The Kitty implementation treats `q=1` as suppressing successful acknowledgements and `q=2` as fully silent, suppressing both successful and error responses. This is consistent with Kitty's own use of `q=2` when host applications must not receive graphics responses.

Capability verification remains the explicit A185 query path; ordinary semantic display does not inject unsolicited Kitty replies into the input stream.

## Public compatibility

A186 changes implementation routing beneath the existing method only:

```csharp
public ValueTask<TerminalControlMutationResult> DisplayRasterAsync(
    TerminalRasterImage image,
    CancellationToken cancellationToken = default
)
```

The public signature and 1.7 raster types remain unchanged. The 1.7 machine-readable public API fingerprint therefore remains the expected baseline.

## Regression coverage

A186 tests prove:

- verified Kitty wins when both backends are verified;
- verified Sixel is used without probing unknown Kitty;
- unknown backends probe Kitty first;
- a correlated Kitty response routes display through Kitty;
- DA-first attribute `4` routes through Sixel without a second DA query;
- DA-first without Sixel evidence returns `Unavailable` without duplicate probing;
- previously known Kitty `Unsupported` evidence allows a direct Sixel probe;
- fractional RGBA alpha is preserved through Kitty;
- committed Kitty transport failure is surfaced with no Sixel retry;
- existing Sixel output remains a segmented D175/D176 transaction and is validated as one concatenated logical DCS frame;
- the public API fingerprint remains unchanged.

## Acceptance

A186 is accepted on exact head:

```text
21fb617965f41270e9f3cd43f405fa6ec6e87b2e
```

Staging workflow:

```text
#1336 / 34426364013
```

The exact head passed Runtime Windows, Runtime Linux, Runtime macOS, package candidate/public-API freeze, Package Foundation, Package Presentation, Package Semantic and hardening, Package Stable 1.x release line, and the validated package artifact.

A186 acceptance does not authorize merge or publication. PR #46 remains draft while A187–A189 are developed.
