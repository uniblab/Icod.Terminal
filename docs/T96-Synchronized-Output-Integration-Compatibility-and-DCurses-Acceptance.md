# T96 — Synchronized Output Integration, Compatibility, and DCurses Acceptance

**Release:** `0.9.0`  
**Tranche:** `T96`  
**Development version:** `0.9.0-alpha.7`  
**Status:** Implemented; exact-head validation pending  
**Theme:** concurrent ownership, cross-platform regression acceptance, compatibility posture, and downstream `Icod.DCurses` refresh integration

## 1. Objective

T96 proves that the synchronized-output API is not merely correct in isolated ownership tests. It must remain stable under concurrent callers, preserve existing `Icod.Terminal` behavior across supported target frameworks and operating systems, and fit the real `Icod.DCurses` refresh boundary without teaching DCurses private escape sequences.

## 2. Concurrent ownership acceptance

The normal `Icod.Terminal.Tests` suite now includes concurrent public-lease stress.

Acceptance cases include:

- 64 simultaneous acquisition requests;
- one and only one physical `CSI ? 2026 h` transition;
- arbitrary concurrent disposal order;
- one and only one final `CSI ? 2026 l` transition;
- exactly one final synchronized-output flush;
- repeated rounds of concurrent acquisition/release proving that no physical ownership leaks between transactions.

The tests exercise the public `TerminalSession.AcquireSynchronizedOutputAsync(...)` and `TerminalSynchronizedOutputLease.DisposeAsync()` surface rather than internal manager methods.

## 3. Compatibility posture

Synchronized output remains optimistic semantic emission.

For terminals which do not implement DEC private mode 2026:

- the begin/end frames may simply be ignored;
- ordinary contained terminal output remains ordinary output;
- the library does not claim synchronized rendering occurred;
- no fallback buffering or alternate screen algorithm is introduced;
- no support state is inferred from `TERM`, OS, terminal name, or emulator lineage.

Terminals which implement their own synchronized-output safety timeout may end rendering deferral before the logical lease ends. `Icod.Terminal` still emits the matching final mode-off frame when logical ownership ends.

## 4. Cross-platform acceptance

The PR workflow continues to build and test the complete solution on:

```text
Windows
Linux
macOS
```

for the supported target-framework set:

```text
net8.0
net9.0
net10.0
```

T96 adds the DCurses acceptance gate to every PR OS job as well, so the downstream integration is not Linux-only evidence.

## 5. Real DCurses refresh boundary

The current stable `Icod.DCurses 0.1.0` implementation already has the correct architectural seam:

```csharp
await curses.RefreshAsync();
```

`CursesSession.RefreshAsync()` owns one terminal-activity scope and sends its retained refresh/diff output through the canonical `TerminalSession` integration layer. It does not need to know DEC private mode 2026.

The intended composition is therefore:

```csharp
TerminalSynchronizedOutputLease synchronized =
	await terminalSession.AcquireSynchronizedOutputAsync();

await curses.RefreshAsync();

await synchronized.DisposeAsync();
```

This leaves cell buffering, damage calculation, refresh diffing, rendition selection, and cursor placement entirely in `Icod.DCurses`.

## 6. Downstream executable acceptance

T96 adds:

```text
tools/dcurses-synchronized-output-acceptance/
```

The executable references:

- the current `Icod.Terminal` project under development;
- the published stable `Icod.DCurses 0.1.0` package.

It opens a scripted interactive `TerminalSession`, transfers that session into a real `CursesSession`, modifies the standard screen, acquires synchronized output through the retained canonical Terminal session, calls the real `CursesSession.RefreshAsync()`, and releases synchronized output.

The acceptance requires:

1. canonical `ESC [ ? 2026 h` is emitted;
2. DCurses emits actual refresh payload between the begin and end frames;
3. canonical `ESC [ ? 2026 l` follows the refresh payload;
4. the synchronized-output final flush occurs.

No private DCurses source or private Terminal API is used by the executable.

## 7. Acceptance automation

`packaging/VerifyDCursesSynchronizedOutput.ps1` restores the acceptance project without cache and runs it under:

```text
net8.0
net9.0
net10.0
```

The gate is integrated into:

- pull-request validation on Windows, Linux, and macOS;
- `VerifyDistribution.ps1` for release/distribution validation.

This deliberately tests dependency resolution between the current Terminal project and the published stable DCurses package before 0.9 is released.

## 8. No premature DCurses dependency change

T96 does not modify the `Icod.DCurses` repository or bump its stable package dependency while `Icod.Terminal 0.9.0` is still an unpublished alpha.

After 0.9 is published, DCurses can adopt the new public API in its own versioned development tranche. The required integration point has already been proven here.

## 9. T96 decision

If the exact-head PR validation is green, synchronized output has satisfied the final implementation/integration gate:

- deterministic concurrent ownership;
- no leaked physical mode across repeated transactions;
- Windows/Linux/macOS regression acceptance;
- net8/net9/net10 acceptance;
- real stable-DCurses refresh composition;
- no new private escape ownership in DCurses;
- no fabricated support inference.

T97 may then focus on public API regret audit, documentation, focused sample, package-only consumption, stable metadata, and release closure.
