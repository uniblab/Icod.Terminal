# T197 — Final Downstream RC Acceptance and Release-Candidate Closure

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T196 — package metadata/documentation artifact closure, workflow #954  
**Status:** Complete; PR #32 merged and the merged `main` candidate passed the six-runner Release distribution matrix  
**Substantive T197 validation:** workflow #956 at `039927f555f8f109a31423825dc0e2191a5c2ac6`  
**Final PR validation:** workflow #957 at `254ceea67f5ca01070af15d928faba630d31af79`  
**Merged `main`:** `852d84722c6d9b5f91b5c6dcf9176f26ded78982`  
**Merged Release validation:** run `34163501979` — green on all six configured runners

## 1. Purpose

T197 is the final release-candidate acceptance tranche. It adds no production API, terminal protocol, wire behavior, or new semantic test scenario.

The tranche closes the artifact-boundary gap between repository/project-reference downstream acceptance and the freshly packed NuGet package.

T197 runs the existing eight-cycle hardening soak unchanged against the **freshly packed `Icod.Terminal 1.0.0-rc1` NuGet artifact** plus published `Icod.DCurses 0.1.0`.

## 2. Package-to-downstream acceptance

T197 adds:

- `tools/dcurses-rc1-package-acceptance/Icod.Terminal.DCursesRc1PackageAcceptance.csproj`;
- `tools/dcurses-rc1-package-acceptance/Program.cs`;
- `packaging/VerifyDCursesRc1Package.ps1`.

The program body is byte-for-byte the existing `tools/dcurses-hardening-soak/Program.cs`. Only the dependency boundary changes:

```text
before: ProjectReference -> current Icod.Terminal source
now:    PackageReference -> freshly packed Icod.Terminal artifact
        PackageReference -> published Icod.DCurses 0.1.0
```

This isolates package/dependency/downstream compatibility from behavioral-test design.

## 3. Scope of downstream evidence

`Icod.DCurses 0.1.0` is an early downstream library, not a mature exhaustive exerciser of every `Icod.Terminal` 1.x contract.

The T197 gate is therefore interpreted as a **real compatibility witness for the integration surface DCurses currently exercises**. It proves that the current published downstream package resolves and runs correctly over the freshly packed rc1 artifact for the covered ownership, presentation, and input paths.

It does **not** claim that DCurses alone proves every `Icod.Terminal` feature, protocol, lifecycle branch, query contract, or security boundary.

Release confidence for areas beyond current DCurses coverage rests primarily on `Icod.Terminal`'s own:

- frozen public-API/enum fingerprint;
- repository unit and hardening tests;
- parser/query/lifecycle/concurrency/rollback/platform invariants;
- exact package verification;
- retained package-only contracts from 0.8 through 0.18;
- fresh rc1 NuGet-only contract on net8.0/net9.0/net10.0.

This distinction is intentional and leaves room for deeper downstream acceptance as `Icod.DCurses` itself advances toward 1.0.

## 4. Soak contract retained

For net8.0, net9.0, and net10.0 the package-based acceptance repeats eight complete ownership cycles covering:

- fresh `TerminalSession` creation;
- rich-input protocol ownership;
- Kitty keyboard negotiation;
- transfer of `TerminalSession` ownership into real `CursesSession`;
- full-screen presentation entry;
- real `CursesSession.RefreshAsync()` output;
- Kitty key decoding;
- focus event decoding;
- bracketed-paste decoding;
- DCurses/TerminalSession teardown;
- exact screen/input protocol leave traffic;
- stale lease disposal after owner cleanup;
- exactly one native mode apply and one native mode restoration per cycle.

No test assertion is weakened for package mode.

## 5. Isolated package resolution

`VerifyDCursesRc1Package.ps1` creates a temporary consumer with an isolated NuGet package cache and exactly two package sources:

1. the current validation artifact directory containing `Icod.Terminal 1.0.0-rc1`;
2. nuget.org for published dependencies, including `Icod.DCurses 0.1.0`.

The verifier restores once and runs the same soak under net8.0, net9.0, and net10.0.

This proves that the package artifact and published downstream package resolve and execute together without a project-reference side channel.

## 6. PR validation

Workflow #956 passed the substantive T197 head at:

`039927f555f8f109a31423825dc0e2191a5c2ac6`

including:

- Windows/Linux/macOS build and tests;
- frozen public-API fingerprint verification;
- project-reference DCurses focused acceptance and hardening soak;
- exact Staging package verification;
- every retained 0.8–0.18 package contract;
- the fresh rc1 NuGet-only contract;
- the new fresh-package + published-DCurses acceptance on net8.0/net9.0/net10.0.

The closure-only head then passed workflow #957 at:

`254ceea67f5ca01070af15d928faba630d31af79`

No later runtime/API change entered PR #32.

## 7. Final RC audit result

The complete PR from published `0.18.0` to rc1 was audited before merge.

The audit confirmed:

- version remained exactly `1.0.0-rc1` / assembly `1.0.0.0`;
- the public API fingerprint remained the T194 freeze;
- the only intentional public breaking correction was removal of `TerminalSession.Input`;
- `ITerminalInput`/`ITerminalOutput`/`ITerminalControlProvider` injection remained public;
- `TerminalSession.Output` remained the explicitly documented advanced borrowed transport;
- all other production-source changes were version-neutral documentation/error-text cleanup rather than protocol or behavior expansion;
- no new terminal protocol or feature family entered rc1;
- package README, nuspec release notes, current roadmap, migration guide, API baseline, and permanent authorities agreed;
- the original long-form roadmap remained preserved under `docs/history/`;
- samples remained task-oriented and compiled under the supported framework matrix;
- no additional sample was justified merely for rc1;
- no additional Terminal-owned regression gap was found that justified expanding the release candidate;
- no unresolved PR review/comment thread remained;
- PR #32 was mergeable against `main`.

## 8. Merged Release qualification

PR #32 merged to `main` as:

`852d84722c6d9b5f91b5c6dcf9176f26ded78982`

Release workflow run `34163501979` completed successfully on:

- Windows x64;
- Windows ARM64;
- Linux x64;
- Linux ARM64;
- macOS x64;
- macOS ARM64.

Each runner executed the distribution verifier. This post-merge qualification proves the merged candidate, not merely the PR merge simulation.

## 9. Publication hardening follow-up

The merged Release audit identified a release-process/documentation gap outside the runtime contract: the tag-triggered `release.yaml` had not yet been brought up to parity with the qualified `main` distribution path and GitHub Releases still relied on sparse auto-generated notes.

A focused publication-hardening follow-up therefore adds:

- tag-time frozen public-API verification;
- tag-time hardening soak;
- tag-time 0.18 hardening, rc1 package, and package-boundary downstream gates;
- curated `docs/releases/<version>.md` notes as a required release artifact;
- a root changelog;
- concise NuGet release notes with immutable release/migration links;
- immutable tag-pinned package documentation links.

These changes do not alter T197's runtime/API conclusion. They must independently pass PR and post-merge Release validation before tagging.

## 10. Publication rule

Tagging triggers publication and is never an automatic consequence of merge or green CI.

Before `v1.0.0-rc1` is created:

1. merge the publication-hardening follow-up only after its exact PR head is green;
2. require the resulting `main` commit to pass the full six-runner Release matrix;
3. review the curated release notes/package metadata one final time;
4. create the tag only with explicit publication authorization.

No further T197 status mutation is required merely to insert that later publication-hardening workflow number.
