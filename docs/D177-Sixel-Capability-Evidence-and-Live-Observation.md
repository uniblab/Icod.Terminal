# D177 — Sixel Capability Evidence and Live Observation

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D177  
**Status:** implemented / validating

## Purpose

D177 connects the Sixel backend to the capability/evidence architecture established in 1.5 without treating terminal identity, operating system, caller preference, or registry membership as proof of graphics support.

The backend subject is:

```text
TerminalProtocolBackend.DcsSixel
```

and the semantic operation remains:

```text
TerminalSemanticOperation.RasterGraphics
```

## Primary Device Attributes evidence

For DEC VT220-and-later style Primary Device Attributes capability parameters, attribute `4` advertises Sixel graphics.

D177 therefore freezes this positive rule:

```text
valid Primary DA response containing attribute 4
    -> DcsSixel
    -> Verified
    -> ProtocolResponse
```

This is protocol-response evidence, not terminal-brand inference.

## Conservative negative policy

D177 deliberately does **not** treat absence of Primary DA attribute `4` as authoritative proof that Sixel is unsupported.

A valid Primary DA response without `4` records:

```text
DcsSixel
Unknown
ProtocolResponse
```

The response proves that the query completed and did not advertise Sixel through this evidence channel; it does not prove that every compatible terminal implementation exhaustively reports graphics support through that channel.

Accordingly, D177 does not emit `Unsupported` merely because `4` is absent.

## Timeout policy

The internal Sixel probe uses the existing bounded terminal-query transaction path with a one-second caller-visible timeout.

A timeout records:

```text
DcsSixel
Unknown
LiveProbe
```

and returns `false` from the internal probe helper.

That Boolean means **Sixel was not verified by this probe**. It must not be interpreted as an authoritative unsupported result.

Caller cancellation is not converted into evidence. It propagates normally.

## Existing public Primary DA query

`TerminalSession.QueryPrimaryDeviceAttributesAsync(...)` keeps its existing public signature and return type.

After a correlated response has been successfully parsed, D177 opportunistically records Sixel evidence from the typed result:

- attribute `4` present -> `Verified / ProtocolResponse`;
- attribute `4` absent -> `Unknown / ProtocolResponse`.

Malformed correlated responses still throw `FormatException` before Sixel evidence is changed.

This lets applications that already perform Primary DA discovery improve later semantic routing without a second probe.

## Live generation ownership

Sixel protocol-response/live-probe evidence is stored in the existing `TerminalCapabilityEvidenceLedger` and therefore remains generation-scoped.

When the session advances its semantic live-evidence generation, D177 evidence expires with the other live protocol observations. Static terminal metadata does not preserve a stale verified result across that boundary.

## Static evidence policy

Version 1.7 does not seed `DcsSixel` merely from:

- terminal name;
- `TERM` value;
- operating system;
- known emulator/vendor identity;
- the fact that `DcsSixel` is registered as a raster backend;
- caller preference for Sixel.

No new built-in-profile Sixel assertion is introduced in D177.

No TermInfo metadata is promoted to `DcsSixel` evidence unless an exact, reviewed, semantically relevant Sixel advertisement is available through the normalized profile contract. D177 does not invent such an inference from unrelated color or graphics capabilities.

## Routing consequence

The existing deterministic semantic resolver already prefers verified backend evidence.

Therefore, after positive Primary DA evidence:

```text
RasterGraphics
    -> DcsSixel
    -> Verified
```

can select Sixel even while the future Kitty Graphics backend remains unknown.

When Sixel remains unknown and Kitty Graphics is also unknown, `RasterGraphics` remains unresolved. There is no blind graphics fallback.

## Security and compatibility

D177:

- sends only the existing canonical Primary DA request;
- reuses the existing one-reader query transaction manager;
- adds no raw response parser;
- adds no public protocol API;
- does not make timeout equivalent to unsupported;
- does not make terminal identity equivalent to capability proof;
- does not alter the released Primary DA wire request or typed response contract.

## Acceptance

D177 is complete when:

- Primary DA attribute `4` records `Verified / ProtocolResponse` for `DcsSixel`;
- verified Sixel evidence selects `DcsSixel` for `RasterGraphics`;
- a valid Primary DA response without attribute `4` remains `Unknown` rather than `Unsupported`;
- a Sixel probe timeout remains `Unknown / LiveProbe`;
- caller cancellation is not translated into negative evidence;
- live-generation advance invalidates prior Sixel protocol-response evidence;
- no terminal-brand/OS/registry heuristic is added;
- the existing public Primary DA signature and request bytes remain unchanged;
- the exact D177 head passes the complete Staging/package matrix.
