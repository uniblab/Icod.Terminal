# T156 — Icod.DCurses Extended OSC 133 Downstream Acceptance

**Release line:** `0.15.0`  
**Version:** `0.15.0-alpha.7`  
**Status:** Implemented; exact-head validation pending

## Purpose

T156 proves that the new public OSC 133 extended semantic-metadata APIs can be used by a real higher-level `Icod.DCurses` full-screen consumer without bypassing `TerminalSession`, introducing a raw OSC path, or interfering with DCurses refresh output.

## Acceptance surface

The existing downstream project remains:

`tools/dcurses-semantic-prompt-acceptance/Icod.Terminal.DCursesSemanticPromptAcceptance.csproj`

It targets:

```text
net8.0
net9.0
net10.0
```

and references the repository `Icod.Terminal` project plus the published `Icod.DCurses` package.

The existing verifier remains:

`packaging/VerifyDCursesSemanticPrompt.ps1`

No second downstream verifier was created.

## Retained portable acceptance

The pre-0.15 acceptance sequence remains intact and still proves the portable OSC 133 contract:

```text
A
B
C
D;0
A
B
D
```

DCurses `RefreshAsync()` payloads must occur between semantic boundaries.

## New 0.15 extended acceptance

T156 appends a second sequence using only the public typed 0.15 APIs.

Prompt options:

```csharp
new TerminalSemanticPromptOptions(
	TerminalSemanticPromptKind.Secondary,
	TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt,
	true,
	TerminalSemanticPromptClickMode.Relative
)
```

Expected frame:

```text
OSC 133;A;redraw=0;special_key=1;k=s;click_events=2 ST
```

Command-output options:

```csharp
new TerminalSemanticCommandOutputOptions(
	"printf café 😀"
)
```

Expected frame:

```text
OSC 133;C;cmdline_url=printf%20caf%C3%A9%20%F0%9F%98%80 ST
```

The acceptance sequence requires:

1. typed extended prompt marker;
2. DCurses refresh payload;
3. portable `B` command-input marker;
4. DCurses refresh payload;
5. typed `cmdline_url` command-output marker;
6. DCurses refresh payload;
7. `D;23` completion marker.

## Ownership and disposal

`CursesSession.OpenAsync(TerminalSession, ...)` receives the existing `TerminalSession` and uses it as the shared terminal/output authority. T156 intentionally performs all OSC 133 calls through that same public `TerminalSession` while the `CursesSession` is active.

No second writer, OSC builder, terminal-brand detector, or side-channel output path is introduced.

T156 does not alter the established `CursesSession` ownership/disposal contract.

## What T156 proves

- the 0.15 prompt options API is consumable by a real DCurses host;
- the 0.15 command-output options API is consumable by a real DCurses host;
- Unicode command lines produce the frozen strict UTF-8 percent-encoded wire form;
- DCurses refresh traffic remains ordered between OSC 133 semantic markers;
- the portable 0.12 OSC 133 sequence remains accepted in the same executable;
- the extended APIs require no raw OSC surface;
- net8/net9/net10 downstream compatibility remains part of the verifier.

## Scope exclusions

T156 does not claim terminal-emulator recognition of OSC 133 extended parameters. It proves library/downstream composition and byte ordering only.

T156 adds no new public `Icod.Terminal` API or protocol form.

## Next

After exact-head validation, T157 performs public API/package/documentation/stable release closure for `0.15.0`.
