# D176 — Committed Streaming Graphics Output

**Release:** `Icod.Terminal 1.7.0`  
**Tranche:** D176  
**Status:** complete and accepted  
**Accepted exact head:** `9e48c1cee4e44a0f272378ff4f60b4cabc9bed8a`  
**Accepted Staging workflow:** `34408371837`

## Purpose

D176 turns the bounded D175 Sixel payload segment stream into one serialized terminal transaction without allocating a complete encoded image and without allowing ordinary caller cancellation to truncate an already-committed DCS control string.

The layering is:

```text
TerminalRasterImage
    -> D174 SixelPaletteQuantizer
        -> D175 SixelEncoder payload segments
            -> D176 committed DCS/session-output transaction
```

D176 remains internal. D178 will decide the public semantic raster-display surface after capability evidence and routing have been completed.

## Pre-commit boundary

D176 receives an already-quantized `SixelPaletteImage`.

Before acquiring output ownership it:

- validates the complete palette-image invariant set;
- creates the lazy bounded D175 payload-segment enumerable;
- observes caller cancellation.

It then acquires the existing `TerminalSession` session-output gate with the caller token.

After the gate is acquired, caller cancellation is checked once more immediately before the first transport write.

No terminal byte has committed before that point.

## Commit point

The transaction commits when D176 begins the first transport write containing the canonical Sixel DCS prefix:

```text
ESC P 0;1;0 q
```

The prefix and terminator are derived from the same internal `DcsWriter` framing contract used by the earlier DCS work rather than from a public raw escape-string surface.

Once this first write begins, caller cancellation no longer participates in the transaction.

This avoids the unsafe state:

```text
ESC P ... partial payload
<caller cancellation>
```

where the terminal would remain inside an unterminated DCS string.

## Post-commit cancellation rule

All post-commit writes use a non-caller-cancelable token.

This includes:

- every D175 payload segment;
- the final seven-bit ST;
- the final flush.

Cancellation that arrives after commitment therefore does not truncate the frame and does not change a successful transaction into a canceled one.

The caller can still cancel while waiting for output ownership or before the commit check.

## Output serialization

D176 holds the existing `TerminalSession` output lease continuously across:

```text
DCS prefix
all payload segments
ST
flush
```

Ordinary `WriteTextAsync(...)`, query request emission, presentation transitions, OSC operations, and later semantic output that use the same session-output/control-output semaphore cannot interleave bytes inside the Sixel DCS transaction.

No second graphics-specific semaphore is introduced.

## Streaming and memory

D176 enumerates D175 payload segments one at a time and writes each segment directly to `ITerminalOutput`.

It does not concatenate them into one payload and does not pass ordinary image output through D170's 4,096-byte complete-frame allocation.

The largest ordinary payload write remains bounded by the D175 color-pass segment ceiling, approximately:

```text
width + 4 <= 16,388 bytes
```

plus small fixed DCS framing and palette/raster command writes.

## Terminator

Successful output emits exactly one canonical seven-bit string terminator:

```text
ESC \
```

No C1 ST is emitted by the canonical 1.7 writer.

There is no trailing payload write after ST.

## Flush policy

D176 performs exactly one explicit flush after the final ST succeeds.

It does not flush:

- after the prefix;
- between palette definitions;
- between color passes;
- between six-row bands.

This keeps the transaction serialized while avoiding transport-level flush amplification for large rasters.

## Transport failure

A transport exception after commitment is surfaced unchanged to the caller.

D176 does not:

- retry the failed segment;
- restart the image;
- replay the DCS prefix;
- attempt a speculative ST after an unknown partial write;
- silently report success.

Once an output implementation reports failure, D176 cannot know how many bytes of that write reached the terminal. Additional recovery bytes could therefore make the stream less predictable.

The session output lease is still released by structured cleanup.

A caller must not automatically retry the same image after an in-flight transport failure.

## Encoder failures after commitment

D176 validates all `SixelPaletteImage` dimensions, storage lengths, palette alpha, transparency-mask values, and opaque palette references before output ownership and commitment.

Generated segment bytes therefore do not depend on unchecked caller protocol strings or dangling palette references.

Catastrophic runtime failures such as allocation failure cannot be made recoverable after commitment; as with transport failure, D176 does not retry a partial control string.

## Session disposal ordering

`TerminalSession.DisposeAsync()` first stops accepting new public session output.

D176 hardens presentation/output teardown by draining the shared control-output gate before presentation restoration proceeds. An already-committed Sixel transaction must therefore release its session-output lease before final output-state cleanup and the later restoration flush can run.

Existing cleanup managers remain free to acquire the control-output gate during disposal. The drain uses that same ownership mechanism rather than a graphics-specific second lock.

## Lifecycle interruption

D176 does not bind post-commit transport writes to ordinary caller cancellation or the session's public termination token. Once committed, completing a syntactically valid DCS frame is safer than voluntarily abandoning it because an interrupt was observed.

Session disposal waits for the shared output lease as described above.

A transport implementation can still fail independently; that failure follows the transport-failure rule.

## Security

D176 accepts no raw DCS or Sixel text from callers.

The prefix and ST are fixed internal framing bytes, and all payload bytes come from D175's validated generated segment stream.

The output gate prevents unrelated session-owned control traffic from being injected inside the DCS frame.

## Acceptance evidence

The D176 transaction suite proves:

- a pre-canceled transaction emits no bytes;
- cancellation while waiting for the shared output gate emits no bytes;
- cancellation after the first prefix write still produces the complete frame;
- successful streaming bytes are identical to the equivalent small `DcsWriter` frame;
- ordinary session text cannot interleave inside a paused committed Sixel transaction;
- transport failure is surfaced without retry, flush, or speculative ST;
- disposal waits behind a committed graphics transaction before restoration flush;
- the successful transaction emits one final ST and one transaction flush.

The accepted exact head passed Windows, Linux, macOS, package candidate, all four package shards, and the validated package artifact.

## Deliberately deferred

D176 does not implement:

- Sixel capability discovery;
- backend selection;
- terminal geometry qualification;
- public raw raster factories;
- public `DisplayRasterAsync(...)`;
- Kitty Graphics;
- retry/resume of partially transmitted images.

Those belong to D177, D178, and later releases.

## Acceptance

D176 is accepted on exact head `9e48c1cee4e44a0f272378ff4f60b4cabc9bed8a`, workflow `34408371837`.
