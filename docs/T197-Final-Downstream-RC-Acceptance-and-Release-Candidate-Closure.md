# T197 — Final Downstream RC Acceptance and Release-Candidate Closure

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T196 — package metadata/documentation artifact closure, workflow #954  
**Status:** Implementation and final audit complete; the exact current PR head must be green for merge readiness  
**Substantive T197 validation:** workflow #956 at `039927f555f8f109a31423825dc0e2191a5c2ac6`

## 1. Purpose

T197 is the final release-candidate acceptance tranche. It adds no production API, terminal protocol, wire behavior, or new semantic test scenario.

The tranche closes the remaining artifact-boundary gap: existing real `Icod.DCurses` acceptance uses the current `Icod.Terminal` project reference, while package validation consumes the NuGet package without exercising the full downstream curses ownership soak.

T197 therefore runs the existing eight-cycle hardening soak unchanged against the **freshly packed `Icod.Terminal 1.0.0-rc1` NuGet artifact** plus published `Icod.DCurses 0.1.0`.

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

## 6. Workflow integration and validation

The package-based downstream acceptance runs after:

- exact package verification;
- retained package-only contracts from 0.8 through 0.18;
- the T196 1.0 release-candidate package contract.

It is wired into both PR Staging validation and `VerifyDistribution.ps1`.

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

After merge, the distribution path exercises the same package/downstream witness on each configured Release runner:

- Windows x64;
- Windows ARM64;
- Linux x64;
- Linux ARM64;
- macOS x64;
- macOS ARM64.

## 7. Final RC audit result

The complete PR from published `0.18.0` to rc1 was audited after workflow #956.

The audit confirms:

- version remains exactly `1.0.0-rc1` / assembly `1.0.0.0`;
- the public API fingerprint remains the T194 freeze;
- the only intentional public breaking correction is removal of `TerminalSession.Input`;
- `ITerminalInput`/`ITerminalOutput`/`ITerminalControlProvider` injection remains public;
- `TerminalSession.Output` remains the explicitly documented advanced borrowed transport;
- all other production-source changes are version-neutral documentation/error-text cleanup rather than protocol or behavior expansion;
- no new terminal protocol or feature family entered rc1;
- package README, nuspec release notes, current roadmap, migration guide, API baseline, and permanent authorities agree;
- the original long-form roadmap remains preserved under `docs/history/`;
- samples remain task-oriented and compile under the supported framework matrix;
- no additional sample is justified merely for rc1;
- no additional Terminal-owned regression gap was found that justifies expanding the release candidate;
- no unresolved PR review/comment thread is present;
- PR #32 remains open, non-draft, and mergeable against `main`.

## 8. Final exact-head rule

The green substantive T197 head is not by itself merge authority because this closure record and status wording are a later commit.

The **exact current PR head** must therefore pass the complete PR matrix before PR #32 is called merge-ready.

This wording is intentionally timeless: no further repository commit is required merely to insert the final workflow number after the closure head passes. The PR description may record that workflow without changing the tested tree.

## 9. Post-merge release rule

Merging rc1 does not itself authorize publication.

After merge:

1. identify the exact resulting `main` commit;
2. require full Release distribution validation on all configured x64/ARM64 runners;
3. review the resulting package artifacts;
4. create the `v1.0.0-rc1` tag only with explicit publication authorization.

Tagging triggers publication and is never an automatic consequence of PR merge readiness.

## 10. Exit gate

T197 is implementation/audit complete. PR #32 becomes merge-ready only when the exact current closure head is green across the complete PR matrix.
