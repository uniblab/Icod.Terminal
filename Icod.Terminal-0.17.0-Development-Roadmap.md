# Icod.Terminal 0.17.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.17.0`  
**Development version:** `0.17.0-alpha.3`  
**Predecessor:** `0.16.0` — OSC 9 Safe Extensions  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** modern keyboard contracts and negotiated keyboard protocols  
**Status:** T170–T171 green; T172 Kitty decoder implemented, exact-head validation pending

---

## 1. Position on the road to 1.0

```text
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

Traditional terminal keyboard decoding remains the default/fallback and is unchanged on the wire. Modern keyboard support is additive and explicit.

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

---

## 2. Frozen protocol architecture

### Traditional input

Traditional key events normalize to `TerminalKeyEventPhase.Press`; ordinary text remains `TerminalInputEventKind.Text`.

### Kitty progressive keyboard protocol

Kitty is the only modern keyboard mode that 0.17 actively negotiates and lease-owns because support can be queried and application state can be pushed/popped reversibly.

Frozen reporting modes map to Kitty flags:

```text
Disambiguated = 5   (1 | 4)
EventTypes    = 7   (1 | 2 | 4)
AllKeys       = 31  (1 | 2 | 4 | 8 | 16)
```

### xterm `modifyOtherKeys`

Decoder compatibility only. The library does not blindly enable/disable xterm state because it cannot prove restoration of pre-existing state. T174 decodes supported xterm forms without claiming Kitty-only semantics.

Full architecture: `docs/T170-Modern-Keyboard-Contract-and-Reference-Freeze.md`.

---

## 3. Frozen public semantic contract

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

`TerminalKeyModifiers` retains Shift=1, Control=2, Alt=4 and appends Super=8, Hyper=16, Meta=32, CapsLock=64, NumLock=128.

`TerminalInputEvent` adds:

```csharp
public TerminalKeyEventPhase? KeyPhase { get; }
public Rune? ShiftedCharacter { get; }
public Rune? BaseLayoutCharacter { get; }
public string? AssociatedText { get; }
```

`TerminalKey` retains every existing value through `Function` and appends semantic identities for the frozen Kitty functional-key table plus `Unrecognized`; raw Kitty private-use integers are not public API.

`TerminalInputProtocolOptions.KeyboardReportingMode` remains deferred until T173 so no request can be accepted before negotiated ownership is implemented.

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

0.17 does not add raw keyboard control-sequence APIs, public Kitty flag integers/private-use key codes, global hotkeys, keyboard remapping/binding policy, OS keyboard hooks, scan-code contracts, automatic terminal-brand activation, IME control, synthetic repeat/release events, blind xterm activation, or lease ownership of globally configured plain CSI-u.

---

## 6. Tranche record

### T170 — modern keyboard contract/reference freeze — `0.17.0-alpha.1`

Complete and green at workflow #771.

Frozen: semantic event/modifier/reporting/key contracts, Kitty flags `5/7/31`, Kitty-only lease ownership, xterm decode-only compatibility, support detection/query routing, managed screen-stack choreography, malformed recovery, lifecycle/security rules.

Record: `docs/T170-Modern-Keyboard-Contract-and-Reference-Freeze.md`.

### T171 — semantic key-event model — `0.17.0-alpha.2`

Complete and green at workflow #779.

Delivered `TerminalKeyEventPhase`, expanded modifiers/named keys, alternate-key/associated-text metadata, `TerminalKeyboardReportingMode`, traditional `Press` normalization, exact numeric compatibility tests, and retained function-key `0..63` internal compatibility.

Record: `docs/T171-Modern-Keyboard-Semantic-Key-Event-Model.md`.

### T172 — Kitty keyboard decoder foundation — `0.17.0-alpha.3`

Implemented; exact-head validation pending.

Canonical Kitty CSI-u frames are now decoded inside the existing incremental input path after active response correlation and before mouse/terminfo/traditional fallback. Coverage includes character/named keys, all modern modifiers, press/repeat/release, shifted/base-layout identities, associated text, pure-text frames, current Kitty functional keys, unknown-PUA `Unrecognized`, fragmentation/coalescing, bounded malformed recovery, and retained traditional CSI behavior.

Internal bounds:

```text
modern CSI-u frame:       4,096 bytes
semicolon parameters:    64
associated-text scalars: 32
```

Record: `docs/T172-Kitty-Keyboard-Decoder-Foundation.md`.

### T173 — negotiated Kitty keyboard ownership — `0.17.0-alpha.4`

Next after green T172 validation.

Add `TerminalKeyboardReportingMode? KeyboardReportingMode` to `TerminalInputProtocolOptions` and `TerminalInputProtocolLease`; implement Kitty support query/detection, flags `5/7/31`, push/pop ownership, nested strongest-mode reconciliation, lifecycle re-entry, and managed main/alternate-screen transition choreography.

### T174 — xterm `modifyOtherKeys` decoder compatibility — `0.17.0-alpha.5`

Decode frozen conventional level-2 and unambiguous CSI-u xterm forms when received. Normalize to `Press` with only semantics actually present. No lease-owned xterm activation.

### T175 — composition and hardening — `0.17.0-alpha.6`

Exercise modern keyboard input with traditional keys/text, paste/focus/mouse, active queries, fragmented/coalesced reads, malformed recovery, concurrent leases, presentation screen transitions, suspend/resume/invalidation/disposal, transition output failures, bounded parser behavior, and lock/deadlock resistance.

### T176 — downstream `Icod.DCurses` acceptance — `0.17.0-alpha.7`

Extend real `Icod.DCurses` acceptance through public APIs, validating negotiated Kitty bytes/events and coexistence with full-screen refresh and existing rich input on net8/net9/net10.

### T177 — public API/package/stable closure — `0.17.0`

Deliver public API baseline, README/sample/security docs, XML/package-only net8/net9/net10 consumer, retained 0.8–0.16 gates, new 0.17 package contract, stable metadata, exact-head validation, and exact-main Release validation before tag.

---

## 7. Required testing matrix

0.17 SHALL prove:

- all existing traditional-key tests remain green;
- existing enum values remain unchanged;
- traditional keys normalize to `Press`;
- modern modifiers remain distinct;
- current Kitty functional keys map semantically;
- shifted/base-layout identities and associated text preserve Unicode data;
- Kitty character/named-key press/repeat/release decoding;
- pure Kitty associated-text input preserves ordinary Text semantics;
- xterm `modifyOtherKeys` decoding for frozen forms;
- incomplete/malformed sequence recovery;
- unknown modern functional codes map to `Unrecognized` without raw PUA exposure;
- Kitty acquisition/release is exact, bounded, and reversible;
- overlapping leases reconcile deterministically;
- managed screen transitions do not strand Kitty stack entries;
- lifecycle suspend/resume does not leak/double-pop modes;
- active-query routing remains correct while modern keyboard input flows;
- no raw/generic keyboard-control API enters the package;
- Windows/Linux/macOS CI and net8/net9/net10 package-only consumers.

---

## 8. Current development state

```text
VersionPrefix:    0.17.0
VersionSuffix:    alpha.3
Version:          0.17.0-alpha.3
PackageVersion:   0.17.0-alpha.3
AssemblyVersion:  0.17.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T172 validation:** T173 — negotiated Kitty keyboard ownership.
