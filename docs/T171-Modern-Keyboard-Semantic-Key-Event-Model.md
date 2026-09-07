# T171 — Modern Keyboard Semantic Key-Event Model

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T171  
**Version:** `0.17.0-alpha.2`  
**Status:** Implemented; exact-head validation pending

## Purpose

T171 implements the additive semantic model frozen by T170 before any modern keyboard wire decoder is introduced.

Traditional input remains the compatibility baseline. Existing applications continue to receive the same text/key classifications and traditional wire behavior; key events now additionally expose an explicit phase whose default is `Press`.

## Public additions

### Key phase

```csharp
public enum TerminalKeyEventPhase {
	Press = 0,
	Repeat = 1,
	Release = 2
}
```

`TerminalInputEvent` adds:

```csharp
public TerminalKeyEventPhase? KeyPhase { get; }
```

Traditional `Key` events normalize to `Press`; non-key events expose `null`.

### Modifier expansion

Existing values remain unchanged:

```text
Shift   = 1
Control = 2
Alt     = 4
```

0.17 appends:

```text
Super    = 8
Hyper    = 16
Meta     = 32
CapsLock = 64
NumLock  = 128
```

### Named-key expansion

`TerminalKey` retains all existing values through `Function` and appends semantic identities for:

- lock/system keys;
- keypad keys;
- media/volume keys;
- left/right modifier keys;
- ISO Level 3 / Level 5 Shift;
- `Unrecognized` as the forward-compatible semantic bucket for valid but unknown modern functional-key identities.

Raw Kitty private-use key integers are not exposed.

### Alternate-key and associated-text metadata

`TerminalInputEvent` adds:

```csharp
public Rune? ShiftedCharacter { get; }
public Rune? BaseLayoutCharacter { get; }
public string? AssociatedText { get; }
```

Character identities remain valid only for `TerminalKey.Character`. Associated text may accompany any semantic key event when a future modern decoder reports it; empty associated text is rejected by the internal event factory.

### Reporting-mode vocabulary

T171 introduces the public semantic reporting vocabulary without yet wiring it into acquisition:

```csharp
public enum TerminalKeyboardReportingMode {
	Disambiguated = 0,
	EventTypes = 1,
	AllKeys = 2
}
```

`TerminalInputProtocolOptions` integration is intentionally deferred to T173 so an option can never be accepted as a no-op before the negotiated manager implementation exists.

## Compatibility safeguards

T171 explicitly preserves:

- `TerminalInputEventKind` numeric values;
- existing `TerminalKey` numeric values from `None` through `Function`;
- existing modifier numeric values;
- the historical internal function-key-number range `0..63`;
- traditional plain-text classification as `TerminalInputEventKind.Text` rather than synthetic key events.

## Tests

`TerminalModernKeyboardContractTests` proves:

- exact retained enum values;
- exact new modifier/phase/reporting values;
- traditional key factory defaulting to `Press`;
- text events retaining null key phase;
- full modern modifier round-trip;
- shifted/base-layout character metadata;
- multi-scalar associated text;
- modern named-key repeat/release semantics;
- retained function-key zero compatibility;
- unknown modern key bucket behavior;
- character metadata cannot leak onto named keys;
- empty associated text is rejected.

## Non-goals

T171 does not parse Kitty or xterm modern keyboard frames and does not negotiate keyboard mode. Those responsibilities begin with T172 and T173.

Next: T172 — Kitty keyboard decoder foundation.
