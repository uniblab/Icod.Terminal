# Semantic screen output sample

This sample plans a text frame through `TerminalSession.Screen`, commits it through one session-owned output transaction, and uses an alternate-screen presentation lease for the interactive demonstration. Its application code uses Terminal types only.

## Prerequisites and commands

Run from the repository root with the .NET 10 SDK used by this repository. The samples target .NET 8, 9, and 10; selecting `-f net8.0` or `-f net9.0` also requires that runtime. Normal execution requires interactive standard input/output and a terminal profile with alternate-screen entry/exit, safe cursor positioning, and a usable rendition baseline. Bold and erase-to-end-of-line are optional.

```sh
dotnet run --project samples/Icod.Terminal.ScreenOutput.Sample -f net10.0 -- --help
dotnet run --project samples/Icod.Terminal.ScreenOutput.Sample -f net10.0
dotnet run --project samples/Icod.Terminal.ScreenOutput.Sample -f net10.0 -- --recovery
```

`--help` does not open a terminal session and works with redirected output. Other unrecognized arguments return usage error 2. Interactive execution should use a terminal, not an IDE output pane or redirected pipeline.

## What you should see

The normal mode enters an alternate screen, writes **Terminal-owned screen output** at row 0, column 0 (bold when safely supported), and displays an exit instruction. Press **r** or **R** to refresh **Input-driven frame** with a temporary hidden cursor, restoring the currently effective presentation owner afterward. If the terminal does not advertise both hide and return capabilities, the refresh uses the ordinary frame path. Press **q**, **Q**, or **Escape** to finish. End of input also ends the demonstration. Other events are ignored, including unfamiliar future event kinds.

The recovery mode deliberately creates pending work, emits an intervening coordinated write, and attempts the now-stale transaction. Its old payload must never appear. After rejection, a newly planned frame displays **Fresh frame after stale rejection.** The same exit keys apply.

Presentation ownership is released on normal exit, cancellation, or failure. Session disposal restores owned input/output modes. This is scoped terminal-state cleanup, not restoration of arbitrary previous text rendition or a guarantee that a broken transport can deliver cleanup. The frame resets its own rendition to Terminal's normalized default. Diagnostic errors are reported after the session's disposal attempt.

## Read the implementation

- [Program.cs](Program.cs) handles arguments, owns the session, and reports exit status.
- [ScreenOutputExample.cs](ScreenOutputExample.cs) contains the reusable frame routine, deliberate stale-work demonstration, and interactive presentation/event loop. The package verifier executes these same methods.

`DrawFrameAsync(...)` plans baseline, cursor, normalized bold entry, and rendition reset before constructing the transaction. Missing mandatory plans return `false` without frame output. A valid zero-byte plan remains usable. Erase-to-end-of-line is added only when available.

`DrawFrameWithHiddenCursorAsync(...)` uses the same plans, then calls `SetCursorVisibilityForCommit(Hidden)` on the one-shot frame builder. This setting emits no bytes until commitment, and Terminal restores the latest active presentation lease request or the ordinary cursor capability after all frame items. The method returns `false` before constructing a frame when advertised entry/return capabilities are absent. Concurrent state changes, a stale output epoch, and output failure can still reject the commit or make physical state uncertain; the sample reports those failures instead of guessing a repaint. For an editor that wants the cursor hidden for the entire editing session, acquire a cursor-visibility presentation lease.

This fixed-origin example does not need dimensions. It leaves text width, clipping, wrapping, and resize-driven layout to the caller; it does not promise the frame fits every terminal. A renderer that needs dimensions must inspect `GetDimensions().IsAvailable` before using its value. For retained cells, windows, layout, and damage tracking, use Icod.DCurses.

`RunInteractiveAsync(...)` requires an alternate-screen lease so it can refuse unsupported presentation instead of drawing over the shell. A missing frame plan can still require releasing an already acquired presentation lease; the no-output guarantee belongs specifically to `DrawFrameAsync(...)`.

## Failure and cancellation

| Situation | Behavior |
| --- | --- |
| Missing required presentation or frame operation | Exit 1 with a diagnostic; release any acquired presentation scope. |
| Deliberately stale transaction in `--recovery` | Reject before emitting its payload; discard the consumed builder; construct one fresh frame. |
| Press r with cursor-hide/return support | Commit one temporarily hidden frame, then restore the effective presentation owner; do not change the lease order. |
| Press r without cursor-hide/return support | Draw the same input-driven content using the ordinary frame path. |
| Cancellation before frame commitment | No frame emission; cancellation propagates. The host returns 130 for an observed `OperationCanceledException`. |
| Cancellation after commitment starts | The committed output and required cleanup are not intentionally truncated; later cancellable work can stop. |
| Transport or cleanup failure | Output may be partial. Surface the error and exit 1; do not replay or retry automatically. |
| Normal exit key or EOF | Release presentation/session ownership and exit 0. |

The recovery method catches `InvalidOperationException` only around the deliberately invalidated commit in this controlled scenario. Do not copy that catch as a general stale-error classifier: ownership, lifetime, and other invalid operations may use the same exception type. The fresh frame is a caller decision, not an automatic retry policy. Cancellation tokens can be supplied by embedding hosts; q/Escape are the standalone demo's portable exit controls.

## Noninteractive verification

The runtime verifier compiles all three frameworks and runs `--help`. To execute the frame, recovery, presentation, and input paths without a physical terminal, use the existing package harness:

```sh
pwsh -NoProfile -File packaging/BuildPackageArtifact.ps1 -ArtifactDirectory artifacts/screen-candidate -Configuration Staging
pwsh -NoProfile -File packaging/VerifyDCursesPackage.ps1 -ArtifactDirectory artifacts/screen-candidate -Configuration Staging
```

The harness copies the actual sample source into an isolated package consumer. It checks exact frame/recovery bytes, unavailable dimensions, missing mandatory plans, q/Escape/EOF, alternate-screen release, cancellation, and committed transport failure. Synthetic capability descriptions belong to that test host. These checks do not certify a physical emulator's rendering behavior.

See the [screen-output guide](../../docs/Screen-Output.md) for the full planning, epoch, commitment, and recovery contract.
