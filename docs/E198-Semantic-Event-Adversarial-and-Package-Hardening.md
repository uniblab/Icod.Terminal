# E198 — Semantic Event Adversarial and Package Hardening

**Release:** `1.9.0`  
**Tranche:** `E198`  
**Result:** accepted with focused semantic-input recovery hardening  
**Final behavioral checkpoint:** `0f9eaa169922fa8679df682239c5d7e2fb6afa8f`  
**Final validation:** pull-request workflow `#1444` / `34526210297`

## Purpose

E198 qualifies the 1.9 unsolicited semantic-event architecture against hostile framing, boundary conditions, queue pressure, repeated waits/lifecycle cycles, fresh-package consumption, and the existing downstream `Icod.DCurses` witnesses.

Unlike E197, E198 uncovered two real decoder defects. Both were fixed inside the established one-reader/query-routing architecture rather than by adding a semantic side channel or weakening parser validation.

## Findings and fixes

### Malformed owned OSC 99 reports must recover instead of poisoning the session

The first adversarial RED checkpoint, `0bbaa28d9b8732fcff4471263022c4041ce18004`, showed five repeatable failures across the supported TFMs:

```text
MalformedOwnedReportIsDiscardedBeforeFollowingInput
CancelledOwnedReportDoesNotLeakIntoOrdinaryInput (CAN)
CancelledOwnedReportDoesNotLeakIntoOrdinaryInput (SUB)
InvalidMixedWidthTerminatorDoesNotLeakOwnedReportBytes
OversizedOwnedReportDrainsThroughTerminatorBeforeRecovery
```

A malformed complete OSC 99 report could propagate `FormatException` through the input coordinator and close its channel. Structurally invalid or oversized reports could also fall through toward ordinary input rather than preserve semantic ownership through bounded recovery.

The focused fix at `d4a99953da048f5e4d736c6eb7fcbf432e34c507` keeps the standalone OSC 99 parser strict while moving recovery responsibility to the decoder:

- malformed complete semantic candidates are consumed rather than terminating the coordinator;
- CAN/SUB and invalid mixed-width termination are consumed through the structural invalidation boundary;
- oversized OSC 99 candidates reuse the existing bounded OSC drain/resynchronization machinery;
- bytes following the rejected frame remain available to ordinary application decoding.

The parser therefore continues to reject invalid data while the live session remains recoverable.

### Recovery must re-enter active-query precedence

A second RED checkpoint, `006b207b3aacf0de5b2a5ea0ffa81e71149f67bf`, proved that semantic recovery could continue locally and bypass the active-query router for the immediately following frame. The focused regression `MalformedSemanticRecoveryReentersActiveQueryPrecedence` timed out because a valid correlated Kitty OSC 99 support reply was not offered back to its active query after a malformed semantic frame was discarded.

The fix at `e78e46b5beee8dc0b0094fee0ea217a7407c2302` adds an internal decoder-only routing-restart result. After semantic recovery, the outer decoder loop starts again at the permanent precedence boundary:

```text
active query response
    -> unsolicited semantic report
        -> ordinary application input
```

The restart state does not escape `ReadNextAsync(...)`, does not create recursion, does not release coordinator demand, and does not change the public API.

## Adversarial coverage

The E198 matrix exercises:

- every split point of seven-bit OSC 99 activation reports;
- every split point of eight-bit OSC 99 activation reports;
- repeated identifiers and concatenated semantic reports;
- malformed duplicate metadata;
- CAN and SUB cancellation bytes;
- invalid mixed-width termination;
- oversized semantic-report drain and recovery;
- preservation of following ordinary application input;
- bounded semantic-event queue backpressure;
- query precedence immediately after malformed semantic recovery;
- repeated timeout and cancellation while one semantic report remains fragmented;
- four repeated suspend/resume cycles with semantic events between cycles;
- no notification replay across those lifecycle cycles.

The repeated lifecycle stress uses the authoritative `TerminalSession.ReadEventAsync(...)` path for lifecycle and semantic events. This matches the permanent 1.x contract: `ReadEventAsync(...)` and `ReadLifecycleEventAsync(...)` consume the same lifecycle queue and are not independent duplicate streams.

## Package-only acceptance

E198 extends the existing OSC 99 package smoke instead of creating a second package-consumer mechanism.

The smoke restores the newly packed `Icod.Terminal` NuGet artifact into a temporary consumer and verifies on `net8.0`, `net9.0`, and `net10.0`:

- `TerminalEventKind.Semantic` and its stable numeric value;
- `TerminalSemanticEventKind` and `TerminalSemanticEvent`;
- `TerminalNotificationEventKind` and `TerminalNotificationEvent`;
- `TerminalEvent.Semantic`;
- `KittyNotificationOptions.ReportActivation`;
- `KittyNotificationOptions.ReportClose`;
- `KittyNotificationOptions.Buttons`;
- expected XML documentation for the new surface;
- continued absence of a second semantic/raw event-reader API.

Workflow `#1439` / `34523522318` proved the fresh NuGet-only consumer on all three target frameworks before final E198 closure.

## Final verification evidence

The final E198 behavioral checkpoint is:

```text
0f9eaa169922fa8679df682239c5d7e2fb6afa8f
```

Pull-request workflow `#1444` / `34526210297` passed:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / public API freeze
Package Foundation
Package Stable 1.x release line
Package Presentation
Package Semantic and hardening
Validated package artifact
```

Fresh Linux and Windows runtime builds reported:

```text
0 warnings
0 errors

net8.0   1,788 passed, 0 failed, 0 skipped
net9.0   1,788 passed, 0 failed, 0 skipped
net10.0  1,788 passed, 0 failed, 0 skipped
```

The notification sample continued to build on all supported TFMs. Existing `Icod.DCurses` synchronized-output, progress, pointer-shape, semantic-prompt, color-observation, modern-keyboard, and hardening-soak witnesses remained green; the hardening soak completed eight ownership cycles on every supported TFM.

## Security consequence

Recognizing an OSC 99 semantic prefix grants bounded decoder ownership, not trust. A hostile terminal cannot turn malformed owned report bytes into ordinary application text merely by failing later grammar/framing validation.

Likewise, recovering from one hostile report cannot weaken the next frame's query correlation. Active query ownership is re-applied before semantic or ordinary input classification.

This preserves the 1.9 security model:

- semantic reports remain untrusted terminal-controlled input;
- malformed owned reports remain bounded and recoverable;
- parser rejection does not poison the session;
- report recovery does not bypass query ownership;
- no raw vendor-event stream or second reader is introduced.

## Test-harness lessons

Several intermediate E198 failures were fixture defects rather than production regressions. They are worth recording because they reinforce the ownership contract:

1. A single five-second cancellation source was initially shared across all four lifecycle cycles, turning a per-operation bound into an accidental total-test deadline.
2. Two subsequent fixture rewrites briefly dropped structural braces; these were compile-time harness errors and never represented runtime behavior.
3. The first repeated lifecycle fixture switched between `ReadEventAsync(...)` and `ReadLifecycleEventAsync(...)` as though they were independent streams. They are not: both consume the same lifecycle queue. The corrected stress test uses the unified reader consistently, matching the permanent 1.x event-loop contract.

No production change was made to support that non-contract mixed-reader pattern.

## Next gate

E199 is the 1.9 public API, documentation, compatibility, package-metadata, and release-closure tranche. It must freeze the final 1.9 API fingerprint, update release-facing and permanent authorities, bump the source/package identity to `1.9.0`, and pass the complete Staging gate on one exact final PR head before the PR is considered release-ready.
