# Icod.Terminal 0.12.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.12.0`  
**Development version:** `0.12.0-alpha.1`  
**Predecessor:** `0.11.0` — OSC 22 terminal mouse-pointer shape control  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** semantic OSC 133 shell-integration / semantic-prompt markers  
**Status:** T120 contract/reference freeze in progress

---

## 1. Release objective

`Icod.Terminal 0.12.0` SHALL add semantic OSC 133 shell-integration markers without exposing arbitrary OSC construction or vendor-specific metadata as untyped strings.

The release SHALL provide a portable core for marking the lifecycle of an interactive command region:

- prompt start;
- command-line input start / prompt end;
- command execution / output start;
- command completion;
- optional command exit status;
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
OSC 133 ; D [ ; exit-status ] ST
```

Semantic meaning:

- `A` — prompt begins;
- `B` — prompt ends and command-line input begins;
- `C` — command line ends / command execution has begun / command output begins;
- `D` — command region is finished, optionally with a decimal exit status.

The exact API names and exit-status validation are frozen in T120 before implementation.

Canonical outbound OSC termination for 0.12 SHALL be ST (`ESC \\`) unless T120 finds a compelling interoperability reason to do otherwise.

---

## 3. Extension posture

OSC 133 has accumulated terminal-specific extensions. 0.12 SHALL not treat them as portable merely because one terminal accepts them.

Examples requiring explicit review include:

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

OSC 133 marks are transient output annotations, not long-lived terminal modes. Therefore scoped lifecycle ownership is expected to be substantially lighter than OSC 22, OSC 9;4, or synchronized output. T120 SHALL nevertheless define what happens when a command region is logically open during session disposal, suspend, or failure.

---

## 5. T120 — OSC 133 contract and reference freeze

Freeze:

- authoritative FinalTerm/iTerm2/Kitty/Windows Terminal/WezTerm-compatible references;
- exact A/B/C/D semantics;
- exact D exit-status grammar and numeric domain;
- canonical ST/BEL policy;
- public semantic naming;
- whether 0.12 exposes individual marker methods, a scoped command-region API, or both;
- state-machine rules for legal/illegal ordering;
- nested-region policy;
- suspend/resume/disposal behavior when a region is open;
- failure and cancellation semantics;
- relationship to OSC 7 current directory publication;
- composition with synchronized output and progress/pointer shape;
- explicit extension/non-goal disposition.

**Gate T120:** no public OSC 133 API is implemented until the portable semantic contract and ordering model are frozen.

Expected development version: `0.12.0-alpha.1`.

---

## 6. T121 — byte-exact OSC 133 writer

Implement:

- specialized internal A/B/C/D encoders;
- optional D exit-status encoding;
- canonical termination;
- strict validation;
- complete-frame non-cancellable commit;
- no implicit flush;
- byte-exact tests for every portable form;
- invalid-value and pre-cancellation no-emission tests.

Expected development version: `0.12.0-alpha.2`.

---

## 7. T122 — semantic marker model

Implement the reviewed public/internal value layer:

- semantic marker names/types from T120;
- typed command-completion representation if appropriate;
- explicit distinction between command completion with no reported status and status `0`;
- exhaustive mapping/validation tests;
- no public raw marker strings.

Expected development version: `0.12.0-alpha.3`.

---

## 8. T123 — session command-region state model

Implement the T120 ordering contract through `TerminalSession`:

- legal prompt/input/execution/completion transitions;
- deterministic serialization;
- misuse detection where meaningful;
- no lifetime-held output gate;
- truthful state after output failure;
- invalidation policy if session state becomes uncertain.

If T120 chooses a scoped command-region abstraction, its ownership/state manager is implemented here.

Expected development version: `0.12.0-alpha.4`.

---

## 9. T124 — public OSC 133 API

Expose the frozen semantic public surface.

The likely API shape will include semantic operations equivalent to:

```csharp
PromptStart
CommandInputStart
CommandOutputStart
CommandFinished
```

but exact method/type names remain a T120 decision.

If a scoped command-region helper is justified, it SHALL be additive to the primitive semantic markers and SHALL not obscure the actual A/B/C/D transitions.

Expected development version: `0.12.0-alpha.5`.

---

## 10. T125 — lifecycle, failure, and ordering hardening

Prove:

- pre-commit cancellation emits nothing;
- failed committed marker writes do not silently advance logical state;
- recovery policy after uncertain writes is truthful;
- session disposal does not fabricate a successful command completion unless explicitly frozen by T120;
- managed suspend/resume behavior is deterministic;
- repeated cleanup/disposal is idempotent where ownership exists;
- malformed ordering is rejected or documented according to the T120 state model.

Expected development version: `0.12.0-alpha.6`.

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
VersionSuffix:   alpha.1
Version:         0.12.0-alpha.1
PackageVersion:  0.12.0-alpha.1
AssemblyVersion: 0.12.0.0
```

**T120 contract/reference freeze is the current tranche.**
