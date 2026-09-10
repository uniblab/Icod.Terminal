# Kitty OSC 99 Desktop Notifications

This document is the permanent 1.x contract for the typed Kitty OSC 99 notification surface introduced by `Icod.Terminal 1.4.0` and extended with bounded interactive reporting in `Icod.Terminal 1.9.0`.

OSC 99 is a distinct notification protocol. It does not replace the legacy OSC 9 `SendNotificationAsync(...)` API or the urxvt-style OSC 777 `SendTitledNotificationAsync(...)` API, and `Icod.Terminal` does not automatically choose among them from terminal branding.

Primary protocol reference:

- Kitty desktop notification protocol: `https://sw.kovidgoyal.net/kitty/desktop-notifications/`

## Public API

The primary send operation is:

```csharp
ValueTask SendKittyNotificationAsync(
	string title,
	string body = "",
	KittyNotificationOptions? options = null,
	CancellationToken cancellationToken = default
);
```

An identified notification can be explicitly closed with:

```csharp
ValueTask CloseKittyNotificationAsync(
	string identifier,
	CancellationToken cancellationToken = default
);
```

Explicit observations are available through:

```csharp
ValueTask<KittyNotificationSupport> QueryKittyNotificationSupportAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

ValueTask<IReadOnlyList<string>> QueryKittyAliveNotificationsAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

The query methods use the session's existing authoritative input/response router. They do not open a competing reader.

Version 1.9 also allows `KittyNotificationOptions` to request unsolicited activation/button and close reporting. Those reports are returned through the same public session event stream:

```csharp
TerminalEvent terminalEvent = await session.ReadEventAsync(...);
```

There is no separate notification-event reader.

## Framing

Every request uses the Kitty OSC 99 envelope:

```text
OSC 99 ; <metadata> ; <payload> ST
```

The two semicolons are always emitted, including when the payload is empty. Outbound requests use seven-bit OSC introduction and canonical ST termination:

```text
ESC ] ... ESC \
```

The bounded input parser accepts the supported OSC framing used by the common query and semantic-event infrastructure.

## Text encoding and chunking

Title/body payloads are encoded as:

1. strict UTF-8;
2. RFC 4648 Base64;
3. Kitty `e=1` payload data.

Base64 keeps arbitrary well-formed Unicode as data rather than raw OSC framing. It provides no confidentiality.

The established resource limits include:

```text
title UTF-8 bytes               <= 65,536
body UTF-8 bytes                <= 65,536
one encoded payload chunk       <= 4,096 bytes
one metadata text value         <= 4,096 UTF-8 bytes
notification-type entries       <= 16
icon-name entries               <= 16
transmitted icon bytes          <= 262,144
one encoded OSC frame           <= 16,384 bytes
notification identifier         <= 128 ASCII characters
alive-query returned IDs        <= 256
support/alive response payload  <= 4,096 bytes
interactive buttons             <= 16
one button label UTF-8 bytes    <= 512
button payload UTF-8 bytes      <= 2,048 including separators
```

Large title/body/icon payloads are split into multiple OSC 99 frames. `d=0` marks an incomplete request and `d=1` marks the final emitted part. Every payload-bearing frame emitted by this API uses `e=1`.

All frames for one `SendKittyNotificationAsync(...)` call are encoded and validated before output commitment. After the shared session output gate is acquired, the complete frame sequence is written without caller cancellation splitting the protocol transaction. No implicit flush is performed for ordinary notification emission.

## Notification identity and updates

`KittyNotificationOptions.Identifier` supplies an explicit stable notification identity.

Identifiers use only:

```text
A-Z a-z 0-9 _ - + .
```

and are bounded to 128 characters. The backwards-compatibility special identifier `0` is rejected for caller-created identities.

Reusing an identifier requests update/replacement semantics from the terminal. `Icod.Terminal` does not maintain its own persistent notification database and does not claim that replacement occurred; successful completion proves only frame emission.

A multi-frame request requires an identifier so the terminal can associate its chunks. When the caller does not provide one, `Icod.Terminal` generates an internal safe identifier solely for that transmission. It is intentionally not exposed as persistent application state.

Callers that need later update, close, or interactive-report correlation semantics must supply their own stable identifier. Version 1.9 specifically requires an explicit caller-supplied identifier when activation/button or close reporting is requested.

## Filtering metadata

The typed options expose:

```text
ApplicationName     -> f=<Base64 UTF-8>
NotificationTypes  -> repeated t=<Base64 UTF-8>
```

These fields may participate in terminal-side filtering policy. They are explicit caller data; `Icod.Terminal` does not inspect the process, executable, environment, or shell to populate them automatically.

## Activation focus and reporting

Kitty's default activation behavior includes focusing the originating terminal window.

`KittyNotificationOptions.FocusOnActivation` defaults to `true`. Setting it to `false` emits `-focus` in the action list.

`KittyNotificationOptions.ReportActivation` is independent. Setting it to `true` requests Kitty activation/button reports using the protocol `report` action. It requires an explicit `Identifier` so the application has a stable correlation identity.

`FocusOnActivation = false` and `ReportActivation = true` therefore compose: an application can request an activation report without requesting that activation focus the terminal window.

## Buttons

`KittyNotificationOptions.Buttons` supplies an optional bounded ordered list of button labels.

The labels are strict UTF-8, joined using protocol-defined U+2028 LINE SEPARATOR, Base64 encoded, and emitted as one `p=buttons` payload. Version 1.9 does not invent multipart button semantics.

The button count, per-label UTF-8 size, and total UTF-8 payload size are bounded before output commitment. Interactive button reporting requires `ReportActivation = true` and an explicit notification identifier.

A reported button number is one-based and is exposed as `TerminalNotificationEvent.ButtonNumber` only for `TerminalNotificationEventKind.ButtonActivated`.

## Close reporting

`KittyNotificationOptions.ReportClose` requests close-event reporting (`c=1`) and requires an explicit caller-supplied notification identifier.

A normal close report becomes `TerminalNotificationEventKind.Closed`.

The protocol `untracked` result becomes `TerminalNotificationEventKind.CloseTrackingUnavailable`. This is not a close event. It means reliable future close tracking is unavailable, and the library preserves that uncertainty instead of fabricating `Closed`.

## Occasion

`KittyNotificationOccasion` is a closed semantic enum:

```text
Always      -> always
Unfocused   -> unfocused
Invisible   -> invisible
```

`Always` is the default and is omitted when no override is needed.

## Urgency

`KittyNotificationUrgency` maps to:

```text
Low       -> 0
Normal    -> 1
Critical  -> 2
```

Urgency is optional. The terminal and host desktop determine presentation policy.

## Expiration

`KittyNotificationOptions.Expiration` has these semantics:

```text
null                  terminal/OS default policy
TimeSpan.Zero         request no automatic expiry
positive TimeSpan     requested auto-close interval in milliseconds
```

Positive intervals are rounded upward to a whole millisecond and bounded to `Int32.MaxValue` milliseconds. Expiration is terminal/desktop policy, not a timer owned by `Icod.Terminal`.

## Sound

`KittyNotificationOptions.SoundName` is strict UTF-8 transported as Base64 metadata.

Kitty-defined common names include `system`, `silent`, `error`, `warn`, `warning`, `info`, and `question`. The API does not close the string to that list because terminals may advertise additional names through the support query.

## Icons

`IconNames` contains ordered icon names, each transported as Base64 UTF-8. The terminal may select the first name it can resolve locally.

`IconData` supports bounded transmission of PNG, JPEG, or GIF bytes and limits the source data to 262,144 bytes. Raw icon transfer **does not require caching**.

`IconDataIdentifier` maps to Kitty's `g` metadata key and is optional. Supplying it with icon data requests that the terminal cache the transmitted image under that identifier. The same identifier can later be supplied without `IconData` to request reuse of previously cached icon data.

The cache identifier uses the same bounded identifier grammar as notification IDs. Applications are responsible for choosing identifiers that are sufficiently unique for their use. `Icod.Terminal` does not fetch image URLs, inspect files, or load icon bytes from disk automatically.

## Close requests

`CloseKittyNotificationAsync(identifier)` emits the typed close form with `i=<identifier>:p=close` and an empty payload.

This operation requires interactive terminal output and participates in shared session output ordering. Successful completion proves request emission only.

A session does not automatically call `CloseKittyNotificationAsync(...)` during disposal. Notifications are not session-owned reversible terminal state.

## Capability query

`QueryKittyNotificationSupportAsync(...)` sends a uniquely identified `p=?` query and correlates the reply by exact query identifier and payload type.

`KittyNotificationSupport` reports terminal-advertised support for focus action, activation reports, close-event reports, title/body/close/icon/alive/buttons payloads, automatic expiration, occasions, urgency levels, and sound names.

Unknown future support keys are ignored where safe. A timeout remains a timeout; silence is not converted into a permanent unsupported claim.

A correlated query response has precedence over unsolicited semantic classification. A frame owned by an active or bounded late-response query transaction is never also delivered as a semantic notification event.

## Alive query

`QueryKittyAliveNotificationsAsync(...)` sends a uniquely identified `p=alive` query and returns a bounded list of notification identifiers reported as alive by the terminal.

The result is a live observation rather than library-owned state and can become stale immediately after receipt. Returned IDs are treated as untrusted terminal data and validated against protocol/resource bounds.

## Unsolicited semantic reports

Version 1.9 recognizes the bounded Kitty OSC 99 application-report forms needed for interactive notification observation and maps them into the protocol-neutral event envelope:

```text
activation             -> Activated
button activation      -> ButtonActivated + one-based ButtonNumber
close                   -> Closed
close + untracked       -> CloseTrackingUnavailable
```

The event carries the validated terminal-reported identifier. The library does not expose raw OSC bytes, selectors, arbitrary metadata dictionaries, or a generic Kitty event object.

Reports enter through the one authoritative `TerminalSession.ReadEventAsync(...)` stream. Same-stream ordering with ordinary input is preserved, active query ownership takes precedence, and there is no second unbounded semantic queue.

Malformed or oversized owned report candidates are rejected/recovered within bounded parser rules and are not leaked into ordinary text. After recovery, routing re-enters active-query precedence before later buffered traffic is considered.

## Redirected output

Send/update/close operations require an interactive terminal output endpoint and reject known redirected output before committing a frame.

Support/alive queries require compatible interactive input and output endpoints through the existing query-availability contract.

## Security and privacy

OSC 99 can publish more metadata than OSC 9/777, including title/body, stable IDs, filtering metadata, urgency/expiry/sound policy, icon names, cache IDs, caller-supplied image bytes, and interactive button labels.

Notification services, terminal logs, multiplexers, remote transports, notification histories, lock screens, or screen-sharing software may expose decoded data. Base64 is framing, not encryption.

Unsolicited activation/button/close reports are validated but unauthenticated terminal input. A terminal, multiplexer, remote peer, or hostile byte source can fabricate a syntactically valid identifier, button number, activation, close, or `untracked` report. Applications must not use these events as an authentication or authorization boundary.

`Icod.Terminal` does not automatically harvest process arguments, shell history, command output, environment variables, filesystem paths, or application state for notifications.

## Lifecycle

OSC 99 notifications and their reports are application metadata/observations, not session-owned reversible terminal state.

`Icod.Terminal` therefore does not replay notification requests on resume, synthesize close requests on disposal, retain a hidden database of sent notification IDs, reconstruct host notification history, or synthesize semantic events across lifecycle transitions.

Already-decoded queued semantic notification events remain observed application input across suspend/resume and are delivered once. Caller cancellation of a `ReadEventAsync(...)` wait does not abandon a fragmented semantic report already owned by the decoder. Session disposal terminates pending session-lifetime reads through normal shutdown.

Late correlated query responses retain their bounded query ownership after caller timeout/cancellation and are not reclassified as semantic reports.

## Compatibility

The 1.9 interactive extension is additive. OSC 9 and OSC 777 notification APIs remain unchanged and independently explicit. Existing noninteractive Kitty OSC 99 calls retain their prior byte behavior when the new interactive options are unused. Existing OSC 633/1337 and all other stable 1.x contracts retain their prior meaning.

The final 1.9 API fingerprint is `e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315` and is recorded in `Public-API-Baseline-1.9.md` / `.sha256`; historical stable public API baseline files remain retained unchanged.
