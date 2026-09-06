# Icod.Terminal 0.16 Public API Baseline

**Release:** `0.16.0`  
**Theme:** bounded safe OSC 9 semantic extensions without exposing hazardous vendor control

---

## 1. Public delta

0.16 adds exactly two public `TerminalSession` methods:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);

ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
	string windowsPath,
	CancellationToken cancellationToken = default
);
```

No new public enum, options type, generic OSC command type, or raw OSC builder is introduced.

All public contracts from 0.15 and earlier remain unchanged.

---

## 2. Legacy OSC 9 desktop notification

`SendNotificationAsync(...)` emits:

```text
OSC 9;<message> ST
```

This is the legacy iTerm2 notification form used as a compatibility protocol by multiple terminals.

Successful completion means the complete request was written to the terminal output. It does not prove a desktop notification was displayed; terminal configuration, focus state, operating-system notification policy, multiplexers, or user preferences can suppress it.

### Validation

- `message == null` -> `ArgumentNullException`;
- empty string is valid;
- whitespace is preserved;
- input must be well-formed UTF-16;
- output is strict UTF-8;
- C0 controls U+0000–U+001F are rejected;
- DEL U+007F is rejected;
- C1 controls U+0080–U+009F are rejected;
- printable Unicode, including non-BMP characters, is preserved;
- no trimming, newline normalization, escaping, percent encoding, Base64 encoding, or secret redaction is performed.

The control rejection prevents BEL/ESC/ST framing injection without changing visible printable text.

### Bound

Maximum OSC payload is **4,096 bytes**, measured after `ESC ]` and before the final ST terminator, including the `9;` prefix.

Oversize input is rejected before output commitment and is never truncated.

---

## 3. OSC 9;9 Windows current-directory compatibility

`PublishWindowsCurrentDirectoryCompatibilityAsync(...)` emits:

```text
OSC 9;9;<windowsPath> ST
```

This is an explicitly named Windows Terminal/ConEmu compatibility operation.

It is not the preferred portable location API. Existing OSC 7:

```csharp
ValueTask PublishCurrentLocationAsync(
	string path,
	TerminalLocationPathStyle pathStyle,
	string? authority = null,
	CancellationToken cancellationToken = default
);
```

remains the preferred/default semantic current-location contract.

`Icod.Terminal` never silently substitutes OSC 9;9 for OSC 7 and never emits both protocols automatically. Applications that intentionally need both call both methods explicitly.

### Path contract

The caller supplies a Windows filesystem path.

The library does not:

- call `wslpath`, `cygpath`, or equivalent converters;
- normalize separators;
- call `Path.GetFullPath`;
- inspect the process current directory;
- perform filesystem existence checks;
- resolve links/reparse points;
- infer a path from shell state;
- inspect `$WT_SESSION`, ConEmu variables, or other terminal-brand environment variables;
- restrict the API to a Windows process, because WSL/Cygwin/MSYS-style applications may legitimately publish Windows paths.

Spaces and printable punctuation are preserved exactly at the Unicode-scalar level.

### Validation

- `windowsPath == null` -> `ArgumentNullException`;
- empty string -> `ArgumentException`;
- malformed UTF-16 -> `ArgumentException`;
- C0, DEL, and C1 controls -> `ArgumentException`;
- no path-existence or path-root validation is performed.

### Bound

Maximum OSC payload is **32,768 bytes**, including the `9;9;` prefix and excluding the OSC introducer/ST terminator.

Oversize input is rejected before output commitment and is never truncated.

---

## 4. Framing

The new text-bearing OSC 9 operations use canonical ST termination:

```text
ESC \\
```

The existing OSC 9;4 progress implementation from 0.10 remains byte-for-byte unchanged and retains its canonical BEL termination.

The two framing families coexist independently; 0.16 does not alter the existing progress writer.

---

## 5. Output ordering and cancellation

Both new operations use the established `TerminalSession` output serialization domain.

Frozen semantics:

- validation/encoding occurs before output commitment where practical;
- cancellation before commitment emits nothing;
- after commitment, the complete frame is written in one transport write using `CancellationToken.None`;
- no implicit flush;
- concurrent operations serialize as whole frames;
- committed transport failure propagates without compensating traffic;
- failure does not poison later independent operations;
- active terminal-query routing remains independent.

---

## 6. Lifecycle semantics

Notification and OSC 9;9 metadata are ephemeral advisory output, not owned/restorable terminal state.

0.16 adds no:

- automatic session-open notification or CWD publication;
- background OSC 9 support probe or cache;
- lifecycle lease;
- suspend-time reset;
- resume replay;
- `InvalidateState()` output;
- disposal synthesis;
- automatic retry after failure.

Explicit calls remain available after ordinary lifecycle transitions when the session is live.

---

## 7. Privacy and disclosure

Notification text can be exposed through desktop notification services, notification history, lock screens, screen sharing, remote/multiplexed sessions, or terminal logs.

OSC 9;9 can expose filesystem paths through terminal metadata/history.

The library does not automatically discover, redact, classify, or suppress sensitive data. Publication is explicit caller intent.

Applications should avoid publishing credentials, bearer tokens, private paths, customer data, or other sensitive values unless that disclosure is appropriate.

---

## 8. Hazardous and duplicate OSC 9 commands remain excluded

0.16 intentionally does not expose ConEmu-family commands for:

```text
9;1   sleep/delay
9;2   GUI message box
9;5   wait for key
9;6   GUI macro execution
9;7   process launch
9;8   environment-variable disclosure
9;10  xterm/emulation mutation
```

Also excluded:

- `9;3` tab-title mutation — existing OSC 0/1/2 APIs own title semantics;
- `9;11` comments — no useful semantic API;
- `9;12` prompt signaling — OSC 133 owns prompt semantics;
- arbitrary OSC 9 command numbers or payloads;
- public `WriteOsc9Async(...)` or generic OSC builders;
- Kitty OSC 99;
- OSC 777 notification actions;
- notification IDs, updates, closes, buttons, sounds, urgency, activation reports, or notification support queries.

These exclusions are part of the safety contract, not missing convenience APIs.

---

## 9. Retained downstream acceptance

The real `Icod.DCurses 0.1.0` acceptance path runs on `net8.0`, `net9.0`, and `net10.0` and proves:

```text
OSC 9 notification
[real CursesSession.RefreshAsync output]
OSC 9;9 Windows CWD compatibility
[real CursesSession.RefreshAsync output]
OSC 9 notification
```

using only public `TerminalSession` APIs on the same session used by `CursesSession`.

No raw OSC shortcut, internal writer, or side-channel transport is used.

---

## 10. Compatibility

The stable package continues to target:

```text
net8.0
net9.0
net10.0
```

All three remain first-class supported targets. Vendor end-of-support alone does not remove net8.0 or net9.0; removal requires a concrete security or security-maintenance reason.

This document freezes the complete stable 0.16 public delta.
