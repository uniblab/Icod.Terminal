# Package Validation Scripts

`verify-release-package.sh` and `verify-release-package.cmd` are equivalent host
wrappers for the T12C package-validation gate.

Both wrappers require an already packed artifact directory and a build
configuration:

```text
<artifact-directory> <Staging|Release>
```

For pull-request/development validation:

```text
.github\scripts\verify-release-package.cmd artifacts Staging
bash .github/scripts/verify-release-package.sh artifacts Staging
```

For final release validation:

```text
.github\scripts\verify-release-package.cmd artifacts Release
bash .github/scripts/verify-release-package.sh artifacts Release
```

T12C expands the original T01 foundation check into a release-grade package gate.
The wrappers now:

- read `PackageVersion` from `Icod.Terminal.csproj` and require the matching
  `.nupkg` and `.snupkg`;
- run `tools/package-verifier` to inspect package structure, metadata, exact
  runtime dependency closure, assembly identity, XML documentation, portable
  symbols, and GitHub Source Link data;
- copy `tools/package-smoke` outside the repository into a temporary directory;
- use an isolated NuGet package cache so repository outputs cannot satisfy the
  consumer by accident;
- restore the current `Icod.Terminal` package from the local artifact directory
  while resolving its stable `Icod.TermInfo` and `Icod.Timing` dependencies from
  NuGet;
- compile and execute that package-reference-only consumer for `net8.0`,
  `net9.0`, and `net10.0`.

The smoke program uses injected terminal services and in-memory byte transports.
It does not require or mutate the host terminal, so the same validation can run
on Windows, Linux, and macOS CI runners.

## Workflow policy

A push to `main` runs the complete Release validation matrix but does not publish
packages. Publication is deliberately tag-driven through
`.github/workflows/release.yaml`.

A release tag must use `v<semver>`, point to a commit contained in `main`, and
match both `<Version>` and `<PackageVersion>` in `Icod.Terminal.csproj` exactly.
The tag workflow reruns the Windows/Linux/macOS Release gate before publication,
then publishes the validated package to NuGet.org and GitHub Packages and creates
a GitHub Release containing the `.nupkg`, `.snupkg`, and SHA-256 checksums.
