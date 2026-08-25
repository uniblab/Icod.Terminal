# T03 — Endpoint Observation and Native Mode Parity

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.3`
**Tranche:** T03 — Endpoint observation and native mode parity
**Reference branch:** `Icod.Terminal/initial_add`
**Reference date:** 2026-08-24
**Implementation status:** Implemented for Linux, macOS, Windows, and controlled unsupported fallback hosts

---

## 1. Purpose

T03 establishes the first canonical live-terminal implementation in `Icod.Terminal`.

The tranche moves the policy-neutral low-level terminal substrate identified by T02 into this package without introducing a runtime dependency on `Icod.CommandFramework` or `Icod.DCurses`.

T03 deliberately remains below the semantic session layer. It provides complete native state and endpoint operations, but canonical/cbreak/raw/noecho policy remains T04 work.

---

## 2. Public low-level contracts

T03 introduces the following canonical public concepts in namespace `Icod.Terminal`:

- `TerminalControlStatus`;
- `TerminalPlatformKind`;
- `TerminalEndpointKind`;
- `TerminalModeApplyTiming`;
- `TerminalConsoleDirection`;
- `TerminalControlCapabilities`;
- `TerminalEndpoint`;
- `TerminalControlResult<T>`;
- `TerminalControlMutationResult`;
- `TerminalEndpointObservation`;
- `TerminalSpeed`;
- `TerminalModeSnapshot`;
- `TerminalModeCodec`;
- `ITerminalControlProvider`;
- `SystemTerminalControlProvider`.

`ITerminalControlProvider` supplies four operations:

```csharp
TerminalControlResult<TerminalEndpointObservation> Observe(
	TerminalEndpoint endpoint
);

TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
	TerminalEndpoint endpoint
);

TerminalControlResult<TerminalModeSnapshot> GetMode(
	TerminalEndpoint endpoint
);

TerminalControlMutationResult SetMode(
	TerminalEndpoint endpoint,
	TerminalModeSnapshot mode,
	TerminalModeApplyTiming timing
);
```

`Icod.TermInfo.TerminalSize` remains the canonical public size value. `Icod.Terminal` owns the live query and endpoint association.

---

## 3. Endpoint model

`TerminalEndpoint` supports:

- standard input as file descriptor 0;
- standard output as file descriptor 1;
- standard error as file descriptor 2;
- arbitrary nonnegative process file descriptors;
- explicit terminal or console device paths.

Existing descriptors are always borrowed. A provider does not close a caller-supplied descriptor.

When a provider opens an explicit path for an operation, the provider owns and closes only that temporary handle or descriptor.

The T03 contract distinguishes:

- attached terminal;
- nonterminal/redirected endpoint;
- operation unavailable for a valid endpoint;
- platform unsupported;
- controlled native failure.

---

## 4. POSIX implementation

The internal `PosixTerminalControlProvider` supports Linux and macOS.

It provides:

- `isatty` attachment observation;
- `ttyname_r` pathname observation;
- `ioctl(TIOCGWINSZ)` live dimensions;
- complete `tcgetattr` capture;
- `tcsetattr` application;
- immediate application;
- application after output drain;
- application after output drain with unread input discarded;
- native control-character arrays;
- native input/output speed codes;
- recognized baud-rate reporting where available;
- Linux and macOS ABI-specific `termios` layouts;
- controlled native error translation.

### 4.1 Captured native image preservation

A captured POSIX `TerminalModeSnapshot` retains an internal copy of the complete native `termios` byte image.

When the snapshot is reapplied, Icod.Terminal starts from that captured image and overlays the modeled fields.

This preserves padding and other host bytes which are not meaningful public API fields and improves exact restoration over reconstructing the native structure from an all-zero buffer.

Snapshots created manually through `TerminalModeSnapshot.CreatePosix` do not invent unknown native bytes; their native image is constructed from the supplied public fields when applied.

### 4.2 POSIX size values

`TIOCGWINSZ` values with zero rows or columns are reported as unavailable rather than converted into fallback dimensions.

Environment and terminal-profile fallback dimensions are not part of this low-level API.

---

## 5. Windows implementation

The internal `WindowsTerminalControlProvider` supports Windows console endpoints.

It provides:

- standard CRT descriptor to native HANDLE resolution;
- explicit `CONIN$` / `CONOUT$` and caller path opening;
- console attachment observation with `GetConsoleMode`;
- input/output direction detection;
- complete Windows console mode capture;
- exact `SetConsoleMode` application;
- current visible console dimensions through `GetConsoleScreenBufferInfo`;
- controlled non-console and native failure results.

Windows console modes can only be applied immediately. POSIX drain/discard timing is reported as unsupported rather than emulated.

Windows console input handles do not expose screen-buffer dimensions. `LiveSize` is therefore advertised for console output endpoints, not input endpoints.

Windows does not synthesize POSIX speeds, line disciplines, or control-character arrays.

---

## 6. Machine serialization

`TerminalModeCodec` is now terminal infrastructure rather than command policy.

The established machine forms are preserved:

- POSIX: colon-separated hexadecimal native flags followed by the complete control-character array;
- Windows input: `win32-v1-input:` followed by eight hexadecimal digits;
- Windows output: `win32-v1-output:` followed by eight hexadecimal digits.

POSIX restoration against a captured baseline preserves:

- input/output speed codes;
- line-discipline presence/value;
- disabled-control-character convention;
- native ABI width;
- the captured native byte image.

Human-facing GNU control-character formatting remains outside `Icod.Terminal` as decided by T02.

---

## 7. Capability reporting

T03 adds `TerminalControlCapabilities.LiveSize`.

On Linux/macOS an attached terminal advertises live-size support alongside mode, speed, control-character, and serialization capabilities.

On Windows:

- console input advertises attachment/path/mode/serialization;
- console output additionally advertises live size.

A nonterminal endpoint reports no terminal capabilities.

---

## 8. Testing policy

Ordinary unit tests do not mutate the active terminal.

The normal suite covers:

- endpoint validation;
- observation invariants;
- controlled result states;
- snapshot validation and ownership;
- mode serialization/restoration;
- provider injection;
- regular-file nonterminal behavior;
- controlled standard-stream observation;
- controlled mode retrieval;
- live-size observation;
- regular-file size rejection.

A live no-op mode apply/capture/restore integration test is present but performs terminal mutation only when explicitly enabled:

```text
ICOD_TERMINAL_RUN_LIVE_TESTS=1
```

When enabled, standard input must be an interactive terminal. The test:

1. captures the baseline;
2. serializes it;
3. reapplies that same baseline;
4. captures the mode again;
5. verifies machine-state equivalence;
6. reapplies the original baseline in `finally`.

This permits deliberate platform validation without making ordinary CI depend on an interactive terminal.

---

## 9. Deferred to T04 and later

T03 does not define:

- canonical/cooked policy;
- cbreak policy;
- raw policy;
- echo/noecho policy;
- signal/processed-input policy;
- `TerminalSession`;
- resize events;
- lifecycle signal handling;
- input byte streams or key decoding;
- alternate-screen/cursor/keypad leases.

T04 is the next tranche and will build semantic input-mode policy on top of the native snapshots implemented here.
