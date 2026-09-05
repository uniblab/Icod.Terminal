# Icod.Terminal 0.7 Public API Baseline

**Release:** `0.7.0`  
**Theme:** semantic OSC 52 clipboard and selection operations  
**Status:** Frozen for stable release

## Public delta

The reviewed 0.7 public API adds exactly one public enum and three method signatures to `TerminalSession`.

```csharp
public enum TerminalClipboardSelection {
	Clipboard,
	Primary,
	Secondary,
	Select
}

public ValueTask WriteClipboardAsync(
	TerminalClipboardSelection selection,
	ReadOnlyMemory<byte> payload,
	CancellationToken cancellationToken = default
);

public ValueTask WriteClipboardAsync(
	TerminalClipboardSelection selection,
	string value,
	CancellationToken cancellationToken = default
);

public ValueTask<byte[]> ReadClipboardAsync(
	TerminalClipboardSelection selection,
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

## Contract

`WriteClipboardAsync(...)` is explicit semantic terminal output. The binary overload treats the payload as exact bytes. The string overload uses strict UTF-8 without a byte-order mark and is independent of `TerminalSession.ApplicationEncoding`. Both are bounded to 65,536 decoded bytes. Empty content explicitly replaces the selected target with empty content.

`ReadClipboardAsync(...)` is an explicit privacy-sensitive query. Every call names one selection and one caller-visible timeout. It returns exact decoded bytes and performs no implicit text decoding. Opening, probing, suspending, resuming, or disposing a session never initiates a clipboard read.

Successful clipboard writes prove emission only, not terminal-side acceptance. Query timeout does not prove lack of OSC 52 support because terminal policy may disable or ignore clipboard reads.

## Deliberate omissions

The 0.7 public API does not expose:

- raw OSC 52 selector letters;
- cut-buffer targets 0 through 7;
- multiple-selection selector lists;
- caller-supplied base64;
- generic OSC framing or `SendOsc(...)` helpers;
- automatic support detection or cached support flags;
- implicit clipboard reads;
- background clipboard monitoring or synchronization;
- OS-native clipboard APIs;
- automatic conversion of read bytes to strings;
- retry policy hidden from callers.

These omissions are intentional. They keep the API semantic, bounded, explicit, and compatible with the existing single-reader query and session-owned output architecture.

## Resource and framing limits

```text
Maximum decoded OSC 52 payload:       65,536 bytes
Maximum encoded OSC 52 payload:       87,384 bytes
Maximum complete OSC 52 frame:        87,400 bytes
Maximum undecoded terminal buffer:    87,400 bytes
Default bracketed-paste chunk:         4,096 bytes
```

The shared undecoded-input ceiling matches the OSC 52 complete-frame ceiling. A structurally correlated OSC 52 response which reaches that ceiling without completing is owned by the active query, fails deterministically, and is drained through its control-string terminator rather than being reinterpreted as ordinary application input. Bracketed-paste chunking remains independently fixed at its historical default.

## Compatibility posture

Outbound OSC 52 is canonical seven-bit OSC terminated by seven-bit ST. Inbound query responses additionally accept BEL termination and the C1 OSC/C1 ST form for compatibility.

Terminal identity, `TERM`, or a recognized emulator name is not treated as proof that clipboard writes or reads are enabled. Terminal-side security policy remains authoritative.

## Regret audit

The 0.7 surface passes the stable-release regret audit because:

1. selector values are semantic rather than protocol strings;
2. bytes remain the authoritative data representation;
3. text convenience is deterministic strict UTF-8;
4. reads require explicit per-call opt-in and a timeout;
5. support uncertainty is not represented as false certainty;
6. no generic extension mechanism is frozen prematurely;
7. the API composes with existing session output/query ownership instead of introducing new reader or writer abstractions.

No further public surface is required for `0.7.0`.
