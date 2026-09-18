# Icod.Terminal 1.18 Public API Baseline

This document records the additive public API for the `Icod.Terminal 1.18.0` rendition-baseline release.

The stable compatibility floor remains `1.0.0`. The complete 1.17 public surface remains available and unchanged. The predecessor 1.17 public API fingerprint is:

```text
c0a051a925d551e526343ef59d8c47d75e41868d84235fa30bfa7debe1b3ceb9
```

The generated public API snapshots are identical on `net8.0`, `net9.0`, and `net10.0`. The 1.18 fingerprint is:

```text
48975f2c42f6c544e9c574a9b3d79f7e2b7b3ecb10ab1a5a0b7067749e38e65d
```

The machine-readable fingerprint is stored in `docs/Public-API-Baseline-1.18.sha256` and is enforced by the package/public-API gate.

## Public addition over 1.17

`TerminalScreenPlanner.PlanRenditionBaseline()` returns an opaque, session-bound rendition plan that restores the normalized default rendition without assuming a known physical starting state. It returns `null` when any attribute or color axis exposed by the selected terminal profile cannot be restored unconditionally.

The plan prefers the global attribute reset; otherwise it emits every required safe specific attribute exit in stable order, followed by original-color restoration when a selectable color axis is exposed. A profile with no exposed rendition entry or selection capability receives a valid zero-byte plan, including reset-only profiles.

The method is additive and exposes no `Icod.TermInfo` type, capability identifier, terminal string, or expansion API. Existing rendition normalization, transition, reset, operation-plan ownership, and transaction behavior remain unchanged.
