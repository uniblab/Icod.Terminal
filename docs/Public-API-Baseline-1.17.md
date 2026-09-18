# Icod.Terminal 1.17 Public API Baseline

This document records the final additive public API for the `Icod.Terminal 1.17.0` screen-planning and session-bound output release.

The stable compatibility floor remains `1.0.0`. The complete 1.16 public surface remains available and unchanged. The predecessor 1.16 public API fingerprint is:

```text
d2acfa85aad87c739b3f682096d4d7139627f12bc9d8981b65529eeb79a2da8d
```

The generated public API snapshots are identical on `net8.0`, `net9.0`, and `net10.0`. The final 1.17 fingerprint is:

```text
c0a051a925d551e526343ef59d8c47d75e41868d84235fa30bfa7debe1b3ceb9
```

The machine-readable fingerprint is stored in `docs/Public-API-Baseline-1.17.sha256` and is enforced by the stable package/public-API gate.

## Public additions over 1.16

### Dimensions and profile

- `TerminalDimensions` provides positive Terminal-owned columns and rows.
- `TerminalSession.GetDimensions()` projects the existing live-size result without removing `GetSize()`.
- `TerminalLifecycleEvent.Dimensions` accompanies the existing `Size` property.
- `TerminalSession.Profile` exposes immutable terminal identity metadata and `TerminalScreenCapabilities` without exposing a new TermInfo-bearing screen contract.

### Semantic screen vocabulary and planner

The new Terminal-owned vocabulary includes screen positions, colors, rendition attributes, line glyphs, alerts, erases, character/line shifts, scrolling regions, operation kinds, and opaque operation plans.

`TerminalSession.Screen` returns the session-bound `TerminalScreenPlanner`. The planner normalizes safe reversible rendition, resolves supported ACS glyphs, and plans cursor, alert, rendition, ACS, erase, shift, scroll, and region operations. Plans expose only semantic kind, encoded byte cost, and affected-line count; their terminal strings and capability identities remain private.

### Session-bound output transaction

`TerminalSession.CreateScreenOutputTransaction(...)` returns a bounded single-use transaction. It accepts same-session operation plans, application text, strict OSC 8 hyperlink text, and current same-session raster-placeholder cells in caller-supplied order.

`TerminalScreenOutputTransactionOptions.UseSynchronizedOutput` requests synchronized-output framing. Commit validates retained ownership, rejects a stale serialized-output epoch before writing, holds the existing session output gate, ignores ordinary cancellation after commitment, attempts required hyperlink/synchronization cleanup, flushes once, and surfaces independent failures.

## Dependency and ownership boundary

The new screen types contain no public `Icod.TermInfo` signatures. TermInfo remains Terminal's private capability-data, expansion, padding, and color authority. Terminal does not acquire cells, windows, layout, clipping, Unicode-width, damage, desired-versus-physical comparison, or repaint policy.
