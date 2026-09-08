# Licensing

`Icod.Terminal` uses different GNU licenses for the reusable library and for repository executable/test programs.

## Library

The `Icod.Terminal` library assembly, its root `Icod.Terminal.csproj`, and the C# sources compiled into that library are licensed under **LGPL-3.0-or-later**.

This is also the license of the published `Icod.Terminal` NuGet package. The package therefore retains:

```text
PackageLicenseExpression = LGPL-3.0-or-later
```

The LGPL permits applications to use the library subject to the terms and conditions of the GNU Lesser General Public License version 3 or, at the recipient's option, any later version.

## Executable, test, sample, and validation programs

Repository programs that exercise, demonstrate, test, inspect, or validate the library are licensed under **GPL-3.0-or-later**. This includes:

- `Icod.Terminal.Tests`;
- the projects under `samples/`;
- package smoke-test programs under `tools/`;
- package/public-API verification utilities under `tools/`;
- downstream `Icod.DCurses` acceptance/soak programs under `tools/`.

These GPL programs are repository development, demonstration, and release-validation artifacts. Their GPL license does **not** change the LGPL license of the `Icod.Terminal` library package they consume.

## Source and project headers

Every tracked `.cs` file carries a project-appropriate license header containing:

1. the owning assembly name;
2. a one-line program description;
3. `Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>`;
4. the LGPL boilerplate for library sources or GPL boilerplate for executable/test/sample/tool sources.

Every tracked `.csproj` begins with:

```xml
<?xml version="1.0" encoding="utf-8"?>
```

followed immediately by the corresponding XML-comment license header before the `<Project ...>` element.

`packaging/VerifyLicenseHeaders.ps1` derives the owning assembly for every C# source and verifies the complete expected header template. The check runs in the package-candidate build used by PR, `main`, distribution, and tag publication validation, and in local `all` / `validate` builds.

## License text

The repository root [`LICENSE`](../LICENSE) contains the GNU Lesser General Public License version 3 terms and the incorporated GNU General Public License version 3 terms. It therefore provides the license text referenced by both the LGPL library headers and GPL executable/test/sample/tool headers.

## Compatibility of repository licensing with package consumption

Consumers installing `Icod.Terminal` from NuGet receive the LGPL-licensed library package. The repository's GPL test/sample/tool programs are separate programs used to validate or demonstrate that library and are not a change to the public package license.

For the exact stable package and source state, use the documentation pinned to the applicable release tag rather than a moving `main` branch.
