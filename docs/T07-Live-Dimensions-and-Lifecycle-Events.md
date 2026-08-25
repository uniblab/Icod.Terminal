# T07 — Live Dimensions and Lifecycle Events

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.7`
**Tranche:** T07 — Live dimensions and lifecycle events
**Reference branch:** `Icod.Terminal/initial_add`
**Reference date:** 2026-08-24
**Implementation status:** Implemented for Linux, macOS, Windows, injected lifecycle sources, and controlled unsupported hosts

---

## 1. Purpose

T07 makes terminal geometry and process/terminal lifecycle coordination reusable session services.

Before T07, `TerminalSession` could be opened, configured, invalidated, and restored, but applications still needed their own resize and process-signal plumbing. T07 moves those host mechanisms into `Icod.Terminal` while leaving application reaction policy to higher layers such as `Icod.DCurses`.

---

## 2. Public lifecycle contract

T07 adds:

```text
TerminalLifecycleEventKind
TerminalLifecycleEvent
TerminalSession.SupportsLifecycleEvents
TerminalSession.TerminationToken
TerminalSession.GetSize()
TerminalSession.ReadLifecycleEventAsync(...)
TerminalSessionOptions.ObserveLifecycleEvents
```

The normalized event kinds are:

- `Resize`;
- `Interrupt`;
- `Termination`;
- `Suspending`;
- `Resumed`.

Resize and resume events carry `Icod.TermInfo.TerminalSize` when live dimensions can be queried. No duplicate terminal-size value type is introduced.

`TerminationToken` is canceled after interrupt or termination requests and after fatal lifecycle-pump failures. The lifecycle event stream remains available to callers that need to distinguish the cause.

---

## 3. Live dimensions

`TerminalSession.GetSize()` queries the existing T03 control provider rather than duplicating platform geometry code.

Endpoint preference is:

1. output when it is an interactive endpoint advertising `LiveSize`;
2. input when output cannot provide dimensions and input advertises `LiveSize`;
3. a controlled unavailable result when neither observed endpoint advertises live dimensions.

This permits ordinary POSIX sessions to use either terminal descriptor while preserving the Windows rule that screen-buffer dimensions belong to console output rather than console input.

No environment `COLUMNS`/`LINES` fallback is performed by this low-level live-size method.

---

## 4. System lifecycle source

The internal `SystemTerminalLifecycleSource` owns operating-system callback registration.

On Linux and macOS it observes:

- `SIGWINCH` as resize;
- `SIGCONT` as resume;
- `SIGTSTP` as an intercepted suspension request;
- `SIGINT` as interrupt;
- `SIGTERM`, `SIGQUIT`, and `SIGHUP` as termination requests.

On Windows it observes `Console.CancelKeyPress` and distinguishes Control-C from Control-Break.

Native/runtime callbacks perform only bounded work: they set cancellation semantics as required and enqueue a compact signal record. Terminal state transitions, mode changes, output flushing, dimension queries, and event publication occur later on the managed lifecycle pump.

Resize publication is coalesced at the low-level source while a prior resize notification remains pending.

---

## 5. Automatic observation policy

`TerminalSessionOptions.ObserveLifecycleEvents` defaults to `true`.

Automatic process-wide lifecycle registration occurs only when the session uses `SystemTerminalControlProvider.Instance`. This preserves the ordinary live-session experience while ensuring injected or custom terminal providers do not unexpectedly install process-global signal handlers.

Tests use an internal injectable lifecycle source through the test assembly friend boundary. That injection mechanism is deliberately not public API.

Unsupported hosts continue to provide a usable `TerminalSession`, but `SupportsLifecycleEvents` is false and `ReadLifecycleEventAsync` reports lifecycle observation as unsupported.

---

## 6. Resize behavior

A resize signal wakes the managed lifecycle pump. The session re-queries current dimensions before publishing the public event.

When successive resize signals resolve to the same known dimensions, duplicate public resize events are suppressed. If dimensions are unavailable, the resize event is still meaningful and may carry no size.

This gives periodic full-screen applications a wake-up mechanism without polling terminal dimensions continuously.

---

## 7. Interrupt and termination behavior

The system source intercepts supported interrupt/termination callbacks long enough to hand them safely into managed session processing.

The session then:

1. cancels `TerminationToken`;
2. publishes either `Interrupt` or `Termination`;
3. leaves final application exit policy to the consumer.

A consumer such as `Icod.DCurses` can therefore restore higher-level presentation state and dispose the terminal session before exiting, without owning `PosixSignalRegistration` or `Console.CancelKeyPress` directly.

---

## 8. POSIX suspend/resume

A catchable `SIGTSTP` request is handled in two phases.

### 8.1 Suspension preparation

Before the process is allowed to stop, Terminal:

1. marks session state invalid;
2. flushes pending terminal output;
3. releases the reversible Windows-output lease if one exists;
4. restores the captured input-mode baseline;
5. publishes `Suspending`;
6. re-delivers the suspend request with its default host behavior.

The suspend source uses a one-shot pass-through marker so the re-delivered signal is not intercepted recursively.

### 8.2 Resume

After `SIGCONT`, Terminal:

1. marks prior session assumptions invalid;
2. re-establishes required output setup;
3. reapplies the requested semantic input mode from the captured baseline;
4. marks session state valid only after successful re-entry;
5. re-queries dimensions;
6. publishes `Resumed` with the current size when available.

A resume without a preceding catchable suspend is also treated as a state-disruption boundary. This covers external stop mechanisms such as uncatchable suspension where no preparation callback was possible.

---

## 9. Failure and rollback semantics

Suspension does not proceed when Terminal cannot prepare a reasonably restored host state.

If suspend completion itself fails, Terminal attempts to re-enter the active session state before surfacing the lifecycle failure.

If lifecycle re-entry fails after output setup or mode mutation has begun, Terminal attempts to roll back to the captured baseline and release any newly acquired output setup. Combined transition/rollback errors are retained as an aggregate exception.

A fatal lifecycle-pump error cancels `TerminationToken` and completes the lifecycle event stream with the error. Final `TerminalSession.DisposeAsync()` remains responsible for deterministic session cleanup.

---

## 10. Disposal ordering

Session disposal now stops lifecycle observation before final T05/T06 restoration.

The ordering is:

```text
stop lifecycle pump
        ↓
dispose host registrations
        ↓
flush output
        ↓
release output setup
        ↓
restore captured input baseline
```

This prevents a late resize/resume callback from re-entering session state while disposal is restoring the host terminal.

Repeated disposal remains idempotent through the existing T05 restoration task.

---

## 11. Responsibility boundary

T07 owns host lifecycle mechanism and session-owned state re-entry.

It does not yet own curses presentation behavior. In particular T07 does not enter or leave:

- alternate-screen mode;
- keypad/application mode;
- cursor visibility state;
- screen rendition state.

Those reversible presentation mechanisms remain T09 work. T10 will then remove the equivalent platform lifecycle plumbing from `Icod.DCurses` and map Terminal lifecycle events into curses policy.

---

## 12. Testing

The deterministic T07 suite uses an injected lifecycle source and control provider. It does not install process signal handlers and does not mutate the process terminal.

The tests cover:

- output-preferred live-size queries;
- input live-size fallback;
- resize wake-up;
- duplicate-dimension suppression;
- interrupt-to-termination-token integration;
- suspend baseline restoration;
- resume semantic-mode re-entry;
- dimension re-observation after resume;
- external/spurious resume re-entry;
- no automatic lifecycle registration for custom control providers.

The existing T03 platform provider tests remain authoritative for the native geometry and mode primitives beneath T07.

---

## 13. Deferred to later tranches

T07 does not implement:

- key-event decoding or Escape ambiguity handling — T08;
- alternate-screen/cursor/keypad leases — T09;
- DCurses migration — T10;
- ProcPs acceptance migration — T11.

T08 can now coordinate input waits with lifecycle wake-up without each application installing host callbacks of its own.
