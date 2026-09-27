# Icod.Terminal 1.19 Public API Baseline

Version 1.19 expands the behavior of the existing `PlanCursorMove(...)` method and qualifies the existing screen-output boundary. It adds no public library member, changes no existing signature, and changes no enum value. Cursor-visibility composition is deferred to preserve presentation-lease ownership.

The complete [1.18 public contract](Public-API-Baseline-1.18.md) remains available. Generated snapshots are identical for `net8.0`, `net9.0`, and `net10.0`, with the unchanged fingerprint:

```text
48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.19.sha256`. Historical baselines remain unchanged; the package verifier selects this release's baseline explicitly.
