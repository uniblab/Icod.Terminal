# Clipboard sample

Run from the repository root:

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample -c Release -f net10.0 -- --help
```

No arguments or `--help` show help without opening a terminal or accessing its
clipboard. Choose `--write "text"`, `--generated-bytes N`, or `--read` explicitly.
Positional text remains an explicit write. Combining a write with `--read` requests
a round-trip comparison. Writes replace the clipboard; no automatic prior-content
read or restoration occurs. Reports contain counts and outcomes, never payloads.

An explicit larger fixture:

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample -c Release -f net10.0 -- --generated-bytes 131072 --maximum-write-bytes 131072 --read --maximum-read-bytes 131072
```

Each limit defaults to 65,536 decoded bytes and accepts 1..16,777,216 inclusive.
The limits are independent and authorize local resources only. A successful write
reports emission; a matching explicit reply establishes only that round trip.
A timeout leaves support and policy unknown. Exit codes: 0 success/help, 1 failed
operation or mismatch, 2 invalid local command/limit/text, 130 caller cancellation.

See [the library guide](../../docs/Clipboard-Transfers.md) for allocation,
UTF-8 accounting, recovery and application fallback guidance.
