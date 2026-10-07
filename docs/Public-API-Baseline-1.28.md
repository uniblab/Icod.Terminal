# Icod.Terminal 1.28 Public API Baseline

The additive Kitty Graphics transaction-confirmation API preserves existing 1.x
signatures and enum values. It adds
`TerminalControlMutationConfirmation` (`Unspecified=0`, `OutputCommitted=1`,
`ProtocolAcknowledged=2`) and the read-only
`TerminalControlMutationResult.Confirmation` property. Existing
`TerminalControlMutationResult.Success()` behavior and every public animation
method signature remain unchanged.

Snapshots generated from the candidate are byte-identical across net8.0, net9.0,
and net10.0. SHA-256:

```text
0cad933938bf9d52ecd568323f05fdc9079597e6ed3029755a74fffead7c0fcc
```

The machine-readable fingerprint is
[Public-API-Baseline-1.28.sha256](Public-API-Baseline-1.28.sha256). The
[1.27 baseline](Public-API-Baseline-1.27.md) remains historical evidence. A
fingerprint qualifies API shape; it does not establish protocol acknowledgement,
rendering, or live emulator support.
