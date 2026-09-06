# Icod.Terminal 0.15.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.15.0`  
**Development version:** `0.15.0-alpha.1`  
**Predecessor:** `0.14.0` — lifecycle-safe color ownership and exact restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 133 extended semantic metadata without weakening the portable core  
**Status:** Roadmap/foundation opened; T150 next

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

0.15 SHALL remain focused on OSC 133. It SHALL NOT pull OSC 9 safe extensions, CSI-u/Kitty keyboard negotiation, `modifyOtherKeys`, generic public OSC construction, broad terminal hardening, PTY hosting, graphics protocols, or OSC 3008 into this release.

---

## 2. Existing portable OSC 133 contract retained from 0.12

The 0.12 public semantic API remains the compatibility foundation:

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

These map to the portable FinalTerm-style core:

```text
OSC 133 ; A ST
OSC 133 ; B ST
OSC 133 ; C ST
OSC 133 ; D ; status ST
OSC 133 ; D ST
```

0.15 SHALL NOT change the existing byte sequences or semantics of those overloads.

The current core remains intentionally stateless from the caller's perspective: markers are independently callable, `TerminalSession` does not impose a shell command-region state machine, successful completion proves emission rather than terminal support, and ordinary output ordering remains protected by the session output serialization domain.

---

## 3. Reference extensions targeted by 0.15

Current Kitty shell integration documents OSC 133 extensions on `A` and `C`:

```text
OSC 133 ; A ; redraw=0 ST
OSC 133 ; A ; special_key=1 ST
OSC 133 ; A ; k=s ST
OSC 133 ; A ; click_events=1 ST
OSC 133 ; A ; click_events=2 ST
OSC 133 ; C ; cmdline=<shell-%q-encoded-text> ST
OSC 133 ; C ; cmdline_url=<UTF-8-percent-escaped-text> ST
```

Contour independently documents:

```text
OSC 133 ; A ; click_events=1 ST
OSC 133 ; C ; cmdline_url=<percent-encoded-command-line> ST
```

Reference material:

- Kitty shell integration: https://sw.kovidgoyal.net/kitty/shell-integration/
- Contour OSC 133 shell integration: https://contour-terminal.org/vt-extensions/osc-133-shell-integration/

### Interoperability tiers

0.15 SHALL document extended metadata by interoperability tier rather than by terminal-brand inference.

**Portable core:**

- bare `A`, `B`, `C`, `D;status`, `D` — retained from 0.12.

**Cross-terminal extended metadata:**

- `C;cmdline_url=...` — documented by Kitty and Contour and therefore the preferred semantic command-line transport;
- `A;click_events=1` — documented by Kitty and Contour, though exact terminal behavior remains implementation-specific.

**Kitty-family extended metadata:**

- `A;redraw=0`;
- `A;special_key=1`;
- `A;k=s`;
- `A;click_events=2`;
- `C;cmdline=...` using shell `%q` encoding.

The library SHALL NOT claim support merely because a terminal is Kitty, Contour, WezTerm, iTerm2, or another named implementation. Emission remains explicit caller intent.

---

## 4. Design principles for the public 0.15 API

### Semantic API, not raw key/value bags

0.15 SHOULD expose typed semantic options rather than `IReadOnlyDictionary<string,string>` or arbitrary OSC 133 parameter text. The public API must prevent malformed delimiters/control bytes from becoming a generic raw-protocol escape hatch.

### Preserve old overloads

The existing 0.12 overloads remain the simplest portable path. Extended metadata should arrive through new overloads/types, not by changing existing behavior.

### Prefer `cmdline_url` over shell `%q`

The semantic command-line API SHOULD accept ordinary .NET `string` command text and encode it as UTF-8 percent-escaped `cmdline_url` internally.

Reasons:

- it is documented by both Kitty and Contour;
- it is independent of Bash/Zsh `%q` details;
- it avoids exposing shell-specific escaping policy in the core terminal library;
- it gives deterministic byte encoding from Unicode input.

`cmdline=...` MAY remain unexposed publicly unless a compelling cross-shell semantic use case emerges during T150. If implemented at all, it should be an internal/specialized writer with strict documented encoding, not a caller-supplied pre-escaped string.

### Prompt metadata should model meaning

Candidate public semantic model for T150 review:

```csharp
public enum TerminalSemanticPromptKind {
	Primary,
	Secondary
}

public enum TerminalSemanticPromptClickEvents {
	None,
	Absolute,
	Relative
}

public readonly struct TerminalSemanticPromptOptions {
	public TerminalSemanticPromptKind Kind { get; }
	public bool? RedrawOnResize { get; }
	public bool UseSpecialCursorKey { get; }
	public TerminalSemanticPromptClickEvents ClickEvents { get; }
}
```

This shape is illustrative, not yet frozen. T150 should specifically determine whether nullable redraw semantics are worthwhile or whether an explicit enum better distinguishes "unspecified/default" from `redraw=0`.

Potential new overload:

```csharp
ValueTask BeginPromptAsync(
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken = default
);
```

Potential command-output overload:

```csharp
ValueTask BeginCommandOutputAsync(
	string commandLine,
	CancellationToken cancellationToken = default
);
```

The command-line overload would emit `C;cmdline_url=...` and retain the existing parameterless `BeginCommandOutputAsync()` unchanged.

---

## 5. Encoding and security invariants

T150/T151 SHALL freeze exact parameter grammar before public API implementation.

Required invariants:

- OSC payloads use the existing OSC 133 writer and session output serialization;
- outbound OSC terminates with ST;
- callers cannot inject `;`, BEL, ST, ESC, or a second OSC frame through metadata values;
- command-line metadata is converted from .NET UTF-16 strings to UTF-8 bytes before percent encoding;
- percent encoding is byte-based, deterministic, and uses one canonical hex case;
- control characters and non-ASCII bytes are encoded rather than emitted raw in `cmdline_url`;
- parameter names and allowed values are library-owned constants;
- duplicate/conflicting prompt parameters cannot be represented by the typed public model;
- all output is fully encoded/validated before commitment;
- cancellation before output commitment emits nothing;
- once transmission is committed, the complete frame is written under the existing output serialization contract;
- a practical finite metadata payload bound is frozen and tested before public release.

The payload bound should be chosen deliberately in T150. It must be large enough for realistic command lines while preventing accidental multi-megabyte OSC frames from ordinary API calls.

---

## 6. Prompt-start metadata semantics to freeze

### Primary vs secondary prompt

`k=s` identifies a secondary/PS2 prompt. The absence of `k=s` remains the primary/default prompt.

The public model should represent this semantically rather than exposing the literal `k` key or `s` value.

### Redraw behavior

Kitty defines `redraw=0` as "the shell will not redraw the prompt on resize, so the terminal should not erase it expecting a redraw."

This is inverted relative to a likely .NET property name. T150 SHALL choose naming that avoids Boolean ambiguity. An enum such as:

```csharp
public enum TerminalSemanticPromptResizeBehavior {
	Unspecified,
	ShellRedrawsPrompt,
	ShellDoesNotRedrawPrompt
}
```

may be preferable even if only `ShellDoesNotRedrawPrompt` produces an explicit 0.15 parameter today.

### Special cursor key

`special_key=1` tells Kitty to use its special cursor-motion key rather than synthetic arrow keys when handling prompt clicks. This is a caller-declared shell-integration capability, not a terminal capability probe.

### Click events

Kitty documents:

- `click_events=1` — absolute Y coordinates;
- `click_events=2` — Y coordinates relative to the current prompt.

Contour currently documents `click_events=1`.

The semantic model should therefore distinguish `None`, `Absolute`, and `Relative`, while documentation identifies `Relative` as the narrower interoperability tier.

No automatic mouse protocol or keyboard binding should be enabled by `Icod.Terminal`; the marker only publishes shell capability metadata.

---

## 7. Command-line metadata semantics to freeze

The preferred public representation is raw command-line text supplied as a .NET `string` and encoded internally as `cmdline_url`.

The library SHALL NOT:

- parse shell syntax;
- normalize quotes/whitespace;
- redact secrets automatically;
- infer whether a command is sensitive;
- inspect shell history;
- capture process command lines automatically;
- emit command-line metadata unless the caller explicitly supplies it.

Documentation MUST warn that command-line metadata can expose secrets present directly in command arguments. This is a semantic publication API, not a safe secret-redaction layer.

T150 SHALL decide whether the public API should use a distinct value type such as `TerminalSemanticCommandMetadata` rather than a direct string overload. A value type may leave cleaner room for future portable metadata without overload proliferation.

---

## 8. Lifecycle and ordering contract

Extended OSC 133 metadata remains ephemeral output metadata, not restorable presentation state.

0.15 SHALL retain these 0.12 invariants:

- no session-open auto-emission;
- no background OSC 133 listener;
- no terminal support cache;
- no restore/reset emission during `InvalidateState()`;
- no semantic marker replay merely because of managed suspend/resume;
- no implicit command-region state machine;
- no lifecycle ownership lease for markers;
- session disposal does not synthesize missing `D` markers;
- extended markers compose with ordinary application output using the existing session output serialization domain.

If a caller wants to re-publish semantic shell state after a suspend/resume boundary, the caller remains responsible for doing so at an appropriate application layer.

---

## 9. Tranche sequence

### T150 — OSC 133 extended-metadata contract and reference freeze

**Version:** `0.15.0-alpha.1`.

Freeze:

- exact supported `A` and `C` parameters;
- interoperability tiers;
- semantic public type names/shapes;
- `cmdline_url` as preferred command-line representation;
- UTF-8 percent-encoding rules;
- metadata size bound;
- validation/error model;
- privacy/security documentation;
- explicit non-goals.

Deliver `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

### T151 — parameter encoder and byte-exact writer foundation

**Expected version:** `0.15.0-alpha.2`.

Implement internal bounded OSC 133 parameter encoding without widening the generic OSC API.

Prove:

- exact bytes for every supported parameter;
- deterministic parameter order;
- UTF-8 percent encoding;
- Unicode/non-BMP command lines;
- delimiters/control characters cannot inject protocol;
- payload-bound edges;
- cancellation-before-commit behavior.

### T152 — prompt-start extended metadata

**Expected version:** `0.15.0-alpha.3`.

Implement typed `A` metadata for:

- primary/secondary prompt distinction;
- redraw behavior;
- special cursor key declaration;
- absolute/relative click event modes.

Retain parameterless `BeginPromptAsync()` byte-for-byte.

### T153 — command-line metadata

**Expected version:** `0.15.0-alpha.4`.

Implement semantic `C;cmdline_url=...` publication.

Prove:

- empty vs non-empty command line policy;
- spaces/quotes/metacharacters;
- ASCII/Unicode/non-BMP input;
- CR/LF/control characters encoded rather than emitted raw;
- long command line boundary;
- no automatic command capture;
- no mutation of caller text.

Decide definitively whether `%q` `cmdline=` remains internal-only or is excluded entirely from 0.15.

### T154 — session API integration and compatibility

**Expected version:** `0.15.0-alpha.5`.

Prove old and new APIs compose with:

- ordinary text output;
- synchronized output;
- progress;
- pointer shape;
- scoped colors;
- hyperlinks;
- current-location publication;
- clipboard operations;
- active queries/input routing.

Existing 0.12 OSC 133 API byte sequences MUST remain unchanged.

### T155 — lifecycle, failure, ordering, and security hardening

**Expected version:** `0.15.0-alpha.6`.

Cover:

- cancellation before frame commitment;
- output failure;
- concurrent marker calls;
- lifecycle suspension while a marker is queued;
- invalidation;
- session disposal;
- no synthetic/replayed semantic metadata;
- injection attempts;
- oversized metadata;
- malformed enum/value inputs;
- no deadlock with output/query/lifecycle serialization.

### T156 — downstream acceptance

**Expected version:** `0.15.0-alpha.7`.

Extend the real `Icod.DCurses` acceptance layer to prove extended OSC 133 metadata can coexist with a higher-level full-screen consumer without requiring raw OSC APIs or a second output path.

Acceptance should focus on public semantic APIs and output ordering, not terminal-brand emulation.

### T157 — public API/package/stable closure

**Stable version:** `0.15.0`.

Deliver:

- `docs/Public-API-Baseline-0.15.md`;
- README OSC 133 metadata examples and privacy warning;
- focused semantic-prompt sample update;
- XML documentation assertions for every new public type/member;
- fresh NuGet-only 0.15 consumer on net8/net9/net10;
- retained 0.8–0.14 package gates;
- retained downstream acceptance gates;
- 0.15 package contract integrated into PR, main/distribution, and tag/release validation;
- stable release notes/tags;
- exact-head PR/main/tag validation.

---

## 10. Testing matrix

0.15 SHALL retain all existing tests and add deterministic coverage for:

- bare 0.12 `A/B/C/D` compatibility;
- each supported `A` parameter individually;
- legal parameter combinations;
- canonical parameter ordering;
- secondary prompt `k=s`;
- click events 1 and 2;
- redraw semantics without Boolean inversion;
- special-key declaration;
- `cmdline_url` percent encoding of ASCII and Unicode;
- percent, semicolon, equals, BEL, ESC, ST bytes, CR/LF, NUL, tabs, shell metacharacters, quotes, and backslashes;
- empty command-line semantics;
- exact maximum-boundary payload and one-byte-over rejection;
- cancellation/output failure/concurrency;
- composition with other session output managers;
- Windows/Linux/macOS CI;
- net8/net9/net10 fresh-package consumers.

The tests SHALL validate emitted bytes directly; success must not depend on the CI host terminal supporting OSC 133 metadata.

---

## 11. Explicit non-goals

0.15 SHALL NOT add:

- OSC 3008 hierarchical context signaling;
- OSC 9 notification/CWD/tab extensions;
- modern keyboard negotiation;
- terminal-brand auto-detection as a support oracle;
- shell auto-injection/configuration;
- automatic shell-history inspection;
- command parsing;
- secret detection/redaction;
- automatic command capture;
- a raw OSC 133 key/value API;
- generic public OSC/CSI/DCS builders;
- marker lifecycle leases;
- marker replay on resume;
- shell process hosting;
- PTY/ConPTY;
- graphics protocols.

OSC 3008 is interesting, but it is a distinct nested-context protocol with different semantics and should be evaluated separately rather than silently folded into OSC 133 metadata work.

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

**Next:** T150 — freeze the exact OSC 133 extended metadata public contract before implementation.
