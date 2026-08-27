# T12C — Package and Fresh-Consumer Validation

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.13`
**Tranche:** T12C — package and fresh-consumer validation
**Reference branch:** `Icod.Terminal/0.1.0`
**Implementation status:** Complete; the package gate passed on Windows, Linux, and macOS and T12D may proceed

---

## 1. Purpose

T12C verifies the artifact which an independent application actually receives.

T01 established only a foundation check: require a `.nupkg` and `.snupkg`, then
run a repository sample. That was adequate while the package surface was still
being built, but it did not prove that the packed artifact had the intended
framework payloads, metadata, dependency closure, symbols, Source Link data, or
fresh-consumer behavior.

T12C replaces that foundation check with two complementary release gates:

1. structural verification of the packed NuGet and symbol archives;
2. compilation and execution of a consumer which has no project reference to
   the repository library.

The validation is deterministic and never requires the CI runner to own an
interactive terminal.

---

## 2. Development package version

T12C advances both:

```xml
<Version>0.1.0-alpha.13</Version>
<PackageVersion>0.1.0-alpha.13</PackageVersion>
```

The final non-prerelease `0.1.0` version remains T12D work.

---

## 3. Structural package verifier

T12C adds:

```text
tools/package-verifier/
    Icod.Terminal.PackageVerifier.csproj
    Program.cs
    README.md
```

The verifier reads the current package version from `Icod.Terminal.csproj` and
opens the corresponding `.nupkg` and `.snupkg` from the supplied artifact
directory.

### 3.1 Primary package payload

The primary package must contain:

```text
README.md
icon.png
lib/net8.0/Icod.Terminal.dll
lib/net8.0/Icod.Terminal.xml
lib/net10.0/Icod.Terminal.dll
lib/net10.0/Icod.Terminal.xml
```

The verifier also requires both XML documentation files to be non-empty,
parseable, identify assembly `Icod.Terminal`, and contain documented members.

The only managed DLL payloads permitted are the two framework-specific
`Icod.Terminal.dll` files. `Icod.TermInfo` and `Icod.Timing` remain NuGet
runtime dependencies rather than copied assemblies inside this package.

The primary package must not contain:

- PDBs, which belong in the symbol package;
- a `runtimes/` payload;
- native `.so`, `.dylib`, `.a`, or `.lib` payloads;
- repository-only `.github`, `docs`, `samples`, `tests`, or `tools` trees.

### 3.2 Assembly identity

The packaged `net8.0` and `net10.0` assemblies must both report:

```text
Assembly name:    Icod.Terminal
Assembly version: 0.1.0.0
Strong-name:      none
```

The package version may change through the prerelease sequence without changing
the `0.1.0.0` assembly identity.

### 3.3 NuGet metadata

The verifier checks the generated nuspec for the intentional package metadata:

- package id and title `Icod.Terminal`;
- author `Timothy J. Bruce`;
- project/repository URL `https://github.com/uniblab/Icod.Terminal`;
- `README.md` and `icon.png` metadata;
- LGPL-3.0-or-later license expression;
- required license acceptance;
- non-empty description and tags;
- git repository metadata containing a 40-character source commit.

`<Version>` and `<PackageVersion>` must remain present and identical before the
archive is inspected.

---

## 4. Dependency-closure gate

`Icod.Terminal` intentionally has two runtime NuGet dependencies:

```text
Icod.TermInfo 1.0.0
Icod.Timing   1.0.0
```

The generated nuspec must contain exactly those two dependencies for each
supported target-framework dependency group and no additional runtime package.

This gate protects two architectural boundaries at once:

- capability/profile behavior remains owned by `Icod.TermInfo` rather than
  copied into Terminal;
- monotonic timing remains owned by `Icod.Timing` rather than duplicated.

It also detects accidental dependencies on `Icod.DCurses`, `Icod.ProcPs`,
`Icod.CommandFramework`, or future `Icod.Pty` work before publication.

---

## 5. Symbol and Source Link gate

The `.snupkg` must contain exactly:

```text
lib/net8.0/Icod.Terminal.pdb
lib/net10.0/Icod.Terminal.pdb
```

for portable debug symbols.

Each PDB must:

- use the portable PDB `BSJB` signature;
- contain the expected GitHub Source Link mapping for
  `uniblab/Icod.Terminal`;
- contain the same repository commit recorded in the primary package nuspec.

The symbol package must not contain managed assemblies.

This couples published symbols to the precise source revision from which the
package was produced.

---

## 6. Fresh package consumer

T12C adds:

```text
tools/package-smoke/
    Icod.Terminal.PackageSmoke.csproj
    Program.cs
    README.md
```

The smoke project is intentionally not part of `Icod.Terminal.sln` and contains
no `ProjectReference`.

The validation wrappers copy only its project and source files into a temporary
directory outside the repository. They then set a temporary `NUGET_PACKAGES`
location and perform a no-cache restore using:

1. the local packed-artifact directory;
2. NuGet.org for the package's stable `Icod.TermInfo` and `Icod.Timing`
   dependencies.

The exact current `Icod.Terminal` package version is passed through the
`IcodTerminalPackageVersion` MSBuild property.

Because the development/final artifact is validated before publication, the
requested Terminal version is supplied by the local package directory rather
than by an already-published copy.

---

## 7. Fresh-consumer behavior exercised

The smoke consumer runs once as `net8.0` and once as `net10.0`.

It creates a `TerminalSession` using only public contracts and injected test
services. No process-standard terminal state is observed or modified.

The consumer proves that a package-only application can:

- resolve the transitive `Icod.TermInfo` terminal profile contract;
- construct and open a session over injected terminal-control and byte services;
- apply cbreak/noecho policy through the package;
- query live dimensions through `Icod.TermInfo.TerminalSize`;
- decode ordinary UTF-8 input through `TerminalSession.ReadEventAsync`;
- emit application text;
- emit a TermInfo capability through the selected terminal description;
- serialize and restore a terminal-mode snapshot;
- dispose deterministically and restore the captured baseline exactly once.

The injected provider models a Windows console snapshot only as a portable
in-memory contract. It does not invoke Win32 APIs and therefore executes the same
way on Windows, Linux, and macOS.

---

## 8. CI integration

Both pull-request Staging validation and main-branch Release validation now pack
and validate on every supported CI host:

```text
windows-latest
ubuntu-latest
macos-latest
```

Windows invokes `verify-release-package.cmd`; Linux and macOS invoke
`verify-release-package.sh`.

Each host therefore performs:

```text
clean
restore
build
unit tests
pack
structural package verification
fresh net8.0 consumer
fresh net10.0 consumer
```

Only the validated Windows-produced package is uploaded as the canonical CI
artifact used by the existing deployment job. The other two package instances
exist solely to prove that pack and package-only consumption are host-clean.

T12D may retain this matrix while changing the package version from the final
prerelease to `0.1.0` and executing the publication gate.

---

## 9. Local validation

A local Staging gate can be run with:

```text
dotnet clean Icod.Terminal.sln -c Staging
dotnet restore Icod.Terminal.sln
dotnet build Icod.Terminal.sln -c Staging --no-restore -p:ContinuousIntegrationBuild=true
dotnet test Icod.Terminal.sln -c Staging --no-build
dotnet pack Icod.Terminal.csproj -c Staging --no-build --output artifacts
```

Then run the host wrapper:

```text
.github\scripts\verify-release-package.cmd artifacts Staging
```

or:

```text
bash .github/scripts/verify-release-package.sh artifacts Staging
```

The same sequence may use `Release` for the final release candidate.

---

## 10. T12C gate

T12C is complete only after the validation sequence succeeds for the branch
artifact, including both fresh target-framework consumers.

A successful gate establishes that:

- the NuGet package contains only the intended managed payload;
- package metadata and dependency closure match the reviewed architecture;
- portable symbols and Source Link identify the exact source commit;
- a clean consumer can restore, compile, and execute against the packed artifact
  on `net8.0` and `net10.0` without repository project references;
- Windows, Linux, and macOS CI can all pack and consume the same package contract.

After that result is recorded, T12D may set `<Version>` and `<PackageVersion>` to
exactly `0.1.0`, run the final Release matrix, and publish the non-prerelease
package.
