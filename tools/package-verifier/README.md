# Package Verifier

`Icod.Terminal.PackageVerifier` performs repository-side structural validation of an already packed `Icod.Terminal` `.nupkg` and `.snupkg`.

It verifies:

- `<Version>` and `<PackageVersion>` remain present and identical;
- `<AssemblyVersion>` is present and valid;
- the `net8.0`, `net9.0`, and `net10.0` library and XML-documentation payloads are present;
- all packaged assemblies match the project-declared assembly version and remain unsigned;
- package metadata identifies the expected id, title, author, project, readme, icon, LGPL license expression, repository, and source commit;
- the package contains the non-empty README image referenced by `README.md`;
- each target-framework dependency group contains exactly `Icod.TermInfo 1.10.0` and `Icod.Timing 1.0.0`;
- dependency assemblies are not accidentally bundled into the primary package;
- no native/runtime payload or repository-only tests, samples, tools, or docs are present in the primary package;
- the symbol package contains exactly one portable PDB per supported framework; and
- all PDBs contain GitHub Source Link data for the commit recorded by the package.

Run the verifier directly after packing with:

```text
dotnet run --project tools/package-verifier/Icod.Terminal.PackageVerifier.csproj -f net10.0 -- artifacts
```

Normal local and CI validation invokes it through the normalized repository entry point:

```text
./packaging/VerifyPackageArtifact.ps1 -ArtifactDirectory artifacts -Configuration Debug
```

That packaging script also performs the isolated package-reference-only consumer smoke for `net8.0`, `net9.0`, and `net10.0`.
