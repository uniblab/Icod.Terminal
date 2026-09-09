# Kitty OSC 99 Desktop Notifications

This document is the permanent 1.x contract for the typed Kitty OSC 99 notification surface introduced by `Icod.Terminal 1.4.0`.

OSC 99 is a distinct notification protocol. It does not replace the legacy OSC 9 `SendNotificationAsync(...)` API or the urxvt-style OSC 777 `SendTitledNotificationAsync(...)` API, and `Icod.Terminal` does not automatically choose between them from terminal branding.

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

Explicit observation is available through:

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

The two semicolons are always emitted, including when the payload is empty.

Outbound requests use canonical seven-bit OSC introduction and ST termination:

```text
ESC ] ... ESC \
```

The response parser accepts the existing bounded OSC response framing supported by `Icod.Terminal`, including BEL/ST seven-bit forms and the recognized eight-bit OSC/ST form.

## Text encoding and chunking

Title/body payloads are encoded as:

1. strict UTF-8;
2. RFC 4648 Base64;
3. Kitty `e=1` payload data.

Using Base64 means arbitrary well-formed Unicode—including text that contains terminal control code points—is transported as data rather than raw OSC framing. This is different from OSC 9 and OSC 777, whose directly embedded text is subject to stricter control-character exclusions.

The 1.4 resource limits are:

```text
title UTF-8 bytes             <= 65,536
body UTF-8 bytes              <= 65,536
one encoded payload chunk     <= 4,096 bytes
one metadata text value       <= 4,096 UTF-8 bytes
notification-type entries     <= 16
icon-name entries             <= 16
transmitted icon bytes        <= 262,144
one encoded OSC frame         <= 16,384 bytes
notification identifier       <= 128 ASCII characters
alive-query returned IDs      <= 256
support/alive response payload <= 4,096 bytes
```

Large title/body/icon payloads are split into multiple OSC 99 frames. `d=0` marks an incomplete request and `d=1` marks the final emitted part. Every payload-bearing frame uses `e=1`.

All frames for one `SendKittyNotificationAsync(...)` call are completely encoded and validated before output commitment. After the shared session output gate is acquired, the complete frame sequence is written without caller cancellation splitting the protocol transaction. No implicit flush is performed for notification emission.

## Notification identity and updates

`KittyNotificationOptions.Identifier` supplies an explicit stable notification identity.

Identifiers are restricted to:

```text
A-Z a-z 0-9 _ - + .
```

and are bounded to 128 characters. The legacy special identifier `0` is rejected.

Reusing an identifier requests update/replacement semantics from the terminal. `Icod.Terminal` does not maintain its own persistent notification database and does not claim that replacement occurred; successful completion proves only frame emission.

A multi-frame request requires an identifier so the terminal can associate the chunks. When a caller does not provide one, `Icod.Terminal` generates an internal safe identifier solely for that transmission. That generated identifier is intentionally not exposed as application state and is not suitable for later caller-directed update/close operations.

Callers that need later update or close semantics must supply their own stable identifier.

## Filtering metadata

The typed options expose:

```text
ApplicationName     -> f=<Base64 UTF-8>
NotificationTypes  -> repeated t=<Base64 UTF-8>
```

These fields may participate in terminal-side filtering policy. They are caller-supplied metadata; `Icod.Terminal` does not inspect the current process, executable name, environment, or shell to populate them automatically.

## Activation focus policy

Kitty's default notification activation behavior includes focusing the originating terminal window.

`KittyNotificationOptions.FocusOnActivation` defaults to `true`. Setting it to `false` emits the protocol's `-focus` action suppression.

Version 1.4 deliberately does **not** expose activation reporting. The `report` action creates unsolicited terminal input, which requires a separate reviewed `TerminalEvent` routing contract.

## Occasion

`KittyNotificationOccasion` is a closed semantic enum:

```text
Always      -> always
Unfocused   -> unfocused
Invisible   -> invisible
```

`Always` is the default and is omitted from outbound metadata when no override is needed.

These values request terminal-side policy. Successful emission does not prove the terminal honored the requested occasion.

## Urgency

`KittyNotificationUrgency` maps to the protocol's numeric urgency field:

```text
Low       -> 0
Normal    -> 1
Critical  -> 2
```

Urgency remains optional. The terminal and host desktop determine how it affects presentation.

## Expiration

`KittyNotificationOptions.Expiration` uses these semantics:

```text
null                  terminal/OS default policy
TimeSpan.Zero         do not expire automatically
positive TimeSpan     requested automatic close interval in milliseconds
```

Positive intervals are rounded upward to the next whole millisecond and bounded to `Int32.MaxValue` milliseconds.

Expiration is a request to the terminal/desktop notification service, not a local timer owned by `Icod.Terminal`.

## Sound

`KittyNotificationOptions.SoundName` is strict UTF-8 transported using Base64 metadata.

Common Kitty-defined names include:

```text
system
silent
error
warn
warning
info
question
```

The API does not restrict callers to that current list because terminals may report additional supported sound names through the capability query. Support must not be inferred from the string alone.

## Icons

`IconNames` contains ordered icon names, each transported as Base64 UTF-8. The terminal may select the first icon it can resolve locally.

`IconData` supports bounded transmission of encoded image bytes. Version 1.4 accepts only data with PNG, JPEG, or GIF signatures and limits the source image to 262,144 bytes.

Transmitted icon data requires `IconDataIdentifier`. The same identifier can later be supplied without `IconData` to reference terminal-cached icon data.

The icon-data identifier uses the same restricted grammar and bound as notification identifiers.

`Icod.Terminal` does not decode image pixels, fetch image URLs, inspect files, or read icon data from disk automatically.

## Close requests

`CloseKittyNotificationAsync(identifier)` emits the typed close form:

```text
OSC 99 ; i=<identifier>:p=close ; ST
```

This operation requires an interactive terminal output endpoint and participates in the shared session output-ordering domain.

Successful completion proves emission only. The library does not claim that a host desktop notification was actually closed.

## Capability query

`QueryKittyNotificationSupportAsync(...)` sends an explicit uniquely identified `p=?` query and correlates the terminal response by both query identifier and payload type.

`KittyNotificationSupport` reports the terminal-advertised support for:

- focus activation action;
- activation reports;
- close-event reports;
- title payloads;
- body payloads;
- explicit close;
- transmitted icon data;
- alive queries;
- buttons;
- automatic expiration;
- advertised occasions;
- advertised urgencies;
- advertised sounds.

Unknown future support keys/values are ignored where safe so a newer terminal does not make the current parser unusable merely by advertising an extension.

A timeout remains a timeout. `Icod.Terminal` does not convert failure to receive an OSC 99 response into a permanent `Unsupported` claim solely from terminal identity or silence.

## Alive query

`QueryKittyAliveNotificationsAsync(...)` sends an explicit uniquely identified `p=alive` query and returns the bounded list of notification IDs reported as alive by the terminal.

The result is a live observation, not library-owned notification state. It can become stale immediately after the response.

Returned identifiers are validated against the OSC 99 identifier grammar and list/resource bounds.

## Redirected output

Send/update/close operations require an interactive terminal output endpoint and reject known redirected output before committing a frame.

Active support/alive queries require compatible interactive input and output endpoints through the existing query-availability contract.

## Security and privacy

OSC 99 can publish more metadata than the older OSC 9/777 notification APIs:

- title/body contents;
- stable notification identifiers;
- application name;
- notification types;
- urgency and expiration policy;
- sound name;
- icon names;
- arbitrary caller-supplied icon bytes.

Base64 protects protocol framing. It provides **no confidentiality**. The terminal, multiplexer, remote transport, host notification service, notification history, lock screen, screen sharing, or logs may expose the decoded content.

Callers are responsible for deciding whether notification metadata and icon content are safe to disclose.

The library does not automatically inspect process arguments, command output, shell history, environment variables, filesystem paths, or application state to build notifications.

## Deliberately deferred OSC 99 forms

Version 1.4 does not expose:

- arbitrary/raw OSC 99 metadata construction;
- buttons;
- activation-event reports;
- close-event reports;
- a protocol-specific raw input reader;
- automatic notification-protocol negotiation/fallback;
- host-native desktop notification APIs.

Buttons and activation/close reports produce unsolicited OSC 99 traffic that is semantically application input. Correct support requires extending the authoritative `TerminalEvent` path so those reports can coexist with ordinary terminal input and active queries without stealing bytes or opening a competing reader.

The capability object may report whether the terminal advertises these deferred features, but 1.4 does not expose operations that would request/report them.

## Relationship to OSC 9 and OSC 777

The three notification APIs remain explicit and independent:

```text
SendNotificationAsync(...)       -> legacy OSC 9
SendTitledNotificationAsync(...) -> urxvt-style OSC 777
SendKittyNotificationAsync(...)  -> Kitty OSC 99
```

No method silently emits another protocol, performs brand detection, or invokes a host-native fallback.
