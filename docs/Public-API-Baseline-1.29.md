# Icod.Terminal 1.29 Public API Baseline

The additive clipboard contract introduces `TerminalClipboardOptions`, its public
parameterless constructor, static `DefaultMaximumBytes` and `SupportedMaximumBytes`,
and independent init-only `MaximumReadBytes` / `MaximumWriteBytes` properties.
`TerminalSessionOptions.ClipboardOptions` is init-only and defaults to new options.
Existing method signatures, default limits and enum values are preserved.

Snapshots generated from the alpha implementation are identical on net8.0,
net9.0 and net10.0. SHA-256:

```text
3239c1682724f7a4df4b55cf1c29117d23d3bfd88e23c3c8c715a5d48cced517
```

The machine-readable fingerprint is [Public-API-Baseline-1.29.sha256](Public-API-Baseline-1.29.sha256).
The [1.28 baseline](Public-API-Baseline-1.28.md) remains historical evidence.
This qualifies API shape, not terminal capacity or clipboard acceptance.
