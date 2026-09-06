# T153 — Typed OSC 133 Command-Output Metadata

**Release:** `Icod.Terminal 0.15.0`  
**Development version:** `0.15.0-alpha.4`  
**Status:** Implemented; exact-head validation pending

## Purpose

T153 exposes the T150-frozen OSC 133 command-output metadata contract through a typed public API while retaining the portable 0.12 bare `C` marker unchanged.

## Public API

```csharp
public readonly struct TerminalSemanticCommandOutputOptions {
	public TerminalSemanticCommandOutputOptions(
		string? commandLine = null
	);

	public string? CommandLine { get; }
}

public ValueTask BeginCommandOutputAsync(
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken = default
);
```

The pre-existing overload remains unchanged:

```csharp
public ValueTask BeginCommandOutputAsync(
	CancellationToken cancellationToken = default
);
```

## Frozen semantic mapping

```text
CommandLine == null
    -> OSC 133;C ST

CommandLine == ""
    -> OSC 133;C;cmdline_url= ST

CommandLine != null && CommandLine.Length != 0
    -> OSC 133;C;cmdline_url=<strict UTF-8 percent-encoded bytes> ST
```

The null/empty distinction is intentional. `null` means no command-line metadata. Empty string means the caller explicitly knows the command line is empty.

`default(TerminalSemanticCommandOutputOptions)` is valid and maps to bare `C`.

## Encoding

T153 reuses the T151 encoder without adding another escaping layer:

- well-formed .NET strings are encoded as strict UTF-8;
- only RFC 3986 unreserved ASCII bytes remain literal;
- every other UTF-8 byte is encoded as uppercase `%HH`;
- malformed UTF-16 is rejected before any output is committed;
- the 65,536-byte OSC 133 payload ceiling is enforced on encoded wire bytes;
- oversize metadata is rejected, never truncated.

Shell `%q` `cmdline=` remains excluded from 0.15.

## Output/lifecycle behavior

The new overload uses the existing `TerminalSession` output serialization gate. It does not introduce a second writer, raw OSC path, support probe, background listener, or lifecycle ownership state.

Command-output metadata is ephemeral application output:

- no replay on resume;
- no reset on suspend;
- no synthetic marker on disposal;
- no session-open auto-emission;
- no implicit command-region state machine.

## Privacy and security

Command-line publication is explicit caller intent. `Icod.Terminal` does not:

- inspect shell history;
- inspect process command lines;
- capture commands automatically;
- parse or normalize shell syntax;
- infer whether text is sensitive;
- redact secrets.

Command lines may contain passwords, bearer tokens, API keys, private paths, host names, or other sensitive material. Percent encoding protects OSC framing only; it does not provide confidentiality.

## Tests

T153 adds public/session tests proving:

- default options emit byte-identical bare `C`;
- empty string emits explicit empty `cmdline_url`;
- Unicode and non-BMP text use strict UTF-8 byte percent encoding;
- caller text is preserved unchanged by the options value;
- malformed UTF-16 is rejected before output;
- an exact 65,536-byte payload is accepted;
- one byte beyond the limit is rejected before output;
- pre-cancelled publication emits nothing;
- committed writes remain one non-cancellable write with no implicit flush.

## Result

T153 completes the public semantic surface planned for extended OSC 133 `C` metadata without weakening the 0.12 portable contract or exposing generic caller-controlled protocol parameters.

**Next after exact-head validation:** T154 — session integration and compatibility.
