# Unix process standard-input regression

This executable runs the real public `TerminalSession.OpenAsync(options)` overload
inside a pseudo-terminal using the host's `script` command. It runs on Linux and
macOS; Windows reports that this Unix-specific check is not applicable.

From the repository root:

```sh
dotnet run --project tools/unix-input-smoke/Icod.Terminal.UnixInputSmoke.csproj --framework net10.0
```

The parent sends a key without a newline, a fragmented UTF-8 scalar, and a primary
device-attributes response without a newline. Exact output comparisons reject
unexpected keyboard or response echo. The child also cancels an idle event wait,
disposes while a transport read is pending, verifies restoration of terminal
flags and control characters, and opens a new session to check that an old reader
does not steal its key. Both CBreak and Raw modes run under an external deadline.
No physical terminal graphics are claimed by this test.

On macOS, the restoration comparison excludes only `PENDIN` (`0x20000000`).
Darwin's [terminal mode setter](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/kern/tty.c)
sets this transient pending-input state when restoring `ICANON`; it is not a
configuration flag. All other flags and every control character must match.

`packaging/VerifyRuntime.ps1` runs this regression on .NET 8, 9, and 10 on Unix
runners. The tool has a source project reference and is not a shipped package.
