# Icod.Terminal 0.15.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.15.0`  
**Development version:** `0.15.0-alpha.5`  
**Predecessor:** `0.14.0` — lifecycle-safe color ownership and exact restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 133 extended semantic metadata without weakening the portable core  
**Status:** T150–T153 green; T154 implemented, exact-head validation pending

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

Cross-terminal extended metadata:

```text
A;click_events=1
C;cmdline_url=...
```

Kitty-documented extended metadata:

```text
A;redraw=0
A;special_key=1
A;k=s
A;click_events=2
```

Shell `%q` `C;cmdline=...` is excluded entirely from 0.15. The library does not infer support from terminal brand; emission is explicit caller intent.

Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

---

## 4. Public semantic model

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

public readonly struct TerminalSemanticCommandOutputOptions {
	public TerminalSemanticCommandOutputOptions(
		string? commandLine = null
	);

	public string? CommandLine { get; }
}
```

New overloads:

```csharp
ValueTask BeginPromptAsync(
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandOutputAsync(
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken = default
);
```

Prompt mapping:

```text
Secondary                    -> k=s
ShellDoesNotRedrawPrompt     -> redraw=0
UseSpecialCursorKey == true  -> special_key=1
Absolute                     -> click_events=1
Relative                     -> click_events=2
```

Command mapping:

```text
CommandLine == null  -> bare C
CommandLine == ""    -> C;cmdline_url=
otherwise            -> C;cmdline_url=<encoded-value>
```

Default option values remain equivalent to the portable bare `A` and `C` methods.

---

## 5. Frozen wire encoding

Prompt parameter order is canonical:

```text
redraw=0
special_key=1
k=s
click_events=<1|2>
```

`cmdline_url`:

1. rejects ill-formed UTF-16;
2. encodes strict UTF-8;
3. emits only RFC 3986 unreserved ASCII literally (`A-Z a-z 0-9 - . _ ~`);
4. encodes every other UTF-8 byte as uppercase `%HH`.

The maximum OSC 133 payload is **65,536 encoded bytes**, measured after `ESC ]` and before final ST. Oversize metadata is rejected before output and is never truncated.

---

## 6. Validation, security, lifecycle, and ordering invariants

- undefined prompt enum -> `ArgumentOutOfRangeException`;
- malformed command-line Unicode -> `ArgumentException`;
- encoded payload overflow -> argument-family exception before output;
- pre-commit cancellation -> cancellation with no output;
- committed marker emission -> one non-cancellable output write, no implicit flush;
- no raw caller-supplied OSC 133 keys/values;
- no shell `%q` encoder;
- no terminal-brand support oracle;
- no automatic command capture or secret redaction;
- no command parsing/normalization;
- no session-open OSC 133 auto-emission;
- no background OSC 133 listener;
- no support cache/probe;
- no replay on resume;
- no lifecycle ownership lease;
- no synthetic `D` on disposal;
- no OSC 133 traffic from `InvalidateState()`;
- one shared `TerminalSession` output serialization domain and existing active-query router.

Command-line metadata may contain credentials, tokens, private paths, host names, or other sensitive material. Percent encoding protects framing only; it does not provide confidentiality.

---

## 7. Tranches

### T150 — contract and reference freeze

**Version:** `0.15.0-alpha.1`  
**Status:** Complete and green at workflow #668.

Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

### T151 — bounded encoder and byte-exact writer foundation

**Version:** `0.15.0-alpha.2`  
**Status:** Complete and green at workflow #678.

Delivered strict UTF-8, canonical byte percent encoding, deterministic prompt ordering, the 65,536-byte payload bound, specialized internal extended `A`/`C` writers, and byte-exact tests.

Record: `docs/T151-OSC-133-Extended-Metadata-Encoder-and-Writer-Foundation.md`.

### T152 — typed prompt-start metadata

**Version:** `0.15.0-alpha.3`  
**Status:** Complete and green at workflow #683.

Delivered the three prompt enums, `TerminalSemanticPromptOptions`, and `BeginPromptAsync(options, ...)` while retaining the portable bare `A` API byte-for-byte.

Record: `docs/T152-Typed-OSC-133-Prompt-Start-Metadata.md`.

### T153 — typed command-output metadata

**Version:** `0.15.0-alpha.4`  
**Status:** Complete and green at workflow #689.

Delivered `TerminalSemanticCommandOutputOptions` and `BeginCommandOutputAsync(options, ...)` using only `cmdline_url`, with null/empty/non-empty semantics, strict Unicode/bound tests, and privacy documentation.

Record: `docs/T153-Typed-OSC-133-Command-Output-Metadata.md`.

### T154 — session integration and compatibility

**Version:** `0.15.0-alpha.5`  
**Status:** Implemented; exact-head validation pending.

No production API or protocol changes were required. Existing composition tests retain byte-exact bare OSC 133 coverage and now additionally prove extended metadata with:

- ordinary application text;
- OSC 7 current-location publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard output;
- OSC 22 pointer shape;
- synchronized output;
- terminal progress;
- an outstanding `QueryDeviceStatusAsync(...)` transaction and its correlated response.

The tests prove extended OSC 133 uses the established session output gate without disturbing the single active-query/response router.

Record: `docs/T154-OSC-133-Session-Integration-and-Compatibility.md`.

### T155 — lifecycle, failure, ordering, and security hardening

**Expected version:** `0.15.0-alpha.6`.

Cover output failure, concurrent marker calls, lifecycle suspension while queued, invalidation/disposal, no replay/synthesis, injection/oversize/malformed inputs, and lock-order/deadlock resistance.

### T156 — downstream acceptance

**Expected version:** `0.15.0-alpha.7`.

Extend real `Icod.DCurses` acceptance to prove extended OSC 133 metadata coexists with a higher-level full-screen consumer using only public semantic APIs and the shared output path.

### T157 — public API/package/stable closure

**Stable version:** `0.15.0`.

Deliver public API baseline, README/sample updates, XML documentation assertions, fresh NuGet-only net8/net9/net10 consumer, retained 0.8–0.14 package gates, retained downstream acceptance, new 0.15 package contract in PR/main/tag validation, release metadata, and exact-head validation.

---

## 8. Required testing matrix

0.15 SHALL include deterministic tests for unchanged bare `A/B/C/D`, default-option equivalence, every supported prompt field/combination, strict ASCII/Unicode/non-BMP command encoding, framing-sensitive characters, malformed UTF-16, empty command lines, exact payload limits, cancellation/output failure/concurrency/lifecycle ordering, composition with session managers/query routing, Windows/Linux/macOS CI, and net8/net9/net10 package-only consumers.

Tests validate emitted bytes directly and do not depend on the CI host terminal understanding extended OSC 133 metadata.

---

## 9. Explicit non-goals

0.15 SHALL NOT add `%q` `cmdline=`, arbitrary OSC 133 key/value parameters, OSC 3008, OSC 9 extensions, modern keyboard negotiation, terminal-brand auto-detection, shell auto-install/configuration, shell-history/process-command inspection, command parsing/execution, automatic secret redaction, generic public OSC/CSI/DCS builders, marker lifecycle leases/resume replay, inbound OSC 133 listeners/queries, PTY/ConPTY hosting, terminal emulation, or graphics protocols.

---

## 10. Current development state

```text
VersionPrefix:    0.15.0
VersionSuffix:    alpha.5
Version:          0.15.0-alpha.5
PackageVersion:   0.15.0-alpha.5
AssemblyVersion:  0.15.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T154 validation:** T155 — lifecycle, failure, ordering, and security hardening.
