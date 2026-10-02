# Icod.Terminal 1.22 Public API Baseline

The additive 1.22 surface consists of `TerminalRasterAnimation.ComposeFrameAsync(TerminalRasterAnimationFrame, TerminalRasterAnimationFrame, TerminalRasterSourceRectangle, int, int, TerminalRasterFrameCompositionMode, CancellationToken)` and the `TerminalRasterFrameCompositionMode` enum (`AlphaBlend = 0`, `Replace = 1`). Existing source geometry, mutation results, frame ownership, and capability types are reused. All 1.21 public signatures and enum values remain unchanged.

The CI-generated normalized public API snapshots are identical across `net8.0`, `net9.0`, and `net10.0` at implementation commit `d619471177f25696978e9c0567301423279ee175`:

```text
61bcebdcff55a16a17d4e5a2546421c89fddc017ca3a05ea5d2673faa39cbe34
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.22.sha256`. The [1.21 baseline](Public-API-Baseline-1.21.md) remains historical evidence. The final candidate must requalify this fingerprint at its own exact source head.
