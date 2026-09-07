# T186 — Compatibility, Package, and Stable Closure

**Release:** `Icod.Terminal 0.18.0`  
**PR:** #31  
**Package-contract validation:** workflow #898  
**Stable-head validation:** workflow #902  
**Final audit additions:** presentation rollback double-failure regression and documentation closure; post-audit validation pending

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

## Final rollback audit

The final pre-merge audit found one direct regression gap in `TerminalPresentationManager`: successful rollback after partial presentation failure was tested, but presentation transition failure plus rollback failure was not.

A focused regression now proves:

1. alternate-screen entry succeeds;
2. keypad entry fails;
3. rollback attempts alternate-screen exit and that rollback fails too;
4. the caller receives an `AggregateException` preserving both failures;
5. the failed acquisition retains no ghost presentation lease;
6. a later clean alternate-screen acquisition/release succeeds through the invalidated-state recovery path.

This matches the already-proven rich-input rollback semantics and requires no production/API change.

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

## Stable validation history

Workflow #902 passed the complete stable PR matrix on the exact stable candidate head before the final audit additions. It covered Windows, Linux, and macOS, all real DCurses acceptance including the hardening soak, exact Staging package validation, retained 0.8–0.17 package contracts, and the 0.18 hardening package contract.

Because the final audit adds one regression test and documentation-only closure edits, the resulting exact head must pass the same PR matrix once more before merge.

## Stable release sequence

The release sequence is intentionally strict:

1. exact stable `0.18.0` version is already set;
2. workflow #902 established a green stable baseline;
3. validate the final post-audit exact PR head on Windows, Linux, and macOS with all downstream and 0.8–0.18 package gates;
4. merge PR #31 only if that exact head is green;
5. validate the exact resulting `main` commit under Release across the configured six x64/ARM64 runners;
6. create/push tag `v0.18.0` only with explicit authorization because tagging triggers publication.

## Closure status

Implementation, public/package compatibility, stable versioning, and documentation closure are complete. The only remaining PR gate is exact post-audit head validation.
