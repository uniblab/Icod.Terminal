# C111 — Persistent Kitty Protocol Foundation

- **Release:** `Icod.Terminal 1.11.0`
- **Tranche:** C111
- **Status:** accepted
- **Validated head:** `a37457fe603977d30a2b8727ecacd61376207857`
- **Acceptance workflow:** `#1542 / 34617396678`

## Purpose

C111 establishes the internal typed Kitty Graphics protocol substrate required for persistent raster resources and placements. It intentionally adds no public API.

## Accepted protocol contract

Persistent resource upload is a direct, transmit-only Kitty Graphics operation:

```text
a=t
t=d
I=<private image number>
```

Unlike the existing ephemeral display path, persistent upload requires a terminal acknowledgement and therefore omits quiet mode. The terminal remains free to return the assigned image id together with the private image number.

The established one-pixel canonical upload payload is:

```text
Ga=t,f=24,s=1,v=1,t=d,I=31,m=0;AAAA
```

Multi-frame persistent upload retains the existing 4096-byte encoded data ceiling but uses acknowledgement-preserving continuation payloads:

```text
Gm=1;
Gm=0;
```

Existing ephemeral `DisplayRasterAsync(...)` bytes are unchanged and continue to use the established `a=T` / `q=2` direct-transfer behavior.

## Placement and deletion contract

Persistent placement uses the terminal-assigned image id and a private placement id:

```text
Ga=p,i=<image>,p=<placement>,C=1[,c=<columns>][,r=<rows>]
```

Re-emitting the same image-id/placement-id pair is the primitive used later by C116 for semantic placement update/replacement.

Targeted placement disposal uses the lower-case image delete selector and retains image data:

```text
Ga=d,d=i,i=<image>,p=<placement>
```

Resource cleanup uses the uppercase image selector after child placements have been closed:

```text
Ga=d,d=I,i=<image>
```

## Correlation contract

`KittyGraphicsPersistentResponseMatcher` correlates acknowledgement traffic by the private image number (`I`) rather than by a preselected terminal image id.

A complete acknowledgement is parsed through the existing bounded `KittyGraphicsCodec.ParseResponse(...)` path. Duplicate keys, unsigned-integer overflow, malformed control data, and response-size limits therefore retain the existing validation behavior.

Prefix correlation recognizes only a complete matching `I=<expected>` field in seven-bit or eight-bit APC framing. Incomplete or different image-number fields do not claim traffic.

## TDD evidence

The RED checkpoint intentionally failed the runtime build across all supported TFMs because `KittyGraphicsPersistentEncoder` did not yet exist. The observed failure was `CS0103` from the new C111 tests, with zero warnings.

GREEN introduced:

- `src/Graphics/KittyGraphicsPersistentEncoder.cs`;
- `src/Graphics/KittyGraphicsPersistentResponseMatcher.cs`.

The final unchanged C111 head passed workflow `#1542 / 34617396678`, including Windows, Linux, macOS runtime validation, package candidate/public-API validation, package-contract shards, and the validated package artifact.

## Result

C111 is accepted. C112 may now add the public semantic capability `PersistentRasterGraphics = 9` and integrate it with the 1.10 capability-planning model without changing the now-qualified persistent Kitty wire substrate.
