# T166 — DCurses Safe OSC 9 Downstream Acceptance

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T166  
**Version:** `0.16.0-alpha.7`  
**Status:** Implemented; exact-head validation pending

## Purpose

T166 extends the existing real `Icod.DCurses 0.1.0` semantic-prompt acceptance project rather than introducing a second downstream harness.

The same `TerminalSession` remains owned by the acceptance program and is passed into `CursesSession.OpenAsync(...)`. The test therefore proves that a higher-level full-screen consumer can coexist with the new safe OSC 9 public APIs without raw OSC construction or a side-channel writer.

## Existing acceptance retained

The portable OSC 133 sequence remains unchanged:

```text
A
[DCurses RefreshAsync payload]
B
[DCurses RefreshAsync payload]
C
[DCurses RefreshAsync payload]
D;0
A
[DCurses RefreshAsync payload]
B
D
```

The typed 0.15 OSC 133 extended sequence also remains unchanged:

```text
A;redraw=0;special_key=1;k=s;click_events=2
[DCurses RefreshAsync payload]
B
[DCurses RefreshAsync payload]
C;cmdline_url=printf%20caf%C3%A9%20%F0%9F%98%80
[DCurses RefreshAsync payload]
D;23
```

## T166 safe OSC 9 sequence

The existing acceptance project now appends:

```text
OSC 9;DCurses safe OSC 9 acceptance ST
[DCurses RefreshAsync payload]
OSC 9;9;C:\work\dcurses ST
[DCurses RefreshAsync payload]
OSC 9;DCurses safe OSC 9 acceptance complete ST
```

The frames are emitted only through:

```csharp
await terminalSession.SendNotificationAsync(...);
await terminalSession.PublishWindowsCurrentDirectoryCompatibilityAsync(...);
```

No internal encoder/writer is called by the acceptance program.

## Validation

The acceptance locates each expected frame in the deterministic `RecordingOutput` write list and requires one or more real `CursesSession.RefreshAsync()` writes between the OSC 9 boundaries.

This proves:

- notification and OSC 9;9 use the same session output serialization domain as DCurses;
- DCurses refresh traffic is neither overwritten nor bypassed;
- the new operations remain independent caller-driven metadata;
- no terminal emulator is required to actually display a desktop notification;
- no second output path or raw OSC builder is required downstream.

## Framework gate

The retained `packaging/VerifyDCursesSemanticPrompt.ps1` already runs the acceptance project on:

```text
net8.0
net9.0
net10.0
```

No new verifier was added.

## Gate

T166 is complete when the exact `0.16.0-alpha.7` PR head is green on Windows, Linux, and macOS, including the retained downstream/package gates.

Next: T167 — public API/package/documentation/stable closure.
