# T137 — Color Composition and Icod.DCurses Observation Acceptance

**Release:** `0.13.0`  
**Tranche:** `T137`  
**Development version:** `0.13.0-alpha.8`  
**Status:** Implemented; exact-head validation pending

---

## 1. Purpose

T137 proves that the 0.13 palette/dynamic-color surface composes with the existing `TerminalSession` output/query architecture and that downstream `Icod.DCurses` can consume typed color observations without parsing raw OSC or opening another input path.

No new public color API is introduced by this tranche.

---

## 2. Session composition coverage

`TerminalColorCompositionTests` proves:

- OSC 4 palette mutation serializes in-order with OSC 133 and ordinary application text;
- dynamic-color mutation/reset serializes through the same session output domain;
- palette reset and dynamic reset remain ordinary unscoped control output;
- active OSC 4 observation can coexist with independently serialized control output while awaiting the correlated reply;
- palette and dynamic-color observations remain exactly correlated across sequential active-query transactions;
- unrelated OSC responses do not satisfy a color observation;
- the existing query flush contract remains intact.

The tests intentionally use public color APIs rather than bypassing through raw protocol helpers.

---

## 3. Downstream Icod.DCurses acceptance

A new standalone acceptance project lives at:

```text
tools/dcurses-color-observation-acceptance
```

It references:

- the current `Icod.Terminal` project;
- `Icod.DCurses 0.1.0` as a real NuGet dependency.

The acceptance path is:

1. query indexed palette entry 2 through `QueryPaletteColorAsync(...)`;
2. receive an OSC 4 reply and obtain typed 16-bit `TerminalColor`;
3. query `DefaultBackground` through `QueryDynamicColorAsync(...)`;
4. receive an OSC 11 reply and obtain typed 16-bit `TerminalColor`;
5. explicitly adapt those 16-bit values to the current downstream `CursesColor.Rgb(byte, byte, byte)` model;
6. construct a `CursesStyle` using the observed colors;
7. render text through a real `CursesSession` refresh;
8. assert that DCurses emitted its `setrgbf` / `setrgbb` terminal capabilities using the adapted observed values.

The downstream layer never parses OSC 4/11 text. It consumes only typed `TerminalColor` values supplied by `Icod.Terminal`.

---

## 4. Precision boundary

`Icod.Terminal` preserves 16-bit channels as required by T130.

`Icod.DCurses 0.1.0` currently represents direct RGB colors with byte channels, so T137's acceptance adapter intentionally uses the most-significant byte of each observed `ushort` channel.

That conversion policy belongs to the downstream consumer for now. 0.13 does not silently reduce `TerminalColor` precision or add a lossy conversion to the terminal abstraction solely for DCurses convenience.

A future DCurses release may choose a richer 16-bit-aware internal color policy while retaining the same observation API.

---

## 5. CI and distribution gates

`packaging/VerifyDCursesColorObservation.ps1` restores and runs the acceptance executable for:

```text
net8.0
net9.0
net10.0
```

The verifier is wired into:

- pull-request validation on Windows, Linux, and macOS;
- `VerifyDistribution.ps1`;
- tagged release validation.

It joins the retained downstream gates for synchronized output, progress, pointer shape, and semantic prompts.

---

## 6. T137 decision

The 0.13 observation surface is sufficiently typed and composable for real downstream consumption.

Future `Icod.DCurses` color policy can build on:

- byte-indexed palette observation;
- seven semantic dynamic-color observations;
- normalized 16-bit RGB values;
- explicit query failures rather than fabricated defaults;
- one shared `TerminalSession` input/query path.

T138 may now close the public API baseline, README/sample/package contract, and stable `0.13.0` release gates.
