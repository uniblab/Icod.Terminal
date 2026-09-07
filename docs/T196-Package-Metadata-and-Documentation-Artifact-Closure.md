# T196 — Package Metadata and Documentation Artifact Closure

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T195 — compatibility/migration/support policy, workflow #952  
**Status:** Complete and green  
**Exact-head validation:** workflow #954 at `09ef20a2b0eee2fe3202459a906e837001c7ba6d`

## 1. Purpose

T196 makes the NuGet artifact itself tell the same 1.x story as the source repository and adds a fresh package-only release-candidate contract.

No runtime feature, terminal protocol, or public API is added by this tranche.

## 2. Package metadata audit

The package metadata audit accepts the existing package ID/title, author, description, LGPL-3.0-or-later license, icon/readme packaging, repository/project URLs, symbols/source-link settings, deterministic/package-validation settings, dependency versions, and feature/search tags.

No metadata churn is introduced merely to make the package look new for 1.0.

The concrete metadata defect was stale `0.16.0` package release notes. T196 replaces them with `1.0.0-rc1` release-candidate notes covering the contract freeze, permanent documentation, machine API freeze, package/downstream validation, and the one pre-1.0 API correction (`TerminalSession.Input`).

## 3. Package README closure

The root/package `README.md` is rewritten for `1.0.0-rc1` and now documents rc1 installation/status, architecture, a minimal session example, one-authoritative-reader semantics, bounded queries, reversible-state categories, semantic features, modern keyboard policy, security/privacy, compatibility/versioning, built-in platform support, permanent 1.x documentation, samples, and release validation.

The packed README is therefore a current release artifact rather than a 0.18 historical summary.

## 4. Current roadmap authority

The original root `Icod.Terminal-Development-Roadmap.md` still described 0.3 as the active target while retaining valuable early architecture/design reasoning.

T196 preserves that exact historical blob at:

`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`

and replaces the root file with a concise permanent roadmap/index pointing to the current rc1 roadmap, permanent 1.x authorities, historical design record, and current release-validation discipline.

No historical design evidence is discarded.

## 5. Fresh 1.0 package-only contract

T196 adds:

- `tools/package-rc1-smoke/Icod.Terminal.PackageRc1Smoke.csproj`;
- `tools/package-rc1-smoke/Program.cs`;
- `packaging/VerifyRc1Package.ps1`.

The verifier consumes only the freshly packed NuGet artifact plus declared NuGet dependencies.

For net8.0, net9.0, and net10.0 it proves the release-candidate package has a 1.x assembly version, does not expose public `TerminalSession.Input`, retains `TerminalSession.Output` plus custom provider/input/output injection seams, exposes representative accumulated session/event/query/state/output APIs, and retains key enum numeric anchors from the frozen rc1 baseline.

## 6. Package documentation artifact assertions

`VerifyRc1Package.ps1` also inspects the `.nupkg` directly.

It requires:

- nuspec release notes containing the current package version and `TerminalSession.Input` migration correction;
- packed `README.md` containing the current package version and permanent compatibility/migration document links;
- generated XML documentation for net8.0/net9.0/net10.0;
- XML documentation for `ITerminalInput`, `TerminalSession.Output`, and canonical `ReadEventAsync(...)` overloads;
- absence of removed `P:Icod.Terminal.TerminalSession.Input` from generated XML.

This makes README/release-note/XML staleness a package gate rather than a manual release-review item.

## 7. Workflow integration

The 1.0 package contract runs after the retained 0.18 hardening package gate in PR Staging validation.

`VerifyDistribution.ps1` runs the same gate during full Release distribution validation, invoked by the six configured Windows/Linux/macOS x64/ARM64 `main` jobs.

Historical package-only contracts from 0.8 through 0.18 remain retained. The rc1 gate does not pretend to replace their feature-specific evidence yet.

## 8. Validation history

The first T196 exact head `a56ce84ea9ae4b943d0a3113a780432f8b68ad4a` reached the new rc1 package verifier only after every preceding gate had passed. Workflow #953 then failed because the new PowerShell verifier used an ambiguous multiline `if` expression and did not execute its assertions.

Commit `09ef20a2b0eee2fe3202459a906e837001c7ba6d` rewrote only verifier condition syntax into simple named booleans; package/API assertions were unchanged.

Workflow #954 then passed at that exact head. The rc1 package gate executed for real and succeeded after all retained build/test, public-API, DCurses, exact-package, and 0.8–0.18 package gates.

## 9. Exit criteria

T196 is complete because workflow #954 passed:

- Windows/Linux/macOS build and tests;
- the frozen public-API fingerprint;
- real `Icod.DCurses` focused acceptance and hardening soak;
- exact Staging package verification;
- all retained 0.8–0.18 package contracts;
- the new fresh 1.0 release-candidate package contract on net8.0/net9.0/net10.0;
- validated current nuspec release notes, packed README, and generated XML documentation.

T197 owns the remaining fresh-package-to-published-DCurses acceptance and final release-candidate closure audit.
