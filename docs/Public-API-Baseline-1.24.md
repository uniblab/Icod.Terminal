# Icod.Terminal 1.24 Public API Baseline

The `1.24.0` surface retains every published 1.23 signature and enum value. Its intentional additive contract consists of:

- the immutable positive `TerminalPixelDimensions` value;
- `TerminalPixelGeometry.TryDeriveCellDimensions(...)` for exact, side-effect-free derivation;
- `TerminalSession.QueryTerminalPixelDimensionsAsync(...)`;
- `TerminalSession.QueryCellPixelDimensionsAsync(...)`;
- `TerminalRasterResource.PixelWidth`;
- `TerminalRasterResource.PixelHeight`;
- the twelve-property `TerminalRasterPlanningSnapshot` value;
- `TerminalSession.GetRasterPlanningSnapshot()`;
- the three-value `TerminalRasterOperation` enum;
- the library-constructed `TerminalRasterOperationStatus` value; and
- `TerminalSession.InspectRasterOperation(...)`.

Both queries reuse the authoritative bounded query/input coordinator. They add no cache, retry, second reader, terminal-brand branch, or unsupported inference.

The CI-generated normalized public API snapshots must remain identical across `net8.0`, `net9.0`, and `net10.0`. After the reviewed T2401–T2405 additions, the expected fingerprint is:

```text
fc3ebf0fb2f6561084deb2fd49a5043373a27da1486a222f932a7a91fd43f6ca
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.24.sha256`. The pre-feature fingerprint was `47ce550ebe58219a46bb711b789608592c3cad34c3c3607ffc7aaf054c8c5358`, identical to the [published 1.23 baseline](Public-API-Baseline-1.23.md); the T2401/T2402 geometry-query fingerprint was `41576a33a971ef9634ac0e409ba06c8e264f2d3d264ab8445b983e7d5ebf0fa2`, the T2403 resource-geometry fingerprint was `c02db531c9f029635ff436785a8a862b4e5f2ce2665e9c22f5347cc3a6759809`, and the T2404 planning-snapshot fingerprint was `67dcb10e5a1dad58db9e4c83e13fda38976e3ac68195fcec7a7f8bbb1b8ca2d8`. Each later intentional public addition must update this baseline only after its exact names, values, XML semantics, failure behavior, and compatibility constraints have passed the API-regret gate.

The representative consumer is a DCurses-facing, TermInfo-free tile presenter using a uniform atlas, opaque placeholder cells, caller-owned damage, two known animation frames, and explicit text fallback. Terminal does not acquire tile maps, viewport policy, damage tracking, asset decoding, or game rules through this contract.
