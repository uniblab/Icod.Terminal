# Fresh Package Smoke Consumer

This project is intentionally **not** part of `Icod.Terminal.sln` and has no
project reference to the repository library. It proves that a fresh application
can restore, compile, and execute using only the packed `Icod.Terminal` NuGet
artifact plus its declared NuGet dependencies.

Release validation copies the project into a temporary directory, uses an
isolated NuGet package cache, restores the current `Icod.Terminal` version from
the local `artifacts` directory, and resolves `Icod.TermInfo 1.0.0` and
`Icod.Timing 1.0.0` through NuGet.

The same source runs once for `net8.0` and once for `net10.0`. It uses an injected
terminal-control provider and in-memory byte transports, so the smoke test never
requires or mutates the CI runner's real terminal.

The smoke path exercises session open/apply/restore, terminal-size observation,
UTF-8 input decoding, application and capability output, TermInfo profile use,
and terminal-mode serialization without relying on repository build outputs.
