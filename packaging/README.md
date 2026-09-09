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

`Icod.Terminal` is a library package. The executable projects in the solution are samples/verification utilities and are deliberately excluded from release-archive discovery.

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

## Runtime versus package validation

Runtime/architecture evidence and portable package evidence are separate contracts.

`VerifyRuntime.ps1` owns solution restore/build/test, focused sample/integration compilation, real `Icod.DCurses` integration checks, and repeated hardening/ownership soak. These checks run on every OS/architecture claimed by the relevant workflow.

`BuildPackageArtifact.ps1` builds the portable package candidate once and verifies the frozen current public API fingerprint. CI uploads that candidate for independent package-contract jobs rather than rebuilding the same RID-independent NuGet package on every architecture.

For 1.7 the current machine public API fingerprint is:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

from `docs/Public-API-Baseline-1.7.sha256`. Historical baselines remain checked in unchanged.

## Package contract shards

`VerifyPackageContractShard.ps1` divides package-only evidence into independent shards which consume the same package artifact:

| Shard | Contracts |
| --- | --- |
| `foundation` | exact package structure/Source Link/XML plus 0.8–0.10 package foundations |
| `presentation` | pointer shape, semantic prompt, colors, and lifecycle-safe color ownership |
| `semantic` | semantic metadata, safe OSC 9, OSC 777, OSC 633, OSC 1337, OSC 99, modern keyboard, **1.7 raster graphics**, and hardening |
| `release` | stable 1.x release-line package contract and packaged `Icod.DCurses` compatibility witness |

The semantic shard includes fresh package-only consumers and generated XML-documentation checks for additive stable APIs.

### Raster package verification

`VerifyRasterGraphicsPackage.ps1` qualifies the public 1.7 raster contract from the freshly packed NuGet artifact. It:

- verifies generated XML documentation for `TerminalRasterPixelFormat`, `TerminalRasterColor`, `TerminalRasterImage`, their reviewed public members, and `TerminalSession.DisplayRasterAsync(...)` under `lib/net8.0`, `lib/net9.0`, and `lib/net10.0`;
- restores `tools/package-raster-graphics-smoke` from an isolated temporary directory against the freshly built package;
- compiles and runs the consumer on all three TFMs;
- compile-binds `DisplayRasterAsync(...)` without a repository project reference;
- constructs RGB24, RGBA32, and Indexed8 images and verifies owned snapshot/value semantics;
- verifies the frozen public raster enum values;
- rejects accidental public raw DCS/Sixel dispatch or public raster backing-memory exposure.

The raster consumer is silent on success and fails by exception so package validation output stays focused on failures and orchestration.

The raster verifier runs anywhere the semantic package shard runs: pull requests, `main`, manual distribution validation, and tag/release qualification.

The semantic shard also retains package-only/XML checks for OSC 633, OSC 777, OSC 1337, OSC 99, modern keyboard, and earlier semantic contracts.

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

The tag workflow's `metadata` job resolves the exact tag version. Jobs that consume that version declare `metadata` as a direct dependency so version and prerelease state remain available to package-contract and GitHub-release stages.

The tag workflow runs runtime acceptance and package preparation in parallel. After the package candidate exists, all four package shards must pass before either registry publication job may start. NuGet.org and GitHub Packages publish independently in parallel; the GitHub Release is created only after both registry publications succeed.

`SelectReleasePackages.ps1` filters by exact tag version and copies the matching symbol package. The GitHub Release attaches the `.nupkg`, `.snupkg`, and SHA-256 checksum file and uses the curated release-note document rather than auto-generated prose.

## 1.7 release-closure rule

For `1.7.0`, D179 requires one unchanged final pull-request head to pass:

```text
Runtime Windows
Runtime Linux
Runtime macOS
Package candidate / API baseline
Package Foundation
Package Presentation
Package Semantic and hardening
Package Stable 1.x release line
Validated package artifact
```

Only after that exact-head Staging matrix succeeds may the PR leave draft status. Merge, post-merge Release validation, and `v1.7.0` tagging/publication remain separate explicit steps.

## Organization template follow-up

Repository-local workflow improvements are proven here first. Generic portions should subsequently be upstreamed to `uniblab/.github` where appropriate, while package-specific semantic/raster verification remains local to `Icod.Terminal`.
