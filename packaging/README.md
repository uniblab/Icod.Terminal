# Icod.Terminal build and packaging workflow

This directory carries the repository-local copy of the `uniblab/.github` C#/.NET build and packaging pattern, specialized where `Icod.Terminal` has a stronger package contract.

## Validation ladder

| Lifecycle | Configuration | Work |
| --- | --- | --- |
| local `build.cmd` / `build.sh` | `Debug` | incremental restore/build/test/pack/exact package validation; `clean` is explicit |
| pull request | `Staging` | Windows/Linux/macOS runtime validation in parallel; one Linux package candidate; four parallel package-contract shards |
| `main` | `Release` | six Windows/Linux/macOS x64/ARM64 runtime jobs plus one portable package candidate and four package-contract shards |
| manual distribution validation | selected | same six-runner runtime/package split as `main` |
| `v<semver>` tag | `Release` | tag metadata gate; runtime acceptance and package preparation in parallel; four package shards; parallel registry publication; curated GitHub Release |

`Icod.Terminal` is a library package. The executable projects in the solution are samples and are deliberately excluded from release-archive discovery.

## Local build

`build.cmd` and `build.sh` are thin wrappers over `Invoke-Build.ps1`.

The default Debug sequence is incremental:

```text
restore -> build -> test -> pack -> validate
```

`clean` is intentionally **not** part of the default path. Use `build.cmd clean` or `./build.sh clean` when a clean rebuild is actually required.

Named targets are self-contained rather than assuming earlier manual stages:

```text
clean     clean
restore   restore
build     restore -> build
test      restore -> build -> test
pack      restore -> build -> pack
validate  restore -> build -> pack -> validate
all       restore -> build -> test -> pack -> validate
```

This preserves the comprehensive default while allowing normal MSBuild incremental behavior between developer invocations.

## Runtime versus package validation

Runtime/architecture evidence and portable package evidence are separate contracts.

`VerifyRuntime.ps1` owns:

- solution restore/build/test;
- focused notification-sample compilation;
- real `Icod.DCurses` focused integration checks;
- repeated DCurses hardening/ownership soak.

These checks run on every OS/architecture claimed by the relevant workflow.

`BuildPackageArtifact.ps1` builds the portable package candidate once and verifies the frozen public API fingerprint. CI then uploads that candidate for independent package-contract jobs rather than rebuilding the same RID-independent NuGet package on every architecture.

## Package contract shards

`VerifyPackageContractShard.ps1` divides package-only evidence into independent shards which consume the same package artifact:

| Shard | Contracts |
| --- | --- |
| `foundation` | exact package structure/Source Link/XML plus 0.8–0.10 package foundations |
| `presentation` | 0.11 pointer shape, 0.12 semantic prompt, 0.13 colors, 0.14 lifecycle-safe color ownership |
| `semantic` | 0.15 semantic metadata, 0.16 safe OSC 9, 0.17 modern keyboard, 0.18 hardening |
| `release` | stable 1.0 release-line package contract and packaged `Icod.DCurses` compatibility witness |

The shards have no ordering dependency on one another once the package candidate exists, so GitHub Actions executes them in parallel. `VerifyPackageDistribution.ps1` runs the same shards sequentially when a single-process local distribution check is desired.

## Exact package verification

`VerifyPackageArtifact.ps1` remains the foundation package gate. It:

- selects exactly the expected `Icod.Terminal` package version;
- requires the matching `.snupkg`;
- runs `tools/package-verifier` for package structure, metadata, dependency closure, assembly identity, XML documentation, portable symbols, and Source Link;
- restores package-reference-only consumers from isolated temporary directories and NuGet caches; and
- runs those consumers for `net8.0`, `net9.0`, and `net10.0`.

Package-only consumers intentionally retain isolated caches because proving external NuGet resolution is part of their contract. Source/project-reference acceptance scripts use the normal runner cache instead of forcing `--no-cache` restores.

## Main and manual distribution validation

`Icod.Terminal` is AnyCPU and produces one RID-independent NuGet package. The six-runner Release matrix therefore focuses architecture-specific jobs on runtime/source behavior:

```text
Windows x64
Windows ARM64
Linux x64
Linux ARM64
macOS x64
macOS ARM64
```

A separate Linux package-preparation job builds the portable package once while those runtime jobs execute. Four package shards validate that single artifact in parallel.

This avoids repeating package layout/XML/Source Link and historical NuGet-consumer work six times while preserving the six-platform runtime contract.

## Tag publication

A release tag must use `v<semver>`, point to a commit contained in `main`, and have curated `docs/releases/<version>.md` notes.

The tag workflow runs runtime acceptance and package preparation in parallel. After the package candidate exists, all four package shards must pass before either registry publication job may start. NuGet.org and GitHub Packages publish independently in parallel; the GitHub Release is created only after both registry publications succeed.

`SelectReleasePackages.ps1` filters by the exact tag version and copies the matching symbol package. The GitHub Release attaches the `.nupkg`, `.snupkg`, and SHA-256 checksum file and uses the curated release-note document rather than auto-generated prose.

## Organization template follow-up

The local incremental-build and workflow-graph improvements are intentionally proven here first. The generic portions should subsequently be upstreamed to `uniblab/.github`, including avoiding absolute runner-specific solution paths in reusable metadata flows.
