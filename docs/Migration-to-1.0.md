# Migration to Icod.Terminal 1.0

This document describes migration from the final pre-1.0 line (`0.18.0`) to stable `1.0.0`.

The migration is intentionally small. The 1.0 program is primarily contract freeze, permanent documentation, compatibility proof, and stable release closure rather than a new protocol release.

## 1. Package update

Change the package reference from the final pre-1.0 release to stable 1.0:

```xml
<PackageReference Include="Icod.Terminal" Version="1.0.0" />
```

The supported target frameworks remain:

```text
net8.0
net9.0
net10.0
```

The public package continues to depend on the same live-terminal architecture: `Icod.TermInfo` below it and higher-level consumers such as `Icod.DCurses` above it.

## 2. The one intentional breaking API correction

The sole D-class pre-1.0 correction is removal of the public `TerminalSession.Input` property.

Before 1.0, code could obtain the raw input transport from a live session. That cannot coexist safely with the session's authoritative incremental decoder and query router because a competing read can steal bytes belonging to UTF-8 scalars, escape sequences, paste/mouse/focus reports, Kitty keyboard frames, or active query responses.

The public `ITerminalInput` interface is **not** removed. It remains the custom-transport injection seam when opening a session.

### If you used `session.Input.ReadAsync(...)` for application input

Replace the raw read with the session event loop:

```csharp
TerminalEvent terminalEvent = await session.ReadEventAsync(
	cancellationToken
);
```

Then inspect `terminalEvent.Kind` and the typed input event.

For bounded waits, use the `TimeSpan` or deadline overloads of `ReadEventAsync(...)` rather than implementing a second reader around the transport.

### If you used `session.Input` to read terminal query responses

Use the typed query operation that owns request/response correlation, for example:

```csharp
TerminalCursorPosition position = await session.QueryCursorPositionAsync(
	TimeSpan.FromSeconds( 1 ),
	cancellationToken
);
```

Other typed operations cover device attributes, status reports, DECRQSS, XTGETTCAP, cursor style, pointer shape, clipboard reads, colors, and the other supported query families.

Do not recreate a raw response parser beside `TerminalSession`.

### If you supplied the input transport yourself

This remains valid:

```csharp
TerminalSession session = await TerminalSession.OpenAsync(
	controlProvider,
	inputEndpoint,
	outputEndpoint,
	input,
	output,
	options,
	cancellationToken
);
```

You still own the lifetime of `input`. Retaining the reference for disposal/host lifecycle is fine; reading it concurrently while the live `TerminalSession` owns that same terminal conversation is not.

## 3. `TerminalSession.Output` remains available

The raw output transport remains public for advanced integrations.

However, direct calls to `session.Output` are outside the session's normal serialization boundary. They can interleave with query requests, lifecycle traffic, semantic output, presentation transitions, or rich-input protocol transitions.

Ordinary applications should prefer session-managed operations such as:

- `WriteTextAsync(...)`;
- semantic title/location/hyperlink/clipboard/prompt/notification operations;
- presentation and input-protocol leases;
- typed query APIs.

`WriteTerminalStringAsync(...)` remains the appropriate advanced path for already-resolved terminfo capability strings; it is not a recommendation to construct arbitrary escape sequences manually.

## 4. No protocol migration is required

All semantic terminal protocol APIs present in 0.18 remain available unless affected by the single raw-input correction above.

Stable 1.0 does not replace one protocol family with another.

In particular:

- OSC 7 remains the preferred portable current-location API;
- OSC 9;9 remains explicitly secondary Windows/ConEmu compatibility;
- OSC 133 remains the semantic prompt/command-region API;
- safe OSC 9 remains notification + progress + Windows-CWD compatibility only;
- modern keyboard support remains negotiated and opt-in;
- xterm `modifyOtherKeys` remains decode-only;
- raw hazardous OSC 9 vendor commands remain inaccessible.

## 5. Input and query behavior to preserve

Applications upgrading to 1.0 should retain one authoritative `TerminalSession` event/query path.

Do not mix a session with competing `Console.Read*`, `Stream.Read*`, or retained custom-transport reads on the same terminal input while the session is live.

The 1.x contract preserves:

- incremental UTF-8/escape decoding;
- caller wait cancellation without discarding partial decoder state;
- bounded paste/input buffering;
- active query correlation;
- pre-emission versus post-emission cancellation semantics;
- bounded late-response ownership;
- lifecycle query generations and resume observation ordering.

## 6. Reversible terminal state

Existing lease-based code should continue to use `await using` / `DisposeAsync()`.

The 1.x documentation makes several distinctions explicit which were previously spread across release records:

- exact restoration uses a captured/observed baseline;
- terminal-policy reset does not claim exact restoration;
- some Icod-owned nested state can restore an outer Icod owner without knowing the pre-Icod external state;
- ephemeral metadata has no lifecycle restoration/replay contract.

Do not replace a documented exact-restoration lease with a reset command merely because both visually appear to “undo” a setting.

## 7. Lifecycle and teardown

New presentation/rich-input state acquisition is rejected while lifecycle has released terminal state or session disposal has begun.

Existing cleanup remains authoritative. Applications should therefore:

- dispose leases when their logical scope ends;
- let `TerminalSession.DisposeAsync()` remain final restoration authority;
- avoid acquiring new terminal state from shutdown/suspend cleanup paths;
- treat `IsStateValid == false` as an indication that previously believed physical state is no longer authoritative.

## 8. Direct TerminalSession vs Icod.DCurses

Use `TerminalSession` directly for applications that need terminal modes, event decoding, semantic terminal operations, queries, and scoped terminal state without a curses-style virtual screen.

Use `Icod.DCurses` for applications that need windows, cells, virtual-screen diff/refresh, or curses presentation semantics.

`Icod.DCurses` may take ownership of a supplied `TerminalSession` after successful initialization according to its integration contract. When ownership transfers, disposing the `CursesSession` also disposes that terminal session.

Do not keep two independent state-owning `TerminalSession` instances over the same physical terminal as a substitute for layering.

## 9. Target frameworks and platforms

No TFM migration is required from 0.18:

```text
net8.0
net9.0
net10.0
```

The built-in system provider supports Windows, Linux, and macOS.

On another operating system, the built-in provider returns controlled `Unsupported` results. Custom providers/transports may be supplied through the public injection interfaces where appropriate.

## 10. Public enum values

The stable 1.0 machine baseline freezes the current public enum numeric layout for the 1.x line.

Applications should not depend on superseded numeric layouts from early pre-1.0 development documents. The authoritative stable values are the values compiled into the `1.0.0` package and recorded by the stable 1.0 public-API fingerprint.

Existing values will not be renumbered within ordinary 1.x releases.

## 11. Failure/result semantics

Do not collapse distinct failure classes during migration.

The 1.x APIs intentionally distinguish cases such as:

- unsupported operation;
- unavailable operation in the current environment/state;
- caller cancellation;
- timeout;
- malformed correlated terminal response;
- transport/output failure;
- invalid lifecycle/state acquisition;
- rollback/restoration failure.

A query timeout should not be translated automatically to “terminal does not support this feature.”

## 12. Recommended upgrade checklist

For a typical 0.18 consumer:

1. update the package reference to `1.0.0`;
2. compile on every TFM you ship;
3. search for `session.Input` / `TerminalSession.Input`;
4. replace raw live-session input reads with `ReadEventAsync(...)` or typed query APIs;
5. search for direct `session.Output` writes and confirm they are genuinely advanced/caller-synchronized use;
6. retain `await using` for state leases;
7. run lifecycle/disposal paths, not just happy-path terminal output;
8. test on each operating system you claim to support;
9. if using `Icod.DCurses`, validate its terminal-session ownership path rather than duplicating terminal control in the application.

If none of your code used the removed raw input property, migration from 0.18 should generally be a package-version change plus normal regression testing.

## 13. Release-candidate history

`1.0.0-rc1` qualified the same public API/behavioral contract before stable promotion. Stable `1.0.0` intentionally does not introduce a new feature or API delta relative to that candidate.

The rc1 roadmap, T-series records, and `Public-API-Baseline-1.0-rc1.*` files remain historical qualification evidence.

## 14. Historical documentation

The `0.x` roadmaps and T-series documents remain useful as design evidence, protocol references, and exact test history.

They are no longer required reading to understand the supported 1.x behavior. Prefer the permanent documentation set beginning with:

- `Architecture.md`;
- `Terminal-Session-and-Ownership.md`;
- `Lifecycle-and-Restoration.md`;
- `Input-and-Events.md`;
- `Queries-and-Responses.md`;
- `Presentation-and-Reversible-State.md`;
- `Semantic-Output-Protocols.md`;
- `Security-and-Privacy.md`;
- `Public-API-Baseline-1.0.md`;
- `Compatibility-and-Versioning.md`.
