# Icod.Terminal Development Roadmap

**Project:** `Icod.Terminal`  
**Package:** `Icod.Terminal`  
**Repository:** `https://github.com/uniblab/Icod.Terminal`  
**Language:** C# 13  
**Initial target frameworks:** `net8.0`; `net10.0`  
**Configurations:** `Debug`; `Staging`; `Release`  
**License:** LGPL-3.0-or-later  
**Current development target:** `0.1.0`  
**Stable contract target:** `1.0.0`  
**Immediate acceptance consumers:** `Icod.DCurses`, `watch`, `slabtop`, `top`  
**Status:** T01-T11 complete; T12A-T12B complete; next T12C package and fresh-consumer validation
**Current tranche:** T12 — `0.1.0` package, CI, documentation, and API gate
**Current subtranche:** T12C — package and fresh-consumer validation

---

## 1. Purpose

`Icod.Terminal` is the managed, cross-platform live-terminal layer for the Icod library family.

It occupies the architectural space between immutable terminal capability data and higher-level terminal user interfaces:

```text
Applications
  command-line tools / monitors / editors / pagers / REPLs
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
          tty / console / terminal streams

Icod.Pty is an adjacent, optional layer for child-process pseudo-terminals.
It is not required by Icod.Terminal or Icod.DCurses.
```

The division of responsibility is deliberate:

- `Icod.TermInfo` answers **what a terminal can do and which capability data describes those operations**;
- `Icod.Terminal` answers **how a live terminal endpoint is observed, configured, read, written, restored, and driven as a session**;
- `Icod.DCurses` answers **how an application composes a two-dimensional virtual screen and efficiently refreshes it**;
- `Icod.Pty` answers **how another process is hosted behind a pseudo-terminal or ConPTY**.

The immediate reason for `Icod.Terminal` is to remove operating-system terminal mechanics from `Icod.DCurses` and application code so that `top`, `slabtop`, and `watch` can migrate from `Icod.CoreUtils` into `Icod.ProcPs` on a clean reusable stack.

Version `0.1.0` is therefore a consumer-driven foundation release. It is complete only when those three programs can use `Icod.DCurses` over `Icod.Terminal` and `Icod.TermInfo` without retaining private terminal-mode, geometry, signal, keyboard-sequence, cursor-lifecycle, or console/tty implementations.

---

## 2. Architectural Boundaries

### 2.1 `Icod.TermInfo` remains the capability authority

`Icod.Terminal` SHALL depend on `Icod.TermInfo` rather than duplicate it.

`Icod.Terminal` SHALL NOT maintain parallel ANSI/xterm/Windows capability tables for operations already represented by `TerminalDescription`.

Terminal identity, capability lookup, terminfo parameter expansion, color capability semantics, compiled terminfo acquisition, and terminal-profile fallback remain `Icod.TermInfo` responsibilities.

Where `Icod.TermInfo` already exposes a stable helper required by a live session, such as reversible Windows virtual-terminal output enablement, `Icod.Terminal` SHOULD consume or wrap that helper rather than reimplement it.

### 2.2 `Icod.Terminal` owns live terminal state

`Icod.Terminal` SHALL own the reusable mechanisms for:

- identifying whether an endpoint is a terminal;
- representing borrowed standard endpoints and explicitly supplied endpoints;
- observing terminal/console identity where the host can provide it;
- capturing and applying terminal mode state;
- canonical, cbreak, and raw input-discipline policy;
- echo policy;
- exact restoration of prior host mode;
- terminal dimensions and resize observation;
- terminal input/output byte flow;
- text decoding needed for terminal input events;
- incremental escape/key-sequence decoding;
- bounded Escape-prefix ambiguity handling;
- lifecycle events such as resize, cancellation, supported suspend, and resume;
- reversible full-screen, cursor-visibility, and keypad/application-mode leases;
- selected `TerminalDescription` associated with a live session;
- deterministic cleanup after normal completion, cancellation, exceptions, and partial initialization.

### 2.3 `Icod.DCurses` owns the virtual presentation model

`Icod.DCurses` SHALL remain responsible for:

- terminal cells and styles;
- Unicode display-width policy at the presentation layer;
- desired and physical screen images;
- damage tracking;
- refresh/diff strategy;
- windows, subwindows, pads, and related presentation abstractions;
- clipping, wrapping, scrolling, and cursor placement within the virtual screen;
- curses-shaped application events and compatibility conveniences;
- policy deciding when a curses session should acquire terminal presentation leases.

`Icod.DCurses` SHALL NOT need to know POSIX `termios` bit values, Windows console mode bit values, `ioctl` request numbers, native file-descriptor constants, or HANDLE details in order to establish raw/cbreak operation.

### 2.4 `Icod.Pty` is orthogonal

Pseudo-terminal creation and child-process plumbing are explicitly outside the `Icod.Terminal` runtime contract.

A future `Icod.Pty` package may depend on or share value types with `Icod.Terminal`, but `Icod.Terminal` SHALL NOT depend on `Icod.Pty`.

`watch` does not require a PTY for its ordinary semantics: watched child output may remain pipe-based. A future optional PTY mode is not part of the `0.1.0` acceptance gate.

### 2.5 `Icod.CommandFramework` remains command policy, not the terminal substrate

The current `Icod.CommandFramework.Terminal` namespace contains valuable neutral terminal foundations created before a dedicated terminal package existed.

`Icod.Terminal` SHALL extract or generalize the genuinely terminal-specific portions while leaving command/presentation policy in `Icod.CommandFramework`.

Likely extraction candidates include:

- terminal endpoint and attachment observations;
- terminal control result contracts;
- mode snapshots and application timing;
- system terminal control provider selection;
- Linux/macOS terminal control interop;
- Windows console control interop;
- terminal-mode serialization/codec functionality where it is truly terminal-generic;
- generic live geometry observation.

Likely `Icod.CommandFramework` responsibilities which SHALL NOT simply move wholesale include:

- GNU command color policy;
- filename quoting/presentation;
- `QUOTING_STYLE` policy;
- command-specific fallback and diagnostic policy;
- option parsing and exit-status decisions;
- `stty` and `tty` command syntax or GNU compatibility behavior.

No code SHALL be deleted from `Icod.CommandFramework` merely because an extraction is planned. Equivalent `Icod.Terminal` behavior must first exist, be tested, and have a migration path for existing consumers.

---

## 3. Design Principles

### 3.1 Managed first, narrow native interop

The implementation SHALL be managed C# except for narrowly scoped operating-system interop required to control or observe terminals.

Native `ncurses`, `curses`, `libtinfo`, or `termcap` SHALL NOT be runtime dependencies.

### 3.2 Explicit session ownership

A live terminal session is an owned resource even when its underlying standard streams or handles are borrowed.

The API SHALL distinguish ownership of the session's state transitions from ownership of the underlying process-standard handles/streams.

Disposing a standard-terminal session SHALL restore state which the session changed, but SHALL NOT close process-standard input/output merely because the session was disposed.

### 3.3 Exact restoration is a core invariant

Mode restoration SHALL be based on captured host state rather than on a guessed "normal" mode.

Restoration SHALL be idempotent and best-effort after partial initialization.

If initialization performs transitions A, B, and C and fails during C, cleanup SHALL attempt to reverse every transition which successfully occurred.

### 3.4 Semantic APIs above native flag manipulation

Ordinary consumers SHOULD request semantic behavior such as canonical, cbreak, raw, echo/noecho, resize observation, or presentation leases.

Public consumers SHALL NOT be required to manipulate platform-native bit masks merely to use those behaviors.

Complete native snapshots MAY remain representable for compatibility, diagnostics, and `stty`-class consumers, but native representation is a lower-level contract than normal session policy.

### 3.5 Capability-driven terminal output

Full-screen entry/exit, cursor visibility, keypad/application mode, and similar terminal presentation transitions SHALL use the selected `TerminalDescription` where terminfo capabilities exist.

The library SHALL NOT equate "interactive terminal" with "xterm-compatible terminal".

### 3.6 No mandatory process-global current terminal

The primary API SHALL be instance-based.

There SHALL be no required process-global current session or current terminal description analogous to native `cur_term`.

Multiple independently supplied terminal endpoints must remain possible in one process.

### 3.7 Platform differences remain observable

Windows SHALL NOT fabricate POSIX termios concepts such as baud rates or control-character arrays.

POSIX SHALL NOT pretend Windows console modes exist.

Unavailable, unsupported, and failed operations SHALL remain distinguishable when that distinction is meaningful to callers.

### 3.8 Async and cancellation are first-class

Input waiting and output flushing SHALL support asynchronous operation and cancellation where the host permits it.

Periodic applications such as `top`, `slabtop`, and `watch` must be able to combine refresh deadlines with immediate keyboard input without creating a process-global polling loop in each application.

### 3.9 Incremental input, not one-shot escape matching

Terminal input arrives in arbitrary fragments.

The decoder SHALL support:

- partial UTF-8 sequences;
- partial escape sequences;
- several input events in one read;
- prefix-sharing key sequences;
- isolated Escape versus Escape-prefixed sequence ambiguity;
- bounded buffering and deterministic timeout policy.

### 3.10 Testability without a real terminal

Core behavior SHALL be testable with injected/in-memory terminal backends.

Normal unit tests SHALL NOT require an interactive developer terminal, mutate the test runner's terminal mode, or write unsolicited data to process stdout/stderr.

Host-terminal integration tests MAY exist separately and SHALL be clearly identified.

A future `Icod.Pty` package MAY be used as test infrastructure, but SHALL NOT become a runtime dependency.

### 3.11 Application policy stays above the library

`Icod.Terminal` SHALL not know how `top` sorts processes, how `slabtop` displays slabs, or how `watch` launches and compares commands.

It provides terminal mechanisms, not ProcPs application behavior.

---

## 4. Dependency and Framework Policy

### 4.1 Runtime dependency graph

The intended runtime graph is:

```text
Icod.TermInfo
      ^
      |
Icod.Terminal
      ^
      |
Icod.DCurses
      ^
      |
Icod.ProcPs interactive applications
```

`Icod.DCurses` MAY also reference `Icod.TermInfo` directly where it needs capability-level data for refresh strategy.

`Icod.CommandFramework` MAY eventually reference `Icod.Terminal` after neutral terminal facilities have migrated. `Icod.Terminal` SHALL NOT reference `Icod.CommandFramework` in the steady-state architecture.

### 4.2 Initial package dependency

Version `0.1.0` SHALL depend on a stable `Icod.TermInfo` 1.x package or equivalent project reference during coordinated development.

No dependency on `Icod.DCurses`, `Icod.ProcPs`, or `Icod.Pty` is permitted in the runtime library.

### 4.3 Target frameworks

The initial package SHOULD target:

```text
net8.0
net10.0
```

This matches the stable `Icod.TermInfo` framework envelope and avoids making the live-terminal substrate unnecessarily specific to one consumer.

If a platform API used by `0.1.0` cannot be expressed cleanly on `net8.0`, the implementation SHALL first prefer narrowly scoped compatibility code before reducing the framework set.

### 4.4 Language and build policy

The project SHALL use C# 13 with nullable reference types enabled.

Repository style SHALL follow the current Icod conventions, including:

- braces for `if`, `else`, loops, and related control-flow bodies;
- parameter validation at public/protected/internal method boundaries where appropriate;
- deterministic builds;
- explicit compile items;
- no test output to stdout/stderr unless communication with another process is the subject of the test;
- `<Version>` and `<PackageVersion>` updated together through development releases.

---

## 5. Repository Shape

The repository SHOULD follow the established `Icod.TermInfo` / `Icod.DCurses` pattern:

```text
Icod.Terminal/
    .github/
        workflows/
    docs/
    samples/
        Icod.Terminal.Sample/
    src/
        Control/
        Input/
        Lifecycle/
        Platform/
        Presentation/
        Session/
    tests/
        Icod.Terminal.Tests/
            src/
    Directory.Build.props
    Icod.Terminal.csproj
    Icod.Terminal.sln
    LICENSE
    README.md
    icon.png
    build.cmd
    build.sh
    Icod.Terminal-Development-Roadmap.md
```

The exact `src/` subdivision MAY evolve while the public contract is pre-1.0.

The root library project SHALL compile only intended library source and SHALL not accidentally compile tests, samples, tools, or generated fixtures.

---

## 6. Version Roadmap

| Version | Theme | Principal outcome |
| --- | --- | --- |
| `0.1.0` | ProcPs live-terminal foundation | Complete live terminal substrate required by `Icod.DCurses`, `watch`, `slabtop`, and `top` |
| `0.2.0` | Rich input events | Mouse, focus, bracketed paste, richer modifiers, input-policy completion |
| `0.3.0` | Query/response routing | Active terminal requests, response correlation, deadlines, probe foundation |
| `0.4.0` | Operational terminal protocols | Titles, hyperlinks, clipboard/selection, synchronized output, cursor-style operations where appropriate |
| `0.5.0` | Modern keyboard protocols | CSI-u / Kitty-style keyboard negotiation and event semantics where supported |
| `0.6.0` | Endpoint and transport expansion | Broader caller-owned endpoints, non-standard tty/console scenarios, transport-oriented abstractions without PTY ownership |
| `0.7.0` | Protocol extensibility | Stable extension points for selected modern terminal protocols without bloating the core session contract |
| `0.8.0` | Platform/lifecycle hardening | Windows/POSIX parity audit, suspend/resume recovery, stress, resource bounds, performance, fuzzing |
| `0.9.0` | Contract freeze | Public-API regret audit, compatibility baselines, package/TFM freeze, migration completion |
| `1.0.0` | Stable release | Production-supported live terminal/session contract for general .NET consumers |

Features MAY move between pre-1.0 versions when implementation experience justifies it, but the architectural boundaries in this roadmap SHALL remain the default unless deliberately revised.

---

# 7. Version 0.1.0 — ProcPs Live-Terminal Foundation

## 7.1 Release objective

Version `0.1.0` SHALL provide the complete live-terminal substrate needed for the first production consumers:

- `Icod.DCurses`;
- `watch`;
- `slabtop`;
- `top`.

This release is not merely a wrapper around `Console` and not merely a relocation of existing `Icod.CommandFramework.Terminal` files.

It SHALL establish the reusable abstractions which allow higher layers to stop owning operating-system terminal mechanics.

At the end of `0.1.0`, `Icod.DCurses` SHOULD NOT contain its own native POSIX terminal-mode bit editing, Windows console-mode bit editing, terminal attachment probes, live size probes, or platform-specific resize/suspend/cancellation plumbing except for presentation policy genuinely belonging to curses.

Likewise, the ProcPs applications SHOULD NOT maintain private terminal escape tables, termios/console snapshots, or resize handlers merely to implement their full-screen interfaces.

---

## 7.2 T01 — Repository and package foundation

Create the repository contract and a buildable package skeleton.

Required work:

- create `Icod.Terminal.sln`;
- create root `Icod.Terminal.csproj`;
- create `src/`;
- create `tests/Icod.Terminal.Tests/`;
- create `samples/Icod.Terminal.Sample/`;
- define `Debug`, `Staging`, and `Release` for every project;
- establish C# 13, nullable reference types, implicit usings, deterministic builds, and explicit compile items;
- target `net8.0;net10.0` unless a documented blocker is found;
- set both `<Version>` and `<PackageVersion>` to the same `0.1.0` development version;
- set assembly/root namespace identity to `Icod.Terminal`;
- add XML documentation output;
- add NuGet and GitHub Packages metadata;
- pack `README.md`, `LICENSE`, and `icon.png`;
- include portable symbols and Source Link metadata;
- create `build.cmd` and `build.sh` supporting clean, restore, build, test, and pack;
- add a minimal smoke test and sample.

**Gate T01:** clean/restore/build/test/pack succeeds from the repository root on all supported target frameworks.

---

## 7.3 T02 — Extraction inventory and contract classification

Before moving implementation, classify the existing `Icod.CommandFramework.Terminal` surface and the low-level terminal code already introduced in `Icod.DCurses`.

For every existing type or behavior, record one of:

1. moves/generalizes into `Icod.Terminal`;
2. remains in `Icod.CommandFramework` as command/presentation policy;
3. remains in `Icod.DCurses` as virtual-screen/presentation policy;
4. becomes a compatibility adapter over `Icod.Terminal`;
5. is obsolete and can be removed only after consumer migration.

The classification SHALL specifically audit:

- endpoint types and observations;
- terminal control result/status contracts;
- native mode snapshots;
- native mode serialization;
- Unix terminal provider/interops;
- Windows terminal provider/interops;
- geometry observation;
- terminal environment observation;
- color/presentation policy;
- current `DCurses` mode editing;
- Windows VT setup currently performed by DCurses;
- current DCurses terminal backend abstractions.

The tranche SHALL also define the namespace/type renaming strategy so the new library does not inherit names which only made sense inside `Icod.CommandFramework`.

**Gate T02:** a written extraction matrix exists and the dependency graph can be made acyclic with `Icod.Terminal -> Icod.TermInfo` and no runtime dependency from `Icod.Terminal` back to `Icod.CommandFramework` or `Icod.DCurses`.

**T02 completion record:** [`docs/T02-Extraction-Inventory-and-Contract-Classification.md`](docs/T02-Extraction-Inventory-and-Contract-Classification.md).

---

## 7.4 T03 — Endpoint observation and native mode parity

Establish the low-level system terminal control boundary.

The initial contract SHALL support at least:

- standard input;
- standard output;
- standard error where observation is meaningful;
- injected/caller-supplied endpoints for tests and advanced use;
- attached versus redirected observation;
- POSIX terminal pathname observation where available;
- stable Windows console endpoint identity where available;
- complete Linux/macOS terminal-mode capture;
- complete Windows input/output console-mode capture;
- mode restoration/application;
- immediate, drain-before-apply, and drain-plus-input-discard semantics where the platform supports them;
- preservation of POSIX control-character arrays and native speed codes;
- controlled available/unavailable/unsupported/failed results.

The existing neutral behavior in `Icod.CommandFramework.Terminal` SHALL be treated as compatibility input: extraction must not casually regress capabilities already used by `tty`, `stty`, or ProcPs consumers.

Native snapshot APIs MAY remain lower-level than the ordinary session API, but their semantics must be deterministic and testable.

**Gate T03:** Linux, macOS, and Windows provider tests demonstrate observation, capture, apply, and exact round-trip restoration semantics without requiring command-framework types.

---

## 7.5 T04 — Semantic input-mode policy

Add a platform-neutral semantic mode layer above native snapshots.

Version `0.1.0` SHALL support:

- canonical/cooked input;
- cbreak-style input;
- raw input;
- echo enabled/disabled;
- a documented signal/processed-input policy for each semantic mode;
- preservation of unrelated host-mode state whenever practical;
- exact restoration to the captured baseline.

The POSIX implementation SHALL centralize native termios knowledge inside the terminal platform layer.

The Windows implementation SHALL centralize console input-mode knowledge inside the terminal platform layer and SHALL not expose raw Win32 mode constants to ordinary consumers.

The implementation SHALL define rather than guess how raw/cbreak semantics map onto Windows, noting where semantic parity is impossible.

**Gate T04:** `Icod.DCurses` can request canonical, cbreak, or raw/noecho behavior without directly editing a POSIX or Windows native mode snapshot.

---

## 7.6 T05 — `TerminalSession` lifecycle and ownership

Introduce the primary live-session abstraction.

The session SHALL support:

- opening against process-standard terminal input/output;
- opening against explicitly supplied terminal endpoints/backends;
- observing whether required endpoints are interactive;
- exposing input and output services without transferring ownership of borrowed process-standard streams;
- capturing baseline mode before mutation;
- applying requested semantic input mode;
- deterministic restoration;
- idempotent disposal;
- asynchronous disposal where cleanup requires asynchronous flushing;
- cancellation-aware operations;
- partial-initialization rollback;
- explicit invalidation when external activity may have changed terminal state.

The session contract SHALL distinguish:

- borrowed endpoint lifetime;
- session state-transition ownership;
- capability/profile selection;
- presentation leases acquired by higher layers.

A failed session open SHALL leave the host terminal as close as possible to its original state.

**Gate T05:** fault-injection tests prove restoration after success, exceptions, cancellation, failed initialization steps, and repeated disposal.

**T05 completion record:** [`docs/T05-TerminalSession-Lifecycle-and-Ownership.md`](docs/T05-TerminalSession-Lifecycle-and-Ownership.md).

---

## 7.7 T06 — Terminal identity, `Icod.TermInfo` integration, and output setup

Bind a live session to an explicit `TerminalDescription`.

Default resolution SHOULD compose:

1. system terminfo discovery;
2. immutable built-in fallback;
3. explicit caller override when supplied.

Unknown terminal names SHALL not silently become xterm merely because xterm behavior is common.

Windows Console and Windows Terminal profile selection SHALL use the existing `Icod.TermInfo` profile model and remain distinct from enabling Windows virtual-terminal output processing.

The session SHALL provide a controlled way to ensure the output endpoint is configured for the selected terminal output model where required by the host.

The library SHALL also establish an explicit application-text encoding policy. Capability strings originating from terminfo remain terminal protocol data and SHALL continue to honor `Icod.TermInfo` byte semantics.

**Gate T06:** a standard live session can resolve a terminal description and emit capability-driven output without `Icod.DCurses` performing platform-specific terminal detection or Windows VT setup.

**T06 completion record:** [`docs/T06-Terminal-Identity-TermInfo-and-Output-Setup.md`](docs/T06-Terminal-Identity-TermInfo-and-Output-Setup.md).

---

## 7.8 T07 — Live dimensions and lifecycle events

Provide reusable terminal lifecycle observation required by periodic full-screen programs.

Version `0.1.0` SHALL account for:

- current live terminal dimensions;
- resize notification or reliable coalesced resize observation;
- cancellation/termination integration appropriate to the platform;
- POSIX suspend preparation where supported;
- POSIX resume observation where supported;
- dimension re-observation after resume;
- state invalidation after external suspension or terminal disruption;
- Windows console cancellation behavior;
- safe handoff from native/signal callback context into ordinary managed processing.

The library SHALL NOT require each application to install its own `PosixSignalRegistration` or `Console.CancelKeyPress` plumbing for ordinary live-terminal use.

Callbacks which execute in restricted host/runtime contexts SHALL do minimal work and defer ordinary session processing.

**Gate T07:** resize wakes a waiting interactive loop, and suspend/resume or equivalent lifecycle disruption can restore/re-enter session state without leaving the terminal corrupted.

**T07 completion record:** [`docs/T07-Live-Dimensions-and-Lifecycle-Events.md`](docs/T07-Live-Dimensions-and-Lifecycle-Events.md).

---

## 7.9 T08 — Input byte stream and 0.1 key-event decoder

Create an incremental terminal input decoder sufficient for the ProcPs acceptance consumers.

The terminal-level event model SHALL represent at least:

- ordinary Unicode text;
- Enter;
- Space as ordinary text or a distinguished key where the final model requires it;
- Escape;
- Backspace;
- Tab;
- Shift+Tab where distinguishable;
- arrow keys;
- Home;
- End;
- Page Up;
- Page Down;
- Insert;
- Delete;
- function keys required by the selected profile and acceptance applications;
- control-key combinations required by `top`, `slabtop`, and `watch`;
- end-of-input/disconnect;
- cancellation;
- resize wake-up either as an input event or a coordinated lifecycle event.

Traditional terminal key sequences SHALL be derived from `Icod.TermInfo` capability data where available.

The decoder SHALL:

- operate incrementally across arbitrary read boundaries;
- preserve partial UTF-8 sequences;
- preserve partial terminal control sequences;
- handle several events in one input buffer;
- use bounded buffering;
- document a bounded Escape ambiguity timeout/deadline;
- support cancellation and caller deadlines so periodic refresh loops need not busy-wait.

Mouse, focus, bracketed paste, and modern keyboard protocols are not required for the `0.1.0` gate unless one of the three acceptance consumers demonstrably requires them.

**Gate T08:** scripted tests cover byte-by-byte fragmentation, combined reads, UTF-8, overlapping prefixes, isolated Escape, escape-prefixed keys, timeout behavior, cancellation, and resize wake-up.

**T08 completion record:** [`docs/T08-Input-Byte-Stream-and-Key-Event-Decoder.md`](docs/T08-Input-Byte-Stream-and-Key-Event-Decoder.md).

---

## 7.10 T09 — Reversible terminal presentation leases

Provide low-level reversible presentation-state operations on top of `Icod.TermInfo` capabilities.

At minimum `0.1.0` SHALL support leases or equivalent deterministic state ownership for:

- alternate/full-screen cursor-addressing mode when the terminal advertises it;
- cursor invisible/normal state;
- keypad/application mode where required;
- output flush at meaningful transition boundaries;
- invalidation/re-entry after resume or terminal-state loss.

The API SHALL distinguish mechanism from policy: `Icod.Terminal` performs the reversible transition, while `Icod.DCurses` decides when its presentation requires that transition.

Nested or repeated acquisition behavior SHALL be defined so cleanup is deterministic and an inner consumer cannot accidentally restore a state still required by an outer owner.

Where a terminal lacks a capability, the result SHALL be controlled rather than replaced with an assumed ANSI sequence.

**Gate T09:** a test backend can verify exact enter/leave ordering, nested/repeated behavior, rollback after partial acquisition, and capability-driven output.

**T09 completion record:** [`docs/T09-Reversible-Terminal-Presentation-Leases.md`](docs/T09-Reversible-Terminal-Presentation-Leases.md).

---

## 7.11 T10 — `Icod.DCurses` integration and responsibility reset

Rebase `Icod.DCurses` on `Icod.Terminal`.

Required coordinated work SHALL include:

- replace direct `Icod.CommandFramework.Terminal` runtime use with `Icod.Terminal`;
- remove DCurses-owned POSIX termios bit editing;
- remove DCurses-owned Windows console input-mode editing;
- replace private terminal attachment/geometry providers where `Icod.Terminal` now supplies them;
- consume `TerminalSession` lifecycle/restoration;
- consume the terminal input-event stream or adapt it cleanly into curses events;
- acquire presentation leases through `Icod.Terminal`;
- retain DCurses ownership of virtual cells, windows, refresh/diff behavior, and curses presentation policy;
- update the DCurses roadmap to reflect the new lower-layer contract.

`Icod.DCurses` MAY keep its own injectable adapter boundary for tests, but that boundary SHALL adapt terminal abstractions rather than reproduce native terminal mechanics.

**Gate T10:** `Icod.DCurses` builds/tests without a runtime dependency on `Icod.CommandFramework.Terminal`, and its platform-specific terminal-mode implementation has been eliminated or reduced only to curses-specific policy.

**T10 integration record:** [`docs/T10-DCurses-Lifecycle-Participant-Integration.md`](docs/T10-DCurses-Lifecycle-Participant-Integration.md).

---

## 7.12 T11 — ProcPs acceptance: `watch`, `slabtop`, `top`

Use the three target applications as architectural acceptance tests.

### `watch`

The migrated `watch` SHALL be able to:

- run its own display through `Icod.DCurses` / `Icod.Terminal`;
- resize cleanly;
- restore the terminal on exit/cancellation/error;
- react to required keys without private escape decoding;
- continue to capture watched child output through ordinary process pipes unless a later feature explicitly requests PTY behavior.

### `slabtop`

The migrated `slabtop` SHALL be able to:

- own a full-screen DCurses presentation;
- refresh periodically without busy-waiting;
- react immediately to required interactive keys;
- resize/repaint correctly;
- restore terminal state deterministically.

### `top`

The migrated `top` SHALL be able to:

- own a full-screen DCurses presentation;
- enter the required input discipline and noecho state through `Icod.Terminal`;
- receive required text/control/navigation keys through the shared decoder;
- combine periodic sampling with keyboard wake-up;
- handle resize and supported suspend/resume;
- restore cursor, keypad/full-screen state, and host mode on every supported exit path;
- contain no direct termios/Win32 console-mode implementation or private terminal escape table.

The acceptance work SHALL identify missing reusable mechanisms rather than solving terminal problems inside the ProcPs applications.

**Gate T11:** all three applications can run on the supported Windows, Linux, and macOS environments using the shared stack, with platform-specific limitations documented rather than hidden.

**T11 completion record:** [`docs/T11-ProcPs-Acceptance.md`](docs/T11-ProcPs-Acceptance.md).

---

## 7.13 T12 — 0.1 package, CI, documentation, and API gate

T12 is the release-closure tranche for `0.1.0` and is executed in four subtranches:

- **T12A — Status reconciliation and consumer acceptance record — complete.** Reconcile the roadmap and README with completed T10/T11 work and preserve the ProcPs acceptance result in writing.
- **T12B — Public API and documentation audit — complete.** Review the public surface for pre-1.0 regret, publish the intentional 0.1 API baseline, and complete the behavioral documentation required for independent consumers.
- **T12C — Package and fresh-consumer validation — current.** Validate package contents, symbols/Source Link, and consumption from clean `net8.0` and `net10.0` projects.
- **T12D — Release closure.** Set the final `0.1.0` package version, run the complete release matrix, and publish the non-prerelease package.

**Gate T12A:** repository status documentation identifies T01-T11 as complete, makes T12 the active release tranche, and records the three ProcPs acceptance consumers.

**T12A completion record:** [`docs/T11-ProcPs-Acceptance.md`](docs/T11-ProcPs-Acceptance.md).

**Gate T12B:** the public 0.1 surface has an intentional review baseline, independent-consumer behavior is documented, and no unresolved API regret requiring a breaking pre-0.1 change remains.

**T12B completion records:** [`docs/T12B-Public-API-and-Consumer-Contract.md`](docs/T12B-Public-API-and-Consumer-Contract.md) and [`docs/Public-API-Baseline-0.1.md`](docs/Public-API-Baseline-0.1.md).

Before publishing `0.1.0`:

- run clean/restore/build/test/pack on Windows, Linux, and macOS;
- test both target frameworks where applicable;
- ensure Release treats warnings according to repository policy;
- verify Source Link and symbol package contents;
- add a fresh-consumer package test;
- document dependency versions and supported platforms;
- document session ownership and restoration guarantees;
- document semantic differences among canonical/cbreak/raw across Windows and POSIX;
- document redirection behavior;
- document terminal profile selection and explicit fallback behavior;
- document Escape ambiguity policy;
- document unsupported-result semantics;
- document that PTY ownership is not part of the package;
- publish an initial public API baseline for intentional review;
- perform an API-regret review before tagging even though the contract remains pre-1.0;
- verify `<Version>` and `<PackageVersion>` are both exactly `0.1.0` for the release commit.

**Release gate `0.1.0`:** the package is buildable and consumable independently, DCurses is integrated on top of it, and `watch`, `slabtop`, and `top` no longer require suite-private terminal-control implementations.

---

## 8. Testing Strategy

### 8.1 Pure/in-memory tests

The majority of tests SHALL use injected terminal backends and scripted byte streams.

These tests SHOULD cover:

- result/status contracts;
- mode transitions;
- semantic mode editing;
- restoration ordering;
- session ownership;
- capability selection;
- presentation leases;
- UTF-8 fragmentation;
- key-sequence fragmentation;
- Escape ambiguity;
- resize/lifecycle event coalescing;
- cancellation;
- failure injection;
- bounded buffering.

### 8.2 Platform interop tests

Platform-specific tests SHALL validate serialization/layout assumptions and native behavior without depending unnecessarily on the CI runner being interactively attached.

Interop structures and constants SHALL be covered by targeted tests and authoritative platform documentation.

### 8.3 Live-terminal integration tests

Tests which actually alter a host terminal SHALL be isolated from ordinary unit tests and SHALL restore state even on failure.

They MAY be opt-in where CI cannot guarantee a real terminal.

When `Icod.Pty` becomes available, a separate integration-test project MAY use it to create deterministic hosted terminal sessions. This does not alter the runtime dependency graph.

### 8.4 Consumer tests

`Icod.DCurses` and the three ProcPs applications are not merely downstream users; during `0.1.0` they are architectural acceptance drivers.

A change which makes the isolated Terminal tests pass but forces DCurses or ProcPs to reintroduce native terminal mechanics has failed the architectural gate.

---

## 9. Public API Direction

The public `0.1.x` surface completed intentional review in T12B. The exact reviewed inventory is recorded in [`docs/Public-API-Baseline-0.1.md`](docs/Public-API-Baseline-0.1.md), and its behavioral/ownership contract is recorded in [`docs/T12B-Public-API-and-Consumer-Contract.md`](docs/T12B-Public-API-and-Consumer-Contract.md).

The baseline centers on `TerminalSession` / `TerminalSessionOptions`, endpoint and control contracts, semantic input modes, decoded input/lifecycle events, reversible presentation leases, and the intentionally public low-level snapshot/serialization/injection seams required by advanced consumers. OS-specific interop providers, decoder internals, lifecycle signal plumbing, and presentation composition remain internal.

The `0.1.x` line reuses the existing public `Icod.TermInfo.TerminalSize` value type rather than defining a duplicate `Icod.Terminal.TerminalSize`. It likewise reuses `Icod.TermInfo` capability/profile types and `Icod.Timing.IMonotonicClock` where those dependency-owned contracts are the correct abstraction.

T12B is an intentional pre-release baseline, not the `1.0.0` compatibility freeze. Later pre-1.0 milestones may extend the surface and may make justified corrections, but no known breaking correction is required before `0.1.0`.

The reviewed names remain subordinate to these rules:

- live mutable state belongs to a session;
- terminal capabilities come from an explicit `TerminalDescription`;
- ordinary callers use semantic mode APIs;
- lower-level native snapshots remain available only where their diagnostic/control value justifies them;
- process-standard handles are borrowed, not silently closed;
- cleanup/restoration is deterministic;
- no mandatory process-global current terminal exists.

---

## 10. Post-0.1 Development Themes

### 10.1 Version 0.2 — Rich input events

Extend the input decoder and event model with features such as:

- mouse protocols;
- focus in/out;
- bracketed paste framing;
- richer modifier representation;
- configurable input policies;
- better composition of text, control keys, and protocol events;
- additional terminfo key capabilities and compatibility fixtures.

This tranche SHALL build on the 0.1 incremental decoder rather than introducing a second read loop.

### 10.2 Version 0.3 — Query/response routing

Add a common request/response mechanism for active terminal conversations.

Candidates include:

- device attributes;
- device status reports;
- cursor-position reports;
- DECRQSS;
- XTGETTCAP;
- bounded deadlines/timeouts;
- correlation between pending requests and incoming response frames;
- routing unsolicited application input around pending terminal responses.

Each protocol feature SHALL NOT invent its own global input loop.

### 10.3 Version 0.4 — Operational protocols

Build controlled live operations on the common session/router foundation, potentially including:

- terminal title operations;
- OSC 8 hyperlinks;
- OSC 52 clipboard/selection where supported and explicitly requested;
- synchronized output;
- cursor-style/color operations;
- notification protocols where they can be modeled safely.

Security-sensitive protocols SHALL remain opt-in and bounded.

### 10.4 Version 0.5 — Modern keyboard protocols

Add negotiated modern keyboard behavior where useful, including candidates such as CSI-u and Kitty keyboard protocols.

Traditional terminfo input MUST continue to work when modern negotiation is unavailable or disabled.

### 10.5 Version 0.6 — Endpoint and transport expansion

Generalize the live-session contract for broader advanced callers without turning `Icod.Terminal` into a PTY or terminal emulator package.

Potential work includes:

- explicitly supplied tty/console endpoints beyond process standard streams;
- caller-owned streams/handles with clear lifetime contracts;
- serial-terminal facts where justified;
- better support for embedding terminal sessions in servers/tools;
- endpoint capability observation separated cleanly from process-global environment.

### 10.6 Version 0.7 — Protocol extensibility

Establish extension points for advanced terminal protocols which need the shared framing/router/session machinery but should not force every protocol into the core API.

Graphics protocols may use this infrastructure but should remain separate components/packages if they become substantial.

### 10.7 Version 0.8 — Platform and lifecycle hardening

Perform a comprehensive hardening pass:

- Windows Console versus Windows Terminal parity audit;
- Linux versus macOS termios/layout audit;
- resize storms;
- suspend/resume stress;
- cancellation races;
- nested presentation leases;
- partial initialization failures;
- broken/disconnected endpoints;
- bounded memory under hostile input;
- performance under high-rate input/output;
- fuzzing of incremental input framing;
- long-running session tests.

### 10.8 Version 0.9 — Contract freeze

Before 1.0:

- audit every public type/member;
- eliminate accidental exposure of platform-native implementation details;
- freeze target framework policy;
- establish public API/binary compatibility baselines;
- document deprecation/versioning policy;
- validate package upgrades from late pre-1.0 builds;
- complete migration away from provisional duplicate terminal substrates where practical;
- verify documentation/sample coverage for all major contracts.

### 10.9 Version 1.0 — Stable release

Version `1.0.0` SHALL represent a stable, reusable live-terminal contract suitable for general-purpose .NET libraries and applications.

The 1.x line should be able to support DCurses, command-line terminal control tools, REPL/readline-style components, pagers, editors, process monitors, and future PTY-backed integration without reopening the fundamental session/endpoint/input/lifecycle architecture.

---

## 11. Explicit Non-Goals for the Initial Roadmap

The following are not part of the `Icod.Terminal` core responsibility unless a later roadmap deliberately changes the boundary:

- curses windows, pads, panels, menus, forms, or widgets;
- virtual-screen diff/refresh engines;
- ProcPs process data, sorting, filtering, or command semantics;
- pseudo-terminal/ConPTY child-process creation;
- terminal emulation;
- shell parsing or command execution policy;
- GNU `stty` option grammar and diagnostics;
- GNU `tty` command policy;
- filename quoting rules;
- directory-listing color policy;
- terminfo source parsing or database tooling;
- bundled native curses/ncurses dependencies.

---

## 12. Completion Definition

The project reaches its first meaningful milestone when this dependency story is true in real code:

```text
Icod.TermInfo
    terminal identity + immutable capabilities
           |
           v
Icod.Terminal 0.1
    live endpoint + modes + input + resize/lifecycle
    + reversible presentation-state mechanisms
           |
           v
Icod.DCurses
    cells + windows + screen diff/refresh
           |
           v
Icod.ProcPs
    watch / slabtop / top
```

At that point the Icod ecosystem has a clean separation between:

1. terminal description;
2. live terminal operation;
3. virtual-screen presentation;
4. application behavior;
5. optional future pseudo-terminal process hosting.

That separation is the principal architectural deliverable of `Icod.Terminal 0.1.0`.
