# T195 — Compatibility, Migration, and Support Policy

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T194 — public API/XML/sample regret closure, workflow #947  
**Status:** Implementation complete; exact-head validation pending

## 1. Purpose

T195 converts the frozen rc1 public surface and permanent semantic documentation into an explicit long-term 1.x compatibility policy and a practical migration path from `0.18.0`.

No runtime feature, protocol, or public API is added by this tranche.

## 2. Permanent authorities

T195 adds:

- `docs/Compatibility-and-Versioning.md`;
- `docs/Migration-to-1.0.md`.

These become the consumer-facing authorities for package versioning, compatibility expectations, TFM/platform support, deprecation, and pre-1.0 migration.

## 3. SemVer policy frozen

The 1.x line uses semantic versioning with a conservative compatibility posture.

- patch: defect fixes, hardening, documentation/test improvements, and compatible implementation improvements;
- minor: compatible additive API/semantic protocol capability with explicit baseline review;
- major: ordinary removals/renames/signature breaks, enum renumbering, or incompatible changes to documented ownership/security/restoration semantics.

A bug fix may correct behavior that violated an already-frozen contract, but release notes must identify compatibility-sensitive corrections rather than silently redefining the contract.

## 4. Machine baseline role

The T194 fingerprint remains the rc1 exact exported surface baseline.

T195 clarifies that this fingerprint is an enforcement/review gate, not a prohibition on all future 1.x additions. Compatible additions require an intentional minor-release baseline update; accidental drift remains a CI failure.

Existing public enum numeric values remain stable throughout 1.x. New enum members, if ever justified, require explicit minor-release compatibility review and must not renumber existing values.

## 5. Behavioral compatibility

Compatibility includes documented semantics beyond signatures, including:

- one authoritative input reader;
- bounded query routing and late-response ownership;
- cancellation commit boundaries;
- exact restoration versus terminal-policy reset;
- lease/session disposal authority;
- resource ceilings;
- truthful unsupported/unavailable/failure distinctions;
- privacy/security boundaries;
- safe OSC 9 exclusions.

Minor/patch releases may improve correctness while preserving those guarantees; they do not silently weaken them.

## 6. TFM and platform support

The first-class package targets remain:

```text
net8.0
net9.0
net10.0
```

Vendor EOL alone is not grounds to remove net8/net9. A concrete security/toolchain/maintenance blocker is required before reconsideration.

The built-in system provider supports Windows, Linux, and macOS. Other operating systems receive controlled `Unsupported` results unless a custom provider/transport implementation is supplied through the public interfaces.

## 7. Deprecation policy

Ordinary 1.x API removal should be preceded by a supported replacement, migration documentation, and an obsolete/deprecation interval where safe and practical.

Exceptional security or unsupportable-contract cases may require a narrower/faster break, but must be explicitly documented.

## 8. Protocol support policy

Package compatibility and terminal protocol support remain distinct.

A successful write normally proves emission, not terminal recognition. Timeout does not automatically prove unsupported behavior, and terminal brand/TERM/environment identity does not automatically prove support.

Negotiated or explicitly queried evidence remains authoritative where available.

## 9. Migration result

The migration from `0.18.0` is intentionally narrow.

The one breaking public correction is removal of `TerminalSession.Input`.

Consumers must replace raw live-session reads with:

- `ReadEventAsync(...)` for application input; or
- typed query APIs for correlated terminal responses.

`ITerminalInput` remains public for custom open/injection and caller-owned lifetime management. `TerminalSession.Output` remains available as an explicitly advanced unsynchronized borrowed transport.

For consumers that did not use `TerminalSession.Input`, migration should generally be a package-version update plus normal cross-platform regression testing.

## 10. Direct vs DCurses guidance

T195 freezes the architectural guidance:

- direct consumers use `TerminalSession` for live terminal mechanics and semantic protocols;
- `Icod.DCurses` consumers use the higher-level virtual-screen/presentation layer and honor its terminal-session ownership transfer;
- applications should not create multiple independent state-owning sessions over the same physical terminal as a substitute for layering.

## 11. T194 closure evidence

T194 is closed by workflow #947 at exact head:

`86ff0cfc923314aca35bcf8fb731a97bd0ade526`

That run passed:

- Windows/Linux/macOS build and tests;
- frozen 1.0 public API fingerprint verification;
- focused real `Icod.DCurses` acceptance and hardening soak;
- exact Staging package verification;
- all retained package-only contracts from 0.8 through 0.18.

## 12. Exit gate

T195 closes when the exact documentation head passes the full PR matrix without public API fingerprint drift.

After that, T196 may proceed with package metadata, README/package documentation artifacts, and a fresh package-only 1.0-rc1 contract.
