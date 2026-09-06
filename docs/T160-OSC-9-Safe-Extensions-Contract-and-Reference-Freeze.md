# T160 — OSC 9 Safe Extensions Contract and Reference Freeze

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T160  
**Version:** `0.16.0-alpha.1`  
**Status:** Contract frozen; implementation deferred to T161+

## 1. Purpose

OSC 9 is not one coherent standard. It is a vendor-extension namespace containing overlapping and sometimes hazardous commands. `Icod.Terminal 0.16.0` therefore does not expose “OSC 9” generically. It exposes only semantic operations whose wire behavior is bounded, non-interactive, non-executing, and appropriate for a terminal library.

The existing OSC `9;4` progress contract from 0.10 remains unchanged.

## 2. Reference tiers

### Notification — approved

The legacy iTerm2 notification form is:

```text
OSC 9 ; message ST
```

Current documentation confirms this form in iTerm2. Kitty documents compatibility with the legacy OSC 9 notification protocol, and WezTerm documents OSC 9 as an iTerm2 system/toast notification.

This is therefore the approved notification form for 0.16.

Successful emission means only that the request was written. It does not prove a notification was shown; terminal configuration, focus state, desktop notification policy, multiplexers, or platform integration may suppress it.

### Current-directory compatibility — approved, explicitly secondary

ConEmu and Windows Terminal document:

```text
OSC 9 ; 9 ; cwd ST
```

as a current-working-directory hint. Windows Terminal specifically requires a Windows filesystem path for this compatibility form, including when emitted from WSL/Cygwin-style environments.

0.16 approves this form only as an explicitly named compatibility API. Existing OSC 7 `PublishCurrentLocationAsync(...)` remains the preferred/default semantic current-location protocol. The library never silently substitutes OSC 9;9 for OSC 7 and never emits both by default.

### Progress — retained unchanged

```text
OSC 9 ; 4 ; state ; value BEL
```

remains owned by the existing progress API/lease. 0.16 does not redesign or duplicate it.

## 3. Hazardous ConEmu OSC 9 commands — excluded

ConEmu documentation assigns additional OSC 9 subcommands to operations including:

- `9;1` — sleep/delay;
- `9;2` — GUI message box;
- `9;5` — wait for Enter/Space/Esc;
- `9;6` — execute GUI macro;
- `9;7` — run a process;
- `9;8` — output an environment-variable value;
- `9;10` — alter xterm keyboard/output emulation.

These are excluded from 0.16 because they can block execution, run code or macros, disclose process/environment state, or mutate emulator behavior.

ConEmu `9;3` tab-title mutation is also excluded because `Icod.Terminal` already has explicit OSC 0/1/2 title APIs. `9;12` prompt-start signaling is excluded because OSC 133 is already the semantic prompt contract. `9;11` comments are excluded because they add no useful public semantic operation.

There is no public arbitrary command-number API, raw OSC 9 payload API, or generic OSC writer.

## 4. Notification public contract

T162 will add:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);
```

### Null/empty

- `null` is rejected with `ArgumentNullException`;
- empty string is allowed and emits an empty notification payload;
- whitespace is not trimmed or normalized.

The library preserves caller text exactly at the Unicode-scalar level except for UTF-8 encoding.

### Unicode

Notification text must be well-formed UTF-16. It is encoded using strict UTF-8. Unpaired surrogates are rejected with `ArgumentException` before output commitment.

### Control policy

Legacy OSC 9 notification has no interoperable escaping layer. Percent encoding or Base64 would alter visible notification text in terminals that implement the legacy protocol.

Therefore 0.16 rejects control characters rather than transforming them.

The message must not contain:

- C0 controls U+0000 through U+001F;
- DEL U+007F;
- C1 controls U+0080 through U+009F.

This rejects BEL and ESC explicitly and prevents OSC/ST framing injection. Printable Unicode, including non-ASCII and non-BMP characters, remains valid.

No newline/tab exception exists in 0.16; notification text is treated as one bounded semantic text payload.

### Payload ceiling

Maximum notification OSC payload: **4,096 bytes**, measured after `ESC ]` and before final ST.

For notification this includes:

```text
9;<UTF-8 message>
```

The complete UTF-8 payload is validated/measured before commitment. Oversize input throws an argument-family exception and emits no bytes. The library never truncates notification text.

### Terminator

Canonical emission uses ST (`ESC \\`) rather than BEL so the BEL byte is never overloaded as both data hazard and terminator.

## 5. OSC 9;9 current-directory compatibility contract

T163 will add an explicitly named API:

```csharp
ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
	string windowsPath,
	CancellationToken cancellationToken = default
);
```

The name intentionally advertises that this is a Windows-Terminal/ConEmu compatibility mechanism, not the preferred portable location API.

### Relationship to OSC 7

- `PublishCurrentLocationAsync(...)` remains unchanged and preferred;
- OSC 9;9 is never emitted automatically by `PublishCurrentLocationAsync(...)`;
- the compatibility method emits only OSC 9;9;
- callers that intentionally need both protocols must call both APIs explicitly;
- no terminal-brand detection or `$WT_SESSION`/ConEmu environment inspection is performed.

### Path representation

The caller supplies a Windows filesystem path. `Icod.Terminal` does not call `wslpath`, `cygpath`, `Path.GetFullPath`, normalize separators, resolve links, inspect the process CWD, or translate Unix paths.

The path is emitted without added quotes or normalization:

```text
OSC 9;9;<windowsPath> ST
```

Spaces and ordinary printable punctuation are preserved.

### Validation

- `null` -> `ArgumentNullException`;
- empty string is rejected with `ArgumentException` because an empty CWD hint has no defined useful semantic;
- ill-formed UTF-16 -> `ArgumentException`;
- C0, DEL, and C1 controls are rejected before output;
- no path-existence check is performed;
- no operating-system restriction is imposed on the caller, because WSL/Cygwin/MSYS applications can legitimately emit a Windows path from a non-Windows process.

### Payload ceiling

Maximum OSC 9;9 payload: **32,768 bytes**, measured after `ESC ]` and before final ST, including the `9;9;` prefix.

This is intentionally larger than the notification limit because filesystem paths can be long, while still bounding allocation and terminal metadata size.

Oversize paths are rejected before output and are never truncated.

### Terminator

Canonical emission uses ST (`ESC \\`).

## 6. Output, cancellation, and lifecycle semantics

Both new operations use the existing `TerminalSession` output serialization domain.

Frozen semantics:

- validate/encode before taking or committing terminal output where practical;
- cancellation before output commitment emits nothing;
- after commitment, emit one complete frame with a non-cancellable transport write;
- no implicit flush;
- transport failure propagates without compensating output;
- later independent operations remain usable after a failed write;
- concurrent operations serialize as complete frames;
- no session-open automatic emission;
- no suspend/reset/restore behavior;
- no resume replay;
- no output from `InvalidateState()`;
- no synthetic notification/location emission during disposal.

Notification and OSC 9;9 metadata are ephemeral advisory output, not terminal state owned by a lifecycle lease.

## 7. Privacy/security contract

`SendNotificationAsync(...)` publishes exactly the caller-provided semantic text after validation/UTF-8 encoding. Notifications can expose sensitive information to the desktop notification service, lock screen, notification history, screen sharing, or other observers. The library does not redact secrets automatically.

`PublishWindowsCurrentDirectoryCompatibilityAsync(...)` can expose a filesystem path to terminal metadata/history. The library does not automatically discover or publish the process working directory.

Neither API reads process arguments, shell history, environment variables, terminal identity variables, or external files to construct its payload.

## 8. Explicitly excluded notification alternatives

0.16 does not add Kitty OSC 99, OSC 777 notification actions, activation reports, buttons, sound selection, urgency, notification IDs, close/update operations, or notification support queries.

Those protocols have materially different interaction/response models and would require their own contract rather than being hidden behind the legacy OSC 9 API.

0.16 also does not claim Windows Terminal supports legacy OSC 9 notifications. Windows Terminal documentation currently establishes OSC 9;9 CWD support; its notification evolution is separate and not a basis for broadening this tranche.

## 9. Testing obligations

T161–T165 must prove:

- byte-exact ST-terminated frames;
- empty notification behavior;
- ASCII, Unicode, and non-BMP payloads;
- malformed UTF-16 rejection;
- all C0/C1/DEL controls rejected with zero output;
- exact 4,096-byte notification boundary and one-byte-over rejection;
- exact 32,768-byte OSC 9;9 boundary and one-byte-over rejection;
- spaces/punctuation preserved in paths;
- OSC 7 remains byte-for-byte unchanged and independent;
- OSC 9;4 progress remains byte-for-byte unchanged;
- cancellation, failure, concurrency, lifecycle non-replay, and disposal non-synthesis;
- no public path to excluded hazardous subcommands.

## 10. Decision

T160 is frozen with two new semantic operations for 0.16:

1. legacy OSC 9 desktop notification;
2. explicitly named Windows-current-directory OSC 9;9 compatibility publication.

Existing OSC 9;4 progress remains unchanged. All other OSC 9 vendor subcommands are outside the 0.16 public surface.

Next: T161 — specialized bounded encoder/writer foundation for only these two approved forms.
