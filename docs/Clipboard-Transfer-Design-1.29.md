# Bounded Clipboard Transfers — 1.29 Design

**Status:** Approved by the maintainer; implemented in the 1.29.0-alpha.1 candidate.
**Decision date:** 2026-10-10 (America/New_York).
**Baseline:** Icod.Terminal main `2c410cd80663fef30912079e2c0de7a550852b97`,
stable [v1.28.0](https://github.com/uniblab/Icod.Terminal/releases/tag/v1.28.0).
**Consumer:** Icod.Dedit, through the forthcoming DCurses clipboard facade.

## Goal

Allow applications to opt into larger, explicitly bounded OSC 52 clipboard
transfers without changing the existing 65,536-byte default, inventing an
acknowledgement, or weakening input/query ownership.

The maintainer explicitly confirmed the 64 KiB default and application opt-in to
larger buffers. Local budget selection is distinct from remote negotiation:
OSC 52 provides no standard maximum-size handshake. Do not call local acceptance
negotiated terminal capacity.

The current implementation caps decoded read and write payloads at 65,536 bytes.
Its base64 ceiling is 87,384 bytes and complete response-frame ceiling 87,400
bytes. Those constants also feed the response framer and input-buffer ceiling.
Changing one constant would affect unrelated input and response behavior.

## Scope and ownership

Terminal owns protocol encoding, response correlation, bounded transport buffers,
serialized output, deadlines, cancellation and lifecycle cleanup.
Applications own selected text, internal clipboard contents, user commands,
fallback messages and document undo. DCurses provides access through its
existing session; it does not create a second reader.

This release covers configurable read/write capacity, efficient bounded encoding
and response accumulation, adversarial tests, public sample/package consumers,
and exact-environment compatibility evidence.

Excluded: OSC 5522/MIME, native operating-system clipboard APIs, clipboard
history/synchronization, automatic clipboard probes, public raw protocol access,
new transfer streams, protocol-specific append workarounds, multiplexer forwarding,
and changes to Widgets or Dedit. Bracketed paste remains a distinct existing
chunked input facility; its total size is not the OSC 52 limit.

## Options considered

1. Raise the global constant: simplest edit, but changes all callers' memory and
   framing policy and does not preserve the default contract. Rejected.
2. Session-scoped read/write budgets with a separately bounded, correlated
   clipboard-response accumulator: recommended. More internal work, but preserves
   ordinary input limits and avoids new overloads or ambiguous default arguments.
3. Public streaming clipboard API or another protocol: larger contract and
   lifecycle scope than the editor currently needs. Deferred.

## Approved public contract

Names and values below are the concrete review proposal. Freeze them at T2900;
they are not shipped API until implemented and qualified.

```csharp
public sealed class TerminalClipboardOptions {
    public static int DefaultMaximumBytes { get; } // 65_536
    public static int SupportedMaximumBytes { get; } // 16_777_216
    public int MaximumReadBytes { get; init; } = 65_536;
    public int MaximumWriteBytes { get; init; } = 65_536;
}

// Add to TerminalSessionOptions:
public TerminalClipboardOptions ClipboardOptions { get; init; } = new();
```

Both budgets accept inclusive range 1..16,777,216 decoded bytes. Zero is not a
disable sentinel. An empty clipboard payload is valid and an empty write retains
its existing explicit-clear meaning. Null options are rejected during session
validation; invalid budgets fail before acquiring modes or emitting output.
The session retains immutable effective values for its lifetime.

The 16 MiB hard ceiling is an approved library resource policy, not a terminal
capability claim or an advertised guarantee that any terminal accepts that size.
Applications may choose any lower positive budget independently for reads and writes.

Existing methods and return types remain unchanged:

- `WriteClipboardAsync(TerminalClipboardSelection, ReadOnlyMemory<byte>, CancellationToken)`
- `WriteClipboardAsync(TerminalClipboardSelection, string, CancellationToken)`
- `ReadClipboardAsync(TerminalClipboardSelection, TimeSpan, CancellationToken)`

The methods use their owning session's budgets. Read/write ceilings are
independent: increasing write capacity must not increase accepted response size.
Text is strict UTF-8 without BOM; limits count encoded bytes, not UTF-16 code
units or displayed characters. Binary reads remain exact bytes; no new text-read
convenience overload is required.

## Derived bounds and ordinary input

For decoded capacity D:

- encoded capacity E = checked(4 * ((D + 2) / 3));
- canonical outgoing frame length = checked(E + 9);
- accepted response framing budget F = checked(E + 16), preserving legacy slack.

At D = 65,536: E = 87,384 and F = 87,400.
At D = 16,777,216: E = 22,369,624 and F = 22,369,640.
Validation additionally checks actual decoded length, canonical base64 and
padding: equal encoded lengths can represent different decoded lengths.

Keep `TerminalSession.MaximumBufferedInputBytes` at 87,400 and preserve
`TerminalInputDecoderOptions.MaximumBufferedBytes` validation/default behavior.
Do not raise every input/response buffer to the clipboard hard maximum.
Explain that the existing property bounds ordinary undecoded input; a recognized
clipboard response may use a separate bounded accumulation budget.

Only a complete matching OSC 52 selection prefix belonging to an active query
or its bounded late-response owner can enter the clipboard accumulator.
Unrelated OSC/CSI/DCS/APC, unsolicited data, wrong-selection responses and
ordinary input retain their existing rules. The generic `ForCompletion`
response-plan behavior must not globally inherit the new clipboard ceiling.

For read budgets at or below the legacy 65,536-byte default, preserve the existing
effective framing ceiling imposed by a caller's smaller ordinary-input buffer.
Only an explicit read budget above 65,536 enables the dedicated larger-response
path. This avoids silently changing existing small-buffer callers. Document this
compatibility rule and test both paths; callers requesting larger reads do not
also have to enlarge their ordinary keyboard buffer.

The accumulator is internal and attached to the existing decoder/query owner.
It accepts fragmented data from that reader; it never opens another input
reader. Allocate lazily according to received bytes, not at session open.
Track incremental parse position rather than rescanning the entire accumulated
response after each byte. Reset/release storage on every terminal outcome.

## Output contract

Validate selection, payload size and text encoding before output commitment.
Serialize through the existing output gate. Emit one complete OSC 52 operation;
several replacement operations are not an append protocol.

Avoid the existing intermediate base64 string plus ASCII conversion for large
writes. Encode directly into the bounded output-frame buffer or use an internal
bounded encoder that preserves a single serialized logical frame. No public
streaming API is introduced.

Preserve pre-commit caller cancellation and complete-frame integrity once
transmission begins. Do not add mid-frame caller cancellation or claim rollback.
Transport failure may leave remote state uncertain; surface the failure and
retain the library's existing lifecycle/failure handling. A nonresponsive output
transport cannot be made universally interruptible by this feature.

Completion means output was emitted, not that the terminal changed its clipboard.
No automatic readback, support inference or write-acknowledgement claim.

## Input, deadlines and recovery

Build an explicit clipboard response plan carrying the session read budget.
The query deadline still starts at queueing and includes waiting for output.
Use the existing query transaction manager and late-response ownership.
A timed-out/cancelled request must not supply a result to a later query.

Return the complete decoded byte array only after validation. Oversized,
malformed or truncated correlated replies yield failure, never partial success.
No silent truncation; no response fragments may become ordinary typed input.

On correlated overflow, stop accumulating and discard through the OSC terminator
using bounded scratch space. Freeze an explicit recovery byte budget from the
query's frame budget and reuse the existing late-ownership/lifecycle timing
contract. Recommended additional discard allowance: one F-byte frame budget;
after that, fail closed with the existing decoder failure mechanism rather than
resuming ordinary input inside an unresolved OSC string. Do not replace the old
global discard limit with an effectively unbounded global limit.

At EOF, release retained storage and expose failure/end-of-input according to
the existing query contract. Caller cancellation does not authorize abandoning
a partial frame and treating its remainder as input. Disposal cancels the
underlying reader and releases retained state deterministically.

Timeout does not establish that clipboard reads are unsupported or denied.
The terminal may ignore reads by policy. Samples must distinguish transmission,
decoded response, timeout and operator-observed clipboard behavior.

## Memory, performance and privacy

Default sessions must not allocate capacity for the 16 MiB ceiling. Write
preflight rejects over-budget input before allocating a frame. Incoming storage
must remain proportional to the active query's configured budget, not peer input.

Use deterministic allocation/buffer-growth instrumentation in tests:
no retained accumulator after completion/disposal; no full-prefix reparse per
fragment; peak response storage stays within F plus bounded growth/copy overhead.
Before merge, record peak allocation and elapsed time for 64 KiB, 1 MiB and
16 MiB reads/writes and fragmented reads. No "unlimited" or zero-copy claims.

Clipboard payloads must not appear in exceptions, diagnostics, sample reports,
CI artifacts or compatibility evidence. Tests/samples use generated fixtures.
Interactive read/write scenarios require an explicit operator action; do not
query or overwrite the user's clipboard during sample startup or session open.

## Compatibility and release gates

- Preserve existing overload signatures, enum values, default frame grammar,
  strict UTF-8 policy and empty-content behavior.
- Retain existing exception categories: local binary size violation is
  ArgumentOutOfRangeException; text-size/invalid-text violation is ArgumentException;
  correlated malformed/oversized response is FormatException.
- Preserve small/default sessions, concurrent-session isolation, query ordering,
  unsolicited events, bracketed paste and late-response behavior.
- C# 13, net8.0/net9.0/net10.0, AnyCPU where possible; existing dependencies only.
- DLL LGPL-3.0-or-later; samples/tests GPL-3.0-or-later; established file headers.
- Scripts remain cmd/sh/PowerShell 5.1-compatible; no Python or native helpers.
- Execute inline, no subagents. Owner merges, tags and publishes.
- Planning PR does not bump runtime/package versions. T2900 establishes
  1.29.0-alpha.1; stable 1.29.0 promotion is a later reviewed step.
- Live results identify terminal/version, OS, transport, runtime, commit and
  payload sizes. Unavailable environments remain NotRun; do not invent coverage.

## Implementation rulings

- Reuse the existing control-sequence scanner incrementally; no second reader or
  duplicate control grammar. Keep the generic scanner ceiling unchanged.
- An invalid/oversized correlated frame reports FormatException after bounded
  terminator drain. If no terminator arrives, the caller deadline may expire first;
  exhausted recovery or late ownership fails the input coordinator closed.
- Preserve existing wrong-selection/unsolicited decoding and tiny ordinary-buffer
  behavior. Seven ordinary bytes accommodate selection-prefix recognition; once
  correlated, larger reads do not depend on the ordinary buffer's payload capacity.
- Verify linear work with one scanner Feed call per input byte and deterministic
  allocation bounds rather than production-only instrumentation counters. Record
  measurements and outstanding live gates in the qualification document.

## Authorities

- [Existing clipboard write contract](T56-Semantic-Clipboard-Write-API.md)
- [Existing clipboard read contract](T57-Explicit-Clipboard-Read-API.md)
- [XTerm control sequences, OSC 52](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)
- [Architecture](Architecture.md)
- [Security and privacy](Security-and-Privacy.md)
- [Development roadmap](../Icod.Terminal-1.29.0-Development-Roadmap.md)
