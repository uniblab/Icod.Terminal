# Icod.Terminal 0.17.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.17.0`  
**Development version:** `0.17.0-alpha.2`  
**Predecessor:** `0.16.0` — OSC 9 Safe Extensions  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** modern keyboard contracts and negotiated keyboard protocols  
**Status:** T170 frozen and green; T171 semantic model implemented, exact-head validation pending

---

## 1. Position on the road to 1.0

```text
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

The 0.17 release modernizes keyboard input without breaking the traditional terminal-input path. Existing applications that never request a modern keyboard protocol continue to receive the traditional decoded key/text model.

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

---

## 2. Frozen protocol architecture

### Traditional input

Traditional terminal keyboard decoding remains the default/fallback and is unchanged on the wire. Traditional key events normalize to `TerminalKeyEventPhase.Press`; ordinary text remains `TerminalInputEventKind.Text`.

### Kitty progressive keyboard protocol

Kitty is the only keyboard mode that 0.17 will actively negotiate and lease-own because its support can be queried and its application state can be pushed/popped reversibly.

The frozen semantic modes map to Kitty flags:

```text
Disambiguated = 5   (1 | 4)
EventTypes    = 7   (1 | 2 | 4)
AllKeys       = 31  (1 | 2 | 4 | 8 | 16)
```

### xterm `modifyOtherKeys`

T170 corrected the initial roadmap direction: xterm `modifyOtherKeys` is **decoder compatibility only** in 0.17. The lease manager does not blindly enable/disable it because xterm lacks the query/push/pop ownership model required to prove restoration of pre-existing terminal state.

T174 therefore decodes supported xterm modified-key forms and normalizes them into the shared semantic key model without claiming Kitty-only semantics.

---

## 3. Frozen semantic public contract

0.17 adds:

```csharp
public enum TerminalKeyEventPhase {
	Press = 0,
	Repeat = 1,
	Release = 2
}

public enum TerminalKeyboardReportingMode {
	Disambiguated = 0,
	EventTypes = 1,
	AllKeys = 2
}
```

`TerminalKeyModifiers` retains:

```text
Shift   = 1
Control = 2
Alt     = 4
```

and appends:

```text
Super    = 8
Hyper    = 16
Meta     = 32
CapsLock = 64
NumLock  = 128
```

`TerminalInputEvent` adds:

```csharp
public TerminalKeyEventPhase? KeyPhase { get; }
public Rune? ShiftedCharacter { get; }
public Rune? BaseLayoutCharacter { get; }
public string? AssociatedText { get; }
```

`TerminalKey` retains every existing value through `Function` and appends semantic identities for the frozen Kitty functional-key table plus `Unrecognized`; raw Kitty private-use integers are not public API.

`TerminalInputProtocolOptions.KeyboardReportingMode` is intentionally deferred until T173 so no public option can be accepted before its negotiated ownership implementation exists.

Full contract: `docs/T170-Modern-Keyboard-Contract-and-Reference-Freeze.md`.

---

## 4. Ownership and lifecycle invariants

Modern keyboard reporting joins the existing `TerminalInputProtocolManager` lease domain.

Frozen rules include:

- explicit acquisition only;
- strongest-request reconciliation: `none < Disambiguated < EventTypes < AllKeys`;
- one physical library-owned Kitty stack entry on the currently managed screen regardless of logical lease count;
- Kitty support detection before acquisition;
- no silent fallback to xterm;
- cancellation before output commitment emits nothing;
- committed transitions are complete serialized writes;
- suspend/resume/invalidation/disposal keep believed state truthful;
- managed main/alternate-screen transitions use pop/switch/push choreography so a library-owned stack entry is never stranded on the inactive screen.

---

## 5. Explicit exclusions

0.17 does not add:

- raw keyboard control-sequence APIs;
- public Kitty flag integers;
- public raw Kitty private-use key codes;
- global hotkeys;
- keyboard remapping/binding policy;
- OS keyboard hooks;
- physical scan-code contracts;
- automatic terminal-brand activation;
- IME configuration/control;
- synthetic repeat detection;
- fabricated release events;
- blind xterm `modifyOtherKeys` activation;
- automatic promotion of globally configured plain CSI-u into a claimed lease-owned mode.

---

## 6. Tranche record

### T170 — modern keyboard contract/reference freeze — `0.17.0-alpha.1`

Complete and green at workflow #771.

Frozen decisions include:

- exact key phase/modifier/reporting-mode public contracts;
- complete semantic Kitty functional-key coverage without raw PUA leakage;
- alternate-key and associated-text shape;
- Kitty flag sets `5`, `7`, `31`;
- Kitty as the only lease-owned modern keyboard protocol;
- xterm `modifyOtherKeys` as decode-only compatibility;
- support-detection/query routing;
- managed main/alternate-screen ownership choreography;
- unknown/malformed frame recovery and security behavior.

Record: `docs/T170-Modern-Keyboard-Contract-and-Reference-Freeze.md`.

### T171 — semantic key-event model — `0.17.0-alpha.2`

Implemented; exact-head validation pending.

Delivered:

- `TerminalKeyEventPhase`;
- expanded `TerminalKeyModifiers`;
- additive `TerminalKey` modern named-key set plus `Unrecognized`;
- `KeyPhase`, `ShiftedCharacter`, `BaseLayoutCharacter`, `AssociatedText` on `TerminalInputEvent`;
- `TerminalKeyboardReportingMode` vocabulary without premature acquisition plumbing;
- traditional key factory normalization to `Press`;
- explicit retention of old enum values and the historical function-key `0..63` internal range;
- dedicated semantic-contract unit tests.

Record: `docs/T171-Modern-Keyboard-Semantic-Key-Event-Model.md`.

### T172 — Kitty keyboard decoder foundation — `0.17.0-alpha.3`

Next after green T171 validation.

Implement incremental Kitty/CSI-u input decoding for the frozen supported forms:

- character keys;
- current Kitty functional-key table;
- modern modifiers;
- press/repeat/release;
- shifted/base-layout key identities;
- associated text;
- pure associated-text frames;
- fragmented/coalesced bounded parsing;
- safe unknown/malformed recovery;
- active response-routing priority.

### T173 — negotiated Kitty keyboard ownership — `0.17.0-alpha.4`

Integrate `TerminalKeyboardReportingMode? KeyboardReportingMode` into `TerminalInputProtocolOptions` and `TerminalInputProtocolLease`, then implement support detection, push/pop ownership, strongest-request reconciliation, lifecycle re-entry, and managed main/alternate-screen transition choreography.

### T174 — xterm `modifyOtherKeys` decoder compatibility — `0.17.0-alpha.5`

Decode the frozen level-2 conventional and CSI-u forms when received. Normalize to `Press` with only semantics actually present. Do not add lease-owned xterm activation.

### T175 — composition and hardening — `0.17.0-alpha.6`

Exercise modern keyboard input with traditional keys/text, paste/focus/mouse, active queries, fragmented/coalesced reads, malformed recovery, concurrent leases, presentation screen transitions, suspend/resume/invalidation/disposal, transition output failures, and bounded parser/deadlock behavior.

### T176 — downstream `Icod.DCurses` acceptance — `0.17.0-alpha.7`

Extend real `Icod.DCurses` acceptance through public APIs, validating negotiated Kitty bytes/events and coexistence with full-screen refresh plus existing rich input on net8/net9/net10.

### T177 — public API/package/stable closure — `0.17.0`

Deliver public API baseline, README/sample/security docs, XML/package-only net8/net9/net10 consumer, retained 0.8–0.16 gates, new 0.17 package contract, stable metadata, and exact-head/exact-main release validation.

---

## 7. Required testing matrix

0.17 SHALL prove at minimum:

- all existing traditional-key tests remain green;
- existing enum values used by 0.16 and earlier remain unchanged;
- traditional keys normalize to `Press`;
- modern modifiers are preserved distinctly;
- all frozen current Kitty functional keys map semantically;
- shifted/base-layout keys and associated text preserve Unicode data;
- Kitty character/named-key press, repeat, and release decoding;
- xterm `modifyOtherKeys` decoding for the frozen supported forms;
- incomplete/malformed sequence recovery;
- unknown modern functional codes map to `Unrecognized` without raw PUA exposure;
- Kitty acquisition/release is exact, bounded, and reversible;
- overlapping leases reconcile deterministically;
- managed screen transitions do not strand Kitty stack entries;
- lifecycle suspend/resume does not leak or double-pop modes;
- active-query routing remains correct while modern keyboard input is flowing;
- no raw/generic keyboard-control API enters the public package;
- Windows/Linux/macOS CI and net8/net9/net10 package-only consumers.

---

## 8. Current development state

```text
VersionPrefix:    0.17.0
VersionSuffix:    alpha.2
Version:          0.17.0-alpha.2
PackageVersion:   0.17.0-alpha.2
AssemblyVersion:  0.17.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T171 validation:** T172 — Kitty keyboard decoder foundation.
