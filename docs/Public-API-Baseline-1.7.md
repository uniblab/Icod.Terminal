# Icod.Terminal Public API Baseline — 1.7.0

**Release:** `1.7.0`  
**Target frameworks:** `net8.0`, `net9.0`, `net10.0`

## Purpose

This document records the intentional additive public-surface change for `Icod.Terminal 1.7.0` while retaining the 1.0 through 1.4 baselines as compatibility evidence.

Versions 1.5 and 1.6 deliberately changed no public API. Version 1.7 introduces the first backend-neutral raw-raster model and one semantic raster-display operation after the internal DCS/Sixel framing, quantization, encoding, streaming, and capability-evidence layers were validated independently.

## Exact machine fingerprint

The deterministic reflection snapshot generated independently for `net8.0`, `net9.0`, and `net10.0` is identical across all three target frameworks.

After normalizing line endings to LF, the reviewed 1.7 SHA-256 is:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

The authoritative fingerprint is stored in:

`docs/Public-API-Baseline-1.7.sha256`

`packaging/VerifyPublicApiBaseline.ps1` regenerates all three snapshots, proves that they agree, and validates this current 1.7 fingerprint.

## Intentional 1.7 additions

Version 1.7 adds these public raster types:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
```

`TerminalRasterPixelFormat` is a closed three-value enum:

```text
Rgb24
Rgba32
Indexed8
```

`TerminalRasterColor` is a readonly RGBA8 value with a public constructor and `Red`, `Green`, `Blue`, and `Alpha` properties. Alpha defaults to `255`.

`TerminalRasterImage` adds the following public construction and inspection surface:

```csharp
public static TerminalRasterImage CreateRgb24(
	int width,
	int height,
	ReadOnlySpan<byte> pixels
);

public static TerminalRasterImage CreateRgba32(
	int width,
	int height,
	ReadOnlySpan<byte> pixels
);

public static TerminalRasterImage CreateIndexed8(
	int width,
	int height,
	ReadOnlySpan<byte> pixels,
	ReadOnlySpan<TerminalRasterColor> palette
);

public int Width { get; }
public int Height { get; }
public TerminalRasterPixelFormat PixelFormat { get; }
public int PixelCount { get; }

public TerminalRasterColor GetPixelColor(
	int x,
	int y
);
```

The factories snapshot caller storage. The public surface deliberately does not expose backing arrays, mutable memory, row strides, Sixel palette registers, or encoded protocol bytes.

Version 1.7 also adds this semantic `TerminalSession` operation:

```csharp
public ValueTask<TerminalControlMutationResult> DisplayRasterAsync(
	TerminalRasterImage image,
	CancellationToken cancellationToken = default
);
```

The operation accepts image intent rather than a terminal-protocol backend. In 1.7 it may execute through verified Sixel support. The same method and raster types are intended to remain valid when Kitty Graphics is added as another backend in 1.8.

## Result and cancellation semantics

`DisplayRasterAsync(...)` reuses the existing `TerminalControlMutationResult` contract rather than adding another result family:

```text
Available    raster emitted successfully
Unavailable  no verified implemented raster backend is currently available
Unsupported  the selected backend cannot preserve the supplied raster semantics
```

Caller cancellation is honored before output commitment. After the first committed graphics control-string byte, cancellation cannot truncate the frame.

Transport exceptions after commitment continue to propagate because a partial remote terminal write has uncertain state and must not be disguised as an ordinary capability result.

## Compatibility anchors

The retained prior public API fingerprints remain unchanged:

```text
1.0  8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
1.1  455736a45b31ee9d29cd655e4b22cb90ea819ce4616d555ca45c7cb13ac5acc1
1.2  dabfaf0231743136a57d4f5632209e14d9c545638e3b8a08205ae5d14faaae26
1.3  0d3d05cc3100f39efde730c05b4d1c91dccbf0c8f510f2465f10ca082bf6bb53
1.4  3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
```

Versions 1.5 and 1.6 retain the 1.4 fingerprint because they introduced no public-surface delta.

Version 1.7 preserves every prior public member while adding the reviewed raster surface above.

## Protocol boundary

The 1.7 public API deliberately does **not** add:

- a generic raw DCS writer;
- a public Sixel encoder or command builder;
- palette-register control;
- DCS parameter injection;
- explicit graphics-backend selection;
- public quantizer configuration;
- PNG/JPEG/GIF decoding;
- placement or persistent image identifiers;
- animation;
- Kitty Graphics protocol APIs;
- terminal-brand automatic activation.

Those concerns remain internal, deferred, or outside the terminal library as documented by the D170–D178 contracts.

## Baseline rule

Any later public-surface change must be deliberate under `docs/Compatibility-and-Versioning.md`. Accidental removals, renames, signature drift, enum renumbering, target-framework divergence, or exposure of protocol-specific implementation details remain release blockers.
