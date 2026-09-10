# A185 — Kitty Graphics Live Capability Probe and Correlation

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A185  
**Status:** complete — accepted on exact head `c6a79421b8e8eb3c0c333493f5925f44a2931152`, Staging workflow #1331 / `34422948067`

## Purpose

A185 integrates the Kitty Graphics protocol-defined support test with the existing one-reader terminal-query and semantic-capability evidence architecture.

The probe sends the documented one-pixel Kitty Graphics query immediately followed by Primary Device Attributes:

```text
ESC _ G i=<id>,s=1,v=1,a=q,t=d,f=24;AAAA ESC \
ESC [ c
```

The Kitty query uses a non-zero correlation image id. Primary DA is the synchronization barrier.

## Why Primary DA remains the transaction completion frame

A correlated Kitty APC response cannot simply be treated as the query transaction completion response. The probe has already requested Primary DA, and completing the transaction on the APC response would leave the following DA response unowned on the input stream.

A185 therefore uses the existing decoder-side probe-observer pattern:

- the authoritative `TerminalInputDecoder` observes and consumes only Kitty Graphics APC responses correlated to the active probe id;
- the ordinary query transaction remains registered for Primary DA;
- Primary DA completes the transaction and therefore cannot leak into application input;
- no second reader or graphics-specific input pump is introduced.

The graphics observer runs before ordinary expected-response matching, just as the established Kitty keyboard probe observer does.

## Correlation

A185 correlation is limited to Kitty Graphics APC responses that identify the active non-zero `i=<id>` value.

The observer accepts both canonical seven-bit APC and normalized eight-bit APC framing through the existing bounded response framer. Seven-bit introducers remain correct when fragmented between `ESC` and `_`.

Unrelated APC traffic is not consumed by the graphics probe.

A complete APC frame that clearly claims the active image id but is malformed is owned by the probe and fails deterministically with `FormatException`; it is not leaked as ordinary application input.

## Evidence rules

The protocol-defined ordering contract is frozen as follows.

### Correlated Kitty Graphics response observed

Any syntactically valid correlated response to the documented `a=q` support query proves Kitty Graphics protocol support, whether the status is `OK` or a well-formed Kitty protocol error.

A185 records:

```text
ApcKittyGraphics
    state  = Verified
    source = ProtocolResponse
```

This follows the Kitty Graphics support-detection contract: receipt of a graphics-query response is the support signal.

If the Kitty response is observed before Primary DA and Primary DA subsequently arrives, the positive Kitty evidence is recorded after the DA barrier completes.

If a valid Kitty response is observed but the Primary DA barrier never arrives before the caller-visible timeout, the positive protocol response still wins: A185 records `Verified / ProtocolResponse` and returns success. The existing query transaction retains its normal bounded late-response ownership for a late DA response.

### Primary DA arrives first

If a valid Primary DA response completes the transaction before any correlated Kitty Graphics response was observed, A185 records:

```text
ApcKittyGraphics
    state  = Unsupported
    source = ProtocolResponse
```

This is authoritative negative evidence only because the Kitty specification requires graphics-query responses to be produced before later input such as the DA request is processed.

The same Primary DA response is also passed through the existing D177 evidence path, so attribute `4` can simultaneously verify Sixel for later fallback.

### Timeout with no correlated Kitty response

Silence alone is not unsupported truth.

If the caller-visible probe deadline expires and no correlated Kitty Graphics response was observed, A185 records:

```text
ApcKittyGraphics
    state  = Unknown
    source = LiveProbe
```

### Cancellation

Caller cancellation propagates normally and does not fabricate negative capability evidence.

## Response parsing and bounds

Kitty Graphics responses remain terminal-controlled, untrusted input.

A185 reuses:

- the normalized APC control-family scanner;
- the 4,096-byte ordinary response framing ceiling;
- A181 typed response parsing;
- A181 bounded printable status text;
- existing query timeout and late-response ownership;
- existing input-coordinator serialization and teardown.

No arbitrary metadata dictionary is exposed.

## Probe identity

Production probe ids are generated from a monotonic process-local counter and skip zero. Tests can call the internal overload with a fixed non-zero id for byte-exact vectors.

Probe ids are correlation tokens for this bounded query only; A185 does not expose or persist them as public Kitty image identifiers.

## Sixel interaction

A185 deliberately lets the Primary DA barrier update D177 Sixel evidence.

Therefore one Kitty capability probe can produce either of these useful results:

```text
Kitty response before DA -> Kitty Verified
DA first, attribute 4     -> Kitty Unsupported + Sixel Verified
DA first, no attribute 4  -> Kitty Unsupported + Sixel Unknown
```

This is the evidence substrate used by A186 multi-backend raster routing.

## Public API boundary

A185 introduces no public API.

The caller-facing raster contract remains:

```text
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

Capability probing and backend identity remain internal.

## Regression coverage

A185 tests cover:

- byte-exact compound probe construction;
- correlated seven-bit Kitty `OK` followed by DA;
- correlated Kitty error followed by DA;
- Primary DA arriving first as authoritative Kitty negative evidence;
- DA-first Sixel attribute `4` producing Sixel fallback evidence;
- timeout with no Kitty/DA response remaining unknown;
- Kitty response observed with missing DA remaining positive at timeout;
- malformed correlated Kitty response failing deterministically;
- pre-cancellation producing no fabricated evidence;
- live-generation expiry;
- unchanged public API fingerprint.

## Acceptance

A185 is accepted on exact head:

```text
c6a79421b8e8eb3c0c333493f5925f44a2931152
```

Staging workflow:

```text
#1331 / 34422948067
```

The exact head passed Runtime Windows, Runtime Linux, Runtime macOS, package candidate/public-API freeze, Package Foundation, Package Presentation, Package Semantic and hardening, Package Stable 1.x release line, and the validated package artifact.

A185 acceptance does not authorize merge or publication. PR #46 remains draft while A186–A189 are developed.
