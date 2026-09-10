# A184 — Committed Multi-Frame APC Graphics Transaction

**Release:** `Icod.Terminal 1.8.0`  
**Tranche:** A184  
**Status:** implemented / validating

## Purpose

A184 connects the bounded A183 direct Kitty Graphics payload stream to the existing `TerminalSession` output-serialization boundary.

Unlike Sixel, one logical Kitty direct transfer may consist of several individually terminated APC frames. A184 therefore treats the complete sequence as one committed session transaction even though each protocol chunk is independently framed.

A184 does not probe capability, select between Kitty and Sixel, or change the public raster API. Those remain A185–A187 concerns.

## Transaction shape

For a logical image whose A183 encoder emits N application payloads, A184 writes:

```text
APC <first Kitty payload> ST
APC <continuation payload> ST
...
APC <final Kitty payload> ST
flush
```

Each APC frame is constructed by the A180 `ApcWriter`, so every chunk is a complete canonical seven-bit string:

```text
ESC _ <payload> ESC \
```

A184 never concatenates multiple Kitty payloads inside one APC frame and never omits the ST between direct-transfer chunks.

## Pre-commit validation

Before acquiring the session output gate, A184:

1. validates the session and raster arguments;
2. observes pre-existing caller cancellation;
3. creates the A183 payload enumerator;
4. materializes the first bounded payload;
5. rejects an impossible empty payload;
6. wraps the first payload through A180 and obtains one complete APC frame.

This moves all ordinary first-frame grammar/framing validation before commitment without allocating the complete logical transfer.

Subsequent A183 payloads remain lazy so large images do not require one complete Base64 or APC-frame allocation.

## Commit point

The logical Kitty transfer commits when the first complete APC frame is passed to `ITerminalOutput.WriteAsync(...)` after the session output gate has been acquired.

Caller cancellation is honored:

- before A184 begins;
- while waiting to acquire the output gate;
- immediately after acquiring the gate and before the first frame write.

The actual committed write uses `CancellationToken.None`. From the first committed frame onward, ordinary caller cancellation is no longer allowed to strand an incomplete multi-chunk transfer.

## Post-commit behavior

After commitment A184:

- retains the same session output lease;
- lazily obtains each remaining A183 payload;
- wraps each payload as one complete A180 APC frame;
- writes every frame with `CancellationToken.None`;
- performs exactly one final flush with `CancellationToken.None` after the final frame.

The caller's cancellation token is deliberately not re-observed after commitment.

This mirrors the 1.7 Sixel integrity rule at the logical-transfer level: cancellation may prevent a transfer from starting, but ordinary cancellation does not intentionally leave a committed protocol object incomplete.

## Serialization and teardown

A184 uses the existing `TerminalSession.AcquireSessionOutputAsync(...)` gate. It does not create a Kitty-specific lock.

Therefore:

- ordinary session output cannot interleave between Kitty chunks;
- another graphics transaction cannot interleave between Kitty chunks;
- the same teardown drainage introduced for committed Sixel output also waits for a committed Kitty transfer before output-state restoration continues.

The output lease is held through the final flush.

## Transport failure

A transport exception is surfaced immediately.

After any frame has committed, A184 does **not**:

- retry the failed frame;
- restart the image transfer;
- append speculative continuation/final chunks;
- switch to Sixel;
- attempt to infer what terminal-side state was applied.

A later semantic layer may consider another backend only before any bytes of the logical operation have committed.

## Resource behavior

A184 preserves the A183 streaming bounds:

- no complete Base64 image allocation;
- no complete multi-frame transfer allocation;
- one bounded A183 application payload at a time;
- one bounded A180 APC frame at a time after the prebuilt first frame.

The largest ordinary frame remains well below the A180 8192-byte complete-frame ceiling because the Base64 section is at most 4096 bytes.

## Response behavior

Ordinary A184 display chunks use the A183 `q=2` policy and therefore do not require acknowledgement correlation. A184 itself registers no query expectation and creates no input reader.

Capability verification and explicit Kitty response correlation belong to A185.

## Public API boundary

A184 introduces no public API.

The public caller contract remains:

```text
TerminalRasterImage
TerminalSession.DisplayRasterAsync(...)
```

A184 is an internal backend transport transaction.

## Tests

A184 regression coverage must prove:

- pre-cancelled transfer writes and flushes nothing;
- cancellation while waiting for the output gate writes and flushes nothing;
- cancellation immediately after the first successful frame write does not prevent remaining chunks or final flush;
- every direct-transfer chunk is one structurally complete APC frame with seven-bit ST;
- unrelated session output cannot interleave between committed Kitty frames;
- failure on a later frame is surfaced without retry or speculative extra writes;
- no success flush occurs after a failed frame write;
- a one-chunk transfer is byte-exact with direct A183 + A180 composition;
- session disposal waits for a paused committed Kitty transfer before restoration flush proceeds;
- the 1.7 public API fingerprint remains unchanged.

## Acceptance rule

A184 is accepted only on one exact PR head passing Runtime Windows, Runtime Linux, Runtime macOS, package candidate/public-API freeze, Package Foundation, Package Presentation, Package Semantic and hardening, Package Stable 1.x release line, and the validated package artifact.

A green A184 checkpoint does not authorize merge or publication. PR #46 remains draft while A185–A189 are developed.
