# Icod.Terminal 0.15.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.15.0`  
**Development version:** `0.15.0-alpha.4`  
**Predecessor:** `0.14.0` — lifecycle-safe color ownership and exact restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 133 extended semantic metadata without weakening the portable core  
**Status:** T150–T152 green; T153 implemented, exact-head validation pending

---

## 1. Road to 1.0

```text
0.15.0       OSC 133 Extended Metadata
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

0.15 remains focused on OSC 133. It does not include OSC 9 extensions, modern keyboard negotiation, OSC 3008, PTY/ConPTY hosting, graphics protocols, terminal emulation, or generic public OSC/CSI/DCS builders.

---

## 2. Portable OSC 133 core retained from 0.12

These existing methods remain byte-for-byte unchanged:

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

Wire forms:

```text
OSC 133;A ST
OSC 133;B ST
OSC 133;C ST
OSC 133;D;status ST
OSC 133;D ST
```

Markers remain independently callable. `TerminalSession` does not impose a shell command-region state machine, support cache, or lifecycle replay model.

---

## 3. T150 frozen extended-metadata contract

Reference sources:

- FinalTerm/iTerm2 OSC 133 core semantics;
- Kitty shell integration;
- Contour OSC 133 shell integration.

Interoperability tiers:

**Portable core**

```text
A
B
C
D;status
D
```

**Cross-terminal extended metadata**

```text
A;click_events=1
C;cmdline_url=...
```

**Kitty-documented extended metadata**

```text
A;redraw=0
A;special_key=1
A;k=s
A;click_events=2
```

Shell `%q` `C;cmdline=...` is excluded entirely from 0.15.

The library does not infer support from terminal brand. Emission is explicit caller intent.

Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

---

## 4. Frozen public semantic model

### Prompt metadata

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

Mapping:

```text
Secondary                    -> k=s
ShellDoesNotRedrawPrompt     -> redraw=0
UseSpecialCursorKey == true  -> special_key=1
Absolute                     -> click_events=1
Relative                     -> click_events=2
```

`default(TerminalSemanticPromptOptions)` is valid and equivalent to bare `A`.

New overload:

```csharp
ValueTask BeginPromptAsync(
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken = default
);
```

### Command-output metadata

```csharp
public readonly struct TerminalSemanticCommandOutputOptions {
	public TerminalSemanticCommandOutputOptions(
		string? commandLine = null
	);

	public string? CommandLine { get; }
}
```

Mapping:

```text
CommandLine == null  -> bare C
CommandLine == ""    -> C;cmdline_url=
otherwise            -> C;cmdline_url=<encoded-value>
```

`default(TerminalSemanticCommandOutputOptions)` is valid and equivalent to bare `C`.

New overload:

```csharp
ValueTask BeginCommandOutputAsync(
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken = default
);
```

---

## 5. Frozen wire encoding

Prompt parameter order is canonical:

```text
redraw=0
special_key=1
k=s
click_events=<1|2>
```

Example:

```text
OSC 133;A;redraw=0;special_key=1;k=s;click_events=2 ST
```

`cmdline_url` encoding:

1. reject ill-formed UTF-16;
2. encode to strict UTF-8;
3. emit only RFC 3986 unreserved ASCII bytes literally:

```text
A-Z a-z 0-9 - . _ ~
```

4. encode every other UTF-8 byte as uppercase `%HH`.

Examples:

```text
space -> %20
%     -> %25
;     -> %3B
=     -> %3D
```

The maximum OSC 133 payload is **65,536 encoded bytes**, measured after `ESC ]` and before final ST. Oversize metadata is rejected before output and is never truncated.

---

## 6. Validation and security invariants

- undefined prompt enum -> `ArgumentOutOfRangeException`;
- malformed command-line Unicode -> `ArgumentException`;
- encoded payload overflow -> argument-family exception before output;
- pre-commit cancellation -> cancellation with no output;
- committed marker emission -> one non-cancellable output write, no implicit flush;
- no raw caller-supplied OSC 133 keys/values;
- no shell `%q` encoder;
- no terminal-brand support oracle;
- no automatic command capture;
- no secret detection/redaction;
- no command parsing or normalization.

Command-line metadata can contain credentials, API keys, bearer tokens, private paths, host names, or other sensitive material. Percent encoding protects framing only; it does not provide confidentiality.

---

## 7. Lifecycle and ordering invariants

Extended OSC 133 metadata is ephemeral output metadata, not restorable terminal state.

0.15 retains:

- no session-open auto-emission;
- no background OSC 133 listener;
- no support cache/probe;
- no replay on resume;
- no suspend-time reset/restore;
- no lifecycle ownership lease;
- no synthetic `D` on disposal;
- no OSC 133 traffic from `InvalidateState()`;
- one shared `TerminalSession` output serialization domain.

---

## 8. Tranches

### T150 — contract and reference freeze

**Version:** `0.15.0-alpha.1`  
**Status:** Complete and green at workflow #668.

Frozen the supported forms, interoperability tiers, public model, encoding rules, payload ceiling, error semantics, lifecycle rules, privacy/security contract, and explicit non-goals.

Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

### T151 — bounded encoder and byte-exact writer foundation

**Version:** `0.15.0-alpha.2`  
**Status:** Complete and green at workflow #678.

Delivered:

- `TerminalOsc133ExtendedMetadataEncoder`;
- strict UTF-8 and canonical byte percent encoding;
- deterministic prompt parameter ordering;
- 65,536-byte payload enforcement;
- specialized internal extended `A`/`C` writers;
- byte-exact tests for supported fields, Unicode, injection characters, payload bounds, cancellation, and committed-write behavior.

Record: `docs/T151-OSC-133-Extended-Metadata-Encoder-and-Writer-Foundation.md`.

### T152 — typed prompt-start metadata

**Version:** `0.15.0-alpha.3`  
**Status:** Complete and green at workflow #683.

Delivered the three public prompt enums, `TerminalSemanticPromptOptions`, and `BeginPromptAsync(options, ...)` while preserving the original bare `A` API byte-for-byte.

Record: `docs/T152-Typed-OSC-133-Prompt-Start-Metadata.md`.

### T153 — typed command-output metadata

**Version:** `0.15.0-alpha.4`  
**Status:** Implemented; exact-head validation pending.

Delivered `TerminalSemanticCommandOutputOptions` and `BeginCommandOutputAsync(options, ...)` using only `cmdline_url`.

Tests cover:

- default/bare `C` byte identity;
- null/empty/non-empty distinctions;
- strict UTF-8 Unicode/non-BMP encoding;
- preservation of caller text;
- malformed UTF-16 rejection before output;
- exact 65,536-byte boundary;
- one-byte-over rejection;
- cancellation and committed-write behavior.

Record: `docs/T153-Typed-OSC-133-Command-Output-Metadata.md`.

### T154 — session integration and compatibility

**Expected version:** `0.15.0-alpha.5`.

Prove old/new OSC 133 APIs compose correctly with:

- ordinary text output;
- synchronized output;
- progress;
- pointer shape;
- scoped colors;
- hyperlinks;
- current-location publication;
- clipboard operations;
- active queries/input routing.

The full 0.12 OSC 133 public surface must remain byte-for-byte unchanged.

### T155 — lifecycle, failure, ordering, and security hardening

**Expected version:** `0.15.0-alpha.6`.

Cover:

- output failure;
- concurrent marker calls;
- lifecycle suspension while a marker is queued;
- invalidation and disposal;
- no replay/synthesis;
- injection attempts;
- oversized metadata;
- malformed enum/Unicode input;
- lock-order/deadlock resistance.

### T156 — downstream acceptance

**Expected version:** `0.15.0-alpha.7`.

Extend real `Icod.DCurses` acceptance to prove extended OSC 133 metadata coexists with a higher-level full-screen consumer using only public semantic APIs and the shared output path.

### T157 — public API/package/stable closure

**Stable version:** `0.15.0`.

Deliver:

- `docs/Public-API-Baseline-0.15.md`;
- README extended-metadata examples and privacy warning;
- semantic-prompt sample update;
- XML documentation assertions for every new public member;
- fresh NuGet-only 0.15 consumer on net8/net9/net10;
- retained 0.8–0.14 package gates;
- retained downstream acceptance;
- new 0.15 package contract in PR, main/distribution, and tag/release validation;
- stable release notes/tags;
- exact-head PR/main/tag validation.

---

## 9. Required testing matrix

0.15 SHALL include deterministic tests for:

- unchanged bare `A/B/C/D` compatibility;
- default options equivalence to bare `A` and `C`;
- every supported prompt field;
- legal prompt combinations and canonical ordering;
- strict `cmdline_url` encoding of ASCII/Unicode/non-BMP text;
- percent, semicolon, equals, BEL, ESC/ST, CR/LF, NUL, tab, quotes, backslash, and shell metacharacters;
- malformed UTF-16;
- empty command-line semantics;
- exact 65,536-byte payload and one-byte-over rejection;
- cancellation, output failure, concurrency, lifecycle ordering;
- composition with other session managers;
- Windows/Linux/macOS CI;
- net8/net9/net10 package-only consumers.

Tests validate emitted bytes directly and do not depend on the CI host terminal understanding extended OSC 133 metadata.

---

## 10. Explicit non-goals

0.15 SHALL NOT add:

- `%q` `cmdline=`;
- arbitrary OSC 133 key/value parameters;
- OSC 3008 hierarchical context signaling;
- OSC 9 notification/CWD/tab extensions;
- modern keyboard negotiation;
- terminal-brand auto-detection;
- shell auto-install/configuration;
- shell-history/process-command inspection;
- command parsing/execution;
- automatic secret redaction;
- generic public OSC/CSI/DCS builders;
- marker lifecycle leases or resume replay;
- inbound OSC 133 listeners/queries;
- PTY/ConPTY hosting;
- terminal emulation;
- graphics protocols.

---

## 11. Current development state

```text
VersionPrefix:    0.15.0
VersionSuffix:    alpha.4
Version:          0.15.0-alpha.4
PackageVersion:   0.15.0-alpha.4
AssemblyVersion:  0.15.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T153 validation:** T154 — session integration and compatibility.
