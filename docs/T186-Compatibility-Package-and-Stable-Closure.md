# T186 — Compatibility, Package, and Stable Closure

**Release:** `Icod.Terminal 0.18.0`  
**PR:** #31  
**Package-contract validation:** workflow #898  
**Stable-head validation:** pending

## Scope

T186 freezes the 0.18 compatibility contract and prepares the stable release candidate. The release intentionally carries no public API signature delta from 0.17.

## Public API compatibility

The changed production files in 0.18 are limited to private lifecycle/teardown availability checks inside existing state-acquisition paths.

No public type, member, enum value, method signature, or wire protocol was added, removed, or renamed.

The stable public baseline is therefore:

- all 0.17 public API remains intact;
- all earlier public API remains intact;
- 0.18 adds behavioral guarantees only.

Reference: `docs/Public-API-Baseline-0.18.md`.

## 0.18 package contract

A dedicated package-only smoke consumes only the freshly packed `Icod.Terminal` NuGet artifact using an isolated temporary NuGet source and package cache.

On net8.0, net9.0, and net10.0 it proves through public APIs that:

- rich-input acquisition works before teardown;
- presentation acquisition works before teardown;
- both restore normally;
- once `TerminalSession.DisposeAsync()` begins, new input-protocol acquisition is rejected with `ObjectDisposedException`;
- new presentation acquisition is rejected with `ObjectDisposedException`;
- rejected post-teardown acquisitions emit no terminal-state output.

The suspend-preparation half of the acquisition barrier remains covered by internal repository tests because the injectable lifecycle source/signal seams are intentionally internal and are not part of the package surface.

Workflow #898 validated the corrected package-only gate successfully.

## Retained compatibility gates

Stable 0.18 retains all package contracts from 0.8 through 0.17:

- cursor style;
- synchronized output;
- terminal progress;
- pointer shape;
- semantic prompt;
- terminal colors;
- lifecycle-safe color ownership;
- OSC 133 extended semantic metadata;
- safe OSC 9;
- modern keyboard.

The general exact-package verifier continues to validate multi-target package contents, assembly identity, metadata, dependency closure, symbols, Source Link, and older 0.4–0.7 package/documentation contracts.

## Downstream integration

The real `Icod.DCurses` acceptance matrix remains part of PR and distribution validation, including the T185 hardening soak.

The soak runs eight complete ownership cycles per TFM and exercises full-screen handoff, rich input, Kitty negotiation, refresh, input decoding, owner-driven cleanup, stale-lease disposal, and native mode apply/restore symmetry.

## Stable release sequence

The release sequence is intentionally strict:

1. remove the prerelease suffix and produce exact version `0.18.0`;
2. validate the exact stable PR head on Windows, Linux, and macOS;
3. require all downstream and 0.8–0.18 package gates to pass on that exact head;
4. review PR #31 for merge readiness;
5. merge only after the exact stable PR head is green;
6. validate the exact resulting `main` commit under Release across the configured six x64/ARM64 runners;
7. create/push tag `v0.18.0` only with explicit authorization because tagging triggers publication.

## Closure status

Implementation and package-contract work are complete.

The remaining T186 gate is exact stable PR-head validation after the final stable version and documentation updates.
