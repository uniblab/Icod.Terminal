# Compatibility and Versioning

This document defines the permanent compatibility and versioning policy for the `Icod.Terminal` 1.x line.

The public API fingerprint, permanent semantic documentation, package contracts, and downstream acceptance tests together define the supported 1.x contract. Compatibility is not limited to “does it compile?”; ownership, restoration, cancellation, query routing, resource bounds, and security behavior are also part of the contract where documented as guarantees.

## 1. Versioning model

`Icod.Terminal` uses semantic versioning for the public package line.

For stable 1.x releases:

- a **patch** release fixes defects, strengthens tests/documentation, improves performance, or hardens implementation without intentionally breaking the documented 1.x contract;
- a **minor** release may add compatible public APIs, semantic protocol support, or new optional behavior while preserving existing public signatures and documented guarantees;
- a **major** release is required for ordinary intentional source/binary breaks, removal or incompatible reinterpretation of public members, enum renumbering, or incompatible changes to documented ownership/security/restoration semantics.

A bug fix may change observed behavior when the previous behavior violated the already-documented contract. Such a correction is not treated as permission to redefine the contract silently; release notes must identify compatibility-sensitive corrections.

## 2. Machine public-API baselines

The stable `1.0.0` exported surface remains frozen by:

- `docs/Public-API-Baseline-1.0.md`;
- `docs/Public-API-Baseline-1.0.sha256`.

Stable 1.0 intentionally adopted the same fingerprint qualified by `1.0.0-rc1`; the rc1 baseline remains historical qualification evidence.

Compatible minor-release additions receive separate reviewed baselines rather than overwriting earlier evidence:

- `docs/Public-API-Baseline-1.1.md` / `.sha256` record the additive OSC 633 surface;
- `docs/Public-API-Baseline-1.2.md` / `.sha256` record the additive OSC 777 titled-notification surface;
- `docs/Public-API-Baseline-1.3.md` / `.sha256` record the additive typed iTerm2 OSC 1337 surface;
- `docs/Public-API-Baseline-1.4.md` / `.sha256` record the additive typed Kitty OSC 99 notification/query surface.

`packaging/VerifyPublicApiBaseline.ps1` points at the current release baseline. It regenerates the reflection snapshot independently for `net8.0`, `net9.0`, and `net10.0`, proves the three surfaces agree, and verifies the current fingerprint. Older baseline files remain checked in as compatibility evidence.

The reflection snapshot covers exported types, constructors, methods, properties, interfaces, nullability/default information represented by the generator, constants, and public enum numeric values across all supported TFMs.

The fingerprint is a review gate, not a declaration that 1.x can never grow. An intentional compatible addition in a minor release requires an explicit new/current baseline in the same reviewed change. Accidental drift must fail CI.

## 3. Source and binary compatibility

Within the stable 1.x line, ordinary releases should preserve existing public type/member names and signatures.

The following are compatibility-sensitive and normally require a major release if they affect an existing public contract:

- removing or renaming a public type/member;
- changing parameter order or types;
- changing a return type incompatibly;
- making an optional parameter required;
- tightening nullability in a way that rejects previously valid calls;
- changing implemented public interfaces incompatibly;
- changing public enum numeric values;
- changing a public constant value when callers may have compiled it into their assemblies;
- changing an existing operation from supported behavior to unconditional failure without an exceptional compatibility reason.

Compatible overloads, new types, and new semantic operations may be introduced in a minor release when they do not make existing source/binary behavior ambiguous or unsafe.

## 4. Public enums

Existing public enum numeric values are stable throughout 1.x.

An existing value must not be renumbered or reused for a different meaning.

Adding an enum value is compatibility-sensitive even though it is normally binary-compatible. A new value therefore requires:

- a minor release;
- explicit compatibility review;
- an intentional public-API baseline update;
- documentation of how callers should handle previously unknown values.

For flags enums, new flags must use previously unused bits and must not reinterpret existing combinations.

The 1.4 `KittyNotificationOccasion` and `KittyNotificationUrgency` enums are newly introduced closed semantic sets. Their numeric values are therefore part of the 1.x compatibility contract from 1.4 onward.

## 5. Behavioral compatibility

The permanent documentation under `docs/` defines behavioral guarantees that are versioned alongside the API.

Examples include:

- one authoritative live-session input reader;
- query correlation and bounded late-response ownership;
- pre-commit versus post-commit cancellation behavior;
- truthful `Unavailable` / `Unsupported` / failure distinctions;
- exact restoration where an API promises exact restoration;
- terminal-policy reset where exact restoration is not claimed;
- session/lease ownership and disposal authority;
- parser/query resource ceilings;
- output serialization boundaries;
- explicit metadata/privacy disclosure;
- no generic hazardous OSC 9 execution/control surface;
- no generic raw OSC 633, OSC 777, OSC 1337, or OSC 99 dispatch replacing typed semantic APIs;
- no second input reader for OSC 99 unsolicited notification events.

A minor/patch release may strengthen correctness while preserving these guarantees, but should not silently weaken or reverse them.

## 6. Protocol support is not package compatibility

Terminal protocol support and NuGet package compatibility are different concerns.

A successful semantic output call proves emission, not terminal recognition or visual application unless a specific query/response contract provides stronger evidence.

Likewise:

- a timeout is not automatically evidence of “unsupported”;
- a terminal brand, `TERM`, environment variable, or operating-system identity is not automatically proof of protocol support;
- explicit negative protocol evidence may be represented as unsupported where the protocol provides such a response;
- negotiated reversible protocols may decline acquisition when safe restoration/ownership cannot be established.

The OSC 99 support/alive queries introduced by 1.4 are live observations through the existing response router. Silence remains a timeout and is not converted into permanent unsupported truth.

Future minor releases may add new semantic protocol APIs without changing the meaning of existing operations.

## 7. Deprecation policy

When an existing 1.x API can be replaced compatibly, deprecation is preferred before removal.

Ordinary removal should:

1. introduce or identify the supported replacement;
2. document the migration;
3. mark the old surface obsolete where practical;
4. preserve it for a reasonable 1.x migration interval when doing so is safe;
5. remove it only in a major release.

Deprecation is not an absolute requirement when retaining an API would create an active security vulnerability, make the documented contract impossible to satisfy, or require an unsupportable platform/runtime dependency. Such exceptional changes require explicit release documentation and the narrowest practical compatibility break.

## 8. Target frameworks

The stable 1.x contract targets:

```text
net8.0
net9.0
net10.0
```

All three are first-class package targets.

Vendor end-of-support alone is not sufficient reason to remove net8.0 or net9.0 from the Icod.Terminal contract. Removal requires a concrete security alert, security-fix incompatibility, runtime/toolchain blocker, or equivalent security/maintenance constraint that prevents responsible support.

Dropping a target framework is a compatibility-sensitive package change and must be documented explicitly.

## 9. Operating-system support

The built-in `SystemTerminalControlProvider` provides native terminal-control behavior for:

- Windows;
- Linux;
- macOS.

Other operating systems receive controlled `Unsupported` results from the built-in provider rather than fabricated POSIX/Windows behavior.

The public injection interfaces remain available for custom platform/transport implementations:

- `ITerminalControlProvider`;
- `ITerminalInput`;
- `ITerminalOutput`.

A platform may therefore be usable through a custom provider even when the built-in system provider does not implement it.

## 10. Architecture compatibility

The permanent layer boundaries are part of the support model:

- `Icod.TermInfo` remains the immutable capability authority;
- `Icod.Terminal` owns the live terminal conversation and reversible terminal/session mechanics;
- `Icod.DCurses` owns the higher-level virtual-screen/curses presentation model;
- PTY/process hosting remains orthogonal rather than hidden inside `Icod.Terminal`.

A future release may improve implementations behind these boundaries without requiring consumers to adopt platform-native mode manipulation or a second terminal parser.

## 11. Direct consumers and Icod.DCurses

Direct consumers should use `TerminalSession` when they need live terminal/session mechanics without a curses virtual-screen model.

Applications that need windows, cells, virtual-screen diff/refresh, or a curses-style presentation model should normally use `Icod.DCurses` and allow that layer to own the supplied `TerminalSession` according to its documented integration contract.

Do not create two independent state-owning sessions over the same physical terminal merely to divide responsibilities. Ownership of one physical terminal conversation should remain unambiguous.

## 12. Security compatibility

Security boundaries are compatibility commitments, not optional implementation details.

In particular, 1.x does not use a minor/patch release to quietly introduce:

- generic raw OSC/CSI/DCS vendor dispatch as the ordinary API;
- hazardous OSC 9 macro/process/environment/emulator-control operations;
- generic OSC 633, OSC 777, OSC 1337, or OSC 99 dispatch that bypasses reviewed typed semantic surfaces;
- invasive OSC 1337 profile/focus/browser/pasteboard/file-transfer/custom-script operations without separate security review;
- automatic clipboard reads;
- automatic command-line or shell/environment metadata capture/redaction assumptions;
- hidden process/network/browser/OS-clipboard/host-notification side effects;
- terminal-brand-triggered activation of state that cannot be restored truthfully;
- automatic notification- or shell-integration-protocol routing presented as capability truth without a negotiation contract;
- a competing OSC 99 event reader that bypasses `TerminalSession.ReadEventAsync(...)` and the authoritative response router.

Buttons and unsolicited OSC 99 activation/close reports require a future reviewed `TerminalEvent` extension rather than an ad hoc callback/raw-reader surface.

New security-sensitive semantic features require explicit API, bounded validation, documentation, and tests.

## 13. Compatibility evidence

A release is not considered compatible merely because unit tests pass.

The repository maintains layered evidence including:

- retained historical machine public-API fingerprints plus the current release fingerprint;
- Windows/Linux/macOS build and tests;
- real `Icod.DCurses` acceptance paths;
- repeated ownership/disposal soak;
- exact NuGet artifact/XML verification;
- fresh package-only consumers for newly added semantic APIs;
- retained package-only consumers for historical stable contracts;
- release/distribution validation on configured architectures.

These gates may evolve or be consolidated, but equivalent coverage must exist before historical compatibility gates are removed.
