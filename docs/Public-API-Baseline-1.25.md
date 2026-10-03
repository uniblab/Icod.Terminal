# Icod.Terminal 1.25 Public API Baseline

The 1.25 surface retains every published 1.24.1 signature and enum value. The sole intentional addition is `TerminalScreenOutputTransaction.WriteRaster(TerminalRasterImage image)`.

The member accepts an immutable image in the same caller-supplied order as semantic cursor plans, application text, hyperlinks, and persistent raster placeholder cells. It does not expose a protocol selector, raster bytes, image identity, or layout policy. A null image fails at addition; a verified ordinary `RasterGraphics` backend is required at commit. Predictable unsupported raster semantics and encoded-payload overflow fail before transaction output.

The CI-generated normalized API snapshots were equal across net8.0, net9.0, and net10.0 at implementation commit `63494cb4ca8495848e2e49444193e8f963e6c67b`. The reviewed fingerprint is:

```text
886a617d961af7eed37feaed026d83bbf06ec508ba248a4ca492baaf7e528146
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.25.sha256`. The published 1.24 baseline remains `fc3ebf0fb2f6561084deb2fd49a5043373a27da1486a222f932a7a91fd43f6ca`. This fingerprint records API shape; later qualification must independently prove package, runtime, and terminal behavior.
