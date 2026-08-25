# T04 — Semantic Input-Mode Policy

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.4`
**Tranche:** T04 — Semantic input-mode policy
**Reference branch:** `Icod.Terminal/initial_add`
**Reference date:** 2026-08-24
**Implementation status:** Implemented for the Linux/macOS POSIX layouts and Windows console input modes established by T03

---

## 1. Purpose

T04 adds the semantic input-mode layer above the complete native snapshots implemented by T03.

Higher-level consumers should no longer need to know POSIX `termios` flag values, Linux/macOS `VMIN` and `VTIME` indices, or Windows console input-mode bit values merely to request canonical, cbreak, raw, echo, or noecho behavior.

T04 remains deliberately below `TerminalSession`. It transforms or applies a captured baseline mode but does not yet own capture lifetime, restoration, cancellation, or partial-initialization rollback. Those responsibilities begin in T05.

---

## 2. Public contract

T04 introduces:

```csharp
TerminalInputMode
TerminalInputModePolicy
```

The semantic modes are:

- `TerminalInputMode.Canonical` — line-oriented canonical/cooked input with host signal/processed-input handling enabled;
- `TerminalInputMode.CBreak` — character-oriented input with canonical buffering disabled while host signal/processed-input handling remains enabled;
- `TerminalInputMode.Raw` — character-oriented input with canonical buffering, host signal handling, extended raw-incompatible processing, ordinary input translations, output post-processing, and parity/character-size transformations adjusted for raw byte input.

Echo is an independent request supplied as `echoInput`.

Two operations are provided:

```csharp
TerminalModeSnapshot TerminalInputModePolicy.Configure(
	TerminalModeSnapshot baseline,
	TerminalInputMode inputMode,
	bool echoInput
);

TerminalControlMutationResult TerminalInputModePolicy.Apply(
	ITerminalControlProvider provider,
	TerminalEndpoint endpoint,
	TerminalModeSnapshot baseline,
	TerminalInputMode inputMode,
	bool echoInput
);
```

`Configure` is a pure snapshot transformation.

`Apply` performs the same transformation and then chooses the platform-appropriate T03 application timing:

- POSIX: `AfterOutputDrained`;
- Windows: `Immediately`.

This means a DCurses-style consumer can request semantic behavior without switching on the native terminal platform or editing native masks.

---

## 3. Relative-to-baseline rule

T04 does not synthesize a guessed universal "normal" or "sane" terminal mode.

Every semantic transition is relative to a complete baseline snapshot.

This rule is important for two reasons:

1. unrelated host flags remain as the caller found them;
2. T05 can later restore the exact captured baseline rather than reconstructing defaults.

`Canonical` therefore establishes canonical buffering and signal processing but does not rewrite every input/output flag into a platform default profile.

`CBreak` changes only the fields necessary for noncanonical character input plus the requested echo policy.

`Raw` necessarily changes a broader set of fields because raw byte input requires disabling translations and processing which would otherwise alter the byte stream.

---

## 4. POSIX semantic mapping

T04 recognizes exactly the POSIX ABI families already supported by T03:

- Linux: 32-bit termios flag layout, 32 control-character slots, `VMIN` index 6, `VTIME` index 5;
- macOS: 64-bit termios flag layout, 20 control-character slots, `VMIN` index 16, `VTIME` index 17.

### 4.1 Canonical

Canonical mode:

- enables `ICANON`;
- enables `ISIG`;
- applies the requested echo policy;
- otherwise preserves baseline input, output, control, local, speed, line-discipline, and control-character state.

When noecho is requested, both `ECHO` and `ECHONL` are cleared so newline echo does not survive ordinary echo suppression.

### 4.2 CBreak

CBreak mode:

- clears `ICANON`;
- enables `ISIG`;
- sets `VMIN = 1`;
- sets `VTIME = 0`;
- applies the requested echo policy;
- preserves unrelated baseline fields.

Signal characters such as interrupt, quit, and suspend therefore remain host-processed in cbreak mode.

### 4.3 Raw

Raw mode:

- clears break/parity/input translation fields inherited from the existing DCurses behavior;
- clears software XON/XOFF flow-control bits used by that behavior;
- clears output post-processing (`OPOST`);
- selects eight-bit characters and disables parity enable;
- clears canonical input (`ICANON`);
- clears signal processing (`ISIG`);
- clears extended input processing (`IEXTEN`);
- sets `VMIN = 1`;
- sets `VTIME = 0`;
- then applies the explicit echo/noecho request.

The final echo request is intentionally orthogonal to the input discipline. A caller may therefore request raw input with host echo when the host can honor it, although interactive full-screen consumers will normally use noecho.

### 4.4 Linux/macOS flow-control distinction

The previous DCurses implementation used Linux values for `IXON` and `IXOFF` on both POSIX hosts.

T04 corrects that accidental coupling. Linux and Darwin use different numeric values for those flags, so the semantic editor now stores separate layout metadata and clears the correct bits for the captured ABI.

This is a correctness fix, not a public semantic difference: callers still request `Raw` and do not see the platform constants.

---

## 5. Windows semantic mapping

Windows semantic input policy applies only to a `WindowsConsole` snapshot whose direction is `Input`.

A console output snapshot is rejected rather than silently treated as input.

### 5.1 Canonical

Canonical mode:

- enables `ENABLE_PROCESSED_INPUT`;
- enables `ENABLE_LINE_INPUT`;
- applies the requested `ENABLE_ECHO_INPUT` state;
- preserves unrelated console input flags.

### 5.2 CBreak

CBreak mode:

- enables `ENABLE_PROCESSED_INPUT`;
- disables `ENABLE_LINE_INPUT`;
- enables `ENABLE_VIRTUAL_TERMINAL_INPUT` so terminal key sequences can be delivered to the later T08 decoder;
- applies the requested echo state;
- preserves unrelated console input flags.

### 5.3 Raw

Raw mode:

- disables `ENABLE_PROCESSED_INPUT`;
- disables `ENABLE_LINE_INPUT`;
- enables `ENABLE_VIRTUAL_TERMINAL_INPUT`;
- applies the requested echo state;
- preserves unrelated console input flags.

Windows does not expose POSIX `ISIG`, `VMIN`, `VTIME`, drain timing, baud-rate, or control-character-array concepts. T04 does not fabricate them.

Windows documents host echo in conjunction with line input. The semantic layer still preserves the explicit echo request as a console-mode bit so the requested state remains observable; applications which require reliable echo while line input is disabled should be prepared to render echo themselves at a higher layer.

---

## 6. Application timing

A higher-level consumer using `TerminalInputModePolicy.Apply` does not need to know which native timing rule the host requires.

POSIX semantic changes use `AfterOutputDrained`. This matches the existing DCurses transition behavior and avoids changing terminal interpretation while previously emitted output remains pending.

Windows console modes use `Immediately`, the only mode-application timing supported by the T03 Windows provider.

No semantic transition discards unread input automatically. Input-discard behavior remains an explicit lower-level T03 capability rather than an implicit side effect of entering cbreak or raw mode.

---

## 7. Testing

T04 tests are entirely deterministic and do not mutate the developer or CI terminal.

The suite covers:

- Linux canonical processing and noecho;
- Linux cbreak signal retention and `VMIN`/`VTIME`;
- Linux raw flag transformation and unrelated-bit preservation;
- macOS raw flow-control bit correctness;
- Windows canonical/cbreak/raw mappings;
- explicit Windows echo requests;
- platform-appropriate apply timing through an injected provider;
- undefined semantic mode validation;
- rejection of Windows output snapshots;
- rejection of incomplete POSIX control-character arrays for noncanonical modes.

The existing opt-in T03 live restoration test remains the native mutation integration gate. T04 does not add another live-terminal mutation test because its transformation behavior can be verified completely with synthetic snapshots and injected providers.

---

## 8. DCurses migration consequence

T04 supplies the canonical replacement for the native bit-editing behavior currently implemented by DCurses `SystemTerminalModeEditor`.

DCurses can eventually map:

```text
CursesInputMode.Canonical -> TerminalInputMode.Canonical
CursesInputMode.CBreak    -> TerminalInputMode.CBreak
CursesInputMode.Raw       -> TerminalInputMode.Raw
```

and call `TerminalInputModePolicy.Apply` without retaining POSIX or Win32 constants.

Actual DCurses dependency migration remains a later integration tranche; T04 establishes the Terminal-side API required for that migration.

---

## 9. Deferred to T05 and later

T04 does not yet define:

- `TerminalSession`;
- baseline capture ownership;
- automatic restoration lifecycle;
- idempotent or asynchronous session disposal;
- partial-initialization rollback;
- capability/profile selection;
- resize/lifecycle event delivery;
- input byte-stream ownership;
- key decoding;
- presentation leases.

T05 is the next tranche and will make semantic mode transitions an owned, automatically restored live-session resource.
