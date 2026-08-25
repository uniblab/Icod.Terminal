# T05 — TerminalSession Lifecycle and Ownership

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.5`
**Tranche:** T05 — `TerminalSession` lifecycle and ownership
**Reference branch:** `Icod.Terminal/initial_add`
**Reference date:** 2026-08-24
**Implementation status:** Implemented; T06 terminal-description/output setup remains deferred

---

## 1. Purpose

T05 introduces `TerminalSession` as the owning live-terminal state abstraction.

T03 established native endpoint and mode control. T04 established platform-neutral canonical, cbreak, raw, and echo policy. T05 composes those layers into a reversible session lifecycle so higher-level consumers no longer need to own capture/apply/restore mechanics themselves.

The dependency boundary remains:

```text
Icod.TermInfo
      ^
      |
Icod.Terminal
      ^
      |
Icod.DCurses
```

`TerminalSession` does not depend on `Icod.CommandFramework`, `Icod.DCurses`, `Icod.ProcPs`, or `Icod.Pty`.

---

## 2. Public T05 surface

T05 introduces the following public types in namespace `Icod.Terminal`:

```csharp
ITerminalInput
ITerminalOutput
TerminalSessionOptions
TerminalSession
```

The byte-transport contracts are intentionally small:

```csharp
ValueTask<int> ITerminalInput.ReadAsync(
	Memory<byte> buffer,
	CancellationToken cancellationToken = default
);

ValueTask ITerminalOutput.WriteAsync(
	ReadOnlyMemory<byte> buffer,
	CancellationToken cancellationToken = default
);

ValueTask ITerminalOutput.FlushAsync(
	CancellationToken cancellationToken = default
);
```

They move the transport concepts identified by T02 out of DCurses without prematurely defining the T08 key-event model.

---

## 3. Session options

`TerminalSessionOptions` controls state entered during session initialization:

- `InputMode`, defaulting to `TerminalInputMode.CBreak`;
- `EchoInput`, defaulting to disabled;
- `RequireInteractiveOutput`, defaulting to `true`.

The input endpoint is always required to be an interactive terminal because T05 sessions capture and own an input-mode transition.

A caller may set `RequireInteractiveOutput` to `false` when deliberately combining interactive terminal input with redirected output. This permits direct terminal-aware tools without weakening the full-screen default used by DCurses/ProcPs consumers.

---

## 4. Standard and explicit opening

The standard overload binds to:

```text
input endpoint   -> file descriptor 0
output endpoint  -> file descriptor 1
input bytes      -> Console.OpenStandardInput()
output bytes     -> Console.OpenStandardOutput()
control provider -> SystemTerminalControlProvider.Instance
```

The explicit overload accepts:

- an `ITerminalControlProvider`;
- an input `TerminalEndpoint`;
- an output `TerminalEndpoint`;
- an `ITerminalInput`;
- an `ITerminalOutput`;
- session options;
- cancellation.

No replacement `TerminalBackend` ownership object is introduced. `TerminalSession` is the owning abstraction; explicit dependency injection is supplied directly to the open operation.

---

## 5. Ownership contract

A session owns only terminal **state transitions**.

It borrows:

- the control provider;
- input and output endpoint descriptors/paths;
- the input byte service;
- the output byte service;
- process-standard streams used by the standard overload.

Disposing a session therefore does not dispose caller-supplied transports or transfer ownership of process-standard handles.

When low-level providers temporarily open an explicit device path for an operation, the T03 provider remains responsible for that temporary native resource.

This distinction allows DCurses to delegate session state to Terminal without losing control of its higher-level presentation lifetime.

---

## 6. Initialization sequence

Session opening follows this order:

1. validate arguments, options, and initial cancellation;
2. observe the input endpoint;
3. require interactive terminal input;
4. observe the output endpoint;
5. enforce output interactivity when requested;
6. capture the complete input `TerminalModeSnapshot`;
7. mark restoration required immediately after capture;
8. apply the requested T04 semantic input mode;
9. observe cancellation again after mutation;
10. mark the configured state valid and return the session.

Restoration responsibility begins immediately after a baseline has been captured, before semantic application is attempted. This is deliberate: a provider failure can occur after partial native mutation, so a failed apply is treated as requiring rollback.

---

## 7. Deterministic rollback

Any exception after baseline capture causes session initialization to attempt restoration before the exception escapes.

This includes:

- a controlled `SetMode` failure;
- an exception thrown by the provider;
- cancellation observed after capture;
- cancellation observed after successful semantic mutation.

If initialization and restoration both fail, both errors are preserved in an `AggregateException`.

A failed open therefore does not silently discard restoration failures or leave the caller believing cleanup succeeded.

---

## 8. Disposal and output flushing

`TerminalSession` implements `IAsyncDisposable`.

T05 deliberately does not add synchronous `IDisposable`: terminal cleanup includes asynchronous output flushing, and the library should not introduce sync-over-async blocking merely to support a synchronous disposal surface.

`DisposeAsync`:

1. marks the applied state invalid;
2. flushes the borrowed output service with `CancellationToken.None`;
3. restores the captured baseline input mode;
4. aggregates flush/restoration failures when necessary.

Cleanup is not cancelable once it starts. Restoration is more important than honoring a cancellation request during teardown.

POSIX baseline restoration uses `AfterOutputDrained`. Windows console restoration uses `Immediately`, matching the native timing capabilities established in T03/T04.

---

## 9. Idempotency and concurrency

The first restoration request creates one cached restoration task.

All subsequent disposal requests await that same task. They do not:

- flush output again;
- reapply the baseline again;
- repeat a failed restoration attempt;
- mutate endpoint ownership.

This makes repeated or concurrent asynchronous disposal deterministic.

---

## 10. Explicit state invalidation

`TerminalSession.InvalidateState()` records that external activity may have changed the terminal after the session configured it.

`IsStateValid` becomes `false`, but T05 does not automatically reapply state.

This is an intentional seam for T07. Suspend/resume, resize/lifecycle disruption, and external console reconfiguration will later invalidate and coordinate session re-entry through lifecycle mechanisms rather than embedding signal handling in T05.

Disposal still restores the original captured baseline even after explicit invalidation.

---

## 11. Terminal-description boundary

T05 does not select or expose a `TerminalDescription`.

That is deliberate. The session now owns live endpoint/mode/transport state, while capability/profile selection remains a separate concern until T06.

T06 will bind an opened session to `Icod.TermInfo` terminal-description discovery and Windows VT-output setup without changing the T05 state-ownership contract.

No unknown `TERM` value is guessed or silently treated as xterm by T05.

---

## 12. Testing policy

T05 tests use injected providers and byte transports; they do not touch the process terminal.

The test matrix proves:

- successful POSIX open/apply/restore;
- Windows immediate restoration timing;
- required-output interactivity checks before mutation;
- explicitly permitted redirected output;
- rollback after controlled apply failure;
- rollback after provider exceptions;
- rollback after cancellation following successful mutation;
- preservation of both initialization and restoration failures;
- explicit invalidation;
- cancellation before any native operation;
- option validation before any native operation;
- one output flush and one baseline restoration despite repeated disposal.

These fault-injection cases satisfy Gate T05 without requiring an interactive CI runner.

---

## 13. Deferred work

T05 intentionally does not implement:

- terminal-description/profile resolution;
- Windows virtual-terminal output leases;
- application text encoding policy;
- resize notifications;
- POSIX suspend/resume handling;
- Windows cancellation events;
- key/input-event decoding;
- alternate-screen, cursor, or keypad leases;
- PTY/ConPTY hosting.

Those remain T06 through T09 and the separate future `Icod.Pty` package.
