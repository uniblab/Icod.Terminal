# Icod.Terminal 0.16.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.16.0`  
**Development version:** `0.16.0-alpha.1`  
**Predecessor:** `0.15.0` — OSC 133 extended semantic metadata  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** bounded, safe OSC 9 extensions without exposing hazardous terminal control  
**Status:** roadmap/reference audit started; T160 contract freeze next

---

## 1. Position on the road to 1.0

```text
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

0.16 is deliberately narrower than “support every command called OSC 9 by every terminal.” OSC 9 is a vendor-extension namespace with incompatible sub-protocols. The release exposes semantic operations only where the operation is bounded, non-interactive, non-executing, and appropriate for a terminal library.

---

## 2. Existing OSC 9 support retained

`Icod.Terminal 0.10` already owns the Windows Terminal / ConEmu-style OSC `9;4` taskbar progress family through the semantic progress API and `TerminalProgressLease`.

That contract remains unchanged in 0.16. The release does not redesign progress merely because other OSC 9 forms are being added.

The existing progress states remain:

```text
clear
normal
indeterminate
error
paused
```

with bounded percentage semantics and lifecycle-aware ownership/restoration behavior already established by 0.10.

---

## 3. Reference families under consideration

OSC 9 is not one universal standard. T160 will freeze explicit interoperability tiers rather than pretending all forms are equivalent.

### A. Desktop notification

A simple OSC 9 notification form is widely associated with iTerm2-style terminal notifications:

```text
OSC 9 ; message ST/BEL
```

0.16 intends to expose this as a semantic notification operation with bounded text and injection-safe framing.

This is an advisory request to the terminal. Successful emission does not prove that a desktop notification was displayed; terminal/user policy may ignore or suppress it.

### B. Current-directory compatibility hint

ConEmu-family documentation defines:

```text
OSC 9 ; 9 ; path ST/BEL
```

for reporting the shell/current working directory.

This overlaps semantically with the existing portable/preferred OSC 7 `PublishCurrentLocationAsync(...)` API. 0.16 may expose OSC 9;9 only as an explicitly named compatibility operation or compatibility option; it SHALL NOT silently replace OSC 7 or make OSC 9;9 the default current-location protocol.

T160 must decide whether this is public API, an internal compatibility emission paired with OSC 7, or excluded if the interoperability/security benefit is insufficient.

### C. Existing taskbar progress

```text
OSC 9 ; 4 ; state ; value BEL
```

Already implemented and retained unchanged.

---

## 4. Explicitly hazardous OSC 9 commands

0.16 SHALL NOT expose vendor commands whose semantics can block the process, execute code, reveal environment state, alter emulator configuration, or launch external programs.

The exclusion set includes ConEmu-family commands for concepts such as:

- sleep/delay;
- modal message boxes;
- waiting for a key press;
- GUI macro execution;
- process/shell launch;
- environment-variable or emulator-state disclosure;
- terminal-emulation mode toggles;
- arbitrary vendor command passthrough.

There will be no public `WriteOsc9Async(string)` or raw OSC 9 command-number/value API.

This is the central meaning of **Safe Extensions** in 0.16.

---

## 5. Design principles

### Semantic APIs, not vendor command APIs

Public names describe intent such as “notification” or “current-location compatibility,” not numeric OSC 9 command slots.

### No generic raw escape hatch

Callers cannot provide arbitrary OSC 9 subcommands or pre-framed payloads.

### Bounded text

All text-bearing operations receive explicit encoded-byte ceilings. Oversize content is rejected before output commitment and is never truncated silently unless T160 freezes an operation-specific truncation contract (default position: reject, do not truncate).

### Injection-safe construction

BEL, ESC, ST, C0/C1 controls, and protocol separators cannot escape a semantic payload. T160 will freeze whether each operation uses rejection, escaping, percent encoding, or another protocol-compatible representation based on the actual wire contract.

### No automatic secret capture

The library does not inspect process arguments, shell history, environment variables, current commands, or application state to manufacture notifications or directory hints.

### Explicit caller intent

No OSC 9 notification/directory emission occurs automatically at session open, suspend, resume, invalidation, or disposal.

### Existing portable APIs remain preferred

- OSC 7 remains the preferred semantic current-location protocol.
- OSC 0/1/2 remain the title APIs.
- OSC 133 remains the semantic shell/prompt contract.
- OSC 9;4 remains the progress API.

0.16 does not create duplicate “OSC 9 versions” of those APIs merely because a vendor namespace contains overlapping commands.

---

## 6. Candidate public surface

T160 must freeze names and exact signatures before implementation. The current direction is intentionally small.

### Notification

Candidate:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);
```

Possible typed options are allowed only if the reference audit finds a cross-terminal semantic need. Arbitrary vendor fields are not.

Questions T160 must settle:

- canonical wire terminator;
- accepted Unicode model;
- control-character handling;
- encoded payload ceiling;
- null/empty semantics;
- whether a title/subtitle concept belongs here or is actually a different OSC family and should stay excluded;
- terminal interoperability tier and documentation language.

### Current-directory compatibility

No public signature is frozen yet.

T160 must choose among:

1. exclude OSC 9;9 and retain OSC 7 only;
2. add an explicitly named compatibility method;
3. add an opt-in compatibility mode to the existing semantic location publication path while keeping OSC 7 primary.

The decision must preserve the existing `PublishCurrentLocationAsync(...)` contract and avoid duplicate emission by default.

---

## 7. Tranche sequence

### T160 — OSC 9 safe-extension contract and reference freeze

**Version:** `0.16.0-alpha.1`.

Freeze:

- authoritative references and interoperability tiers;
- exact safe inclusion/exclusion set;
- notification semantic model and wire framing;
- text encoding/control policy and payload ceiling;
- OSC 9;9 current-directory decision;
- relationship to OSC 7 and OSC 9;4;
- public names/signatures;
- lifecycle, failure, privacy, and security invariants.

No implementation before this contract is explicit.

### T161 — bounded OSC 9 text encoder/writer foundation

**Expected version:** `0.16.0-alpha.2`.

Implement specialized internal encoding/writing only for the T160-approved safe forms.

Prove:

- byte-exact framing;
- Unicode behavior;
- control/injection resistance;
- exact payload boundaries;
- pre-commit cancellation;
- one committed non-cancellable write;
- no generic OSC 9 builder.

### T162 — semantic notification API

**Expected version:** `0.16.0-alpha.3`.

Implement the frozen notification surface.

Prove null/empty/Unicode/control/boundary behavior, no implicit flush, output serialization, and unchanged existing title/progress APIs.

### T163 — current-directory compatibility decision/implementation

**Expected version:** `0.16.0-alpha.4`.

If T160 approves OSC 9;9, implement it according to the frozen compatibility model and prove OSC 7 remains primary/default and byte-for-byte unchanged.

If T160 excludes OSC 9;9, this tranche closes as a documented no-addition decision rather than inventing scope.

### T164 — composition and compatibility

**Expected version:** `0.16.0-alpha.5`.

Prove safe OSC 9 operations compose with:

- OSC 9;4 progress ownership;
- OSC 133 semantic metadata;
- OSC 7 location publication;
- OSC 0/1/2 title operations;
- synchronized output;
- hyperlinks/clipboard/pointer/color managers;
- active terminal-query/input routing.

No manager may bypass the shared `TerminalSession` output serialization domain.

### T165 — lifecycle, failure, ordering, and security hardening

**Expected version:** `0.16.0-alpha.6`.

Cover:

- queued/pre-commit cancellation;
- committed transport failure;
- concurrent notification/location/progress output;
- invalidation;
- suspend/resume;
- disposal;
- no replay/synthesis;
- injection attempts;
- malformed/oversize text;
- lock-order/deadlock resistance;
- explicit proof that hazardous OSC 9 commands are not publicly reachable.

### T166 — downstream acceptance

**Expected version:** `0.16.0-alpha.7`.

Extend real `Icod.DCurses` acceptance so a higher-level full-screen consumer can coexist with the approved safe OSC 9 operations through public `TerminalSession` APIs.

Do not require the CI terminal emulator to display a real desktop notification; acceptance validates emitted bytes/order through the existing deterministic transport harness.

### T167 — public API/package/stable closure

**Stable version:** `0.16.0`.

Deliver:

- `docs/Public-API-Baseline-0.16.md`;
- README and focused sample updates;
- security/interoperability documentation;
- XML documentation assertions for all new public members;
- fresh NuGet-only net8/net9/net10 consumer;
- retained 0.8–0.15 package gates;
- retained downstream acceptance;
- new 0.16 package contract in PR/main/tag validation;
- stable release metadata;
- exact-head PR/main/tag validation.

---

## 8. Required testing matrix

0.16 SHALL include deterministic tests for:

- retained OSC 9;4 progress byte compatibility;
- safe notification byte framing;
- ASCII, Unicode, and non-BMP notification text;
- every framing-sensitive/control case frozen by T160;
- exact payload-bound behavior;
- cancellation and transport failure;
- concurrent whole-frame serialization;
- no lifecycle replay/synthesis;
- composition with progress and other session managers;
- OSC 7 compatibility if OSC 9;9 is included;
- no public path to hazardous vendor OSC 9 commands;
- Windows/Linux/macOS CI;
- net8/net9/net10 package-only consumers.

Tests validate bytes directly and do not depend on a runner terminal understanding OSC 9 notifications.

---

## 9. Explicit non-goals

0.16 SHALL NOT add:

- arbitrary OSC 9 command numbers or raw payloads;
- sleep/delay terminal commands;
- modal terminal message boxes;
- wait-for-key terminal commands;
- GUI macro execution;
- process/shell launch;
- environment-variable/emulator-state disclosure;
- terminal-emulation toggles;
- generic public OSC/CSI/DCS builders;
- notification support probing/caching;
- automatic notifications from progress or command completion;
- automatic current-directory discovery;
- automatic secret redaction;
- modern keyboard negotiation;
- OSC 3008;
- PTY/ConPTY hosting;
- terminal emulation;
- graphics protocols.

---

## 10. Current development state

```text
VersionPrefix:    0.16.0
VersionSuffix:    alpha.1
Version:          0.16.0-alpha.1
PackageVersion:   0.16.0-alpha.1
AssemblyVersion:  0.16.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next:** T160 — contract/reference freeze before any OSC 9 safe-extension implementation.
