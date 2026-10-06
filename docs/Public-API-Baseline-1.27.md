# Icod.Terminal 1.27 Public API Baseline

The additive environment-awareness API preserves existing 1.x signatures and enum values. It adds TerminalAppearance (Unknown=0, Dark=1, Light=2), TerminalAppearanceEvent, TerminalInBandResizeEvent, independent appearance/resize reporting leases, and TerminalSession.QueryAppearanceAsync / AcquireAppearanceReportingAsync / AcquireInBandResizeReportingAsync. Each operation accepts TimeSpan and an optional CancellationToken. Semantic event kinds append Appearance=1 and InBandResize=2 after Notification=0, with mutually exclusive typed payloads.

Snapshots generated from the candidate are byte-identical across net8.0, net9.0 and net10.0. SHA-256:

```text
356f455ec27065c63a642ae3d5b02125d2aed408d728cb6fadd0d47aeab12487
```

The machine-readable fingerprint is [Public-API-Baseline-1.27.sha256](Public-API-Baseline-1.27.sha256). The [1.25 baseline](Public-API-Baseline-1.25.md) remains historical evidence; 1.26 added no runtime API. A fingerprint qualifies API shape; it does not establish live emulator support.
