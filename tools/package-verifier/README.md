# Package Verifier

`Icod.Terminal.PackageVerifier` performs repository-side structural validation of
an already packed `Icod.Terminal` `.nupkg` and `.snupkg`.

It verifies:

- `<Version>` and `<PackageVersion>` remain present and identical;
- the `net8.0` and `net10.0` library and XML-documentation payloads are present;
- both packaged assemblies retain assembly version `0.1.0.0` and remain unsigned;
- package metadata identifies the expected id, title, author, project, readme,
  icon, LGPL license expression, repository, and source commit;
- each target-framework dependency group contains exactly `Icod.TermInfo 1.0.0`
  and `Icod.Timing 1.0.0`;
- dependency assemblies are not accidentally bundled into the primary package;
- no native/runtime payload or repository-only tests, samples, tools, or docs are
  present in the primary package;
- the symbol package contains exactly one portable PDB per supported framework;
- both PDBs contain GitHub Source Link data for the commit recorded by the package.

Run it after packing:

```text
dotnet run --project tools/package-verifier/Icod.Terminal.PackageVerifier.csproj -- artifacts
```

Normal T12C/release validation invokes the tool through either
`.github/scripts/verify-release-package.sh` or
`.github/scripts/verify-release-package.cmd`.
