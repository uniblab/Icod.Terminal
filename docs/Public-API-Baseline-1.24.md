# Icod.Terminal 1.24 Public API Baseline

The pre-feature `1.24.0-alpha` surface is intentionally identical to the published 1.23 surface. It is the API-regret gate for the additive pixel-geometry, persistent-resource geometry, bounded raster-planning, and generation-scoped operation-evidence contracts specified for 1.24.

The CI-generated normalized public API snapshots must remain identical across `net8.0`, `net9.0`, and `net10.0`. At T2400, before any new public member is introduced, the expected fingerprint is:

```text
47ce550ebe58219a46bb711b789608592c3cad34c3c3607ffc7aaf054c8c5358
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.24.sha256`. Each intentional public addition must update this baseline only after its exact names, values, XML semantics, failure behavior, and compatibility constraints have passed the API-regret gate. The [1.23 baseline](Public-API-Baseline-1.23.md) remains the published comparison authority.

The representative consumer is a DCurses-facing, TermInfo-free tile presenter using a uniform atlas, opaque placeholder cells, caller-owned damage, two known animation frames, and explicit text fallback. Terminal does not acquire tile maps, viewport policy, damage tracking, asset decoding, or game rules through this contract.
