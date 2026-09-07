# Icod.Terminal 0.17 Public API Baseline

**Release:** `0.17.0`  
**Theme:** modern keyboard contracts and negotiated keyboard protocols

---

## 1. Public delta

0.17 extends the existing terminal-input model without replacing traditional decoding.

The stable public additions are:

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

`TerminalKeyModifiers` retains all existing values and adds:

```text
Super
Hyper
Meta
CapsLock
NumLock
```

`TerminalInputEvent` adds:

```csharp
TerminalKeyEventPhase? KeyPhase { get; }
Rune? ShiftedCharacter { get; }
Rune? BaseLayoutCharacter { get; }
string? AssociatedText { get; }
```

`TerminalInputProtocolOptions` and `TerminalInputProtocolLease` add:

```csharp
TerminalKeyboardReportingMode? KeyboardReportingMode { get; }
```

`TerminalKey` retains every pre-0.17 value and appends semantic identities for the frozen Kitty functional-key table plus `Unrecognized`.

All public contracts from 0.16 and earlier remain available.

---

## 2. Traditional keyboard compatibility

Traditional terminal keyboard decoding remains the default and fallback.

Traditional key events normalize to:

```text
KeyPhase = Press
```

Ordinary text remains `TerminalInputEventKind.Text`. 0.17 does not synthesize repeat/release events for traditional protocols that do not carry those semantics.

Existing enum numeric values are preserved; new enum values are appended.

---

## 3. Kitty progressive keyboard protocol

Kitty is the only modern keyboard protocol that 0.17 actively negotiates and owns.

The public reporting modes map to exact Kitty flag sets:

```text
Disambiguated = 5
EventTypes    = 7
AllKeys       = 31
```

Applications request keyboard reporting through the existing compound rich-input lease:

```csharp
TerminalControlResult<TerminalInputProtocolLease> result =
	await session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
		}
	);
```

A successful lease means `Icod.Terminal` has negotiated Kitty support and owns one reversible Kitty stack entry for the strongest active keyboard request.

Overlapping requests reconcile as:

```text
none < Disambiguated < EventTypes < AllKeys
```

Logical lease count never creates multiple library-owned Kitty stack entries on the same managed screen.

---

## 4. Kitty semantic event data

Canonical Kitty CSI-u input may provide:

- press, repeat, and release phases;
- expanded modifier state;
- shifted character identity;
- base-layout character identity;
- associated text;
- semantic functional-key identities.

Raw Kitty private-use integers are not exposed as public API. Known Kitty functional keys map to `TerminalKey`; unknown private-use functional codes map to `TerminalKey.Unrecognized`.

Pure associated-text input remains ordinary `Text` events rather than being promoted to key events.

---

## 5. xterm `modifyOtherKeys`

0.17 supports decode-only compatibility for the frozen xterm forms documented by T174.

The library does **not** automatically enable or disable xterm `modifyOtherKeys`, because it cannot prove restoration of arbitrary pre-existing terminal state.

xterm-derived events normalize to `Press` and expose only the Shift/Alt/Control semantics represented by that protocol. Kitty-only phase, alternate-key, associated-text, and modern-modifier semantics are not invented.

---

## 6. Rich-input composition

Modern keyboard reporting shares `TerminalInputProtocolManager` ownership with:

- bracketed paste;
- focus reporting;
- mouse reporting.

One compound lease may request any supported combination.

Acquisition remains explicit. If the requested reversible contract is unavailable, the operation returns an unavailable `TerminalControlResult` rather than silently activating a different protocol.

---

## 7. Presentation and screen-local ownership

Kitty stack state is screen-local. When a managed presentation transition changes between the main and alternate screen, `Icod.Terminal` performs:

```text
Kitty pop
screen switch
Kitty push
```

while preserving paste/focus/mouse ownership without unnecessary cycling.

Input and presentation mutations share a session composition serialization domain. The frozen lock order is:

```text
state composition -> manager -> control output
```

so a concurrent keyboard lease mutation cannot interleave inside the pop/switch/push handoff.

Failure and rollback are surfaced; believed state is not silently advanced after an incomplete transition.

---

## 8. Lifecycle semantics

Managed suspend restores owned input state before handing control away.

Managed resume re-detects Kitty support through the sanctioned lifecycle observation-query window before re-establishing a requested keyboard mode.

If support cannot be re-established, the manager fails closed and marks its believed state untrustworthy rather than pretending the requested protocol is active.

Session disposal restores owned rich-input state exactly once. Stale leases released after owner-driven cleanup are idempotent and emit no duplicate terminal traffic.

---

## 9. Parser and routing guarantees

Modern keyboard parsing remains inside the existing bounded incremental input decoder.

0.17 preserves:

- fragmented-read support;
- coalesced-event separation;
- bounded malformed-sequence recovery;
- active terminal-query response correlation before ordinary input decoding;
- traditional input fallback after malformed modern frames;
- no unbounded buffering introduced by Kitty or xterm compatibility.

---

## 10. Explicit exclusions

0.17 does not expose:

- raw keyboard control-sequence writers;
- generic Kitty flag integers;
- raw Kitty private-use key codes;
- blind xterm activation/deactivation;
- global hotkeys;
- OS keyboard hooks;
- scan-code contracts;
- IME control;
- keyboard remapping/binding policy;
- synthetic repeat/release events for protocols that do not report them;
- automatic terminal-brand activation.

These exclusions are part of the stable safety and ownership boundary.

---

## 11. Downstream acceptance

A real `Icod.DCurses 0.1.0` consumer runs on net8.0, net9.0, and net10.0 and proves through public APIs:

- negotiated Kitty `AllKeys` acquisition;
- bracketed paste, focus, and mouse coexistence;
- alternate-screen Kitty pop/switch/push transfer;
- real `CursesSession.RefreshAsync()` output while Kitty is active;
- modern key phase/modifier/alternate-key/associated-text decoding;
- focus and paste decoding on the same event stream;
- owner-driven DCurses/TerminalSession cleanup;
- idempotent stale-lease disposal.

---

## 12. Compatibility

The stable package targets:

```text
net8.0
net9.0
net10.0
```

All three remain first-class supported targets.

The package-only 0.17 contract gate must verify this public surface from the freshly packed NuGet artifact on every supported TFM, independently of project references.

This document freezes the stable 0.17 public delta.
