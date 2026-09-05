# T132 — OSC 4 Indexed Palette Mutation and Observation

**Release:** `0.13.0`  
**Tranche:** `T132`  
**Development version:** `0.13.0-alpha.3`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T132 builds typed OSC 4 indexed-palette mutation and observation on the T130/T131 color contract without introducing a parallel input path, raw OSC API, or authoritative palette cache.

---

## 2. Public surface

T132 adds:

```csharp
public readonly record struct TerminalPaletteColor {
	public TerminalPaletteColor(
		byte index,
		TerminalColor color
	);

	public byte Index { get; }
	public TerminalColor Color { get; }
}

public ValueTask SetPaletteColorAsync(
	byte index,
	TerminalColor color,
	CancellationToken cancellationToken = default
);

public ValueTask SetPaletteColorsAsync(
	IReadOnlyList<TerminalPaletteColor> entries,
	CancellationToken cancellationToken = default
);

public ValueTask<TerminalColor> QueryPaletteColorAsync(
	byte index,
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

All palette indices remain in the frozen byte domain `0..255`.

---

## 3. Mutation framing

Single-entry mutation emits:

```text
OSC 4 ; index ; rgb:rrrr/gggg/bbbb ST
```

Bulk mutation uses the xterm repeated-pair grammar in one complete frame:

```text
OSC 4 ; index ; color ; index ; color ... ST
```

Bulk rules:

- one through 256 entries;
- duplicate indices are rejected;
- caller order is preserved;
- every entry is validated/encoded before output commitment;
- one complete frame is written;
- committed write uses `CancellationToken.None`;
- no implicit flush occurs.

The bulk operation is mutation-only in T132. No all-or-nothing bulk observation abstraction is invented over terminals that may answer repeated OSC 4 queries independently.

---

## 4. Observation framing and routing

Single-entry query emits canonical ST framing:

```text
OSC 4 ; index ; ? ST
```

Observation reuses the existing `TerminalSession.ExecuteQueryAsync(...)` transaction manager and OSC response router.

The response matcher correlates both:

- OSC family `4`;
- exact requested palette index.

A reply for another palette index does not satisfy the outstanding query.

The parser accepts the existing response framing forms supported by the shared OSC infrastructure:

- seven-bit `ESC ] ... BEL`;
- seven-bit `ESC ] ... ESC \\`;
- C1 `OSC ... ST`.

The color payload is parsed only through `TerminalColorCodec` and returned as a typed `TerminalColor`.

---

## 5. Failure semantics

T132 preserves the active-query distinctions:

- pre-cancelled query emits nothing;
- caller cancellation remains cancellation;
- timeout remains `TimeoutException`;
- a correlated reply with malformed color data fails with `FormatException`;
- an unrelated palette-index response does not satisfy the query;
- session/transport failures propagate normally.

No failure is converted into a permanent unsupported flag.

---

## 6. Observation truthfulness

`QueryPaletteColorAsync(...)` returns only a color explicitly reported for the requested index during that transaction.

T132 does not:

- synthesize fallback ANSI colors;
- assume an xterm default palette;
- cache a successful observation as authoritative state;
- infer support from terminal identity or environment;
- query automatically during session open or lifecycle events.

This keeps the observation suitable for future `Icod.DCurses` policy.

---

## 7. Testing

T132 tests cover:

- decimal palette-index width boundaries `0`, `9`, `10`, `99`, `100`, `255`;
- canonical color spelling and ST termination;
- repeated-pair bulk mutation ordering;
- empty, duplicate-index, and oversized bulk rejection;
- query framing at index boundaries;
- ST and BEL seven-bit responses;
- C1 OSC/ST response framing;
- typed color normalization through T131;
- wrong-index rejection/correlation;
- correlated malformed-color failure;
- pre-cancelled query no-emission behavior;
- session mutation no-flush behavior;
- non-cancellable committed mutation writes.

---

## 8. Deferred work

T132 deliberately does not implement:

- OSC 104 palette reset — T133;
- dynamic colors — T134/T135;
- scoped palette restoration — T136;
- bulk palette observation API beyond independently truthful single-index queries;
- automatic support detection;
- downstream color-selection policy.

---

## 9. Result

T132 establishes OSC 4 as a typed observable palette API over the same serialization and active-query architecture already used by `Icod.Terminal`.

T133 may now add OSC 104 reset semantics while preserving the frozen distinction between terminal-policy reset and exact restoration.
