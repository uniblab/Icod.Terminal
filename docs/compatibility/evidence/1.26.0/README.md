# Icod.Terminal 1.26.0 Accepted Evidence

This directory holds maintainer-reviewed JSON reports produced by `Icod.Terminal.Compatibility.Sample` for the 1.26 compatibility matrix.

No live report has been accepted yet. The generated matrix therefore keeps all ten primary terminal lanes and the VS Code companion lane explicitly `NotRun`.

Before accepting a report:

1. confirm the package version and complete source commit;
2. confirm the exact terminal, operating-system, and optional transport versions;
3. confirm the scenario identifier/revision and stated success condition;
4. distinguish automated observation from operator confirmation;
5. reject secrets, raw clipboard data, keystrokes, command lines, environment dumps, arbitrary reply bytes, or implicit user/host identity;
6. keep timeout, denied permission, policy suppression, and ambiguous behavior `Inconclusive`;
7. regenerate [`../../1.26.0.md`](../../1.26.0.md) and review the exact diff.

File names should be stable, descriptive, lowercase, and collision resistant, for example `kitty-0.32.2-ubuntu-24.04-wsl2-query-dimensions-v1.json`. Each file contains one evidence object. Do not edit generated observations to strengthen their outcome.

See the [sample walkthrough](../../../../samples/Icod.Terminal.Compatibility.Sample/README.md) and [evidence policy](../../README.md) for the complete contract.
