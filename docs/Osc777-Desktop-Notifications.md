# OSC 777 Titled Desktop Notifications

This document defines the `Icod.Terminal 1.2` contract for urxvt-style OSC 777 desktop notifications.

OSC 777 is a terminal-emulator extension used by multiple modern terminal implementations for desktop notifications with separate title and body fields. It is distinct from the existing legacy OSC 9 notification API and from richer notification protocols such as Kitty OSC 99.

## Supported wire form

`Icod.Terminal 1.2` emits exactly:

```text
OSC 777 ; notify ; title ; message ST
```

with canonical ST termination:

```text
ESC ] 777 ; notify ; title ; message ESC \
```

The literal command field is always `notify`.

## Public API

The semantic public surface is:

```csharp
ValueTask SendTitledNotificationAsync(
	string title,
	string message,
	CancellationToken cancellationToken = default
);
```

The existing method remains unchanged:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);
```

`SendNotificationAsync(...)` remains the legacy OSC 9 notification path. `SendTitledNotificationAsync(...)` is the OSC 777 titled-notification path. `Icod.Terminal` does not silently switch between them or infer which one a terminal supports.

## Field grammar

OSC 777 uses semicolon-delimited title and message fields but does not define a broadly interoperable escaping mechanism for embedded semicolons.

To preserve exact semantic fields and prevent ambiguous framing, `Icod.Terminal` rejects semicolons in both `title` and `message` rather than replacing, truncating, or heuristically escaping them.

Both fields:

- may be empty;
- must contain well-formed Unicode;
- are encoded using strict UTF-8;
- must not contain C0, DEL, or C1 controls;
- are validated completely before output commitment.

The library does not silently sanitize caller text.

## Bounds

The complete OSC payload between `ESC ]` and ST is bounded to 4,096 encoded bytes, including:

```text
777;notify;
<title>
;
<message>
```

The bound is measured after strict UTF-8 encoding, so multibyte Unicode consumes its actual encoded byte length.

Oversized input is rejected before waiting for the session output gate.

## Output and cancellation

OSC 777 uses the normal `TerminalSession` output serialization domain.

Each call:

1. validates `title` and `message`;
2. encodes the complete frame;
3. observes caller cancellation;
4. waits for the shared session output gate;
5. observes cancellation again immediately before commit;
6. writes the complete frame with `CancellationToken.None`;
7. does not implicitly flush.

This prevents caller cancellation from splitting a committed OSC frame.

## Endpoint and support posture

OSC 777 notification emission requires an interactive terminal output endpoint. Known redirected/non-terminal output is rejected.

Successful completion proves only that the complete frame was written. It does not prove that the terminal:

- implements OSC 777;
- accepted the `notify` command;
- displayed a desktop notification;
- displayed the supplied title/body exactly;
- bypassed focus-based notification suppression or user policy.

`Icod.Terminal` does not infer OSC 777 support from `TERM`, `TERM_PROGRAM`, host OS, emulator name, environment variables, or version strings.

## Lifecycle

OSC 777 notification requests are ephemeral output metadata.

They add:

- no lifecycle participant;
- no restoration lease;
- no replay after resume;
- no synthetic notification during disposal;
- no persistent notification identity or update state.

The application remains the authority for when a notification should be sent.

## Security and privacy

Desktop notification titles and messages may leave the terminal window and appear in desktop notification services, notification history, lock screens, screen sharing, remote-session logs, or other OS/UI surfaces.

The library therefore sends OSC 777 only when explicitly requested by the caller. It does not:

- inspect application state to invent notifications;
- read shell history or process output automatically;
- redact secrets heuristically;
- execute `notify-send`, AppleScript, PowerShell, or another host notification command;
- perform network or IPC activity beyond writing the terminal frame.

Validation protects the OSC framing. It does not make notification content confidential or trustworthy.

## Relationship to OSC 9

The existing OSC 9 notification API remains available for terminals/applications that choose that compatibility path:

```text
SendNotificationAsync(message)
    -> OSC 9 ; message ST
```

OSC 777 adds a separate title field:

```text
SendTitledNotificationAsync(title, message)
    -> OSC 777 ; notify ; title ; message ST
```

Neither method calls the other and no automatic fallback is performed.

## Deliberately excluded surface

`Icod.Terminal 1.2` does not expose:

- a generic `WriteOsc777Async(...)` API;
- arbitrary OSC 777 command names;
- raw field arrays;
- automatic terminal-brand routing between OSC 9, OSC 777, or OSC 99;
- host-native notification fallbacks;
- notification actions, IDs, replacement semantics, activation callbacks, urgency, icons, or timeouts.

Richer notification semantics require a separately reviewed protocol/API contract rather than expanding OSC 777 into a generic vendor-command escape hatch.
