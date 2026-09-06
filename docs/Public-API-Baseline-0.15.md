# Icod.Terminal 0.15 Public API Baseline

**Release:** `0.15.0`  
**Theme:** typed OSC 133 extended semantic metadata while preserving the portable 0.12 marker contract

---

## 1. Relationship to 0.12–0.14

The existing portable OSC 133 methods remain public and byte-for-byte unchanged:

```csharp
ValueTask BeginPromptAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandInputAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandOutputAsync(
	CancellationToken cancellationToken = default
);

ValueTask FinishCommandAsync(
	byte exitStatus,
	CancellationToken cancellationToken = default
);

ValueTask AbortCommandAsync(
	CancellationToken cancellationToken = default
);
```

Their wire forms remain bare `A`, `B`, `C`, `D;status`, and bare `D` respectively.

0.15 adds typed extended metadata only. It does not change the color ownership/restoration contract introduced in 0.14.

---

## 2. Prompt metadata types

```csharp
public enum TerminalSemanticPromptKind {
	Primary = 0,
	Secondary = 1
}

public enum TerminalSemanticPromptResizeBehavior {
	Unspecified = 0,
	ShellDoesNotRedrawPrompt = 1
}

public enum TerminalSemanticPromptClickMode {
	None = 0,
	Absolute = 1,
	Relative = 2
}

public readonly struct TerminalSemanticPromptOptions {
	public TerminalSemanticPromptOptions(
		TerminalSemanticPromptKind kind = TerminalSemanticPromptKind.Primary,
		TerminalSemanticPromptResizeBehavior resizeBehavior = TerminalSemanticPromptResizeBehavior.Unspecified,
		bool useSpecialCursorKey = false,
		TerminalSemanticPromptClickMode clickMode = TerminalSemanticPromptClickMode.None
	);

	public TerminalSemanticPromptKind Kind { get; }
	public TerminalSemanticPromptResizeBehavior ResizeBehavior { get; }
	public bool UseSpecialCursorKey { get; }
	public TerminalSemanticPromptClickMode ClickMode { get; }
}
```

`default(TerminalSemanticPromptOptions)` is valid and semantically equivalent to the existing portable bare `A` marker.

Mapping:

```text
Secondary                    -> k=s
ShellDoesNotRedrawPrompt     -> redraw=0
UseSpecialCursorKey == true  -> special_key=1
Absolute                     -> click_events=1
Relative                     -> click_events=2
```

When multiple fields are present, canonical parameter order is:

```text
redraw=0
special_key=1
k=s
click_events=<1|2>
```

Undefined enum values are rejected with `ArgumentOutOfRangeException` before output commitment.

---

## 3. Extended prompt-start API

```csharp
ValueTask BeginPromptAsync(
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken = default
);
```

The operation uses the existing `TerminalSession` output serialization domain, does not flush implicitly, does not probe terminal support, and does not configure shell bindings or mouse/key protocols.

`A;click_events=1` is the broader cross-terminal tier documented by Kitty and Contour. `redraw=0`, `special_key=1`, `k=s`, and `click_events=2` are narrower Kitty-documented extensions.

The library does not infer support from terminal brand.

---

## 4. Command-output metadata type

```csharp
public readonly struct TerminalSemanticCommandOutputOptions {
	public TerminalSemanticCommandOutputOptions(
		string? commandLine = null
	);

	public string? CommandLine { get; }
}
```

Semantic mapping:

```text
CommandLine == null  -> bare C
CommandLine == ""    -> C;cmdline_url=
otherwise            -> C;cmdline_url=<encoded-value>
```

The null/empty distinction is intentional: null means no command-line metadata, while empty means the caller explicitly knows the command line is empty.

---

## 5. Extended command-output API

```csharp
ValueTask BeginCommandOutputAsync(
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken = default
);
```

Only `cmdline_url` is supported in 0.15. Shell-specific `%q` `cmdline=` is deliberately excluded.

Callers provide ordinary .NET strings, not pre-escaped protocol text. The library does not inspect shell history, capture process command lines, parse shell syntax, normalize quoting/whitespace, infer sensitivity, or redact secrets.

---

## 6. `cmdline_url` encoding

The command line is validated as well-formed UTF-16, converted with strict UTF-8, and percent encoded at the UTF-8 byte level.

Only RFC 3986 unreserved bytes are emitted literally:

```text
A-Z a-z 0-9 - . _ ~
```

Every other UTF-8 byte is encoded as uppercase `%HH`.

Examples:

```text
space -> %20
%     -> %25
;     -> %3B
=     -> %3D
```

Control bytes, OSC framing-sensitive bytes, shell metacharacters, and non-ASCII UTF-8 bytes therefore cannot escape the metadata value.

Ill-formed UTF-16 is rejected with `ArgumentException` before output commitment.

Percent encoding is framing protection only. It does not provide confidentiality.

---

## 7. Payload bound

The maximum OSC 133 payload is **65,536 encoded bytes** measured after `ESC ]` and before the final ST terminator.

The complete payload is encoded and measured before output commitment. Oversize metadata is rejected with an argument-family exception and emits no bytes. Metadata is never truncated.

The limit is an `Icod.Terminal` API safety bound rather than a claim about every terminal emulator's implementation limit.

---

## 8. Ordering and failure semantics

Extended OSC 133 metadata uses the existing `TerminalSession` output serialization gate.

- pre-commit cancellation emits nothing;
- committed frame writes use a non-cancellable transport write;
- concurrent semantic-marker calls serialize as complete frames;
- committed transport failure propagates without compensating OSC traffic;
- a failed marker write does not poison later independent semantic-marker calls;
- no implicit flush is added.

The active terminal-query reader/router architecture is unchanged.

---

## 9. Lifecycle semantics

OSC 133 metadata is ephemeral output metadata, not restorable terminal state.

0.15 adds no:

- session-open automatic marker emission;
- background OSC 133 listener or support cache;
- lifecycle ownership lease;
- suspend-time reset/restore;
- resume replay;
- synthetic `D`/abort during disposal;
- OSC 133 traffic from `InvalidateState()`;
- implicit shell command-region state machine.

Markers remain independently callable before and after lifecycle transitions when the session is otherwise live.

---

## 10. Privacy contract

Publishing command-line metadata is explicit caller intent.

Command lines may contain credentials, bearer tokens, private paths, host names, environment values, or other sensitive information. Terminals and shell-integration/history features may retain or expose published metadata.

`Icod.Terminal` does not automatically redact secrets. Applications must decide whether a command line is appropriate to publish.

---

## 11. Downstream acceptance

The real `Icod.DCurses 0.1.0` semantic-prompt acceptance path runs on `net8.0`, `net9.0`, and `net10.0` and proves both portable and typed extended OSC 133 sequences coexist with `CursesSession.RefreshAsync()` output using only public `TerminalSession` APIs.

No raw OSC shortcut or side-channel writer is used.

---

## 12. Deliberate exclusions

0.15 does not add:

- shell `%q` `cmdline=`;
- arbitrary OSC 133 key/value parameters;
- terminal-brand auto-detection;
- OSC 3008;
- OSC 9 safe extensions;
- modern keyboard negotiation;
- shell auto-install/configuration;
- shell-history/process-command inspection;
- automatic secret redaction;
- generic public OSC/CSI/DCS builders;
- marker lifecycle leases or replay;
- inbound OSC 133 listeners/queries;
- PTY/ConPTY hosting;
- terminal emulation;
- graphics protocols.

---

## 13. Compatibility

The stable package targets:

```text
net8.0
net9.0
net10.0
```

All three remain first-class supported targets. Vendor end-of-support alone does not remove net8.0 or net9.0; removal requires a concrete security or security-maintenance reason.

This document freezes the stable 0.15 public delta.
