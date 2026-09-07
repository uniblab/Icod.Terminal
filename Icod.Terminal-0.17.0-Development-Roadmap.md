# Icod.Terminal 0.17.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.17.0`  
**Development version:** `0.17.0-alpha.5`  
**Predecessor:** `0.16.0` — OSC 9 Safe Extensions  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** modern keyboard contracts and negotiated keyboard protocols  
**Status:** T170–T173 complete and green; T174 xterm decode compatibility implemented, exact-head validation pending

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

`TerminalInputProtocolOptions` and `TerminalInputProtocolLease` now expose nullable `KeyboardReportingMode` as part of the existing compound rich-input ownership surface.

---

## 4. Ownership and lifecycle invariants

Modern keyboard reporting joins the existing `TerminalInputProtocolManager` lease domain.

Frozen release rules include:

- explicit acquisition only;
- strongest-request reconciliation: `none < Disambiguated < EventTypes < AllKeys`;
- one physical library-owned Kitty stack entry on the currently managed screen regardless of logical lease count;
- Kitty support detection before acquisition;
- no silent fallback to xterm;
- cancellation before output commitment emits nothing;
- committed transitions are complete serialized writes;
- suspend/resume/invalidation/disposal keep believed state truthful;
- managed main/alternate-screen transitions use pop/switch/push choreography so a library-owned stack entry is never stranded on the inactive screen.

The first six ownership primitives are implemented in T173. Cross-manager managed-screen handoff and post-resume re-establishment remain mandatory T175 hardening gates so they can be validated with lock-order and rollback fault injection.

---

## 5. Explicit exclusions

0.17 does not add raw keyboard control-sequence APIs, public Kitty flag integers/private-use key codes, global hotkeys, keyboard remapping/binding policy, OS keyboard hooks, scan-code contracts, automatic terminal-brand activation, IME control, synthetic repeat/release events, blind xterm activation, or lease ownership of globally configured plain CSI-u.

---

## 6. Tranche record

### T170 — modern keyboard contract/reference freeze — `0.17.0-alpha.1`

Complete and green at workflow #771.

Record: `docs/T170-Modern-Keyboard-Contract-and-Reference-Freeze.md`.

### T171 — semantic key-event model — `0.17.0-alpha.2`

Complete and green at workflow #779.

Delivered explicit phase, expanded modifiers/named keys, alternate-key/associated-text metadata, reporting modes, traditional `Press` normalization, and compatibility tests.

Record: `docs/T171-Modern-Keyboard-Semantic-Key-Event-Model.md`.

### T172 — Kitty keyboard decoder foundation — `0.17.0-alpha.3`

Complete and green at workflow #786.

Canonical Kitty CSI-u is decoded inside the existing incremental input path after active response correlation. Coverage includes character/named keys, modern modifiers, phases, alternate identities, associated text, pure text, current Kitty functional keys, unknown-PUA `Unrecognized`, fragmentation/coalescing, and bounded malformed recovery.

Record: `docs/T172-Kitty-Keyboard-Decoder-Foundation.md`.

### T173 — negotiated Kitty keyboard ownership — `0.17.0-alpha.4`

Complete and green at workflow #799.

Delivered nullable keyboard reporting requests on the existing compound input-protocol lease, timing-independent Kitty support detection using `CSI ? u` + Primary DA delimiter, exact flag pushes `5/7/31`, one-entry pop, strongest-mode nesting/downgrade, and composition with bracketed paste/focus/mouse.

Cross-manager alternate-screen handoff and post-resume state re-establishment remain mandatory T175 hardening gates.

Record: `docs/T173-Negotiated-Kitty-Keyboard-Ownership.md`.

### T174 — xterm `modifyOtherKeys` decoder compatibility — `0.17.0-alpha.5`

Implemented; exact-head validation pending.

Decode-only compatibility now accepts conventional level-2 `CSI 27;modifier;key~` and the unambiguous xterm/fixterms-style CSI-u shape. xterm events normalize to `Press`, preserve only Shift/Alt/Control semantics actually present, and never gain Kitty-only alternate-key, associated-text, release/repeat, or modern-modifier semantics. No xterm activation is emitted.

Record: `docs/T174-Xterm-ModifyOtherKeys-Decoder-Compatibility.md`.

### T175 — composition and hardening — `0.17.0-alpha.6`

Next after green T174 validation.

Exercise modern keyboard input with traditional keys/text, paste/focus/mouse, active queries, fragmented/coalesced reads, malformed recovery, concurrent leases, managed presentation screen transitions, suspend/resume/invalidation/disposal, Kitty post-resume re-detection/re-establishment, transition output failures, bounded parser behavior, and lock/deadlock resistance.

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
- lifecycle suspend/resume re-establishes negotiated keyboard state truthfully;
- active-query routing remains correct while modern keyboard input flows;
- no raw/generic keyboard-control API enters the package;
- Windows/Linux/macOS CI and net8/net9/net10 package-only consumers.

---

## 8. Current development state

```text
VersionPrefix:    0.17.0
VersionSuffix:    alpha.5
Version:          0.17.0-alpha.5
PackageVersion:   0.17.0-alpha.5
AssemblyVersion:  0.17.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T174 validation:** T175 — composition and hardening.
