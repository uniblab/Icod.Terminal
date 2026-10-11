# Clipboard transfers

`TerminalSession` reads and writes explicit OSC 52 selections. Session startup,
capability inspection and disposal never access clipboard contents automatically.

Configure local resources when opening the session:

```csharp
using Icod.Terminal;

await using TerminalSession session = await TerminalSession.OpenAsync(new TerminalSessionOptions {
    ClipboardOptions = new TerminalClipboardOptions {
        MaximumReadBytes = 1_048_576,
        MaximumWriteBytes = 1_048_576
    }
});

// Run these only in response to the application's explicit copy/paste commands.
await session.WriteClipboardAsync(TerminalClipboardSelection.Clipboard, "Copied text");
byte[] bytes = await session.ReadClipboardAsync(
    TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds(5));
```

Read and write limits are independent, default to 65,536 bytes, and accept
1..16,777,216 bytes inclusive. Text counts strict UTF-8 bytes without a BOM,
not UTF-16 characters. An empty write explicitly clears the chosen selection.
No clipboard operation silently truncates. Binary oversize throws
`ArgumentOutOfRangeException`; oversized/invalid text throws `ArgumentException`.

Increasing a limit authorizes allocation; it does not allocate the whole budget
at session open. Output preflight counts bytes before allocating one complete
encoded frame. Once emission commits, caller cancellation cannot deliberately
split that frame; output transport failure can still leave remote state uncertain.
Write completion means emission through the output abstraction, not remote acceptance.

Larger reads retain only an exactly correlated reply in a separate accumulator,
grown as bytes arrive. Ordinary undecoded input keeps its 87,400-byte ceiling.
If customizing `InputDecoderOptions.MaximumBufferedBytes`, allow at least seven
bytes for the complete selection prefix. Existing ordinary-buffer behavior is
retained for read budgets at or below 64 KiB. Wrong-selection or unsolicited
frames retain existing ordinary handling and receive no larger allowance.

The caller deadline includes queueing. Cancellation/timeout after emission leaves
bounded late ownership so a delayed reply cannot satisfy the next query. Malformed
or oversized replies are drained through a terminator before `FormatException`.
Recovery discards at most one additional frame budget, using bounded scratch space.
If that allowance or late ownership expires with no terminator, input fails closed.
The application should dispose/reopen the session before resuming input after such
a failure. A caller deadline may produce `TimeoutException` first.

The frame budget is `4 * ceil(N / 3) + 16`, where N is the decoded read limit.
That is a framing allowance, not a total managed-memory limit: geometric growth,
the exact response copy and the returned byte array have additional costs. See
the [qualification record](Clipboard-Transfer-Qualification-1.29.md).

OSC 52 does not negotiate maximum capacity. Terminal policy, multiplexers, SSH
paths or transport constraints may impose smaller limits or suppress replies.
There is no automatic probe, readback, retry, splitting into replacement writes,
native clipboard bridge or automatic fallback. A timeout establishes no support
status. Editors should retain their own clipboard, offer a clear external-copy
failure message, and ask the user to explicitly retry a smaller selection if needed.

## Runnable sample

From the repository root, help is headless and has no clipboard side effects:

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample -c Release -f net10.0 -- --help
```

The following explicitly overwrites the clipboard with 131,072 generated ASCII
bytes and explicitly queries it for a byte-for-byte round-trip check:

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample -c Release -f net10.0 -- --generated-bytes 131072 --maximum-write-bytes 131072 --read --maximum-read-bytes 131072
```

Use `--read` alone for an explicit read, or `--write "text"` for an explicit write.
No arguments show help. The legacy positional-text invocation is an explicit
write only. Samples print counts and outcomes, never payloads.
