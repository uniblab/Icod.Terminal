# Icod.Terminal 0.2.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Repository:** `https://github.com/uniblab/Icod.Terminal`  
**Release line:** `0.2.0`  
**Predecessor:** `0.1.0` — released as `v0.1.0`  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`
**Language:** C# 13  
**Runtime dependencies:** `Icod.TermInfo 1.0.0`; `Icod.Timing 1.0.0`  
**Primary integration consumer:** `Icod.DCurses`  
**Theme:** Rich terminal input events and reversible input-protocol control  
**Stable contract target:** `1.0.0`
**Current development version:** `0.2.0-alpha.1`
**Status:** T13 foundation and contract reset current
**Current tranche:** T13 — 0.2 foundation and contract reset

---

## 1. Release Objective

`Icod.Terminal 0.2.0` SHALL extend the live-terminal substrate established by
`0.1.0` from keyboard-and-lifecycle input into a richer, still bounded and
deterministic input model.

The release SHALL add the reusable mechanisms required for:

- mouse input;
- focus-in/focus-out reporting;
- bracketed paste;
- richer key modifiers and traditional modified-key decoding;
- configurable input-decoder policy where callers reasonably need control;
- reversible enablement of terminal input protocols;
- clean `Icod.DCurses` integration without private escape-sequence readers or
  private terminal-state management.

The `0.2.0` work SHALL build directly on the existing `TerminalSession`,
`TerminalInputDecoder`, `TerminalInputEvent`, `TerminalEvent`, lifecycle model,
and presentation/session ownership contracts. It SHALL NOT introduce a second
input loop or a parallel terminal-event subsystem.

---

## 2. Architectural Continuity from 0.1

The architectural boundaries established by `0.1.0` remain in force.

### 2.1 `Icod.TermInfo`

`Icod.TermInfo` remains the immutable terminal-capability authority.

`Icod.Terminal` SHOULD use terminfo capabilities where appropriate for
traditional key decoding and terminal state transitions. It SHALL NOT grow a
second general-purpose terminal-capability database.

### 2.2 `Icod.Terminal`

`Icod.Terminal` owns live terminal state and transport behavior, including:

- terminal input-mode state;
- incremental byte decoding;
- lifecycle coordination;
- input protocol enablement and disablement;
- restoration after normal exit, failure, cancellation, and suspension;
- conversion from terminal protocol bytes into platform-neutral events.

### 2.3 `Icod.DCurses`

`Icod.DCurses` owns presentation policy and higher-level TUI behavior.

It MAY decide that a curses session wants mouse tracking, focus reporting, or
bracketed paste, but it SHOULD request those capabilities through
`Icod.Terminal`. It SHALL NOT emit private enable/disable sequences or maintain
a competing mouse/paste/focus parser.

### 2.4 `Icod.Pty`

PTY/ConPTY creation and child-process hosting remain outside the
`Icod.Terminal 0.2.0` runtime contract.

---

## 3. Core 0.2 Design Rules

### 3.1 One incremental decoder

All keyboard, mouse, focus, paste, and traditional terminal-input protocol
decoding SHALL pass through the existing incremental input machinery.

There SHALL NOT be:

- a second reader thread for mouse input;
- a special paste reader;
- a focus-report side channel;
- a protocol-specific global input loop.

### 3.2 Bounded buffering

The decoder SHALL remain bounded.

Large bracketed pastes SHALL NOT require the complete paste payload to be held
in one internal buffer before the application can receive data.

### 3.3 Protocol enablement is reversible terminal state

Mouse tracking, focus reporting, and bracketed-paste reporting alter terminal
state. Their enablement therefore belongs to the same ownership/restoration
model as other reversible terminal transitions.

The library SHALL define deterministic behavior for:

- repeated acquisition;
- nested acquisition;
- release ordering;
- partial enablement failure;
- session disposal;
- suspend/resume;
- state invalidation and re-entry.

### 3.4 Mechanism below, policy above

`Icod.Terminal` SHALL provide the mechanisms to enable, disable, decode, and
represent rich input.

Consumers such as `Icod.DCurses` SHALL decide whether those mechanisms are
desired for a particular application/session.

### 3.5 No premature query router

Terminal query/response correlation remains the `0.3.0` milestone.

`0.2.0` SHALL not grow a general response router merely to support mouse,
focus, paste, or traditional modified-key input.

### 3.6 No modern keyboard negotiation yet

CSI-u, Kitty keyboard protocol negotiation, and similar modern keyboard
protocols remain scheduled for the later modern-keyboard milestone.

`0.2.0` SHALL improve traditional keyboard decoding without pulling the
`0.5.0` negotiation work forward.

---

## 4. Proposed Public Event Direction

The existing `TerminalEvent` remains the outer session event envelope:

```text
TerminalEvent
    Input
    Lifecycle
    Timeout
    Cancelled
```

Rich terminal input remains under `TerminalInputEvent`.

The `TerminalInputEventKind` direction for `0.2.0` is:

```text
Text
Key
Mouse
Focus
Paste
EndOfInput
```

The new kinds SHOULD expose typed payloads rather than encoding protocol
details into generic strings or integer arrays.

### 4.1 Mouse event direction

A mouse event SHOULD represent at least:

- action: press, release, move, wheel;
- button identity where applicable;
- cell coordinates;
- keyboard modifiers;
- whether motion was reported with or without a pressed button where the
  protocol distinguishes it.

The public event SHOULD expose normalized terminal-cell coordinates and hide
wire-format offsets and escape encoding.

The recommended public coordinate convention is zero-based column/row
coordinates because they compose naturally with managed buffers and virtual
screen models. Protocol-specific one-based coordinates SHALL be normalized by
the decoder.

### 4.2 Focus event direction

Focus reporting SHOULD become a simple typed event with an explicit state:

```text
FocusIn
FocusOut
```

No consumer should need to recognize the raw focus sequences.

### 4.3 Paste event direction

Bracketed paste SHOULD be modeled as framed input rather than one unbounded
string.

The preferred design is a small typed paste event carrying a phase such as:

```text
Begin
Data
End
```

`Data` events SHOULD carry bounded chunks of decoded application text or bytes
according to the finalized T14 contract.

While bracketed paste is active, terminal-looking escape sequences inside the
paste body SHALL remain paste data unless they are the exact paste terminator.

---

## 5. Version and Tranche Plan

| Tranche | Development version | Theme | Principal outcome |
| --- | --- | --- | --- |
| T13 | `0.2.0-alpha.1` | 0.2 foundation and contract reset | Close 0.1, establish the 0.2 line, remove 0.1-specific validation assumptions |
| T14 | `0.2.0-alpha.2` | Rich input event model and decoder policy | Freeze the public rich-input contract and decoder configuration model |
| T15 | `0.2.0-alpha.3` | Reversible input-protocol leases | Session-owned mouse/focus/paste enablement with restoration |
| T16 | `0.2.0-alpha.4` | Focus and bracketed paste | Incremental focus and streaming paste decoding |
| T17 | `0.2.0-alpha.5` | Mouse input | SGR mouse plus compatibility decoding and normalized mouse events |
| T18 | `0.2.0-alpha.6` | Traditional keyboard completeness | Richer modifiers and modified navigation/function-key coverage |
| T19 | `0.2.0-alpha.7` | DCurses integration and acceptance | DCurses consumes rich input without private terminal protocol machinery |
| T20 | `0.2.0` | API/package/release gate | API audit, package validation, final three-host release and tag publication |

Development versions MAY gain additional alpha increments if a tranche needs
more than one publishable iteration. The tranche ordering is architectural, not
a requirement that every tranche map to exactly one prerelease package.

---

# 6. T13 — 0.2 Foundation and Contract Reset

T13 begins the new development line without adding rich-input behavior.

Required work:

- record `0.1.0` / T12D as released and complete;
- make `0.2.0` the active roadmap target;
- set:
  - `<Version>0.2.0-alpha.1</Version>`;
  - `<PackageVersion>0.2.0-alpha.1</PackageVersion>`;
  - `<AssemblyVersion>0.2.0.0</AssemblyVersion>`;
- update package release notes for the 0.2 foundation;
- preserve the `net8.0;net9.0;net10.0` framework set;
- retain `Icod.TermInfo 1.0.0` and `Icod.Timing 1.0.0` unless a concrete
  implementation requirement justifies a dependency update;
- make the package verifier derive or validate the expected assembly version
  from project metadata rather than hard-coding `0.1.0.0`;
- update README status for 0.2 development;
- add a `0.2.0` roadmap/completion record if the repository keeps milestone
  documents separately.

**Gate T13:** clean/restore/build/test/pack and package validation succeed for
`0.2.0-alpha.1`, with no runtime behavior change and no remaining
`0.1.0`-specific release assumptions in the validation tooling.

**T13 implementation record:** [`docs/T13-0.2-Foundation-and-Contract-Reset.md`](docs/T13-0.2-Foundation-and-Contract-Reset.md).

---

# 7. T14 — Rich Input Event Model and Decoder Policy

T14 SHALL define the public model before adding protocol implementations.

Required work:

- extend `TerminalInputEventKind` for mouse, focus, and paste;
- introduce strongly typed public payload contracts;
- define normalized mouse action/button/modifier/coordinate semantics;
- define focus event semantics;
- define bracketed-paste framing semantics;
- decide whether paste `Data` carries `ReadOnlyMemory<byte>`, decoded text,
  runes, or a dedicated immutable chunk representation;
- define how paste interacts with application encoding;
- define rich modifier semantics for traditional keys;
- define decoder policy/configuration required by 0.2;
- make Escape ambiguity configurable only if doing so does not undermine
  deterministic defaults;
- define maximum retained prefix/protocol buffer limits;
- define unknown/unsupported protocol-sequence behavior;
- preserve backward compatibility for existing `Text`, `Key`, and
  `EndOfInput` consumers where practical.

Recommended decoder-policy candidates:

- Escape ambiguity interval;
- maximum protocol-prefix buffering;
- paste chunk size;
- mouse protocol preference where more than one can be enabled;
- whether unsupported rich protocol frames are ignored, surfaced, or treated as
  literal input.

The API SHOULD avoid exposing low-level protocol escape strings as normal
configuration.

**Gate T14:** the rich-input public contract is documented, unit-testable, and
does not require any protocol-specific reader outside the existing session
input path.

---

# 8. T15 — Reversible Input-Protocol Leases

T15 SHALL add terminal-state ownership for rich-input reporting.

The implementation SHALL support controlled acquisition for:

- bracketed-paste reporting;
- focus reporting;
- mouse reporting;
- specific mouse tracking intensity/mode where supported.

Mouse tracking policy SHOULD distinguish at least:

- button events only;
- button-motion tracking;
- any-motion tracking.

Protocol/state selection SHOULD prefer the best supported mechanism without
silently claiming support the terminal description cannot justify.

The implementation SHALL define:

- nested/repeated acquisitions;
- compatibility between different requested mouse tracking levels;
- deterministic downgrade when a stronger inner lease is released;
- exact release ordering;
- rollback after partial enablement;
- suspend preparation;
- resume re-entry;
- disposal restoration.

If terminfo provides a suitable capability, it SHOULD be used. Where a widely
standardized terminal protocol is not representable by the current terminfo
surface, any explicit built-in protocol sequence MUST be narrowly scoped,
documented, and associated with an explicit capability/profile decision rather
than emitted for every terminal.

**Gate T15:** in-memory tests prove exact enable/disable ordering, nesting,
partial-failure rollback, suspend/resume re-entry, and final restoration for
all rich input protocols.

---

# 9. T16 — Focus and Bracketed Paste

T16 SHALL implement the simpler framed rich-input protocols first.

## 9.1 Focus

Implement incremental decoding of focus-in and focus-out reports.

Tests SHALL cover:

- complete frame in one read;
- byte-by-byte fragmentation;
- adjacent focus events;
- focus events adjacent to text/key events;
- cancellation and timeout boundaries around partial frames.

## 9.2 Bracketed paste

Implement bracketed-paste start and end framing without unbounded buffering.

The decoder SHALL:

- recognize a fragmented paste-begin marker;
- enter a dedicated paste state;
- emit bounded paste data while inside the frame;
- preserve escape-looking bytes as paste content unless they complete the exact
  paste terminator;
- recognize a fragmented paste-end marker;
- recover deterministically from end-of-input while paste is active;
- define behavior when the maximum pending terminator-prefix buffer is reached;
- preserve ordering relative to ordinary keyboard/lifecycle events.

The implementation SHOULD avoid translating paste data into synthetic
keystrokes. Paste is semantically distinct input.

**Gate T16:** tests cover arbitrarily fragmented markers, embedded escape
sequences, multi-chunk payloads, Unicode boundaries, cancellation, timeout,
end-of-input, and bounded-memory behavior.

---

# 10. T17 — Mouse Input

T17 SHALL add normalized terminal mouse events.

## 10.1 Protocol priority

The primary protocol SHOULD be SGR mouse reporting because it avoids the
coordinate limitations and ambiguity of older encodings.

Compatibility support SHOULD include the traditional X10-style form where it is
useful for terminals/profiles which do not provide SGR reporting.

Additional historical encodings MAY be added only when they materially improve
compatibility and do not complicate the public event model.

## 10.2 Semantic event model

The decoder SHALL normalize wire protocol into stable semantic fields.

Mouse events SHOULD represent:

- press;
- release;
- movement;
- wheel up/down and, where distinguishable, horizontal wheel directions;
- primary/secondary/middle buttons;
- additional buttons when the protocol exposes them cleanly;
- Shift/Control/Alt modifiers where represented;
- normalized terminal-cell coordinates.

The event model SHALL distinguish a movement report from a button press or
release rather than forcing callers to infer action from button codes.

## 10.3 Validation

Tests SHALL cover:

- one-frame and fragmented decoding;
- multiple frames per read;
- boundary coordinates;
- malformed frames;
- modifier combinations;
- wheel events;
- motion with and without pressed buttons;
- old-protocol coordinate limits;
- SGR large coordinates;
- interaction with Escape ambiguity;
- interleaving with text, keys, focus, and paste.

**Gate T17:** a single session input stream can deliver keyboard, mouse, focus,
paste, and lifecycle events in deterministic order without private consumer
parsers.

---

# 11. T18 — Traditional Keyboard Completeness

T18 SHALL improve the traditional keyboard path without introducing modern
keyboard negotiation.

Required work SHOULD include:

- modified arrow keys;
- modified Home/End;
- modified Page Up/Page Down;
- modified Insert/Delete;
- broader function-key coverage;
- Shift/Ctrl/Alt combinations represented by traditional CSI/SS3 forms;
- additional terminfo key capabilities where available;
- compatibility fixtures for common xterm-family, Linux console, Windows
  terminal, and other supported profiles.

The decoder SHALL continue to prefer terminal-description data for traditional
named key sequences when the information is available.

The implementation SHALL avoid interpreting every unknown CSI sequence as a
keyboard event. Unknown protocol data must remain bounded and deterministic.

**Gate T18:** modified traditional keys decode consistently across fragmented
input and profile fixtures without regressing the 0.1 key contract.

---

# 12. T19 — `Icod.DCurses` Integration and Rich-Input Acceptance

T19 SHALL prove that the rich-input contract is suitable for the next layer.

`Icod.DCurses` SHOULD be able to:

- request mouse tracking through `Icod.Terminal`;
- receive normalized mouse events;
- request focus reporting;
- receive focus events;
- request bracketed paste;
- receive framed paste events;
- receive richer modified-key events;
- suspend/resume without leaving rich-input protocols enabled incorrectly;
- dispose without leaking terminal protocol state.

`Icod.DCurses` MAY translate Terminal events into curses-shaped events, but it
SHALL NOT:

- maintain a private mouse escape parser;
- maintain a private bracketed-paste reader;
- emit its own mouse/focus/paste enable/disable sequences;
- install a second terminal input loop.

Acceptance SHOULD include at least one small DCurses demonstration or test
consumer for each new event family.

**Gate T19:** DCurses consumes all 0.2 rich-input mechanisms through
`Icod.Terminal`, and disabling/removing Terminal support would leave no hidden
parallel terminal protocol implementation in DCurses.

---

# 13. T20 — 0.2 API, Package, and Release Gate

T20 closes the release in the same disciplined manner as 0.1.

Required work:

- perform a public API regret review of the new rich-input types;
- publish a `0.2` public API baseline;
- update README examples for rich input;
- document protocol enablement ownership and restoration;
- document mouse coordinate conventions;
- document paste framing/chunking semantics;
- document default decoder policy and configurable policy;
- extend package-smoke validation to exercise at least one rich-input event of
  each new family using injected input;
- verify package structure, XML docs, symbols, Source Link, and dependency
  closure;
- run Release validation on Windows, Linux, and macOS;
- set:
  - `<Version>0.2.0</Version>`;
  - `<PackageVersion>0.2.0</PackageVersion>`;
  - `<AssemblyVersion>0.2.0.0</AssemblyVersion>`;
- update final package release notes;
- publish only via the matching `v0.2.0` tag.

**Release gate `0.2.0`:** the package provides mouse, focus, paste, and richer
traditional keyboard input through one bounded incremental session input path;
all reversible input-protocol state is owned and restored by
`Icod.Terminal`; `Icod.DCurses` consumes the new features without duplicating
terminal protocol machinery; and the final package passes the complete
three-host package-only consumer gate.

---

## 14. Testing Strategy

### 14.1 Pure decoder tests

The majority of rich-input tests SHOULD operate on scripted byte fragments and
an injected monotonic clock.

Every protocol SHALL be tested with:

- single-byte fragmentation;
- arbitrary chunk boundaries;
- several events in one read;
- partial prefixes at timeout;
- cancellation;
- end-of-input;
- malformed or unsupported frames;
- maximum buffer boundaries.

### 14.2 Protocol-state tests

Input protocol leases SHALL use fake/in-memory output backends to verify exact
terminal bytes and ordering.

Tests SHALL cover:

- acquire;
- nested acquire;
- release;
- partial failure;
- session disposal;
- suspend;
- resume;
- invalidation/re-entry.

### 14.3 Platform tests

No ordinary unit test SHALL require a real interactive terminal.

Any host-terminal integration tests SHALL remain isolated and SHALL guarantee
restoration.

### 14.4 Package-only consumer tests

The T12C fresh-consumer model SHALL continue through 0.2.

The package-only consumer SHOULD exercise rich input using injected transports
so that Windows, Linux, and macOS CI can validate identical semantics without
needing an interactive runner terminal.

---

## 15. Resource and Safety Constraints

The 0.2 decoder SHALL remain resistant to hostile or accidental input streams.

At minimum:

- all protocol-prefix buffers are bounded;
- numeric CSI/SGR fields have explicit parsing limits;
- absurd mouse coordinates fail or clamp according to a documented rule rather
  than overflowing;
- paste processing does not accumulate unbounded payloads;
- malformed frames do not cause infinite prefix retention;
- cancellation does not leak protocol/session state;
- unknown sequences cannot force an unbounded retry loop.

---

## 16. Compatibility Policy

`0.2.0` remains pre-1.0, but compatibility with `0.1.0` consumers SHOULD be
preserved unless a concrete defect requires correction.

Existing applications which only consume:

- `TerminalEventKind.Input`;
- `TerminalInputEventKind.Text`;
- `TerminalInputEventKind.Key`;
- `TerminalInputEventKind.EndOfInput`;
- the existing `TerminalKey` values;
- the existing `TerminalKeyModifiers` values;

SHOULD continue to compile and behave as before.

Adding new enum values means consumers using exhaustive switches must be
prepared for new cases. This SHALL be called out clearly in the `0.2.0`
release notes and migration documentation.

---

## 17. Explicitly Deferred Beyond 0.2

The following remain outside the `0.2.0` release gate:

- active terminal query/response routing;
- device-attribute and status queries;
- cursor-position request correlation;
- DECRQSS;
- XTGETTCAP request routing;
- OSC 8 hyperlinks as a session protocol feature;
- OSC 52 clipboard/selection operations;
- synchronized-output protocol;
- Kitty/CSI-u keyboard negotiation;
- graphics protocols;
- PTY/ConPTY creation and child-process hosting;
- general terminal emulation.

These belong to later roadmap milestones unless a concrete blocker requires the
roadmap to be deliberately revised.

---

## 18. Completion Definition

`Icod.Terminal 0.2.0` is complete when:

1. rich input is represented through one coherent public event model;
2. mouse, focus, and bracketed paste are incrementally decoded;
3. paste buffering remains bounded;
4. richer traditional modifier/key combinations are supported;
5. rich input protocol enablement is reversible session-owned terminal state;
6. suspend/resume and disposal restore that state deterministically;
7. `Icod.DCurses` consumes the new mechanisms without private protocol readers;
8. package-only consumers succeed for `net8.0`, `net9.0`, and `net10.0`;
9. Windows, Linux, and macOS Release validation is green;
10. the final package is published only by the `v0.2.0` tag-controlled release
    workflow.

At that point the project proceeds to `0.3.0`, whose principal theme remains
active terminal query/response routing.
