# T12B — Public API and Consumer Contract Audit

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.12`
**Tranche:** T12B — public API and documentation audit
**Status:** Complete

---

## 1. Purpose

T12B performs the intentional pre-release review required before `Icod.Terminal 0.1.0` is treated as the first reusable public live-terminal contract.

The review is informed by actual consumers rather than by an isolated API design exercise. `Icod.DCurses`, `watch`, `slabtop`, and `top` have already exercised the session, mode, input, lifecycle, presentation, timing, and restoration surfaces established by T03-T11.

The T12B questions are therefore:

1. which public types are the ordinary application contract;
2. which low-level types are deliberately public for advanced/custom-provider and `stty`-class use;
3. which implementation details must remain internal;
4. whether any public name or ownership rule has enough regret to justify a breaking change before `0.1.0`;
5. whether an independent consumer can understand the important cross-platform behavior without reading platform interop source.

The reviewed surface is recorded in [`Public-API-Baseline-0.1.md`](Public-API-Baseline-0.1.md).

---

## 2. Audit result

**No breaking public-API revision is required before `0.1.0`.**

The current layering survived the DCurses/ProcPs acceptance work without forcing higher layers to reacquire native terminal responsibilities. The public surface is larger than a minimal `TerminalSession` facade, but the additional low-level surface is intentional: it provides complete terminal observation/mode snapshots, semantic mode transformation, serialization, and injected control/transport contracts needed by diagnostic and advanced consumers.

This decision is not a `1.0` compatibility freeze. The package remains pre-1.0, and later milestones may add APIs or make justified corrections. T12B does establish that there is no known public-surface defect serious enough to deliberately break the `0.1.0` contract before release.

---

## 3. Public surface classification

### 3.1 Primary application contract

Ordinary terminal-aware applications should begin with:

- `TerminalSession`;
- `TerminalSessionOptions`;
- `TerminalEvent` / `TerminalInputEvent`;
- `TerminalInputMode`;
- `TerminalPresentationOptions` / `TerminalPresentationLease`;
- `TerminalLifecycleEvent`;
- `TerminalIdentity`.

This is the preferred layer for applications, TUIs, monitors, editors, pagers, and REPLs. It owns the live state transition and composes the lower-level mechanisms.

### 3.2 Intentional low-level contract

The following public types are retained intentionally rather than hidden as implementation detail:

- `TerminalEndpoint` and endpoint observations;
- `ITerminalControlProvider` and `SystemTerminalControlProvider`;
- `TerminalControlResult<T>` and `TerminalControlMutationResult`;
- `TerminalModeSnapshot`, `TerminalSpeed`, and `TerminalModeApplyTiming`;
- `TerminalInputModePolicy`;
- `TerminalModeCodec`;
- `ITerminalInput` and `ITerminalOutput`.

These APIs serve injected/test backends, caller-owned endpoints, diagnostic tooling, complete mode round-tripping, and command implementations which legitimately need lower-level terminal facts. They also keep native manipulation out of ordinary application code while allowing consumers that truly need the information to inspect it in a controlled representation.

The low-level surface does not make native platform providers public. Linux/macOS `termios` interop, Windows console interop, the incremental decoder, lifecycle signal source, output adapter, and presentation manager remain internal.

### 3.3 Dependency-owned types in the contract

`Icod.Terminal` deliberately reuses public value/service types from its foundational dependencies rather than cloning them:

- `Icod.TermInfo.TerminalDescription`;
- `Icod.TermInfo.TerminalDatabase`;
- `Icod.TermInfo.TerminalSize`;
- `Icod.TermInfo.StringCapability`;
- `Icod.TermInfo.PaddingMode`;
- `Icod.TermInfo.ITermInfoDelayProvider`;
- `Icod.Timing.IMonotonicClock`.

The `0.1.x` line therefore has runtime dependencies on `Icod.TermInfo 1.0.0` and `Icod.Timing 1.0.0`. `Icod.DCurses`, `Icod.ProcPs`, and a future `Icod.Pty` are not runtime dependencies of `Icod.Terminal`.

---

## 4. Session ownership and restoration contract

A `TerminalSession` owns **terminal state transitions**, not the lifetime of the endpoints or byte transports it is given.

For the standard-session overload, process standard input/output are borrowed. For the explicit overload, the supplied `ITerminalControlProvider`, `TerminalEndpoint`, `ITerminalInput`, and `ITerminalOutput` objects are also borrowed. Disposing the session does not close or dispose those caller-owned resources merely because the session used them.

The session does own the reversible state it acquired:

- the semantic input-mode transition;
- system-backed Windows virtual-terminal output setup when applicable;
- active presentation leases;
- lifecycle re-entry of those owned states.

`DisposeAsync()` is asynchronous by design. Cleanup may require output flushing before restoration, and the library does not add synchronous `IDisposable` merely to perform sync-over-async teardown.

Restoration uses the captured baseline, not a guessed "normal" terminal mode. Cleanup is idempotent and is attempted after partial initialization failures. When a primary operation and restoration both fail, the cleanup failure is preserved rather than silently discarded.

---

## 5. Semantic input modes

The public request is semantic: `Canonical`, `CBreak`, or `Raw`, plus an independent echo request. Platform-native flags remain below that request.

### POSIX

- `Canonical` enables canonical line buffering and host signal processing while preserving unrelated baseline state.
- `CBreak` disables canonical buffering, retains host signal processing, sets immediate character-oriented `VMIN`/`VTIME` behavior, and preserves unrelated baseline state.
- `Raw` disables canonical buffering, host signal processing, ordinary input translations, output post-processing, and the other raw-incompatible fields required by the supported host layout.
- semantic changes are applied after pending output drains;
- no semantic mode implicitly discards unread input.

### Windows Console

- `Canonical` enables processed and line input;
- `CBreak` retains processed input, disables line input, and enables virtual-terminal input;
- `Raw` disables processed and line input and enables virtual-terminal input;
- Windows mode changes are applied immediately because Win32 does not provide POSIX drain-before-apply semantics;
- POSIX concepts such as `ISIG`, `VMIN`, `VTIME`, baud rates, and control-character arrays are not fabricated on Windows.

Windows host echo and non-line input do not have perfect POSIX semantic parity. `EchoInput` remains an explicit requested console-mode state; higher layers that require deterministic character echo in noncanonical modes should render it themselves.

---

## 6. Endpoint and redirection behavior

A live `TerminalSession` always requires an interactive **input** endpoint because the session captures and owns an input-mode transition.

Output is interactive by default. `TerminalSessionOptions.RequireInteractiveOutput` may be set to `false` for applications that intentionally combine terminal input with redirected output.

Permitting redirected output does not manufacture terminal capabilities for that output stream. Operations which require live terminal presentation remain capability-driven and may report unavailable when the selected endpoint/profile cannot satisfy them.

The standard overload borrows standard input and standard output. Advanced callers may provide explicit endpoint/control/byte-service combinations through the injected overload without transferring their lifetime to the session.

---

## 7. Terminal identity and fallback

Terminal capability identity remains explicit and observable.

Resolution precedence is:

1. `TerminalSessionOptions.TerminalOverride`, when supplied;
2. an explicit `TerminalName`, otherwise the process `TERM` value;
3. lookup through the supplied terminal database, or the default system-discovery plus built-in database;
4. a controlled platform fallback when the requested name is missing or unavailable.

An unknown POSIX terminal does **not** silently become xterm. The safe fallback is `TerminalProfiles.Dumb`.

For Windows console endpoints, platform fallback uses `TerminalProfiles.MsTerminalDirect` when a Windows Terminal `WT_SESSION` is observed and `TerminalProfiles.WinConsole` otherwise.

`TerminalIdentity.Source` records whether the selected description came from an explicit override, a named profile, or platform fallback.

---

## 8. Input decoding, timeouts, and cancellation

Traditional special-key sequences come from the selected `TerminalDescription`; the decoder does not add a second hard-coded xterm/ANSI key table.

The decoder is incremental and preserves fragmented UTF-8 and escape-prefixed key sequences across transport reads. Undecoded input is bounded by `TerminalSession.MaximumBufferedInputBytes`, currently 4096 bytes.

An ESC byte may be either a literal Escape key or the prefix of a configured key sequence. `TerminalSession.DefaultEscapeSequenceTimeout`, currently 100 milliseconds, bounds this ambiguity. Continuation bytes arriving within that interval may complete the longer sequence; otherwise the shorter exact key or literal Escape is emitted as appropriate.

`TerminalSession.ReadEventAsync(...)` is the ordinary unified reader for input and lifecycle events. Relative timeouts use the configured `IMonotonicClock`. The absolute `DateTimeOffset` overload converts the absolute deadline to a relative interval at call time and then uses the monotonic wait machinery.

Caller timeout/cancellation ends the **current wait**, not the underlying pending terminal read. Pending bytes remain available for the next read. The returned event distinguishes `Timeout` from `Cancelled`.

Session disposal is different: it cancels the session-lifetime read machinery so a cancellable borrowed input source can terminate outstanding work.

---

## 9. Lifecycle contract

System-backed sessions may observe resize, interrupt, termination, POSIX suspend, and resume without requiring each application to install its own process-global signal handlers.

`TerminationToken` is canceled after an interactive interrupt, termination request, or fatal lifecycle-pump failure.

`ReadEventAsync(...)` and `ReadLifecycleEventAsync(...)` consume the same lifecycle queue. A consumer should choose one ownership pattern rather than independently reading both APIs concurrently.

Higher layers that own terminal state not known to `Icod.Terminal` may register `ITerminalSessionLifecycleParticipant`. Suspend preparation runs in reverse registration order before Terminal releases its presentation/output/input state. Resume callbacks run in registration order after Terminal re-enters its owned state.

This is the seam used by `Icod.DCurses`; it avoids installing a second native signal/cancellation subsystem above `TerminalSession`.

---

## 10. Presentation leases

`TerminalSession.AcquirePresentationAsync(...)` owns reversible capability-driven requests for:

- alternate/full-screen mode;
- keypad/application transmit mode;
- physical cursor visibility.

The API is mechanism, not presentation policy. `Icod.DCurses` decides when a full-screen application should acquire these states.

Leases may overlap. Alternate-screen and keypad state remain active until the last requesting lease is released. Cursor requests compose by acquisition order, with the most recently acquired active request winning.

A missing terminal capability produces a controlled unavailable result instead of an assumed ANSI escape sequence.

`TerminalPresentationLease` is `IAsyncDisposable` because releasing terminal state can require asynchronous output work.

---

## 11. Controlled result semantics

Low-level operations use a four-state result lattice:

- `Available` — the query returned a value, or a mutation completed successfully;
- `Unavailable` — the platform supports the operation but this endpoint/session cannot provide it;
- `Unsupported` — the current platform implementation does not support the operation;
- `Failed` — a host/native operation failed in a controlled manner.

For query results, use `TerminalControlResult<T>.IsAvailable` and `GetRequiredValue()`.

For mutations, use `TerminalControlMutationResult.Succeeded`. The shared status value for a successful mutation is `Available`; callers should prefer `Succeeded` rather than infer mutation success from status-name wording.

A native error code is exposed only when the underlying operation reports one. Controlled status is not a replacement for argument validation: invalid public API arguments still throw the appropriate .NET argument exceptions.

---

## 12. PTY boundary

Pseudo-terminal creation, ConPTY creation, child-process hosting, and PTY transport ownership are **not** part of `Icod.Terminal 0.1.0`.

A future `Icod.Pty` package is an adjacent layer. It may use or compose terminal abstractions, but `Icod.Terminal` does not depend on it. Ordinary `watch` child output remains pipe-based unless a later feature explicitly requests PTY behavior.

---

## 13. API-regret decisions

The audit specifically reviewed several potentially questionable surfaces and retains them deliberately:

- `TerminalControlStatus.Available` remains the shared successful state for queries and mutations; `TerminalControlMutationResult.Succeeded` is the mutation-facing convenience.
- `TerminalModeSnapshot` remains public because complete mode capture/inspection is a legitimate diagnostic and control contract.
- `TerminalModeSnapshot.WithPosixSerializedState(...)` remains public because restoring/editing a captured POSIX state must preserve baseline host ABI details that a newly synthesized snapshot may not carry.
- `TerminalInputModePolicy` and `TerminalModeCodec` remain public for advanced and `stty`-class consumers; ordinary applications should prefer `TerminalSession`.
- `ITerminalInput`, `ITerminalOutput`, and `ITerminalControlProvider` remain public injection seams. Concrete stream adapters and OS-specific providers remain internal.
- decoded event constructors remain internal so the library preserves event invariants; tests and advanced consumers inject bytes/lifecycle sources at the lower seam rather than manufacturing incoherent events.
- `TerminalPresentationLease` remains async-disposable only; synchronous teardown would introduce undesirable sync-over-async behavior.
- `TerminalSession` exposes no mandatory process-global current session or current terminal profile.

No reviewed item justifies a breaking change before `0.1.0`.

---

## 14. Deferred evolution

T12B does not pull later roadmap features into `0.1.0`.

- `0.2.0` may extend input events with mouse, focus, bracketed paste, and richer modifiers.
- `0.3.0` owns active terminal query/response correlation.
- `0.4.0` owns operational protocols such as titles, hyperlinks, clipboard, and synchronized output.
- `0.5.0` owns negotiated modern keyboard protocols.
- `0.6.0` may add more ergonomic caller-owned endpoint/transport scenarios without changing PTY ownership.

These features should extend the reviewed 0.1 session/input/lifecycle machinery rather than create parallel read loops or platform-control stacks.

---

## 15. Gate

**Gate T12B is satisfied when:**

- the reviewed public API is recorded;
- the ownership, restoration, semantic-mode, redirection, identity/fallback, Escape ambiguity, result-status, lifecycle, presentation, and PTY boundaries are documented;
- the sample demonstrates `TerminalSession` rather than the earlier T04-only low-level surface;
- no unresolved public-API regret requiring a breaking pre-0.1 change remains.

T12C may then validate the actual package and fresh-consumer experience without reopening terminal architecture unless package consumption reveals a concrete defect.
