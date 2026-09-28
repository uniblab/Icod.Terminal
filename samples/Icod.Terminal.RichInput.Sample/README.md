# Rich input sample

This interactive inspector reads `TerminalSession.ReadEventAsync()` and prints text, keys, paste, focus, mouse, lifecycle, and semantic events. It also shows how a small editor can consume reported Left-key press/release and Control+Insert repeat events without inventing releases for traditional input. The application source uses `Icod.Terminal` only.

## Run it

From the repository root, use the .NET 10 SDK and an interactive terminal with standard input and output attached:

```sh
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

The sample also targets `net8.0` and `net9.0` when the matching runtime is installed. Type text, use navigation/editing keys, paste, click, or change focus. Press **q**, **Q**, or **Escape** to exit; end of input also ends the loop. Terminal support determines which reports actually arrive, so a physical terminal need not emit every event shown in the scripted package witness.

## Reporting and fallback

The sample first requests an input-protocol lease for Kitty `AllKeys` reporting, bracketed paste, focus, and button mouse tracking. If that combined request is unavailable, it tries paste/focus/mouse without modern keyboard reporting. If that is also unavailable, the basic event loop still runs. The lease is disposed on exit; the session remains the sole reader throughout.

With modern reporting acquired, Kitty's phase-bearing functional CSI forms can produce semantic keys such as Left release (`CSI 1;1:3 D`) and Control+Insert repeat (`CSI 2;5:2 ~`). The sample prints the key, phase, modifiers, reported characters, and associated text. For example, a Left press followed by a Left release can produce `Key key=Left phase=Press ...` then `Key key=Left phase=Release ...`; each updates the `Editor Left held=...` line. A Control+Insert repeat increments `Control+Insert repeats`. The associated text and alternate characters appear only when the terminal reports them.

`EditorKeyState` is enabled only while a modern keyboard-reporting lease is active. Traditional terminfo keys remain available as fallback, but a traditional key press does not promise a later release. The sample therefore does not treat such a key as indefinitely held. Supported CSI-u keys, ordinary text, paste, focus, mouse, and active-query routing continue through the same event stream. The phase-bearing form excludes ambiguous cursor-position `R` and unknown key identities; it is not a generic vendor-event channel.

The program prints paste content and associated text to the terminal. They can contain private user input; do not use this diagnostic sample for secrets or copy its output into persistent logs without considering disclosure. Treat unsolicited terminal reports as external input.

## Read and verify

- [Program.cs](Program.cs) acquires reporting, consumes the authoritative event stream, and formats each event.
- [EditorKeyState.cs](EditorKeyState.cs) holds the two editor-specific examples; it is also compiled by the fresh-package input witness with no direct TermInfo dependency in the controlled editor source.
- [Input and events](../../docs/Input-and-Events.md) explains the event contract; [modern keyboard compatibility](../../docs/Modern-Keyboard-Security-and-Compatibility.md) covers reporting prerequisites and limits.

The package verifier feeds scripted phase-bearing frames to the same editor-state source through a freshly built `Icod.Terminal` package. This checks decoding and consumer behavior; it cannot certify that a particular physical terminal sends those frames. The fixture provider in the test host uses TermInfo to construct a synthetic profile; the sample application itself does not use TermInfo directly.
