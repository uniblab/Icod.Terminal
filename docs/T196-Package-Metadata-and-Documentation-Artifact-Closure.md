# T196 — Package Metadata and Documentation Artifact Closure

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T195 — compatibility/migration/support policy  
**Status:** Implementation complete; exact-head validation pending

## 1. Purpose

T196 makes the NuGet artifact itself tell the same 1.x story as the source repository and adds a fresh package-only release-candidate contract.

No runtime feature, terminal protocol, or public API is added by this tranche.

## 2. Package metadata audit

The package metadata audit accepts the existing:

- package ID and title;
- author metadata;
- package description;
- LGPL-3.0-or-later license expression and acceptance requirement;
- icon and README packaging;
- repository/project URLs;
- symbols/source-link settings;
- deterministic/package-validation settings;
- dependency versions;
- existing feature/search tags.

No churn is introduced merely to make metadata look new for 1.0.

The concrete metadata defect was stale `0.16.0` package release notes. T196 replaces them with `1.0.0-rc1` release-candidate notes covering the contract freeze, permanent documentation, machine API freeze, package/downstream validation, and the one pre-1.0 API correction (`TerminalSession.Input`).

## 3. Package README closure

The root/package `README.md` is rewritten for `1.0.0-rc1`.

It now documents:

- rc1 status and installation;
- the layer architecture;
- a minimal `TerminalSession` example;
- one-authoritative-reader semantics;
- bounded query routing;
- exact restoration vs terminal-policy reset vs Icod-owned state vs ephemeral metadata;
- semantic terminal feature inventory;
- modern keyboard posture;
- security/privacy boundaries;
- compatibility/versioning policy;
- Windows/Linux/macOS built-in platform support;
- permanent 1.x documentation links;
- sample navigation and release validation.

The packed README is therefore a current release artifact rather than a 0.18 historical summary.

## 4. Current roadmap authority

The original root `Icod.Terminal-Development-Roadmap.md` still described 0.3 as the active target even though it contained valuable early architecture/design reasoning.

T196 preserves that exact historical blob at:

`docs/history/Icod.Terminal-Initial-Development-Roadmap.md`

and replaces the root file with a concise permanent roadmap/index pointing to:

- the current `1.0.0-rc1` roadmap;
- the permanent 1.x contract documents;
- the historical design record;
- current release validation discipline.

No historical design evidence is discarded.

## 5. Fresh 1.0 package-only contract

T196 adds:

- `tools/package-rc1-smoke/Icod.Terminal.PackageRc1Smoke.csproj`;
- `tools/package-rc1-smoke/Program.cs`;
- `packaging/VerifyRc1Package.ps1`.

The verifier consumes only the freshly packed NuGet artifact plus its declared NuGet dependencies.

For net8.0, net9.0, and net10.0 it proves the release-candidate package:

- has a 1.x assembly version;
- does not expose public `TerminalSession.Input`;
- retains public `TerminalSession.Output`;
- retains `ITerminalInput`, `ITerminalOutput`, and `ITerminalControlProvider` injection seams;
- exposes representative session/event/query/state/output operations from the accumulated 1.x contract;
- retains key enum numeric anchors from the frozen rc1 baseline.

## 6. Package documentation artifact assertions

`VerifyRc1Package.ps1` also inspects the `.nupkg` directly.

It requires:

- nuspec release notes containing the current package version and the `TerminalSession.Input` migration correction;
- packed `README.md` containing the current package version and permanent compatibility/migration document links;
- generated XML documentation for net8.0/net9.0/net10.0;
- XML documentation for `ITerminalInput`, `TerminalSession.Output`, and canonical `ReadEventAsync(...)` overloads;
- absence of the removed `P:Icod.Terminal.TerminalSession.Input` member from generated XML.

This makes README/release-note/XML staleness a package gate rather than a manual release-review item.

## 7. Workflow integration

The 1.0 package contract runs after the retained 0.18 hardening package gate in PR Staging validation.

`VerifyDistribution.ps1` runs the same gate during full Release distribution validation, which is invoked by the six configured Windows/Linux/macOS x64/ARM64 `main` jobs.

Historical package-only contracts from 0.8 through 0.18 remain retained. The rc1 gate does not pretend to replace their feature-specific evidence yet.

## 8. T195 closure evidence

T195 closes when workflow #952 succeeds at exact head:

`676aaff4ac552eeb3e824fb7b6027ef241b3c486`

That exact-head gate is the predecessor evidence for T196.

## 9. Exit gate

T196 closes when the exact package/documentation head passes:

- Windows/Linux/macOS build and tests;
- the frozen public-API fingerprint;
- real `Icod.DCurses` focused acceptance and hardening soak;
- exact Staging package verification;
- all retained 0.8–0.18 package contracts;
- the new fresh 1.0 release-candidate package contract.

After that, T197 may perform final downstream RC acceptance, documentation/status audit, and release-candidate closure.
