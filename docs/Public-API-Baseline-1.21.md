# Icod.Terminal 1.21 Public API Baseline

The 1.21 input expansion decodes additional Kitty keyboard frames into the existing semantic input event model. It introduces no public input types or enum values.

The only new public API is the instance method `TerminalScreenOutputTransaction.SetCursorVisibilityForCommit(TerminalCursorVisibility)`; the enum already exists and belongs to presentation leases. The method stores an opt-in temporary request and emits nothing until a successful transaction admission. All 1.20 signatures and enum numeric values remain unchanged.

The CI-generated API snapshots for `net8.0`, `net9.0`, and `net10.0` are identical at the 1.21 implementation checkpoint:

```text
939649e1d5c110039cfb3e5057561f8ef7fcb4de20af2de2152f9ec6ab357824
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.21.sha256`. The [1.20 baseline](Public-API-Baseline-1.20.md) remains frozen. Requalify the fingerprint at the final source head before stable release closure.
