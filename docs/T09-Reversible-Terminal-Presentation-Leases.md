# T09 — Reversible Terminal Presentation Leases

## Status

T09 implements the reversible presentation-state mechanisms required before
`Icod.DCurses` can move its terminal ownership down into `Icod.Terminal`.

Development version: `0.1.0-alpha.9`.

## Scope

T09 owns three low-level terminal presentation transitions:

- alternate/full-screen cursor-addressing mode;
- keypad/application transmit mode;
- physical cursor visibility.

These mechanisms are capability-driven. They do not decide that an application
*should* use a full screen, hide the cursor, or enable keypad mode. That policy
remains with higher layers such as `Icod.DCurses`.

T09 does not add virtual cells, screen diffing, rendition policy, mouse input,
terminal titles, or other higher-level presentation behavior.

## Public contract

The public presentation contract consists of:

```text
TerminalCursorVisibility
TerminalPresentationOptions
TerminalPresentationLease
TerminalSession.AcquirePresentationAsync(...)
```

A request may contain any combination of:

```text
AlternateScreen = true
KeypadMode = true
CursorVisibility = Hidden | Normal | VeryVisible
```

At least one presentation state must be requested.

Acquisition returns a `TerminalControlResult<TerminalPresentationLease>`.
A missing required TermInfo capability is therefore a controlled unavailable
result rather than an assumed ANSI/xterm escape sequence.

I/O failures and caller cancellation remain exceptions because they occur while
performing an otherwise supported asynchronous state transition.

## TermInfo remains authoritative

T09 obtains every presentation string from the `TerminalDescription` already
selected by the live `TerminalSession`.

The mappings are:

| Mechanism | Enter/request | Restore |
| --- | --- | --- |
| alternate screen | `EnterCursorAddressingMode` | `ExitCursorAddressingMode` |
| keypad/application mode | `EnterKeypadMode` | `ExitKeypadMode` |
| hidden cursor | `CursorInvisible` | normal cursor capability |
| normal cursor | `CursorNormal`, then `CursorVeryVisible` fallback | normal cursor capability |
| very-visible cursor | `CursorVeryVisible`, then `CursorNormal` fallback | normal cursor capability |

The normal cursor restoration capability is `CursorNormal` when available,
falling back to `CursorVeryVisible` only when the profile lacks a normal form.

Alternate-screen and keypad acquisition require both enter and leave
capabilities before any output is emitted. A cursor request requires both a
usable requested cursor capability and a usable restoration capability.

T09 never substitutes hard-coded ANSI or xterm strings for missing data.

## Nested ownership

Alternate-screen and keypad state use first-owner/last-owner semantics.

For example:

```text
outer acquires alternate screen  -> emit enter
inner acquires alternate screen  -> no output
outer releases                   -> no output
inner releases                   -> emit exit
```

This remains correct when leases are released out of acquisition order. An
inner or earlier owner cannot restore a state that is still required by another
active lease.

Cursor ownership is ordered rather than reference-counted because different
owners may request different presentations. The most recently acquired active
cursor request wins.

For example:

```text
outer requests Hidden     -> CursorInvisible
inner requests Normal     -> CursorNormal
inner releases            -> CursorInvisible
outer releases            -> CursorNormal
```

If a structural transition such as entering or leaving alternate-screen mode
occurs while a cursor lease remains active, T09 reapplies the effective cursor
request after the structural transition. This avoids assuming that a terminal
preserves cursor presentation across full-screen state changes.

## Transition ordering

A grouped acquisition enters state in this order:

```text
alternate screen
keypad/application mode
cursor presentation
flush
```

Returning to the baseline uses the reverse presentation order:

```text
normal cursor
leave keypad/application mode
leave alternate screen
flush
```

This ordering matches the restoration discipline already used by the DCurses
prototype while moving ownership to the correct layer.

A transition that changes structural state while retaining an active cursor
request reapplies that cursor request after the structural changes.

## Transactional acquisition and release

Grouped acquisition is transactional.

Before writing output, T09 verifies that all capabilities needed by the request
and its eventual restoration are available. After that preflight, successful
steps are tracked as the transition proceeds.

If a later write, flush, or cancellation fails, T09 attempts to transition from
the successfully reached state back to the state that existed before the
operation. Rollback ignores the caller cancellation token so cleanup is not
abandoned merely because the initiating wait was canceled.

If both the requested transition and rollback fail, an `AggregateException`
preserves both failures.

Lease release uses the same transactional discipline. If a release transition
fails but rollback succeeds, the lease remains active and may be disposed again
or ultimately cleaned up by session disposal.

Transport writes can fail after an operating system or stream has accepted only
part of a byte sequence. No managed abstraction can know exactly how many bytes
such a transport consumed. T09 therefore provides best-effort rollback from the
last transition step that completed successfully.

## Session ownership and disposal

A presentation lease owns a request; the `TerminalSession` remains the final
owner of host terminal cleanup.

Consequently, session disposal does not require every lease owner to run first.
The disposal sequence is now:

```text
stop lifecycle observation
restore all active presentation state
flush / release output setup
restore captured terminal mode
```

The presentation manager marks outstanding leases released when the session
closes. Disposing one of those lease objects afterward is a no-op.

Presentation cleanup is best effort during session shutdown. Cursor, keypad,
and alternate-screen restoration are each attempted even if an earlier cleanup
step fails. Any resulting error is combined with the existing session
restoration error path rather than preventing native mode restoration.

## Suspend and resume

T09 integrates with the T07 lifecycle contract.

Before a caught POSIX suspend is re-delivered, active presentation state is
temporarily restored to the terminal baseline:

```text
restore cursor
leave keypad mode
leave alternate screen
flush
restore/release T07 host state
suspend
```

The lease set is retained while the process is suspended.

After resume, T07 first reacquires output setup and reapplies the requested input
mode. T09 then re-enters the presentation state still required by active leases:

```text
enter alternate screen
enter keypad mode
reapply effective cursor request
flush
publish Resumed
```

An external resume notification invalidates the previous presentation-state
assumption before re-entry. The session therefore does not assume that a
terminal preserved full-screen, keypad, or cursor state while the process was
stopped or otherwise disrupted.

Calling `TerminalSession.InvalidateState()` also invalidates the presentation
manager's knowledge. The next lifecycle re-entry or presentation transition
will re-establish state from the active lease set rather than trusting stale
physical-state assumptions.

## Output semantics

Presentation strings continue through the T06 terminal-protocol output path:

- TermInfo byte semantics remain Latin-1/reversible;
- TermInfo padding behavior remains centralized in `Icod.TermInfo`;
- application text encoding is unrelated;
- meaningful state transition batches are flushed before ownership changes are
  reported complete.

## Testing

T09 unit tests use injected terminal descriptions, terminal-control providers,
byte output, and lifecycle sources. They do not mutate the developer or CI
terminal.

The deterministic suite covers:

- compound enter/leave ordering;
- first-owner/last-owner alternate-screen behavior;
- out-of-order disposal;
- nested cursor override and restoration;
- missing-capability controlled results;
- rollback after a mid-acquisition output failure;
- manager usability after successful rollback;
- session disposal with an undisposed active lease;
- no-op late lease disposal after session cleanup;
- suspend/resume presentation leave and re-entry;
- invalid presentation options.

## Deferred work

T09 deliberately does not move DCurses itself. T10 performs that coordinated
migration and removes the now-duplicated terminal mechanics from the curses
layer.

Also deferred are richer post-0.1 presentation protocols such as titles,
hyperlinks, synchronized output, cursor-style operations, and security-sensitive
OSC facilities.

## Gate

T09 is complete when tests prove capability-driven enter/leave ordering,
nested/repeated ownership, out-of-order release, rollback after partial
acquisition, deterministic session cleanup, and suspend/resume re-entry without
requiring `Icod.DCurses` or native terminal APIs.
