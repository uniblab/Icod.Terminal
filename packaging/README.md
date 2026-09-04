# Icod.Terminal build and packaging workflow

This directory carries the repository-local copy of the `uniblab/.github` C#/.NET build and packaging pattern, specialized only where `Icod.Terminal` has a stronger package contract.

## Validation ladder

| Lifecycle | Configuration | Work |
| --- | --- | --- |
| local `build.cmd` / `build.sh` | `Debug` | clean, restore, build, test, pack, exact package validation |
| pull request | `Staging` | Windows/Linux/macOS build and test; Linux also validates the generated package |
| `main` | `Release` | six-runner Windows/Linux/macOS x64/ARM64 distribution validation |
| `v<semver>` tag | `Release` | package production, exact verification, publication, and GitHub Release |

`Icod.Terminal` is a library package. The executable projects in the solution are samples and are deliberately excluded from release-archive discovery.

## Local build

`build.cmd` and `build.sh` are thin wrappers over `Invoke-Build.ps1`. The default Debug sequence is:

```text
clean -> restore -> build -> test -> pack -> validate
```

A single section may also be requested, for example `build.cmd test` or `./build.sh pack`.

## Exact package verification

`VerifyPackageArtifact.ps1` retains the stronger Icod.Terminal release gate while adopting the common PowerShell entry point. It:

- selects exactly the expected `Icod.Terminal` package version;
- requires the matching `.snupkg`;
- runs `tools/package-verifier` for package structure, metadata, dependency closure, assembly identity, XML documentation, portable symbols, and Source Link;
- restores a package-reference-only consumer from an isolated temporary directory and NuGet cache; and
- runs that consumer for `net8.0`, `net9.0`, and `net10.0`.

The old host-specific `.github/scripts/verify-release-package.cmd` and `.sh` wrappers are therefore unnecessary.

## Release selection

`SelectReleasePackages.ps1` follows the organization template's tag-version filtering and additionally copies the matching symbol package so NuGet symbol publication and GitHub Release assets remain intact.

A release tag must use `v<semver>` and point to a commit contained in the default branch. Only package artifacts whose actual nuspec version equals the tag version are selected for publication.
