# Icod.Terminal 0.13 Public API Baseline

**Release:** `0.13.0`  
**Theme:** observable terminal palette and dynamic-color control

---

## 1. Public value types

### `TerminalColor`

```csharp
public readonly struct TerminalColor : IEquatable<TerminalColor> {
	public TerminalColor(
		ushort red,
		ushort green,
		ushort blue
	);

	public ushort Red { get; }
	public ushort Green { get; }
	public ushort Blue { get; }

	public static TerminalColor FromRgb8(
		byte red,
		byte green,
		byte blue
	);

	public bool Equals( TerminalColor other );
	public override bool Equals( object? obj );
	public override int GetHashCode();

	public static bool operator ==(
		TerminalColor left,
		TerminalColor right
	);

	public static bool operator !=(
		TerminalColor left,
		TerminalColor right
	);
}
```

`TerminalColor` is normalized 16-bit RGB. It carries no alpha channel, color-space tag, raw wire spelling, terminal identity, or `System.Drawing` dependency.

`FromRgb8(...)` expands each byte by multiplying by 257 so both endpoints and repeated-byte precision are preserved.

Equality, `GetHashCode()`, `==`, and `!=` are defined over the three normalized 16-bit channel values.

### `TerminalPaletteColor`

```csharp
public readonly record struct TerminalPaletteColor {
	public TerminalPaletteColor(
		byte index,
		TerminalColor color
	);

	public byte Index { get; }
	public TerminalColor Color { get; }
}
```

The indexed-palette domain is exactly `0..255` and therefore uses `byte` directly.

### `TerminalDynamicColor`

```csharp
public enum TerminalDynamicColor {
	DefaultForeground,
	DefaultBackground,
	TextCursor,
	MouseForeground,
	MouseBackground,
	HighlightBackground,
	HighlightForeground
}
```

The enum is semantic rather than numeric. Tektronix OSC 15/16/18 identities are deliberately absent.

---

## 2. Indexed-palette API

```csharp
ValueTask SetPaletteColorAsync(
	byte index,
	TerminalColor color,
	CancellationToken cancellationToken = default
);

ValueTask SetPaletteColorsAsync(
	IReadOnlyList<TerminalPaletteColor> entries,
	CancellationToken cancellationToken = default
);

ValueTask<TerminalColor> QueryPaletteColorAsync(
	byte index,
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

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

Wire mapping:

```text
SetPaletteColorAsync(i,c)     -> OSC 4 ; i ; rgb:rrrr/gggg/bbbb ST
QueryPaletteColorAsync(i,...) -> OSC 4 ; i ; ? ST
ResetPaletteColorAsync(i)     -> OSC 104 ; i ST
ResetPaletteColorsAsync(...)  -> OSC 104 ; i ; i ... ST
ResetPaletteAsync()           -> OSC 104 ST
```

`SetPaletteColorsAsync(...)` may encode multiple distinct index/color pairs in one OSC 4 frame. It is bounded to 256 entries, rejects an empty collection and duplicate indices, validates before commitment, and preserves caller order.

---

## 3. Dynamic-color API

```csharp
ValueTask SetDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	CancellationToken cancellationToken = default
);

ValueTask<TerminalColor> QueryDynamicColorAsync(
	TerminalDynamicColor kind,
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

ValueTask ResetDynamicColorAsync(
	TerminalDynamicColor kind,
	CancellationToken cancellationToken = default
);
```

Semantic wire mapping:

```text
DefaultForeground   OSC 10 / reset 110
DefaultBackground   OSC 11 / reset 111
TextCursor          OSC 12 / reset 112
MouseForeground     OSC 13 / reset 113
MouseBackground     OSC 14 / reset 114
HighlightBackground OSC 17 / reset 117
HighlightForeground OSC 19 / reset 119
```

OSC 10–12 are the common/core interoperability tier. OSC 13/14/17/19 are the extended xterm tier and may have lower cross-terminal support.

---

## 4. Color wire grammar

Canonical outbound color encoding is exactly:

```text
rgb:rrrr/gggg/bbbb
```

with four lowercase hexadecimal digits per 16-bit component and canonical ST (`ESC \\`) OSC termination.

Inbound observations accept strict equal-width 1–4 digit `rgb:` components and these hash forms:

```text
#RGB
#RRGGBB
#RRRGGGBBB
#RRRRGGGGBBBB
```

`rgb:` shorthand scales component precision to the complete 16-bit range. Hash shorthand supplies the most-significant bits and zero-fills the remaining low bits. The two grammars are intentionally not normalized by the same rule.

Named colors, `rgbi:`, CSS syntax, alpha forms, mixed-width `rgb:` components, whitespace, and trailing junk are outside the 0.13 parser contract.

---

## 5. Observation semantics

Palette and dynamic-color queries reuse the existing `TerminalSession` active-query transaction and input-response router.

A successful query means only that a matching conforming response was received for that transaction and parsed to a typed `TerminalColor`.

Failure remains explicit:

- caller cancellation remains cancellation;
- no matching response before the deadline is `TimeoutException`;
- a correlated malformed color response is `FormatException`;
- transport/session failure propagates normally.

Timeout is not converted into an `Unsupported` boolean and successful observations are not cached as authoritative terminal state.

No second input reader, raw OSC observation stream, background color listener, or automatic open-time probing is introduced.

---

## 6. Mutation and reset semantics

Mutation/reset operations participate in the existing session output-serialization domain.

- known redirected output is rejected;
- cancellation is observed before output commitment;
- a complete frame is constructed before commitment;
- committed writes use `CancellationToken.None`;
- mutation/reset methods do not implicitly flush;
- successful completion proves emission only, not terminal recognition.

OSC 104 and OSC 110–119 are **terminal-policy reset** operations. They do not mean exact restoration of a color previously observed by this library.

---

## 7. Lifecycle and ownership posture

0.13 deliberately exposes no palette-color or dynamic-color lease.

All 0.13 color setters are unscoped. `InvalidateState()`, suspend/resume, and `DisposeAsync()` do not automatically query, reset, or replay color state.

This is intentional. A lifecycle-safe restoration lease would need to establish a truthful baseline and, after resume, re-observe state before reapplying owned color. The current lifecycle architecture re-enables active queries only after lifecycle participants finish resuming, so 0.13 does not alter core lifecycle/query ordering merely to manufacture a color lease.

---

## 8. Downstream contract

The observation API is designed for direct higher-level consumption. T137 proves real `Icod.DCurses 0.1.0` can consume typed 16-bit `TerminalColor` observations, explicitly adapt them to its current 8-bit `CursesColor.Rgb` representation, and render them without parsing raw OSC or opening another input path.

Color-distance metrics, nearest-palette selection, contrast/accessibility policy, and theme inference remain downstream responsibilities rather than `Icod.Terminal` policy.

---

## 9. Deliberate exclusions

0.13 does not add:

- arbitrary public OSC construction;
- raw public color-specification strings;
- X11 named-color injection;
- OSC 15/16/18 or resets 115/116/118;
- terminal-emulator detection as a support oracle;
- automatic palette probing;
- an authoritative long-lived terminal-color cache;
- `System.Drawing` dependency;
- lifecycle-safe color leases;
- downstream color-selection or accessibility policy.

This baseline is the stable 0.13 public contract.
