# Icod.Terminal 1.9.0 Development Roadmap

**Release:** `1.9.0`  
**Theme:** unsolicited semantic terminal events and interactive Kitty OSC 99 notifications  
**Status:** E190-E198 accepted; E199 release-candidate closure assembled and exact-head Staging qualification pending  
**Stable compatibility floor:** `1.0.0`  
**Prior release:** `1.8.1`  
**Release-candidate source/package identity:** `1.9.0`

## Why this release exists

`Icod.Terminal` already owns one authoritative live input conversation. Ordinary application input, lifecycle activity, and typed terminal query responses are coordinated through that one session rather than competing readers.

Before 1.9, the stable public `TerminalEvent` model represented:

```text
Input
Lifecycle
Timeout
Cancelled
```

Kitty OSC 99 desktop notifications expose a missing category: activation, button, and close reports can arrive without a corresponding active query. Version 1.4 deliberately deferred those forms because they are unsolicited application-relevant terminal traffic and must therefore enter through the authoritative event path rather than a second reader.

Versions 1.5–1.8 supplied the infrastructure needed to do this safely: normalized control-family framing, bounded parsing, query transactions, capability evidence, semantic routing, CSI/DCS/APC hardening, and side-observation for correlated Kitty traffic.

Version 1.9 closes that event-model gap before public capability planning or persistent raster ownership.

The architectural objective is:

> Establish a bounded, protocol-neutral path for unsolicited semantic terminal events inside the existing authoritative reader, then use Kitty OSC 99 activation, button, close, and close-tracking reports as the first complete implementation.

## Protocol reference

Primary reference:

`https://sw.kovidgoyal.net/kitty/desktop-notifications/`

Relevant report forms include activation, one-based button activation, close, and the `untracked` close-tracking result. Interactive request controls include `a=report`, `c=1`, and `p=buttons`; button labels use UTF-8 separated by U+2028 LINE SEPARATOR.

The existing `QueryKittyNotificationSupportAsync(...)` contract reports whether the terminal advertises activation reports, close events, and buttons. Version 1.9 reuses that observation rather than creating a competing capability database.

## Architecture result

The authoritative input path is:

```text
terminal bytes
    -> bounded decoder / control-language scanner
        -> active query response
        -> unsolicited semantic terminal event
        -> ordinary application input
```

The application-facing event stream remains unified:

```text
TerminalSession.ReadEventAsync(...)
    -> Input
    -> Lifecycle
    -> Timeout
    -> Cancelled
    -> Semantic
```

`TerminalEventKind.Semantic = 4` is appended after the existing stable values. Existing enum numeric values do not move.

Input and unsolicited semantic events originate from the same terminal byte stream. Their relative byte-stream order is preserved in one bounded coordinator domain rather than independent queues that can reorder decoded observations.

Lifecycle events remain a distinct source and retain the existing documented lifecycle semantics. `ReadEventAsync(...)` and `ReadLifecycleEventAsync(...)` consume the same lifecycle queue rather than duplicated lifecycle streams; applications should choose one ownership pattern rather than independent competing readers.

## Public semantic result

The final additive protocol-neutral shape is:

```text
TerminalEvent
    Kind
    Input?
    Lifecycle?
    Semantic?          new

TerminalSemanticEvent
    Kind
    Notification?      first 1.9 family

TerminalNotificationEvent
    Kind
    Identifier
    ButtonNumber?
```

The first notification event kinds are:

```text
Activated
ButtonActivated
Closed
CloseTrackingUnavailable
```

`CloseTrackingUnavailable` is not equivalent to `Closed`; it means the terminal reports that the host cannot reliably observe a future close event.

The semantic envelope does not expose raw OSC bytes, raw selectors, arbitrary metadata dictionaries, backend identifiers, or a generic vendor-event payload.

Version 1.9 does not add a second `ReadSemanticEventAsync(...)` path. Semantic events belong to the same `ReadEventAsync(...)` stream.

## Interactive notification request result

The existing typed Kitty notification API remains the protocol-specific request surface. `KittyNotificationOptions` gains:

```text
ReportActivation
ReportClose
Buttons
```

Rules:

1. Reporting is explicit opt-in.
2. Existing noninteractive notification calls remain byte-compatible when new options are unused.
3. Interactive reporting requires an explicit caller-supplied notification identifier; generated multipart identifiers are not stable application identities.
4. Buttons are bounded to 16 labels.
5. Each button label is bounded to 512 UTF-8 bytes.
6. The complete button payload is bounded to 2,048 UTF-8 bytes including U+2028 separators.
7. Button payload construction uses strict UTF-8 and the protocol-defined U+2028 separator.
8. Validation completes before output commitment.
9. Send completion proves request emission, not user interaction or host notification state.
10. The library does not retain a hidden database of sent notifications.
11. Returned identifiers and button numbers are untrusted terminal-controlled data.
12. Event identifiers are correlation data, not authentication.

The existing `FocusOnActivation` behavior composes independently with reporting.

## Event ownership and precedence

Ownership order is frozen as:

```text
1. active query/response ownership
2. recognized unsolicited semantic-report ownership
3. ordinary application-input decoding
```

A frame claimed by an active or bounded late-response query is never also published as a semantic event. A recognizable unsolicited semantic report never satisfies an unrelated query merely because it shares the OSC family.

Recognition establishes bounded ownership, not trust. Grammar, length, numeric, and framing checks remain mandatory. Malformed or oversized owned reports are consumed/recovered deterministically and are not leaked into ordinary application text.

After semantic recovery, routing restarts at active-query precedence before later buffered traffic is considered.

## Ordering and bounded buffering

Version 1.9 preserves the existing bounded, demand-driven coordinator model while widening the application-facing decoded stream to include semantic events.

Required and qualified behavior:

- same-stream input and semantic events preserve decode order;
- no unbounded semantic side queue exists;
- slow consumers produce bounded backpressure;
- active query completion can progress without losing already-decoded application events;
- timeout/cancellation of one `ReadEventAsync(...)` wait does not discard queued events or fragmented report state;
- disposal unblocks pending waits through existing session shutdown;
- enabling notification reporting does not create a second always-running reader.

The public contract is ordering, boundedness, and one-reader ownership, not a specific `Channel<T>` layout.

## Query coexistence

The release qualifies semantic reports before and after query responses, between fragmented frames, during query timeout/cancellation, during OSC 99 support/alive queries, and alongside existing query/control traffic.

A semantic event does not satisfy an unrelated query, extend a query deadline, become lost when a query completes, become double-delivered, or reorder already-decoded same-stream input.

Malformed semantic recovery re-enters query precedence, so an immediately following correlated response remains available to the active query transaction.

## Lifecycle, cancellation, and failure

OSC 99 reports are application observations, not reversible state. Version 1.9 does not replay notifications on resume, synthesize events, automatically close notifications on disposal, or reconstruct host notification history.

Already-decoded queued semantic events are preserved as observed application input; lifecycle generation changes continue to invalidate generation-scoped capability/state beliefs rather than retroactively invalidating observations already consumed from the byte stream.

Caller cancellation of `ReadEventAsync(...)` cancels the wait, not decoder ownership of fragmented bytes. Malformed reports are not converted into successful events, and transport/input failures remain failures rather than semantic events.

## Security and privacy

Unsolicited terminal events are untrusted external input. A terminal, multiplexer, remote endpoint, or hostile byte source can fabricate identifiers, activations, button numbers, close reports, `untracked`, and malformed frames.

The library validates framing and resource bounds but does not authenticate the terminal or prove an interaction originated from the host notification service. Applications must not use a typed notification event as an authorization boundary.

The semantic event model does not automatically attach process arguments, environment variables, shell history, command output, clipboard data, or unrelated raw terminal traffic.

## Final public API freeze

The final 1.9 public API fingerprint is:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

Authoritative files:

```text
docs/Public-API-Baseline-1.9.md
docs/Public-API-Baseline-1.9.sha256
```

Historical baselines remain retained unchanged.

## Release invariants

1. Stable `1.0.0` remains the compatibility floor.
2. `net8.0`, `net9.0`, and `net10.0` remain first-class targets.
3. One live `TerminalSession` remains the authoritative input reader.
4. No public second raw-input or semantic-event reader is introduced.
5. Existing `TerminalEventKind` numeric values remain unchanged; `Semantic = 4` is appended.
6. Ordinary input and semantic events preserve same-stream order.
7. Active query response ownership has deterministic precedence.
8. A frame is never double-delivered as query response and semantic event.
9. Semantic recognition remains bounded and incremental.
10. Malformed owned reports recover without leaking hostile bytes into ordinary input.
11. Recovery re-enters query precedence before later traffic is decoded.
12. Event buffering remains bounded and backpressured.
13. Notification reporting remains opt-in.
14. Existing 1.4 Kitty notification calls remain compatible when interactive options are unused.
15. Interactive correlation requires an explicit caller notification identifier.
16. `untracked` is surfaced as close-tracking uncertainty, not a fabricated close.
17. Interaction reports are validated but unauthenticated terminal input.
18. Existing support/alive queries remain authoritative and use the same reader/router.
19. Existing raster, keyboard, presentation, lifecycle, and restoration semantics remain unchanged.
20. No new package dependency is introduced merely for OSC 99 event support.
21. Fresh package-only validation covers all intentional new 1.9 public members on every supported TFM.
22. Current `Icod.DCurses` compatibility remains a release gate.

## Tranche plan

```text
E190  unsolicited semantic-event contract and reference freeze          accepted
E191  public TerminalEvent semantic envelope and API regret gate        accepted
E192  ordered decoder/coordinator semantic-event substrate              accepted
E193  Kitty OSC 99 unsolicited-report grammar and ownership             accepted
E194  query coexistence, bounded buffering, and routing integration     accepted
E195  interactive Kitty notification request options and buttons        accepted
E196  typed activation/button/close semantic event projection           accepted
E197  lifecycle, cancellation, disposal, and late-report semantics      accepted
E198  adversarial hardening, downstream/package/sample acceptance       accepted
E199  public API, documentation, compatibility, and release closure     qualification pending
```

## E190 — unsolicited semantic-event contract and reference freeze

Freeze terminology, ownership rules, protocol references, current OSC 99 query/report collision points, resource-bound direction, and permanent authorities before public API or parser work. E190 changes no public API or runtime behavior.

## E191 — public `TerminalEvent` semantic envelope and API regret gate

Freeze the smallest additive public model. Append one event-kind value, add one nullable semantic payload property, define protocol-neutral semantic and notification payload types, preserve existing enum values, and reject raw protocol exposure.

## E192 — ordered decoder/coordinator semantic-event substrate

Widen the internal application-facing decoded result so it can carry either ordinary terminal input or one semantic event while preserving the current bounded queue, demand-driven reader, query-demand behavior, end-of-input semantics, and one-reader invariant.

## E193 — Kitty OSC 99 unsolicited-report grammar and ownership

Recognize activation, button activation, close, and close+`untracked`. Harden seven/eight-bit framing as appropriate, fragmentation, identifier and number bounds, malformed/oversized input, CAN/SUB, termination, and query-response collision behavior.

## E194 — query coexistence, bounded buffering, and routing integration

Prove semantic events coexist with active queries, timeout/cancellation, rich input, modern keyboard, lifecycle events, and queue saturation without query interference, loss, reordering, or double delivery.

## E195 — interactive Kitty notification request options and buttons

Add typed opt-in request support for activation/button reporting, close reporting, and bounded button labels; compose with current focus/occasion/urgency/expiry/sound/icon/update semantics and preserve existing noninteractive byte output.

## E196 — typed activation/button/close semantic event projection

Publish the complete first semantic event family through `ReadEventAsync(...)`, including one-based button numbers and distinct close-tracking-unavailable semantics. Add a focused public sample without raw stream access.

## E197 — lifecycle, cancellation, disposal, and late-report semantics

Qualify queued events across suspend/resume, fragmented-report cancellation, disposal with pending waits, late query responses, end-of-input, and the no-auto-close/no-replay rules.

**Accepted:** the six-case semantic lifecycle matrix passed unchanged against the E196 runtime on `net8.0`, `net9.0`, and `net10.0` across Windows, Linux, and macOS. No production runtime change was required; the established authoritative reader, bounded application-event domain, query late-response ownership, and session lifecycle machinery already satisfy the 1.9 semantic-event contract. See `docs/E197-Semantic-Event-Lifecycle-and-Failure-Hardening.md`.

## E198 — adversarial hardening, downstream/package/sample acceptance

Exercise every split point, concatenation, boundary/overflow, malformed metadata/payload, oversized drain/recovery, identifier collisions, repeated cancellation/timeouts, queue saturation, interleaving stress, and repeated lifecycle cycles. Add fresh NuGet-only net8/net9/net10 acceptance and retain the current DCurses witness.

**Accepted:** adversarial testing exposed and corrected bounded semantic-report recovery and post-recovery query-precedence defects. The final E198 checkpoint `0f9eaa169922fa8679df682239c5d7e2fb6afa8f` passed pull-request workflow `#1444` / `34526210297` across Windows, Linux, macOS, the package/API candidate, all four package shards, and the validated artifact. Linux and Windows each completed 1,788 tests on every supported TFM with 0 warnings and 0 errors. Fresh NuGet-only OSC 99 semantic-event acceptance runs on `net8.0`, `net9.0`, and `net10.0`, while the existing `Icod.DCurses` acceptance and eight-cycle hardening-soak witnesses remain green. See `docs/E198-Semantic-Event-Adversarial-and-Package-Hardening.md`.

## E199 — public API, documentation, compatibility, and release closure

Freeze the final 1.9 API fingerprint, set source/package identity to `1.9.0`, update README/changelog/release notes, align permanent input/query/notification/security/architecture/versioning authorities, verify the release-line package contract, and run the complete Staging gate on one unchanged final PR head.

**Closure candidate assembled:** the release identity, NuGet release metadata, final public API baseline wording, root README, changelog, long-range roadmap, curated `docs/releases/1.9.0.md`, permanent authorities, and `docs/E199-1.9.0-Public-API-Documentation-Compatibility-and-Release-Closure.md` are being aligned on the feature branch. E199 is not accepted until the complete exact-head Staging matrix succeeds.

## Explicit exclusions

Version 1.9 does not add a raw OSC event stream, arbitrary vendor-event dispatcher, second semantic reader, callback delivery competing with `ReadEventAsync(...)`, host-native notifications, terminal-brand activation, persistent notification database, authentication of terminal reports, notification replay, automatic close-on-dispose, unrelated future OSC 99 payloads, persistent raster placement, image codecs, PTY/ConPTY hosting, or DCurses window/widget/layout policy.

## Relationship to later work

```text
1.8.1  stable maintenance baseline
    |
1.9.0  unsolicited semantic event architecture + Kitty OSC 99 reports
    |
1.10.0 public semantic capability inspection/planning
    |
1.11.0 persistent raster resource/placement lifecycle
    |
1.12.0 advanced placement only if justified
```

The 1.9 event architecture permits future reviewed unsolicited terminal protocols to add semantic event families without adding readers or exposing raw frames.

## Development PR rule

PR #50 remains the 1.9 development/release-candidate branch until one unchanged E199 head passes the complete Staging qualification matrix. Merge, tagging, and publication are not performed by the development tranche.

## Release rule

A completed E199 Staging gate is necessary but not sufficient to publish `1.9.0`. The maintainer performs the merge. The exact resulting `main` commit must then pass the complete Release distribution validation before the maintainer creates/pushes `v1.9.0` or publishes the release/package.
