# T150 — OSC 133 Extended Metadata Contract and Reference Freeze

**Release:** `Icod.Terminal 0.15.0`  
**Version:** `0.15.0-alpha.1`  
**Status:** Frozen  
**Theme:** Typed OSC 133 extended metadata without weakening the portable 0.12 core

## 1. Scope

T150 freezes the semantic, wire, validation, interoperability, lifecycle, and security contract for the OSC 133 extensions that may be implemented in 0.15.0.

No implementation code is required by this tranche. T151 and later tranches SHALL conform to this document unless a subsequent explicit contract revision is recorded before stable release.

The 0.12 public OSC 133 API remains unchanged and byte-for-byte compatible:

```text
OSC 133 ; A ST
OSC 133 ; B ST
OSC 133 ; C ST
OSC 133 ; D ; status ST
OSC 133 ; D ST
```

The existing public methods remain the portable path:

```csharp
BeginPromptAsync(...)
BeginCommandInputAsync(...)
BeginCommandOutputAsync(...)
FinishCommandAsync(...)
AbortCommandAsync(...)
```

0.15 adds metadata only to `A` and `C` through new typed overloads.

## 2. References frozen for 0.15

The reference set is:

- FinalTerm/iTerm2 OSC 133 core semantics (`A`, `B`, `C`, `D`):
  https://iterm2.com/documentation-one-page.html
- Kitty shell integration / notes for shell developers:
  https://sw.kovidgoyal.net/kitty/shell-integration/
- Contour OSC 133 shell integration:
  https://contour-terminal.org/vt-extensions/osc-133-shell-integration/

Kitty currently documents these `A` parameters:

```text
redraw=0
special_key=1
k=s
click_events=1
click_events=2
```

and these `C` alternatives:

```text
cmdline=<shell-%q-encoded-text>
cmdline_url=<UTF-8 URL-percent-escaped text>
```

Contour independently documents:

```text
A;click_events=1
C;cmdline_url=<percent-encoded-command-line>
```

The iTerm2/FinalTerm documentation remains the compatibility reference for the bare `A/B/C/D` core but is not used to claim support for the extended parameters above.

## 3. Interoperability tiers

0.15 documents protocol forms by evidence tier, not by terminal-brand inference.

### Portable core

Retained unchanged:

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

These are the preferred extended forms when the caller wants the broadest evidence-backed interoperability available in 0.15.

### Kitty-documented extended metadata

```text
A;redraw=0
A;special_key=1
A;k=s
A;click_events=2
```

These remain valid 0.15 semantic APIs, but documentation SHALL identify their narrower reference tier.

### Explicitly excluded

`C;cmdline=...` using shell `%q` encoding is excluded from 0.15 implementation, both public and internal.

Reasons:

- `%q` is shell-family policy rather than terminal-semantic policy;
- Bash/Zsh/Fish do not share one universal `%q` contract;
- the same semantic information is available through `cmdline_url`, which is documented by both Kitty and Contour;
- implementing `%q` in the core library would unnecessarily make shell escaping part of `Icod.Terminal`'s public or internal protocol surface.

A future shell-specific integration layer may revisit `%q` if a real consumer requires it.

## 4. Frozen public semantic types

T150 freezes the following intended public model for implementation in T152/T153.

### Prompt kind

```csharp
public enum TerminalSemanticPromptKind {
	Primary = 0,
	Secondary = 1
}
```

Wire mapping:

- `Primary` -> no `k` parameter;
- `Secondary` -> `k=s`.

`k=s` is semantic secondary/PS2-prompt metadata. No public literal `k` or `s` values are exposed.

### Prompt resize behavior

```csharp
public enum TerminalSemanticPromptResizeBehavior {
	Unspecified = 0,
	ShellDoesNotRedrawPrompt = 1
}
```

Wire mapping:

- `Unspecified` -> no `redraw` parameter;
- `ShellDoesNotRedrawPrompt` -> `redraw=0`.

There is deliberately no `ShellRedrawsPrompt` value in 0.15. The protocol form frozen here only has an explicit negative declaration (`redraw=0`); absence remains the default/unspecified behavior. This avoids inventing a wire-level affirmative value such as `redraw=1` that is not part of the frozen reference contract.

### Prompt click mode

```csharp
public enum TerminalSemanticPromptClickMode {
	None = 0,
	Absolute = 1,
	Relative = 2
}
```

Wire mapping:

- `None` -> no `click_events` parameter;
- `Absolute` -> `click_events=1`;
- `Relative` -> `click_events=2`.

`Absolute` is the cross-terminal extended tier. `Relative` is the narrower Kitty-documented tier.

The enum describes the coordinate mode of prompt click events; it does not enable an `Icod.Terminal` mouse protocol or keyboard binding.

### Prompt options

```csharp
public readonly struct TerminalSemanticPromptOptions {
	public TerminalSemanticPromptKind Kind { get; }
	public TerminalSemanticPromptResizeBehavior ResizeBehavior { get; }
	public bool UseSpecialCursorKey { get; }
	public TerminalSemanticPromptClickMode ClickMode { get; }
}
```

The implementation SHOULD provide a constructor whose optional/default values produce the same semantics as a bare primary `A` marker:

```csharp
new TerminalSemanticPromptOptions(
	kind: TerminalSemanticPromptKind.Primary,
	resizeBehavior: TerminalSemanticPromptResizeBehavior.Unspecified,
	useSpecialCursorKey: false,
	clickMode: TerminalSemanticPromptClickMode.None
)
```

`default(TerminalSemanticPromptOptions)` SHALL therefore be valid and equivalent to bare `A` semantics.

`UseSpecialCursorKey == true` maps to `special_key=1`; `false` omits the parameter. No `special_key=0` form is emitted.

No cross-field dependency is imposed between `UseSpecialCursorKey` and `ClickMode`. The library publishes caller-declared shell capability metadata; it does not infer whether the caller configured a corresponding binding.

### Command-output options

```csharp
public readonly struct TerminalSemanticCommandOutputOptions {
	public string? CommandLine { get; }
}
```

Semantics:

- `CommandLine == null` -> bare `C` marker;
- `CommandLine == ""` -> `C;cmdline_url=`;
- any other string -> `C;cmdline_url=<encoded-value>`.

The distinction between `null` and empty string is intentional: `null` means "no command-line metadata supplied" while empty means "the caller explicitly supplied a known empty command line".

The implementation SHOULD provide a constructor accepting `string? commandLine`.

`default(TerminalSemanticCommandOutputOptions)` SHALL be valid and equivalent to bare `C` semantics.

## 5. Frozen public overloads

T152/T153 SHALL add overloads equivalent to:

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

The existing parameterless semantic overloads remain unchanged and byte-for-byte compatible.

The options overloads SHALL remain independently callable. `TerminalSession` SHALL NOT require a preceding `A`, `B`, or `C` marker and SHALL NOT introduce an implicit shell command-region state machine.

## 6. Frozen `A` parameter order

When multiple prompt parameters are emitted together, the wire order is canonical and deterministic:

```text
redraw=0
special_key=1
k=s
click_events=<1|2>
```

Only parameters represented by the semantic options are emitted.

Examples:

```text
Primary/default:
OSC 133;A ST

Secondary only:
OSC 133;A;k=s ST

No redraw + special key + secondary + relative clicks:
OSC 133;A;redraw=0;special_key=1;k=s;click_events=2 ST
```

T151 tests SHALL verify this ordering directly. Terminal parsers are expected to treat the fields semantically, but `Icod.Terminal` emits one canonical order for reproducibility and package-contract stability.

## 7. Frozen `cmdline_url` encoding

Command-line metadata is supplied as an ordinary .NET `string`; callers never supply pre-escaped bytes or percent escapes.

### Unicode conversion

The string SHALL be converted using strict UTF-8.

Ill-formed UTF-16 input containing an unpaired surrogate SHALL be rejected before output commitment rather than silently replaced with U+FFFD.

Public failure SHOULD be surfaced as `ArgumentException` associated with the command-line metadata argument/options.

### Percent-encoding safe set

Only RFC 3986 unreserved ASCII bytes are emitted literally:

```text
A-Z a-z 0-9 - . _ ~
```

Every other UTF-8 byte is emitted as:

```text
%HH
```

where `HH` is two uppercase hexadecimal digits.

Therefore:

- space -> `%20`, never `+`;
- `%` -> `%25`;
- `;` -> `%3B`;
- `=` -> `%3D`;
- BEL, ESC, CR, LF, NUL, tab, quotes, backslashes, shell metacharacters, and all non-ASCII UTF-8 bytes are percent encoded;
- no metadata string can inject another OSC field, terminator, or frame.

The percent encoder SHALL operate on UTF-8 bytes, not UTF-16 code units.

## 8. Frozen payload bound

The maximum OSC 133 payload is **65,536 bytes**.

For this contract, "payload" means the bytes after the OSC introducer (`ESC ]`) and before the final ST (`ESC \\`). Thus the count includes:

```text
133;A;...
```

or

```text
133;C;cmdline_url=...
```

but excludes the two-byte OSC introducer and two-byte ST terminator.

The full frame SHALL be encoded and measured before output commitment.

If the encoded payload would exceed 65,536 bytes:

- no bytes are emitted;
- public API invocation fails with `ArgumentException` (or a more specific argument-range exception if the implementation can preserve a clear parameter association);
- no partial/truncated metadata form is emitted.

The limit is intentionally on encoded wire bytes, not .NET character count. A Unicode command line may therefore reach the bound sooner than a simple ASCII command line.

The payload limit is an `Icod.Terminal` API safety bound, not a claim that every terminal supports OSC strings of that length.

## 9. Validation contract

Before acquiring/committing output, extended semantic APIs SHALL validate:

- all enum values are defined;
- the command-line string is valid UTF-16 and can be encoded using strict UTF-8;
- the final encoded OSC payload does not exceed 65,536 bytes;
- only library-owned keys and frozen values are present.

Invalid enum values SHALL produce `ArgumentOutOfRangeException`.

Invalid command-line Unicode or encoded-size overflow SHALL produce an argument-family exception before output commitment.

Caller cancellation observed before transmission commitment SHALL remain `OperationCanceledException`/task cancellation and emit nothing.

Terminal/output/session failures remain distinct from argument validation failures.

## 10. Output framing and commitment

Extended metadata SHALL reuse the existing OSC 133 writer/session output path.

Outbound framing remains:

```text
ESC ] 133 ; ... ESC \
```

with ST termination. 0.15 does not add a BEL-terminated outbound mode.

All bytes for a frame SHALL be encoded before output commitment.

Once the session output serialization lease is acquired and transmission is committed, the writer SHALL attempt one complete frame under the existing session ordering contract. Extended metadata SHALL not open a second output path.

## 11. Lifecycle contract

OSC 133 metadata remains ephemeral semantic output, not restorable terminal state.

0.15 SHALL NOT add:

- automatic marker emission on session open;
- marker replay on resume;
- reset/restore markers on suspend;
- lifecycle leases for semantic markers;
- synthetic missing `D` markers on disposal;
- automatic command-region state tracking;
- terminal-support caches or probes.

`InvalidateState()` does not emit OSC 133 traffic.

Managed suspend/resume does not replay a previously emitted extended marker. Applications that need to re-publish shell semantic state after lifecycle interruption remain responsible for doing so.

## 12. Privacy and security contract

Command-line publication is always explicit caller intent.

`Icod.Terminal` SHALL NOT:

- inspect shell history;
- inspect process command lines automatically;
- infer the active shell command;
- parse shell syntax;
- normalize quoting or whitespace;
- redact credentials/tokens/passwords automatically;
- infer whether a command line is sensitive.

Documentation for the public command-output metadata API MUST warn that command lines can contain secrets, credentials, bearer tokens, private paths, host names, or other sensitive data and that publishing them through OSC 133 may make them available to terminal history/integration features.

Percent encoding is framing protection, not confidentiality or redaction.

## 13. Compatibility requirements

T151–T157 SHALL prove that:

- existing `BeginPromptAsync()` still emits exactly bare `A`;
- existing `BeginCommandInputAsync()` still emits exactly bare `B`;
- existing `BeginCommandOutputAsync()` still emits exactly bare `C`;
- `FinishCommandAsync(byte)` and `AbortCommandAsync()` remain byte-for-byte unchanged;
- new default options emit the same bare `A`/`C` forms as the existing methods;
- no existing 0.12 consumer needs source changes;
- no new public raw OSC parameter API is exposed.

## 14. Explicit 0.15 non-goals

T150 excludes the following from the 0.15 contract:

- `C;cmdline=` shell `%q` encoding;
- arbitrary/unknown OSC 133 key/value parameters;
- OSC 3008 hierarchical context signaling;
- OSC 9 safe extensions;
- modern keyboard negotiation;
- automatic terminal-brand detection;
- automatic shell integration installation;
- command parsing or execution;
- secret detection/redaction;
- PTY/ConPTY hosting;
- terminal emulation;
- generic public OSC/CSI/DCS construction;
- inbound OSC 133 query/listener APIs;
- marker restoration or lifecycle replay.

## 15. T151 implementation gate

T151 may begin only from this frozen contract.

The next tranche SHALL implement internal primitives for:

- strict UTF-8 conversion;
- canonical RFC-3986-unreserved percent encoding;
- frozen `A` parameter ordering;
- bounded payload construction;
- byte-exact OSC 133 `A`/`C` frame emission;
- no public raw metadata surface.

No T151 implementation should invent additional semantics beyond this document.
