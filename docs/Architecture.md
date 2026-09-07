# Icod.Terminal Architecture

This document is a permanent 1.x architecture authority for `Icod.Terminal`. Historical roadmaps and `Txxx` records explain how the design evolved; this document describes the supported architecture consumers should rely on.

## 1. Role in the Icod terminal stack

`Icod.Terminal` is the live-terminal/session layer between immutable terminal capability data and higher-level terminal user interfaces.

```text
Applications
  commands / monitors / editors / pagers / REPLs
                         |
                +--------+--------+
                |                 |
          Icod.DCurses       direct consumers
                |                 |
                +--------+--------+
                         |
                   Icod.Terminal
                         |
                   Icod.TermInfo
                         |
          tty / console / terminal transports

future Icod.Pty is adjacent: it may create child-process PTYs/ConPTYs,
but it is not part of the Icod.Terminal runtime dependency chain.
```

The layer boundaries are intentional.

### `Icod.TermInfo`

`Icod.TermInfo` owns immutable terminal capability data and terminfo interpretation. It answers questions such as:

- which capability strings describe a terminal;
- how parameterized terminfo strings expand;
- which named/extended capabilities are present;
- how compiled/system/built-in terminal descriptions are resolved.

`Icod.Terminal` consumes that information; it does not maintain a competing terminal-capability database.

### `Icod.Terminal`

`Icod.Terminal` owns live terminal mechanics and one live terminal conversation:

- endpoint observation and native platform identity;
- terminal-mode capture and semantic input policy;
- exact restoration of captured host state;
- live dimensions and supported lifecycle observation;
- one authoritative input-reader/decoder path;
- active query/response correlation;
- bounded incremental parsing;
- semantic terminal-output operations;
- reversible presentation, rich-input, and color ownership;
- serialization of session-managed terminal output;
- lifecycle-aware re-entry and deterministic cleanup.

### `Icod.DCurses`

`Icod.DCurses` owns two-dimensional presentation policy:

- cells, styles, windows, pads, and virtual-screen state;
- Unicode display width at the presentation layer;
- clipping, wrapping, scrolling, and damage tracking;
- desired-vs-physical screen comparison;
- refresh/diff strategy;
- curses-shaped application abstractions.

DCurses may request terminal state from `Icod.Terminal`; it should not reimplement POSIX termios, Windows console-mode, terminal query routing, or lifecycle restoration.

### Future `Icod.Pty`

Pseudo-terminal creation, child-process hosting, ConPTY/PTY plumbing, and process ownership are outside the `Icod.Terminal` 1.x contract.

A PTY package may supply endpoints/transports that can be used with terminal abstractions, but `Icod.Terminal` does not depend on PTY ownership and does not launch child processes.

## 2. Three public abstraction levels

The public surface deliberately contains more than one abstraction level. They are not interchangeable.

### 2.1 Ordinary semantic session API

This is the preferred level for applications.

A `TerminalSession` exposes semantic operations for:

- reading normalized terminal events;
- querying live terminal state through typed query methods;
- writing application text;
- writing selected semantic terminal metadata/control operations;
- acquiring reversible presentation, input-protocol, progress, pointer, color, and related state where the library can own restoration truthfully.

Ordinary callers should prefer these APIs because they participate in session ordering, lifecycle, validation, resource bounds, and restoration semantics.

### 2.2 Advanced transport/provider API

`ITerminalInput`, `ITerminalOutput`, `ITerminalControlProvider`, `TerminalEndpoint`, native mode snapshots, and controlled result types are public intentionally.

They exist for:

- custom hosts and injected transports;
- platform/provider implementations;
- diagnostics and `stty`-class scenarios;
- test infrastructure;
- higher-level libraries that deliberately operate below the ordinary semantic API.

These lower-level contracts do not imply that a live session may be bypassed arbitrarily.

In particular, a `TerminalSession` owns the only authoritative input-reader path while it is active. The raw input transport supplied at open time is not returned publicly by the session in 1.0.

The borrowed `TerminalSession.Output` transport remains an explicit advanced escape hatch. Direct use is outside session serialization; callers that use it accept responsibility for avoiding interleaving with session-managed output.

### 2.3 Internal wire/platform machinery

Protocol encoders/parsers, query transactions, lifecycle signal sources, presentation/input managers, OS signal plumbing, and concrete state-reconciliation machinery are implementation details unless represented separately by a public semantic contract.

Internal implementation names or wire selectors are not compatibility promises merely because a public semantic API ultimately uses them.

## 3. Capability-driven, not terminal-brand-driven

`Icod.Terminal` does not equate “interactive terminal” with “xterm.”

Where an operation is represented by terminfo, the selected `TerminalDescription` is the authority. Where a protocol has a separate explicit contract, support is based on that contract rather than broad terminal-brand heuristics.

The library does not make a general promise that `TERM`, an environment variable, or one emulator brand implies support for every modern protocol.

Automatic terminal-brand guessing is not a substitute for capability data, explicit observation, or a deliberately frozen compatibility contract.

## 4. Managed-first platform model

The library is managed C# with narrowly scoped native interop for terminal/console mechanics.

There is no runtime dependency on native `ncurses`, `curses`, `libtinfo`, or `termcap`.

Platform differences remain visible:

- POSIX mode snapshots represent termios-style flags, control characters, line discipline, and speed data where available;
- Windows snapshots represent console input/output modes and direction;
- Windows does not fabricate POSIX baud/control-character semantics;
- POSIX does not fabricate Windows console-mode semantics.

Ordinary applications normally request semantic policies such as canonical, cbreak, raw, or echo behavior rather than editing native bit masks.

## 5. Ownership is the central design rule

A `TerminalSession` is an owner of terminal **state transitions**, not necessarily an owner of the underlying handle, descriptor, stream, or transport object.

This distinction drives the architecture:

- supplied endpoints/transports remain borrowed;
- session disposal restores state the session changed but does not close caller-owned transports;
- reversible terminal features use leases when several logical consumers may overlap;
- restoration is based on captured/observed state rather than a guessed “normal” terminal;
- state that cannot be observed/restored truthfully is not blindly lease-owned;
- invalidation marks believed physical state untrustworthy rather than inventing certainty.

See `Terminal-Session-and-Ownership.md` and `Lifecycle-and-Restoration.md` for the permanent ownership contract.

## 6. One authoritative input conversation

Input is not a collection of independent readers.

The live session owns one incremental byte stream that may contain, in arbitrary fragmentation and interleaving:

- UTF-8 text;
- traditional keys;
- mouse/focus/bracketed-paste reports;
- modern keyboard frames;
- responses to active terminal queries;
- malformed or unknown terminal traffic.

The decoder/query router therefore form one authoritative input path. Public event reads and typed queries coordinate through that path.

A competing raw read cannot be made safe by the library because it could consume part of a correlated or fragmented frame. This is why `TerminalSession.Input` is not a public 1.0 member even though `ITerminalInput` remains a public injection interface.

Permanent event/query semantics are documented by T192 in `Input-and-Events.md` and `Queries-and-Responses.md`.

## 7. Session-managed output ordering

High-level application text, semantic output operations, query requests, and state-manager traffic share session-owned serialization domains appropriate to their contracts.

The library guarantees ordering among operations that participate in those domains; it does not claim to serialize writes made directly to the borrowed `TerminalSession.Output` transport.

Low-level `WriteTerminalStringAsync(...)` is retained for already-resolved terminfo strings used by capability-driven renderers. It participates in session output handling and is not intended as a generic arbitrary-vendor-command API.

## 8. Reversible state composition

Some terminal state is logically independent but physically coupled. Modern keyboard state can be screen-local, for example, so an alternate-screen transition may have to coordinate input-protocol and presentation ownership.

The permanent ordering for session-managed input/presentation mutation is:

```text
state composition
    -> lifecycle/teardown availability
        -> manager
            -> control output
```

This is a behavioral ordering invariant, not a requirement that future implementations continue to use the current semaphore/manager classes.

The contract requires that a state transition from one manager cannot interleave inside a coupled transition owned by another manager, and that lifecycle/teardown cannot admit new public ownership after session state has been released.

## 9. Lifecycle is a state transition, not a notification-only feature

When supported, suspend/resume is integrated with terminal ownership.

Before suspension, the session restores owned terminal state needed to return control safely to the host. After resume, it invalidates stale beliefs, re-establishes configured/owned state, refreshes observation-dependent state where required, resumes registered participants, and only then reports normal resumed operation.

Lifecycle failure is allowed to make session state invalid and terminate the lifecycle stream rather than falsely reporting recovery.

See `Lifecycle-and-Restoration.md`.

## 10. Failure and uncertainty

The architecture prefers truthful uncertainty over optimistic state advancement.

For multi-step reversible operations:

- successfully completed steps are tracked;
- failure triggers contract-defined rollback where possible;
- rollback failure is surfaced rather than hidden;
- a failed acquisition must not leave ghost logical ownership;
- physical state may be marked unknown/invalid if exact recovery cannot be proven;
- later cleanup retains restoration authority where meaningful.

This rule applies across presentation, rich input, lifecycle re-entry, color ownership, and other stateful contracts.

## 11. No process-global current terminal contract

The primary API is instance-based. `Icod.Terminal` does not require a process-global current session or `cur_term`-style mutable singleton.

Multiple session objects may exist, but the library does not promise that two independently created sessions may concurrently mutate the **same physical terminal endpoint** without external coordination. Ownership of a physical terminal conversation must remain unambiguous.

## 12. Stable architectural exclusions

The 1.0 architecture does not include:

- arbitrary raw escape-sequence dispatch as the normal application API;
- arbitrary OSC/vendor command selection;
- blind activation of unverifiable terminal state;
- terminal-brand-triggered feature enablement as a general policy;
- virtual-screen/window/cell management owned by `Icod.Terminal`;
- PTY/ConPTY child-process ownership;
- application command policy;
- global keyboard hooks, remapping, IME control, or OS-wide hotkeys;
- graphics/image protocol ownership merely for completeness.

Future additions must preserve the layer boundaries above or explicitly revise this architecture in a major-version-compatible way.
