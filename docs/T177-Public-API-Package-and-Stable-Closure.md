# T177 — Public API, Package, and Stable Closure

**Release line:** `0.17.0`  
**Tranche version:** `0.17.0`  
**Status:** Stable candidate; exact-head validation pending

---

## 1. Purpose

T177 closes the 0.17 release without adding new feature surface. It freezes the public contract, updates user-facing guidance and samples, validates the freshly packed NuGet artifact as a consumer would, and carries the new release contract through PR, distribution, and tagged-release workflows.

---

## 2. Stable documentation

T177 delivers:

- `docs/Public-API-Baseline-0.17.md`;
- `docs/Modern-Keyboard-Security-and-Compatibility.md`;
- a 0.17-focused repository `README.md`;
- the expanded `samples/Icod.Terminal.RichInput.Sample`.

The API baseline freezes the semantic 0.17 additions and explicit exclusions. The security/compatibility note documents negotiation, bounded parsing, screen-local state, lifecycle recovery, traditional fallback, and associated-text exposure.

---

## 3. Sample contract

The rich-input sample now attempts a compound lease containing:

```text
bracketed paste
focus reporting
button-event mouse tracking
Kitty AllKeys keyboard reporting
```

If Kitty negotiation is unavailable, the sample retains traditional keyboard input and retries the pre-existing paste/focus/mouse feature set without modern keyboard ownership.

When modern reporting is available, key output includes:

- semantic key identity;
- key phase;
- character;
- shifted character;
- base-layout character;
- associated text;
- full modifier set.

---

## 4. Package-only 0.17 contract

`packaging/VerifyModernKeyboardPackage.ps1` validates the freshly packed `Icod.Terminal` package independently of project references.

For net8.0, net9.0, and net10.0 it requires generated XML documentation for the public modern-keyboard surface and runs `tools/package-modern-keyboard-smoke` against the package artifact.

The smoke verifies:

- reporting-mode enum values;
- key-phase enum values;
- modern modifier symbols;
- new `TerminalInputEvent` properties;
- keyboard-reporting properties on options and lease;
- `TerminalKey.Unrecognized`;
- absence of raw/vendor Kitty, `modifyOtherKeys`, or raw-keyboard control methods on `TerminalSession`.

Workflow #854 proved the new package verifier on `0.17.0-alpha.7` after every retained 0.8–0.16 package contract also passed.

---

## 5. Downstream and release workflow retention

The real `Icod.DCurses` modern-keyboard acceptance remains part of PR and distribution validation.

The tagged `release.yaml` now explicitly runs both:

- `VerifyDCursesModernKeyboard.ps1` before packing;
- `VerifyModernKeyboardPackage.ps1` against the selected release artifact.

This keeps the tagged publication path equivalent to the closure gates rather than stopping at the 0.16 contract.

---

## 6. Stable metadata

Repository version authority is now:

```text
VersionPrefix:    0.17.0
VersionSuffix:    <empty>
Version:          0.17.0
PackageVersion:   0.17.0
AssemblyVersion:  0.17.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

No tag is to be created merely because metadata is stable.

---

## 7. Remaining release gates

Before merge, the exact stable PR head must pass Staging validation on Windows, Linux, and macOS, including:

- full build/tests;
- all DCurses acceptance, including modern keyboard;
- exact stable package artifact verification;
- retained 0.8–0.16 package contracts;
- the 0.17 XML/package-only modern-keyboard contract.

After merge, the exact resulting `main` commit must pass the Release `main` workflow across its full platform matrix.

Only after that exact-main Release validation is green may `v0.17.0` be created. The tag-triggered release workflow will then rerun the downstream and package contracts before publication.

---

## 8. Current gate

The implementation and closure assets are complete. Exact-head stable validation is the remaining T177 gate.
