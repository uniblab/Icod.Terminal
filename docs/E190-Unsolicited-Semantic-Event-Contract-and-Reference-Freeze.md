# E190 — Unsolicited Semantic Event Contract and Reference Freeze

**Release:** `1.9.0`  
**Status:** contract/reference freeze  
**Scope:** architecture and protocol ownership only; no public API or runtime behavior change

## Purpose

E190 freezes the architectural rules required before `Icod.Terminal` can surface unsolicited semantic terminal events.

The immediate forcing case is Kitty OSC 99 desktop-notification interaction. Version 1.4 intentionally deferred activation, button, and close reports because they arrive without a corresponding active query and therefore cannot be modeled safely as ordinary query responses or by adding a second input reader.

Version 1.9 treats Kitty OSC 99 as the first implementation of a broader semantic-event lane. E190 therefore freezes the event category, ownership precedence, ordering, boundedness, query coexistence, lifecycle direction, trust model, and exclusions before E191 defines the public API.

Primary protocol reference:

`https://sw.kovidgoyal.net/kitty/desktop-notifications/`

## Existing authoritative input architecture

The stable 1.x input/query architecture is:

```text
TerminalSession
    -> TerminalQueryTransactionManager
        -> TerminalInputCoordinator
            -> TerminalInputDecoder
                -> one borrowed ITerminalInput
```

`TerminalSession.ReadEventAsync(...)` is the public unified application read path. Active terminal queries share the same decoder/coordinator and claim correlated response traffic internally.

No part of 1.9 may create a second terminal-input reader, background OSC listener, notification-specific raw reader, or callback path that competes with `ReadEventAsync(...)` for application-visible terminal traffic.

## Definition: semantic terminal event

A **semantic terminal event** is application-relevant terminal traffic that:

1. is produced by the terminal byte stream;
2. is not ordinary text/key/mouse/focus/paste input;
3. is not a lifecycle event from the host lifecycle source;
4. is not consumed as the response to an active typed terminal query;
5. has a reviewed semantic interpretation that can be exposed without publishing raw protocol framing.

This definition is intentionally protocol-neutral.

Kitty OSC 99 notification interaction is the first accepted family, but the public/event architecture must not encode “semantic event” as “Kitty event.” Future protocols require separate review and must reuse the same authoritative reader.

## Frozen ownership precedence

For one decoded terminal-control candidate, ownership is resolved in this order:

```text
1. active query/response ownership
2. recognized unsolicited semantic-event ownership
3. ordinary application-input decoding/fallback
```

### Query ownership wins first

If an armed active query owns a frame under its reviewed matcher/correlation rules, that frame belongs only to the query transaction.

It must not also be emitted as a semantic event, even if its outer OSC family and metadata resemble an unsolicited report.

This preserves the existing rule that one terminal frame has one authoritative consumer.

### Semantic ownership is second

A frame not owned by an active query may become a semantic event only after a protocol-specific recognizer reaches its reviewed ownership point.

For Kitty OSC 99 this requires sufficient structure to identify an accepted unsolicited report form rather than merely seeing `OSC 99`.

Recognition establishes bounded ownership, not trust. Once owned, malformed continuation, invalid payload, overflow, CAN/SUB, missing termination, or oversize does not cause the frame to be reinterpreted as harmless user text.

### Ordinary input is last

Traffic that is neither query-owned nor recognized as a reviewed semantic event continues through the existing application-input/control-language behavior.

Version 1.9 does not turn unknown OSC/DCS/APC/CSI frames into a generic raw event API.

## Same-stream ordering

Ordinary terminal input and semantic terminal events originate from the same byte stream and therefore share one ordering domain.

If the terminal emits:

```text
text A
semantic event B
key C
```

then application-visible delivery must preserve:

```text
A -> B -> C
```

subject only to the existing distinction that lifecycle events originate from a separate lifecycle source.

A dedicated semantic-event side queue that can race the existing application-input queue is rejected. E192 may change the internal coordinator representation, but it must preserve one bounded same-stream ordering domain.

## Boundedness and backpressure

Terminal-controlled event traffic is untrusted and must remain bounded.

The existing `TerminalInputCoordinator` uses bounded deferred application-event storage. Version 1.9 must preserve equivalent boundedness when the application-facing decoded stream widens to include semantic events.

Frozen rules:

- no unbounded semantic-event queue;
- slow consumers apply backpressure;
- queue capacity remains explicitly bounded;
- timeout/cancellation of one `ReadEventAsync(...)` wait does not discard already-decoded queued events;
- query processing must remain able to make progress without losing application events;
- disposal must unblock pending reads through the existing session shutdown authority.

E192/E194 may retain the current capacity values or change them only with explicit measured/tested justification.

## Kitty OSC 99 unsolicited report subset

The 1.9 accepted report family is restricted to:

```text
activation
button activation
close
close tracking unavailable (`untracked`)
```

The current Kitty protocol defines interactive request controls including:

```text
a=report    request activation/button reports
c=1         request close reports
p=buttons   transmit button labels
```

Buttons are transported as UTF-8 labels separated by U+2028 LINE SEPARATOR. Returned button numbers are one-based.

`untracked` is not a close event. It reports that the terminal/host cannot reliably track notification closure. The public projection must preserve that uncertainty.

## Query/report collision inventory

The existing OSC 99 implementation already has typed support and alive queries. Those query responses share the same outer protocol family as unsolicited notification reports.

Therefore E193/E194 must prove that:

- support-query responses remain owned by their active query;
- alive-query responses remain owned by their active query;
- an unsolicited activation/button/close report cannot accidentally satisfy either query;
- an active query response cannot also be emitted as an unsolicited event;
- unrelated query identifiers do not grant semantic trust;
- identifier collisions are handled by exact protocol ownership rules rather than arrival-order guesses.

The same coexistence principle applies to non-OSC99 queries sharing the authoritative decoder, including Primary DA, DECRQSS, XTGETTCAP, color, geometry, and Kitty Graphics probes.

## Recognition and recovery direction

E190 freezes these parser requirements for E193:

- incremental recognition under arbitrary fragmentation;
- accepted seven-bit and existing-compatible eight-bit OSC framing where supported by the common scanner;
- bounded identifier parsing;
- bounded positive button-number parsing with overflow rejection;
- exact recognition of the close selector;
- exact distinction between close and `untracked`;
- deterministic CAN/SUB and malformed-termination handling;
- bounded oversize drain/resynchronization;
- no malformed owned report leaks into ordinary text/key events;
- unknown future OSC 99 metadata/payload forms remain outside the 1.9 semantic contract unless separately reviewed.

## Public API constraints for E191

E190 does not freeze exact public names, but it freezes the shape constraints E191 must honor:

- additive only;
- existing `TerminalEventKind` numeric values remain unchanged;
- any new event-kind member is appended;
- semantic events are delivered through `ReadEventAsync(...)`;
- no second public semantic reader;
- no raw OSC payload/frame exposure;
- no arbitrary metadata dictionary;
- no public protocol-backend identifier merely to classify the event;
- notification activation, button activation, close, and close-tracking-unavailable remain semantically distinct.

## Request-side constraints for E195

Interactive reporting remains explicit opt-in.

When reporting is requested:

- the caller supplies a stable explicit notification identifier;
- internal generated multipart identifiers do not become application correlation identities;
- button labels and count are bounded;
- strict UTF-8 and existing OSC 99 Base64/framing rules are retained;
- `FocusOnActivation` remains an independent option and is not silently rewritten by report configuration;
- existing noninteractive notification emission remains byte-compatible when new options are unused;
- successful send completion proves only emission of the request.

## Lifecycle direction

Notification reports are observed application events, not reversible terminal state.

Version 1.9 does not replay notifications after resume, synthesize interaction events, close notifications automatically on disposal, or retain a permanent notification-state database.

Already-decoded queued semantic events represent observations that occurred on the byte stream and should remain drainable across lifecycle transitions unless E197 discovers a correctness conflict that requires an explicit documented change.

Live capability evidence and believed terminal state remain generation-scoped under their existing contracts.

## Trust and security model

A typed semantic event remains untrusted terminal input.

A terminal emulator, multiplexer, remote endpoint, or hostile byte source can fabricate notification identifiers, activation reports, button numbers, close reports, and `untracked` results.

`Icod.Terminal` validates syntax and resource bounds but does not authenticate:

- the terminal emulator;
- the desktop notification service;
- the originating process;
- the relationship between a received identifier and a prior notification sent by the current process.

Applications must not use a notification interaction event as an authentication or authorization boundary.

No automatic process arguments, environment variables, shell history, command output, clipboard data, or unrelated terminal contents are attached to semantic events.

## Resource-limit direction

Exact report-parser constants are finalized in E193/E194, but E190 freezes these principles:

- identifier limits must not exceed the existing reviewed OSC 99 identifier contract without separate justification;
- semantic report frame handling must remain within existing bounded control-frame infrastructure or a smaller dedicated ceiling;
- button numbers use a finite positive integer range appropriate to the public representation selected in E191;
- event queues remain finite;
- malformed/oversized traffic consumes bounded memory during recovery.

## Explicit exclusions

E190 rejects the following from the 1.9 scope:

- generic raw terminal-event delivery;
- arbitrary OSC 99 metadata exposure;
- second terminal reader/listener;
- event callbacks competing with `ReadEventAsync(...)`;
- terminal-brand or `TERM` based activation;
- host-native desktop notification APIs;
- hidden notification databases;
- authentication of terminal reports;
- automatic notification replay or close-on-dispose;
- persistent raster ownership/placement;
- image-file decoding;
- PTY/ConPTY hosting;
- DCurses layout/widget policy.

## Permanent authorities affected later

E199 must reconcile at least:

- `Icod.Terminal-Development-Roadmap.md`;
- `Icod.Terminal-1.9.0-Development-Roadmap.md`;
- `docs/Architecture.md`;
- `docs/Input-and-Events.md`;
- `docs/Queries-and-Responses.md`;
- `docs/Kitty-Osc99-Desktop-Notifications.md`;
- `docs/Security-and-Privacy.md`;
- `docs/Compatibility-and-Versioning.md`;
- `README.md`;
- `CHANGELOG.md`;
- curated `docs/releases/1.9.0.md`;
- package/API verification authorities.

## E190 acceptance gate

E190 is complete when:

1. the roadmap and this contract use the same semantic-event definition;
2. query > semantic > ordinary-input ownership precedence is frozen;
3. same-stream ordering and bounded buffering are frozen;
4. OSC 99 report/query collision points are explicitly identified;
5. lifecycle and trust direction are explicit;
6. no public API, runtime protocol behavior, or package version is changed by E190 itself;
7. later E191–E199 work can proceed without reopening these architectural ownership decisions.
