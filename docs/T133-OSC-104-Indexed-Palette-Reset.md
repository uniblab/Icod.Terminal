# T133 — OSC 104 Indexed Palette Reset

**Release:** `0.13.0`  
**Tranche:** `T133`  
**Development version:** `0.13.0-alpha.4`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T133 adds explicit OSC 104 indexed-palette reset operations on top of the T132 palette mutation/observation foundation.

The tranche preserves the T130 distinction between **reset** and **restoration**. OSC 104 asks the terminal to return palette entries to its own policy/resource/default values. It does not restore a color previously observed by `Icod.Terminal`.

---

## 2. Public reset surface

T133 adds:

```csharp
ValueTask ResetPaletteColorAsync(
	byte index,
	CancellationToken cancellationToken = default
);

ValueTask ResetPaletteColorsAsync(
	IReadOnlyList<byte> indices,
	CancellationToken cancellationToken = default
);

ValueTask ResetPaletteAsync(
	CancellationToken cancellationToken = default
);
```

The three operations intentionally distinguish one-entry, bounded multi-entry, and whole-palette reset.

An empty collection passed to `ResetPaletteColorsAsync(...)` is invalid and does not silently become whole-palette reset; callers use `ResetPaletteAsync()` for that semantic operation.

---

## 3. Canonical framing

T133 emits canonical ST-terminated frames:

```text
ResetPaletteColorAsync(7)
    -> OSC 104 ; 7 ST

ResetPaletteColorsAsync([3, 255, 0])
    -> OSC 104 ; 3 ; 255 ; 0 ST

ResetPaletteAsync()
    -> OSC 104 ST
```

Indices use minimal invariant ASCII decimal.

The multi-index operation:

- requires 1–256 entries;
- rejects duplicate indices;
- preserves caller order;
- validates the complete collection before output commitment;
- builds one complete frame before output commitment.

---

## 4. Session/output semantics

OSC 104 resets reuse the T132 palette output path and therefore:

- require interactive terminal output;
- serialize through the existing session-owned output gate;
- observe caller cancellation before commitment;
- use `CancellationToken.None` for the committed frame write;
- emit one complete write;
- do not flush implicitly;
- prove emission only, not terminal recognition or application.

No palette cache or retained palette-state machine is introduced.

---

## 5. Reset is not restoration

T133 deliberately does not query before reset and does not retain a prior observed color.

For example:

```text
SetPaletteColorAsync(5, X)
ResetPaletteColorAsync(5)
```

emits an OSC 4 mutation followed by `OSC 104;5 ST`. The reset does not replay X, does not query the terminal, and does not claim to recover whatever value preceded X.

Any T136 scoped owner that claims exact restoration must first obtain a truthful observation baseline and later restore by explicit OSC 4 color mutation.

---

## 6. Tests

T133 tests cover:

- bare OSC 104 whole-palette reset;
- 1/2/3-digit palette-index boundaries;
- ordered multi-index reset;
- empty collection rejection;
- duplicate rejection;
- >256 entry rejection;
- explicit 256-index bounded operation;
- public single/multiple/all reset framing;
- no implicit flush;
- non-cancellable committed writes;
- pre-cancelled no-emission behavior;
- invalid collection no-emission behavior;
- explicit proof that reset emits OSC 104 rather than replaying a previously set color.

---

## 7. T133 decision

T133 completes OSC 104 indexed-palette reset as a typed, bounded, session-serialized terminal-policy operation.

The indexed-palette family now has:

- OSC 4 typed mutation;
- OSC 4 typed observation;
- OSC 104 one/multiple/all terminal-policy reset.

T134 may now build OSC 10–12 / 110–112 common dynamic colors on the same `TerminalColor` and active-query foundations.
