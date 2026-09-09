# Kitty OSC 99 Desktop Notifications

This document is the permanent 1.x contract for the typed Kitty OSC 99 notification surface introduced by `Icod.Terminal 1.4.0`.

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

## Framing

Every request uses the Kitty OSC 99 envelope:

```text
OSC 99 ; <metadata> ; <payload> ST
```

The two semicolons are always emitted, including when the payload is empty. Outbound requests use seven-bit OSC introduction and canonical ST termination:

```text
ESC ] ... ESC \
```

The response parser accepts the bounded OSC response framing already supported by the common query infrastructure.

## Text encoding and chunking

Title/body payloads are encoded as:

1. strict UTF-8;
2. RFC 4648 Base64;
3. Kitty `e=1` payload data.

Base64 keeps arbitrary well-formed Unicode as data rather than raw OSC framing. It provides no confidentiality.

The 1.4 resource limits are:

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

Callers that need later update or close semantics must supply their own stable identifier.

## Filtering metadata

The typed options expose:

```text
ApplicationName     -> f=<Base64 UTF-8>
NotificationTypes  -> repeated t=<Base64 UTF-8>
```

These fields may participate in terminal-side filtering policy. They are explicit caller data; `Icod.Terminal` does not inspect the process, executable, environment, or shell to populate them automatically.

## Activation focus policy

Kitty's default activation behavior includes focusing the originating terminal window.

`KittyNotificationOptions.FocusOnActivation` defaults to `true`. Setting it to `false` emits `a=-focus`.

Version 1.4 deliberately does **not** expose activation reporting. The `report` action creates unsolicited terminal input and therefore requires a separately reviewed `TerminalEvent` routing contract.

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

## Capability query

`QueryKittyNotificationSupportAsync(...)` sends a uniquely identified `p=?` query and correlates the reply by exact query identifier and payload type.

`KittyNotificationSupport` reports terminal-advertised support for focus action, activation reports, close-event reports, title/body/close/icon/alive/buttons payloads, automatic expiration, occasions, urgency levels, and sound names.

Unknown future support keys are ignored where safe. A timeout remains a timeout; silence is not converted into a permanent unsupported claim.

## Alive query

`QueryKittyAliveNotificationsAsync(...)` sends a uniquely identified `p=alive` query and returns a bounded list of notification identifiers reported as alive by the terminal.

The result is a live observation rather than library-owned state and can become stale immediately after receipt. Returned IDs are treated as untrusted terminal data and validated against protocol/resource bounds.

## Redirected output

Send/update/close operations require an interactive terminal output endpoint and reject known redirected output before committing a frame.

Support/alive queries require compatible interactive input and output endpoints through the existing query-availability contract.

## Security and privacy

OSC 99 can publish more metadata than OSC 9/777, including title/body, stable IDs, filtering metadata, urgency/expiry/sound policy, icon names, cache IDs, and caller-supplied image bytes.

Notification services, terminal logs, multiplexers, remote transports, notification histories, lock screens, or screen-sharing software may expose decoded data. Base64 is framing, not encryption.

`Icod.Terminal` does not automatically harvest process arguments, shell history, command output, environment variables, filesystem paths, or application state for notifications.

## Deliberately deferred interactive forms

Kitty OSC 99 also defines buttons and unsolicited activation/close reports. Version 1.4 deliberately does **not** expose those forms.

Those reports are application input, not query responses. Supporting them correctly requires a separate reviewed extension to the authoritative `TerminalEvent` routing path so they can coexist with ordinary input and active queries without stealing bytes or opening a competing reader.

The capability query can still report that a terminal advertises those features; this release simply does not request or surface their unsolicited events.

There is no generic raw OSC 99 selector/metadata API.

## Lifecycle

OSC 99 notifications are application metadata, not session-owned reversible terminal state.

`Icod.Terminal` therefore does not replay notification requests on resume, synthesize close requests on disposal, retain a hidden database of sent notification IDs, or infer host notification state from previous emission.

## Compatibility

Version 1.4 is additive. OSC 9 and OSC 777 notification APIs remain unchanged and independently explicit. Existing OSC 633/1337 and all other stable 1.x contracts retain their prior meaning.

The exact 1.4 API fingerprint is recorded in `Public-API-Baseline-1.4.md` / `.sha256`, while all earlier stable baseline files remain retained unchanged.
