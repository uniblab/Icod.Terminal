# T120 — OSC 133 Semantic-Prompt Contract and Reference Freeze

**Release:** `0.12.0`  
**Tranche:** `T120`  
**Development version:** `0.12.0-alpha.1`  
**Status:** Frozen

---

## 1. Purpose

T120 freezes the portable OSC 133 shell-integration contract before `Icod.Terminal` exposes any public semantic-prompt API.

OSC 133 is a semantic annotation protocol. It tells a terminal where prompts, command input, and command output begin and end so that the terminal may implement command marks, navigation, output selection, exit-status decoration, and related shell-integration features.

It does not render text itself and is not a long-lived terminal mode.

---

## 2. Reference model

The portable core is the FinalTerm/iTerm2 A/B/C/D protocol implemented by multiple modern terminals and terminal frameworks.

Canonical semantic flow:

```text
OSC 133 ; A ST
    prompt text
OSC 133 ; B ST
    user-entered command text
OSC 133 ; C ST
    command output
OSC 133 ; D ; exit-status ST
```

The protocol is commonly described as:

- `A` — prompt start;
- `B` — command start / prompt end / command-input start;
- `C` — command executed / command-output start;
- `D` — command finished.

The public API SHALL use semantic names rather than exposing the A/B/C/D letters.

---

## 3. Frozen portable wire forms

T120 freezes these portable outbound frames:

### Prompt start

```text
ESC ] 133 ; A ESC \
```

### Command-input start

```text
ESC ] 133 ; B ESC \
```

### Command-output start

```text
ESC ] 133 ; C ESC \
```

### Successful/failed command completion

```text
ESC ] 133 ; D ; status ESC \
```

where `status` is one or more ASCII decimal digits representing an integer in the inclusive range `0..255`.

### Aborted/cancelled command region

```text
ESC ] 133 ; D ESC \
```

The no-status D form is semantically distinct from completed status `0`.

---

## 4. Exit-status contract

The portable completion API SHALL accept exactly the FinalTerm/iTerm2 command-status domain:

```text
0 <= status <= 255
```

Public representation SHALL therefore use `byte` unless implementation ergonomics reveal a compelling reason to expose a wider integer with explicit validation.

Frozen interpretation:

- `0` means command success;
- nonzero means command failure;
- omitted status is not represented as nullable success/failure state;
- omitted status is reserved for the semantic abort/cancel operation.

This intentionally chooses the stronger FinalTerm/iTerm2 meaning rather than the looser Windows Terminal behavior which also accepts an omitted finish status.

---

## 5. Canonical terminator

0.12 SHALL emit OSC 133 using ST:

```text
ESC \
```

rather than BEL.

Reasons:

- ST is the documented form used by Kitty's shell-integration guidance;
- it matches the canonical 0.11 OSC 22 policy;
- it avoids coupling semantic prompt markers to audible/legacy BEL termination conventions;
- terminals which accept OSC 133 commonly accept ST.

Inbound parsing is not part of 0.12 portable core because OSC 133 is an application-to-terminal annotation protocol, not a query/response protocol.

---

## 6. Frozen public semantic operations

T120 freezes the semantic API shape at the operation level.

The intended public operations are equivalent to:

```csharp
ValueTask BeginPromptAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandInputAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandOutputAsync(
	CancellationToken cancellationToken = default
);

ValueTask FinishCommandAsync(
	byte exitStatus,
	CancellationToken cancellationToken = default
);

ValueTask AbortCommandAsync(
	CancellationToken cancellationToken = default
);
```

Exact final names may receive minor naming polish in T124, but the semantic distinctions SHALL NOT change without reopening T120.

In particular:

- there is no public `WriteOsc133Async(string ...)`;
- there is no public `char marker` API;
- there is no nullable exit-status overload which conflates completion and abort.

---

## 7. No long-lived OSC 133 lease

0.12 SHALL NOT create a long-lived `TerminalCommandLease` or similar owner for the portable core.

Rationale:

1. OSC 133 markers are event annotations, not a persistent terminal mode requiring restoration.
2. There is no meaningful terminal-side state to “undo” when a scope ends.
3. A lease disposing because of exception/unwind cannot truthfully know whether the shell command completed, failed, or was aborted.
4. Automatically emitting `D` from `DisposeAsync()` would fabricate semantic history.
5. Primitive explicit markers compose naturally with shells, REPLs, command frameworks, and downstream TUIs.

A higher-level helper may be considered in a future release only if it requires explicit completion/abort calls and cannot fabricate semantic state.

---

## 8. Ordering posture

The portable public marker methods SHALL be independently callable and SHALL NOT enforce a strict in-memory A→B→C→D state machine.

The library SHALL document the canonical order, but it SHALL not reject a marker merely because a previous marker was not observed through the same `TerminalSession` instance.

Reasons:

- shells may redraw prompts;
- shell integration may begin after a prompt is already active;
- applications may recover after interrupted output;
- multiplexers, subshells, and nested REPLs may produce marker sequences outside the library's knowledge;
- the terminal itself remains the semantic consumer and defines how malformed/out-of-order sequences are interpreted.

`Icod.Terminal` therefore owns byte correctness and output ordering, not a synthetic shell-history state machine.

---

## 9. Cancellation and commit

Each marker operation SHALL follow the established control-output transaction rule:

1. validate semantic arguments;
2. observe caller cancellation before output commitment;
3. acquire session output serialization with the caller token;
4. build/retain the complete marker frame;
5. observe cancellation immediately before commit;
6. write the complete frame with `CancellationToken.None`;
7. do not implicitly flush.

A committed marker is indivisible from the library's perspective.

If the transport reports failure during the committed write, the operation propagates the failure. The library SHALL NOT emit compensating OSC 133 markers because doing so could fabricate semantic history.

---

## 10. Lifecycle posture

OSC 133 adds no lifecycle participant.

### Suspend

Managed terminal suspension SHALL NOT automatically emit `D`, abort, prompt start, or any other OSC 133 marker.

### Resume

Managed terminal resume SHALL NOT recreate or replay semantic markers.

### Session disposal

`TerminalSession.DisposeAsync()` SHALL NOT automatically finish or abort a command region.

The application which knows command semantics remains responsible for emitting the correct marker before lifecycle transition if desired.

This is a deliberate contrast with persistent library-owned state such as synchronized output, progress, pointer shape, presentation mode, or cursor style.

---

## 11. Output serialization and flushing

All public OSC 133 operations SHALL participate in the normal session output serialization domain.

They SHALL:

- preserve ordering relative to text and other semantic terminal operations;
- never hold the output gate beyond the individual write;
- never flush implicitly.

The caller may explicitly flush through whatever higher-level operation requires it.

---

## 12. Composition with OSC 7 current directory

OSC 133 and OSC 7 are complementary.

OSC 133 marks command-region boundaries.
OSC 7 publishes semantic current working directory.

0.12 SHALL NOT automatically emit OSC 7 from any OSC 133 marker method and SHALL NOT infer a working directory from process state.

A shell or application may explicitly compose:

```text
command finished
publish new current location
begin next prompt
```

or another ordering appropriate to its shell integration.

---

## 13. Support posture

Successful marker emission proves only that the complete OSC 133 frame was written.

It does not prove that the terminal:

- supports OSC 133;
- recognizes the marker;
- creates a command mark;
- records command text;
- records output boundaries;
- displays the exit status;
- keeps the annotation after scrollback/history changes.

0.12 SHALL NOT infer OSC 133 support from:

- operating system;
- `TERM`;
- emulator environment variables;
- terminal identity;
- terminal version strings.

There is no portable OSC 133 support query in the frozen core.

---

## 14. Vendor-extension disposition

The following are explicitly outside the portable T120 contract:

### Kitty extensions

Examples include:

- `A;k=s` secondary-prompt annotation;
- `A;redraw=0`;
- `A;special_key=1`;
- `A;click_events=...`;
- `C;cmdline=...`;
- `C;cmdline_url=...`.

These are useful but are not silently added to the portable public methods.

### Additional marker letters

Some terminal ecosystems recognize additional semantic-zone markers such as `P`, `I`, or other extensions. They are not part of the portable A/B/C/D 0.12 surface.

### OSC 633

VS Code's OSC 633 family is related shell-integration functionality but is a distinct protocol namespace. 0.12 SHALL NOT alias OSC 633 to OSC 133.

### OSC 1337

iTerm2 OSC 1337 metadata such as current directory, remote host, and user variables is a distinct protocol family. It is not included merely because iTerm2 shell integration uses it alongside OSC 133.

Future releases may expose typed extensions in separate APIs after dedicated review.

---

## 15. Security and data-minimization posture

The portable 0.12 core emits no command text, environment variables, hostname, username, working-directory value, or arbitrary metadata.

This is deliberate.

The A/B/C/D markers communicate boundaries only, with an optional one-byte semantic exit status value encoded as decimal text.

Any future command-line metadata extension requires separate review because command text may contain secrets, tokens, credentials, paths, or other sensitive data.

---

## 16. Downstream intent

The portable API should be usable by:

- shells;
- REPLs;
- command frameworks;
- terminal multiplexing front ends;
- interactive command tools;
- `Icod.DCurses` applications which want terminal-native command-region annotations.

The downstream consumer should not need to know that prompt/input/output/completion are encoded as A/B/C/D.

---

## 17. T120 decision

T120 freezes `Icod.Terminal 0.12.0` around the portable FinalTerm/iTerm2 OSC 133 semantic-prompt core:

```text
A  prompt start
B  command-input start
C  command-output start
D  command abort
D;0..255  command completion with exit status
```

The release will provide explicit semantic marker operations, canonical ST framing, byte-exact session serialization, no implicit flush, no automatic support detection, no lifecycle fabrication, and no public arbitrary OSC 133 metadata.

T121 may now implement the byte-exact writer against this frozen contract.
