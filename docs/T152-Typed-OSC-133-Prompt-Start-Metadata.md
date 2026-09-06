# T152 — Typed OSC 133 Prompt-Start Metadata

**Release:** `Icod.Terminal 0.15.0`  
**Version:** `0.15.0-alpha.3`  
**Status:** Implemented; exact-head validation pending

## Purpose

T152 exposes the prompt-start portion of the T150 OSC 133 extended-metadata contract through typed public APIs while preserving the existing portable `BeginPromptAsync()` behavior byte-for-byte.

No command-line metadata is added in this tranche; that remains T153.

## Public types

T152 adds:

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

The default struct value is deliberately valid and represents the portable primary prompt with no extended metadata.

Undefined enum values are rejected with `ArgumentOutOfRangeException`.

## Public session API

T152 adds:

```csharp
ValueTask BeginPromptAsync(
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken = default
);
```

The existing overload remains unchanged:

```csharp
ValueTask BeginPromptAsync(
	CancellationToken cancellationToken = default
);
```

`BeginPromptAsync(default(TerminalSemanticPromptOptions))` emits exactly the same bytes as the existing parameterless overload:

```text
ESC ] 133 ; A ESC \
```

## Semantic-to-wire mapping

The typed fields map only to the forms frozen in T150:

| Semantic value | Wire metadata |
| --- | --- |
| `Primary` | omitted |
| `Secondary` | `k=s` |
| `Unspecified` resize behavior | omitted |
| `ShellDoesNotRedrawPrompt` | `redraw=0` |
| `UseSpecialCursorKey == false` | omitted |
| `UseSpecialCursorKey == true` | `special_key=1` |
| `ClickMode.None` | omitted |
| `ClickMode.Absolute` | `click_events=1` |
| `ClickMode.Relative` | `click_events=2` |

No inverse forms such as `redraw=1` or `special_key=0` are invented.

## Canonical ordering

When multiple fields are represented, T151's frozen writer order is retained:

```text
redraw=0
special_key=1
k=s
click_events=1|2
```

For example:

```text
ESC ] 133 ; A ; redraw=0 ; special_key=1 ; k=s ; click_events=2 ESC \
```

## Interoperability semantics

`click_events=1` is the broader cross-terminal extended form documented by Kitty and Contour.

`redraw=0`, `special_key=1`, `k=s`, and `click_events=2` remain the narrower Kitty-documented tier.

The API does not infer terminal support from brand/identity and does not auto-probe before emission. Supplying options is explicit caller intent.

The metadata only publishes shell-integration semantics. It does not enable a mouse mode, bind keys, or implement prompt-click handling.

## Session integration

The new overload reuses the same `TerminalSession` output serialization domain as the portable OSC 133 methods.

It retains the existing semantics:

- interactive terminal output is required;
- cancellation before output commitment emits nothing;
- committed frames are written as one non-cancellable complete write;
- no flush is implied;
- no command-region state machine is introduced;
- no lifecycle lease or resume replay is introduced;
- successful emission does not prove terminal support.

## Tests

T152 adds focused tests for:

- default options/property values;
- undefined prompt-kind rejection;
- undefined resize-behavior rejection;
- undefined click-mode rejection;
- default-options byte identity with bare `A`;
- canonical combined prompt metadata;
- absolute and relative click-mode wire values;
- pre-cancelled public operation emits nothing;
- committed output uses the existing non-cancellable write semantics.

Existing T124/T121 portable OSC 133 tests remain unchanged and continue to protect the 0.12 public/wire contract.

## Non-goals

T152 does not add:

- command-line metadata;
- `%q` command-line encoding;
- arbitrary OSC 133 parameters;
- support probing;
- terminal-brand dispatch;
- mouse/key configuration;
- lifecycle replay;
- a command-region state machine.

T153 is next and will add the frozen typed `C;cmdline_url=...` command-output metadata contract.
