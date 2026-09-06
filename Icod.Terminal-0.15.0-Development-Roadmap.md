# Icod.Terminal 0.15.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.15.0`  
**Development version:** `0.15.0-alpha.1`  
**Predecessor:** `0.14.0` — lifecycle-safe color ownership and exact restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 133 extended semantic metadata without weakening the portable core  
**Status:** T150 contract/reference freeze complete; T151 next after exact-head validation

---

## 1. Position on the road to 1.0

```text
0.15.0       OSC 133 Extended Metadata
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

0.15 remains focused on OSC 133. It does not pull OSC 9 safe extensions, modern keyboard negotiation, OSC 3008, PTY hosting, graphics protocols, generic public OSC construction, or broad 0.18 hardening into this release.

---

## 2. Portable OSC 133 core retained from 0.12

The existing public semantic API remains unchanged:

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

These continue to emit exactly:

```text
OSC 133 ; A ST
OSC 133 ; B ST
OSC 133 ; C ST
OSC 133 ; D ; status ST
OSC 133 ; D ST
```

0.15 SHALL NOT alter the bytes or semantics of these methods.

The marker API remains intentionally stateless: markers are independently callable; the session does not impose a shell command-region state machine; successful completion proves emission rather than terminal support; and output ordering remains protected by the session output serialization domain.

---

## 3. T150 frozen reference tiers

Reference material:

- FinalTerm/iTerm2 core OSC 133 semantics: https://iterm2.com/documentation-one-page.html
- Kitty shell integration: https://sw.kovidgoyal.net/kitty/shell-integration/
- Contour OSC 133 shell integration: https://contour-terminal.org/vt-extensions/osc-133-shell-integration/

### Portable core

```text
A
B
C
D;status
D
```

### Cross-terminal extended metadata

Documented independently by Kitty and Contour:

```text
A;click_events=1
C;cmdline_url=...
```

### Kitty-documented extended metadata

```text
A;redraw=0
A;special_key=1
A;k=s
A;click_events=2
```

### Excluded from 0.15

Kitty also documents:

```text
C;cmdline=<shell-%q-encoded-text>
```

T150 excludes `%q` `cmdline=` from 0.15 entirely. Shell-family escaping policy does not belong in the core terminal-semantic library when the same semantic information is available through cross-terminal `cmdline_url`.

The library does not infer support from a terminal brand. Extended emission is explicit caller intent.

Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

---

## 4. Frozen public semantic model

### Prompt kind

```csharp
public enum TerminalSemanticPromptKind {
	Primary = 0,
	Secondary = 1
}
```

- `Primary` -> no `k` parameter;
- `Secondary` -> `k=s`.

### Resize behavior

```csharp
public enum TerminalSemanticPromptResizeBehavior {
	Unspecified = 0,
	ShellDoesNotRedrawPrompt = 1
}
```

- `Unspecified` -> no `redraw` parameter;
- `ShellDoesNotRedrawPrompt` -> `redraw=0`.

There is no invented `redraw=1` form and no `ShellRedrawsPrompt` enum value in 0.15.

### Click mode

```csharp
public enum TerminalSemanticPromptClickMode {
	None = 0,
	Absolute = 1,
	Relative = 2
}
```

- `None` -> no `click_events` parameter;
- `Absolute` -> `click_events=1`;
- `Relative` -> `click_events=2`.

`Absolute` is the broader cross-terminal tier. `Relative` is the narrower Kitty-documented tier.

### Prompt options

```csharp
public readonly struct TerminalSemanticPromptOptions {
	public TerminalSemanticPromptKind Kind { get; }
	public TerminalSemanticPromptResizeBehavior ResizeBehavior { get; }
	public bool UseSpecialCursorKey { get; }
	public TerminalSemanticPromptClickMode ClickMode { get; }
}
```

`default(TerminalSemanticPromptOptions)` is valid and equivalent to bare primary `A` semantics.

`UseSpecialCursorKey == true` emits `special_key=1`; `false` emits no parameter. No `special_key=0` form is generated.

No cross-field dependency is imposed between special-key declaration and click mode. The API publishes caller-declared shell capability metadata; it does not configure shell key bindings or a terminal mouse protocol.

### Command-output options

```csharp
public readonly struct TerminalSemanticCommandOutputOptions {
	public string? CommandLine { get; }
}
```

- `null` -> bare `C`;
- empty string -> `C;cmdline_url=`;
- non-empty string -> `C;cmdline_url=<encoded-value>`.

The null/empty distinction is intentional: no metadata versus explicitly known empty command line.

`default(TerminalSemanticCommandOutputOptions)` is valid and equivalent to bare `C` semantics.

### New overloads

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

The old overloads remain the simplest portable path.

---

## 5. Frozen prompt parameter ordering

When multiple `A` parameters are present, 0.15 emits one canonical order:

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

Only represented options are emitted. T151 shall test byte-exact deterministic order.

---

## 6. Frozen `cmdline_url` encoding

Callers provide ordinary .NET strings, never pre-escaped metadata.

Encoding is:

1. validate the input as well-formed UTF-16;
2. encode with strict UTF-8;
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

BEL, ESC, ST bytes, CR, LF, NUL, tabs, quotes, backslashes, shell metacharacters, and all non-ASCII UTF-8 bytes are percent encoded.

The encoder operates on UTF-8 bytes, not UTF-16 code units.

Ill-formed UTF-16 containing an unpaired surrogate is rejected before output commitment instead of silently becoming U+FFFD.

Percent encoding provides framing safety, not confidentiality or secret redaction.

---

## 7. Frozen payload bound and validation

The maximum OSC 133 payload is **65,536 bytes**.

"Payload" means bytes after `ESC ]` and before final ST, so the count includes:

```text
133;A;...
```

or

```text
133;C;cmdline_url=...
```

but excludes the OSC introducer and ST terminator.

The complete frame is encoded and measured before output commitment. Oversize input produces an argument-family exception and emits no bytes; metadata is never truncated.

The bound applies to encoded wire bytes, not .NET character count, and is an `Icod.Terminal` API safety limit rather than a claim about every terminal emulator's maximum OSC size.

Validation contract:

- undefined enum -> `ArgumentOutOfRangeException`;
- ill-formed command-line Unicode -> `ArgumentException`;
- encoded payload overflow -> argument-family exception before output commitment;
- pre-commit cancellation -> cancellation, no output;
- transport/session failures remain distinct.

No raw caller-supplied parameter keys or values exist in the public model.

---

## 8. Lifecycle, ordering, and privacy invariants

Extended OSC 133 metadata is ephemeral output metadata, not restorable terminal state.

0.15 retains:

- no session-open automatic OSC 133 emission;
- no background OSC 133 listener;
- no terminal support cache/probe;
- no marker replay on resume;
- no suspend-time marker reset/restore;
- no marker lifecycle lease;
- no synthesized missing `D` marker during disposal;
- no implicit command-region state machine;
- no OSC 133 traffic from `InvalidateState()`;
- one existing session output serialization path.

Command-line publication is explicit caller intent only. `Icod.Terminal` does not inspect shell history/process command lines, parse shell syntax, normalize quoting, automatically capture commands, detect secrets, or redact credentials.

Public documentation must warn that command lines can contain credentials, bearer tokens, private paths, host names, or other sensitive material and that terminal shell-integration/history features may retain or expose the published metadata.

---

## 9. Tranche sequence

### T150 — OSC 133 extended-metadata contract and reference freeze

**Version:** `0.15.0-alpha.1`  
**Status:** Complete; exact-head validation pending.

Frozen:

- reference/interoperability tiers;
- supported `A` and `C` parameters;
- `%q` exclusion;
- public semantic types and overloads;
- deterministic `A` parameter order;
- strict UTF-8 / percent-encoding rules;
- 65,536-byte OSC payload ceiling;
- validation/error behavior;
- lifecycle and privacy/security invariants.

Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

### T151 — parameter encoder and byte-exact writer foundation

**Expected version:** `0.15.0-alpha.2`.

Implement internal primitives only:

- strict UTF-8 conversion;
- canonical byte percent encoding;
- frozen prompt parameter serialization;
- bounded OSC 133 payload construction;
- specialized `A`/`C` frame encoding/writing.

Prove exact bytes, Unicode/non-BMP input, injection resistance, payload-bound edges, and cancellation-before-commit behavior. Do not expose a raw metadata API.

### T152 — prompt-start extended metadata

**Expected version:** `0.15.0-alpha.3`.

Implement the frozen prompt enums/options and `BeginPromptAsync(options, ...)`.

Prove each field independently, legal combinations, default-options equivalence to bare `A`, canonical ordering, common/narrower interoperability documentation, and byte-for-byte compatibility of the existing parameterless API.

### T153 — command-line metadata

**Expected version:** `0.15.0-alpha.4`.

Implement `TerminalSemanticCommandOutputOptions` and `BeginCommandOutputAsync(options, ...)` using only `cmdline_url`.

Prove null/empty/non-empty distinctions, ASCII/Unicode/non-BMP text, controls and shell metacharacters, exact payload boundary, strict malformed-Unicode rejection, no automatic capture, and no mutation of caller text.

### T154 — session integration and compatibility

**Expected version:** `0.15.0-alpha.5`.

Prove old/new OSC 133 APIs compose with ordinary output, synchronized output, progress, pointer shape, scoped colors, hyperlinks, current-location publication, clipboard operations, and active query/input routing.

The complete 0.12 OSC 133 public surface remains byte-for-byte unchanged.

### T155 — lifecycle, failure, ordering, and security hardening

**Expected version:** `0.15.0-alpha.6`.

Cover cancellation, output failure, concurrent marker calls, lifecycle suspension while queued, invalidation, disposal, no replay/synthesis, injection attempts, oversized metadata, malformed enum/Unicode input, and lock-order/deadlock resistance.

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

## 10. Required testing matrix

0.15 SHALL add deterministic tests for:

- unchanged bare `A/B/C/D` compatibility;
- default options equivalence to bare `A` and `C`;
- every supported `A` field;
- legal combinations and canonical ordering;
- secondary `k=s`;
- click modes 1 and 2;
- non-redraw declaration;
- special-key declaration;
- `cmdline_url` ASCII/Unicode/non-BMP percent encoding;
- `%`, semicolon, equals, BEL, ESC/ST, CR/LF, NUL, tab, quotes, backslash, and shell metacharacters;
- malformed UTF-16;
- empty command-line semantics;
- exact 65,536-byte payload and one-byte-over rejection;
- cancellation, output failure, concurrency, lifecycle ordering;
- composition with other session managers;
- Windows/Linux/macOS CI;
- net8/net9/net10 package-only consumers.

Tests validate emitted bytes directly and do not depend on the CI host terminal understanding extended OSC 133 metadata.

---

## 11. Explicit non-goals

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

## 12. Current development state

```text
VersionPrefix:    0.15.0
VersionSuffix:    alpha.1
Version:          0.15.0-alpha.1
PackageVersion:   0.15.0-alpha.1
AssemblyVersion:  0.15.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T150 validation:** T151 — bounded parameter encoder and byte-exact writer foundation.
