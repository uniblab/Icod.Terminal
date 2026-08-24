# Package Validation Scripts

`verify-release-package.sh` and `verify-release-package.cmd` are equivalent host
wrappers for the T01 package-validation contract.

Both wrappers require:

```text
<artifact-directory> <Staging|Release>
```

For pull-request/development validation:

```text
.github\scripts\verify-release-package.cmd artifacts Staging
bash .github/scripts/verify-release-package.sh artifacts Staging
```

For final main-branch release validation:

```text
.github\scripts\verify-release-package.cmd artifacts Release
bash .github/scripts/verify-release-package.sh artifacts Release
```

During T01 the scripts intentionally perform only foundation checks:

- read `PackageVersion` from `Icod.Terminal.csproj`;
- require the matching `.nupkg` and `.snupkg`;
- run the already-built repository sample non-interactively.

Public-API snapshots, isolated package-consumer tests, and deeper package-content
validation belong to the later release-gate tranches in the development roadmap.
