# Icod.Terminal 1.20 Public API Baseline

Version 1.20 retains every public 1.19 signature and enum value. It adds twelve members to the existing immutable `TerminalScreenCapabilities` value:

- Read-only Boolean properties: `AdvertisesCursorHome`, `AdvertisesCursorRowAddressing`, `AdvertisesCursorColumnAddressing`, `AdvertisesCarriageReturn`, `AdvertisesCursorUp`, `AdvertisesCursorDown`, `AdvertisesCursorLeft`, `AdvertisesCursorRight`, and `AdvertisesScrollRegion`.
- Boolean methods: `AdvertisesErase(TerminalScreenEraseKind)`, `AdvertisesCharacterShift(TerminalScreenCharacterShiftKind)`, and `AdvertisesLineShift(TerminalScreenLineShiftKind)`.

These are static presence facts, including present empty/malformed representations. They do not expand parameters or guarantee a concrete plan. Default values advertise no operations; unknown operation kinds throw `ArgumentOutOfRangeException`. Existing `Supports...` members retain their meaning. The additions expose no dependency-owned types or raw terminal strings.

Generated snapshots are identical for `net8.0`, `net9.0`, and `net10.0`:

```text
d308fb6ead5bd24c564d159297e6d08793c08eb4db21418eca6a5563aa5c4cbf
```

The machine-readable fingerprint is `docs/Public-API-Baseline-1.20.sha256`. The [1.19 baseline](Public-API-Baseline-1.19.md) and all earlier evidence remain unchanged. The runtime generation fix and source-linked sample add no other public contract.
