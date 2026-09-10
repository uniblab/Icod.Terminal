# A188 — APC/Kitty Hardening, Fragmentation, and Resource Closure

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A188  
**Status:** complete — accepted on exact head `997feb9628d34389199ca3fffdf90e829f33819a`, Staging workflow #1348 / `34469956370`

## Purpose

A188 qualifies the APC/Kitty Graphics path under hostile framing, fragmentation, correlation, timeout, resource, and lifecycle conditions without expanding the public raster API.

The tranche is deliberately a hardening closure rather than a feature tranche. A180–A187 already define the family writer, Kitty dialect, raw raster adaptation, Base64 chunking, committed output transaction, live capability probe, multi-backend routing, and cross-backend semantic boundary. A188 attempts to break those contracts at their edges and changes production behavior only where a test exposes a real ownership or recovery weakness.

## Accepted checkpoint

The accepted implementation head is:

```text
997feb9628d34389199ca3fffdf90e829f33819a
```

Staging workflow:

```text
#1348
34469956370
```

The exact head passed:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- package candidate and unchanged public-API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- validated package artifact.

The 1.7 public API fingerprint remains unchanged.

## Correlated-prefix ownership

A185 originally correlated a Kitty Graphics response after a complete APC frame had been parsed. A188 strengthens that boundary for hostile responses.

Once the active decoder has observed a complete Kitty Graphics `i=<probe-image-id>` control-data field in a syntactically recognizable APC prefix, the response is transaction-owned by that probe even if the remainder later becomes malformed, aborted, oversized, or unterminated.

The correlation helper accepts both reviewed APC introducer forms on input:

```text
ESC _ G ...
C1 APC G ...
```

A prefix is not considered correlated merely because it ends with the digits of the expected identifier. The complete `i=<value>` field must be closed by `,` or `;`, the numeric value must parse without overflow, and it must equal the active non-zero image id.

This prevents partial or adversarial identifier prefixes from stealing unrelated application traffic while preventing an already-correlated response from leaking back into the ordinary input path after later structural failure.

## Seven-bit and eight-bit fragmentation

The hardening suite exercises representative successful Kitty responses at every read split point for both accepted input forms:

```text
ESC _ ... ESC \
C1 APC ... C1 ST
```

The authoritative `TerminalSession` input coordinator remains the only reader. Fragmentation does not create a graphics-specific transport reader or a second response pump.

Canonical library output remains seven-bit. Eight-bit APC/ST support in this tranche is input compatibility only.

## CAN, SUB, and malformed termination

A correlated Kitty APC interrupted by `CAN` or `SUB` is owned through the interruption point and fails the active probe with `FormatException`.

A correlated APC whose escape terminator is structurally invalid is likewise consumed through the invalid boundary and fails the probe rather than being reinterpreted as ordinary terminal input.

The failure does not fabricate Kitty support or unsupported evidence.

## Oversized correlated responses

The ordinary terminal-response framing ceiling remains:

```text
4096 bytes
```

A188 does not inflate that bound for Kitty Graphics.

If a response has already correlated to the active Kitty probe and then exceeds the normal 4096-byte frame ceiling, the probe records a framing failure and the decoder uses the existing bounded oversized-response recovery path to drain through the terminal string terminator before returning to ordinary response routing.

The drain remains bounded by the pre-existing hard response-resynchronization ceiling. The decoder does not retain an unbounded hostile APC in memory and does not leave the following CSI barrier trapped behind the oversized string.

The accepted regression uses a 5000-byte correlated APC delivered in transport chunks smaller than the decoder's fixed read buffer, proving the framing limit itself rather than accidentally testing the scripted transport.

## Unterminated correlated responses

A timeout has different semantics depending on whether the active probe ever obtained correlation evidence.

If no correlated Kitty response prefix was observed before the protocol deadline, timeout remains ordinary uncertainty and may produce `Unknown / LiveProbe` evidence.

If a complete matching `i=<probe-id>` field was observed but the APC never terminated structurally before the deadline, A188 fails the probe with `FormatException` instead of converting the malformed response into false `Unknown` capability state.

This distinction preserves the general rule that silence is not unsupported truth while still treating an identified but structurally incomplete protocol object as malformed input.

## Unrelated control traffic

During an active Kitty support probe, unrelated terminal traffic does not satisfy or corrupt the probe merely because it uses a control family recognized by the normalized parser.

The A188 live suite injects unrelated:

- Kitty APC with a different image id;
- OSC traffic;
- DCS traffic;
- CSI traffic;
- the eventual correlated Kitty response and Primary DA barrier.

Only the matching Kitty response is owned by the side observation. Existing query expectations continue to own their own response families and ordinary input remains distinct.

## Late responses and subsequent queries

A late Kitty APC after a completed Primary DA barrier must not corrupt a later CSI query on the same session.

The hardening suite verifies that a subsequent Primary DA transaction still receives its own CSI response and that the earlier Kitty evidence remains the result established by the completed probe rather than being rewritten by unrelated late traffic.

This builds on the permanent query-manager rule that response ownership is bounded and generation-aware rather than a global parser mode.

## Capability generations

Repeated semantic live-evidence generation advancement expires Kitty protocol-response evidence deterministically.

A188 verifies repeated cycles of:

```text
Verified / ProtocolResponse
    -> advance live evidence generation
    -> Unknown
```

without accumulating stale evidence or allowing an older generation to become authoritative again.

`InvalidateState()` and managed lifecycle invalidation continue to use the same generation model established before 1.8.

## Existing bounds retained

A188 reuses, rather than weakens, the previously accepted graphics/resource ceilings:

```text
public raster maximum dimension       16,384
public raster maximum pixels          16 Mi
public owned raster pixel storage     64 MiB
public indexed palette                256 entries
normal terminal response frame        4,096 bytes
Kitty Base64 payload per APC chunk    4,096 bytes
small complete APC frame              8,192 bytes
```

A181 retains bounded control-data/numeric parsing. A182 retains checked raster adaptation. A183 retains deterministic lazy Base64 segmentation. A184 retains one bounded APC frame at a time. A186/A187 retain the public raster limits and no hidden placement/scaling state.

## Output-side hardening witnesses

A188 does not duplicate every output transaction test in a new file. The accepted release closure relies on the existing witnesses from earlier 1.8 tranches:

- A181 for duplicate, invalid, and overflow Kitty control fields;
- A182 for raw-format and raster conversion bounds;
- A183 for exact 4096-byte Base64 chunk boundaries and deterministic segmentation;
- A184 for pre-commit cancellation, post-commit completion, no interleaving, transport failure, and teardown drainage;
- A186 for deterministic two-backend routing and no post-commit backend retry;
- A187 for fractional-alpha, transparent-raster, intrinsic-dimension, geometry, and cursor semantic boundaries.

The new A188 suite concentrates on the missing live correlated-input ownership layer.

## Test harness discipline

A188 initially exposed two test-harness defects during qualification and corrected them without loosening production contracts.

First, asynchronous session disposal is used directly rather than synchronously blocking on `IAsyncDisposable` cleanup.

Second, hardening awaits carry a short wall-clock harness guard while protocol behavior continues to use the injected monotonic clocks. The guard exists only to make a missed completion name the failing case instead of consuming an entire hosted CI job.

The oversized-response test originally published 512-byte scripted reads even though `TerminalInputDecoder` reads at most 256 bytes at once. That caused the scripted transport to reject the test input before the decoder reached the 4096-byte framing boundary. The accepted test keeps the hostile APC at 5000 bytes but publishes it in 128-byte reads.

No production resource ceiling was relaxed to make the hardening suite pass.

## Public API boundary

A188 introduces no public API.

The caller-facing graphics surface remains exactly the 1.7 contract:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

There is still no generic public APC writer, generic Kitty key/value dispatcher, public backend selector, placement id, persistent image id, z-order control, source rectangle, deletion API, animation API, or cursor-normalization option.

## Security conclusion

A188 closes the principal hostile-input gap identified after A185: an attacker-controlled Kitty APC that had already identified itself as the active probe response could previously lose transaction ownership after later structural failure. The accepted implementation now keeps that response owned and bounded through its malformed/aborted/oversized recovery boundary.

The resulting rule is:

> correlation grants bounded transaction ownership; it does not grant trust.

Correlated content is still parsed strictly, remains size-bounded, and can fail the probe without becoming application input or fabricated capability evidence.

## Acceptance rule

A188 is complete only on the exact accepted head and workflow recorded above. That checkpoint does not authorize merge, release, tag, or publication.

A189 must still synchronize release-facing documentation/package evidence and produce a final unchanged PR head that passes the complete Staging matrix before readiness or merge is considered.
