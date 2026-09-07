# T197 — Final Downstream RC Acceptance and Release-Candidate Closure

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T196 — package metadata/documentation artifact closure  
**Status:** Implementation complete; exact-head validation pending

## 1. Purpose

T197 is the final release-candidate acceptance tranche. It adds no production API, terminal protocol, wire behavior, or new semantic test scenario.

The tranche closes the one remaining artifact-boundary gap: existing real `Icod.DCurses` acceptance uses the current `Icod.Terminal` project reference, while package validation consumes the NuGet package without exercising the full downstream curses ownership soak.

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

## 3. Soak contract retained

For net8.0, net9.0, and net10.0 the package-based acceptance repeats eight complete ownership cycles covering:

- fresh `TerminalSession` creation;
- rich-input protocol ownership;
- Kitty keyboard negotiation;
- transfer of TerminalSession ownership into real `CursesSession`;
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

## 4. Isolated package resolution

`VerifyDCursesRc1Package.ps1` creates a temporary consumer with an isolated NuGet package cache and exactly two package sources:

1. the current validation artifact directory containing `Icod.Terminal 1.0.0-rc1`;
2. nuget.org for published dependencies, including `Icod.DCurses 0.1.0`.

The verifier restores once and runs the same soak under net8.0, net9.0, and net10.0.

This proves that the package artifact and published downstream package resolve and execute together without a project-reference side channel.

## 5. Workflow integration

The package-based downstream acceptance runs after:

- exact package verification;
- retained package-only contracts from 0.8 through 0.18;
- the new T196 1.0 release-candidate package contract.

It is wired into both PR Staging validation and `VerifyDistribution.ps1`.

After merge, the distribution path therefore exercises the fresh-package downstream soak on each configured Release runner:

- Windows x64;
- Windows ARM64;
- Linux x64;
- Linux ARM64;
- macOS x64;
- macOS ARM64.

## 6. Final RC audit

After the substantive T197 head is green, release-candidate closure requires one final audit of the complete PR from published `0.18.0` to rc1.

The audit must confirm:

- version remains exactly `1.0.0-rc1` / assembly `1.0.0.0`;
- public API fingerprint remains unchanged from the T194 freeze;
- the only intentional public breaking correction is removal of `TerminalSession.Input`;
- no production protocol/feature expansion entered rc1;
- all current permanent docs are version-current and do not say “in progress” or “validation pending” after closure;
- the package README, nuspec release notes, current roadmap, migration guide, and API baseline agree;
- the original long-form roadmap remains preserved under `docs/history/`;
- samples remain task-oriented and compile on all supported TFMs;
- no unresolved PR review thread or release-blocking review remains;
- PR remains mergeable against the intended `main` base.

## 7. Final exact-head rule

The first green T197 implementation head is not by itself merge authority if the audit requires documentation/status edits afterward.

Any final closure-only commit must itself receive a fresh exact-head PR validation. PR #32 is merge-ready only when that final exact head is green.

## 8. Post-merge release rule

Merging rc1 does not itself authorize publication.

After merge:

1. identify the exact resulting `main` commit;
2. require full Release distribution validation on all configured x64/ARM64 runners;
3. review the resulting package artifacts;
4. create the `v1.0.0-rc1` tag only with explicit publication authorization.

Tagging triggers publication and is never an automatic consequence of PR merge readiness.

## 9. Exit gate

T197 closes only when:

- the fresh-package + published-DCurses acceptance passes on all supported TFMs;
- the full existing PR matrix remains green;
- the complete rc1 audit finds no release-blocking gap;
- any closure/status changes have passed their own exact-head validation.

At that point PR #32 is ready for merge review as the `1.0.0-rc1` release candidate.
