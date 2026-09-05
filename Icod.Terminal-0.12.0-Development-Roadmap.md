# Icod.Terminal 0.12.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.12.0`  
**Development version:** `0.12.0-alpha.6`  
**Predecessor:** `0.11.0` — OSC 22 terminal mouse-pointer shape control  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** semantic OSC 133 shell-integration / semantic-prompt markers  
**Status:** T125 lifecycle/failure/order hardening implemented; validation pending

---

## 1. Release objective

`Icod.Terminal 0.12.0` SHALL add semantic OSC 133 shell-integration markers without exposing arbitrary OSC construction or vendor-specific metadata as untyped strings.

The release SHALL provide a portable core for marking the lifecycle of an interactive command region:

- prompt start;
- command-line input start / prompt end;
- command execution / output start;
- command completion;
- explicit command abort/cancellation;
- command completion with exit status `0..255`;
- deterministic ordering through the existing session output domain;
- composition with text, presentation, synchronized output, progress, pointer shape, OSC 7 current-location publication, and active queries;
- downstream `Icod.DCurses` acceptance where semantic command-region marking is useful.

The release SHALL distinguish semantic shell-integration marks from ordinary text output. The marks annotate the terminal's scrollback/command model; they do not render application text themselves.

---

## 2. Portable protocol core

The portable 0.12 core SHALL be based on the broadly implemented FinalTerm/iTerm2-style OSC 133 markers:

```text
OSC 133 ; A ST
OSC 133 ; B ST
OSC 133 ; C ST
OSC 133 ; D ST
OSC 133 ; D ; exit-status ST
```

Semantic meaning:

- `A` — prompt begins;
- `B` — prompt ends and command-line input begins;
- `C` — command line ends / command execution has begun / command output begins;
- bare `D` — command region aborted/cancelled;
- `D;0..255` — command completed with an explicit exit status.

Canonical outbound OSC termination for 0.12 is ST (`ESC \\`).

---

## 3. Extension posture

OSC 133 has accumulated terminal-specific extensions. 0.12 SHALL not treat them as portable merely because one terminal accepts them.

Examples outside the portable core include:

- Kitty `A` properties such as `k=s`, `redraw=0`, `special_key=1`, and `click_events=...`;
- Kitty command-line metadata attached to `C`;
- extended semantic-zone markers such as secondary-prompt or command-input variants used by some terminals;
- arbitrary key/value properties;
- VS Code's OSC 633 family;
- iTerm2 OSC 1337 metadata which complements but is not the same protocol as OSC 133.

Vendor-specific extensions MAY be added later as typed opt-in operations if they prove useful and can be kept orthogonal to the portable core.

No public raw OSC 133 field list or arbitrary metadata dictionary is planned for the portable 0.12 API.

---

## 4. Architectural rules

0.12 reuses the established terminal-control architecture:

- semantic public APIs;
- specialized internal OSC encoders;
- complete-frame construction before commit;
- validation before output;
- caller cancellation before commit;
- committed control frames written non-cancellably;
- session-owned output serialization;
- interactive-output requirement;
- truthful optimistic support semantics;
- no automatic terminal-emulator inference;
- no generic OSC escape hatch;
- no second output transport.

OSC 133 marks are transient output annotations, not long-lived terminal modes. T120 froze that they do not create a lifecycle participant, a command-region lease, or an in-memory shell-history state machine.

---

## 5. T120 — OSC 133 contract and reference freeze

**Status:** Complete.  
**Development version:** `0.12.0-alpha.1`.

Frozen decisions include:

- portable FinalTerm/iTerm2 A/B/C/D core;
- `A` = prompt start;
- `B` = command-input start / prompt end;
- `C` = command-output start / command executed;
- `D;0..255` = completed command with exit status;
- bare `D` = aborted/cancelled command region;
- canonical outbound ST termination;
- `byte` completion status;
- no nullable status API conflating abort with completion;
- independently callable semantic markers rather than a synthetic shell-history state machine;
- no long-lived OSC 133 lease;
- no automatic marker emission on suspend/resume/disposal;
- no compensating markers after failed writes;
- no automatic support detection;
- no raw OSC 133 strings or arbitrary metadata dictionaries.

Record: `docs/T120-OSC-133-Semantic-Prompt-Contract-and-Reference-Freeze.md`.

---

## 6. T121 — byte-exact OSC 133 writer

**Status:** Complete.  
**Development version:** `0.12.0-alpha.2`.

Implemented:

- specialized internal A/B/C/D encoders;
- bare D abort and `D;0..255` completion forms;
- canonical ST termination;
- minimal ASCII decimal status encoding;
- complete-frame non-cancellable commit;
- no implicit flush;
- byte-exact tests and pre-cancellation no-emission tests.

Record: `docs/T121-OSC-133-Byte-Exact-Writer.md`.

---

## 7. T122 — semantic marker model

**Status:** Complete.  
**Development version:** `0.12.0-alpha.3`.

Implemented:

- internal typed semantic marker vocabulary;
- typed `byte` command-completion status;
- explicit distinction between abort and completed status `0`;
- exhaustive semantic-to-wire mapping tests;
- invalid default/uninitialized marker rejection;
- no public raw marker strings.

Record: `docs/T122-OSC-133-Semantic-Marker-Model.md`.

---

## 8. T123 — session marker integration and ordering semantics

**Status:** Complete.  
**Development version:** `0.12.0-alpha.4`.

T123 implements the frozen T120 ordering posture through `TerminalSession` without adding a synthetic command-region state machine.

Implemented:

- deterministic serialization through the existing session output gate;
- ordering relative to application text and control output;
- interactive-output validation;
- caller cancellation while waiting for the output gate and immediately before commit;
- no lifetime-held output gate;
- no retained prompt/input/output/completion state;
- no transition rejection merely because earlier markers were not observed through the same session;
- failed committed marker writes propagate without compensating markers or fabricated history;
- later independently callable markers remain available after a failed write;
- session-output closure rejects new markers after disposal begins.

No nested-region owner, lifecycle participant, cleanup state, or recovery marker is required because OSC 133 annotations are transient events rather than library-owned terminal modes.

Record: `docs/T123-OSC-133-Session-Marker-Integration-and-Ordering-Semantics.md`.

---

## 9. T124 — public OSC 133 API

**Status:** Complete.  
**Development version:** `0.12.0-alpha.5`.

Exposed frozen semantic public surface:

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

The public surface retains T120's independent-call semantics and does not expose raw OSC 133 marker letters, arbitrary metadata, a nullable completion-status overload, or a scoped command-region lease.

Record: `docs/T124-Public-OSC-133-Semantic-Prompt-API.md`.

---

## 10. T125 — lifecycle, failure, and ordering hardening

**Status:** Implemented; validation pending.  
**Development version:** `0.12.0-alpha.6`.

Hardening proves the final public surface across lifecycle and failure edges:

- pre-commit cancellation emits nothing;
- cancellation while queued for session output emits nothing;
- cancellation after commitment cannot deliberately truncate a frame;
- committed writes use `CancellationToken.None`;
- failed committed marker writes propagate without compensating markers;
- later independent marker calls remain truthful after a failed write;
- session disposal emits no automatic finish or abort marker;
- repeated disposal is idempotent with respect to OSC 133 output;
- managed suspend emits no OSC 133 marker;
- managed resume replays no OSC 133 marker;
- lifecycle re-entry failure cannot fabricate semantic command history;
- noncanonical ordering remains accepted as frozen by T120.

No production recovery state or lifecycle hook was added because the existing stateless event design already satisfies the frozen contract structurally.

Record: `docs/T125-OSC-133-Lifecycle-Failure-and-Ordering-Hardening.md`.

---

## 11. T126 — composition and downstream acceptance

Prove composition with:

- ordinary text writes;
- OSC 0 title;
- OSC 7 current-location publication;
- OSC 8 hyperlinks;
- OSC 9;4 progress;
- OSC 22 pointer shape;
- OSC 52 clipboard;
- DECSCUSR cursor style;
- presentation state;
- input-protocol leases;
- synchronized output;
- active terminal queries.

Add downstream `Icod.DCurses` acceptance showing a higher-level TUI can annotate command/prompt regions without constructing OSC bytes or disrupting refresh output.

Expected development version: `0.12.0-alpha.7`.

---

## 12. T127 — public API, docs, sample, package, stable closure

Deliver:

- `docs/Public-API-Baseline-0.12.md`;
- root README update;
- focused OSC 133 sample;
- package release notes/tags;
- XML-documentation assertions for the complete 0.12 public delta;
- fresh NuGet-only consumer on net8.0/net9.0/net10.0;
- retained 0.8/0.9/0.10/0.11 package-contract gates;
- retained downstream DCurses acceptance gates;
- new OSC 133 downstream/package gates;
- stable `0.12.0` metadata;
- exact PR/main/tag release gates.

Expected stable version: `0.12.0`.

---

## 13. Explicit non-goals

0.12 SHALL NOT add, unless explicitly promoted by a later reviewed tranche:

- public arbitrary OSC construction;
- public arbitrary OSC 133 strings;
- arbitrary key/value metadata dictionaries;
- automatic shell detection or shell-script installation;
- automatic modification of PS1/PROMPT_COMMAND or shell startup files;
- VS Code OSC 633 as though it were OSC 133;
- iTerm2 OSC 1337 metadata as though it were OSC 133;
- terminal-emulator detection as a capability oracle;
- shell command parsing or execution;
- retained scrollback model or command-history database inside `Icod.Terminal`.

---

## 14. Current development state

```text
VersionPrefix:   0.12.0
VersionSuffix:   alpha.6
Version:         0.12.0-alpha.6
PackageVersion:  0.12.0-alpha.6
AssemblyVersion: 0.12.0.0
```

**T125 lifecycle/failure/order hardening is implemented. Exact-head validation is required before T126 begins.**
