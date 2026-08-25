# T02 — Extraction Inventory and Contract Classification

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.2`
**Tranche:** T02 — Extraction inventory and contract classification
**Reference branch:** `Icod.Terminal/initial_add`
**Reference date:** 2026-08-24
**Implementation status:** Contract/classification tranche; no terminal-control implementation moves in T02

---

## 1. Purpose

T02 freezes the ownership boundary before terminal-control implementation is copied, rewritten, or removed.

The existing terminal work is spread across three packages:

- `Icod.TermInfo` already owns immutable terminal descriptions, capability expansion, terminal-profile discovery, terminal-size value semantics, and Windows virtual-terminal support;
- `Icod.CommandFramework.Terminal` contains a substantial neutral terminal-control substrate together with command/presentation policy which does not all belong in a terminal runtime library;
- `Icod.DCurses` contains a second terminal backend abstraction plus live-session mode/lifecycle logic introduced to make curses development possible before `Icod.Terminal` existed.

The T02 objective is not to preserve those accidental boundaries. It is to identify the canonical future owner for each behavior while preserving compatibility long enough for consumers to migrate safely.

The target dependency graph is:

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
Icod.ProcPs
```

The steady-state `Icod.Terminal` runtime SHALL NOT depend on `Icod.CommandFramework`, `Icod.DCurses`, `Icod.ProcPs`, or `Icod.Pty`.

---

## 2. Classification Legend

Every audited item receives one of the following classifications.

| Code | Classification | Meaning |
| --- | --- | --- |
| **M** | Move/generalize | The behavior belongs canonically in `Icod.Terminal`. The implementation may be adapted rather than copied literally. |
| **CF** | CommandFramework | The behavior remains command/presentation policy in `Icod.CommandFramework`. |
| **DC** | DCurses | The behavior remains curses/virtual-screen/presentation policy in `Icod.DCurses`. |
| **A** | Compatibility adapter | The existing public surface may remain temporarily, but should delegate to canonical `Icod.Terminal` behavior. |
| **R** | Retire after migration | The type/behavior is duplicate or transitional and should disappear only after its consumers have migrated. |

An entry may carry more than one code when a source file must be split.

---

## 3. Cross-Library Decisions Frozen by T02

### 3.1 `Icod.TermInfo` remains a dependency, not a source tree to absorb

`Icod.Terminal` SHALL consume `Icod.TermInfo 1.x`.

It SHALL NOT copy or fork:

- `TerminalDescription`;
- `TerminalDatabase`;
- built-in terminal profiles;
- terminfo capability metadata;
- parameter expansion;
- terminfo output/padding logic;
- terminal color capability semantics;
- Windows virtual-terminal enablement helpers.

Live session orchestration belongs in `Icod.Terminal`, but the immutable description and capability machinery remains in `Icod.TermInfo`.

### 3.2 Reuse `Icod.TermInfo.TerminalSize`

`Icod.TermInfo` already exposes the public immutable value type:

```csharp
Icod.TermInfo.TerminalSize
```

with `Columns` and `Rows`.

T02 therefore rejects creation of a third equivalent size struct in `Icod.Terminal`.

For the `0.1.x` line:

- `Icod.Terminal` SHALL use `Icod.TermInfo.TerminalSize` as the canonical public terminal-size value;
- `Icod.Terminal` SHALL own *live size observation, endpoint association, resize notification, and session invalidation*;
- `Icod.DCurses.Terminal.TerminalSize` SHALL be retired during DCurses migration;
- `Icod.CommandFramework.Terminal.TerminalDimensions` SHALL remain only as compatibility/presentation surface until CommandFramework consumers migrate.

This is a refinement of the provisional `TerminalSize` item in the master roadmap's public-API sketch.

### 3.3 Reuse the existing Windows VT lease

`Icod.TermInfo.WindowsVirtualTerminal` and its lease already provide reversible Windows virtual-terminal output enablement.

`Icod.Terminal` SHALL consume or wrap that helper in session/output setup. It SHALL NOT introduce a second independent implementation of `GetConsoleMode`/`SetConsoleMode` solely for enabling VT output.

Windows console input/output *mode snapshots and semantic input modes* still belong in `Icod.Terminal`; the specific TermInfo helper remains the authority for the already-published VT-output lease.

### 3.4 Preserve low-level native snapshots, hide them from ordinary session use

Complete native terminal snapshots are useful for:

- `stty`-class diagnostics and restoration;
- exact round-trip restoration;
- platform testing;
- advanced callers.

They therefore remain part of the lower-level Terminal control contract.

Ordinary curses and ProcPs consumers SHALL use semantic APIs such as canonical/cbreak/raw and echo policy rather than manipulating native flag values.

### 3.5 CommandFramework compatibility is migration work, not an immediate deletion target

`Icod.CommandFramework.Terminal` already exposes public types. T02 does not require an immediate breaking removal of those types.

The compatibility direction is:

```text
existing CommandFramework public surface
                 |
                 v
       compatibility adapter
                 |
                 v
          Icod.Terminal
```

Where a CommandFramework type represents command policy, it remains there permanently.

Where it duplicates the canonical live-terminal substrate, CommandFramework MAY later:

- delegate to `Icod.Terminal`;
- mark the old surface obsolete in an appropriate release;
- remove it only as part of a deliberate compatibility/major-version decision.

`Icod.Terminal 0.1.0` SHALL NOT be blocked on removing the old CommandFramework namespace.

---

## 4. `Icod.CommandFramework.Terminal` Extraction Matrix

### 4.1 `TerminalControlContracts.cs`

This file contains the strongest seed for T03.

| Existing type/behavior | Classification | Target / decision |
| --- | --- | --- |
| `TerminalControlStatus` | **M** | Canonical `Icod.Terminal.TerminalControlStatus`. Preserve `Available`, `Unavailable`, `Unsupported`, `Failed`. |
| `TerminalPlatformKind` | **M** | Canonical low-level platform discriminator in `Icod.Terminal`. Preserve POSIX versus Windows distinction. |
| `TerminalEndpointKind` | **M** | Canonical endpoint-control target discriminator. |
| `TerminalModeApplyTiming` | **M** | Canonical low-level mode-application timing contract. |
| `TerminalConsoleDirection` | **M** | Retain as lower-level Windows snapshot metadata. |
| `TerminalControlCapabilities` | **M** | Move/generalize. T03 SHOULD add explicit live-size capability rather than relying on unrelated presentation code. |
| `TerminalEndpoint` | **M** | Canonical low-level endpoint/control target. Preserve standard input/output/error and explicit descriptor/path creation. |
| `TerminalControlResult<T>` | **M** | Canonical controlled query-result contract. |
| `TerminalControlMutationResult` | **M** | Canonical controlled mutation-result contract. |
| `TerminalEndpointObservation` | **M** | Canonical attachment/path/platform/capability observation. |
| `TerminalSpeed` | **M** | Preserve native speed code plus optional recognized baud value. |
| `TerminalModeSnapshot` | **M** | Preserve complete POSIX/Windows snapshot semantics and exact-host metadata. |
| `ITerminalControlProvider` | **M** | Canonical low-level provider seam. T03 may extend it with live-size observation. |

The existing validation rules in these types are compatibility requirements for T03 unless a documented design improvement replaces them.

### 4.2 `SystemTerminalControlProvider.cs`

**Classification: M**

The operating-system dispatch belongs in `Icod.Terminal`.

Target public name:

```csharp
Icod.Terminal.SystemTerminalControlProvider
```

The system provider SHALL select:

- Windows implementation on Windows;
- POSIX implementation on Linux/macOS;
- a controlled unsupported provider elsewhere.

The implementation SHALL remain injectable through `ITerminalControlProvider`; ordinary tests must not require process-global terminal mutation.

### 4.3 `UnixTerminalControlProvider.cs`

**Classification: M**

The behavior belongs wholly in `Icod.Terminal`:

- `isatty`;
- tty pathname discovery;
- terminal-device acquisition;
- complete Linux/macOS termios capture;
- native speed preservation;
- native control-character preservation;
- mode application timing;
- exact POSIX restoration;
- native error translation.

The implementation name SHALL change from `UnixTerminalControlProvider` to:

```text
PosixTerminalControlProvider
```

because the implementation specifically models POSIX termios semantics and currently supports Linux/macOS. `Unix` is broader than the actual contract.

The provider SHOULD remain internal; callers should normally use `SystemTerminalControlProvider` or an injected `ITerminalControlProvider`.

### 4.4 `WindowsTerminalControlProvider.cs`

**Classification: M**

The behavior belongs wholly in `Icod.Terminal`:

- standard descriptor to Windows handle resolution;
- `CONIN$` / `CONOUT$` handling;
- console attachment observation;
- input/output direction detection;
- complete `GetConsoleMode` snapshots;
- exact `SetConsoleMode` restoration;
- controlled non-console/unsupported/failure results.

Target implementation name:

```text
WindowsTerminalControlProvider
```

The provider SHOULD remain internal behind the system provider.

Windows SHALL continue not to synthesize POSIX baud rates, control-character arrays, drain semantics, or line disciplines.

### 4.5 `TerminalModeCodec.cs`

This file is split.

| Existing behavior | Classification | Target / decision |
| --- | --- | --- |
| `TerminalModeCodec` | **M** | Move to `Icod.Terminal`; preserve stable machine-readable POSIX and Windows round-trip behavior. |
| `TerminalControlCharacterFormatter` | **CF** | Remain with CommandFramework/`stty` policy because its visible notation is explicitly GNU-facing presentation behavior. |

The machine codec is useful terminal-state infrastructure. Human-readable GNU control-character formatting is command policy.

### 4.6 `TerminalContracts.cs`

This file mixes generic observation with CommandFramework presentation infrastructure and SHALL NOT move wholesale.

| Existing type/behavior | Classification | Target / decision |
| --- | --- | --- |
| `TerminalStreamKind` | **CF/A** | Keep for CommandFramework standard-stream presentation compatibility; adapters may map it to Terminal standard endpoints. |
| `TerminalProbeStatus` | **CF/A** | Keep only as presentation compatibility if needed; canonical low-level status is `TerminalControlStatus` plus endpoint observation. |
| `TerminalDimensionSource` | **CF** | Remains CommandFramework fallback/provenance policy. |
| `TerminalDimensions` | **A/R** | Adapt to `Icod.TermInfo.TerminalSize`; do not recreate it in Terminal. |
| `TerminalDeviceObservation` | **A** | May remain as a presentation adapter over `Icod.Terminal` observation/size data. |
| `ITerminalDeviceProvider` | **A** | May remain as a CommandFramework test seam implemented through Terminal. |
| `SystemTerminalDeviceProvider` | **A** | Should ultimately delegate to `Icod.Terminal`, not perform a second terminal probe. |
| `IEnvironmentVariableProvider` | **CF** | Remains a command/presentation environment abstraction. |
| `SystemEnvironmentVariableProvider` | **CF** | Remains with the preceding abstraction. |

### 4.7 `TerminalEnvironment.cs`

**Classification: CF**

`TerminalEnvironmentSnapshot` captures more than terminal runtime identity:

- `TERM`;
- `COLORTERM`;
- `COLUMNS`;
- `LINES`;
- `SHELL`;
- `QUOTING_STYLE`;
- derived directory-listing terminal names.

That is CommandFramework presentation policy.

`Icod.Terminal` may independently read the small environment subset required to select a live terminal description, but it SHALL NOT adopt `SHELL`, `QUOTING_STYLE`, or directory-listing policy merely to reuse this type.

### 4.8 `TerminalPresentationProvider.cs`

**Classification: CF/A**

The fallback order `COLUMNS`/`LINES` -> live terminal -> configured fallback is command presentation policy and remains in CommandFramework.

However, the live-terminal attachment/size portion SHOULD eventually be supplied by an adapter over `Icod.Terminal` rather than by an independent system probe.

`TerminalPresentationOptions`, `TerminalPresentationSnapshot`, dimension provenance, and command fallback semantics remain CommandFramework concerns.

### 4.9 `TerminalColorPolicy.cs`

**Classification: CF**

The following remain CommandFramework policy:

- `never` / `auto` / `always`;
- attachment-sensitive color enabling;
- command-facing fallback behavior;
- heuristic environment interpretation used by existing command consumers.

`Icod.TermInfo` remains the canonical capability source for terminal color semantics. A later CommandFramework cleanup MAY use richer TermInfo/Terminal data instead of heuristics, but `Icod.Terminal` shall not absorb GNU command color policy.

### 4.10 `FileNamePresentation.cs`

**Classification: CF**

Filename quoting, shell/C quoting styles, `QUOTING_STYLE`, control-character replacement, and directory-listing filename presentation are not live-terminal substrate.

No portion of this file moves into `Icod.Terminal` as part of T03/T04.

---

## 5. `Icod.DCurses` Extraction Matrix

DCurses is pre-1.0 and currently exposes several terminal-backend types publicly. T02 treats those types as transitional unless they represent genuine curses policy.

### 5.1 `TerminalBackendContracts.cs`

| Existing type/behavior | Classification | Target / decision |
| --- | --- | --- |
| `TerminalBackendStatus` | **R** | Replace with canonical Terminal controlled-result semantics. |
| `TerminalBackendResult<T>` | **R** | Replace with Terminal query/result contracts or higher-level session exceptions/results as appropriate. |
| `TerminalBackendMutationResult` | **R** | Replace with Terminal mutation result. |
| DCurses `TerminalEndpoint` | **R** | Replace with Terminal endpoint/observation metadata. |
| DCurses `TerminalSize` | **R** | Replace with `Icod.TermInfo.TerminalSize`. |
| DCurses `TerminalModeApplyTiming` | **R** | Replace with `Icod.Terminal.TerminalModeApplyTiming`. |
| `ITerminalModeState` | **R** | Terminal session/control layer owns captured baseline state. |
| `ITerminalInput` | **M** | Generalize into `Icod.Terminal`; this is live-terminal byte transport. |
| `ITerminalOutput` | **M** | Generalize into `Icod.Terminal`; this is live-terminal byte transport. |
| `ITerminalDimensionProvider` | **R/M** | Absorb the concept into Terminal live-size/session infrastructure; do not copy this exact public interface automatically. |
| `ITerminalModeController` | **R/M** | Split between Terminal low-level control provider and semantic session mode handling. |
| `TerminalBackend` | **R** | Replace as the owning runtime abstraction with `TerminalSession`. DCurses may keep a narrow internal test adapter if needed. |

DCurses SHOULD NOT maintain a second generic terminal result/status/endpoint stack once Terminal provides the canonical one.

### 5.2 `TerminalSessionModeContracts.cs`

**Classification: M/R**

`ITerminalSessionModeController` represents a real Terminal responsibility, but the exact curses-shaped interface is not canonical.

The semantic behavior moves to `Icod.Terminal` T04 through concepts equivalent to:

```csharp
TerminalInputMode.Canonical
TerminalInputMode.CBreak
TerminalInputMode.Raw
echo enabled / disabled
```

`CursesInputMode` MAY remain temporarily as a DCurses compatibility façade, but it must map to Terminal semantics rather than edit native state itself.

### 5.3 `SystemTerminalModeEditor.cs`

**Classification: M**

All native flag knowledge in this file moves out of DCurses.

That includes:

- Linux/macOS input flag manipulation;
- output post-processing changes;
- character-size/parity changes;
- `ISIG`, `ICANON`, `IEXTEN`, echo-family behavior;
- VMIN/VTIME placement differences;
- Windows processed/line/echo/VT-input bits;
- semantic canonical/cbreak/raw mapping.

The type itself need not survive.

T04 SHALL implement the semantics in Terminal using platform-local code so public consumers never need these numeric constants.

### 5.4 `SystemTerminalBackendFactory.cs`

This file is split.

| Existing behavior | Classification | Target / decision |
| --- | --- | --- |
| Standard input/output stream wrappers | **M** | Terminal live I/O/session infrastructure. |
| Interactive attachment detection | **M** | Terminal endpoint observation. |
| Terminal description resolution | **M** orchestration | Terminal session uses `Icod.TermInfo`; TermInfo remains the data authority. |
| Live size query | **M** | Terminal owns live observation; canonical value type remains `Icod.TermInfo.TerminalSize`. |
| Mode capture/apply/restore adapter | **M** | Terminal T03/T04/T05. |
| Windows VT output lease acquisition | **M** orchestration | Terminal session/output setup, reusing TermInfo helper. |
| DCurses decision to create a curses presentation | **DC** | Remains in `CursesSession`; implementation should become a thin call into Terminal session creation. |

The current `SystemTerminalModeState` wrapper becomes unnecessary once Terminal owns baseline state directly.

### 5.5 `TerminalLifecycleContracts.cs` and `SystemTerminalLifecycleSource.cs`

**Classification: M/DC**

Host lifecycle *mechanism* belongs in Terminal:

- `SIGWINCH`;
- `SIGCONT`;
- `SIGTSTP` interception/re-delivery;
- `SIGINT`;
- `SIGTERM`;
- `SIGQUIT`;
- `SIGHUP`;
- Windows `Console.CancelKeyPress`;
- safe queued handoff from restricted callback context.

Curses *reaction policy* remains DCurses:

- invalidate/repaint after resize/resume;
- leave/re-enter curses presentation;
- convert Terminal lifecycle notifications into `CursesLifecycleEvent`;
- decide what application-facing event is raised.

The low-level types `TerminalLifecycleSignalKind`, `TerminalLifecycleSignal`, `ITerminalLifecycleSource`, and `ITerminalSuspendController` are transitional DCurses implementation details and SHOULD be replaced by Terminal equivalents in T07/T10 rather than copied verbatim in T03.

### 5.6 `CursesSession` and related partials

**Classification: DC with Terminal adapters**

`CursesSession` remains a DCurses type.

Its eventual responsibilities are:

- own curses virtual/presentation state;
- select curses policy/options;
- acquire Terminal presentation leases;
- translate Terminal input/lifecycle events into curses events;
- coordinate repaint and virtual-screen invalidation.

The following current responsibilities migrate downward:

- native terminal-mode capture;
- semantic raw/cbreak/canonical mutation;
- host-mode restoration;
- raw standard-stream transport construction;
- platform attachment detection;
- live size probing;
- host lifecycle registration;
- Windows VT-output setup.

Direct use of terminfo capabilities for *curses refresh/presentation strategy* may remain in DCurses. Reversible device-state ownership should use Terminal leases when T09 is available.

---

## 6. Canonical Namespace and Naming Strategy

### 6.1 Public namespace

The primary public API SHALL use:

```csharp
namespace Icod.Terminal;
```

Public callers should not need to import several implementation-oriented namespaces to open and use a terminal.

Source directories may still separate concerns:

```text
src/
    Control/
    Input/
    Lifecycle/
    Platform/
        Posix/
        Windows/
    Presentation/
    Session/
```

Directory layout does not require public namespace fragmentation.

### 6.2 Platform implementation names

Platform-specific implementation types SHOULD be internal.

Preferred terminology:

```text
PosixTerminalControlProvider
WindowsTerminalControlProvider
```

Do not introduce a public `UnixTerminalControlProvider` contract. POSIX termios is the actual abstraction implemented for Linux/macOS.

### 6.3 Canonical T03 low-level names

T03 SHOULD begin with names equivalent to:

```csharp
TerminalControlStatus
TerminalControlResult<T>
TerminalControlMutationResult
TerminalPlatformKind
TerminalEndpointKind
TerminalEndpoint
TerminalEndpointObservation
TerminalControlCapabilities
TerminalModeApplyTiming
TerminalConsoleDirection
TerminalSpeed
TerminalModeSnapshot
TerminalModeCodec
ITerminalControlProvider
SystemTerminalControlProvider
```

These are lower-level building blocks. Their presence does not require `TerminalSession` callers to manipulate them directly.

### 6.4 Canonical T04 semantic names

The semantic input-discipline contract SHALL use:

```csharp
TerminalInputMode
```

with at least:

```text
Canonical
CBreak
Raw
```

Echo policy SHALL be explicit rather than encoded by callers directly into native mode bits.

### 6.5 Live I/O names

The DCurses concepts:

```csharp
ITerminalInput
ITerminalOutput
```

are suitable generic concepts and SHOULD migrate into `Icod.Terminal`.

Their exact public shape may be finalized in T05/T08, but T02 reserves the names for the live-terminal layer rather than DCurses.

### 6.6 Size naming

There SHALL be no `Icod.Terminal.TerminalSize` type in the `0.1.x` line unless a concrete incompatibility is discovered.

Public Terminal APIs returning character-cell dimensions SHALL return:

```csharp
Icod.TermInfo.TerminalSize
```

This avoids three identical size structs across TermInfo, Terminal, and DCurses.

---

## 7. T03 Contract Seed

T03 is now authorized to implement the low-level control boundary without further ownership discovery.

The initial T03 implementation SHOULD:

1. introduce the canonical low-level contracts identified in section 6.3;
2. adapt the tested CommandFramework validation and result semantics;
3. port/generalize system provider selection;
4. port/generalize POSIX termios control;
5. port/generalize Windows console-mode control;
6. add live size observation for explicit endpoints, returning `Icod.TermInfo.TerminalSize`;
7. add an explicit size-read capability to endpoint observations if the final provider design uses capability flags;
8. preserve exact mode round-trip behavior;
9. add equivalent unit/platform tests in `Icod.Terminal.Tests`;
10. keep all platform implementation types internal unless a public requirement is demonstrated;
11. keep `Icod.Terminal.csproj` free of any `Icod.CommandFramework` or `Icod.DCurses` runtime reference.

T03 SHALL NOT yet implement:

- canonical/cbreak/raw policy;
- curses session setup;
- `TerminalSession`;
- key decoding;
- lifecycle signal pumping;
- presentation leases;
- PTYs.

Those remain T04 and later.

---

## 8. Compatibility/Migration Sequence

The dependency migration SHOULD occur in this order:

```text
T02  freeze ownership/names
 |
 v
T03  Icod.Terminal low-level control parity
 |
 v
T04  Icod.Terminal semantic input modes
 |
 v
T05+ Icod.Terminal live session
 |
 v
T10  DCurses switches to Icod.Terminal
 |
 +--> CommandFramework compatibility adapters may switch internally
 |
 v
later deliberate removal of duplicate legacy surfaces
```

During this process:

- do not delete CommandFramework terminal control merely because Terminal now has an equivalent;
- do not make Terminal reference CommandFramework to ease copying;
- do not make Terminal reference DCurses;
- do not make DCurses retain raw native flag constants as a fallback;
- do not introduce PTY dependencies to solve ordinary live-terminal control.

---

## 9. T02 Completion Gate

T02 is complete when this document is accepted as the extraction contract.

The resulting ownership rules are:

1. `Icod.TermInfo` remains capability/data authority and supplies the canonical `TerminalSize` value type.
2. `Icod.Terminal` becomes the canonical live-terminal control/session layer.
3. `Icod.CommandFramework` keeps command/presentation policy and may retain compatibility adapters.
4. `Icod.DCurses` keeps virtual-screen/curses policy and sheds native terminal mechanics.
5. `Icod.Pty` remains orthogonal.
6. T03 may now implement endpoint observation/native mode parity without creating a dependency cycle.

No source implementation is moved by T02 itself.
